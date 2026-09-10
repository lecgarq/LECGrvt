# Class-collector setter batch results

The frozen 18-case batch in `class-collector-manifest.json` completed in Revit
2026 against the 12 approved Autodesk sample models. The completed run is
`class-collector-runs/20260910T185125-461e924a092c422db154debab769aba5` and its
runner record is
`classcollectors-20260910T183749-ef59e30b3d7d408a9ee058ccae98d0d2.trx`.

- Harness cases: 18 passed, 0 failed.
- Model receipts: 103.
- Properties with a committed changed value: 13.
- New changed-value setter identities: 12. `Material.CutBackgroundPatternId`
  was the preregistered positive control and added a second passing model only.
- Attempted writes: 41, all with verified outer rollback and cleanup.
- Infrastructure, cleanup, rollback, and source-model hash failures: 0.
- Ledger: 647/805 `changed_value_tested`, 41 `same_value_only`, 35
  `context_rejected`, and 82 `missing_fixture`.

The five unresolved properties exhausted all 12 preregistered models:

| Property (`Autodesk.Revit.DB.` omitted) | Recorded outcome |
| --- | --- |
| `Electrical.CircuitNamingSchemeSettings.CircuitNamingSchemeId` | 12 `no_proven_valid_alternative` |
| `View.AnalysisDisplayStyleId` | 10 `no_proven_valid_alternative`; 2 setter rejections stating that the style cannot be set for the view |
| `ViewSheet.SheetCollectionId` | 12 `no_proven_valid_alternative` |
| `ViewSheetSet.SheetOrganizationId` | 8 `no_proven_valid_alternative`; 4 committed without an observable value change |
| `ViewSheetSet.ViewOrganizationId` | 8 `no_proven_valid_alternative`; 4 committed without an observable value change |

The first execution attempt,
`class-collector-runs/20260910T183154-1130276927fa451d981398f7806a5f78`,
aborted after Revit rejected a 268-character disposable model path. Its one
earlier success was not credited. The evidence path builder now uses deterministic
32-character segments; the longest projected disposable path in the completed run
was 218 characters. The entire frozen batch was then rerun from the beginning.

`ledger-reconciliation.json` links the frozen manifest, completed TRX, prior ledger
reconciliation, runtime provenance, and every receipt by SHA-256. The production
ledger's `source_sha256` is the SHA-256 of that reconciliation file.
