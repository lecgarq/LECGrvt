# Substance batch validation and consumer contract — 2026-09-10

## Verified Revit workflow

Open **LECG → PBR Material**, choose the `SubstanceBakes` root, and use the batch review window. The scanner selects all categories instead of requiring the Asphalt folder. The initial physical texture size is **2500 mm**. The ribbon exposes PBR Material and Render Match; the batch implementation remains available through PBR Material.

Revit 2026.5 smoke testing against a disposable live model resolved the window and scanned **534 materials in 28 categories with zero warnings**. It verified an Advanced Opaque appearance, linked texture transforms, `bumpmap_Type = 1`, and a stored scale of 98.4251968503937 inches, which converts to 2500 mm. The post-contract runtime log is `C:\Users\luis.cortes\AppData\Local\LECG\SubstanceSmoke\20260910-104542\result.log`; it ends with `PASS: all runtime assertions`. Its Ceiling sidecar reports contract version 1, Revit consumer, DirectX source, OpenGL output, AO embedded in albedo, and derived output.

`bumpmap_Type = 1` is Autodesk's `BumpmapType.NormalMap` value. Autodesk documents the shared `surface_normal` appearance slot as consuming an OpenGL-convention tangent-space normal. The canonical Substance library is the Unreal source and uses DirectX normals where the convention is resolved. Revit therefore writes a separate green-inverted `*_normal_gl.png` file. A declared OpenGL input is now passed through without inversion, and any other declared value is rejected.

Legacy manifests without `normal_format` keep the library's audited DirectX default so the existing 534-material batch remains available. The audit verified 508 library normals as DirectX and found none as OpenGL. The remaining 26 are unresolved: 16 are spatially constant, one has constant green, and ten nonconstant cases still require the retained Unreal A/B visual ruling. This limit applies to orientation confidence, not library completeness or batch creation.

The unresolved set is: Glass — `flint_glass_bottle`, `glass_smoked_02`; Granite — `kashmir_white_granite`; Marble — `botticino_flemish_bond_tiles`, `botticino_marble`, `calacatta_marble`, `carrara_marble`, `macael_marble`, `marble_emperador_brown`, `stylized_soft_gray_marble`, `stylized_white_marble`, `yule_marble`; Metal — `aluminium_polished`, `brass`, `brush_painted_metal`, `copper`, `cross_brushed_metal`, `gold_polished`, `metal_grinded`, `sandblasted_aluminium`, `silver_glossy`, `steel`, `steel_polished`; Plastic — `nebula_marble_acrylic_polymer`, `plastic_line_grain_thin`, `white_stone_acrylic_polymer`. Unreal retains `flip_green_channel = True` for these 26. Revit applies the legacy DirectX default when their manifests do not declare a format. No cross-renderer relief-equivalence claim is made for this unresolved set.

## Two consumers, two outputs

`C:\LECG\SubstanceBakes\<Category>\<Material>` is the canonical, regenerable source used by Unreal. Unreal imports the canonical maps, keeps ambient occlusion and metallic data separate, and does not read `_revit`. Its verified DirectX textures use `flip_green_channel = False`.

`C:\LECG\SubstanceBakes\_revit\<Category>\<Material>` is a one-way derived output used only by Revit. The bake:

- converts DirectX normals to OpenGL by inverting green;
- multiplies ambient occlusion into base color when AO exists;
- converts metallic response to an F0 texture and scales metallic contribution out of albedo;
- converts roughness to an 8-bit grayscale output;
- carries optional opacity as a cutout map; and
- does not use height.

These transformations are lossy. `_revit` must never be imported into Unreal, treated as canonical source, or used to rebuild the original maps. Each new `*_bake.json` records `consumer = Revit`, `derivedOutput = true`, source and output normal conventions, whether the source convention was declared, and whether AO was baked into base color. The contract version invalidates older sidecars so the next selected bake rewrites them under this recorded contract.

## Count reconciliation

There are **534 real Substance material directories**, each with one manifest and the required BaseColor, Normal, Roughness, and Metallic channels. The earlier Unreal count of 535 came from `enumerate_library()`: it returned those 534 materials plus `_derived_variants`, a bookkeeping record for `MI_glass_smoked_02_Thin → MI_glass_smoked_02`. It has no category and is not a material. Nothing dropped from the Revit scan.

The separate total of 555 normal files includes 534 canonical library normals, 20 Revit exports, and one generated detail normal. It is a file count, not a material count.

## Merge order

`feature/purge-substance-integration` is one commit ahead of `feature/purge-unused-maturity` at the shared base `aaff35e`. Merge the integration branch before resuming setter work, or resume setters from the integration branch. Continuing setters first on the old branch would create avoidable divergence in the same repository.

The contract update passed **331 tests** with 5 intentional skips. The Release build completed with zero warnings and zero errors. The integration branch remains unmerged for review.

## Primary references

- [Revit 2026 `BumpmapType` enum](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/c86fe875-6d55-b286-c28d-7e0bcc243f2a.htm): `NormalMap = 1`.
- [Autodesk `surface_normal` appearance contract](https://help.autodesk.com/cloudhelp/ENU/Fusion-360-API/files/core_Appearance_normalTexture.htm): the shared appearance slot interprets OpenGL-convention tangent-space normals.
- [Unreal Engine 5.8 `UTexture`](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/UTexture?lang=en-US): `bFlipGreenChannel` inverts the green channel.
