# Kiota Output Fix Tasks

## Overview

This document tracks fixes to the LegacyModernizer.Generation code generator to eliminate build errors (CS0029, CS1977) and reduce warnings (CS0618, CS8603) in generated solutions. All generator corrections will be applied in two logical phases, followed by validation.

**Progress**: 0/3 tasks complete (0%) ![0%](https://progress-bar.xyz/0)

---

## Tasks

### [▶] TASK-001: Fix operation/return resolution and collection mapping
**References**: Plan §Project: LegacyModernizer.Generation (steps 1-2), Plan §Project: LegacyModernizer.Generation (Kiota inspector)

- [ ] (1) Update `ResolveKiotaOperation` method in `SolutionCompositionService.cs` to correctly map operations by path + access expression per Plan §Migration Steps item 1
- [ ] (2) Update `ResolveReturnType` method in `SolutionCompositionService.cs` to identify collections in `*Response` types (Value/Items properties) per Plan §Migration Steps item 2
- [ ] (3) Update `KiotaOutputInspectionService.cs` to detect `*Response` wrappers containing `public List<T> Value` or `ICollection<T> Value` per Plan §Project: LegacyModernizer.Generation (Kiota inspector)
- [ ] (4) Propagate `IsCollection` and `ReturnTypeName` metadata with actual item types per Plan §Project: LegacyModernizer.Generation (Kiota inspector)
- [ ] (5) Build `LegacyModernizer.Generation` project and verify no new compilation errors introduced (**Verify**)
- [ ] (6) Commit changes with message: "TASK-001: Fix operation/return resolution and collection mapping (resolves CS0029)"

---

### [ ] TASK-002: Fix builder chain generation and nullability
**References**: Plan §Project: LegacyModernizer.Generation (steps 3-5)

- [ ] (1) Update `BuildKiotaCallExpression` method in `SolutionCompositionService.cs` to generate typed calls and avoid dynamic lambda expressions per Plan §Migration Steps item 3
- [ ] (2) Update `BuildKiotaBuilderChain` method in `SolutionCompositionService.cs` to prefer `ByXxx()` methods or typed indexers over obsolete `this[string]` indexer per Plan §Migration Steps item 4
- [ ] (3) Adjust return type signatures in generated code templates to use nullable types (`T?`) or coalesce to empty collections (`result?.Value ?? []`) per Plan §Migration Steps item 5
- [ ] (4) Build `LegacyModernizer.Generation` project and verify no compilation errors (**Verify**)
- [ ] (5) Commit changes with message: "TASK-002: Fix builder chain generation and nullability (resolves CS1977, CS0618, CS8603)"

---

### [ ] TASK-003: Generate test solution and validate fixes
**References**: Plan §Testing & Validation Strategy, Plan §Success Criteria

- [ ] (1) Generate reference solution (e.g., Ark.Infrastructure) using updated generator per Plan §Testing & Validation Strategy
- [ ] (2) Generated solution created successfully (**Verify**)
- [ ] (3) Execute `dotnet build` on generated solution
- [ ] (4) Build output contains 0 CS0029 errors (**Verify**)
- [ ] (5) Build output contains 0 CS1977 errors (**Verify**)
- [ ] (6) Build output contains 0 compilation errors overall (**Verify**)
- [ ] (7) Count CS0618 and CS8603 warnings in build output and verify reduction compared to baseline per Plan §Success Criteria (**Verify**)

---
