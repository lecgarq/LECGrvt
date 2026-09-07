# Autodesk sample benchmark — 5 September 2026

## Outcome

Completed a local, deterministic campaign on disposable copies of all 12 installed Autodesk Revit 2026 sample projects. All originals passed SHA-256 verification afterward. No generated candidate code was executed, no AI calls were made by the benchmark, and no production add-in was deployed.

This is a coverage baseline, not certification of 3,000 executable functions or proof of general agent intelligence.

| Installed capability group | Passed on at least one fixture | Failed on all attempted fixtures | Missing suitable fixture | Awaiting reviewed test |
| --- | ---: | ---: | ---: | ---: |
| Native operations | 39 | 0 | 0 | 0 |
| API property getters | 1,189 | 6 | 216 | 0 |
| API property setters | 7 | 0 | 0 | 798 |

The native operations comprise 22 reads and 17 changes. A global pass does **not** mean passing on every sample. The campaign recorded 202 failed getter/model combinations and one failed fixture suite. There were no MCP transport failures in the completed campaign.

The Japanese architectural sample's fixture suite stopped during slab-offset apply after its earlier view-scale test passed. Revit rejected a warning-producing transaction and rolled it back. Subsequent cases in that suite were not executed; prior successes remain in the coverage accounting. The rollback guard was not bypassed. Fixture setup itself logged an overlapping-floor warning. This needs a better isolated floor fixture and retesting, not relaxed production failure handling.

Getter passes establish real invocation and successful serialization on a matching element, with transaction/count guards. They are not independent semantic validation of every returned value. The reviewed write cases include preview/apply and direct API postconditions, with an outer transaction group rolling back the fixture suite.

## MCP speed

Same workload in both modes: levels, materials and views, each limited to two rows. Twelve persistent MCP sessions, one per sample; eleven alternating repetitions per mode, discarding the first warm-up. Reported percentiles use nearest rank across 120 warm observations per mode.

| Measurement | Three separate requests | One three-read batch |
| --- | ---: | ---: |
| Median full round trip | 29.89 ms | 20.49 ms |
| 95th percentile | 119.19 ms | 39.80 ms |
| Median UTF-8 response size | 2,923 bytes | 2,790 bytes |

Batching reduced the observed median by approximately 31%. MCP process startup plus initialization had a 273.85 ms median, ranging from 266.99 to 573.83 ms. These are local shared-desktop observations, not guaranteed service levels. Revit loading, AI reasoning, and simultaneous users are outside those timings. Desktop/build activity can introduce timing noise; use a dedicated idle run for release-grade performance claims.

528 actual MCP tool calls were exercised, including warm-up calls. The harness measures process startup, JSON-RPC transport, Revit queueing, tool execution, response serialization and response bytes separately where available. It does not convert bytes into estimated tokens.

## Accounting for all 3,000 research results

| Research result class | Count | Meaning |
| --- | ---: | --- |
| Lookup references | 2,216 | Reference entries mapped to installed property bindings; not new methods and not a retrieval-quality evaluation |
| Reviewed native promotions | 3 | Handwritten integrations passed runtime tests |
| Compilation-qualified candidates | 759 | Still awaiting manual review, fixtures and runtime qualification; not loaded or deployed |
| Rejected results | 22 | Remain rejected; not executed |

The installed executable catalog contains 39 native operations plus 2,216 property bindings, not 3,000 independently verified functions. The remaining 798 setters are already bound but are **not runtime-qualified by this campaign**. Do not describe them as tested or allow unattended use based on these results.

## Six getters without a successful fixture

- `Family.CurtainPanelVerticalSpacing`, `CurtainPanelHorizontalSpacing`, `CurtainPanelTilePattern`: require the appropriate curtain-panel owner family/document context.
- `SunAndShadowSettings.Visible`: available fixtures were not view-specific.
- `ViewSchedule.RowHeight`: fixture schedules had no row-height override.
- `Structure.FabricSheet.FabricHostReference`: one matching sample failed without a useful API error message; needs a controlled valid host fixture.

Full identifiers in the coverage CSV start with `api.get:Autodesk.Revit.DB.`. Other getters failed on some samples but succeeded elsewhere; their failure counts and attempts remain visible in the raw results.

## Safety and sample scope

