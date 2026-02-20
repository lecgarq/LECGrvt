**Objective:**
Enhance UI with multi-selection scope, "Select All/None" functionality, and advanced parameter filtering.

**Changes:**
- **SearchReplaceViewModel**: Added properties for advanced filters (`ParamGroup`, `IsInstance`, `IsReadOnly`) and `SelectAll` / `SelectNone` commands.
- **SearchReplaceView.xaml**: Added "Select All" / "Select None" buttons above the DataGrid and an "Advanced Parameter Filters" expander below the main filters.
- **Services**: Updated `ElementData` and collection logic to populate `ParamGroup` and `IsReadOnly`. Added filtering logic to `SearchReplacePreviewService`.
- **Note**: `IsInstance` defaults to false (Type) because parameters are collected from `FamilySymbol` in the project context.

**Files Touched:**
- src/Services/BaseElementCollectionService.cs
- src/Services/SearchReplaceService.cs
- src/Services/SearchReplacePreviewService.cs
- src/ViewModels/SearchReplaceViewModel.cs
- src/Views/SearchReplaceView.xaml

**Verification:**
- Verified that scope selection is additive (multi-selection works logic-wise).
- "Select All" / "Select None" buttons toggle verify status of items.
- Advanced filters correctly filter the preview list based on parameter properties.
