namespace LegacyModernizer.Generation.Parsing
{
    public sealed class SpecificationValidationService : ISpecificationValidationService
    {
        public async Task<ApiSpecification> ValidateAsync(ApiSpecification specification,
                                                          CancellationToken cancellationToken = default)
        {
            if (specification is null)
                throw new ArgumentNullException(nameof(specification));

            if (string.IsNullOrWhiteSpace(specification.LocalPath))
                throw new InvalidOperationException("Specification local path was not defined.");

            if (!File.Exists(specification.LocalPath))
                throw new FileNotFoundException("Specification file was not found.", specification.LocalPath);

            var content = await File.ReadAllTextAsync(specification.LocalPath, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                specification.MarkValidationStatusAsInvalid();
                throw new InvalidOperationException("Specification content is empty.");
            }

            var isValid = specification.Format switch
            {
                SpecificationFormat.Json => ValidateJson(content),
                SpecificationFormat.Yaml => ValidateYaml(content),
                _ => false
            };

            if (!isValid)
            {
                specification.MarkValidationStatusAsInvalid();
                throw new InvalidOperationException("The specification is not a valid OpenAPI/Swagger document.");
            }

            specification.MarkValidationStatusAsValid();
            return specification;
        }

        private static bool ValidateJson(string content)
        {
            try
            {
                using var document = JsonDocument.Parse(content);

                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object)
                    return false;

                /*
                 * FORMATOS OPENAPI/Swagger:
                 * 
                 * {
                 *     "openapi": "3.0.1",
                 *     ...
                 * }
                 * 
                 * {
                 *    "swagger": "2.0",
                 *   ...
                 * }
                 * 
                */

                return root.TryGetProperty("openapi", out _) ||
                       root.TryGetProperty("swagger", out _);
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool ValidateYaml(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return lines.Any(line =>
                line.StartsWith("openapi:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("swagger:", StringComparison.OrdinalIgnoreCase));
        }
    }
}
