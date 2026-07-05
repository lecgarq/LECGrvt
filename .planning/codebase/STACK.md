# Technology Stack

**Analysis Date:** 2026-07-04

## Languages

**Primary:**
- C# (.NET 8) - Add-in code and tests; target framework `net8.0-windows` for the main add-in, `net8.0` for shared library `LECG.Core`

## Runtime

**Environment:**
- .NET SDK 8.0.415 (specified in `global.json:4`)
- Platform: Windows only (x64 architecture; `Platforms=x64` in all `.csproj` files)
- Roll-forward policy: `rollForward: latestFeature` (use latest feature version within major.minor)

**Package Manager:**
- NuGet (implicit; no custom config file needed; reference assemblies from Nice3point)
- Lockfile: `packages.lock.json` implicitly managed by .NET restore

## Frameworks

**Core:**
- WPF (`UseWPF=true` in `LECG.csproj:6`) - Desktop UI framework for Windows forms and dialogs
- Revit 2026 API - via NuGet reference assemblies: `Nice3point.Revit.Api.RevitAPI` 2026.4.10 and `Nice3point.Revit.Api.RevitAPIUI` 2026.4.10 (`LECG.csproj:25-26`). These are reference-only (not copied to output; Revit provides real DLLs at runtime).

**MVVM & UI:**
- CommunityToolkit.Mvvm 8.2.2 (`LECG.csproj:32`) - MVVM patterns with source generators; `BaseViewModel` inherits `ObservableObject` (`src/ViewModels/BaseViewModel.cs:10`)

**Validation:**
- FluentValidation 11.11.0 (`LECG.csproj:33`) - Fluent-style validators; validators registered in `Bootstrapper.ConfigureServices` via `AddValidatorsFromAssemblyContaining` (`src/Core/Bootstrapper.cs:77`)

**Logging:**
- Serilog 4.2.0 (`LECG.csproj:41`) - Structured logging library
- Serilog.Extensions.Logging 8.0.0 (`LECG.csproj:42`) - Integration with Microsoft.Extensions.Logging
- Serilog.Sinks.File 6.0.0 (`LECG.csproj:43`) - File sink for rolling logs to `%APPDATA%\LECG\Logs` (`src/Services/Infrastructure/Logging/SerilogBootstrapper.cs:18-37`)

**Dependency Injection:**
- Microsoft.Extensions.DependencyInjection (implicit; no explicit version pin, but targets .NET 8 standard) - Service container; initialized in `Bootstrapper.Initialize()` (`src/Core/Bootstrapper.cs:20-51`)
- Scrutor 5.0.1 (`LECG.csproj:40`) - DI utilities (though not heavily used; most registrations are manual for clarity)

**Caching:**
- Microsoft.Extensions.Caching.Memory 8.0.1 (`LECG.csproj:34`) - In-memory cache provider

**Geometry & Math:**
- Clipper2 2.0.0 (`LECG.csproj:29`) - Polygon clipping and set operations (used in `src/Services/Alignment/` for edge alignment)
- geometry3Sharp 1.0.324 (`LECG.csproj:30`) - 3D geometry utilities and vector math
- Unofficial.Triangle.NET 0.0.1 (`LECG.csproj:44`) - Triangle mesh generation and manipulation

**Utilities:**
- System.Windows.Extensions 8.0.0 (`LECG.csproj:31`) - Additional WPF/Windows APIs

## Key Dependencies

**Critical:**
- `Nice3point.Revit.Api.*` 2026.4.10 - Revit API bindings; failure breaks all Revit document operations. Reference-only package allows build on CI runners without Revit installed.
- `CommunityToolkit.Mvvm` 8.2.2 - All ViewModels depend on source-generated change notifications; removing breaks UI binding
- `Serilog` 4.2.0 - Logging is initialized at startup; initialization failure logs to stderr fallback but add-in continues (see `src/Services/Infrastructure/Logging/SerilogBootstrapper.cs:42-50`)

**Infrastructure:**
- `Microsoft.Extensions.DependencyInjection` - Core DI container at runtime; misconfiguration breaks service resolution in `ServiceLocator.GetRequiredService<T>()`
- `Microsoft.Extensions.Caching.Memory` 8.0.1 - In-memory cache for performance-sensitive lookups (e.g., material symbol cache in `MaterialTextureLookupService`)
- `FluentValidation` 11.11.0 - Validator discovery at bootstrap time; no runtime impact if validators are skipped (graceful degradation)

**Geometry:**
- `Clipper2` 2.0.0 - Critical for `AlignEdgesCommand` vertex classification; note assembly version conflict logged in Revit journal (journal also loads Clipper2Lib 1.1.1.0 from other add-ins; first-loaded version wins in shared AppDomain) - see `docs/ai/repo-context.md` Concerns
- `geometry3Sharp` 1.0.324 - Used in topography/geometry services; no fallback if missing

## Configuration

**Environment:**
- Logging: configured via environment (logs written to `%APPDATA%\LECG\Logs`, hardcoded path in `SerilogBootstrapper.cs:18-21`)
- DI: configured in `src/Core/Bootstrapper.cs` at `App.OnStartup` (no config files; all registrations in code)
- No `.env` files or external configuration files; all settings are code-based or code-initialized

