# Substance Batch PBR Materials — Design

Date: 2026-09-04
Branch: `feature/substance-batch-pbr` (off `codex/review`)
Status: approved in chat, pending spec review

## Goal

Create Revit materials in batch from the Substance bake library at
`C:\LECG\SubstanceBakes` (534 materials, 28 categories) using Revit's
**Advanced Opaque** appearance schema. One window, select categories or
individual materials, click Create. The existing single-material PBR
creator switches to the same Advanced wiring so both paths share one
implementation.

## Verified facts this design relies on

Source: string dumps of Revit 2026 `MaterialDB.dll` / `OGSProtein.dll`,
`docs/review/revit-api/*.jsonl`, and analysis of the bake folder.

- Library layout: `<Category>/<slug>/<slug>_<map>.png` plus
  `<slug>_manifest.json`. Manifest lists every channel with `file`,
  `bit_depth`, `colorspace`; `missing_canonical` lists absent maps;
  numeric channels (IOR) have `value` and no `file`.
- Always present: basecolor (8-bit sRGB), metallic (8-bit), normal
  (16-bit RGBA), roughness (16-bit), height (16-bit). AO present on 382.
  Three materials name AO `_ambient_occlusion.png`; manifest `file` is
  authoritative.
- Normal maps are DirectX (Y-). Revit expects OpenGL (Y+). Green must be
  inverted.
- Advanced Opaque properties: `opaque_albedo`, `opaque_f0`,
  `surface_roughness`, `surface_normal`, `surface_cutout`. Roughness is
  direct, no inversion. No metallic slot, no AO slot, no displacement.
- Normal slot takes a `BumpMap` connected asset: `bumpmap_Bitmap`,
  `bumpmap_Type` (`BumpmapType.NormalMap` = 1), `bumpmap_NormalScale`,
  plus `texture_RealWorldScaleX/Y`, `texture_RealWorldOffsetX/Y`,
  `texture_WAngle`. This is from API docs, not yet exercised at runtime;
  the diagnostic command in §7 verifies it.
- `Material.MaterialClass` and `Material.MaterialCategory` are writable
  string properties. Description is `ALL_MODEL_DESCRIPTION`. Keywords has
  no BuiltInParameter in the dump; use `LookupParameter("Keywords")`.

## Decisions (from chat)

| Decision | Choice |
|---|---|
| Schema | Advanced Opaque for every material in v1 |
| Bump slot | Always the normal map, never height |
| Baked texture output | `C:\LECG\SubstanceBakes\_revit\<Category>\<slug>\`, 2K 8-bit default, 4K option |
| Material name | Slug title-cased, e.g. `Asphalt Rough` (no category prefix) |
| Real-world size | One global value in mm for the whole batch, default 2500 |
| Existing materials | Skipped by default; "Overwrite appearance" toggle re-wires them |

## Components

All new code lives under `src/` following existing folder conventions
(`Models`, `Services`, `Services/Interfaces`, `ViewModels`, `Views`,
`Commands`). Services register in `Core/Bootstrapper.cs`.

### 1. `SubstanceLibraryScanService` (pure)

Input: library root path.
Output: `IReadOnlyList<SubstanceMaterialEntry>`.

```
SubstanceMaterialEntry
  Category        string   folder name, e.g. "Asphalt"
  Slug            string   "asphalt_rough"
  DisplayName     string   "Asphalt Rough"
  FolderPath      string
  BaseColorPath   string   required
  NormalPath      string   required
  RoughnessPath   string   required
  MetallicPath    string   required
  AoPath          string?  from manifest, honors odd filenames
  OpacityPath     string?  extended channel "Opacity"
  Ior             double?  extended numeric "IOR"
  Resolution      int      from manifest
```

Rules:
- Enumerate `<root>/*/*/*_manifest.json`. Skip directories starting with
  `_`.
- Parse with `System.Text.Json`. Map channels by the `channel` field, not
  by filename suffix. A channel with no `file` is numeric; only IOR is
  kept.
- A manifest missing any of the four required channels is reported as a
  scan warning and excluded.
- DisplayName: split slug on `_`, title-case each token, join with space.
  Digits stay as-is (`patch_01` becomes `Patch 01`).
- Deterministic ordering: category, then slug, ordinal ignore-case.

### 2. `PbrTextureBakeService` (pure, WPF imaging)

Input: entry, `BakeOptions { OutputRoot, TargetSize (2048|4096) }`.
Output: `BakedTextureSet { BaseColor, NormalGl, Roughness, F0?, Opacity? }`
with absolute paths.

Output folder: `<OutputRoot>/<Category>/<slug>/`. File names:
`<slug>_basecolor.png`, `<slug>_normal_gl.png`, `<slug>_roughness.png`,
`<slug>_f0.png`, `<slug>_opacity.png`.

Per-map rules (all outputs 8-bit PNG, resized to TargetSize with
`BitmapScalingMode.Fant`):
- basecolor: if AO present, multiply RGB by AO (AO treated as linear
  0..1, applied to sRGB values directly; good enough for viewport and
  Raytracer, documented as an approximation).
- normal_gl: invert green: `g' = 255 - g`. Red, blue unchanged. Alpha
  dropped.
