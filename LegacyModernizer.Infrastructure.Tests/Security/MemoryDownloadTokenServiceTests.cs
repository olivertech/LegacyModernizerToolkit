using LegacyModernizer.Web.Security;
using Microsoft.Extensions.Caching.Memory;

namespace LegacyModernizer.Infrastructure.Tests.Security;

public sealed class MemoryDownloadTokenServiceTests : IDisposable
{
    private readonly string _tempRootPath;

    public MemoryDownloadTokenServiceTests()
    {
        _tempRootPath = Path.Combine(Path.GetTempPath(), "LegacyModernizer", Guid.NewGuid().ToString("N"), "package");
        Directory.CreateDirectory(_tempRootPath);
    }

    [Fact]
    public void IssueToken_AndResolvePackagePath_AllowsRegisteredZipInsideWorkspaceScope()
    {
        var packagePath = CreateZipFile("sample.zip");
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var service = new MemoryDownloadTokenService(memoryCache);

        var token = service.IssueToken(packagePath);
        var resolved = service.TryResolvePackagePath(token, out var resolvedPath);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(packagePath), resolvedPath);
    }

    [Fact]
    public void IssueToken_RejectsZipOutsideAllowedWorkspaceScope()
    {
        var externalRoot = Path.Combine(Path.GetTempPath(), "OtherRoot", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(externalRoot);
        var externalPath = Path.Combine(externalRoot, "sample.zip");
        File.WriteAllText(externalPath, "content");

        try
        {
            using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
            var service = new MemoryDownloadTokenService(memoryCache);

            var exception = Assert.Throws<InvalidOperationException>(() => service.IssueToken(externalPath));
            Assert.Contains("outside the allowed download scope", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(externalRoot))
                Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public void TryResolvePackagePath_ReturnsFalse_ForUnknownToken()
    {
        using var memoryCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1024 });
        var service = new MemoryDownloadTokenService(memoryCache);

        var resolved = service.TryResolvePackagePath("invalid-token", out var packagePath);

        Assert.False(resolved);
        Assert.Equal(string.Empty, packagePath);
    }

    private string CreateZipFile(string fileName)
    {
        var fullPath = Path.Combine(_tempRootPath, fileName);
        File.WriteAllText(fullPath, "content");
        return fullPath;
    }

    public void Dispose()
    {
        var rootPath = Directory.GetParent(_tempRootPath)!.FullName;

        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }
}
