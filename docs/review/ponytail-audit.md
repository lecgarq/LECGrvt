# Ponytail whole-repository simplification

Status: implemented and verified.

- Removed 35 single-implementation CAD interfaces; callers and dependency injection now use the existing concrete services.
- Replaced four FluentValidation validators and reflection-based registration with one explicit `ValidationRules` switch; removed FluentValidation.
- Removed `MaterialService`, `IMaterialService`, and four single-purpose forwarding services; callers now use the exact implementation they need.
- Removed `IFamilyLoadOptionsFactory` and `IRibbonService`; the existing concrete policies remain unchanged.
- Removed `SimpleProgressReporter`; CAD conversion now uses the shared `RevitCommandProgressReporter`.
- Kept the short-lived geometry cache because Revit sketch extraction is expensive and mutable, but removed its custom interface and wrapper by using `IMemoryCache` directly.

Measured production/test diff: 332 lines added, 1,409 deleted, net 1,077 lines removed across 57 deleted files and two focused replacements. One dependency removed.

Verification: `dotnet build -p:SkipRevitDeploy=true --no-restore` succeeds; `dotnet test LECG.Tests/LECG.Tests.csproj -c Debug -p:SkipRevitDeploy=true --no-restore` passes.

Live Revit 2026 smoke test: deployed the per-user build, replaced the stale duplicate ProgramData manifest, loaded the complete LECG ribbon in Autodesk's Snowdon Architectural sample, and read project information plus 5 of 1,128 walls through `lecg-revit` in 16.8 ms and 21.9 ms. No model data was modified.
