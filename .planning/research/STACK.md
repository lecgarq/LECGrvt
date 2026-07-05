# Stack Research: Hardening Tooling for a Revit 2026 / .NET 8 Add-in

**Domain:** Production Revit add-in hardening (dependency isolation, testability, profiling, static analysis)
**Researched:** 2026-07-05
**Confidence:** HIGH for dependency isolation and profiling; MEDIUM for testing frameworks; HIGH for analyzers (all verified against official docs / NuGet / GitHub — no Context7 access in this environment, so WebSearch + WebFetch verification was used in place of Context7 per the tool-priority fallback)

This is not an application stack — LECG's runtime stack (C#/.NET 8, Revit 2026 API, WPF, Serilog, Clipper2, etc.) is fixed per `PROJECT.md` and already documented in `.planning/codebase/STACK.md`. This document recommends **tooling to add or configure** to execute the hardening milestone: dependency isolation, testing, profiling, and static analysis.

---

## Recommended Stack

### 1. Assembly Version-Conflict Isolation

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|------------------|
| **Native Revit 2026 add-in context isolation** (`.addin` manifest `ManifestSettings`) | Built into Revit 2026 (no package) | Isolates LECG's assemblies (Clipper2Lib, `Microsoft.Extensions.DependencyInjection.Abstractions`, etc.) into their own `AssemblyLoadContext` so they never collide with Enscape/ModPlus/Forma's copies | This is a first-party Autodesk feature added specifically to solve this exact problem in Revit 2026. It requires **zero code changes** — only a manifest edit — which fits the milestone's "no framework changes" and "must not break other add-ins" constraints better than any code-level workaround. Confirmed via official Autodesk API docs (`RevitAddInManifestSettings` class, `UseRevitContext`/`ContextName` properties) and via a live Autodesk forum thread that reproduces the exact manifest XML. |

**How it works (verified, HIGH confidence):**
- .NET 6+ removed AppDomains as an isolation mechanism; `AssemblyLoadContext` (ALC) is the replacement. Revit's own loader was ALC-unaware for add-ins until Revit 2025/2026.
- Revit 2026 adds two new manifest-level settings under a `<ManifestSettings>` element that is a **sibling of `<AddIn>` nodes, nested directly under `<RevitAddIns>`** (not inside an individual `<AddIn>` block):

