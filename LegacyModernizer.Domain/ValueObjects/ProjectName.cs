namespace LegacyModernizer.Domain.ValueObjects;

/// <summary>
/// Representa o nome do projeto gerado,
/// com as seguintes regras de validação:
/// - não pode ser vazio
/// - deve ser compatível com nome de solução / projeto.NET
/// </summary>
public sealed record ProjectName
{
    private static readonly Regex ValidPattern = new(@"^[A-Za-z][A-Za-z0-9._-]*$", RegexOptions.Compiled);

    public string Value { get; }

    public ProjectName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Project name cannot be null or empty.", nameof(value));

        value = value.Trim();

        if (!ValidPattern.IsMatch(value))
            throw new ArgumentException(
                "Project name must start with a letter and contain only letters, numbers, dot, underscore or hyphen.",
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
