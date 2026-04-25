using System.Text;

namespace LegacyModernizer.Generation.Kiota;

public sealed class KiotaOutputInspectionService : IKiotaOutputInspectionService
{
    private static readonly Regex NamespaceRegex =
        new(@"namespace\s+([A-Za-z0-9_.]+);", RegexOptions.Compiled);

    private static readonly Regex PropertyRegex =
        new(
            @"public\s+(?<type>(?:global::)?[A-Za-z0-9_.<>,\s\?\[\]]+)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get;\s*set;\s*\}",
            RegexOptions.Compiled | RegexOptions.Singleline);

    private sealed record KiotaMethodSignature(
        string ReturnType,
        string ParametersText);

    public Task<KiotaClientMetadata> InspectAsync(
        GeneratedArtifact generatedClientArtifact,
        IReadOnlyCollection<ApiGroupDefinition> apiGroups,
        CancellationToken cancellationToken = default)
    {
        if (generatedClientArtifact is null)
            throw new ArgumentNullException(nameof(generatedClientArtifact));

        if (apiGroups is null)
            throw new ArgumentNullException(nameof(apiGroups));

        if (generatedClientArtifact.Type != ArtifactType.GeneratedClient)
            throw new InvalidOperationException("The provided artifact is not a generated client artifact.");

        var clientRootPath = generatedClientArtifact.Location.FullPath;

        if (string.IsNullOrWhiteSpace(clientRootPath))
            throw new InvalidOperationException("Generated client artifact path was not defined.");

        if (!Directory.Exists(clientRootPath))
            throw new DirectoryNotFoundException($"Generated client directory was not found: {clientRootPath}");

        cancellationToken.ThrowIfCancellationRequested();

        var allCsFiles = Directory.GetFiles(clientRootPath, "*.cs", SearchOption.AllDirectories);

        if (allCsFiles.Length == 0)
            throw new InvalidOperationException("No C# files were found in the generated client output.");

        var rootNamespace = DetectRootNamespace(allCsFiles);
        var clientClassName = DetectClientClassName(allCsFiles);
        var detectedGroups = DetectGroups(clientRootPath);

        EnrichGroupsWithEndpointOperations(
            clientRootPath,
            detectedGroups,
            apiGroups);

        return Task.FromResult(new KiotaClientMetadata
        {
            RootNamespace = rootNamespace,
            ClientClassName = clientClassName,
            Groups = detectedGroups
        });
    }

    private static void EnrichGroupsWithEndpointOperations(
        string clientRootPath,
        List<KiotaGroupMetadata> detectedGroups,
        IReadOnlyCollection<ApiGroupDefinition> apiGroups)
    {
        foreach (var apiGroup in apiGroups)
        {
            var kiotaGroup = ResolveKiotaGroupForApiGroup(detectedGroups, apiGroup);

            if (kiotaGroup is null)
                continue;

            foreach (var endpoint in apiGroup.Endpoints)
            {
                var builderFile = FindRequestBuilderFileForEndpoint(clientRootPath, endpoint);

                if (string.IsNullOrWhiteSpace(builderFile))
                    continue;

                var operation = ExtractOperationFromBuilderFile(
                    clientRootPath,
                    builderFile,
                    endpoint,
                    kiotaGroup);

                if (operation is null)
                    continue;

                if (!kiotaGroup.Operations.Any(x =>
                        x.HttpMethod.Equals(operation.HttpMethod, StringComparison.OrdinalIgnoreCase) &&
                        x.AccessExpression.Equals(operation.AccessExpression, StringComparison.OrdinalIgnoreCase)))
                {
                    kiotaGroup.Operations.Add(operation);
                }
            }
        }
    }

