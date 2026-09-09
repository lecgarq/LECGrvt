# Dedicated-collection batch — frozen before execution

Run the exact 14 `dedicated_collection` operations from elementid-classification.csv,
separately from the 18 class-collector operations. Do not revise the original
18–34/70 estimate until this batch is complete. CableType.ConductorMaterial is
already validated; it is a positive control, not a potential fourteenth new success.

Use the frozen 12-model Autodesk corpus. For each operation, consider only models
whose historical receipt recorded roundtrip_only, ordered by source file size then
name. Use a fresh detached copy per operation/model; stop that operation after the
first accepted changed-value result, otherwise exhaust its historical reachable
models. Never count unopened models as attempted. No new element fixtures, family
documents, enabling unrelated properties, arbitrary IDs or class-based substitutes.

Candidates come from the documented collection in the classification. Stair profile
collections use the specific ProfileFamilyUsage and single-loop restriction, with
existing HasTreads/HasRisers gates. No default/sentinel fallback is added in this
batch. Use the first different eligible ID by numeric identity, the first existing
eligible target by ElementId, and at most one write per model/operation. An empty
or singleton set is a generator refusal, not an API exception or a universal failure.

Retain the pilot's committed-inner-transaction/read-back/outer-rollback checks and
fresh-copy disposal. Retain explicit unreadable-parameter reporting. No complete
internal-state restoration or document-reuse claim. Abort the batch after an
isolation failure. All runs use the installed Revit 2026.5 API, while compilation
uses pinned Nice3point references 2026.4.10 (separately runtime-gated).

Separately probe ViewSheetSet.IsAutomatic on the same Electrical sample as the pilot:
read before; set negation; compare the same wrapper and a fresh wrapper; make that
set current in the document's ViewSheetSetting; call Save inside the transaction;
commit/read fresh; rollback/verify. PrintRange.Select is set on the local PrintManager
only to access ViewSheetSetting; never Apply or SubmitPrint. This is a classification
probe of a compound persistence step, never credit for the standalone setter.

After the 14, revise the estimate at most once using actual outcomes and disclose
that a documented collection can still be empty or contain context-invalid choices.
Keep the 18 class-collector cases as the next separate batch, not mixed into this run.
