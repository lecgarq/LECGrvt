# Phase 1: Test Scaffolding + Shared Error-Handling Helper - Context

**Gathered:** 2026-07-05
**Status:** Ready for planning

<domain>
## Phase Boundary

A sanctioned, safe `dotnet test` workflow exists (TEST-04) and a reusable suppression-logging helper (`LogAndIgnore`, ERR-01) is built and piloted, ready for Phase 2's 54-site bare-catch audit. The mass catch-block audit itself, analyzer activation (CA1031/RCS1075), and broad test coverage are later phases.

**Critical scouting correction:** the CI-mode compile blocker flagged in STATE.md is ALREADY FIXED (commit `db1203f` removed the IsCiBuild split). Verified 2026-07-05: `dotnet test -p:SkipRevitDeploy=true` compiles and runs — 198 passed, 5 skipped, 0 failed. The phase is therefore "verify, harden, and document," not "diagnose and fix." Success criterion 3 (CI-mode issue resolved) is satisfiable by verification + the safety change below.

</domain>

<decisions>
## Implementation Decisions

### Safe test invocation (TEST-04)
- Make bare `dotnet test` inherently safe: add MSBuild conditioning so the `DeployToRevit` target (LECG.csproj:61) never fires when the build is triggered by a test run. No one has to remember a flag.
- Keep `dotnet build` deploying to the live add-in folder by default — the intentional-deploy workflow is unchanged (matches PROJECT.md constraint; Phase 10 owns deploy guardrail docs).
- The sanctioned invocation is the plain run (`dotnet test`). Filters (`--filter Category=...`), coverage, and watch mode are optional extras, not part of the sanctioned baseline.
- Phase 1 proves the sanctioned invocation runs green (0 failures) and documents skip-gating as the convention. CS0618 SimpleProgressReporter warnings and the 5 skipped tests are left alone — that cleanup is the separate Bucket B migration track, out of scope here.

### LogAndIgnore helper API (ERR-01)
- Form: extension method on `LECG.Services.Logging.ILogger` — call shape `_logger.LogAndIgnore(ex, reason: "...", scope: "...")`.
- Placement: new file in `src/Services/Infrastructure/Logging/` (e.g. `LoggerSuppressionExtensions.cs`), namespace `LECG.Services.Logging`, so call sites with the existing ILogger using need no extra import.
- The `reason` parameter is mandatory with no default — the compiler enforces suppress-with-reason quality for Phase 2.
- Log level: Warning, always. Satisfies ERR-02's Warning-before-suppress requirement; one consistent audit rule.
- Visibility: delegates to the existing `LogWarning(message, scope, exception)` path unchanged — entries appear in BOTH the user-visible log panel and the Serilog file. Panel's 300-entry trim bounds noise.
- Log format: greppable template `Suppressed {ExceptionType}: {reason}` — every intentional suppression findable by one keyword. Exception object passed through so Serilog captures the stack trace.
- Messages in English, matching existing log text.
- Name locked: `LogAndIgnore` (matches REQUIREMENTS.md/roadmap vocabulary).

### Suppression vocabulary (feeds Phase 2 classification)
- Two named methods, making the audit's operation-vs-item classification visible in code:
  - `LogAndIgnore(ex, reason, scope)` — operation-level suppression (whole action is best-effort).
  - `LogAndContinueItem(ex, itemLabel, reason, scope)` — per-item batch skip; carries the failed item's identity into the log so batch operations stay partial-success.
- Both log at Warning. A noisy batch SHOULD look noisy (fail-loudly milestone value).
- NO silent/no-log variant. If Phase 2 finds a site where logging is truly wrong, it keeps a plain catch with a mandatory explanatory comment — the helper never becomes a silence tool.
- Pilot in Phase 1: convert `CadTempFileCleanupService` and `FamilyTempFileCleanupService` to `LogAndIgnore` as reference usage. Validates the API on real call sites before 50+ conversions, and lands ERR-02 early as a bonus.

