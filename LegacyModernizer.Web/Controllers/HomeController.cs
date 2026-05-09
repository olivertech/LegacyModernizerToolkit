namespace LegacyModernizer.Web.Controllers;

/// <summary>
/// Controlador principal da interface Web do Toolkit.
/// </summary>
public sealed class HomeController : Controller
{
    private readonly IGenerateModernizedClientUseCase _generateModernizedClientUseCase;
    private readonly IDownloadTokenService _downloadTokenService;

    public HomeController(IGenerateModernizedClientUseCase generateModernizedClientUseCase,
                          IDownloadTokenService downloadTokenService)
    {
        _generateModernizedClientUseCase = generateModernizedClientUseCase ?? throw new ArgumentNullException(nameof(generateModernizedClientUseCase));
        _downloadTokenService = downloadTokenService ?? throw new ArgumentNullException(nameof(downloadTokenService));
    }

    /// <summary>
    /// Exibe o formulário inicial de geração.
    /// </summary>
    [HttpGet]
    public IActionResult Index()
    {
        return View(new GenerateModernizedClientViewModel());
    }

    /// <summary>
    /// Recebe a solicitação da UI, aciona o caso de uso e prepara o resultado para a view.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(GenerateModernizedClientViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var request = new GenerateModernizedClientRequest
        {
            SpecificationUrl = model.ActiveSpecificationUrl,
            ProjectName = model.ProjectName,
            BaseNamespace = model.BaseNamespace,
            TargetFramework = model.TargetFramework,
            GenerationMode = model.GenerationMode,
            AuthenticationMode = model.AuthenticationMode,
            EmbeddedProjectPrefix = model.EmbeddedProjectPrefix
        };

        var response = await _generateModernizedClientUseCase.ExecuteAsync(request, cancellationToken);
        // O token opaco evita expor o caminho físico do pacote na interface ou em links de download.
        var downloadToken = response.Success && !string.IsNullOrWhiteSpace(response.PackagePath)
            ? _downloadTokenService.IssueToken(response.PackagePath)
            : null;

        var resultViewModel = new GenerateModernizedClientResultViewModel
        {
            Success = response.Success,
            Message = response.Message,
            ExecutionId = response.ExecutionId,
            DownloadToken = downloadToken
        };

        return View("Result", resultViewModel);
    }

    /// <summary>
    /// Faz o download do pacote gerado a partir de um token previamente emitido.
    /// </summary>
    [HttpGet]
    public IActionResult Download(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid download token.");

        if (!_downloadTokenService.TryResolvePackagePath(token, out var packagePath))
            return NotFound("Download token was not found or has expired.");

        var fileName = Path.GetFileName(packagePath);
        return PhysicalFile(packagePath, "application/zip", fileName);
    }
}