**Build:**
- `LECG.csproj:61-72` - `DeployToRevit` MSBuild target copies DLL/PDB/deps.json to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG` unless `SkipRevitDeploy=true`
- `LECG.csproj:13` - Automatic `SkipRevitDeploy=true` when `GITHUB_ACTIONS` or `CI` environment variable is set
- `Directory.Build.props` - Shared build settings: version prefix 0.1.1, warning suppressions (CS0436 for MVVM duplicates, MSB3277 for Revit DLL version overlaps)
- `Directory.Solution.props:3-4` - Serial build (parallel disabled) for consistent output

## Platform Requirements

**Development:**
- Windows only (x64)
- .NET 8 SDK 8.0.415 or later (rollForward: latestFeature)
- Optional: Revit 2026 installed locally for interactive testing (not required for build; Nice3point reference assemblies compile the code)

**Production (Runtime):**
- Revit 2026 (tested: 2026.4.10 API version) on Windows x64
- Machine-wide deployment: `.addin` manifest at `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` (not generated by build; manually maintained)
- DLL/PDB/deps.json deployed to `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG\LECG.dll` etc. by `DeployToRevit` MSBuild target
- Logs directory created on first run: `%APPDATA%\LECG\Logs`

---

## Practice Candidates

**[Observed]** (scope: repo-wide) Reference assemblies strategy for Revit API: `Nice3point.Revit.Api.RevitAPI/RevitAPIUI` are reference-only (Private=false, not copied to output). This enables builds on CI runners without Revit installed, while allowing Revit itself to provide the real runtime DLLs.
  - Evidence: `LECG.csproj:20-26` (comment + NuGet refs); `LECG.Tests/LECG.Tests.csproj:30-37` (comment + same refs); `.github/workflows/ci.yml:29` (build runs on windows-latest without Revit)
  - Why it matters: Enables fast CI/CD without Revit licenses/installations; reduces machine setup overhead
  - Preserve by: Keep reference-only strategy for Revit API; any Revit upgrade should source from Nice3point NuGet to maintain this pattern; never use HintPath to local Revit install

**[Observed]** (scope: repo-wide) Geometry stack: Clipper2 (polygon clipping), geometry3Sharp (3D utilities), Triangle.NET (triangulation). No single unified geometry library.
  - Evidence: `LECG.csproj:29-30,44`; used across `src/Services/Alignment/`, `src/Services/Topography/`, `src/Utilities/`
  - Why it matters: Each library fills a gap; Clipper2 is critical for deterministic polygon operations; geometry3Sharp for matrix/vector math; Triangle for mesh generation. Removing any breaks specific command chains.
  - Preserve by: When adding geometry-heavy features, check if Clipper2/geometry3Sharp/Triangle.NET already solve it before adding new dependencies

**[Observed]** (scope: repo-wide) Version lock: SDK 8.0.415 via `global.json`, framework lock via `net8.0-windows` in `.csproj`, package versions pinned (e.g., Clipper2 2.0.0 not 2.0+).
  - Evidence: `global.json:4`, `LECG.csproj:3`, individual `PackageReference` Version attributes
  - Why it matters: Prevents silent behavior changes from minor/patch upgrades (e.g., Clipper2 1.1 vs 2.0 algorithm differences); CI/CD reproducibility
  - Preserve by: Update versions deliberately, not via auto-increment ranges; test geometry/validation changes after SDK/framework upgrades

**[Observed]** (scope: repo-wide) Strict null handling and implicit usings: `Nullable=enable`, `ImplicitUsings=enable` in all `.csproj` files.
  - Evidence: `LECG.csproj:4-5`, `LECG.Core/LECG.Core.csproj:3-4`, `LECG.Tests/LECG.Tests.csproj:3-4`
  - Why it matters: Enforces null-safety at compile-time; reduces NullReferenceException risk; implicit usings reduce boilerplate
  - Preserve by: Keep these flags enabled; use `!` null-forgiving operator only with explicit comments; new code should not suppress these

**[Observed]** (scope: build pipeline) Clean build discipline: `Directory.Build.props` explicitly suppresses known, expected warnings (CS0436 MVVM duplicates, MSB3277 Revit DLL overlaps) with comments explaining why.
  - Evidence: `Directory.Build.props:11-18`; CI runs with `TreatWarningsAsErrors=true` for LECG.Core and full solution
  - Why it matters: Build is clean at 0 warnings; new warnings surface real issues instead of getting lost in noise
  - Preserve by: Do not add new NoWarn suppressions without comments; run CI check on all changes; if a warning appears, fix it or explicitly document why it's benign

**[Inferred]** (scope: project) Deployment is automatic via MSBuild: plain `dotnet build` deploys to live Revit addins folder; `SkipRevitDeploy=true` bypasses deployment.
  - Evidence: `LECG.csproj:61-72` (DeployToRevit target); `LECG.csproj:13` (auto-enable in CI); `docs/ai/repo-context.md` Concerns section; agent guideRAIL documented
  - Why it matters: Agents building for validation must use `-p:SkipRevitDeploy=true` to avoid clobbering a running Revit session
  - Preserve by: Document in AGENTS.md or README that plain `dotnet build` is deployment; safe default for agents is `dotnet build -p:SkipRevitDeploy=true`

**[Inferred]** (scope: project) No external databases, APIs, or cloud services: all computation is in-memory or within Revit's document model.
  - Evidence: No `HttpClient`, `SqlClient`, `EntityFrameworkCore`, or cloud SDK imports found in `src/` grep; all data flows in `Revit.DB.Document` or in-process memory
  - Why it matters: Simplifies deployment (no backend setup), scaling (no network bottlenecks), and security (no external credentials); enables offline-first UX
  - Preserve by: When new features are needed, prefer compute-local or Revit-native solutions; if external service is added, document the integration point and credential handling

