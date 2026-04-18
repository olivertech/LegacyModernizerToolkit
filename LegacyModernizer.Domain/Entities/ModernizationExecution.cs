namespace LegacyModernizer.Domain.Entities;

/// <summary>
/// A execução do caso de uso como um todo.
/// Entidade principal do domínio e representa o processo de modernização em si, 
/// ponta a ponta, incluindo todas as etapas intermediárias,
/// desde a obtenção da especificação até a geração da solução modernizada.
/// </summary>
public sealed class ModernizationExecution
{
    public Guid Id { get; private set; }

    /// <summary>
    /// Representa a intenção original do usuário, incluindo a fonte da especificação, o nome do projeto e o namespace base.
    /// </summary>
    public ModernizationRequest Request { get; private set; }

    /// <summary>
    /// É a representação estruturada da especificação da API, obtida a partir do Request.
    /// Nasce inicialmente como null porque a execução começa antes de obter a especificação, 
    /// mas é preenchida assim que a especificação é processada.
    /// </summary>
    public ApiSpecification? Specification { get; private set; }

    /// <summary>
    /// É o ambiente temporário onde a execução ocorre, incluindo arquivos intermediários, logs e artefatos gerados.
    /// Nasce inicialmente como null porque a execução começa antes de criar o workspace, 
    /// mas é preenchido assim que o workspace é inicializado.
    /// </summary>
    public Workspace? Workspace { get; private set; }

    /// <summary>
    /// Lista que armazena os artefatos gerados ao longo do processo, como arquivos de código, logs, relatórios, etc.
    /// </summary>
    private readonly List<GeneratedArtifact> _artifacts = new();
    public IReadOnlyCollection<GeneratedArtifact> Artifacts => _artifacts.AsReadOnly();

    /// <summary>
    /// Statuses possíveis: Created, Running, Completed, Failed.
    /// </summary>
    public ExecutionStatus Status { get; private set; }

    /// <summary>
    /// Steps possíveis: InputValidation, SpecificationProcessing, WorkspaceSetup, CodeGeneration, Packaging, Completed.
    /// </summary>
    public ExecutionStep CurrentStep { get; private set; }

    /// <summary>
    /// Informa a solução modernizada final, estruturada e organizada, pronta para ser empacotada e entregue ao usuário.
    /// </summary>
    public ModernizedSolution? Solution { get; private set; }

    /// <summary>
    /// Só em caso de falha, para armazenar a mensagem de erro que explica o motivo da falha.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    public ModernizationExecution(ModernizationRequest request)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));

        Id = Guid.NewGuid();
        Status = ExecutionStatus.Created;
        CurrentStep = ExecutionStep.InputValidation;
    }

    public void Start()
    {
        EnsureNotFinished();

        Status = ExecutionStatus.Running;
    }

    public void SetSpecification(ApiSpecification specification)
    {
        Specification = specification ?? throw new ArgumentNullException(nameof(specification));
    }

    public void SetWorkspace(Workspace workspace)
    {
        Workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    public void SetSolution(ModernizedSolution solution)
    {
        Solution = solution ?? throw new ArgumentNullException(nameof(solution));
    }

    public void AdvanceToStep(ExecutionStep step)
    {
        EnsureRunning();

        CurrentStep = step;
    }

    public void AddArtifact(GeneratedArtifact artifact)
    {
        if (artifact is null)
            throw new ArgumentNullException(nameof(artifact));

        _artifacts.Add(artifact);
    }

    public void RemoveArtifact(Guid artifactId)
    {
        var artifact = _artifacts.FirstOrDefault(a => a.Id == artifactId);
        if (artifact is null)
            throw new ArgumentException("Artifact with the specified ID does not exist.", nameof(artifactId));

        _artifacts.Remove(artifact);
    }

    public void ClearArtifacts()
    {
        _artifacts.Clear();
    }

    public void Complete()
    {
        EnsureRunning();

        Status = ExecutionStatus.Completed;
        CurrentStep = ExecutionStep.Completed;
    }

    public void Fail(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

        Status = ExecutionStatus.Failed;
        ErrorMessage = errorMessage.Trim();
    }

    private void EnsureRunning()
    {
        if (Status != ExecutionStatus.Running)
            throw new InvalidOperationException("Execution must be running to perform this operation.");
    }

    private void EnsureNotFinished()
    {
        if (Status == ExecutionStatus.Completed || Status == ExecutionStatus.Failed)
            throw new InvalidOperationException("Execution has already finished.");
    }
}
