namespace LegacyModernizer.Generation.Parsing;

/// <summary>
/// Traduz o bloco <c>paths</c> do OpenAPI em grupos de endpoints úteis para a composição da solução.
/// </summary>
public sealed class ApiGroupingService : IApiGroupingService
{
    /// <summary>
    /// Extrai os grupos de API a partir de uma specification já validada.
    /// </summary>
    public async Task<IReadOnlyCollection<ApiGroupDefinition>> GetGroupsAsync(ApiSpecification specification,
                                                                              CancellationToken cancellationToken = default)
    {
        if (specification is null)
            throw new ArgumentNullException(nameof(specification));

        if (specification.ValidationStatus != SpecificationValidationStatus.Valid)
            throw new InvalidOperationException("Specification must be valid before extracting API groups.");

        if (string.IsNullOrWhiteSpace(specification.LocalPath))
            throw new InvalidOperationException("Specification local path was not defined.");

        if (!File.Exists(specification.LocalPath))
            throw new FileNotFoundException("Specification file was not found.", specification.LocalPath);

        return specification.Format switch
        {
            SpecificationFormat.Json => await ExtractFromJsonAsync(specification.LocalPath, cancellationToken),
            SpecificationFormat.Yaml => throw new NotSupportedException("YAML group extraction is not supported yet."),
            _ => throw new InvalidOperationException("Unsupported specification format for group extraction.")
        };
    }

    private static async Task<IReadOnlyCollection<ApiGroupDefinition>> ExtractFromJsonAsync(string specificationPath, CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(specificationPath, cancellationToken);

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var globalSecurityRequired = HasGlobalSecurity(root);

        if (!root.TryGetProperty("paths", out var pathsElement) ||
            pathsElement.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<ApiGroupDefinition>();
        }

        var groups = new Dictionary<string, ApiGroupDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var pathProperty in pathsElement.EnumerateObject())
        {
            var path = pathProperty.Name;
            var pathItem = pathProperty.Value;

            if (pathItem.ValueKind != JsonValueKind.Object)
                continue;

            foreach (var operationProperty in pathItem.EnumerateObject())
            {
                if (!IsHttpVerb(operationProperty.Name))
                    continue;

                var operation = operationProperty.Value;
                var groupName = ExtractGroupName(operation, path);

                if (string.IsNullOrWhiteSpace(groupName))
                    continue;

                if (!groups.TryGetValue(groupName, out var group))
                {
                    group = new ApiGroupDefinition
                    {
                        Name = groupName
                    };

                    groups[groupName] = group;
                }

                var endpoint = new ApiEndpointDefinition
                {
                    Path = path,
                    Method = operationProperty.Name.ToUpperInvariant(),
                    OperationId = ExtractOperationId(operation),
                    // Parâmetros podem existir no path item e também na operação.
                    // O merge posterior preserva a versão mais completa da assinatura.
                    Parameters = ExtractParameters(root, pathItem, operation),
                    HasRequestBody = HasRequestBody(operation),
                    RequiresAuthorization = RequiresAuthorization(operation, globalSecurityRequired)
                };

                if (!ContainsEndpoint(group, endpoint))
                    group.Endpoints.Add(endpoint);
            }
        }