### Documentation (TEST-04)
- Canonical home: `docs/ai/repo-context.md`, with a one-line pointer from CLAUDE.md so every session sees it.
- Docs cover: (1) the sanctioned `dotnet test` invocation and why it's safe, (2) the skip-gating convention for Revit-runtime tests (anchor facts, `[Fact(Skip)]` vs manual smoke test — the convention Phase 8 will follow), (3) helper usage guide: when to use `LogAndIgnore` vs `LogAndContinueItem` vs plain catch-with-comment — the classification rule for Phase 2's audit.
- CI: verify `ci.yml`'s test step still passes after the csproj safety change (CI already auto-sets SkipRevitDeploy=true via LECG.csproj:13). No workflow edits unless the change breaks it.

### Claude's Discretion
- Exact MSBuild mechanism for making test-triggered builds skip deploy (property flow from LECG.Tests, test-host detection, or equivalent) — as long as bare `dotnet test` is provably safe and `dotnet build` still deploys.
- Exact file name of the extension class and unit-test structure for the helper (follow TESTING.md patterns: xUnit + FluentAssertions, `[Trait("Category", "Unit")]`, `[Method]_[Condition]_[Expected]` naming).
- Exact wording/structure of the repo-context.md documentation sections.

</decisions>

<code_context>
## Existing Code Insights

### Reusable Assets
- `LECG.Services.Logging.ILogger` (`src/Services/Infrastructure/Logging/Logger.cs:11-22`): the target interface — all methods take a required `scope` string; `LogWarning(message, scope, Exception?)` already forwards exceptions to Serilog with stack traces. The helper is a thin extension over this.
- `Logger.ForwardToStructuredLogger` (`Logger.cs:183-210`): scope becomes the Serilog category — the helper inherits structured logging for free.
- Existing test stack: xUnit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0; `Logger` public ctor is deliberately dispatcher-free for synchronous test behavior (`Logger.cs:38-43`) — the helper's unit tests can assert against `Entries` directly.
- Skip-gating + anchor-fact pattern already established (`LECG.Tests/Services/BatchRenameExecutionServiceTests.cs:28-32`).

### Established Patterns
- `ArgumentNullException.ThrowIfNull` (633 uses) — helper should follow.
- `SkipRevitDeploy` property: defaults false, auto-true on CI (`LECG.csproj:12-13`); `DeployToRevit` target fires `AfterTargets="Build"` unless skipped (`LECG.csproj:61`). The safety change hooks here.
- LECG.Tests references LECG.csproj as ProjectReference + Nice3point reference-only Revit assemblies; whole suite compiles without a Revit install (LECG.Tests.csproj comment confirms IsCiBuild split removed).
- Log messages are English, plain sentences.

### Integration Points
- Pilot call sites: `CadTempFileCleanupService` and `FamilyTempFileCleanupService` (currently suppress deletion failures silently — ERR-02's targets).
- Phase 2 consumes the two-method vocabulary at the remaining ~52 catch sites; Phase 8 consumes the documented skip-gating convention.
- CI workflows: `.github/workflows/ci.yml` (verify-only this phase).

</code_context>

<specifics>
## Specific Ideas

- Reference call shape agreed via preview: `_logger.LogAndIgnore(ex, reason: "temp file cleanup is best-effort", scope: "CadTempFileCleanup");`
- Log rendering agreed via preview — panel: `⚠ Suppressed IOException: temp file cleanup is best-effort`; Serilog: `[WRN] [CadTempFileCleanup] Suppressed IOException: ...` followed by the stack trace.
- Per-item shape: `_logger.LogAndContinueItem(ex, itemLabel: familyType.Name, reason: "rename continues with remaining types", scope: "BatchRename");`

</specifics>

<deferred>
## Deferred Ideas

- CS0618 SimpleProgressReporter obsolete-API cleanup (remaining call sites incl. ConvertCadCommand, SearchReplaceServiceTests) — pre-existing Bucket B migration track, not test scaffolding.
- `TreatWarningsAsErrors` for LECG.Tests — revisit after warning cleanup lands.
- Actively unifying ci.yml's test step with the sanctioned invocation — Phase 10 (deploy/guardrail documentation) territory if wanted.
- STATE.md stale blocker (CI-mode compile issue) should be cleared when this phase completes — housekeeping, handled by GSD state updates.

</deferred>

---

*Phase: 01-test-scaffolding-shared-error-handling-helper*
*Context gathered: 2026-07-05*
