namespace LegacyModernizer.Generation.Composition;

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
    public string ContractsDtosNamespace => $"{ContractsNamespace}.Dtos";
    public string ContractsInterfacesNamespace => $"{ContractsNamespace}.Interfaces";
    public string HttpFacadesNamespace => $"{HttpNamespace}.Facades";
    public string HttpServicesNamespace => $"{HttpNamespace}.Services";
    public string HttpMappersNamespace => $"{HttpNamespace}.Mappers";
    public string HttpDependencyInjectionNamespace => $"{HttpNamespace}.DependencyInjection";
}
