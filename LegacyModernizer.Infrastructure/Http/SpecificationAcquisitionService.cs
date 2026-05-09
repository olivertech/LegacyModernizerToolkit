namespace LegacyModernizer.Infrastructure.Http
{

    /// <summary>
    /// Recupera a specification remota e a materializa no workspace para as próximas etapas do pipeline.
    /// </summary>
    public sealed class SpecificationAcquisitionService : ISpecificationAcquisitionService
    {
        private readonly HttpClient _httpClient;

        public SpecificationAcquisitionService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        /// <summary>
        /// Baixa a specification, infere o formato e grava uma cópia local controlada no workspace.
        /// </summary>
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

            // O fluxo atual está otimizado para URL porque esse é o cenário principal da interface Web.
            // Suporte a arquivo/local pode ser adicionado sem mudar a orquestração principal.
            if (source.Type != SpecificationSourceType.Url)
                throw new NotSupportedException($"Specification source type '{source.Type}' is not supported yet.");

            // Envia uma requisição HTTP GET para a URL especificada na fonte. Se a resposta não for bem-sucedida, uma exceção será lançada.
            // Se a URL responder com erro HTTP, o método já falha adequadamente.
            var response = await _httpClient.GetAsync(source.Value, cancellationToken);

            response.EnsureSuccessStatusCode();

            // A pipeline seguinte depende de uma cópia local física da spec, por isso o conteúdo é persistido
            // no workspace antes de qualquer inspeção ou geração.
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
            specification.SetFormat(format);
            specification.SetLocalPath(localPath);

            return specification;
        }

        private static SpecificationFormat InferFormatFromSource(string sourceValue)
        {
            // A inferência por extensão é suficiente para decidir o nome do arquivo local
            // e orientar a etapa de validação/parsing.
            if (sourceValue.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) ||
                sourceValue.EndsWith(".yml", StringComparison.OrdinalIgnoreCase))
            {
                return SpecificationFormat.Yaml;
            }

            if (sourceValue.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return SpecificationFormat.Json;
            }

            // fallback
            return SpecificationFormat.Json;
        }
    }
}
