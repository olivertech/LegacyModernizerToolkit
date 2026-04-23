namespace LegacyModernizer.Generation.Kiota;

public sealed class KiotaOutputInspectionService : IKiotaOutputInspectionService
{
    private static readonly Regex NamespaceRegex =
        new(@"namespace\s+([A-Za-z0-9_.]+);", RegexOptions.Compiled);

    public Task<KiotaClientMetadata> InspectAsync(GeneratedArtifact generatedClientArtifact,
                                                  CancellationToken cancellationToken = default)
    {
        if (generatedClientArtifact is null)
            throw new ArgumentNullException(nameof(generatedClientArtifact));

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
        var groups = DetectGroups(clientRootPath);

        var metadata = new KiotaClientMetadata
        {
            RootNamespace = rootNamespace,
            ClientClassName = clientClassName,
            Groups = groups
        };

        return Task.FromResult(metadata);
    }

    private static string DetectRootNamespace(string[] allCsFiles)
    {
        foreach (var file in allCsFiles)
        {
            var content = File.ReadAllText(file);
            var match = NamespaceRegex.Match(content);

            if (match.Success)
            {
                var fullNamespace = match.Groups[1].Value.Trim();

                if (!string.IsNullOrWhiteSpace(fullNamespace))
                    return fullNamespace;
            }
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
            {
                groups[candidate.GroupName] = candidate;
            }
        }

        return groups.Values
            .OrderBy(x => x.GroupName)
            .ToList();
    }

    private static KiotaGroupMetadata? TryCreateGroupMetadata(string clientRootPath, string requestBuilderFile)
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

        var directorySegments = relativeDirectory
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (directorySegments.Length == 0)
            return null;

        var parentSegment = directorySegments[^1];

        // Regra principal:
        // pasta pai deve ter o mesmo nome do builder base:
        // App/AppRequestBuilder.cs
        // V1/App/AppRequestBuilder.cs
        if (!parentSegment.Equals(builderStem, StringComparison.OrdinalIgnoreCase))
            return null;

        // Aceitamos somente estruturas rasas:
        // [App]
        // [V1, App]
        if (directorySegments.Length > 2)
            return null;

        var builderAccessExpression = string.Join(".", directorySegments.Select(NormalizeIdentifier));

        return new KiotaGroupMetadata
        {
            GroupName = NormalizeIdentifier(builderStem),
            BuilderTypeName = fileNameWithoutExtension,
            BuilderPropertyName = NormalizeIdentifier(builderStem),
            BuilderAccessExpression = builderAccessExpression
        };
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

    private static string NormalizeIdentifier(string rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
            return string.Empty;

        var cleaned = new string(rawValue
            .Trim()
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        if (cleaned.Length == 1)
            return cleaned.ToUpperInvariant();

        return char.ToUpperInvariant(cleaned[0]) + cleaned[1..];
    }
}