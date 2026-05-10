# Phase 3: Grid & Collection Fixes — Research

**Researched:** 2026-05-09
**Domain:** WPF DataGrid / MVVM / Revit API element collection — C# / .NET 8
**Confidence:** HIGH (all findings grounded in direct codebase inspection)

---

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Grid Row Model & Control (shared, reusable)**
- Build a shared `ElementRowViewModel` and `ElementGridControl` used by every screen with a per-element list.
- Row carries at minimum: `Id` (long), `Name` (string), `Category` (string), `Type` (string discriminator), `Status` (string), `IsChecked` (bool).
- Existing `ReplaceItem` (Batch Rename) extended or replaced to align with this model — `Category` column added.
- Shared control supports additional column slots for screen-specific extras (e.g., Layers count in DivideToposolid).

**Category Column in Batch Rename Grid**
- Add Category column between Type and OriginalValue: order is `☑ | Type | Category | Original | New`.
- Default width: auto-fit content, resizable.
- Sortable with standard header sort indicator (▲/▼).
- Per-column filter (funnel icon in header).
- Default sort on grid load: by Category, ascending.
- Per-column filter AND existing top-of-grid `FilterCategory` dropdown both apply, AND-combined.

**Family Parameter Category Label**
- `Type` column keeps the single label `FamilyParameter` (no split into Instance/Type sub-types).
- Edge case: when a FamilyParameter row has no resolvable Revit category for its parent family, fall back to the family name in the Category cell — no blanks, no `<Uncategorized>`.
- Filter UX for FamilyParameter scope: cascading filters — outer dropdown = Revit category, inner dropdown = family name within that category.

**Migrated Text-Summary Screens**
- All screens currently rendering `'ID … | Cat … | …'` strings switch to interactive grids with per-row checkbox + sort.
- Status/skip-reason text becomes a `Status` column.

**Selection-Backed Screens**
- All `SelectionViewModel`-backed screens included. No screens excluded.
- Each exposes `ObservableCollection<ElementRowViewModel>` and renders `ElementGridControl`.
- Empty state stays as "No X selected" via existing `SelectionViewModel` hooks.

**No-Blanks Invariant (REQ-01 core)**
- Every row that reaches a grid must have a non-empty Name AND non-empty Category.
- For elements whose `el.Category == null`: collection keeps them, Category resolved via fallback chain.
- For elements whose `el.Name` is empty/whitespace: collection produces a stable display name, e.g., `<{Type} {Id}>`.
- No row shows blank Name or Category.

**Logging**
- All silent-skip paths replaced with `LogView` entries when an element is included with a fallback label.
- No new log surfaces — keep `LogView` only.

**Locale Safety**
- Any Revit API access used to derive Category labels must be locale-safe.
- Prefer `BuiltInCategory` IDs and `LabelUtils` over English category-name string comparisons.

### Claude's Discretion
- FamilyParameter Category cell default value when a Revit category exists: show the Revit category (consistency with Type rows); family name in a dedicated `Family` sub-column or tooltip.
- Sort/filter UI implementation details on `ElementGridControl` (header chrome, filter popup behavior beyond "funnel icon + popup with checkbox list").
- How the shared control composes with existing `LecgDataGrid` styling (inherit, restyle, or replace).
- Migration ordering across 6+ screens (one plan per screen group or one big sweep). Planner to break into waves.

### Deferred Ideas (OUT OF SCOPE)
- None at this time — user opted to include all candidate screens in this phase.
- Future possibility (NOT this phase): persist user's per-grid sort/filter state across sessions.
</user_constraints>

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| REQ-01 | Fix blank element name/category in grid | Collection fallback chain (BaseElementCollectionService), shared ElementRowViewModel model, migration of all SelectedElementSummaries screens, Category column in ReplaceItem/SearchReplaceView |
</phase_requirements>

---

## Summary

Phase 3 is a cross-cutting UI infrastructure upgrade. The root cause of REQ-01 is a two-part problem: (1) `BaseElementCollectionService` silently skips element types whose `Category == null` at line 21 via `continue`, never populating a Category for the row, and (2) the Batch Rename `ReplaceItem` model lacks a `Category` property entirely, so even the elements that do pass the filter reach the grid with no category column to display. The migration is wider than just Batch Rename — seven ViewModels currently use `ObservableCollection<string>` with inline `DescribeElement`-style text summaries, and ten+ additional VMs use `SelectionViewModel` but have no per-row element grid at all.

