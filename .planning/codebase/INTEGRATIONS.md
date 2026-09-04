# External Integrations

**Analysis Date:** 2026-07-04

## APIs & External Services

**None detected.** This is a standalone Revit add-in with no HTTP clients, REST API calls, or external service SDKs. All computation is local (in-memory) or via the Revit API.

## Data Storage

**Databases:**
- None. No SQL database, ORM (Entity Framework), or database driver packages found. All data is ephemeral (in-memory) or persistent in Revit document models via `Autodesk.Revit.DB`.

**File Storage:**
- Local filesystem only. CAD import/family conversion workflows write temporary files to system temp (see `CadTempDwgExtractionService`, `FamilyTempFileCleanupService` in `src/Services/`). Logs written to `%APPDATA%\LECG\Logs`.

**Caching:**
- In-memory only via `Microsoft.Extensions.Caching.Memory` (configured in `Bootstrapper.cs:70`): `IMemoryCache` and `IAppMemoryCache`. Examples: material symbol caches in `MaterialTextureLookupService.cs`, element lookup caches during batch operations.

## Authentication & Identity

**Auth Provider:**
- None. Revit handles user authentication. LECG inherits the Revit user context and document-level permissions. No login, OAuth, or credential management in LECG code.

**Implementation:**
- Commands run in Revit's `IExternalCommand.Execute` context with `UIDocument` and `Document` provided by Revit. Document access is controlled by Revit's user session.

## Monitoring & Observability

**Error Tracking:**
- None. No external error reporting service (Sentry, Application Insights, etc.). Errors are logged locally only.

**Logs:**
- Serilog file-based logging to `%APPDATA%\LECG\Logs\lecg-YYYY-MM-DD.log` (daily rolling). Rolling interval: daily; retained files: 14 days; file size limit: 5 MB per file; output template: `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}`.
  - Implementation: `src/Services/Infrastructure/Logging/SerilogBootstrapper.cs:18-38`
  - Fallback: If Serilog initialization fails (e.g., permission denied on log directory), falls back to `Microsoft.Extensions.Logging.ILoggerFactory` with `LogLevel.Warning` minimum (see `:42-50`)
  - Initialization: Called at `App.OnStartup`, before DI container is built; startup warnings are buffered and replayed after logger singleton is available

## CI/CD & Deployment

