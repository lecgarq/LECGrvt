# RESEARCH 16: Silent Family Processing Engine

## Objective
Design a high-performance background service that can modify family properties (Categories, Parameters, Geometry) and reload them into the project without UI overhead.

## 1. Technical Strategy: Silent Family Modification
To change a family category or convert content "in-place":
1.  **Open**: Use `Document.EditFamily(Family)` to get the family document instance (`familyDoc`).
2.  **Modify**:
    - For **Category Changer**: Access `familyDoc.OwnerFamily.FamilyCategory` and set it to the new `Category`. Note: Check if the category is valid for family usage via `Category.IsFamilyCategory`.
    - For **Convert Family**: Replace geometry/parameters inside the `familyDoc`.
3.  **Reload**: Use `familyDoc.LoadFamily(projectDoc, IFamilyLoadOptions)` with a choice to overwrite parameter values.

## 2. Performance Optimizations
- **Transaction Batching**: Run multiple family edits within a single project transaction if possible to reduce redraws.
- **Background Mode**: Ensure `IFamilyLoadOptions` is used to suppress "Family already exists" prompts.
- **Memory Management**: Explicitly `Close` each `familyDoc` after loading to prevent Revit memory bloat.
- **Lazy Loading**: Only open the family if the target category is different from the current one.

## 3. Category Filter Logic
The user specifically wants categories that families can inherit. 
- API Check: `Category.IsFamilyCategory` or `Category.CategoryType == CategoryType.Model`.
- Exclude "System Families" (Walls, Floors, Roofs) which cannot be created/saved as separate `.rfa` files.

## 4. "In-Place Replace" Logic
In `ConvertFamily V2`, to keep the same family and replace:
1.  Identify the existing Family and the new Geometry.
2.  Open the existing Family via `EditFamily`.
3.  Wipe existing geometry, import new geometry, reload into project.
4.  This preserves all existing instances (same Family/Symbol IDs).

## Success Criteria for Phase 16
- [ ] A reusable `IFamilyEditorService` that handles the open/modify/reload lifecycle.
- [ ] High responsiveness (minimal lag between "Run" and "Complete" for single items).
- [ ] Support for batching.