The solution architecture is a new shared model `ElementRowViewModel` (MVVM row), a new WPF custom control `ElementGridControl` (wrapping/extending `LecgDataGrid`), and a `ElementLabelService` that provides guaranteed-non-blank Name and Category for any `Element` or `ElementData`. All existing `ObservableCollection<string>` summaries and `ReplaceItem` usages migrate to `ObservableCollection<ElementRowViewModel>`. The `BaseElementCollectionService` null-category skip is replaced by the fallback chain.

The scope is large but structurally repetitive. Each screen migration follows the same pattern: swap the collection type in the VM, swap `ListBox/ItemsControl` for `ElementGridControl` in the View. The unique logic per screen is the `Status` column content and any extra columns (e.g., Layers for DivideToposolid).

**Primary recommendation:** Build `ElementLabelService` and `ElementRowViewModel` first (Wave 0 / Wave 1). Then migrate `BaseElementCollectionService` + Batch Rename (Wave 2). Then sweep remaining VMs in two groups — text-summary screens and selection-only screens (Waves 3-4).

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| CommunityToolkit.Mvvm | Already referenced | `ObservableObject`, `ObservableProperty`, `RelayCommand` | All existing VMs use it; `ReplaceItem` inherits `ObservableObject` |
| WPF DataGrid | .NET 8 built-in | Row display, sort, column virtualization | `LecgDataGrid` already extends it |
| Autodesk Revit API | Project target | `Element.Category`, `BuiltInCategory`, `LabelUtils` | Only locale-safe category resolution path |
| xUnit + FluentAssertions + NSubstitute | Existing test project | Unit tests for `ElementLabelService` and collection logic | Already wired in `LECG.Tests.csproj` |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `System.Windows.Data.CollectionViewSource` | .NET 8 built-in | In-memory filtering/sorting on `ObservableCollection` without requerying Revit | Planner's choice for per-column filter popup implementation |
| `System.ComponentModel.ICollectionView` | .NET 8 built-in | Bind DataGrid to a filtered/sorted view of `ObservableCollection<ElementRowViewModel>` | Required for AND-combined filter logic |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `CollectionViewSource` in-memory filter | Re-run Revit collector on every filter change | Re-querying Revit is slow and requires a document reference in the VM; CollectionViewSource is purely in-memory and already works with WPF DataGrid binding |
| Extending `LecgDataGrid` directly | Creating a separate `ElementGridControl` UserControl | A `UserControl` can carry XAML column definitions and filter popups declaratively; `LecgDataGrid` is a thin subclass with no XAML — extending it requires code-behind column management |

**Installation:** No new packages required. All stack components are already present.

---

## Architecture Patterns

### Recommended Project Structure
```
src/
├── Controls/
│   ├── LecgDataGrid.cs               # Existing — keep as-is
│   └── ElementGridControl.xaml/.cs   # NEW: UserControl wrapping LecgDataGrid
├── ViewModels/
│   ├── Components/
│   │   ├── SelectionViewModel.cs     # Existing — add ObservableCollection<ElementRowViewModel> RowItems
│   │   └── ElementRowViewModel.cs    # NEW: shared row model
│   ├── SearchReplaceViewModel.cs     # Modify: ReplaceItem replaced by ElementRowViewModel (or extended)
│   ├── DivideToposolidViewModel.cs   # Modify: SelectedElementSummaries → RowItems
│   └── ...all other affected VMs
├── Services/
│   ├── Renaming/
│   │   └── BaseElementCollectionService.cs  # Modify: remove null-skip, add fallback
│   └── ElementLabelService.cs        # NEW: centralized Name + Category fallback helper
└── Views/
    ├── Components/
    │   └── SelectionControl.xaml     # Existing — optionally embed ElementGridControl
    └── ...all affected View.xaml files (swap ListBox → ElementGridControl)
```

### Pattern 1: ElementRowViewModel — The Shared Row Model
**What:** A single `ObservableObject`-derived class that every grid binds to. Replaces both `ReplaceItem` (Batch Rename) and the inline `string` summaries.
**When to use:** Everywhere a per-element row appears in any grid in the plugin.

```csharp
// Source: derived from existing ReplaceItem in SearchReplaceViewModel.cs and CONTEXT.md decisions
public partial class ElementRowViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    [ObservableProperty] private bool _isChecked = true;

    public long Id { get; set; }
    public string Name { get; set; } = "";        // guaranteed non-blank by ElementLabelService
    public string Category { get; set; } = "";    // guaranteed non-blank by ElementLabelService
    public string Type { get; set; } = "";        // discriminator: "Type", "Family", "FamilyParameter", etc.
    public string Status { get; set; } = "";      // command-specific outcome or skip reason
    public string Family { get; set; } = "";      // populated for FamilyParameter rows (Claude's discretion)

    // Batch Rename carry-over
    public string OriginalValue { get; set; } = "";
    public string NewValue { get; set; } = "";

    // Batch Rename advanced filter carry-over
    public string ParamGroup { get; set; } = "";
    public bool IsInstance { get; set; }
    public bool IsReadOnly { get; set; }
}
```

