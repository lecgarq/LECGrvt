# REQUIREMENTS.md

| ID | Requirement | Source | Status |
|----|-------------|--------|--------|
| REQ-01 | Fix blank element name/category in grid | Goal 2 | Complete (Phase 3 Plans 03-00..03-09; manual Revit acceptance 2026-05-09 — 22/22 verification items PASS across Batch Rename, 6 text-summary screens, 8 selection-backed screens, LogView fallback warnings) |
| REQ-02 | Implement "Safe Rename" for formula-referenced parameters | Goal 1 | Pending |
| REQ-03 | Implement "Safe Rename" for dimension-label parameters | Goal 1 | Pending |
| REQ-04 | Provide "Reason for Skip" in Batch Rename UI/Logs | Goal 3 | Pending |
| REQ-05 | Unit tests for all renaming services | Goal 4 | Pending |
| REQ-06 | Consolidate Renaming services | Goal 4 | Complete |
| REQ-07 | Fix FormulaAutoGrouping: parameters with formulas must reliably move to "Other" group without rollback | Bug Fix | Complete (SubTransaction loop + EnsureCurrentType guard + clear-replace-restore for cross-references; xUnit green 79/79; manual Revit deferred by user trust) |
| REQ-08 | Compact Styles must not merge visually different text styles on localized Revit (locale-safe parameter lookup) | Audit 2026-05-08 | Complete (BuiltInParameter locale-safe lookups + orientation sentinel) |
| REQ-09 | Category Changer must transplant ALL instance location types (LocationCurve, hosted, face-hosted) or refuse with a clear log — no silent partial success | Audit 2026-05-08 | Complete (refuse path implemented; hosted/curve placement deferred to v1.2) |
| REQ-10 | Convert Family must preserve host, location curve, and original symbol identity, and must not delete originals before new family loads | Audit 2026-05-08 | Complete (load-before-delete + pre-flight + exact symbol match + hosted/curve placement; face-hosted deferred to v1.2) |
