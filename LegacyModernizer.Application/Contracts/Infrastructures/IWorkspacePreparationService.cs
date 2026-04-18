namespace LegacyModernizer.Application.Contracts.Infrastructures;

public interface IWorkspacePreparationService
{
    Task<Workspace> PrepareAsync(CancellationToken cancellationToken = default);
}
