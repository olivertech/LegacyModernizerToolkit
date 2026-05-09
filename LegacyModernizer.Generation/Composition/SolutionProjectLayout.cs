namespace LegacyModernizer.Generation.Composition;

/// <summary>
/// Reúne os nomes e namespaces que a composição precisa usar ao gerar a solução final.
/// </summary>
internal sealed record SolutionProjectLayout(
    string SolutionName,
    string SolutionRootFolderName,
    string ApiClientProjectName,
    string ContractsProjectName,
    string HttpProjectName,
    string ApiClientNamespace,
    string ContractsNamespace,
    string HttpNamespace,
    string ClientClassName)
{
    /// <summary>
    /// Namespace dos DTOs expostos ao consumidor.
    /// </summary>
    public string ContractsDtosNamespace => $"{ContractsNamespace}.Dtos";

    /// <summary>
    /// Namespace das interfaces públicas da solução gerada.
    /// </summary>
    public string ContractsInterfacesNamespace => $"{ContractsNamespace}.Interfaces";

    /// <summary>
    /// Namespace das facades que encapsulam o Kiota.
    /// </summary>
    public string HttpFacadesNamespace => $"{HttpNamespace}.Facades";

    /// <summary>
    /// Namespace dos services por grupo de API.
    /// </summary>
    public string HttpServicesNamespace => $"{HttpNamespace}.Services";

    /// <summary>
    /// Namespace dos mapeadores internos entre tipos do Kiota e DTOs próprios.
    /// </summary>
    public string HttpMappersNamespace => $"{HttpNamespace}.Mappers";

    /// <summary>
    /// Namespace dos componentes de autenticação gerados.
    /// </summary>
    public string HttpAuthenticationNamespace => $"{HttpNamespace}.Authentication";

    /// <summary>
    /// Namespace das extensões de DI consumidas pela aplicação hospedeira.
    /// </summary>
    public string HttpDependencyInjectionNamespace => $"{HttpNamespace}.DependencyInjection";
}
