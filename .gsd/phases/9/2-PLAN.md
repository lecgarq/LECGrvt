---
phase: 9
plan: 2
wave: 2
---

# Plan 9.2: Per-Scope Advanced Filters

## Objective
Each scope should show its own contextual advanced filters, not just the parameter-specific ones.

## Context
- src/ViewModels/SearchReplaceViewModel.cs — Filter properties and preview logic
- src/Services/SearchReplacePreviewService.cs — FilterProcessing logic  
- src/Views/SearchReplaceView.xaml — Advanced filter UI section
- src/Services/BaseElementCollectionService.cs — Element collection (to see what data is available)

## Tasks

<task type="auto">
  <name>Add per-scope filter properties to ViewModel</name>
  <files>src/ViewModels/SearchReplaceViewModel.cs</files>
  <action>
    Add a computed property `ActiveScope` that returns which scope is currently active (string).
    
    Add contextual filter properties for each scope:
    - Types: filter by Revit Category (already have FilterCategory)
    - Families: filter by Category (already have FilterCategory)
    - Views: filter by ViewType (FloorPlan, Section, Elevation, etc.)
    - Sheets: filter by SheetNumber prefix
    - Materials: no extra filters needed (Category filter suffices)
    - Object Styles: no extra filters
    - Line Styles: no extra filters
    - Fill Patterns: no extra filters
    - Parameters: keep existing ParamGroup, Instance/Type, Editable filters
    
    Key new properties:
    - `[ObservableProperty] private string _filterViewType = "All";`
    - `[ObservableProperty] private ObservableCollection<string> _availableViewTypes`
    
    Update RefreshScope() to populate available view types when Views scope is active.
    
    Add a computed property or method that the XAML can use to show/hide filter sections:
    - `public bool IsParameterScope => ScopeFamilyParameterName;`
    - `public bool IsViewScope => ScopeViewName;`
    - `public bool IsSheetScope => ScopeSheetName;`
    - etc.
    
    Raise PropertyChanged for these when scope changes.
  </action>
  <verify>dotnet build LECG.csproj -c Release</verify>
  <done>Build succeeds; new filter properties exist</done>
</task>

<task type="auto">
  <name>Add per-scope filter UI and preview logic</name>
  <files>
    src/Views/SearchReplaceView.xaml
    src/Services/SearchReplacePreviewService.cs
  </files>
  <action>
    In SearchReplaceView.xaml:
    Replace the single Expander "Advanced Parameter Filters" with a dynamic section
    that shows different filter controls based on active scope:
    
    - Parameters scope: show ParamGroup, Instance/Type, Editable (as now)
    - Views scope: show ViewType dropdown
    - Sheets: no advanced filters (Category is enough)
    - Types: no advanced filters (Category is enough)  
    - All others: no advanced filters
    
    Use Visibility binding to IsParameterScope, IsViewScope etc.
    
    In SearchReplacePreviewService.cs:
    Add filtering logic for view type:
    ```csharp
    if (el.Type == "View" && !string.IsNullOrEmpty(vm.FilterViewType) && vm.FilterViewType != "All")
    {
        if (!string.Equals(el.Category, vm.FilterViewType, StringComparison.OrdinalIgnoreCase)) continue;
    }
    ```
    Note: View category is already set to ViewType.ToString() in the collector.
  </action>
  <verify>dotnet build LECG.csproj -c Release</verify>
  <done>Build succeeds; per-scope filters show/hide based on active scope; view type filter works</done>
</task>

## Success Criteria
- [ ] Build succeeds
- [ ] Views scope shows ViewType filter
- [ ] Parameters scope shows ParamGroup/Instance/Editable filters
- [ ] Other scopes hide advanced filters (rely on name + category filters)
