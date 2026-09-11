# Substance batch validation and consumer contract — 2026-09-10

## Verified Revit workflow

Open **LECG → PBR Material**, choose the `SubstanceBakes` root, and use the batch review window. The scanner selects all categories instead of requiring the Asphalt folder. The initial physical texture size is **2500 mm**. The ribbon exposes PBR Material and Render Match; the batch implementation remains available through PBR Material.

Revit 2026.5 smoke testing against a disposable live model resolved the window and scanned **534 materials in 28 categories with zero warnings**. It verified an Advanced Opaque appearance, linked texture transforms, `bumpmap_Type = 1`, and a stored scale of 98.4251968503937 inches, which converts to 2500 mm. The strict-contract runtime log is `C:\Users\luis.cortes\AppData\Local\LECG\SubstanceSmoke\20260910-121049\result.log`; it ends with `PASS: all runtime assertions`. Its Ceiling sidecar reports contract version 1, Revit consumer, DirectX source, OpenGL output, AO embedded in albedo, and derived output.

The PBR window also supports relocating an existing library. Choose the new **Baked texture output**, keep the relevant materials selected, and click **Repath Selected**. This updates the existing appearance assets without re-baking or changing their 2500 mm transforms. A live Revit 2026.5 run copied two complete Ceiling texture sets to a second root, repathed six required bitmap slots, and verified that every albedo, roughness, and OpenGL normal path resolved from the new root. The runtime log is `C:\Users\luis.cortes\AppData\Local\LECG\SubstanceSmoke\20260910-193117\result.log`; it ends with `PASS: all runtime assertions`.

`bumpmap_Type = 1` is Autodesk's `BumpmapType.NormalMap` value. Autodesk documents the shared `surface_normal` appearance slot as consuming an OpenGL-convention tangent-space normal. The canonical Substance library is the Unreal source and uses DirectX normals where the convention is resolved. Revit therefore writes a separate green-inverted `*_normal_gl.png` file. A declared OpenGL input is now passed through without inversion, and any other declared value is rejected.

All **534 canonical manifests now declare `normal_format = DirectX`**. Missing declarations and unsupported values are rejected by both consumers instead of receiving a fallback convention. The earlier D1b/D1c audits supplied hash-backed DirectX evidence for 508 maps. Their 182 previously unstamped D1c manifests were reconciled only after every current normal hash matched the approved evidence list.

The remaining 26 were regenerated from their SBSAR sources at 4096 px and 16-bit precision on 2026-09-10. Twenty-four archives expose `normal_format` and were rendered with `sbsrender --set-value normal_format@0`, the DirectX value for these Adobe graphs. `cross_brushed_metal` and `metal_grinded` are older Adobe-authored stock Substances that do not expose the parameter; they were rendered at Adobe's documented stock DirectX default. The two source archives were extracted to verify the Adobe author metadata and absence of the control. Each manifest records the source hash, output hash, renderer version, method, and main repository revision. The D1d audit ends with 26 passes and zero unresolved normals.

## Two consumers, two outputs

`C:\LECG\SubstanceBakes\<Category>\<Material>` is the canonical, regenerable source used by Unreal. Unreal imports the canonical maps, keeps ambient occlusion and metallic data separate, and does not read `_revit`. Its DirectX textures use `flip_green_channel = False`. Live-editor readback after the D1d reimports verified all **534 of 534** normal textures with that flag disabled, normal-map compression, sRGB disabled, virtual-texture streaming enabled, and no dirty packages. A complete library gate verifies 534 material manifests, 28 categories, all required files, and 534 DirectX declarations. `white_marble_base` remains the documented fixed 16×16, 16-bit graph output; all other canonical normals are 4096×4096 and 16-bit.

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

## Mainline provenance

Pull request [#18](https://github.com/lecgarq/LECGrvt/pull/18) merged the completed integration into `main` with linear history. The resulting main revision is `4b5d6c372fe496822c39eb2d37eeaa5093e3c0c7`. Historical setter receipts keep the revision on which they actually ran; `setter-validation-gate/revision-reconciliation.json` maps all eight rebased feature revisions to patch-identical main revisions. Substance D1d evidence and subsequent setter work use mainline revisions.

The completed contract passed **332 tests** with 5 intentional skips. The Release build completed with zero warnings and zero errors.

## Primary references

- [Revit 2026 `BumpmapType` enum](https://help.autodesk.com/cloudhelp/2026/ENU/Revit-API-MainReference/files/html/c86fe875-6d55-b286-c28d-7e0bcc243f2a.htm): `NormalMap = 1`.
- [Autodesk `surface_normal` appearance contract](https://help.autodesk.com/cloudhelp/ENU/Fusion-360-API/files/core_Appearance_normalTexture.htm): the shared appearance slot interprets OpenGL-convention tangent-space normals.
- [Unreal Engine 5.8 `UTexture`](https://dev.epicgames.com/documentation/unreal-engine/API/Runtime/Engine/UTexture?lang=en-US): `bFlipGreenChannel` inverts the green channel.
- [Adobe working with normals](https://experienceleague.adobe.com/en/docs/substance-3d/ecosystem/3d-applications/modo/working-with-normals): stock Substances use DirectX orientation and setting Normal Format to 1 flips them to OpenGL.
- [Adobe normal-format preference](https://experienceleague.adobe.com/en/docs/substance-3d-sampler/using/interface/preferences/normal-format): DirectX is the default and SBSAR hosts may set the exposed normal-format parameter.
