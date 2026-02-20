---
phase: 10
plan: 1
wave: 1
---

# Plan 10.1: Purge Parameter Service — Core Logic

## Objective

Create `IPurgeParameterService` and `PurgeParameterService` that opens each loaded family in the project, scans its FamilyManager for unused parameters, and safely removes them. A parameter is considered SAFE TO DELETE only if ALL of the following are true:

1. It is NOT a built-in parameter (BuiltInParameter)
2. It has NO formula (`FamilyParameter.Formula` is null/empty)
3. It is NOT referenced by ANY other parameter's formula (no param uses it in their formula string)
4. It is NOT a dimension label (`Dimension.FamilyLabel` does not reference it)
5. It is NOT associated with any nested family instance parameter (not mapped via `FamilyInstance.GetAssociatedFamilyParameter`)
6. It is NOT a reporting parameter (`IsReporting == false`)

## Context

- `src/Services/PurgeMaterialService.cs` — Follow this service pattern exactly
- `src/Services/Interfaces/IPurgeMaterialService.cs` — Interface pattern
- `src/Services/PurgeDeleteElementService.cs` — Reuse for logging pattern
- Revit API: `Document.EditFamily()` opens family doc, `FamilyManager.RemoveParameter()` deletes, `FamilyManager.Parameters` enumerates, `Dimension.FamilyLabel` checks constraints

## Tasks

### Task 1: Create Interface

**File**: `src/Services/Interfaces/IPurgeParameterService.cs`

Create the interface following the exact pattern of `IPurgeMaterialService`:

```csharp
public interface IPurgeParameterService
{
    int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null);
}
```

### Task 2: Create PurgeParameterService

**File**: `src/Services/PurgeParameterService.cs`

Implement the full service with the following algorithm:

```
PurgeUnusedParameters(doc, logCallback):
  1. Collect all Family elements in the project
     - FilteredElementCollector(doc).OfClass(typeof(Family))
  2. For each Family:
     a. Skip if !family.IsEditable
     b. Open family document: familyDoc = doc.EditFamily(family)
     c. Get FamilyManager: fm = familyDoc.FamilyManager
     d. Build safety sets:
        - formulaReferencedParams: scan ALL params, parse their Formula strings for param names
        - dimensionLabelParams: scan ALL Dimensions in familyDoc, collect FamilyLabel params
        - nestedAssociatedParams: scan ALL FamilyInstance elements in familyDoc,
          for each instance call GetAssociatedFamilyParameter() on each of its parameters
          to find params in the host family that are associated/mapped
     e. For each FamilyParameter fp in fm.Parameters:
        - Skip if fp.Id.Value < 0 (built-in)
        - Skip if fp.IsReporting
        - Skip if fp.Formula is not null/empty
        - Skip if formulaReferencedParams.Contains(fp)
        - Skip if dimensionLabelParams.Contains(fp)
        - Skip if nestedAssociatedParams.Contains(fp)
        - SAFE: add to deletionList
     f. Delete each param in deletionList via fm.RemoveParameter(fp)
     g. If any deleted: LoadFamily(doc) to push changes back, close familyDoc
     h. If none deleted: close familyDoc without loading
  3. Return total count deleted
```

**Critical safety notes:**
- Wrap each family processing in try/catch to avoid crashing on corrupt families
- Use `familyDoc.Close(false)` to close without saving if no changes
- Use `familyDoc.LoadFamily(doc)` BEFORE close to push changes back
- Each param removal should be inside a transaction in the family document
- Log: family name, param name, reason for skip, count deleted per family

### Verification

- Build succeeds: `dotnet build LECG.csproj -c Release`
- Interface exists at expected path
- Service follows `IPurgeMaterialService` naming + constructor patterns

### Done Criteria

- `IPurgeParameterService.cs` created with single method signature
- `PurgeParameterService.cs` created with full safety-check algorithm
- All 6 safety conditions implemented (built-in, formula, formula-ref, dimension-label, nested-association, reporting)
- Build succeeds with zero errors
