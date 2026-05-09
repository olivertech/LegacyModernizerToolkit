namespace LegacyModernizer.Web.Controllers;

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

    [HttpGet]
    public IActionResult Index()
    {
        return View(new GenerateModernizedClientViewModel());
    }

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
        var downloadToken = response.Success && !string.IsNullOrWhiteSpace(response.PackagePath)
            ? _downloadTokenService.IssueToken(response.PackagePath)
            : null;

        var resultViewModel = new GenerateModernizedClientResultViewModel
        {
            Success = response.Success,
            Message = response.Message,
            ExecutionId = response.ExecutionId,
            SolutionRootPath = response.SolutionRootPath,
            PackagePath = response.PackagePath,
            DownloadToken = downloadToken
        };

        return View("Result", resultViewModel);
    }

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