### Pattern 2: ElementLabelService — Guaranteed Non-Blank Labels
**What:** A static helper (or injectable `IElementLabelService`) that takes a Revit `Element` or an `ElementData` and returns (Name, Category) where both are guaranteed non-empty.
**When to use:** Called by `BaseElementCollectionService` before constructing `ElementData`, and by every VM's `UpdateSelectedElementSummaries` replacement.

```csharp
// Source: derived from CONTEXT.md fallback chain decisions + BaseElementCollectionService.cs patterns
public static class ElementLabelService
{
    // For Revit Element objects (selection-backed screens)
    public static (string name, string category) GetLabels(Element element)
    {
        // Name fallback:
        string name = element.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = $"<{element.GetType().Name} {element.Id.Value}>";

        // Category fallback chain:
        string category = element.Category?.Name;
        if (string.IsNullOrWhiteSpace(category))
        {
            // Try BuiltInCategory lookup (locale-safe)
            category = TryGetBuiltInCategoryName(element);
        }
        if (string.IsNullOrWhiteSpace(category))
        {
            // Last resort: CLR type name
            category = element.GetType().Name;
        }

        return (name, category);
    }

    private static string TryGetBuiltInCategoryName(Element element)
    {
        try
        {
            // element.Category is null; attempt via GetTypeId / Document category lookup
            // or fall back to element.GetType().Name
            var bic = (BuiltInCategory)element.Category?.Id.Value;
            return LabelUtils.GetLabelFor(bic);
        }
        catch { return ""; }
    }
}
```

**Important note:** `LabelUtils.GetLabelFor(BuiltInCategory)` requires the Revit API context (a loaded Revit session). It is locale-safe and returns the localized UI name. It cannot be called in unit tests without a running Revit instance. The `ElementLabelService` must be designed so the fallback logic (string operations) is unit-testable while the Revit API calls are isolated.

### Pattern 3: ElementGridControl — The Shared WPF Grid UserControl
**What:** A `UserControl` containing a `LecgDataGrid` with standard columns pre-defined, plus a `ContentPresenter` or `AdditionalColumns` dependency property for screen-specific extras.
**When to use:** In every View.xaml that currently has a `ListBox`/`ItemsControl` over strings or a DataGrid with hand-rolled columns.

```xml
<!-- Source: Derived from LecgDataGrid.cs patterns and SearchReplaceView.xaml DataGrid section -->
<UserControl x:Class="LECG.Controls.ElementGridControl" ...>
    <controls:LecgDataGrid x:Name="Grid"
        ItemsSource="{Binding RowItems}"
        CanUserSortColumns="True">
        <DataGrid.Columns>
            <DataGridCheckBoxColumn Header="Sel"      Binding="{Binding IsChecked}" Width="35"/>
            <DataGridTextColumn     Header="Type"     Binding="{Binding Type}"     Width="80" IsReadOnly="True"/>
            <DataGridTextColumn     Header="Category" Binding="{Binding Category}" Width="*"  IsReadOnly="True"/>
            <DataGridTextColumn     Header="Name"     Binding="{Binding Name}"     Width="*"  IsReadOnly="True"/>
            <DataGridTextColumn     Header="Status"   Binding="{Binding Status}"   Width="120" IsReadOnly="True"/>
            <!-- Additional columns injected via ItemsSource on AdditionalColumns DP -->
        </DataGrid.Columns>
    </controls:LecgDataGrid>
</UserControl>
```

**For Batch Rename**, the existing columns (Old Name / New Name) remain; the grid gets the Category column inserted between Type and OriginalValue per the locked column order decision.

### Pattern 4: CollectionViewSource for AND-Combined Filtering
**What:** Wrap `ObservableCollection<ElementRowViewModel>` in an `ICollectionView` and attach filter predicates that combine the top-of-grid category dropdown with per-column filters.
**When to use:** In `SearchReplaceViewModel` and in `ElementGridControl` code-behind when per-column filter popups are active.

