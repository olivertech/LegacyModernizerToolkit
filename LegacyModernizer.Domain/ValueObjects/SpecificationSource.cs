namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// Define o tipo e o valor da origem da especificação.
/// </summary>
public sealed record SpecificationSource
{
    public SpecificationSourceType Type { get; }
    public string Value { get; }

    public SpecificationSource(SpecificationSourceType type, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Specification source value cannot be null or empty.", nameof(value));

        value = value.Trim();

        Validate(type, value);

        Type = type;
        Value = value;
    }

    private static void Validate(SpecificationSourceType type, string value)
    {
        switch (type)
        {
            case SpecificationSourceType.Url:
                if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new ArgumentException("Specification source must be a valid HTTP or HTTPS URL.", nameof(value));
                }
                break;

            case SpecificationSourceType.File:
                if (string.IsNullOrWhiteSpace(Path.GetFileName(value)))
                {
                    throw new ArgumentException("Specification source file path is invalid.", nameof(value));
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported specification source type.");
        }
    }

    /// <summary>
    /// Esse override é útil para facilitar a depuração, logs, 
    /// mensagens, debug, concatenação de string, geração de código e templates,
    /// expondo o valor "real" de forma limpa.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"{Type}: {Value}";
}
