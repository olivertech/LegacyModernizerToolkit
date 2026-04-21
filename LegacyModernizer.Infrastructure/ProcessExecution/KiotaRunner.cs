namespace LegacyModernizer.Infrastructure.ProcessExecution;

public sealed class KiotaRunner : IKiotaRunner
{
    public async Task<KiotaExecutionResult> ExecuteAsync(KiotaGenerationRequest request,
                                                         CancellationToken cancellationToken = default)  
    {
        if (request is null)
            throw new ArgumentNullException(nameof(request));

        ValidateRequest(request);

        // Montar os argumentos para a execução do processo Kiota
        var startInfo = new ProcessStartInfo
        {
            FileName = "kiota",
            Arguments = BuildArguments(request),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Atribuir os argumentos ao processo e iniciar a execução
        using var process = new Process
        {
            StartInfo = startInfo
        };

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start Kiota process. Ensure the 'kiota' CLI is installed and available in the environment PATH.",
                ex);
        }

        // Ler a saída padrão e o erro padrão de forma assíncrona, ao mesmo tempo que aguarda a conclusão do processo
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        // Aguardar a conclusão do processo
        await process.WaitForExitAsync(cancellationToken);

        // Aguardar a leitura da saída padrão e do erro padrão
        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        // Retornar o resultado da execução do processo Kiota
        return new KiotaExecutionResult
        {
            Success = process.ExitCode == 0,
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput,
            StandardError = standardError
        };
    }

    private static void ValidateRequest(KiotaGenerationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SpecificationPath))
            throw new ArgumentException("Specification path is required.", nameof(request.SpecificationPath));

        if (string.IsNullOrWhiteSpace(request.OutputPath))
            throw new ArgumentException("Output path is required.", nameof(request.OutputPath));

        if (string.IsNullOrWhiteSpace(request.ClientNamespace))
            throw new ArgumentException("Client namespace is required.", nameof(request.ClientNamespace));

        if (string.IsNullOrWhiteSpace(request.Language))
            throw new ArgumentException("Language is required.", nameof(request.Language));
    }

    private static string BuildArguments(KiotaGenerationRequest request)
    {
        return $"generate -l {request.Language} -d \"{request.SpecificationPath}\" -o \"{request.OutputPath}\" -n \"{request.ClientNamespace}\"";
    }
}
