# Substance batch creation review — 2026-09-04

## User workflow

Open **LECG → PBR Material**, browse to `C:\LECG\SubstanceBakes`, then click **Review Batch**. The library's materials are selected across all categories. Size starts at **2500 mm**. Click **Create Materials** to create the batch; existing names are skipped unless Overwrite is checked. Per the user's handoff preference, the ribbon exposes only PBR Material and Render Match. The batch and diagnostic implementation remains in the source.

The root-folder problem was in the PBR Material entry point: its texture lookup only inspected files directly in the selected folder. Selecting the library root therefore found no textures and never entered the batch workflow. The PBR dialog now recognizes the existing two-level Substance manifest layout, displays the detected material count, and passes the selected root to the batch command. Individual texture folders retain the existing single-material workflow.

## Fixes and verification

- Added a regression check that first failed when selecting a library root through `PbrMaterialCreatorViewModel`. It now verifies both categories are selected, the root is preserved, the batch size resets to 2500 mm, and returning to a single-material folder exits batch mode.
- Fixed `MaterialBitmapPropertyService.SetDistance`: appearance distance values use the property's declared unit. The installed template uses inches, so assigning internal feet directly made 2500 mm become 208.33 mm. Conversion now writes 98.42519685039369 inches, which is 2500 mm. The shared fix covers both batch and individual PBR creation.
- Task 13 diagnostic now resolves painted materials, traverses every connected asset, and reports actual distance units. Revit selection/dump logic lives in a service; the command delegates to it.

## Revit 2026.5 runtime evidence

The disposable-project smoke test completed successfully before the final PBR entry-point integration:

- Source scan: **534 materials, 28 categories, 0 warnings**.
- Ceiling: 2 created; repeat run skipped both; overwrite updated both.
- Metal: **28 created, 0 failed, 84.1 seconds**, with 25 F0 files. The default project already contains Copper: default skip was verified, then that built-in was renamed only inside the disposable test project to test all 28 creations.
- Construction rebar grid: created and opacity connection verified. The actual Ceiling manifests contain no opacity maps; the plan's expectation of Ceiling cutouts cannot be met by those inputs.
- All 31 created materials: base-color, roughness and normal image paths exist; texture size converts back to **2500 mm**; normal-map type is 1. Painted-face diagnostic returned the painted Ceiling material and its connected normal asset.
- Advanced asset: `Name=PrismOpaqueSchema`, `BaseSchema=PrismOpaqueSchema`. Connected normal: `Name=BumpMapSchema`, `BaseSchema=BumpMapSchema`. The API creation string `BumpMap` works.

Runtime log: `C:\Users\luis.cortes\AppData\Local\LECG\SubstanceSmoke\20260904-170914\result.log` (ends `PASS: all runtime assertions`).

Saved scene: `C:\Users\luis.cortes\AppData\Local\LECG\SubstanceSmoke\20260904-170914\Substance-verification.rvt`.

The visual inspection was stopped by the user. Normal relief direction and visual cutout appearance are **not signed off**. This is distinct from the verified batch creation and stored physical dimensions. The final PBR entry-point change is covered by the regression test; it was not subsequently exercised through desktop automation.

## Repeatable checks

Final suite: **133 passed, 0 failed, 0 skipped**. Core Release with warnings treated as errors: **0 warnings, 0 errors**. Final main assembly deployed and its SHA256 matched the tested build: `F60D5B067F9C111DD9A55CFE0C0972A440620DC0084C3046ABACF51CF6B31945`. The temporary smoke manifest was renamed with a `.disabled` suffix.

From the repository root:

```powershell
dotnet test LECG.Tests/LECG.Tests.csproj -p:RevitVersion=2026 -p:SkipRevitDeploy=true --no-restore
dotnet build LECG.Core/LECG.Core.csproj -c Release -p:TreatWarningsAsErrors=true --no-restore
dotnet build tools/SubstanceSmoke/SubstanceSmoke.csproj --no-restore
```

For a deliberate Revit smoke run, deploy the main Debug build first, copy the smoke DLL into `%APPDATA%\Autodesk\Revit\Addins\2026\LECG`, and copy its source manifest as `ZZZ-LECG-SubstanceSmoke.addin` into the parent add-ins folder. Launch Revit 2026. The harness uses its own disposable project and writes results under `%LOCALAPPDATA%\LECG\SubstanceSmoke`. Remove the temporary smoke manifest after testing so normal launches do not run it.

Branch: `feature/substance-batch-pbr`. No merge performed. Existing unrelated worktree changes were preserved.

## Other-computer handoff

Fetch and check out `feature/substance-batch-pbr` from `https://github.com/lecgarq/LECGrvt`. This build was verified against **Revit 2026.5** and **.NET SDK 10.0.201**. The baked source library is external to Git: copy `C:\LECG\SubstanceBakes` separately or choose its new location in PBR Material.

To restore other command panels, uncomment the existing panel calls in `src/Core/Ribbon/RibbonService.cs`, method `InitializeRibbon`. The Visualization panel intentionally contains only **Render Match** and **PBR Material**. The standalone Substance Batch and Dump Asset buttons were removed from the ribbon at the user's request; PBR Material retains batch creation internally.

With Revit closed, build/deploy for 2026 using `dotnet build LECG.csproj -p:RevitVersion=2026`. The handoff commit includes the current plugin source, APS/batch source updates, dependency/build compatibility changes, tests, and this report. Machine-local editor settings and unrelated agent/tool deletions are excluded.

## Window recovery follow-up

The user's later window-opening report had no command exception in the Revit log. PBR window settings restored `Left=2095.2`, `Top=84`, `Width=1536`, `Height=926.4`. Its position was reset to `(40,25)` with size `900x760`; the previous settings were backed up to `C:\LECG\Addin\pbr-window-settings-before-recovery.json`.

A regression test demonstrated that the shared off-screen fallback left coordinates at `(20000,20000)` when Revit supplied a native HWND owner rather than a WPF `Window`. Setting `WindowStartupLocation` after initialization did not reposition the window. The fallback now explicitly fits and centers the window in the primary work area. The full suite passes **134 tests**. The corrected assembly was deployed; desktop reopening still requires user confirmation because desktop control remains stopped.
