# GSD Log

> Append-only log of GSD runs. Historical record, NOT an authority — entries reflect the code as of their date; current code wins.

## 2026-07-04 — GSD protocol smoke test (seed repo memory)

- **Purpose:** Behavioral test of the GSD protocol stack (repo-memory / revit-addin-repo / as-is-best-practices / protocol-compliance) on this repository. Documentation-only: no product code changed.
- **Decisions:** Ran the build with `-p:SkipRevitDeploy=true` (the csproj's own opt-out) instead of plain `dotnet build`, because the default build deploys DLLs into the live Revit 2026 addins folder. Did not commit — working tree carries the user's unrelated WIP.
- **Files inspected:** `src/App.cs`, `src/Core/RevitCommand.cs`, `src/Core/ExternalEventCommand.cs`, `src/Core/Bootstrapper.cs`, `src/Core/Ribbon/*` (listing), `src/ViewModels/BaseViewModel.cs`, `src/Services/Infrastructure/TransactionService.cs` (grep), `LECG.csproj`, `Directory.Build.props`, `LECG.Tests/LECG.Tests.csproj` (partial), `AGENTS.md` (grep), folder listings of `src/`, `scripts/`, `docs/`; repo-wide greps for Revit indicators and API patterns.
- **Files changed:** `docs/ai/repo-context.md` (created), `docs/ai/gsd-log.md` (created).
- **Revit detection result:** POSITIVE — Nice3point Revit 2026 API package refs (`LECG.csproj:25-26`), `IExternalApplication` (`src/App.cs:13`), `IExternalCommand` base (`src/Core/RevitCommand.cs:14`), 348 files using `Autodesk.Revit.DB`, 65 using `Autodesk.Revit.UI`, 39 `.xaml`, `Views/`/`ViewModels/`/`Services/` folders, ribbon in `src/Core/Ribbon/`, deploy target to `Addins\2026`. No `.addin` manifest tracked in repo (logged as [Open]).
- **Revit surface:** none modified (documentation-only run).
- **API context:** none used — no Revit interaction.
- **Transactions:** none (read-only).
- **Modeless/ExternalEvent:** none.
- **Validation:** `dotnet build -p:SkipRevitDeploy=true` → Build succeeded, 0 warnings, 0 errors (LECG.Core, LECG, LECG.Tests). Read-only greps + `git status` before/after (only `docs/ai/` added).
- **Revit runtime validation:** not executed — Revit was not opened. Pending smoke test: open Revit 2026, confirm LECG ribbon tab loads, run any command, verify with active document and empty selection.

## Lessons Learned

### Best Practices Observed
- Writes via `ITransactionService` (~71 usages vs 7 raw `new Transaction(`); commands via `RevitCommand`/`ExternalEventCommand<T>` bases; CommunityToolkit.Mvvm with `BaseViewModel`; DI composition root in `Bootstrapper`. Filed as ledger entries in repo-context.md.

### Concerns / Pitfalls
- Plain `dotnet build` deploys to the live Revit addins folder (`LECG.csproj:60-72`) — always pass `-p:SkipRevitDeploy=true` for validation-only builds.
- `pack://` URI registration in `App.OnStartup` (`src/App.cs:22-27`) is a load-bearing workaround; `ExternalEventCommand` static handler/event state is shared per command type.

### Decisions to Preserve
- `Directory.Build.props` CS0436 suppression and MSB3277 downgrade are deliberate, documented clean-build measures — not cleanup targets.

### Future-Agent Warnings
- Prior session-memory claims that CI could not compile the test project appear outdated: `LECG.Tests.csproj:34` now states the suite "compiles and runs on CI runners (no IsCiBuild split needed)", and the test project builds locally. Verify against current code; do not act on the old claim.
- The working tree routinely carries user WIP — do not commit repo memory updates together with unrelated modified files; stage `docs/ai/` paths explicitly.

### Open Questions
- `.addin` manifest location/ownership; collector conventions; canonical unit-conversion helper; linked-model transform handling; sanctioned `dotnet test` invocation. (Detailed in repo-context.md → Open Questions.)

## 2026-07-04 — Repo-memory stabilization pass

- **Purpose:** Stabilize the first GSD memory snapshot created during the smoke test. Documentation-only: no product code changed; only `docs/ai/` touched.
- **Files inspected:** `docs/ai/repo-context.md`, `docs/ai/gsd-log.md`, `LECG.csproj` (full), `docs/deployment/README.md`, `docs/deployment/LECG.addin.template`, live environment `C:\ProgramData\Autodesk\Revit\Addins\2026\` (+ `LECG\` subfolder) and `%AppData%\Autodesk\Revit\Addins\2026` (absent), live `LECG.addin` content, `git ls-files docs/deployment`, `git status docs/ai docs/deployment`, `git log --all -- "*.addin*"`, greps: `SkipRevitDeploy` (repo-wide), `\.addin` (repo-wide), `GetTotalTransform|GetTransform(` (src), `class *Unit|Parameter*(Helper|Service|Utils|Extensions)` (src).
- **Decisions made:**
  - RESOLVED `.addin` ownership (was [Open]): manifest is manually installed, machine-wide, outside the repo; repo tracks only `docs/deployment/LECG.addin.template`; build does not generate or copy it. Promoted into the Revit Add-in section as observed fact. A narrower [Open] remains: README documents `%AppData%` install + `AddInId`, actual is ProgramData + `ClientId` — which is canonical is a user question.
  - KEPT WEAK (re-checked, no new evidence to promote): units [Inferred] (no unit helper class in `src/`), parameters [Inferred] (no shared parameter helper), collectors (no stable evidence), linked transforms [Open] (still zero `GetTotalTransform`/`GetTransform(` hits in `src/`).
  - PROMOTED build guardrail wording: `dotnet build -p:SkipRevitDeploy=true` is now recorded as the DEFAULT validation build for future GSD runs; plain `dotnet build` marked avoid-unless-deploying. Strengthened the [Concern] with the manifest link (live manifest points Revit at the exact folder the build overwrites).
  - CREATED `docs/ai/revit-smoke-test.md`: 9-step manual checklist (startup, ribbon, representative command, active document, empty/invalid selection, no unexpected modification, transaction commit/rollback, modeless ExternalEvent double-run, shutdown). Explicitly marked never-executed.
- **Build / deployment guardrails:** Safe validation: `dotnet build -p:SkipRevitDeploy=true` (re-verified this run: succeeded, 0 warnings/0 errors, 3 projects, no deploy step). Risk: plain `dotnet build` copies dll/pdb/deps.json into `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG` (`LECG.csproj:61-72`), which the live manifest loads — never run it or deployment scripts unless deployment is explicitly requested. CI builds auto-skip deploy (`LECG.csproj:13`).
- **Manifest finding:** RESOLVED — manually installed by design, not missing by accident. Live at `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` (Type=Application, FullClassName=LECG.App, Assembly=…\Addins\2026\LECG\LECG.dll, ClientId 99112487-…). Tracked template at `docs/deployment/LECG.addin.template`; install procedure in `docs/deployment/README.md`. No `.addin` file was created or modified this run. Residual [Open]: README/actual path + GUID-element divergence.
- **Validation:** `dotnet build -p:SkipRevitDeploy=true` → Build succeeded, 0 warnings, 0 errors. `git status` confirms only `docs/ai/` untracked/changed by this run. Revit runtime validation: not executed.

## Lessons Learned (stabilization pass)

### Best Practices Observed
- The csproj already auto-disables deploy on CI (`SkipRevitDeploy` flips to true under `GITHUB_ACTIONS`/`CI`) — the guardrail is a first-class repo mechanism, not an agent invention.
- Deployment knowledge lives in `docs/deployment/` (README + tracked manifest template); check there before inferring deployment behavior from the csproj alone.

### Concerns / Pitfalls
- The live add-in loads from the SAME folder the default build deploys to — "just building" is a production deploy on this machine. Deploying while Revit is open either fails on locked files or leaves a mixed-version folder.
- `docs/deployment/README.md` does not match the actual install (per-user vs machine-wide path; `AddInId` vs `ClientId`). Future agents must not "fix" either side without asking — the divergence may be intentional history.
- A stale `LECG.dll.old` sits in the live deploy folder — evidence of past manual file juggling; don't treat the live folder as clean build output.

### Decisions to Preserve
- `dotnet build -p:SkipRevitDeploy=true` is the default validation build for all future GSD runs on this repo unless repo evidence later proves otherwise.
- Weak entries (units, parameters, collectors, linked transforms) stay [Inferred]/[Open] — two GSD runs have found no promotable evidence; do not promote for completeness.

### Future-Agent Warnings
- Never claim Revit runtime validation passed without actually opening Revit — use the checklist in `docs/ai/revit-smoke-test.md` and the exact wording "Revit runtime validation: not executed." otherwise.
- `docs/ai/` is currently untracked (`?? docs/ai/` in git status). When committing memory updates, stage `docs/ai/` paths explicitly; the working tree carries unrelated user WIP.
- Do not create or modify any `.addin` file (repo template or live ProgramData copy) unless the user explicitly requests it.

### Open Questions
- Canonical `.addin` install location + GUID element (README vs reality); collector conventions; unit-conversion helper; linked-model transforms; sanctioned `dotnet test` invocation; first execution of the Revit smoke-test checklist. (Detailed in repo-context.md → Open Questions.)

## 2026-07-04 — Runtime validation + deployment-doc drift pass

- **Purpose:** Execute the Revit smoke-test checklist for the first time and decide whether `docs/deployment/README.md` is stale vs the actual installed manifest. Documentation-only: no product code changed.
- **Preflight (all clean):** `git status` — only pre-existing user WIP plus untracked `docs/ai/`; safe build `dotnet build -p:SkipRevitDeploy=true` → succeeded, 0 warnings/0 errors, 3 projects, no deploy step; live manifest `C:\ProgramData\Autodesk\Revit\Addins\2026\LECG.addin` exists (read-only check); referenced DLL `...\Addins\2026\LECG\LECG.dll` exists (LastWrite 2026-07-02 19:25 — untouched by this pass); no deployment script/target executed; no per-user `%AppData%` LECG.addin exists.
- **Revit runtime validation: not executed** — the agent cannot drive the Revit GUI, and no Revit session was running (only the `RevitAccelerator` preloader). The interactive checklist remains pending its first human-driven run. HOWEVER, read-only journal inspection (`journal.0879.txt`, user session 2026-07-04 22:12–22:15, running the currently deployed DLL) yielded direct evidence for a subset of steps:
  - Step 1 startup: `API_SUCCESS { Starting External Application: LECG, Class: LECG.App, ... Assembly Version: 0.1.1.0 }` from the ProgramData path.
  - Step 2 ribbon: all LECG panels/pushbuttons registered via `API_SUCCESS` (Home, Project Health, Standards, Toposolids, Align, Model Organization, Visualization).
  - Step 9 shutdown: clean `ExitNativeInstance` / `finished recording journal file`.
  - Steps 3–8 NOT tested: journal shows no LECG command executed that session.
  - New finding: `API_ERROR { Assembly version conflict in some references in LECG.dll assembly }` — Clipper2Lib 2.0.0.0 vs preloaded 1.1.1.0; Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0.0 vs preloaded 9.0.0.0 (other add-ins preload them). Add-in loads anyway; filed as [Concern] in repo-context.md (affects Clipper2-dependent commands, e.g. Align Edges).
- **Files inspected:** live `LECG.addin` + deploy folder (read-only), `%AppData%\Autodesk\Revit\Addins\2026` (no LECG manifest), Revit journals folder + `journal.0879.txt` (greps for LECG, RibbonEvent, session tail), `docs/deployment/README.md`, `docs/deployment/LECG.addin.template`, `LECG.csproj` deploy target (from prior pass, re-confirmed), process list.
- **Decisions made:**
  - Runtime memory: added narrow [Observed] entry (scope: startup/ribbon ONLY, journal-evidenced) to repo-context.md; recorded run history in `revit-smoke-test.md` explicitly marked "NOT an interactive checklist run". Did NOT claim any command-level step passed.
  - README drift: judged CLEARLY STALE (option 1) — it never mentioned `DeployToRevit`, documented a per-user `%AppData%` install that does not exist, and pointed `<Assembly>` at `bin\Debug`, while csproj target + live manifest + journal all agree on machine-wide ProgramData. Updated `docs/deployment/README.md` (documentation-only) to document: machine-wide manifest path, template location, deploy-target behavior, safe validation build, plain-build deploy warning, no-`.addin`-generation fact, and don't-edit-live-manifest rule. Kept the `<ClientId>` vs `<AddInId>` divergence as a documented quirk + [Open] (cosmetic until someone installs from the template; needs an interactive Revit test to unify).
  - Kept weak entries weak: units/parameters/collectors/linked transforms untouched — this pass produced no new evidence about them.
- **Build / deployment guardrails:** unchanged and re-verified — `dotnet build -p:SkipRevitDeploy=true` is the default validation build; plain `dotnet build` deploys to the live folder; no `.addin` file was created or modified.
- **Manifest finding:** README/actual divergence RESOLVED as README staleness; machine-wide ProgramData install confirmed canonical by journal evidence (Revit demonstrably loads from it). Residual [Open]: GUID element name (`ClientId` live vs `AddInId` template).
- **Validation:** Build/typecheck: safe build succeeded 0/0. XAML: compiled within the same build (none changed). Manifest/deployment: read-only verification only; deployed DLL timestamps unchanged after the pass. Revit runtime validation: not executed. Pending: interactive checklist steps 3–8 (plus a formal human-driven full run).

## Lessons Learned (runtime/drift pass)

### Best Practices Observed
- Revit journals are a high-value read-only evidence source: add-in load success/failure, per-button ribbon registration, assembly conflicts, and clean-shutdown markers are all recorded — an agent can verify startup health without launching Revit.

### Concerns / Pitfalls
- Assembly version conflicts with OTHER installed add-ins (Enscape/ModPlus/Forma preload shared assemblies) are logged at every LECG load. In Revit's shared load context the first-loaded version can win — geometry/DI bugs that only reproduce inside Revit should be checked against the journal conflict lines before blaming LECG code.
- The deployed-DLL timestamp (2026-07-02) lags the working tree — the live add-in does not include current WIP; journal evidence validates the deployed build, not HEAD.

### Decisions to Preserve
- Runtime claims stay narrowly scoped: journal evidence promoted startup/ribbon/shutdown only; command-level behavior remains open until a human runs the checklist.
- `docs/deployment/README.md` now reflects verified reality — future agents should keep it aligned with the csproj deploy target and live install, not re-introduce the `%AppData%` instructions.

### Future-Agent Warnings
- Do not launch Revit unattended to "complete" the smoke test — the checklist requires human observation (dialogs, undo list, responsiveness). Use journals for read-only evidence instead.
- Do not unify `<ClientId>`/`<AddInId>` between live manifest and template without an interactive Revit test proving the template variant loads.

### Open Questions
- GUID element name in the manifest template; interactive command-level smoke test (steps 3–8); collector conventions; unit-conversion helper; linked-model transforms; sanctioned `dotnet test` invocation. (Detailed in repo-context.md → Open Questions.)

---

## 2026-07-25 — GSD removal, Revit API index, WPF theme scoping

Commits: `6c386ec` (tooling + theme scoping), `2cd13fe` (view merge fix + guard tests).

### Changed
- Removed the GSD plugin (20,063 lines of markdown, 33 slash commands); replaced with eight `lecg-*` skills in `.claude/skills/` (~620 lines).
- Added `docs/ai/revit-api/` — 33,091 members / 2,670 types indexed from the 2026.4.10 reference assemblies, plus `SEMANTICS.md`. Generator: `scripts/revit-api-index`.
- Fixed a process-wide UI leak: `LecgTheme.xaml` was merged into `Application.Current.Resources` (Revit's own WPF application), applying LECG's implicit `Button`/`TextBox`/`ComboBox`/`RadioButton`/`ProgressBar`/`TreeView`/`TreeViewItem` styles to Revit dialogs and every other loaded add-in. Theme now merges per window.

### Validation

- Build: `dotnet build -p:SkipRevitDeploy=true` — 0 errors, 5 pre-existing warnings.
- Tests: 211 passed, 0 failed, 5 skipped.
- **Revit runtime validation: EXECUTED** — first interactive smoke test recorded in this repo. Deployed build 2026-07-25 10:14, observed by the user in Revit 2026:
  - `HomeView` renders styled (regressed and fixed mid-session — see below).
  - `SearchReplaceView` renders styled.
  - Other vendors' add-in dialogs render in their own style — **the leak is closed**.
- Not covered by this run: the remaining 23 LECG views individually, the startup error dialog (`LecgDialogWindow`), and the explicit before/after ordering check (smoke test step B7).

### Lessons Learned
- **XAML `<X.Resources><ResourceDictionary>` REPLACES the dictionary, it does not merge into it.** A base-class constructor cannot reliably inject resources into derived views: `InitializeComponent()` discards them, and `StaticResource` then fails at parse time. `HomeView` and `SearchReplaceView` broke on `DashboardCardStyle` this way. Every view must merge `LecgTheme.xaml` in its own XAML — now enforced by `LECG.Tests/Views/ThemeScopingTests.cs`.
- Neither compilation nor the unit suite can detect a WPF styling break; XAML compiles without its bindings or `StaticResource` lookups resolving. Only Revit shows it.
- `ElementId.IntegerValue` and `DisplayUnitType` do not exist in Revit 2026 — confirmed by absence from the generated index, which reads exactly what the build compiles against.

### Decisions to Preserve
- No third-party WPF control library (WPF-UI, HandyControl, MaterialDesignInXAML) — all theme via application-level implicit styles, which is correct for an app owning its process and wrong for an add-in. WinUI 3 / WindowsAppSDK is a separate app model, not hostable here. Rationale in `docs/ai/ui-guide.md`.
- The Revit API index is committed ground truth. Query it; do not recall API signatures.

### Future-Agent Warnings
- Never merge anything into `Application.Current.Resources`. The guard test will fail, and the real cost is other vendors' UI.
- Regenerate the API index only on a Revit version bump, and pass the Revit install directory as a probe path — without it, `RibbonButton..ctor` and `PointCloudType.GetReCapProject` cannot be decoded.

### Open Questions
- The 23 untested views and the startup error dialog remain visually unverified after the scoping change (low risk: all merge the theme in XAML, unchanged by this work).
