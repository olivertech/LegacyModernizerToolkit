namespace LegacyModernizer.Domain.Entities;

/// <summary>
/// Representar a spec da API adquirida e validada.
/// É a partir dessa entidade que o processo de modernização irá gerar os artefatos, templates, código, etc.
/// É o ponto de partida para o processo de modernização.
/// </summary>
public sealed class ApiSpecification
{
    public Guid Id { get; private set; }
    public SpecificationSource Source { get; private set; }
    public SpecificationFormat Format { get; private set; }
    public string? LocalPath { get; private set; }
    public SpecificationValidationStatus ValidationStatus { get; private set; }

    public ApiSpecification(SpecificationSource source,
                            SpecificationFormat format = SpecificationFormat.Unknown)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Format = format;
        ValidationStatus = SpecificationValidationStatus.Pending;
        Id = Guid.NewGuid();
    }

    public void SetLocalPath(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath))
            throw new ArgumentException("Local path cannot be null or empty.", nameof(localPath));

        LocalPath = localPath.Trim();
    }

    public void SetFormat(SpecificationFormat format)
    {
        Format = format;
    }

    public void MarkValidationStatusAsValid()
    {
        if (string.IsNullOrWhiteSpace(LocalPath))
            throw new InvalidOperationException("Cannot mark specification as valid without a local path.");

        ValidationStatus = SpecificationValidationStatus.Valid;
    }

    public void MarkValidationStatusAsInvalid()
    {
        if(ValidationStatus != SpecificationValidationStatus.Valid)
            ValidationStatus = SpecificationValidationStatus.Invalid;
    }

    public void MarkValidationStatusAsNone()
    {
        if (ValidationStatus != SpecificationValidationStatus.Valid)
            ValidationStatus = SpecificationValidationStatus.None;
    }

    public void MarkValidationStatusAsWarning()
    {
        if (ValidationStatus != SpecificationValidationStatus.Valid)
            ValidationStatus = SpecificationValidationStatus.Warning;
    }

    public void MarkValidationStatusAsPending()
    {
        if (ValidationStatus != SpecificationValidationStatus.Valid)
            ValidationStatus = SpecificationValidationStatus.Pending;
    }
}
