# Kiota Output Generator Fix Tasks

## Overview

This document tracks the execution of fixes to the LegacyModernizer.Generation code generator to eliminate CS0029, CS1977 errors and reduce CS0618, CS8603 warnings in generated modernized solutions.

**Progress**: 0/2 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [ ] TASK-001: Atomic generator bug fixes
**References**: Plan §Migration Strategy, Plan §Project-by-Project Plans, Plan §Error & Warning Mapping

- [ ] (1) Update `ResolveKiotaOperation` method in `SolutionCompositionService.cs` to correctly map operation by path + access expression per Plan §Migration Steps item 1
- [ ] (2) Update `ResolveReturnType` method in `SolutionCompositionService.cs` to identify collection wrappers with `Value`/`Items` properties per Plan §Migration Steps item 2
- [ ] (3) Update `KiotaOutputInspectionService.cs` to detect `*Response` types containing `public List<T> Value` or `ICollection<T> Value` and propagate `IsCollection` metadata per Plan §Project-by-Project Plans (Kiota inspector section)
- [ ] (4) Update `BuildKiotaCallExpression` method in `SolutionCompositionService.cs` to generate typed calls avoiding dynamic/object lambda per Plan §Migration Steps item 3
- [ ] (5) Update `BuildKiotaBuilderChain` method in `SolutionCompositionService.cs` to prefer `ByXxx()` methods and typed indexer over obsolete string indexer per Plan §Migration Steps item 4
- [ ] (6) Adjust return type generation in facade methods to use nullable types (`T?`) or coalesce to empty collections per Plan §Migration Steps item 5
- [ ] (7) Distinguish direct collection returns (`Task<List<T>>`) from wrapper responses with `.Value` property and generate appropriate return expression per Plan §Migration Steps item 5
- [ ] (8) Build `LegacyModernizer.Generation` project
- [ ] (9) Project builds with 0 errors (**Verify**)
- [ ] (10) Commit changes with message: "TASK-001: Fix all generator issues for Kiota output"

---

### [ ] TASK-002: Validate fixes by regenerating modernized solution
**References**: Plan §Testing & Validation Strategy, Plan §Success Criteria

- [ ] (1) Regenerate modernized solution (e.g., Ark.Infrastructure) using updated generator
- [ ] (2) Execute `dotnet build` on generated solution
- [ ] (3) CS0029 and CS1977 errors eliminated (**Verify**)
- [ ] (4) CS0618 and CS8603 warnings reduced compared to baseline per Plan §Success Criteria
- [ ] (5) Generated solution builds with 0 errors (**Verify**)
- [ ] (6) Commit validation results with message: "TASK-002: Validate generator fixes"

---