- Tested Snowdon Towers Architectural, Structural, Site, Facades, Electrical, HVAC and Plumbing; both Golden Nugget samples; and the three Japanese samples.
- Source: `C:\Program Files\Autodesk\Revit 2026\Samples`.
- Each run creates a new output directory, copies samples without overwriting, and clears read-only attributes on copies only.
- Model/CAD links are unloaded through transmission data on copies; loaded Revit links are checked and rejected before testing. Loaded-link behavior is therefore excluded.
- Models are opened detached with worksets discarded. No synchronization or save-back to a source is performed.
- Loading warnings and explicitly named fixture-creation warnings are logged locally. Operation errors and production confirmations are not suppressed.
- Reviewed changes run with test-owned confirmation in disposable copies, fresh previews and outer rollback. Originals are hash-checked again at completion.
- The test process exits after closing its sample without saving. User Revit processes must be closed before starting this harness.
- Earlier interrupted harness-development runs are not the completed evidence. Their folders were retained and must not be counted as successful campaigns.

## Evidence

Completed run directory:

`C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\bin\x64\Release\net10.0-windows\benchmarks\20260905_183257`

- [Summary JSON](C:/LECG/RevitAddins/LECG/RevitCopilot/Tests/bin/x64/Release/net10.0-windows/benchmarks/20260905_183257/summary.json)
- [Operation coverage CSV](C:/LECG/RevitAddins/LECG/RevitCopilot/Tests/bin/x64/Release/net10.0-windows/benchmarks/20260905_183257/operation-coverage.csv)
- [3,000-result accounting CSV](C:/LECG/RevitAddins/LECG/RevitCopilot/Tests/bin/x64/Release/net10.0-windows/benchmarks/20260905_183257/research-3000-accounting.csv)
- [Per-sample MCP latency CSV](C:/LECG/RevitAddins/LECG/RevitCopilot/Tests/bin/x64/Release/net10.0-windows/benchmarks/20260905_183257/mcp-latency.csv)
- Raw `benchmark-results.json` includes every getter attempt, errors, fixture checks, warnings, source hashes and timing samples.

Final build: zero errors and warnings. Offline native-library and API-library structural checks also passed. The main installed LECG DLL remained SHA-256 `CD9E11E32D4EB8E5262F26AE143379C379160359804BD56F300BB2323FD7815A`.

## Build and rerun

Target: `net10.0-windows`, x64; Revit 2026 only. Run from `C:\LECG\RevitAddins\LECG\RevitCopilot` to select its .NET 10 SDK. Do not build while a benchmark is running, because test/MCP binaries may be locked.

```powershell
Set-Location 'C:\LECG\RevitAddins\LECG\RevitCopilot'
dotnet build Tests/RevitCopilot.SmokeTests.csproj -c Release -p:Platform=x64 -p:SkipRevitDeploy=true --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
if (Get-Process Revit -ErrorAction SilentlyContinue) { throw 'Close Revit before starting the isolated test.' }
$env:REVIT_COPILOT_SAMPLE_BENCHMARK = '1'
$env:REVIT_COPILOT_LIVE_CODEX_TEST = '0'
$env:REVIT_COPILOT_USAGE_TEST = '0'
Start-Process -FilePath 'C:\Program Files\Autodesk\Revit 2026\Revit.exe' -ArgumentList '"C:\LECG\RevitAddins\LECG\RevitCopilot\Tests\journal.RevitCopilotSmokeTest.txt"' -WorkingDirectory 'C:\LECG\RevitAddins\LECG\RevitCopilot\Tests' -WindowStyle Hidden
```

After a run reaches `completed`, generate its CSVs using `C:\LECG\RevitAddins\LECG\tools\SummarizeSampleBenchmark.ps1 -ResultPath` followed by the full path to that run's `benchmark-results.json`. The script refuses incomplete campaigns and validates all 3,000 unique research IDs and binding mappings.

## Next improvement, requiring a separate reviewed change

Use this evidence to add context checks before risky property calls, isolate the Japanese floor fixture, and add targeted fixtures for uncovered element classes. Promote generated methods in small reviewed batches with independent postconditions. Preserve compact discovery, small results, batching, and project-scoped context; do not send the full research corpus in every prompt.

The bulk benchmark made zero AI requests. This development conversation still consumes normal Codex quota. GPT-5.5 with medium reasoning was requested for the task; no newer model was invoked by the benchmark.