- roughness: 16-bit gray to 8-bit gray.
- f0: computed only when `max(metallic) > 0.10`. Per pixel
  `f0 = lerp(0.04, basecolor, metallic)` in linear space, written as
  sRGB 8-bit RGB. When metallic passes the threshold the basecolor
  output is additionally multiplied by `(1 - metallic)` so albedo does not
  double-count the metal color.
- opacity: 16-bit to 8-bit gray, unchanged.

Skip-if-fresh: an output is not rewritten when it exists and its
`LastWriteTimeUtc` is newer than every source it depends on and the
TargetSize matches (encoded in a sidecar `<slug>_bake.json` that records
TargetSize and source timestamps). Force-rebake is a UI toggle.

Decoding: `PngBitmapDecoder` with `BitmapCacheOption.OnLoad`, converted
through `FormatConvertedBitmap` to `Bgra32` / `Gray8` before pixel math.
Pixel math runs on `byte[]` buffers, no per-pixel WPF calls.

Performance target: under 5 s per material at 2K on the dev machine.
Bake runs on a background `Task` per material; the command awaits it
before touching the Revit API on the main thread.

### 3. `AdvancedAppearanceAssetService` (Revit)

Interface `IAdvancedAppearanceAssetService`:

```
ElementId EnsureAdvancedOpaqueAsset(Document doc, string assetName, Action<string>? log)
void ApplyBakedTextures(Document doc, Material mat, BakedTextureSet set, TextureTransform xf, Action<string>? log)
```

Template lookup: iterate `doc.Application.GetAssets(AssetType.Appearance)`;
pick the first asset whose `FindByName("BaseSchema")` string value is
`AdvancedOpaqueSchema`, else whose `Name` contains `Opaque`
(ordinal ignore-case). If none, throw `InvalidOperationException` with the
list of available asset names in the message. Cache the template per
document session.

Wiring inside one `AppearanceAssetEditScope`:
- `opaque_albedo` ← UnifiedBitmap(basecolor)
- `surface_roughness` ← UnifiedBitmap(roughness)
- `surface_normal` ← BumpMap: `bumpmap_Bitmap`, `bumpmap_Type = 1`,
  `bumpmap_NormalScale = 1.0`
- `opaque_f0` ← UnifiedBitmap(f0) when present
- `surface_cutout` ← UnifiedBitmap(opacity) when present

`MaterialBitmapPropertyService.SetupBitmapProperty` gains a `schemaName`
parameter (`"UnifiedBitmap"` or `"BumpMap"`) and sets the transform
properties that exist on the connected asset. It stops writing the
non-existent `texture_UScale`, `texture_Scale_X`, `unifiedbitmap_*Scale*`
names.

Real-world scale: `TextureRealWorldScaleX/Y` in feet from the global mm
value. Offsets 0, angle 0, `texture_LinkTextureTransforms` true.

### 4. `SubstanceMaterialCreateService` (Revit, orchestration)

Per entry:
1. Resolve name collision: if a material with `DisplayName` exists and
   Overwrite is off, skip with a log line. If Overwrite is on, reuse it.
2. Bake textures (§2).
3. Create material if new: `Material.Create`, solid fill patterns and
   average color from baked basecolor via existing
   `IRenderMaterialGraphicsApplyService` and `IImageColorExtractionService`.
4. Identity: `MaterialClass = Category`, `MaterialCategory = Category`,
   Description `"{Category} / {slug} / Substance {Resolution/1024}K bake"`,
   Keywords `"substance, pbr, {category lower}"` via `LookupParameter`,
   Manufacturer `LECG Arquitectura`, Model `Arq. Luis Eduardo Cortés`
   (same values the current code writes).
5. Appearance: ensure asset named `DisplayName`, apply textures (§3),
   assign `AppearanceAssetId`, `UseRenderAppearanceForShading = true`.

One transaction per material so a failure rolls back only that material.
Exceptions are caught per material, logged with the slug, and the batch
continues. The result object counts created, updated, skipped, failed.

### 5. Existing single-material creator

`MaterialAppearanceAssetService.ApplyPbrTextures` is replaced by a call
into §3 after baking through §2 with `OutputRoot = <textureFolder>/_revit`.
Its request model drops `DisplacementPath`; the XAML row and view-model
properties for Displacement are removed. The legacy `ApplyTextures`
(Generic schema, used by `CreatePBRMaterial(doc, name, folder)`) stays
untouched.

Dead property names removed: `generic_bump_map_type`, `generic_metalness`,
`generic_glossiness_is_roughness`, `generic_displacement`, and the AO to
`generic_self_illum_filter_map` mapping.

### 6. Batch UI

Window `SubstanceBatchView` (`LecgWindow`), view-model
`SubstanceBatchViewModel`, row view-model `SubstanceMaterialRowViewModel`,
category view-model `SubstanceCategoryViewModel`. New ribbon button
"Substance Batch" beside "Material Creator". Command
`SubstanceBatchCommand`.

