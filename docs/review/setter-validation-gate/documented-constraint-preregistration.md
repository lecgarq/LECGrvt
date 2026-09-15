# Documented-constraint setter batch — frozen before execution

Test four remaining same-value setters whose alternative values are established by
Revit 2026 API predicates or collections. Do not infer compatible IDs by class name,
cycle arbitrary enum values, or invent numeric offsets.

- Both energy export-category setters may receive only `OST_Rooms` or
  `OST_MEPSpaces`, filtered by `EnergyDataSettings.CheckExportCategory`.
- `AssemblyInstance.NamingCategoryId` candidates come only from the target
  assembly's actual member categories and must pass
  `AssemblyInstance.IsValidNamingCategory(document, categoryId, memberIds)`.
- `LoadCase.SubcategoryId` candidates come only from `OST_LoadCases`
  subcategories and must pass the target's `IsLoadCaseSubcategoryId` predicate.

Use the frozen per-case model order. Prefer an original 12-corpus model so a
successful receipt replaces its earlier same-value outcome by identical source hash;
the four user-authorized CASA EUCALIPTO fixtures are fallbacks. Project originals are
read-only: resolve manifest-relative paths beneath `LECG_PROJECT_FIXTURE_ROOT`, copy
to the run directory, detach, unload links, close without saving, verify source hash,
and delete the copy. Abort after any isolation, rollback, source-hash, or cleanup
failure. Never open a family document and never broaden the MCP write contract.

Stop each case after its first validated receipt; otherwise exhaust its frozen order.
Evidence proves only the named Revit binary, revision, model hash, and context.
