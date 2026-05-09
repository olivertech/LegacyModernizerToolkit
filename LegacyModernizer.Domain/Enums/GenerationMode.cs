namespace LegacyModernizer.Domain.Enums;

/// <summary>
/// Define como a solução final deve ser organizada para o consumidor.
/// </summary>
public enum GenerationMode
{
    /// <summary>
    /// Gera uma solution autônoma e pronta para avaliação isolada.
    /// </summary>
    Standalone = 0,

    /// <summary>
    /// Gera um módulo pronto para ser incorporado a uma solution já existente.
    /// </summary>
    Embedded = 1
}
