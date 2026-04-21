namespace LegacyModernizer.Application.UseCases;

/// <summary>
/// Classe responsável por orquestrar o processo de geração de um cliente modernizado a partir de uma especificação de API.
/// </summary>
public sealed class GenerateModernizedClientUseCase : IGenerateModernizedClientUseCase
{
    private readonly IWorkspacePreparationService _workspacePreparationService;
    private readonly ISpecificationAcquisitionService _specificationAcquisitionService;
    private readonly ISpecificationValidationService _specificationValidationService;
    private readonly IClientGenerationService _clientGenerationService;
    private readonly ISolutionCompositionService _solutionCompositionService;
    private readonly IPackageGenerationService _packageGenerationService;

    public GenerateModernizedClientUseCase(IWorkspacePreparationService workspacePreparationService,
                                           ISpecificationAcquisitionService specificationAcquisitionService,
                                           ISpecificationValidationService specificationValidationService,
                                           IClientGenerationService clientGenerationService,
                                           ISolutionCompositionService solutionCompositionService,
                                           IPackageGenerationService packageGenerationService)
    {
        _workspacePreparationService = workspacePreparationService ?? throw new ArgumentNullException(nameof(workspacePreparationService));
        _specificationAcquisitionService = specificationAcquisitionService ?? throw new ArgumentNullException(nameof(specificationAcquisitionService));
        _specificationValidationService = specificationValidationService ?? throw new ArgumentNullException(nameof(specificationValidationService));
        _clientGenerationService = clientGenerationService ?? throw new ArgumentNullException(nameof(clientGenerationService));
        _solutionCompositionService = solutionCompositionService ?? throw new ArgumentNullException(nameof(solutionCompositionService));
        _packageGenerationService = packageGenerationService ?? throw new ArgumentNullException(nameof(packageGenerationService));
    }

