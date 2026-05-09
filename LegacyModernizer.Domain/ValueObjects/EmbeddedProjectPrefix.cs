namespace LegacyModernizer.Domain.ValueObjects;

public sealed record EmbeddedProjectPrefix
{
    private static readonly Regex ValidPattern = new(@"^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.Compiled);

    public string Value { get; }

    public EmbeddedProjectPrefix(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Embedded project prefix cannot be null or empty.", nameof(value));

        value = value.Trim();

        if (!ValidPattern.IsMatch(value))
        {
            throw new ArgumentException(
                "Embedded project prefix must start with a letter and contain only letters, numbers, dot, underscore or hyphen.",
                nameof(value));
        }

        Value = value;
    }

    public override string ToString() => Value;
}
