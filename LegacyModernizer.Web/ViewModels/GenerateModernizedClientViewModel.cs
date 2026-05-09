using System.ComponentModel.DataAnnotations;

namespace LegacyModernizer.Web.ViewModels;

public sealed class GenerateModernizedClientViewModel : IValidatableObject
{
    [Required]
    [Display(Name = "Generation Mode")]
    public GenerationMode GenerationMode { get; set; } = GenerationMode.Standalone;

    [Required]
    [Display(Name = "Authentication Mode")]
    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.PerMethodToken;

    [Required(ErrorMessage = "Please select a URL mode.")]
    [Display(Name = "URL Mode")]
    public string UrlMode { get; set; } = "production";

    [Display(Name = "Local Specification URL")]
    [RegularExpression(
        @"^https?://localhost(:\d+)?(/.*)?$",
        ErrorMessage = "Please provide a valid localhost URL (e.g. https://localhost:7054/swagger/v1/swagger.json).")]
    public string? LocalSpecificationUrl { get; set; }

    [Display(Name = "Specification URL")]
    [Url(ErrorMessage = "Please provide a valid URL.")]
    public string? SpecificationUrl { get; set; }

    [Display(Name = "Embedded Project Prefix")]
    public string? EmbeddedProjectPrefix { get; set; }

    [Required(ErrorMessage = "The project name is required.")]
    [Display(Name = "Project Name")]
    public string ProjectName { get; set; } = string.Empty;

    [Required(ErrorMessage = "The base namespace is required.")]
    [Display(Name = "Base Namespace")]
    public string BaseNamespace { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Target Framework")]
    public string TargetFramework { get; set; } = "net8.0";

    public string ActiveSpecificationUrl => UrlMode == "localhost"
        ? LocalSpecificationUrl ?? string.Empty
        : SpecificationUrl ?? string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (UrlMode == "localhost")
        {
            if (string.IsNullOrWhiteSpace(LocalSpecificationUrl))
                yield return new ValidationResult(
                    "The local specification URL is required.",
                    [nameof(LocalSpecificationUrl)]);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SpecificationUrl))
                yield return new ValidationResult(
                    "The specification URL is required.",
                    [nameof(SpecificationUrl)]);
        }

        if (GenerationMode == GenerationMode.Embedded &&
            string.IsNullOrWhiteSpace(EmbeddedProjectPrefix))
        {
            yield return new ValidationResult(
                "The embedded project prefix is required when generation mode is Embedded.",
                [nameof(EmbeddedProjectPrefix)]);
        }
    }
}