```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>LECG</Name>
    <Assembly>LECG.dll</Assembly>
    <AddInId>YOUR-GUID-HERE</AddInId>
    <FullClassName>LECG.App</FullClassName>
    <VendorId>LECG</VendorId>
    <VendorDescription>LECG, https://lecg.com</VendorDescription>
  </AddIn>
  <ManifestSettings>
    <UseRevitContext>False</UseRevitContext>
    <ContextName>LECG</ContextName>
  </ManifestSettings>
</RevitAddIns>
```
- `UseRevitContext` defaults to `true` (shared context with Revit and every other add-in — today's broken behavior). Setting it to `false` moves **every add-in registered in that manifest file** into its own ALC.
- Add-ins loaded from the *same folder* share one context by default; add-ins from different folders get separate contexts unless they opt into the same `ContextName`. Since LECG deploys to its own folder (`C:\ProgramData\Autodesk\Revit\Addins\2026\LECG`) and nothing else shares that folder, this isolates LECG's `Clipper2Lib.dll`, `Microsoft.Extensions.DependencyInjection.Abstractions.dll`, `geometry3Sharp.dll`, etc. from any other add-in's copies — solving both version conflicts named in `CONCERNS.md` in one change.
- Because `CopyLocalLockFileAssemblies=true` already causes LECG's build to place its own `Clipper2Lib.dll` etc. next to `LECG.dll`, no build changes are needed beyond the manifest edit — the isolated context will resolve those local copies.
- Source: Autodesk help docs — [`RevitAddInManifestSettings.UseRevitContext` Property](https://help.autodesk.com/view/RVT/2026/ENU/?guid=2a8a515b-bdda-dfc0-e68b-1bd782e0c8b9), [`RevitAddInManifestSettings` Class](https://help.autodesk.com/view/RVT/2026/ENU/?guid=51d39ce9-9de9-3b59-1788-7c055bb5db75), Autodesk forum — [Context Isolation in Revit 2026](https://forums.autodesk.com/t5/revit-api-forum/context-isolation-in-revit-2026/td-p/13661248).

**Caveat to test explicitly (MEDIUM confidence, flag for the interactive smoke test):**
Revit 2026 added a *deduplication mechanism* alongside this feature specifically because "WPF and XAML parsers are ALC-unaware and fetch the first matching name+version from the AppDomain's global pool." A related Autodesk forum thread ([Revit 2027 ManifestSettings deduplication mechanism (from Revit 2026) missing?](https://forums.autodesk.com/t5/revit-api-forum/revit-2027-manifestsettings-deduplication-mechanism-from-revit/td-p/14105252)) confirms this dedup behavior is new and version-sensitive. LECG already has a fragile, documented `pack://` URI-registration workaround for WPF resource loading (`src/App.cs:22-27`, flagged in `CONCERNS.md`). **Enabling `UseRevitContext=False` must be verified against LECG's WPF windows/dialogs in the interactive Revit smoke test** — isolating LECG's assembly into its own ALC could interact with the existing pack:// workaround in ways that are only visible at runtime.

**Also required (code-level, not a library):** a startup diagnostic that logs which `Clipper2Lib` / `Microsoft.Extensions.DependencyInjection.Abstractions` version is actually loaded, via `AppDomain.CurrentDomain.GetAssemblies()` (or `AssemblyLoadContext.All`) filtered to those two assembly names, logged through the existing Serilog pipeline at `App.OnStartup`. This is what `CONCERNS.md` already asks for ("startup diagnostic logs actually-loaded versions") and doubles as the verification step that the manifest isolation is actually working (i.e., the loaded version should now always be LECG's own, regardless of load order).

---

### 2. What NOT to Use for Isolation

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| **AppDomain-based isolation** (e.g. hand-rolled secondary AppDomain, or blog patterns like "Using AppDomains to Resolve DLL Conflicts in Revit Plugins") | AppDomains are not supported for isolation starting with .NET 6; Revit 2026 add-ins run on .NET 8. Any AppDomain-based approach targets .NET Framework-era Revit versions and does not apply here. | Native `UseRevitContext=False` manifest isolation (above) |
| **ILRepack / ILMerge** (merging Clipper2Lib, DI.Abstractions, etc. into `LECG.dll`) | Community reports (Autodesk forum, "Error When Merging With ILMerge/Repack") show ILRepack throws assembly-resolution errors once a project has its own project references (LECG has `LECG.Core` as a `ProjectReference`), and merging changes the merged types' assembly identity, which can silently break reflection-based code and third-party integrations that expect the original assembly name. It also requires re-merging on every build and adds a non-trivial MSBuild step for a problem Revit 2026 now solves natively. | Native manifest isolation |
| **Costura.Fody** (embed dependencies as resources) | Documented failure mode ("dll hell is real", archi-lab): Costura still publishes the *same* `AssemblyName` as the original, so when two add-ins embed different versions, the AppDomain-wide "first loaded wins" rule still applies — Costura does not create true isolation, only a different packaging mechanism for the same conflict. | Native manifest isolation |
| **Third-party ALC-isolation *libraries*** (`Scotec.Revit.Isolation`, `Nice3point.Revit.Toolkit`'s isolated `ExternalCommand`/`ExternalCommandAvailability`, Nice3point's attribute-based codegen isolation) | Both require inheriting from library-specific base classes or attributes (`[RevitCommandIsolation]`, `Nice3point.Revit.Toolkit.ExternalCommands.ExternalCommand`, etc.) and registering auto-generated factory classes in the manifest instead of LECG's own command classes. LECG already has its own `RevitCommand` / `ExternalEventCommand<T>` base classes with established transaction/logging/availability conventions (per `PROJECT.md`'s "no framework changes" constraint and `ARCHITECTURE.md`). Adopting either library would mean rewriting every command's inheritance chain — out of scope for a hardening milestone, and redundant now that Revit ships the same capability natively at the manifest level. | Native manifest isolation (same underlying mechanism, zero code/inheritance changes) |
| **Downgrading LECG's Clipper2 to 1.1.1.0** to match whatever other add-ins load | `STACK.md` already documents Clipper2 2.0.0 as a deliberate, pinned choice (API differences between 1.1 and 2.0 affect geometry results). Downgrading trades a version-conflict bug for a silent regression in `AlignEdges`/`FixPoints` geometry correctness. | Native manifest isolation (keep Clipper2 2.0.0) |
| **Strong-naming LECG's private copy of Clipper2/DI.Abstractions** | Strong-naming alone does not create separate load contexts in the *same* AppDomain for two assemblies with the *same* simple name; the CLR's default (non-ALC-aware) probing still resolves by simple name first in many binding paths, and Clipper2/DI.Abstractions are not strong-named upstream, so this would require forking and re-signing the packages — high maintenance cost for a problem already solved by the manifest feature. | Native manifest isolation |

---

### 3. Unit Testing Revit API Code Without Revit

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|------------------|
| **xUnit + FluentAssertions + NSubstitute** (already in `LECG.Tests.csproj`) | xUnit 2.6.5, FluentAssertions 6.12.0, NSubstitute 5.1.0 | Keep as the test stack — no change | This is already LECG's established, working pattern (`TESTING.md`). The milestone's own constraint is "no framework changes"; introducing a second test framework for command-level coverage would fragment the test suite for no benefit, since the blocker isn't the test framework, it's how Revit types are referenced in the code under test. |
| **Wrap-and-mock pattern**: extract a narrow interface (e.g. `IElementProvider`, `IDocumentAccessor`, `IUiDocumentAccessor`) around the specific `Document`/`UIDocument`/`FilteredElementCollector` calls a command or service needs, and mock the *interface* with NSubstitute | N/A (a pattern, not a package) | Makes high-risk commands (Purge, AlignEdges, FixPoints, CategoryChanger) testable without Revit running | **Root cause verified from LECG's own codebase**: `Autodesk.Revit.DB.Document`/`UIDocument`/`Element` have no public constructors and (in the reference-assembly-only build) no runtime implementation at test time, so NSubstitute — which proxies via Castle DynamicProxy and needs an accessible constructor or interface — cannot create instances of them at all (confirmed in `TESTING.md`'s own analysis of the existing suite, and consistent with Castle DynamicProxy's documented requirement for a public/protected parameterless constructor on class targets, which sealed/internal-ctor Revit types lack). NSubstitute mocks **interfaces** without this restriction. The existing codebase already does this partially (pure-data static methods, reflection-based ctor null-guard tests); the milestone should extend the same pattern specifically to the command entry points named in `CONCERNS.md`, not introduce a new mocking library. |

**What NOT to use:**

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| **Moq** | No capability advantage over NSubstitute for this problem (same Castle DynamicProxy constraint on concrete Revit types); switching would only add a second mocking library with no functional gain, contradicting "no framework changes." | NSubstitute (already in use) |
| **Running the full command-level test suite inside a live Revit process** (e.g. `ricaun.RevitTest`, `Nice3point.TUnit.Revit` / RevitUnit, `DynamoDS/RevitTestFramework`) as the *primary* CI test strategy | These frameworks genuinely solve "test against the real Revit API," but they require Revit to be installed and running (interactive or via a Revit-hosted headless launcher), which breaks LECG's current CI-runner-without-Revit setup (`.planning/codebase/STACK.md` explicitly notes this as a deliberate, working CI design). Introducing one now would be a second, heavier test execution model layered on top of the existing xUnit suite — a bigger change than this hardening milestone's stated scope ("high-risk-focused test coverage," not "comprehensive"). Also, of the frameworks surveyed, only **Nice3point's `Nice3point.TUnit.Revit` (RevitUnit)** confirms Revit 2026 support today (package version `2026.0.1`); `ricaun.RevitTest` documentation currently states support through Revit 2025 only, with 2026 support unconfirmed as of this research (MEDIUM confidence — verify directly before considering). | Keep the mock-the-interface pattern above for automated coverage; use the existing manual/interactive Revit smoke-test checklist (`docs/ai/revit-smoke-test.md`) for the handful of behaviors that truly require a live Document (transaction commit/rollback, modeless dialog lifecycle, ExternalEvent reentrancy) — which is exactly what `PROJECT.md`'s "Runtime validation (interactive, user-assisted)" requirement already plans for. |

**Confidence:** MEDIUM. The mocking-limitation root cause is corroborated by LECG's own existing test suite behavior (a first-party, current source), which is stronger than a generic claim, but the Revit-hosted test framework version-support claims (ricaun.RevitTest 2019–2025, Nice3point.TUnit.Revit 2026.0.1) come from NuGet/GitHub listings via WebSearch, not a maintainer changelog read in full — treat as directional, verify the exact version pin before adopting either (which this research does **not** recommend adopting for this milestone anyway).

---

### 4. Profiling .NET Code Inside Revit

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|------------------|
| **`dotnet-trace`** (global .NET tool) | Latest (`dotnet tool install --global dotnet-trace`; verified against .NET 9.0.661903+ docs, compatible with .NET 8 target processes) | Attach to the already-running `Revit.exe` process by PID and capture a CPU/thread-time trace while a user drives a BatchRename/Purge/Alignment operation, with zero code changes to LECG | This is the correct tool for LECG's exact scenario: **Revit.exe is the host process**, not something LECG launches itself, so profiling must attach to a running PID rather than launch-and-trace. `dotnet-trace collect --process-id <PID>` supports exactly this (confirmed via official Microsoft Learn docs, `dotnet-trace collect -p|--process-id <PID>` option). Default profile combo (`dotnet-common` + `dotnet-sampled-thread-time`) is a low-overhead sampling profiler appropriate for validating whether `FilteredElementCollector` usage is a real bottleneck before optimizing (the milestone's explicit "profile first" policy). |
| **PerfView** (Microsoft, open source) | Latest from `microsoft/perfview` on GitHub | View the `.nettrace` file captured above; drill into "top N methods by inclusive/exclusive time" | Official companion viewer for `dotnet-trace` output on Windows (confirmed in the same Microsoft Learn page: ".nettrace files [can be viewed] in Visual Studio or PerfView for analysis"). Since LECG's dev environment is Windows-only, this is a free, zero-install-friction option (single portable .exe). `dotnet-trace report <tracefile> topN` also gives a quick CLI-only summary without opening PerfView, useful for a first pass. |
| **`Stopwatch`-based ad hoc instrumentation** (already idiomatic .NET, no package) | N/A | First-pass, low-ceremony timing around suspected hot loops (e.g. a `FilteredElementCollector` call site inside `BatchRenameExecutionService`) before reaching for a full trace | Cheapest way to get a coarse before/after number for a specific suspected bottleneck; appropriate given the milestone's "only proven bottlenecks optimized" policy — a quick `Stopwatch` check can rule out non-issues before justifying the ceremony of an attached trace session. |

**Optional / commercial alternative:**

| Tool | When to use it instead |
|------|------------------------|
| **JetBrains dotTrace** (standalone, or integrated in Rider) | If the team already holds a JetBrains license, dotTrace's "Attach to Process" + Timeline profiling mode gives a more approachable GUI than raw `dotnet-trace` + PerfView, including call-tree and hot-path visualizations without leaving the IDE. Timeline mode requires installing the JetBrains ETW Host Service with admin privileges (one-time setup) — confirmed via JetBrains' own documentation. Not recommended as the *only* tool since it requires a paid license; `dotnet-trace` + PerfView is free and sufficient for the profile-first validation this milestone needs. |

**Confidence:** HIGH for `dotnet-trace`/PerfView (verified directly against current Microsoft Learn docs, dated 2026-06-10). MEDIUM for dotTrace (JetBrains docs confirm the workflow exists; did not verify a live Revit.exe attach scenario specifically).

---

### 5. Static Analysis for the Catch-Block Audit

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|------------------|
| **Enable `CA1031` explicitly** (`Microsoft.CodeAnalysis.NetAnalyzers`, already referenced at 8.0.0) | Add `dotnet_diagnostic.CA1031.severity = warning` (or `error`, matching LECG's existing `TreatWarningsAsErrors` policy) to `.editorconfig` | Flags every `catch (Exception)` / bare `catch` across the 54 sites named in `CONCERNS.md`, at build time, permanently (prevents regression after the audit) | **Verified against the official CA1031 rule page**: "Enabled by default in .NET 10: No" — CA1031 ships with `Microsoft.CodeAnalysis.NetAnalyzers` (already a LECG dependency) but is **not** activated by `AnalysisLevel=latest` alone; it requires an explicit `.editorconfig` severity entry. LECG's `.editorconfig` currently has no `CA1031` entry (confirmed by inspection), so this rule is silently inert today despite the package already being referenced — a zero-new-dependency fix. |
| **Enable Roslynator `RCS1075`** (`Roslynator.Analyzers`, already referenced at 4.15.0) | Add `dotnet_diagnostic.RCS1075.severity = warning` to `.editorconfig` | Flags empty `catch` clauses that swallow `System.Exception`, **even when the block contains only a comment** | Also a zero-new-dependency fix — Roslynator is already a LECG package reference. RCS1075's stricter "flags even with an explanatory comment" behavior is actually useful here: it forces every intentional suppression to go through a named, auditable helper (`LogAndIgnore()`, per `CONCERNS.md`'s own suggested fix) rather than a comment that a future editor might delete along with the catch body. |
| **Optional addition: `SonarAnalyzer.CSharp`**, rule `S2486` only | Latest (`10.28.0.143324` at time of research) | More semantically precise than CA1031/RCS1075: S2486 ("Exceptions should not be ignored") specifically flags a catch block that does nothing meaningful, and explicitly treats a catch block containing only a comment as **compliant** if the comment documents intent — a middle ground between "must call a logger" (too strict for the small number of truly-intentional swallows) and "no rule at all" | Confirmed via SonarSource's own rule documentation excerpt (csharpsquid:S2486). If added, scope it down via `.editorconfig` to only `S2486` (`dotnet_diagnostic.S2486.severity = warning`, all other `SXXXX` rules `none`) — SonarAnalyzer.CSharp ships hundreds of other rules that would otherwise flood the build with unrelated findings, which is out of scope for this milestone. This is presented as **optional**, not required: CA1031 + RCS1075 together already cover the "must log or explicitly suppress" requirement in `CONCERNS.md` using packages LECG already has. |

**What NOT to use:**

| Avoid | Why | Use Instead |
|-------|-----|--------------|
| **A hand-written custom Roslyn analyzer project** for "every catch block must log or use `LogAndIgnore`" | Building and maintaining a custom `DiagnosticAnalyzer` (with its own NuGet packaging, test project, and versioning) is disproportionate engineering for a one-time 54-site audit plus ongoing regression prevention that CA1031 + RCS1075 already provide out of the box. | CA1031 + RCS1075 (both already-referenced packages, `.editorconfig`-only change) |
| **Full `SonarAnalyzer.CSharp` ruleset** (not scoped to S2486) | Would introduce hundreds of new findings across the whole 39-command codebase unrelated to the catch-block concern, in a milestone explicitly scoped to fixing *documented* concerns only (`PROJECT.md`'s "Out of Scope" section rules out speculative, non-profiled work in the same spirit). | Scope to `S2486` only via `.editorconfig`, or skip Sonar entirely and rely on CA1031 + RCS1075 |

**Confidence:** HIGH. CA1031's default-disabled status is directly quoted from the current official Microsoft Learn rule page; RCS1075's behavior is corroborated by Roslynator's own docs and GitHub issue discussion; both packages are already present in `LECG.csproj` (no new dependency risk). SonarAnalyzer/S2486 behavior is corroborated by SonarSource's own rule text via WebSearch (not independently re-verified against a live SonarQube instance), so treat the S2486-specific "comment counts as compliant" nuance as MEDIUM confidence pending a quick manual check against one real LECG catch block before relying on it broadly.

---

## Installation

```bash
# Nothing new required for isolation (manifest-only change) or for testing
# (xUnit/FluentAssertions/NSubstitute already referenced).

# Profiling tool (one-time, machine-wide, not a project dependency):
dotnet tool install --global dotnet-trace

# Optional: scope SonarAnalyzer.CSharp to S2486 only (if adopted)
dotnet add LECG.csproj package SonarAnalyzer.CSharp --version 10.28.0
```

```ini
# .editorconfig additions (no new packages — activates existing analyzers)
[*.cs]
dotnet_diagnostic.CA1031.severity = warning
dotnet_diagnostic.RCS1075.severity = warning
# If SonarAnalyzer.CSharp is added, scope it down:
# dotnet_diagnostic.S2486.severity = warning
```

```xml
<!-- docs/deployment/LECG.addin.template and the live manifest -->
<RevitAddIns>
  <AddIn Type="Application">
    <!-- ...existing AddIn block unchanged... -->
  </AddIn>
  <ManifestSettings>
    <UseRevitContext>False</UseRevitContext>
    <ContextName>LECG</ContextName>
  </ManifestSettings>
</RevitAddIns>
```

---

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|--------------------------|
| Native manifest ALC isolation (`UseRevitContext=False`) | `Scotec.Revit.Isolation` or `Nice3point.Revit.Toolkit` isolated command base classes | If LECG ever migrates its command base classes to Nice3point's toolkit wholesale (a much larger, currently out-of-scope architectural change) — at that point the library-level isolation would come "for free" alongside the migration. Not applicable to this milestone. |
| Mock-the-interface + xUnit/NSubstitute | `Nice3point.TUnit.Revit` (RevitUnit) running inside live Revit | If a future milestone explicitly scopes "comprehensive Revit-hosted integration test suite" (out of scope per `PROJECT.md`) and the team accepts adding Revit as a CI/test-runner dependency. |
| `dotnet-trace` + PerfView | JetBrains dotTrace | If the team already has JetBrains tooling licensed and prefers an integrated Rider workflow over CLI + separate viewer. |
| CA1031 + RCS1075 via `.editorconfig` | SonarAnalyzer.CSharp scoped to S2486 | If the CA1031/RCS1075 combination proves too strict in practice (e.g., flags call sites where a comment-only suppression is genuinely appropriate and `LogAndIgnore()` would be awkward) — S2486's "comment counts as compliant" nuance may better match a handful of edge cases. Add narrowly, don't replace. |

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|------------------|-------|
| Revit 2026 manifest `ManifestSettings` (`UseRevitContext`) | .NET 8 / `net8.0-windows` (LECG's target) | Feature is Revit-version-gated, not .NET-version-gated; confirmed present in Revit 2026, with a caveat that Revit 2027 changed the deduplication behavior underneath the same setting — re-verify this section if/when LECG ever targets Revit 2027. |
| `Nice3point.Revit.Api.RevitAPI/RevitAPIUI` 2026.4.10 | `Microsoft.CodeAnalysis.NetAnalyzers` 8.0.0, `Roslynator.Analyzers` 4.15.0 | No interaction; these are compile-time/analysis-only packages, independent of the Revit reference assemblies. |
| `dotnet-trace` (global tool) | Any .NET 8 process, including Revit's in-process CLR hosting LECG's assembly | `dotnet-trace` traces the CLR hosting the target process (Revit.exe's .NET 8 runtime), not LECG.dll specifically — traces will include Revit-internal .NET activity alongside LECG's; filter by module/method name in PerfView's call-tree view when analyzing. |

## Sources

- [RevitAddInManifestSettings.UseRevitContext Property — Autodesk Help (Revit 2026 API)](https://help.autodesk.com/view/RVT/2026/ENU/?guid=2a8a515b-bdda-dfc0-e68b-1bd782e0c8b9) — HIGH confidence, official docs
- [RevitAddInManifestSettings Class — Autodesk Help](https://help.autodesk.com/view/RVT/2026/ENU/?guid=51d39ce9-9de9-3b59-1788-7c055bb5db75) — HIGH confidence, official docs
- [Context Isolation in Revit 2026 — Autodesk Community Forum](https://forums.autodesk.com/t5/revit-api-forum/context-isolation-in-revit-2026/td-p/13661248) — MEDIUM confidence (community, but reproduces official manifest XML consistent with the above)
- [Revit 2027 ManifestSettings deduplication mechanism (from Revit 2026) missing? — Autodesk Community Forum](https://forums.autodesk.com/t5/revit-api-forum/revit-2027-manifestsettings-deduplication-mechanism-from-revit/td-p/14105252) — MEDIUM confidence; used only to surface the WPF/XAML dedup caveat, not as a primary claim
- [Innovative Revit Add-in Development (part 3) — Scotec](https://www.scotec.com/en/blog/posts/Innovative-Revit-Addin-Development-Part-3) — MEDIUM confidence, vendor blog describing `Scotec.Revit.Isolation` (used to document an alternative NOT recommended)
- [Nice3point/RevitToolkit Releases — GitHub](https://github.com/Nice3point/RevitToolkit/releases) — MEDIUM confidence, release notes for 2025.0.1 ALC isolation feature (alternative NOT recommended for this milestone)
- [Error When Merging With ILMerge/Repack — Autodesk Community Forum](https://forums.autodesk.com/t5/revit-api-forum/error-when-merging-with-ilmerge-repack/td-p/12424564) — MEDIUM confidence, evidence against ILRepack approach
- [Using AppDomains to Resolve DLL Conflicts in Revit Plugins — sharpbim](https://sharpbim.hashnode.dev/using-appdomains-to-resolve-dll-conflicts-in-revit-plugins) — LOW confidence (pre-.NET-6 pattern, cited only to explain why AppDomains are excluded)
- [encapsulating addins with costura.fody — archi-lab](https://archi-lab.net/encapsulating-addins-with-costura-fody/) and [dll hell is real — archi-lab](https://archi-lab.net/dll-hell-is-real/) — MEDIUM confidence, community post-mortems documenting Costura.Fody's "same AssemblyName" limitation
- [ricaun.RevitTest — GitHub / ricaun.com](https://ricaun.com/revittest/) — MEDIUM confidence, version-support claim (2019–2025) via WebSearch summary, not directly read in full
- [Nice3point/RevitUnit — GitHub](https://github.com/Nice3point/RevitUnit) and [Nice3point.TUnit.Revit 2026.0.1 — Libraries.io / NuGet](https://libraries.io/nuget/Nice3point.TUnit.Revit) — MEDIUM confidence, confirms Revit 2026 package version exists
- [dotnet-trace diagnostic tool — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/dotnet-trace) — HIGH confidence, official docs, fetched directly (dated 2026-06-10 update)
- [Use dotTrace Command-Line Profiler / Profile Running Process — JetBrains dotTrace Documentation](https://www.jetbrains.com/help/profiler/Performance_Profiling__Profiling_Using_the_Command_Line.html) — MEDIUM confidence, official JetBrains docs, not fetched directly in full
- [CA1031: Do not catch general exception types — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/quality-rules/ca1031) — HIGH confidence, official docs, fetched directly; confirms "Enabled by default in .NET 10: No"
- [RCS1075: Avoid empty catch clause that catches System.Exception — Roslynator docs](https://josefpihrt.github.io/docs/roslynator/analyzers/RCS1075/) and [GitHub issue #386](https://github.com/JosefPihrt/Roslynator/issues/386) — MEDIUM confidence, official project docs via WebSearch summary
- [C# rule S2486: Generic exceptions should not be ignored — SonarQube rules](https://cloud-ci.sgs.com/sonar/coding_rules?open=csharpsquid:S2486&rule_key=csharpsquid:S2486) — MEDIUM confidence, rule text via WebSearch summary, not independently re-verified
- LECG's own repository (`LECG.csproj`, `LECG.Tests.csproj`, `.editorconfig`, `docs/deployment/LECG.addin.template`) — HIGH confidence, first-party evidence read directly during this research

---
*Stack research for: Revit 2026 add-in hardening (dependency isolation, testing, profiling, static analysis)*
*Researched: 2026-07-05*
