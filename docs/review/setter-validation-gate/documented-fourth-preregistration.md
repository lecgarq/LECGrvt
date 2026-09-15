# Documented changed-value batch 4 preregistration

## Scope and expected yield

Six remaining `same_value_only` view/sheet setters. Expected yield before execution: **1-6 validated of 6**. The broad range reflects that saved view positions, sheet collections, and multiple browser organizations are optional document content.

## Generators

- `View.AnalysisDisplayStyleId`: only target a view for which `AllowsAnalysisDisplay()` is true, then choose another existing `AnalysisDisplayStyle`.
- `View.ViewPositionId`: choose another existing `ViewPosition` element, including replacing `InvalidElementId` when a saved position exists.
- `ViewSheet.SheetCollectionId`: only target non-assembly sheets, then choose another existing `SheetCollection`.
- `ViewSheetSet.IsAutomatic`: only change `true` to `false`; the earlier false-to-true probe failed because an automatic set requires compatible organization state.
- Sheet/view organization IDs: only target automatic sets and choose another existing `BrowserOrganization` of the exact compatible type.

## Corpus and isolation

Installed Autodesk samples are tried first, followed where useful by CASA EUCALIPTO structural or MEP fixtures. The slow architecture project is excluded. Every operation uses a fresh detached disposable copy, rollback, close-without-save, source-hash verification, and physical copy removal.
