namespace LegacyModernizer.Web.Security;

public interface IDownloadTokenService
{
    string IssueToken(string packagePath);
    bool TryResolvePackagePath(string token, out string packagePath);
}
