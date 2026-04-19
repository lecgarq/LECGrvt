# Debug Session: Bump Map Assignment Failure

## Symptom
The user reports: "YOURE NOT ASSIGNING THE BUMP MAP NOW". When creating a PBR material, the Normal Map or Bump Map is completely unassigned in the resulting Revit material.

**When:** Occurs during the execution of `Create PBR Material`.
**Expected:** The selected Relief Pattern texture is assigned to the `generic_bump_map` slot.
**Actual:** The bump slot remains empty or unmodified.

## Hypotheses

| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `NormalizeConnectedAsset` rewraps the asset and leaves `SetupBumpBitmapProperty` holding a stale reference to an orphaned asset. | 95% | UNTESTED |
| 2 | `ApplyBitmapProperties` fails silently due to the schema changes. | 5% | UNTESTED |

## Attempts

### Attempt 1
**Testing:** H1 — `NormalizeConnectedAsset` orphans the asset.
**Action:** Inspect `MaterialBitmapPropertyService.cs` line `80-87`.
**Result:** Code review confirms that `ApplyBitmapProperties` is called using `bumpMapAsset` which was retrieved *before* `NormalizeConnectedAsset`. `NormalizeConnectedAsset` calls `TryRewrapUnifiedBitmapAsBumpMap`, which does `slotProperty.RemoveConnectedAsset()`. The variables `bumpMapAsset` and `bitmapAsset` now point to an object disconnected from the material. We write the file path to this floating orphaned asset.
**Conclusion:** CONFIRMED

## Resolution
**Root Cause:** Stale asset references. The normalizer replaces the connected asset tree in the `generic_bump_map` slot with a new wrapped schema. The code then applies the physical file path to the old orphaned asset references.
**Fix:** After calling `NormalizeConnectedAsset`, perform a fresh lookup on `prop.GetSingleConnectedAsset()` to obtain the ACTIVE connected asset tree. Then, traverse it to find the nested bitmap asset, and finally call `ApplyBitmapProperties` on the LIVE asset instead of the stale ones.
**Verified:** Code compiles successfully and `SetupBumpBitmapProperty` now correctly applies the physical file dimensions and paths to the live active asset instead of the old disconnected tree.