**Hosting:**
- Revit add-in (standalone executable plugin). Deployed as a set of files to `%APPDATA%\Autodesk\Revit\Addins\2026\LECG\`:
  - `LECG.dll` (main assembly)
  - `LECG.pdb` (debug symbols, optional for production)
  - `LECG.deps.json` (dependency manifest for .NET runtime)
  - Manifest file: `LECG.addin` (XML file in `Addins\2026\` parent, registered manually; see `docs/deployment/README.md`)

**CI Pipeline:**
- GitHub Actions (`.github/workflows/ci.yml`):
  - Trigger: push and pull_request to any branch
  - Runner: `windows-latest` (GitHub-hosted)
  - Steps: Checkout → Setup .NET 8 SDK → Restore → Build (LECG.Core with warnings-as-errors) → Build full solution (with `SkipRevitDeploy=true`) → Run tests (xUnit) → Collect coverage (Cobertura XML) → Publish coverage summary → Enforce 90% line coverage threshold
  - Timeout: 20 minutes
  - Key detail: `SkipRevitDeploy=true` is set automatically by build (`.csproj:13` detects `GITHUB_ACTIONS` or `CI` env var), preventing deploy step in CI
  - Coverage: Enforced minimum 90% line coverage; failure if below threshold

## Environment Configuration

**Required env vars:**
- None explicitly required. Optional environment variables:
  - `GITHUB_ACTIONS`, `CI` - Auto-detected by build to enable `SkipRevitDeploy`
  - Revit-supplied: Revit passes `UIControlledApplication` to `App.OnStartup`; no env vars needed

**Secrets location:**
- None. No API keys, credentials, or secrets in code or config. Credentials are not applicable to this add-in (Revit authentication is platform-level).

**Logging path:** `%APPDATA%\LECG\Logs` (created on first run; writable by current user)

## Webhooks & Callbacks

**Incoming:**
- None. This is a client-side Revit plugin; no HTTP server or webhook endpoints.

**Outgoing:**
- None. No HTTP POST/PUT calls to external services. Internal use of `IExternalEventHandler` and `ExternalEvent` for modeless command flows (e.g., `CategoryChangerCommand`, `ConvertCadCommand`), but these are Revit-internal mechanisms, not external callbacks.

## Assembly Version Conflicts

**Known risk (logged by Revit, not a fatal error):**
- Clipper2Lib: LECG references 2.0.0; Revit journal logs conflict with pre-loaded 1.1.1.0 from other add-ins. First-loaded version wins in the shared AppDomain. Impact: geometry operations in `AlignEdgesCommand` may silently use the wrong Clipper2 version depending on add-in load order.
  - Evidence: `docs/ai/repo-context.md` Concerns section, `journal.0879.txt` 2026-07-04
  - Mitigation: App.cs includes an `AssemblyResolve` handler (documented in `docs/deployment/README.md`), but conflicts are still logged
  - Debugging: Check journal for assembly version lines if geometry/DI issues appear only in Revit (not in tests)

- Microsoft.Extensions.DependencyInjection.Abstractions: LECG references 8.0.0; conflict logged with pre-loaded 9.0.0 from other add-ins. DI still works, but first-loaded version's API surface is used.

---

## Practice Candidates

**[Observed]** (scope: project) Logging is local file-based (Serilog), never cloud/remote: `%APPDATA%\LECG\Logs` with 14-day retention and 5 MB per file.
  - Evidence: `src/Services/Infrastructure/Logging/SerilogBootstrapper.cs:18-38`; `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` hardcoded path
  - Why it matters: Logs are user-local; sensitive Revit document info stays off-cloud; no privacy/compliance risk; no external service dependency for observability
  - Preserve by: When adding new logging, write to Serilog context (injected logger); never add cloud telemetry or external API logging without explicit request and security review

**[Observed]** (scope: project) Deployment is manual MSBuild target, not automatic CI/CD pipeline push: `DeployToRevit` target copies files to the per-user `%APPDATA%\Autodesk\Revit\Addins6\LECG` folder.
  - Evidence: `LECG.csproj:61-72`; CI disables deploy via `SkipRevitDeploy=true`; live `.addin` manifest is not git-tracked (stored only in `docs/deployment/LECG.addin.template`)
  - Why it matters: Enables offline development; prevents accidental overwrites during normal CI/CD; explicit deployment step for manual testing
  - Preserve by: To deploy to Revit, run `dotnet build` locally (no `-p:SkipRevitDeploy=true` flag); do not add auto-push to the add-in folder in CI; manifest updates require manual verification

**[Inferred]** (scope: repo-wide) No external integrations = no credential/secret management needed in code, config, or `.env` files.
  - Evidence: No `.env` files, no credential stores, no API key handling anywhere in `src/`; `docs/ai/repo-context.md` confirms no external services
  - Why it matters: Simplifies security model; no risk of credential leaks; no need for secrets rotation or vault setup
  - Preserve by: If external API/service is ever needed in future, add credential handling as explicit new feature with security review; never hard-code keys

**[Inferred]** (scope: project) Temporary files cleanup is explicit, not implicit: CAD conversion and family workflows create temp files and have dedicated cleanup services.
  - Evidence: `ICadTempDwgExtractionService`, `FamilyTempFileCleanupService` in `src/Services/`; cleanup logic in `*FinalizeService` classes
  - Why it matters: Prevents disk space leaks; temp files are cleaned even if a command is cancelled or errors
  - Preserve by: When adding temp file workflows, create a corresponding cleanup service; call it in try/finally or dispose pattern

**[Concern]** (scope: project, runtime) Assembly version conflicts in Revit's shared AppDomain: Clipper2Lib 2.0.0 vs 1.1.1.0 and Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0 vs 9.0.0 are logged at startup. First-loaded version wins, potentially causing silent behavior divergence.
  - Evidence: `journal.0879.txt` (2026-07-04 session) `API_ERROR { Assembly version conflict in some references in LECG.dll assembly ... }`; `docs/ai/repo-context.md:103-104`
  - Why it matters: Geometry code (Clipper2-dependent `AlignEdgesCommand`) may silently use wrong version; hard to debug
  - Mitigation: `App.cs` has `AssemblyResolve` handler (documented in `docs/deployment/README.md`); conflicts are logged but don't prevent load
  - Preserve by: When debugging oddities in Revit that don't reproduce in tests, check journal for assembly version conflict lines first; consider version-pinning strategy if conflicts worsen

**[Open]** (scope: unknown) Linked model coordinate transforms: `LinkedModelExportService` exists but no centralized transform API found.
  - Evidence: `src/Services/Infrastructure/LinkedModelExportService.cs` exists; re-checked 2026-07-04, no `GetTotalTransform` or shared transform helper identified
  - Why it matters: Linked models may require coordinate system adjustments; inconsistent handling could cause geometry misalignment
  - Action: Document (or create) canonical transform strategy if linked model commands are added or expanded