        return groups.Values
            .OrderBy(g => g.Name)
            .ToArray();
    }

    private static string ExtractGroupName(JsonElement operation, string path)
    {
        if (operation.ValueKind == JsonValueKind.Object &&
            operation.TryGetProperty("tags", out var tagsElement) &&
            tagsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsElement.EnumerateArray())
            {
                if (tag.ValueKind != JsonValueKind.String)
                    continue;

                var normalizedTag = NormalizeGroupName(tag.GetString());

                if (!string.IsNullOrWhiteSpace(normalizedTag))
                    return normalizedTag;
            }
        }

        return ExtractGroupNameFromPath(path);
    }

    private static string ExtractGroupNameFromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => !IsVersionSegment(segment))
            .Where(segment => !segment.StartsWith("{") && !segment.EndsWith("}"))
            .ToArray();

        return NormalizeGroupName(segments.FirstOrDefault());
    }

    private static string ExtractOperationId(JsonElement operation)
    {
        if (operation.ValueKind == JsonValueKind.Object &&
            operation.TryGetProperty("operationId", out var operationIdElement) &&
            operationIdElement.ValueKind == JsonValueKind.String)
        {
            return operationIdElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static List<ApiParameterDefinition> ExtractParameters(JsonElement root, JsonElement pathItem, JsonElement operation)
    {
        var parameters = new List<ApiParameterDefinition>();

        AddParametersFromElement(root, pathItem, parameters);
        AddParametersFromElement(root, operation, parameters);

        return parameters
            .GroupBy(x => $"{x.Location}:{x.Name}", StringComparer.OrdinalIgnoreCase)
            // Em specs reais, o mesmo parâmetro pode aparecer repetido com níveis diferentes de detalhe.
            .Select(g => g.Aggregate(MergeParameterDefinitions))
            .OrderBy(x => x.Location)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static void AddParametersFromElement(JsonElement root, JsonElement element, List<ApiParameterDefinition> parameters)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        if (!element.TryGetProperty("parameters", out var parametersElement) ||
            parametersElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var parameter in parametersElement.EnumerateArray())
        {
            var resolvedParameter = ResolveParameterReference(root, parameter);
            if (resolvedParameter is null)
                continue;

            var parameterObject = resolvedParameter.Value;

            var name = parameterObject.TryGetProperty("name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;

            var location = parameterObject.TryGetProperty("in", out var inElement)
                ? inElement.GetString() ?? string.Empty
                : string.Empty;

            var required = parameterObject.TryGetProperty("required", out var requiredElement) &&
                           requiredElement.ValueKind == JsonValueKind.True;

            var schemaType = string.Empty;
            var schemaFormat = string.Empty;

            if (parameterObject.TryGetProperty("schema", out var schemaElement) &&
                schemaElement.ValueKind == JsonValueKind.Object)
            {
                (schemaType, schemaFormat) = ExtractSchemaTypeInfo(root, schemaElement);
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(location))
                continue;

            parameters.Add(new ApiParameterDefinition
            {
                Name = name,
                Location = location,
                Required = required,
                SchemaType = schemaType,
                SchemaFormat = schemaFormat
            });
        }
    }

    private static JsonElement? ResolveParameterReference(JsonElement root, JsonElement parameter)
    {
        if (parameter.ValueKind != JsonValueKind.Object)
            return null;

        if (!parameter.TryGetProperty("$ref", out var refElement) ||
            refElement.ValueKind != JsonValueKind.String)
        {
            return parameter;
        }

        var reference = refElement.GetString();
        if (string.IsNullOrWhiteSpace(reference) ||
            !reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        var current = root;
        var segments = reference[2..].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var rawSegment in segments)
        {
            var segment = rawSegment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.Object
            ? current
            : null;
    }

    private static (string Type, string Format) ExtractSchemaTypeInfo(JsonElement root, JsonElement schemaElement)
    {
        if (schemaElement.ValueKind != JsonValueKind.Object)
            return (string.Empty, string.Empty);

        var type = schemaElement.TryGetProperty("type", out var typeElement) &&
                   typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString() ?? string.Empty
            : string.Empty;

        var format = schemaElement.TryGetProperty("format", out var formatElement) &&
                     formatElement.ValueKind == JsonValueKind.String
            ? formatElement.GetString() ?? string.Empty
            : string.Empty;

        if (!string.IsNullOrWhiteSpace(type))
            return (type, format);

        if (schemaElement.TryGetProperty("$ref", out var refElement) &&
            refElement.ValueKind == JsonValueKind.String)
        {
            var resolvedSchema = ResolveJsonReference(root, refElement.GetString());

            if (resolvedSchema is not null)
                return ExtractSchemaTypeInfo(root, resolvedSchema.Value);
        }

        foreach (var compositionKeyword in new[] { "allOf", "oneOf", "anyOf" })
        {
            if (!schemaElement.TryGetProperty(compositionKeyword, out var compositionElement) ||
                compositionElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var candidate in compositionElement.EnumerateArray())
            {
                var candidateTypeInfo = ExtractSchemaTypeInfo(root, candidate);

                if (!string.IsNullOrWhiteSpace(candidateTypeInfo.Type))
                    return candidateTypeInfo;
            }
        }

        return (string.Empty, string.Empty);
    }

    private static JsonElement? ResolveJsonReference(JsonElement root, string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference) ||
            !reference.StartsWith("#/", StringComparison.Ordinal))
        {
            return null;
        }

        var current = root;
        var segments = reference[2..].Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var rawSegment in segments)
        {
            var segment = rawSegment
                .Replace("~1", "/", StringComparison.Ordinal)
                .Replace("~0", "~", StringComparison.Ordinal);

            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static ApiParameterDefinition MergeParameterDefinitions(
        ApiParameterDefinition current,
        ApiParameterDefinition next)
    {
        return new ApiParameterDefinition
        {
            Name = !string.IsNullOrWhiteSpace(next.Name) ? next.Name : current.Name,
            Location = !string.IsNullOrWhiteSpace(next.Location) ? next.Location : current.Location,
            Required = current.Required || next.Required,
            SchemaType = !string.IsNullOrWhiteSpace(next.SchemaType) ? next.SchemaType : current.SchemaType,
            SchemaFormat = !string.IsNullOrWhiteSpace(next.SchemaFormat) ? next.SchemaFormat : current.SchemaFormat
        };
    }

    private static bool HasRequestBody(JsonElement operation)
    {
        return operation.ValueKind == JsonValueKind.Object &&
               operation.TryGetProperty("requestBody", out _);
    }

    private static bool HasGlobalSecurity(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("security", out var securityElement) &&
               securityElement.ValueKind == JsonValueKind.Array &&
               securityElement.GetArrayLength() > 0;
    }

    private static bool RequiresAuthorization(JsonElement operation, bool globalSecurityRequired)
    {
        if (operation.ValueKind != JsonValueKind.Object)
            return globalSecurityRequired;

        if (!operation.TryGetProperty("security", out var securityElement))
            return globalSecurityRequired;

        if (securityElement.ValueKind == JsonValueKind.Array &&
            securityElement.GetArrayLength() == 0)
        {
            return false;
        }

        return securityElement.ValueKind == JsonValueKind.Array &&
               securityElement.GetArrayLength() > 0;
    }

    private static bool ContainsEndpoint(ApiGroupDefinition group, ApiEndpointDefinition endpoint)
    {
        return group.Endpoints.Any(existing =>
            existing.Path.Equals(endpoint.Path, StringComparison.OrdinalIgnoreCase) &&
            existing.Method.Equals(endpoint.Method, StringComparison.OrdinalIgnoreCase) &&
            existing.OperationId.Equals(endpoint.OperationId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsHttpVerb(string value)
    {
        return value.Equals("get", StringComparison.OrdinalIgnoreCase)
            || value.Equals("post", StringComparison.OrdinalIgnoreCase)
            || value.Equals("put", StringComparison.OrdinalIgnoreCase)
            || value.Equals("patch", StringComparison.OrdinalIgnoreCase)
            || value.Equals("delete", StringComparison.OrdinalIgnoreCase)
            || value.Equals("head", StringComparison.OrdinalIgnoreCase)
            || value.Equals("options", StringComparison.OrdinalIgnoreCase)
            || value.Equals("trace", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsVersionSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return false;

        segment = segment.Trim();

        if (segment.Length < 2)
            return false;

        if (segment[0] != 'v' && segment[0] != 'V')
            return false;

        return char.IsDigit(segment[1]);
    }

    private static string NormalizeGroupName(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        var parts = rawValue
            .Split(new[] { '-', '_', '.', '/', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => new string(part.Where(char.IsLetterOrDigit).ToArray()))
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Length == 0)
            return string.Empty;

        return string.Concat(parts.Select(part =>
            part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..]));
    }
}