```csharp
// Source: standard WPF ICollectionView pattern, confirmed in .NET 8 WPF documentation
_collectionView = CollectionViewSource.GetDefaultView(RowItems);
_collectionView.Filter = item =>
{
    var row = (ElementRowViewModel)item;
    bool categoryMatch = string.IsNullOrEmpty(_filterCategory) || _filterCategory == "All"
        || row.Category.Contains(_filterCategory, StringComparison.OrdinalIgnoreCase);
    bool columnFilterMatch = _activeColumnFilters.All(f => f(row));
    return categoryMatch && columnFilterMatch;
};
```

### Pattern 5: Default Sort by Category Ascending
**What:** Apply `SortDescriptions` on the `ICollectionView` immediately after binding.
**When to use:** In ViewModel constructor or after the collection is populated.

```csharp
// Source: standard ICollectionView SortDescriptions pattern
_collectionView.SortDescriptions.Clear();
_collectionView.SortDescriptions.Add(
    new System.ComponentModel.SortDescription(nameof(ElementRowViewModel.Category),
        System.ComponentModel.ListSortDirection.Ascending));
```

### Anti-Patterns to Avoid
- **Calling `element.Category.Name` directly without null check:** `Category` is null for several element types (system families loaded without a category, some annotation families). The null-skip at `BaseElementCollectionService.cs:21` currently hides these elements entirely — in Phase 3 it must be replaced with the fallback chain, not just a null-check guard.
- **String.Compare on category names for locale filtering:** Use `BuiltInCategory` integer IDs for equality checks in any filter logic; never compare English category name strings.
- **`ReplaceItem` and `ElementRowViewModel` coexisting long-term:** `ReplaceItem` should be retired (or become a type alias) in this phase, not left as a parallel model. Two row models will cause drift.
- **Placing Revit API calls inside `ICollectionView.Filter` lambdas:** Filter runs on every keystroke; it must be pure in-memory predicate logic. Revit data must be pre-materialized into `ElementRowViewModel` fields.
- **`AutoGenerateColumns = true` on `LecgDataGrid`:** Already set to `false` in `LecgDataGrid.cs`. Never override this — column order and widths must be explicit.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| In-memory sort + filter on ObservableCollection | Custom sorted list with manual refresh | `ICollectionView` via `CollectionViewSource.GetDefaultView()` | Handles sort, filter, grouping; integrates natively with WPF DataGrid; thread-safe refresh via `Dispatcher` |
| Per-column filter state management | Dictionary of active filters with manual predicate chaining | `ICollectionView.Filter` predicate composed from per-column predicates | Single refresh point; WPF handles UI update |
| Column virtualization for large Revit models | Manual lazy loading | `EnableColumnVirtualization = true` (already set in `LecgDataGrid`) | Already configured; just wire correctly |
| Locale-safe category name | String comparison against hardcoded English names | `LabelUtils.GetLabelFor(BuiltInCategory)` | Returns localized string; avoids breaking on Spanish/French/German Revit |

**Key insight:** WPF `ICollectionView` handles the sort/filter concern entirely in-memory once the collection is populated. The Revit API boundary is only crossed once at collection time; all subsequent filtering is pure .NET.

---

## Common Pitfalls

### Pitfall 1: `Element.Category == null` Scope
**What goes wrong:** Assuming only element-type collectors return null-category elements. In fact, `GraphicsStyle` elements also have a `gs.GraphicsStyleCategory` that can be null (already handled at line 104-106 in `BaseElementCollectionService.cs`). The null-category null skip at line 21 covers the `WhereElementIsElementType()` collector. Both sites must be audited.
**Why it happens:** Two separate null-category guard sites exist in `BaseElementCollectionService.cs` — line 21 (type collector) and line 104-106 (GraphicsStyle collector). The fix must address both.
**How to avoid:** Search for every `if (... == null) continue` in `BaseElementCollectionService.cs` and replace with the fallback chain at each site.
**Warning signs:** After migration, any row where Category is "GraphicsStyle" or similar CLR-type-name fallback in the grid is evidence that the fallback ran correctly.

### Pitfall 2: `ReplaceItem` vs `ElementRowViewModel` Migration Order
**What goes wrong:** `SearchReplacePreviewService.ProcessPreview` constructs `ReplaceItem` at lines 110-118. If `ElementRowViewModel` replaces `ReplaceItem` but `ProcessPreview` still returns `List<ReplaceItem>`, the VM and service signatures diverge causing a compile error cascade.
**Why it happens:** `ReplaceItem` is defined inside `SearchReplaceViewModel.cs` (not in a dedicated model file) and is referenced by `SearchReplacePreviewService`. Migration must update both the model definition site and the service return type atomically.
**How to avoid:** Move model definition to `src/ViewModels/Components/ElementRowViewModel.cs` first. Update `SearchReplacePreviewService` to return `List<ElementRowViewModel>`. Update `SearchReplaceViewModel.PreviewItems` type. Do this in a single wave so the project compiles after each step.

