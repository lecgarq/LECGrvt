# Close audit — v1.0 LECG Codebase Hardening

**Closed:** 2026-07-27 · **Decision:** closed as-is (gaps recorded as accepted debt in `.planning/codebase/CONCERNS.md`)

Context: the milestone was planned 2026-07-05 under the GSD process, which was retired 2026-07-25. No phase was ever formally executed, but real work landed untracked (rename safety, CI/test fix, deploy guardrails, bug fixes). Verdicts below are grounded in code read directly on 2026-07-27, not phase summaries.

**Test suite at close:** `dotnet test -c Debug -p:SkipRevitDeploy=true` → 211 passed, 0 failed, 5 skipped (Revit-runtime tests that skip by design outside Revit).

**Score: 7 met, 9 partial, 16 not met** (of 32).

| Requirement | Verdict | Evidence |
|---|---|---|
| STATE-01 | met | Flag set `src/Commands/FormulaAutoGroupingCommand.cs:156`, reset in job `Dispose()` (:251), guaranteed by `finally` in `src/Core/RevitIdlingRunner.cs:59-65` |
| STATE-02 | **not met** | `src/Core/ExternalEventCommand.cs:19-27` — `Raise()` with no `IsPending` check; zero `IsPending` hits in `src` |
| STATE-03 | **not met** | No lifecycle doc/comment in `ExternalEventCommand.cs`; static `_handler`/`_externalEvent` with no per-invocation validation |
| ERR-01 | **not met** | `LogAndIgnore` exists only in `.planning` docs; zero hits in `src`/`LECG.Core` |
| ERR-02 | **not met** | `CadTempFileCleanupService.cs:16-18` bare `catch {}` (no logger at all); `FamilyTempFileCleanupService.cs:26-29` bare catch despite injected logger |
| ERR-03 | partial | 31 silent catch blocks across 20 files in `src` (baseline 54); e.g. `SchemaCleanerService` (4), `PurgeParameterService` (4), `RibbonFactory.cs:46` |
| ERR-04 | **not met** | `.editorconfig` — no CA1031/RCS1075; baseline severity `none` |
| BUG-01 | met | `src/Services/FamilyConversion/FamilyEditorService.cs:42,66-83` — actionable message + Generic Model bridge workaround |
| REN-01 | met | `BatchRenameExecutionService.cs:206,845` formula-referenced set; `:321-344` formula rewrite in per-param SubTransaction |
| REN-02 | met | Same file `:204-205,346-373` — dimension-label reassignment with stale-reference refetch guard |
| REN-03 | partial | `FormulaUpdateService` + `LECG.Core/Rename/FormulaNameUpdater` extracted; bulk remains ~15 internal static helpers in the 1000-line service |
| REN-04 | met | `LECG.Tests/Services/BatchRenameSafeRenameTests.cs` (formula + dimension-label scenarios), `FormulaUpdateServiceTests`, `FormulaNameUpdaterTests` |
| UX-01 | partial | `TransactionService.cs:122-126` throws on rolled-back commit; `RevitCommand.cs:45-53` surfaces it — rollback not silent, but no uniform COMPLETED/ROLLED BACK status |
| UX-02 | **not met** | `RibbonService.cs:246-247` — empty availability string for all 8 Align/Distribute sub-buttons |
| SEC-01 | partial | Sanitization only in `LinkedModelExportService.cs:73,370-372`; nothing in `CadFamilySaveService`, `SettingsManager`, `FamilyEditorService`; no containment checks anywhere |
| SEC-02 | partial | Several `Path.GetFileName` log sites, but `LinkedModelExportService.cs:83` logs full path; no documented review |
| DEP-01 | **not met** | `src/App.cs OnStartup` — no version logging |
| DEP-02 | **not met** | No `ManifestSettings` in template or any `.addin`; research only |
| DEP-03 | **not met** | Moot — isolation never applied; workaround comment exists but no post-isolation verification |
| TEST-01 | **not met** | Only command-level test is `FormulaAutoGroupingCommandTests`; no Purge/AlignEdges/FixPoints/CategoryChanger command tests |
| TEST-02 | **not met** | Zero Alignment tests in LECG.Tests |
| TEST-03 | partial | Rename fixes locked by tests; no reentrancy/rollback regression tests (those fixes weren't made) |
| TEST-04 | partial→met at close | Recorded in `docs/review/14-current-debt-audit.md:6`; validated this close: `dotnet test -c Debug -p:SkipRevitDeploy=true` (repo-context.md updated, [Open] item cleared) |
| PERF-01 | **not met** | Stopwatch only in `ExecutionTimer` used by `FamilyConversionService`; no profiling of BatchRename/Purge/Alignment, no findings doc |
| PERF-02 | **not met** | No before/after measurement docs |
| DEPLOY-01 | met | `docs/deployment/README.md:33-52` + `LECG.addin.template` (documented, not scripted) |
| DEPLOY-02 | **not met** | No manifest validation in `App.OnStartup` or scripts |
| DEPLOY-03 | met | `LECG.csproj:12-13,56` CI auto-skip + conditioned deploy target; documented in deployment README |
| DOC-01 | partial | Generic guidance in `revit-protocol.md`/`SEMANTICS.md`; unit-conversion and link-transform conventions still [Open] |
| DOC-02 | **not met at audit, fixed at close** | Stale ribbon-availability claim in CONCERNS.md corrected 2026-07-27 during this close |
| VAL-01 | **not met** | `DialogWhitelist.cs:41-63` — all five entries still commented "confidence: low — unverified" |
| VAL-02 | partial | `docs/ai/revit-smoke-test.md` Run History: 2026-07-25 theme-scoping subset (A1/A2/B5 PASS); never executed end-to-end (steps 3–8 untested) |

Side finding fixed during close: `docs/deployment/README.md:67` claimed an AssemblyResolve handler in App.cs that does not exist — corrected.
