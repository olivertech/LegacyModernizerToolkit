namespace LegacyModernizer.Generation.Tests.Scaffolding;

internal static class GoldenFileAssert
{
    public static void Matches(string actualPath, string goldenPath)
    {
        Assert.True(File.Exists(actualPath), $"Generated file was not found: {actualPath}");
        Assert.True(File.Exists(goldenPath), $"Golden file was not found: {goldenPath}");

        var actual = Normalize(File.ReadAllText(actualPath));
        var expected = Normalize(File.ReadAllText(goldenPath));

        Assert.Equal(expected, actual);
    }

    public static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "LegacyModernizerToolkit.slnx")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate the repository root from the current test context.");
    }

    private static string Normalize(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
