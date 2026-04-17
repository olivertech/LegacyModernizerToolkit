namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// Representa o namespace base, e
/// carrega regra para formato válido
/// de namespace em C#.
/// </summary>
public sealed record NamespaceName
{
    private static readonly Regex ValidPattern = new(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);

    public string Value { get; }

    public NamespaceName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Namespace cannot be null or empty.", nameof(value));

        value = value.Trim();

        if (!ValidPattern.IsMatch(value))
            throw new ArgumentException(
                "Namespace must be a valid C# namespace.",
                nameof(value));

        Value = value;
    }

    /// <summary>
    /// Esse override é útil para facilitar a depuração, logs, 
    /// mensagens, debug, concatenação de string, geração de código e templates,
    /// expondo o valor "real" de forma limpa.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => Value;
}
