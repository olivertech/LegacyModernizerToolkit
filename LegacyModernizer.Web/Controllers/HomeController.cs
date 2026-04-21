using LegacyModernizer.Application.Contracts.Services;
using LegacyModernizer.Application.DTOs.Requests;
using LegacyModernizer.Web.ViewModels;

namespace LegacyModernizer.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly IGenerateModernizedClientUseCase _generateModernizedClientUseCase;

    public HomeController(IGenerateModernizedClientUseCase generateModernizedClientUseCase)
    {
        _generateModernizedClientUseCase = generateModernizedClientUseCase ?? throw new ArgumentNullException(nameof(generateModernizedClientUseCase));
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
            SpecificationUrl = model.SpecificationUrl,
            ProjectName = model.ProjectName,
            BaseNamespace = model.BaseNamespace
        };

        // Chama o caso de uso para gerar o cliente modernizado
        var response = await _generateModernizedClientUseCase.ExecuteAsync(request, cancellationToken);

        // Prepara o ViewModel para exibir os resultados
        var resultViewModel = new GenerateModernizedClientResultViewModel
        {
            Success = response.Success,
            Message = response.Message,
            ExecutionId = response.ExecutionId,
            SolutionRootPath = response.SolutionRootPath,
            PackagePath = response.PackagePath,
            DownloadToken = response.PackagePath
        };

        return View("Result", resultViewModel);
    }

    [HttpGet]
    public IActionResult Download(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid download token.");

        if (!System.IO.File.Exists(token))
            return NotFound("Package file was not found.");

        var fileName = Path.GetFileName(token);
        var contentType = "application/zip";

        return PhysicalFile(token, contentType, fileName);
    }
}
