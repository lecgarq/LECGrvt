# Writable snapshot amendment — approved conditional scope, now established

The user authorized this amendment if the exact offending parameter is read-only.
The live read-only probe confirmed IsReadOnly=true for element 1462965, parameter
-1006490 (SPOT_ELEV_SINGLE_OR_UPPER_VALUE), in the original Golden Nugget sample.
No transaction or setter was used in that probe; cleanup and source hashes verified.
The earlier no-write control reproduced the mismatch, so it was not setter-specific.

## Boundary

For every non-target element, compare all element identities, type/group/category,
pinned state and parameter identities/writability flags. Compare parameter values
only where Revit reports IsReadOnly=false. This is an operational writable-state
boundary, not an assertion that IsReadOnly reveals every native storage mechanism.
Do not read excluded values or normalize doubles. A parameter becoming read-only
or writable changes its snapshot and still fails restoration.

For the target element, compare ALL readable parameter values, including read-only
parameters, and its metadata. Independently compare the exact target API property.
Retain warning-set checks, committed fresh read-back, verified transaction-group
rollback, source hashes and unsaved-copy checks. Undefined parameter definitions
remain explicit observational gaps, never silently treated as verified.

Report excluded read-only value counts before/after separately from unreadable gaps.
Do not describe this scoped snapshot as complete document/internal-state restoration.
Fresh detached disposable copies per operation/model remain mandatory.

## Execution and evidence

Run the existing no-write control under this boundary first. It must pass before
rerunning all 14 dedicated cases. Keep the original candidate rules, model hashes,
model order, stop-after-first-success rule and strict infrastructure-abort behavior.
No sample, parameter identity or setter is quarantined. No fixtures created.
The 18 class-collector cases stay outside this batch. Original estimate stays
18–34/70 until all 14 finish; revise at most once afterward.

Freeze a new dedicated-writable-manifest.json that links the original manifest,
this amendment and the exact IsReadOnly receipt by hash. Preserve prior failed
receipts and their original manifests; do not rewrite past evidence.
