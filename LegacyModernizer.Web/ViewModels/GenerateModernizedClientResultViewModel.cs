namespace LegacyModernizer.Web.ViewModels;

public sealed class GenerateModernizedClientResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ExecutionId { get; set; }
    public string? DownloadToken { get; set; }
}
