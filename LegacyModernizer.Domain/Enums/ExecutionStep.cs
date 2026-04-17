namespace LegacyModernizer.Domain.Enums;

public enum ExecutionStep
{
    InputValidation,
    WorkspacePreparation,
    SpecificationAcquisition,
    SpecificationValidation,
    ClientGeneration,
    SolutionComposition,
    ArtifactGeneration,
    Packaging,
    Completed
}