    public async Task<GenerateModernizedClientResponse> ExecuteAsync(GenerateModernizedClientRequest request,
                                                                     CancellationToken cancellationToken = default)
    {
        ModernizationExecution? execution = null;

        try
        {
            ValidateRequest(request);

            // Cria o Value Object para a fonte da especificação
            var specificationSource = new SpecificationSource(SpecificationSourceType.Url, request.SpecificationUrl);
            // Cria os Value Objects para o nome do projeto
            var projectName = new ProjectName(request.ProjectName);
            // Cria os Value Objects para namespace base
            var baseNamespace = new NamespaceName(request.BaseNamespace);

            // Cria o request de modernização agregando as informações necessárias para o processo
            var modernizationRequest = new ModernizationRequest(specificationSource,
                                                                projectName,
                                                                baseNamespace);

            // ===========================================================================================================================
            // 1 - Inicia a execução do processo de modernização, registrando o request e o estado inicial
            // da execução. A partir deste ponto, a execução passa a ser rastreada e monitorada, permitindo acompanhar o progresso
            // e diagnosticar eventuais falhas ou gargalos durante o processo.
            // ===========================================================================================================================
            execution = new ModernizationExecution(modernizationRequest);
            execution.Start();

            // ===========================================================================================================================
            // 2 - Avança para a etapa de preparação do workspace, executa a preparação e registra o workspace na execução
            // A preparação do workspace envolve a criação de um ambiente isolado e controlado onde todas as operações subsequentes
            // serão realizadas. Isso inclui a configuração de diretórios, a instalação de dependências necessárias, a definição de
            // variáveis de ambiente.
            // O workspace preparado nesta etapa serve como a base para todas as operações subsequentes, garantindo que elas
            // sejam executadas de maneira consistente e isolada.
            // O workspace é registrado na execução para fins de rastreabilidade e auditoria, permitindo acompanhar o ambiente em que
            // as operações subsequentes serão realizadas.
            // ===========================================================================================================================
            execution.AdvanceToStep(ExecutionStep.WorkspacePreparation);
            var workspace = await _workspacePreparationService.PrepareAsync(cancellationToken);
            execution.SetWorkspace(workspace);

            // ===========================================================================================================================
            // 3 - Avança para a etapa de aquisição da especificação, executa a aquisição e registra a especificação na execução
            // A aquisição da especificação envolve a obtenção da definição da API a partir da fonte especificada (neste caso, uma URL).
            // O processo de aquisição pode incluir etapas como download do arquivo, validação da acessibilidade da fonte, e armazenamento
            // da especificação adquirida no workspace.
            // A especificação adquirida nesta etapa é fundamental para as operações subsequentes, pois ela define a estrutura e os detalhes
            // da API que serão utilizados para gerar o cliente modernizado.
            // A especificação é registrada na execução para fins de rastreabilidade e auditoria, permitindo acompanhar a origem e o conteúdo
            // da definição da API.
            // ===========================================================================================================================
            execution.AdvanceToStep(ExecutionStep.SpecificationAcquisition);
            var specification = await _specificationAcquisitionService.AcquireAsync(specificationSource, workspace, cancellationToken);
            execution.SetSpecification(specification);

            // ===========================================================================================================================
            // 4 - Avança para a etapa de validação da especificação, executa a validação e atualiza a especificação
            // na execução com o resultado da validação
            // A validação da especificação envolve a verificação da conformidade da definição da API com os padrões e requisitos esperados.
            // O processo de validação pode incluir etapas como análise sintática, verificação de consistência, e identificação de potenciais
            // problemas ou inconsistências na definição da API.
            // O resultado da validação nesta etapa é crucial para garantir que a especificação da API seja adequada para a geração do cliente
            // modernizado, e para identificar eventuais problemas que possam impactar a qualidade ou a funcionalidade do cliente gerado.
            // A especificação é atualizada na execução com o resultado da validação para fins de rastreabilidade e auditoria, permitindo acompanhar
            // a evolução da especificação ao longo do processo de modernização.
            // ===========================================================================================================================
            execution.AdvanceToStep(ExecutionStep.SpecificationValidation);
            specification = await _specificationValidationService.ValidateAsync(specification, cancellationToken);
            execution.SetSpecification(specification);

            // ===========================================================================================================================
            // 5 - Avança para a etapa de geração do artefato de cliente, executa a geração e registra o artefato na execução
            // O artefato gerado nesta etapa é o código-fonte do cliente modernizado, que será utilizado na composição da solução
            // posterior. Ele é registrado como um artefato gerado na execução para fins de rastreabilidade e auditoria.
            // O artefato do cliente modernizado pode incluir informações como o local onde o código-fonte foi gerado, o tamanho
            // do artefato, a etapa em que foi criado e outros metadados relevantes, e ele cobre todos os arquivos de código-fonte
            // gerados para o cliente modernizado, incluindo classes, interfaces, arquivos de configuração e outros recursos relacionados
            // ao cliente.
            // ===========================================================================================================================
            execution.AdvanceToStep(ExecutionStep.ClientGeneration);
            var generatedClientArtifact = await _clientGenerationService.GenerateAsync(modernizationRequest, specification, workspace, cancellationToken);
            execution.AddArtifact(generatedClientArtifact);

            // ===========================================================================================================================
            // 6 - Avança para a etapa de composição da solução, executa a composição e registra a solução na execução
            // A composição da solução envolve a integração do artefato do cliente modernizado com outros componentes e recursos necessários
            // para formar uma solução completa e funcional. Isso pode incluir a adição de arquivos de configuração, scripts de build,
            // e outros recursos necessários para que a solução seja executável e implantável.
            // A solução composta nesta etapa é o resultado final do processo de modernização, e ela é registrada na execução para fins de
            // rastreabilidade e auditoria, permitindo acompanhar a evolução da solução ao longo do processo de modernização.
            // ===========================================================================================================================
            execution.AdvanceToStep(ExecutionStep.SolutionComposition);
            var solution = await _solutionCompositionService.ComposeAsync(modernizationRequest, workspace, generatedClientArtifact, cancellationToken);
            execution.SetSolution(solution);

            // ===========================================================================================================================
            // 7 - Avança para a etapa de geração do pacote, executa a geração e registra o artefato do pacote na execução
            // A geração do pacote envolve a criação de um arquivo compactado (como um .zip) que contém a solução composta, incluindo
            // o código-fonte, arquivos de configuração, scripts de build e outros recursos necessários para a execução e implantação da solução.
            // ===========================================================================================================================
            execution.AdvanceToStep(ExecutionStep.Packaging);
            var packageArtifact = await _packageGenerationService.GenerateAsync(solution, workspace, cancellationToken);
            execution.AddArtifact(packageArtifact);
            solution.MarkAsPackaged();

            execution.Complete();

            return new GenerateModernizedClientResponse
            {
                Success = true,
                Message = "Modernized solution generated successfully.",
                ExecutionId = execution.Id.ToString(),
                SolutionRootPath = solution.RootPath,
                PackagePath = packageArtifact.Location.FullPath
            };
        }
        catch (Exception ex)
        {
            if (execution is not null && execution.Status != ExecutionStatus.Completed)
            {
                execution.Fail(ex.Message);
            }

            return new GenerateModernizedClientResponse
            {
                Success = false,
                Message = ex.Message,
                ExecutionId = execution?.Id.ToString()
            };
        }
    }

    private static void ValidateRequest(GenerateModernizedClientRequest request)
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.SpecificationUrl))
            throw new ArgumentException("Specification URL is required.", nameof(request.SpecificationUrl));

        if (string.IsNullOrWhiteSpace(request.ProjectName))
            throw new ArgumentException("Project name is required.", nameof(request.ProjectName));

        if (string.IsNullOrWhiteSpace(request.BaseNamespace))
            throw new ArgumentException("Base namespace is required.", nameof(request.BaseNamespace));
    }
}