Layout:
- Header row: Library root (browse, default `C:\LECG\SubstanceBakes`),
  Output folder (default `<root>\_revit`), Resolution combo (2K, 4K),
  Size mm (numeric, default 2500), Overwrite toggle, Force re-bake toggle.
- Left pane: categories with tri-state checkbox and `(n)` count, plus
  All / None.
- Center: rows, virtualized `ListView`. Columns: checkbox, Name,
  Category, Maps, Status. Maps column renders six chips `BC N R M AO OP`;
  filled chip when the map exists, hollow when absent. `M` chip is
  filled only when the metallic map is non-trivial (max > 10%). The
  manifest cannot tell this, so the row computes it on first display
  from a 64 px decode of the metallic map and caches the result. Status
  shows `In doc` when a material with that name already exists.
- Search box filters rows by name or category, substring, ignore-case.
- Footer: "{selected} selected · {inDoc} already in document ·
  {est} MB to bake", Cancel, Create.

Run: the command closes the window, opens the existing log window, and
processes selected rows in order with `UpdateProgress`. A Cancel in the
log window stops after the current material. Final summary line lists
counts and the first ten failures.

Persisted settings (library root, output root, resolution, size) go to
the existing user settings mechanism if one exists in `Core`; otherwise a
small JSON file under `%APPDATA%\LECG\substance-batch.json`.

### 7. Diagnostic command

`DumpAppearanceAssetCommand`: pick a material by name from a small input
dialog (or the first selected element's material), walk its appearance
asset recursively, and log every property name, type, value, and
connected asset schema. Also logs the names of all appearance assets
returned by `GetAssets`. Ribbon: hidden behind the existing dev/tools
panel if one exists, else a plain button labeled "Dump Asset".

Purpose: confirm `surface_normal` uses the BumpMap schema on an Autodesk
library material before the first batch run, and learn the Advanced
Opaque template's asset name.

## Data flow

```
SubstanceBatchView ──select──▶ SubstanceBatchCommand
   │ entries (scan)                    │ per entry
   ▼                                   ▼
SubstanceLibraryScanService     PbrTextureBakeService (bg task)
                                       │ BakedTextureSet
                                       ▼
                         SubstanceMaterialCreateService (main thread, 1 txn)
                                       │
                         AdvancedAppearanceAssetService
                                       │
                         MaterialBitmapPropertyService (UnifiedBitmap / BumpMap)
```

## Error handling

- Scan: bad manifest JSON or missing required channel → warning list
  shown in the window header ("3 materials skipped, see log"), never a
  crash.
- Bake: decode failure → material fails, logged, batch continues.
- Template not found → batch aborts before the first material with the
  asset-name list in the message.
- Property not found on an asset → logged at warning level with the
  property name; the material is still created so the user sees partial
  results rather than nothing.

## Testing (LECG.Tests, no Revit dependency)

- `SubstanceLibraryScanServiceTests`: fixture folder with three
  manifests (full, missing AO with odd filename, glass with numeric IOR)
  plus one malformed. Asserts entries, DisplayName, warnings, ordering.
- `DisplayNameTests`: `asphalt_rough` → `Asphalt Rough`,
  `clean_asphalt_ground_patch_01` → `Clean Asphalt Ground Patch 01`.
- `PbrTextureBakeServiceTests`: 4×4 synthetic PNGs. Green inversion,
  AO multiply, F0 lerp values at metallic 0 and 1, F0 skipped under
  threshold, albedo scaled when metal, skip-if-fresh, force re-bake,
  output paths.
- `TextureTransformTests`: mm to feet conversion.
- `SubstanceBatchViewModelTests`: category tri-state, search filter,
  selected count, in-doc flag from an injected name set.

Revit-dependent services are exercised manually via the diagnostic
command and one real batch of a small category (Ceiling, 2 materials).

## Out of scope (v1)

- Advanced Metal, Layered (car paint coat), Transparent (glass IOR).
- Per-category or per-row size overrides.
- Thumbnails in the batch grid.
- Height/displacement anywhere.
- Regenerating bakes from `.sbsar`.

## Runtime findings (Task 13)

Verified in Revit 2026.5: Advanced Opaque `Name`/`BaseSchema` = `PrismOpaqueSchema`; `surface_normal` connected `Name`/`BaseSchema` = `BumpMapSchema`, `bumpmap_Type` = 1. The `BumpMap` creation string works. Appearance distance units are inches on this template; the shared bitmap service now converts internal feet into the property's units, preserving the requested 2500 mm size.

The runtime smoke passed for 2 Ceiling, 28 Metal, and 1 opacity material. The source Ceiling manifests have no opacity channel, so a construction rebar grid tested cutout wiring. Visual relief direction remains unverified after the user stopped desktop control. Full evidence and PBR root-folder integration: [validation report](../../review/16-substance-batch-validation.md).
