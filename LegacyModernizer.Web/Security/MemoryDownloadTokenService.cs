using System.Security.Cryptography;
using Microsoft.Extensions.Caching.Memory;

namespace LegacyModernizer.Web.Security;

/// <summary>
/// Emite tokens efêmeros para download de pacotes gerados sem expor caminhos físicos ao cliente.
/// </summary>
public sealed class MemoryDownloadTokenService : IDownloadTokenService
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);
    // O escopo permitido é limitado ao diretório temporário controlado do Toolkit.
    private static readonly string AllowedPackagesRoot = Path.GetFullPath(
        Path.Combine(Path.GetTempPath(), "LegacyModernizer"));

    private readonly IMemoryCache _memoryCache;

    public MemoryDownloadTokenService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <summary>
    /// Associa um token opaco ao pacote zip gerado e o mantém temporariamente em cache.
    /// </summary>
    public string IssueToken(string packagePath)
    {
        var normalizedPackagePath = NormalizeAndValidatePackagePath(packagePath);
        var token = GenerateToken();

        _memoryCache.Set(
            token,
            normalizedPackagePath,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TokenLifetime,
                Size = 1
            });

        return token;
    }

    /// <summary>
    /// Resolve um token apenas se ele ainda existir, estiver no escopo permitido e apontar para um zip válido.
    /// </summary>
    public bool TryResolvePackagePath(string token, out string packagePath)
    {
        packagePath = string.Empty;

        if (string.IsNullOrWhiteSpace(token))
            return false;

        if (!_memoryCache.TryGetValue<string>(token, out var cachedPackagePath))
            return false;

        if (string.IsNullOrWhiteSpace(cachedPackagePath))
            return false;

        if (!IsPathAllowed(cachedPackagePath) || !File.Exists(cachedPackagePath))
            return false;

        packagePath = cachedPackagePath;
        return true;
    }

    private static string NormalizeAndValidatePackagePath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required.", nameof(packagePath));

        var normalizedPath = Path.GetFullPath(packagePath);

        // A validação antecipada garante que apenas pacotes produzidos dentro do workspace do Toolkit
        // entrem no fluxo de download.
        if (!IsPathAllowed(normalizedPath))
            throw new InvalidOperationException("The package path is outside the allowed download scope.");

        if (!File.Exists(normalizedPath))
            throw new FileNotFoundException("The package file was not found.", normalizedPath);

        return normalizedPath;
    }

    private static bool IsPathAllowed(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            return false;

        var normalizedPath = Path.GetFullPath(packagePath);

        return normalizedPath.StartsWith(AllowedPackagesRoot, StringComparison.OrdinalIgnoreCase)
               && normalizedPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        // A versão base64url evita caracteres problemáticos em links e query strings.
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