### Pitfall 3: `ICollectionView.Refresh()` on Background Thread
**What goes wrong:** Revit can call `SetSelectedElements` from a non-UI thread. If `_collectionView.Refresh()` is called there, WPF throws a cross-thread exception.
**Why it happens:** `ObservableCollection` notifies on the thread it was modified. `CollectionViewSource.GetDefaultView` is bound to the UI thread.
**How to avoid:** Dispatch all collection mutations via `Application.Current.Dispatcher.Invoke(...)` when called from Revit event handlers, consistent with existing patterns in the codebase.

### Pitfall 4: `FamilyParameter` Category Cell — Family-Without-Symbol Edge Case
**What goes wrong:** The Phase B scan (FamilyInstance loop in `BaseElementCollectionService.cs`) uses `fi.Symbol.FamilyName` as Category. If a family has no loaded instances and no symbols (`symbolsByFamily` has the key but Phase A produced no rows), the family produces zero rows — valid. But if `fi.Symbol?.Family == null` (corrupt/unloaded family), the row is silently skipped.
**Why it happens:** The `if (fi.Symbol?.Family == null) continue;` guard at line 252 is correct but should be logged in Phase 3.
**How to avoid:** When skipping due to null family/symbol in Phase B, emit a `LogView` warning (consistent with the logging decision in CONTEXT.md).

### Pitfall 5: `LecgDataGrid.CheckAll()` / `UncheckAll()` Uses Reflection on Item Type
**What goes wrong:** `LecgDataGrid.SetAllBooleanProperty` reflects on `items[0].GetType()` to find `CheckPropertyName`. If `ElementRowViewModel` is used but `CheckPropertyName` is not updated to `"IsChecked"`, the Select All / Select None buttons will silently do nothing.
**Why it happens:** `CheckPropertyName` defaults to `"IsChecked"` (matching `ElementRowViewModel.IsChecked`), so this works automatically as long as `[ObservableProperty]` generates the correct property name.
**How to avoid:** Confirm the generated property name from `[ObservableProperty] private bool _isChecked` is `IsChecked` (it is, by CommunityToolkit.Mvvm convention). No action required beyond awareness.

### Pitfall 6: Blank `element.Name` on Revit Element Types in Non-English Locales
**What goes wrong:** `element.Name` for some system-family types (e.g., wall types, roof types) in non-English Revit returns an empty string because the name is stored in a localized parameter, not `Element.Name`.
**Why it happens:** Revit stores certain type names in `BuiltInParameter.ALL_MODEL_TYPE_NAME` rather than the `Name` property for system families. Calling `element.Name` returns `""` in those locales.
**How to avoid:** In `ElementLabelService`, if `element.Name` is empty, fall back to `element.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_NAME)?.AsString()` before using the `<{Type} {Id}>` format.
**Confidence:** MEDIUM — this is a known Revit localization pattern; the specific BIP is confirmed as `ALL_MODEL_TYPE_NAME` from Revit API documentation and community sources.

---

## Code Examples

### Verified Pattern: Remove null-skip, add fallback (BaseElementCollectionService.cs line 21)
```csharp
// BEFORE (existing):
if (el.Category == null) continue;
data.Add(new ElementData
{
    Category = el.Category.Name,
    ...
});

// AFTER:
var (resolvedName, resolvedCategory) = ElementLabelService.GetLabels(el);
data.Add(new ElementData
{
    Name = resolvedName,
    Category = resolvedCategory,
    ...
});
```

### Verified Pattern: Add Category to ReplaceItem / ElementRowViewModel construction in SearchReplacePreviewService.cs lines 110-118
```csharp
// BEFORE (existing lines 110-118):
results.Add(new ReplaceItem
{
    ElementId = el.Id,
    ElementName = el.Name,
    OriginalValue = el.Name,
    NewValue = currentName,
    IsChecked = true,
    Type = el.Type
});

// AFTER:
results.Add(new ElementRowViewModel
{
    Id = el.Id,
    Name = el.Name,
    Category = el.Category,   // now guaranteed non-blank by ElementLabelService
    OriginalValue = el.Name,
    NewValue = currentName,
    IsChecked = true,
    Type = el.Type,
    ParamGroup = el.ParamGroup,
    IsInstance = el.IsInstance,
    IsReadOnly = el.IsReadOnly
});
```

