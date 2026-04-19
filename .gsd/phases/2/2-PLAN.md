---
phase: 2
plan: 2
wave: 1
---

# Plan 2.2: Sync Enhancement Implementation

## Objective
Refactor the synchronization execution path to handle the "Force Normal Map" fix and apply the user-defined UV Scale.

## Context
- .gsd/SPEC.md
- src/Services/Interfaces/IMaterialService.cs
- src/Services/MaterialService.cs
- src/Services/RenderAppearanceBatchSyncService.cs
- src/Services/RenderMaterialSyncExecutionService.cs
- src/Services/RenderMaterialGraphicsApplyService.cs

## Tasks

<task type="auto">
  <name>Refactor Service Chain for Settings</name>
  <files>
    - src/Services/Interfaces/IMaterialService.cs
    - src/Services/MaterialService.cs
    - src/Services/Interfaces/IRenderAppearanceService.cs
    - src/Services/RenderAppearanceService.cs
    - src/Services/Interfaces/IRenderAppearanceBatchSyncService.cs
    - src/Services/RenderAppearanceBatchSyncService.cs
    - src/Services/Interfaces/IRenderMaterialSyncExecutionService.cs
    - src/Services/RenderMaterialSyncExecutionService.cs
  </files>
  <action>
    Propagate `RenderAppearanceSettings` through the entire sync chain:
    1. Update all `BatchSync` and `Sync` interfaces to accept the settings object.
    2. Update implementation classes to forward the settings.
    (Note: This re-applies any missing wiring from Phase 1 if files were reverted).
  </action>
  <verify>dotnet build LECG.sln</verify>
  <done>Service chain is correctly wired to receive user-defined settings.</done>
</task>

<task type="auto">
  <name>Implement Normal Map Property & UV Scaling</name>
  <files>
    - src/Services/RenderMaterialSyncExecutionService.cs
    - src/Services/RenderMaterialGraphicsApplyService.cs
  </files>
  <action>
    Modify the sync execution:
    1. If `ForceNormalMap` is true, access the `AppearanceAssetEditScope`.
    2. Find the Bump map asset and set its `unifiedbitmap_Bump_Type` (or equivalent `generic_bump_map_type`) property to 1 (Normal).
    3. Iterate through all bitmap assets and set their scale based on `settings.UVScale`.
    4. Ensure `RenderMaterialGraphicsApplyService` enforces "Use Render Appearance" = true.
  </action>
  <verify>dotnet build LECG.sln</verify>
  <done>Materials are successfully updated with the correct Normal Map flag and UV Scale.</done>
</task>

## Success Criteria
- [ ] Render Appearance properties are correctly updated in Revit.
- [ ] UV Scaling is applied to bitmap assets.
- [ ] Shading graphics are standardized as "Use Render Appearance".
