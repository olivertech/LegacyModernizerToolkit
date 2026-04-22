namespace LegacyModernizer.Generation.Parsing;

public sealed class ApiGroupingService : IApiGroupingService
{
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

    private static async Task<IReadOnlyCollection<ApiGroupDefinition>> ExtractFromJsonAsync(string specificationPath,
                                                                                            CancellationToken cancellationToken)
    {
        var content = await File.ReadAllTextAsync(specificationPath, cancellationToken);

        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        if (!root.TryGetProperty("paths", out var pathsElement) || pathsElement.ValueKind != JsonValueKind.Object)
            return Array.Empty<ApiGroupDefinition>();

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
                    OperationId = ExtractOperationId(operation)
                };

                group.Endpoints.Add(endpoint);
            }
        }

        return groups.Values
            .OrderBy(g => g.Name)
            .ToArray();
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

                var tagValue = tag.GetString();
                var normalizedTag = NormalizeGroupName(tagValue);

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

        var firstSegment = segments.FirstOrDefault();

        return NormalizeGroupName(firstSegment);
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

    private static string NormalizeGroupName(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        var cleaned = new string(rawValue
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }
}
