namespace LegacyModernizer.Domain.Enums;

/// <summary>
/// Define como a autenticação será refletida no contrato público gerado.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// Mantém o token como argumento explícito nos métodos gerados.
    /// </summary>
    PerMethodToken = 0,

    /// <summary>
    /// Move a resolução do token para a infraestrutura da aplicação hospedeira.
    /// </summary>
    AccessTokenAccessor = 1
}