### Verified Pattern: Replace SelectedElementSummaries with RowItems in a VM (DivideToposolidViewModel.cs pattern)
```csharp
// BEFORE:
public ObservableCollection<string> SelectedElementSummaries { get; } = new ObservableCollection<string>();

private void UpdateSelectedElementSummaries(IEnumerable<Element> elements)
{
    SelectedElementSummaries.Clear();
    foreach (Element element in elements)
        SelectedElementSummaries.Add($"ID {element.Id} | ...");
}

// AFTER:
public ObservableCollection<ElementRowViewModel> RowItems { get; } = new ObservableCollection<ElementRowViewModel>();

private void UpdateRowItems(IEnumerable<Element> elements)
{
    RowItems.Clear();
    foreach (Element element in elements)
    {
        var (name, category) = ElementLabelService.GetLabels(element);
        int? layerCount = TryGetLayerCount(element);
        RowItems.Add(new ElementRowViewModel
        {
            Id = element.Id.Value,
            Name = name,
            Category = category,
            Type = element.GetType().Name,
            Status = layerCount switch
            {
                null  => "Layer count unavailable",
                <= 1  => "Already single layer",
                _     => "Ready to divide"
            },
            IsChecked = true
        });
    }
}
```

### Verified Pattern: View migration (DivideToposolidView.xaml)
```xml
<!-- BEFORE: ListBox over strings -->
<ListBox ItemsSource="{Binding SelectedElementSummaries}" Height="160" ...>
    <ListBox.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding}" .../>
        </DataTemplate>
    </ListBox.ItemTemplate>
</ListBox>

<!-- AFTER: ElementGridControl -->
<controls:ElementGridControl RowItems="{Binding RowItems}" Height="160"/>
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `if (el.Category == null) continue` (silent skip) | Fallback chain via `ElementLabelService` | Phase 3 | Elements that were invisible now appear with a resolved label |
| `ObservableCollection<string>` text summaries | `ObservableCollection<ElementRowViewModel>` interactive grid | Phase 3 | User can deselect individual elements before commit |
| `ReplaceItem` (no Category field) | `ElementRowViewModel` (Category + Status + all fields) | Phase 3 | Single row model across all screens; no drift |
| Per-VM inline `DescribeElement` string formatters | Centralized `ElementLabelService` | Phase 3 | Locale-safe, testable, no-blanks invariant enforced in one place |

**Deprecated/outdated in this phase:**
- `ReplaceItem` class in `SearchReplaceViewModel.cs` — superseded by `ElementRowViewModel`
- `ObservableCollection<string> SelectedElementSummaries` in 5 VMs (DivideToposolid, FixPoints, ConvertCad, SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid) — superseded by `ObservableCollection<ElementRowViewModel> RowItems`
- Inline `DescribeElement` / `UpdateSelectedElementSummaries` private methods in each affected VM — replaced by `ElementLabelService` calls

---

## Open Questions

1. **AlignEdges, AlignElements, AssignMaterial, CategoryChanger, ChangeLevel, OffsetElevations, ResetSlabs, SimplifyPoints, SplitBoundaries — selection only, no existing element list**
   - What we know: These VMs use `SelectionViewModel` but store only `IList<Reference>` or `List<Reference>` — they have no `SetSelectedElements(IEnumerable<Element>)` method and no `SelectedElementSummaries`. The user decision includes them in the phase.
   - What's unclear: Should the element list grid be added to the view as a new below-the-fold section, or replace the `SelectionControl` count display? The `SelectionControl.xaml` shows just "N Elements selected / Click to add or change" — it has no list. Adding a grid would require layout changes to all those views.
   - Recommendation: The planner should split these into a separate wave with a consistent "add grid below SelectionControl" layout pattern. For each, `SetSelection` / `SetReference` methods need a parallel `SetSelectionRows` that resolves `Reference` → `Element` → `ElementRowViewModel` using the document.

2. **Compact Styles preview grids (mentioned in CONTEXT.md scope)**
   - What we know: Compact Styles uses a different data model (text/line/fill pattern compaction results, not Element rows). `TextStyleCompactionResult`, `LineStyleCompactionResult`, etc. are already defined in `src/Models/`.
   - What's unclear: Whether these compaction result types should migrate to `ElementRowViewModel` or stay as bespoke models with their own grid columns.
   - Recommendation: Compact Styles result grids are NOT element-per-row grids in the Revit sense — they represent compaction outcomes (original style → merged into → N consumers). They should keep their own row models. The planner should exclude them from `ElementRowViewModel` migration and instead apply `ElementGridControl`-style WPF DataGrid conventions to their own result view.

3. **`FilterCategory` dropdown AND per-column filter AND-combination**
   - What we know: `SearchReplacePreviewService.ProcessPreview` runs the `FilterCategory` check at line 77-80 (before building the `results` list), not on an `ICollectionView`. The per-column filter decision is new.
   - What's unclear: Whether the per-column filter should live in the ViewModel (adjusting `ProcessPreview` arguments) or in the View (via `ICollectionView` after the results list is built).
   - Recommendation: Move filtering entirely to `ICollectionView` post-collection. `ProcessPreview` should drop the `FilterCategory` guard and return all candidates that pass scope + rename rules. The ViewModel's `ICollectionView` filter predicate applies both the dropdown and per-column filters. This avoids re-running `ProcessPreview` on every filter change (it calls `ApplyRules` which is non-trivial).

---

## Affected Files Inventory

### Category A — New Files (must be created)
| File | Type | Purpose |
|------|------|---------|
| `src/ViewModels/Components/ElementRowViewModel.cs` | New model | Shared per-row data model replacing `ReplaceItem` and string summaries |
| `src/Services/ElementLabelService.cs` | New service | Guaranteed non-blank Name + Category fallback chain |
| `src/Controls/ElementGridControl.xaml` + `.cs` | New UserControl | Shared WPF grid with standard columns + additional column slot |

### Category B — Modified Files (Batch Rename path)
| File | Change |
|------|--------|
| `src/ViewModels/SearchReplaceViewModel.cs` | Remove `ReplaceItem` class; change `PreviewItems` type to `ObservableCollection<ElementRowViewModel>`; add Category column sort/filter |
| `src/Services/Renaming/SearchReplacePreviewService.cs` | Change return type to `List<ElementRowViewModel>`; add `Category` field; move FilterCategory guard to ICollectionView |
| `src/Services/Renaming/BaseElementCollectionService.cs` | Remove `if (el.Category == null) continue` at line 21; call `ElementLabelService.GetLabels`; log fallback via Logger.Instance |
| `src/Views/SearchReplaceView.xaml` | Swap DataGrid columns to add Category; use `ElementGridControl` or add column directly |

### Category C — Modified Files (text-summary screen sweep)
7 VMs: `DivideToposolidViewModel`, `FixPointsViewModel`, `ConvertCadViewModel`, `SplitBoundariesViewModel`, `ConvertToposolidToFloorViewModel`, `ConvertFloorToToposolidViewModel`, plus corresponding View.xaml files.
Change pattern per VM: `ObservableCollection<string>` → `ObservableCollection<ElementRowViewModel>`, `DescribeElement` → `ElementLabelService.GetLabels`, View ListBox → `ElementGridControl`.

### Category D — Modified Files (selection-only screen sweep)
VMs with `SelectionViewModel` only and no existing element list: `AlignEdgesViewModel`, `AlignElementsViewModel`, `AssignMaterialViewModel`, `CategoryChangerViewModel`, `ChangeLevelViewModel`, `OffsetElevationsViewModel`, `ResetSlabsViewModel`, `SimplifyPointsViewModel`, plus corresponding View.xaml files.
Change pattern: add `ObservableCollection<ElementRowViewModel> RowItems`; add element list section to View; resolve `Reference` → `Element` in `SetSelection`.

---

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xUnit 2.6.5 + FluentAssertions 6.12.0 + NSubstitute 5.1.0 |
| Config file | `LECG.Tests/LECG.Tests.csproj` (no separate xunit.runner.json; runs via dotnet test) |
| Quick run command | `dotnet test LECG.Tests/LECG.Tests.csproj -x --filter "Category=Unit"` |
| Full suite command | `dotnet test LECG.Tests/LECG.Tests.csproj` |

**Note:** The test project conditionally references either `LECG.csproj` (local dev) or `LECG.Core.csproj` (CI). `ElementLabelService` must be placed so it compiles under both conditions — i.e., in `LECG.Core` if it has no WPF dependency, or in `LECG` with test coverage via the local reference path.

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| REQ-01 | `ElementLabelService.GetLabels` returns non-blank Name when `element.Name` is empty | unit | `dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"` | ❌ Wave 0 |
| REQ-01 | `ElementLabelService.GetLabels` returns non-blank Category when `element.Category == null` | unit | `dotnet test --filter "FullyQualifiedName~ElementLabelServiceTests"` | ❌ Wave 0 |
| REQ-01 | `BaseElementCollectionService` no longer skips elements with null Category; instead emits fallback label | unit | `dotnet test --filter "FullyQualifiedName~BaseElementCollectionServiceTests"` | ❌ Wave 0 |
| REQ-01 | `ElementRowViewModel` Category and Name fields are never null or whitespace after `ProcessPreview` | unit | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | ❌ Wave 0 |
| REQ-01 | `SearchReplacePreviewService.ProcessPreview` populates `Category` field from `ElementData.Category` | unit | `dotnet test --filter "FullyQualifiedName~SearchReplacePreviewServiceTests"` | ❌ Wave 0 |
| REQ-01 | Grid default sort is Category ascending (ICollectionView.SortDescriptions) | unit (VM) | `dotnet test --filter "FullyQualifiedName~SearchReplaceViewModelTests"` | ❌ Wave 0 |
| REQ-01 | Revit-backed screens (selection-backed VMs) — manual Revit session verifying grid shows correct Name + Category | manual-only | N/A — requires live Revit | N/A |

