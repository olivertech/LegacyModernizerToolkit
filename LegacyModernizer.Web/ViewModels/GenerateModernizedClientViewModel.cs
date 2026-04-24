namespace LegacyModernizer.Web.ViewModels;

public sealed class GenerateModernizedClientViewModel
{
    [Required(ErrorMessage = "The specification URL is required.")]
    [Display(Name = "Specification URL")]
    [Url(ErrorMessage = "Please provide a valid URL.")]
    public string SpecificationUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "The project name is required.")]
    [Display(Name = "Project Name")]
    public string ProjectName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The base namespace is required.")]
    [Display(Name = "Base Namespace")]
    public string BaseNamespace { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Target Framework")]
    public string TargetFramework { get; set; } = "net8.0";
}
