# ViewSheetSet persistence classification

Revision and model hashes are frozen by `project-view-sheet-set-manifest.json`.
Run only against a fresh detached copy of the CASA EUCALIPTO architecture
model; never save or modify the source model.

The installed Revit 2026 API documentation defines
`ViewSheetSetting.Save()` as the operation that saves changes to the current
view/sheet set. For each of `IsAutomatic`, `SheetOrganizationId`, and
`ViewOrganizationId`, load the saved set into `CurrentViewSheetSet`, mutate
that returned wrapper, call `Save()`, commit, read the saved element again,
then roll back the outer transaction group and verify restoration.

This is classification evidence only. Even if read-back changes, keep the
ledger state and 668/805 numerator unchanged because the generic `api.set`
contract does not perform the required `ViewSheetSetting.Save()` workflow.
Record whether the compound workflow persists, the exact exception or
read-back mismatch, rollback evidence, source hash, and copy cleanup.