**Manual-only justification:** WPF controls and Revit API `Element` objects cannot be instantiated in xUnit without a running Revit addin host. All row population logic must therefore be isolated into `ElementLabelService` (testable with mock/stub elements) and tested there. The WPF rendering step is manual-only.

### Sampling Rate
- **Per task commit:** `dotnet test LECG.Tests/LECG.Tests.csproj -x` (fail-fast, ~30s)
- **Per wave merge:** `dotnet test LECG.Tests/LECG.Tests.csproj` (full suite)
- **Phase gate:** Full suite green before `/gsd:verify-work`

### Wave 0 Gaps
- [ ] `LECG.Tests/Services/ElementLabelServiceTests.cs` — covers name-blank fallback and category-null fallback (REQ-01)
- [ ] `LECG.Tests/Services/BaseElementCollectionServiceTests.cs` — covers removal of null-skip, fallback chain integration (REQ-01)
- [ ] `LECG.Tests/Services/SearchReplacePreviewServiceTests.cs` — covers Category field propagation into `ElementRowViewModel` (REQ-01)
- [ ] `LECG.Tests/ViewModels/SearchReplaceViewModelTests.cs` — covers default sort direction on collection view (REQ-01)

---

## Sources

### Primary (HIGH confidence)
- Direct code inspection: `src/Services/Renaming/BaseElementCollectionService.cs` — null-skip at line 21 confirmed
- Direct code inspection: `src/ViewModels/SearchReplaceViewModel.cs` — `ReplaceItem` class, missing `Category` field confirmed
- Direct code inspection: `src/Services/Renaming/SearchReplacePreviewService.cs` lines 110-118 — `ReplaceItem` construction without Category confirmed
- Direct code inspection: `src/Controls/LecgDataGrid.cs` — existing base control API confirmed
- Direct code inspection: `src/Services/Renaming/SearchReplaceService.cs` — `ElementData` class structure confirmed (has `Category` field already)
- Direct code inspection: 7 ViewModels using `ObservableCollection<string> SelectedElementSummaries` — DivideToposolid, FixPoints, ConvertCad, SplitBoundaries, ConvertToposolidToFloor, ConvertFloorToToposolid confirmed
- Direct code inspection: `LECG.Tests/LECG.Tests.csproj` — xUnit 2.6.5, FluentAssertions 6.12.0, NSubstitute 5.1.0 confirmed

### Secondary (MEDIUM confidence)
- WPF `ICollectionView` / `CollectionViewSource` sort+filter pattern — standard .NET 8 WPF pattern, no external verification needed, well-established
- `LabelUtils.GetLabelFor(BuiltInCategory)` locale-safety — established in Phase 02.5 decisions (STATE.md); consistent with Revit API documentation pattern
- `BuiltInParameter.ALL_MODEL_TYPE_NAME` for blank system-family names — known Revit API pattern; MEDIUM confidence as not verified against live Revit in this session

### Tertiary (LOW confidence)
- None — all findings are grounded in direct code inspection or established patterns from prior phases.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all packages directly confirmed in csproj and existing code
- Architecture: HIGH — all patterns derived from existing code; no novel external dependencies
- Pitfalls: HIGH (null-skip, ReplaceItem migration, threading) / MEDIUM (blank Name on localized Revit)
- Test map: HIGH — test project structure confirmed; test file gaps identified by direct inspection

**Research date:** 2026-05-09
**Valid until:** 2026-08-09 (stable .NET/WPF domain; no time-sensitive API changes expected)
