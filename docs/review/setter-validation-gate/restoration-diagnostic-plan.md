# Restoration diagnostic — no qualification credit

The first dedicated run (20260909T054746-f3bf2dc25d9d46c2aa7cac6638bb7f1a)
aborted after MassLevelData.ConceptualConstructionId. The target restored, but the
observable snapshot of element 1462965 did not. All 13 subsequent cases were refused
before opening a document. Source and unsaved-copy hashes verified at cleanup.

Repeat only that case through ricaun.RevitTest on a fresh detached copy of the same
frozen Golden Nugget sample. Keep the same generator and all rollback requirements.
Capture readable parameter details for element 1462965 before/after, in addition to
the existing snapshot hashes. Do not omit the element, normalize its values, weaken
restoration requirements, merge this diagnostic into the ledger or revise the forecast.
Stop the run after this single diagnostic; decide next steps from the actual delta.

## No-write control, registered after that diagnostic

The repeat identified parameter -1006490, SPOT_ELEV_SINGLE_OR_UPPER_VALUE, on a
spot elevation (category -2000263). Its exact double representation changed from
0 to 8E-323. This is not permission to add an epsilon or exclude the parameter.
Run one control on a fresh copy: keep snapshot, empty inner transaction,
regeneration, commit and outer rollback, but skip the property setter entirely.
Mark the receipt classification_only and no_write_control; prohibit ledger credit.
Keep identical restoration checks. This distinguishes an observable control failure
from a setter-specific effect without modifying production code or test fixtures.