    private static KiotaGroupMetadata? ResolveKiotaGroupForApiGroup(
        IReadOnlyCollection<KiotaGroupMetadata> detectedGroups,
        ApiGroupDefinition apiGroup)
    {
        var exactMatch = detectedGroups.FirstOrDefault(x =>
            x.GroupName.Equals(apiGroup.Name, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
            return exactMatch;

        var firstEndpoint = apiGroup.Endpoints.FirstOrDefault();

        if (firstEndpoint is null)
            return null;

        var firstBusinessSegment = ExtractFirstBusinessSegment(firstEndpoint.Path);

        if (string.IsNullOrWhiteSpace(firstBusinessSegment))
            return null;

        return detectedGroups.FirstOrDefault(x =>
            x.GroupName.Equals(firstBusinessSegment, StringComparison.OrdinalIgnoreCase));
    }

    private static string DetectRootNamespace(string[] allCsFiles)
    {
        foreach (var file in allCsFiles)
        {
            var content = File.ReadAllText(file);
            var match = NamespaceRegex.Match(content);

            if (!match.Success)
                continue;

            var fullNamespace = match.Groups[1].Value.Trim();

            if (!string.IsNullOrWhiteSpace(fullNamespace))
                return fullNamespace;
        }

        return string.Empty;
    }

    private static string DetectClientClassName(string[] allCsFiles)
    {
        var preferredCandidate = allCsFiles
            .Select(Path.GetFileNameWithoutExtension)
            .FirstOrDefault(name =>
                !string.IsNullOrWhiteSpace(name) &&
                name.EndsWith("Client", StringComparison.OrdinalIgnoreCase));

        return preferredCandidate ?? string.Empty;
    }

    private static List<KiotaGroupMetadata> DetectGroups(string clientRootPath)
    {
        var requestBuilderFiles = Directory.GetFiles(
            clientRootPath,
            "*RequestBuilder.cs",
            SearchOption.AllDirectories);

        var groups = new Dictionary<string, KiotaGroupMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (var requestBuilderFile in requestBuilderFiles)
        {
            var candidate = TryCreateGroupMetadata(clientRootPath, requestBuilderFile);

            if (candidate is null)
                continue;

            if (!groups.ContainsKey(candidate.GroupName))
                groups[candidate.GroupName] = candidate;
        }

        return groups.Values
            .OrderBy(x => x.GroupName)
            .ToList();
    }

    private static KiotaGroupMetadata? TryCreateGroupMetadata(
        string clientRootPath,
        string requestBuilderFile)
    {
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(requestBuilderFile);

        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
            return null;

        if (!fileNameWithoutExtension.EndsWith("RequestBuilder", StringComparison.OrdinalIgnoreCase))
            return null;

        var builderStem = fileNameWithoutExtension[..^"RequestBuilder".Length];

        if (string.IsNullOrWhiteSpace(builderStem))
            return null;

        if (IsIgnoredBuilderName(builderStem))
            return null;

        var relativeDirectory = Path.GetRelativePath(
            clientRootPath,
            Path.GetDirectoryName(requestBuilderFile) ?? string.Empty);

        if (string.IsNullOrWhiteSpace(relativeDirectory) || relativeDirectory == ".")
            return null;

        var directorySegments = SplitRelativePath(relativeDirectory);

        if (directorySegments.Length == 0)
            return null;

        var parentSegment = directorySegments[^1];

        if (!parentSegment.Equals(builderStem, StringComparison.OrdinalIgnoreCase))
            return null;

        if (directorySegments.Length > 2)
            return null;

        var builderAccessExpression = string.Join(
            ".",
            directorySegments.Select(NormalizeIdentifier));

        return new KiotaGroupMetadata
        {
            GroupName = NormalizeIdentifier(builderStem),
            BuilderTypeName = fileNameWithoutExtension,
            BuilderPropertyName = NormalizeIdentifier(builderStem),
            BuilderAccessExpression = builderAccessExpression
        };
    }

    private static string? FindRequestBuilderFileForEndpoint(
    string clientRootPath,
    ApiEndpointDefinition endpoint)
    {
        var pathSegments = endpoint.Path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsVersionSegment(x))
            .Select(x => IsPathParameterSegment(x) ? "Item" : NormalizeIdentifier(x))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (pathSegments.Length == 0)
            return null;

        var builderName = $"{pathSegments[^1]}RequestBuilder.cs";

        var matches = Directory.GetFiles(
            clientRootPath,
            builderName,
            SearchOption.AllDirectories);

        if (matches.Length == 0)
            return null;

        var expectedTail = Path.Combine(pathSegments.Append(builderName).ToArray());

        var exactMatch = matches.FirstOrDefault(file =>
            file.EndsWith(expectedTail, StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
            return exactMatch;

        var normalizedExpectedTail = NormalizePathForComparison(expectedTail);

        var bestMatch = matches
            .OrderByDescending(file =>
            {
                var normalizedFile = NormalizePathForComparison(file);

                if (normalizedFile.EndsWith(normalizedExpectedTail, StringComparison.OrdinalIgnoreCase))
                    return 100;

                var score = 0;

                foreach (var segment in pathSegments)
                {
                    if (normalizedFile.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase))
                        score++;
                }

                return score;
            })
            .FirstOrDefault();

        return bestMatch;
    }

    private static bool IsPathParameterSegment(string segment)
    {
        return !string.IsNullOrWhiteSpace(segment)
            && segment.StartsWith("{", StringComparison.Ordinal)
            && segment.EndsWith("}", StringComparison.Ordinal);
    }

    private static string NormalizePathForComparison(string path)
    {
        return path
            .Replace('\\', '/')
            .Trim('/');
    }

    private static KiotaOperationMetadata? ExtractOperationFromBuilderFile(
        string clientRootPath,
        string builderFile,
        ApiEndpointDefinition endpoint,
        KiotaGroupMetadata kiotaGroup)
    {
        var content = File.ReadAllText(builderFile);
        var asyncMethodName = GetKiotaAsyncMethodName(endpoint.Method);

        var signature = ExtractKiotaMethodSignature(content, asyncMethodName);

        if (signature is null)
            return null;

        var rawReturnType = CleanTypeName(signature.ReturnType);
        var isCollection = IsCollectionReturnType(rawReturnType);
        var returnType = rawReturnType;

        // Check if the return type is a wrapper response with a Value collection property
        if (!isCollection && !rawReturnType.Equals("object?", StringComparison.OrdinalIgnoreCase))
        {
            var collectionItemType = ExtractCollectionItemTypeFromWrapperResponse(clientRootPath, rawReturnType);
            if (!string.IsNullOrWhiteSpace(collectionItemType))
            {
                isCollection = true;
                returnType = collectionItemType;
            }
        }

        if (isCollection && returnType.Equals(rawReturnType, StringComparison.Ordinal))
        {
            returnType = ExtractInnerTypeFromCollection(rawReturnType);
        }

        var requestBodyType = DetectRequestBodyTypeName(asyncMethodName, signature.ParametersText);

        var accessExpression = BuildAccessExpressionFromBuilderFile(
            clientRootPath,
            builderFile,
            kiotaGroup);

        var bodyProperties = requestBodyType.Equals("object?", StringComparison.OrdinalIgnoreCase)
            ? new List<KiotaRequestBodyPropertyMetadata>()
            : ExtractRequestBodyProperties(clientRootPath, requestBodyType);

        var pathParameters = ExtractPathParameters(
            clientRootPath,
            builderFile,
            endpoint,
            kiotaGroup);

        return new KiotaOperationMetadata
        {
            OperationId = endpoint.OperationId,
            MethodName = asyncMethodName,
            HttpMethod = endpoint.Method,
            ReturnTypeName = returnType,
            RequestBodyTypeName = requestBodyType,
            AccessExpression = accessExpression,
            EndpointPath = NormalizeOpenApiPath(endpoint.Path),
            RequestBodyProperties = bodyProperties,
            IsCollection = isCollection,
            PathParameters = pathParameters
        };
    }

    private static KiotaMethodSignature? ExtractKiotaMethodSignature(
        string content,
        string asyncMethodName)
    {
        var lines = content
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(x => x.Trim())
            .ToArray();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (!line.Contains("public async Task<", StringComparison.Ordinal))
                continue;

            var signatureBuilder = new StringBuilder(line);

            for (var j = i; j < lines.Length; j++)
            {
                if (j > i)
                    signatureBuilder.Append(' ').Append(lines[j]);

                var candidate = signatureBuilder.ToString().Trim();

                if (!candidate.Contains($"{asyncMethodName}(", StringComparison.Ordinal))
                    continue;

                var returnType = ExtractTaskReturnType(candidate);

                if (string.IsNullOrWhiteSpace(returnType))
                    continue;

                var parameters = ExtractMethodParameters(candidate, asyncMethodName);

                if (parameters is null)
                    continue;

                return new KiotaMethodSignature(returnType, parameters);
            }
        }

        return null;
    }

    private static string ExtractTaskReturnType(string methodSignatureLine)
    {
        var taskStart = methodSignatureLine.IndexOf("Task<", StringComparison.Ordinal);

        if (taskStart < 0)
            return string.Empty;

        var typeStart = taskStart + "Task<".Length;
        var depth = 1;

        for (var i = typeStart; i < methodSignatureLine.Length; i++)
        {
            var current = methodSignatureLine[i];

            if (current == '<')
                depth++;

            if (current == '>')
                depth--;

            if (depth == 0)
                return methodSignatureLine[typeStart..i].Trim();
        }

        return string.Empty;
    }

    private static string? ExtractMethodParameters(
        string methodSignatureLine,
        string asyncMethodName)
    {
        var methodIndex = methodSignatureLine.IndexOf(
            asyncMethodName,
            StringComparison.Ordinal);

        if (methodIndex < 0)
            return null;

        var openParenthesisIndex = methodSignatureLine.IndexOf('(', methodIndex);

        if (openParenthesisIndex < 0)
            return null;

        var depth = 0;

        for (var i = openParenthesisIndex; i < methodSignatureLine.Length; i++)
        {
            var current = methodSignatureLine[i];

            if (current == '(')
                depth++;

            if (current == ')')
                depth--;

            if (depth == 0)
                return methodSignatureLine[(openParenthesisIndex + 1)..i].Trim();
        }

        return null;
    }

    private static string BuildAccessExpressionFromBuilderFile(
        string clientRootPath,
        string builderFile,
        KiotaGroupMetadata kiotaGroup)
    {
        var relativeDirectory = Path.GetRelativePath(
            clientRootPath,
            Path.GetDirectoryName(builderFile) ?? string.Empty);

        var fullAccessExpression = string.Join(
            ".",
            SplitRelativePath(relativeDirectory)
                .Select(NormalizeIdentifier)
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        if (string.IsNullOrWhiteSpace(fullAccessExpression))
            return string.Empty;

        var groupPrefix = kiotaGroup.BuilderAccessExpression;

        if (fullAccessExpression.Equals(groupPrefix, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        if (fullAccessExpression.StartsWith(groupPrefix + ".", StringComparison.OrdinalIgnoreCase))
            return fullAccessExpression[(groupPrefix.Length + 1)..];

        return fullAccessExpression;
    }

    private static List<KiotaPathParameterMetadata> ExtractPathParameters(
        string clientRootPath,
        string builderFile,
        ApiEndpointDefinition endpoint,
        KiotaGroupMetadata kiotaGroup)
    {
        var pathParameters = endpoint.Parameters
            .Where(x => x.Location.Equals("path", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (pathParameters.Length == 0)
            return new List<KiotaPathParameterMetadata>();

        var result = new List<KiotaPathParameterMetadata>();

        foreach (var pathParameter in pathParameters)
        {
            var parameterName = ToCamelCase(NormalizeIdentifier(pathParameter.Name));
            var accessExpression = DetectPathParameterAccessExpression(
                clientRootPath,
                builderFile,
                pathParameter.Name,
                parameterName,
                kiotaGroup);

            result.Add(new KiotaPathParameterMetadata
            {
                Name = pathParameter.Name,
                AccessExpression = accessExpression
            });
        }

        return result;
    }

    private static string DetectPathParameterAccessExpression(
        string clientRootPath,
        string builderFile,
        string originalParameterName,
        string parameterName,
        KiotaGroupMetadata kiotaGroup)
    {
        var parentBuilderFile = FindParentRequestBuilderFile(
            clientRootPath,
            builderFile,
            kiotaGroup);

        if (string.IsNullOrWhiteSpace(parentBuilderFile) || !File.Exists(parentBuilderFile))
            return $"[{parameterName}]";

        var content = File.ReadAllText(parentBuilderFile);

        if (HasIndexerForParameter(content, parameterName))
            return $"[{parameterName}]";

        var byMethod = FindByMethodForParameter(content, originalParameterName, parameterName);

        if (!string.IsNullOrWhiteSpace(byMethod))
            return $".{byMethod}({parameterName})";

        return $"[{parameterName}]";
    }

    private static string? FindParentRequestBuilderFile(
        string clientRootPath,
        string builderFile,
        KiotaGroupMetadata kiotaGroup)
    {
        var builderDirectory = Path.GetDirectoryName(builderFile);

        if (string.IsNullOrWhiteSpace(builderDirectory))
            return null;

        var parentDirectory = Directory.GetParent(builderDirectory)?.FullName;

        if (string.IsNullOrWhiteSpace(parentDirectory))
            return null;

        var relativeParentDirectory = Path.GetRelativePath(clientRootPath, parentDirectory);

        var parentSegments = SplitRelativePath(relativeParentDirectory);

        if (parentSegments.Length == 0)
            return null;

        var parentBuilderStem = parentSegments[^1];

        var parentBuilderFile = Path.Combine(
            parentDirectory,
            $"{NormalizeIdentifier(parentBuilderStem)}RequestBuilder.cs");

        if (File.Exists(parentBuilderFile))
            return parentBuilderFile;

        var alternative = Directory
            .GetFiles(parentDirectory, "*RequestBuilder.cs", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        return alternative;
    }

    private static bool HasIndexerForParameter(string content, string parameterName)
    {
        return content.Contains("this[", StringComparison.OrdinalIgnoreCase)
            && content.Contains(parameterName, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindByMethodForParameter(
        string content,
        string originalParameterName,
        string parameterName)
    {
        var normalizedOriginal = NormalizeIdentifier(originalParameterName);
        var normalizedParameter = NormalizeIdentifier(parameterName);

        var candidates = new[]
        {
            $"By{normalizedOriginal}",
            $"By{normalizedParameter}",
            "ById",
            "ByUserId",
            "ByKey"
        };

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (content.Contains($"{candidate}(", StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static List<KiotaRequestBodyPropertyMetadata> ExtractRequestBodyProperties(
        string clientRootPath,
        string requestBodyTypeName)
    {
        var requestBodyClassName = ExtractClassNameFromTypeName(requestBodyTypeName);

        if (string.IsNullOrWhiteSpace(requestBodyClassName))
            return new List<KiotaRequestBodyPropertyMetadata>();

        var filePath = FindClassFile(clientRootPath, requestBodyClassName);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return new List<KiotaRequestBodyPropertyMetadata>();

        var content = File.ReadAllText(filePath);
        var matches = PropertyRegex.Matches(content);

        var properties = new List<KiotaRequestBodyPropertyMetadata>();

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            var typeName = CleanTypeName(match.Groups["type"].Value);
            var propertyName = match.Groups["name"].Value.Trim();

            if (string.IsNullOrWhiteSpace(propertyName))
                continue;

            if (IsIgnoredRequestBodyProperty(propertyName))
                continue;

            properties.Add(new KiotaRequestBodyPropertyMetadata
            {
                Name = propertyName,
                TypeName = typeName,
                IsNullable = typeName.EndsWith("?", StringComparison.Ordinal)
            });
        }

        return properties
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.Name)
            .ToList();
    }

    private static bool IsIgnoredRequestBodyProperty(string propertyName)
    {
        return propertyName.Equals("AdditionalData", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("BackingStore", StringComparison.OrdinalIgnoreCase)
            || propertyName.Equals("OdataType", StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindClassFile(
        string clientRootPath,
        string className)
    {
        var expectedFileName = $"{className}.cs";

        return Directory
            .GetFiles(clientRootPath, expectedFileName, SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private static string ExtractClassNameFromTypeName(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        var cleaned = typeName
            .Replace("?", string.Empty)
            .Trim();

        var genericStartIndex = cleaned.IndexOf('<');

        if (genericStartIndex >= 0)
            cleaned = cleaned[..genericStartIndex];

        var lastDotIndex = cleaned.LastIndexOf('.');

        return lastDotIndex >= 0
            ? cleaned[(lastDotIndex + 1)..]
            : cleaned;
    }

    private static string DetectRequestBodyTypeName(
        string methodName,
        string parametersText)
    {
        if (methodName.Equals("GetAsync", StringComparison.OrdinalIgnoreCase) ||
            methodName.Equals("DeleteAsync", StringComparison.OrdinalIgnoreCase))
        {
            return "object?";
        }

        if (string.IsNullOrWhiteSpace(parametersText))
            return "object?";

        var parameters = SplitMethodParameters(parametersText);

        foreach (var parameter in parameters)
        {
            var cleaned = parameter.Trim();

            if (cleaned.Contains("CancellationToken", StringComparison.OrdinalIgnoreCase))
                continue;

            if (cleaned.Contains("Action<", StringComparison.OrdinalIgnoreCase))
                continue;

            if (cleaned.Contains("RequestConfiguration", StringComparison.OrdinalIgnoreCase))
                continue;

            if (cleaned.Contains("= default", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[..cleaned.IndexOf("= default", StringComparison.OrdinalIgnoreCase)].Trim();

            var tokens = cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length < 2)
                continue;

            return CleanTypeName(string.Join(" ", tokens.Take(tokens.Length - 1)));
        }

        return "object?";
    }

    private static string[] SplitMethodParameters(string parametersText)
    {
        if (string.IsNullOrWhiteSpace(parametersText))
            return Array.Empty<string>();

        var result = new List<string>();
        var current = new List<char>();
        var genericDepth = 0;
        var parenthesisDepth = 0;

        foreach (var character in parametersText)
        {
            if (character == '<')
                genericDepth++;

            if (character == '>')
                genericDepth--;

            if (character == '(')
                parenthesisDepth++;

            if (character == ')')
                parenthesisDepth--;

            if (character == ',' && genericDepth == 0 && parenthesisDepth == 0)
            {
                result.Add(new string(current.ToArray()));
                current.Clear();
                continue;
            }

            current.Add(character);
        }

        if (current.Count > 0)
            result.Add(new string(current.ToArray()));

        return result
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string NormalizeOpenApiPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return string.Join(
            "/",
            path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(x => !IsVersionSegment(x))
                .Select(x => IsPathParameterSegment(x) ? "{param}" : x.Trim().ToLowerInvariant()));
    }

    private static string GetKiotaAsyncMethodName(string httpMethod)
    {
        return httpMethod.ToUpperInvariant() switch
        {
            "GET" => "GetAsync",
            "POST" => "PostAsync",
            "PUT" => "PutAsync",
            "PATCH" => "PatchAsync",
            "DELETE" => "DeleteAsync",
            _ => "GetAsync"
        };
    }

    private static string CleanTypeName(string rawType)
    {
        if (string.IsNullOrWhiteSpace(rawType))
            return "object?";

        var cleaned = rawType
            .Replace("global::", string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Trim();

        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ");

        return cleaned;
    }

    private static string ExtractFirstBusinessSegment(string path)
    {
        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !IsVersionSegment(x))
            .Where(x => !x.StartsWith("{") && !x.EndsWith("}"))
            .Select(NormalizeIdentifier)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? string.Empty;
    }

    private static bool IsIgnoredBuilderName(string builderStem)
    {
        if (string.IsNullOrWhiteSpace(builderStem))
            return true;

        return builderStem.Equals("Base", StringComparison.OrdinalIgnoreCase)
            || builderStem.Equals("Item", StringComparison.OrdinalIgnoreCase)
            || builderStem.Equals("Count", StringComparison.OrdinalIgnoreCase)
            || builderStem.StartsWith("With", StringComparison.OrdinalIgnoreCase);
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

    private static string[] SplitRelativePath(string relativePath)
    {
        return relativePath
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string NormalizeIdentifier(string rawValue)
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

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToLowerInvariant();

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static bool IsCollectionReturnType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        return typeName.Contains("List<", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("IList<", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("ICollection<", StringComparison.OrdinalIgnoreCase)
            || typeName.Contains("IEnumerable<", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ExtractCollectionItemTypeFromWrapperResponse(
        string clientRootPath,
        string wrapperTypeName)
    {
        var className = ExtractClassNameFromTypeName(wrapperTypeName);

        if (string.IsNullOrWhiteSpace(className))
            return null;

        var filePath = FindClassFile(clientRootPath, className);

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var content = File.ReadAllText(filePath);

        // Look for "public List<T> Value" or "public ICollection<T> Value" property
        var valuePropertyMatch = System.Text.RegularExpressions.Regex.Match(
            content,
            @"public\s+(List|IList|ICollection|IEnumerable)<(?<innerType>[^>]+)>\???\s+Value\s*\{\s*get",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (valuePropertyMatch.Success)
        {
            var innerType = valuePropertyMatch.Groups["innerType"].Value.Trim();
            return CleanTypeName(innerType);
        }

        return null;
    }

    private static string ExtractInnerTypeFromCollection(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return "object?";

        var start = typeName.IndexOf('<');

        if (start < 0)
            return typeName;

        var depth = 0;

        for (var i = start; i < typeName.Length; i++)
        {
            if (typeName[i] == '<')
                depth++;

            if (typeName[i] == '>')
                depth--;

            if (depth == 0)
            {
                return typeName[(start + 1)..i].Trim();
            }
        }

        return typeName;
    }
}
