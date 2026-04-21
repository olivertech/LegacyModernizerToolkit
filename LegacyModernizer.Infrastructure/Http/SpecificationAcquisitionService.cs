namespace LegacyModernizer.Infrastructure.Http
{

    public sealed class SpecificationAcquisitionService : ISpecificationAcquisitionService
    {
        private readonly HttpClient _httpClient;

        public SpecificationAcquisitionService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<ApiSpecification> AcquireAsync(SpecificationSource source,
                                                         Domain.Entities.Workspace workspace,
                                                         CancellationToken cancellationToken = default)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            if (workspace is null)
                throw new ArgumentNullException(nameof(workspace));

            if (!workspace.IsPrepared)
                throw new InvalidOperationException("Workspace must be prepared before acquiring the specification.");

            // Por enquanto, suportando apenas fontes do tipo URL. Suporte a arquivos locais pode ser adicionado posteriormente.
            if (source.Type != SpecificationSourceType.Url)
                throw new NotSupportedException($"Specification source type '{source.Type}' is not supported yet.");

            // Envia uma requisição HTTP GET para a URL especificada na fonte. Se a resposta não for bem-sucedida, uma exceção será lançada.
            // Se a URL responder com erro HTTP, o método já falha adequadamente.
            var response = await _httpClient.GetAsync(source.Value, cancellationToken);

            response.EnsureSuccessStatusCode();

            // Tenta ler o conteúdo retornado como string. Se for vazio ou nulo, lança uma exceção.
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Specification content is empty.");

            var format = InferFormatFromSource(source.Value);
            var fileName = format == SpecificationFormat.Yaml ? "openapi.yaml" : "openapi.json";
            var localPath = Path.Combine(workspace.Paths.InputPath, fileName);

            // Salva o conteúdo da especificação no caminho local do workspace. Se já existir um arquivo com o mesmo nome, ele será sobrescrito.
            // Salva a spec localmente e permite que as próximas etapas usem o arquivo.
            await File.WriteAllTextAsync(localPath, content, cancellationToken);

            var specification = new ApiSpecification(source);
            specification.SetLocalPath(localPath);

            return specification;
        }

        private static SpecificationFormat InferFormatFromSource(string sourceValue)
        {
            if (sourceValue.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                sourceValue.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                return SpecificationFormat.Yaml;
            }

            return SpecificationFormat.Json;
        }
    }
}
