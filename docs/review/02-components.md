# Component Catalog

## Document Purpose

This document provides a complete inventory of all components in the LECG Revit plugin, including commands, services, ViewModels, views, and their relationships.

**Target Audience**: Developers, architects

**Last Updated**: February 2026

---

## Quick Stats

| Component Type | Count | Total LOC | Avg LOC/File |
|----------------|-------|-----------|--------------|
| **Commands** | 19 | ~1,400 | ~74 |
| **Services** | 16 | ~3,200 | ~200 |
| **ViewModels** | 18 | ~1,900 | ~106 |
| **Views** | 18 | ~900 (code-behind) | ~50 |
| **Core Infrastructure** | 6 | ~650 | ~108 |
| **Total** | **77** | **~8,695** | **~113** |

---

## Commands Inventory

Commands are the entry points for user actions from the Revit ribbon. They extend `RevitCommand` base class (except `HomeCommand`).

### Home Panel

#### 1. HomeCommand
- **File**: `src/Commands/HomeCommand.cs`
- **Purpose**: Main launcher/router that shows a custom home screen with all available tools
- **Pattern**: Router pattern - dispatches to other commands based on user selection
- **Ribbon**: "Home" button
- **Special**: Does NOT extend `RevitCommand` (directly implements `IExternalCommand`)
- **Dependencies**: None (manually instantiates other commands)
- **Dialog**: `HomeView` → `AlignDashboardView` (for align sub-menu)

### Health Panel

#### 2. PurgeCommand
- **File**: `src/Commands/PurgeCommand.cs`
- **Purpose**: Purge unused elements (line styles, fill patterns, materials) from the project
- **Service**: `IPurgeService`
- **ViewModel**: `PurgeViewModel`
- **View**: `PurgeView`
- **Transaction**: Manual (handled in service)
- **Settings File**: `PurgeSettings.json`

#### 3. CleanSchemasCommand
- **File**: `src/Commands/CleanSchemasCommand.cs`
- **Purpose**: Clean extensible storage schemas from the project
- **Service**: `ISchemaCleanerService`
- **Transaction**: Auto (`"Clean Schemas"`)
- **Notes**: Removes orphaned extensible storage data

### Standards Panel

#### 4. SearchReplaceCommand
- **File**: `src/Commands/SearchReplaceCommand.cs` (128 LOC)
- **Purpose**: Search and replace text in element names across the project
- **Service**: `ISearchReplaceService`
- **ViewModel**: `SearchReplaceViewModel`
- **View**: `SearchReplaceView`
- **Transaction**: Manual (handled per batch)
- **Features**: Live preview, category filtering, batch operations

#### 5. FilterCopyCommand
- **File**: `src/Commands/FilterCopyCommand.cs`
- **Purpose**: Copy elements based on advanced filtering criteria
- **ViewModel**: `FilterCopyViewModel` (347 LOC - largest ViewModel)
- **View**: `FilterCopyView`
- **Transaction**: Manual
- **Features**: Complex filtering UI, parameter-based filtering

### Toposolids Panel

#### 6. AssignMaterialCommand
- **File**: `src/Commands/AssignMaterialCommand.cs`
- **Purpose**: Assign materials to Toposolids/Floors based on Type Name
- **Service**: `IMaterialService`
- **ViewModel**: `AssignMaterialViewModel`
- **View**: `AssignMaterialView`
- **Transaction**: Manual (handled in service)
- **Features**: Material library browsing, type-based assignment

#### 7. OffsetElevationsCommand
- **File**: `src/Commands/OffsetElevationsCommand.cs`
- **Purpose**: Offset elevations of Toposolid points
- **Service**: `IOffsetService`
- **ViewModel**: `OffsetElevationsVM`
- **View**: `OffsetElevationsView`
- **Transaction**: Manual

#### 8. ResetSlabsCommand
- **File**: `src/Commands/ResetSlabsCommand.cs` (123 LOC)
- **Purpose**: Reset slab elevations to level
- **Service**: `ISlabService`
- **ViewModel**: `ResetSlabsVM`
- **View**: `ResetSlabsView`
- **Transaction**: Auto (`"Reset Slabs"`)

#### 9. SimplifyPointsCommand
- **File**: `src/Commands/SimplifyPointsCommand.cs`
- **Purpose**: Simplify Toposolid point clouds (reduce point count)
- **Service**: `ISimplifyPointsService`
- **ViewModel**: `SimplifyPointsViewModel`
- **View**: `SimplifyPointsView`
- **Transaction**: Manual
- **Features**: Douglas-Peucker algorithm, tolerance-based simplification

#### 10. AlignEdgesCommand
- **File**: `src/Commands/AlignEdgesCommand.cs`
- **Purpose**: Align edges of Toposolids
- **Service**: `IAlignEdgesService`
- **ViewModel**: `AlignEdgesViewModel`
- **View**: `AlignEdgesView`
- **Transaction**: Manual

#### 11. UpdateContoursCommand
- **File**: `src/Commands/UpdateContoursCommand.cs`
- **Purpose**: Update contour lines for Toposolids
- **Service**: `IToposolidService`
- **ViewModel**: `UpdateContoursViewModel`
- **View**: `UpdateContoursView`
- **Transaction**: Manual

### Align Panel

#### 12-19. AlignCommands (8 commands in one file)
- **File**: `src/Commands/AlignCommands.cs`
- **Purpose**: Align selected annotation elements (Left, Center, Right, Top, Middle, Bottom, Distribute H, Distribute V)
- **Service**: `IAlignElementsService`
- **Classes**:
  - `AlignLeftCommand` - Align to left edge
  - `AlignCenterCommand` - Align to horizontal center
  - `AlignRightCommand` - Align to right edge
  - `AlignTopCommand` - Align to top edge
  - `AlignMiddleCommand` - Align to vertical middle
  - `AlignBottomCommand` - Align to bottom edge
  - `DistributeHorizontallyCommand` - Distribute evenly horizontally
  - `DistributeVerticallyCommand` - Distribute evenly vertically
- **ViewModel**: `AlignElementsViewModel`
- **View**: `AlignElementsView`, `AlignDashboardView` (launcher)
- **Transaction**: Auto (`"Align Elements"`)
- **Target Elements**: TextNotes, Dimensions, Tags, etc.

### Visualization Panel

#### 20. SexyRevitCommand
- **File**: `src/Commands/SexyRevitCommand.cs` (52 LOC)
- **Purpose**: Beautify the current view with optimal visual settings (realistic mode, shadows, sun, hide grids/levels)
- **Service**: `ISexyRevitService`
- **ViewModel**: `SexyRevitViewModel`
- **View**: `SexyRevitView`
- **Transaction**: Manual (handled in service)
- **Settings File**: `SexyRevitSettings.json`
- **Features**: Display style, detail level, sun settings, category hiding

#### 21. RenderAppearanceMatchCommand
- **File**: `src/Commands/RenderAppearanceMatchCommand.cs`
- **Purpose**: Match render appearance between materials
- **Service**: `IMaterialService`
- **View**: `RenderAppearanceView`
- **Transaction**: Auto (`"Match Render Appearance"`)

### Conversion/Import Panel (Additional)

#### 22. ChangeLevelCommand
- **File**: `src/Commands/ChangeLevelCommand.cs`
- **Purpose**: Change level association for elements
- **Service**: `IChangeLevelService`
- **ViewModel**: `ChangeLevelViewModel`
- **View**: `ChangeLevelView`
- **Transaction**: Manual

#### 23. ConvertCadCommand
- **File**: `src/Commands/ConvertCadCommand.cs` (191 LOC - largest command)
- **Purpose**: Convert CAD imports to Revit elements (lines, detail lines)
- **Service**: `ICadConversionService`
- **ViewModel**: `ConvertCadViewModel` (219 LOC)
- **View**: `ConvertCadView`
- **Transaction**: Manual
- **Features**: Layer filtering, style mapping, geometry conversion

#### 24. ConvertFamilyCommand
- **File**: `src/Commands/ConvertFamilyCommand.cs`
- **Purpose**: Convert family instances between types
- **Service**: `IFamilyConversionService`
- **ViewModel**: `ConvertFamilyViewModel`
- **View**: `ConvertFamilyView`
- **Transaction**: Manual

#### 25. ConvertSharedCommand
- **File**: `src/Commands/ConvertSharedCommand.cs`
- **Purpose**: Convert project parameters to shared parameters
- **Service**: Not specified (likely direct API calls)
- **Transaction**: Manual

### Development/Debug

#### 26. DebugCommand
- **File**: `src/Commands/DebugCommand.cs`
- **Purpose**: Development debugging and testing
- **Transaction**: Manual
- **Notes**: Internal use, not exposed in production ribbon

---

## Services Inventory

Services encapsulate all Revit API interactions and business logic. All services are **Singletons** registered in the DI container.

### Core Services

#### 1. SexyRevitService
- **File**: `src/Services/SexyRevitService.cs` (136 LOC)
- **Interface**: `ISexyRevitService`
- **Purpose**: Apply view beautification settings
- **Methods**:
  - `ApplyBeauty(Document, View, SexyRevitViewModel, Action<string>?, Action<double, string>?)` - Apply settings
- **Operations**:
  - Set display style (Realistic)
  - Configure sun and shadows
  - Set detail level
  - Hide categories (grids, levels, ref points, scope boxes)
  - Configure ambient occlusion (if supported)
- **Dependencies**: None (pure Revit API)

#### 2. PurgeService
- **File**: `src/Services/PurgeService.cs` (384 LOC - Large)
- **Interface**: `IPurgeService`
- **Purpose**: Purge unused elements from document
- **Methods**:
  - `PurgeUnused(Document, PurgeViewModel, Action<string>?)` - Purge based on settings
- **Purge Categories**:
  - Line styles (graphics patterns)
  - Fill patterns
  - Materials
  - (Future: Views, families, etc.)
- **Notes**: Large service, could benefit from decomposition

#### 3. SearchReplaceService
- **File**: `src/Services/SearchReplaceService.cs` (224 LOC)
- **Interface**: `ISearchReplaceService`
- **Purpose**: Search and replace text in element names
- **Methods**:
  - `SearchElements(Document, string pattern, categories)` - Find matching elements
  - `ReplaceInElements(Document, elements, old, new)` - Replace text
- **Features**: Regex support, category filtering, batch operations

#### 4. MaterialService
- **File**: `src/Services/MaterialService.cs` (403 LOC - Large)
- **Interface**: `IMaterialService`
- **Purpose**: Material assignment and render appearance management
- **Methods**:
  - `AssignMaterialsToToposolids(Document, ...)` - Assign materials by type
  - `MatchRenderAppearance(Document, source, targets)` - Copy render appearance
  - `GetAvailableMaterials(Document)` - List materials
- **Operations**:
  - Material assignment to Toposolids/Floors
  - Render appearance copying
  - Material library management
- **Notes**: Multiple responsibilities, could be split into `MaterialAssignmentService` and `RenderAppearanceService`

### Toposolid/Geometry Services

#### 5. SlabService
- **File**: `src/Services/SlabService.cs`
- **Interface**: `ISlabService`
- **Purpose**: Reset slab/floor elevations
- **Methods**:
  - `ResetSlabElevations(Document, slabs)` - Reset to level elevation

#### 6. OffsetService
- **File**: `src/Services/OffsetService.cs`
- **Interface**: `IOffsetService`
- **Purpose**: Offset Toposolid point elevations
- **Methods**:
  - `OffsetToposolidPoints(Document, toposolid, offsetValue)` - Offset all points

#### 7. ToposolidService
- **File**: `src/Services/ToposolidService.cs`
- **Interface**: `IToposolidService`
- **Purpose**: Toposolid-specific operations (contours, subdivision, etc.)
- **Methods**:
  - `UpdateContours(Document, toposolid)` - Regenerate contour lines
  - Additional toposolid geometry operations

#### 8. SimplifyPointsService
- **File**: `src/Services/SimplifyPointsService.cs`
- **Interface**: `ISimplifyPointsService`
- **Purpose**: Simplify Toposolid point clouds
- **Methods**:
  - `SimplifyPoints(Document, toposolid, tolerance)` - Reduce points using Douglas-Peucker
- **Algorithm**: Douglas-Peucker line simplification

#### 9. AlignEdgesService
- **File**: `src/Services/AlignEdgesService.cs` (281 LOC)
- **Interface**: `IAlignEdgesService`
- **Purpose**: Align Toposolid edges
- **Methods**:
  - `AlignEdges(Document, toposolids, direction)` - Align edges
- **Operations**: Edge detection, alignment, geometric calculations

#### 10. ChangeLevelService
- **File**: `src/Services/ChangeLevelService.cs`
- **Interface**: `IChangeLevelService`
- **Purpose**: Change level association for elements
- **Methods**:
  - `ChangeLevel(Document, elements, newLevel)` - Reassign level

### Alignment Services

#### 11. AlignElementsService
- **File**: `src/Services/AlignElementsService.cs` (123 LOC)
- **Interface**: `IAlignElementsService`
- **Purpose**: Align annotation elements (TextNotes, Tags, Dimensions)
- **Methods**:
  - `AlignLeft(Document, elements)` - Align to left edge
  - `AlignCenter(Document, elements)` - Align to horizontal center
  - `AlignRight(Document, elements)` - Align to right edge
  - `AlignTop(Document, elements)` - Align to top edge
  - `AlignMiddle(Document, elements)` - Align to vertical middle
  - `AlignBottom(Document, elements)` - Align to bottom edge
  - `DistributeHorizontally(Document, elements)` - Even horizontal spacing
  - `DistributeVertically(Document, elements)` - Even vertical spacing
- **Target Elements**: 2D annotation (TextNote, Dimension, Tag, etc.)

### Conversion Services

#### 12. CadConversionService
- **File**: `src/Services/CadConversionService.cs` (566 LOC - Largest service)
- **Interface**: `ICadConversionService`
- **Purpose**: Convert CAD imports to Revit native elements
- **Methods**:
  - `ConvertCadToDetailLines(Document, cadInstance, settings)` - Convert to detail lines
  - `ExtractCadGeometry(Document, cadInstance)` - Extract curves
  - `MapLayers(Document, cadInstance, layerMapping)` - Map CAD layers to Revit line styles
- **Features**: Layer filtering, style mapping, geometry conversion, batch processing
- **Notes**: Very large service (566 LOC), strong candidate for decomposition into:
  - `CadImportService` - Handle CAD import and geometry extraction
  - `CadGeometryConverter` - Convert geometry to Revit elements
  - `CadLayerMappingService` - Layer/style mapping

#### 13. FamilyConversionService
- **File**: `src/Services/FamilyConversionService.cs` (216 LOC)
- **Interface**: `IFamilyConversionService`
- **Purpose**: Convert family instances between types
- **Methods**:
  - `ConvertFamilyType(Document, instances, targetType)` - Change family type
  - `SwapFamilies(Document, instances, targetFamily)` - Replace family

### Data Management Services

#### 14. SchemaCleanerService
- **File**: `src/Services/SchemaCleanerService.cs` (150 LOC)
- **Interface**: `ISchemaCleanerService`
- **Purpose**: Clean extensible storage schemas
- **Methods**:
  - `CleanSchemas(Document, schemaNames)` - Remove schemas
  - `ListSchemas(Document)` - Get all schemas
- **Operations**: Extensible storage cleanup

#### 15. SettingsManager (Utility Service)
- **File**: `src/Services/SettingsManager.cs`
- **Purpose**: Persist ViewModel settings as JSON
- **Methods**:
  - `static T Load<T>(string filename)` - Load settings from AppData
  - `static void Save<T>(T settings, string filename)` - Save settings to AppData
- **Storage**: `%AppData%\LECG\` (or similar)
- **Notes**: Generic utility, not registered in DI (static methods)

### Configuration Services

#### 16. RenameRules (Data Class)
- **File**: `src/Services/RenameRules.cs` (216 LOC)
- **Purpose**: Configuration data for rename operations
- **Notes**: Not a true "service", more of a configuration/data class

---

## ViewModels Inventory

ViewModels handle presentation logic and data binding. All extend `BaseViewModel` which uses CommunityToolkit.Mvvm.

### Base ViewModel

#### BaseViewModel
- **File**: `src/ViewModels/BaseViewModel.cs` (32 LOC)
- **Base Class**: `ObservableObject` (CommunityToolkit.Mvvm)
- **Properties**:
  - `Title` (string) - Window title
  - `IsBusy` (bool) - Loading indicator
- **Commands**:
  - `ApplyCommand` - Confirm action (calls `CloseAction`)
  - `CancelCommand` - Cancel action (calls `CloseAction`)
- **Events**:
  - `CloseAction` (Action?) - Delegate to close window

### Feature ViewModels (Alphabetical)

1. **AlignEdgesViewModel** - Align toposolid edges settings
2. **AlignElementsViewModel** (116 LOC) - Alignment direction, target elements
3. **AssignMaterialViewModel** - Material selection, target elements
4. **ChangeLevelViewModel** - Level selection, element filtering
5. **ConvertCadViewModel** (219 LOC) - CAD layer mapping, style selection, conversion settings
6. **ConvertFamilyViewModel** - Family type selection, instance filtering
7. **FilterCopyViewModel** (347 LOC - Largest) - Complex filtering UI, parameter-based filters
8. **LogViewModel** - Log window data binding (displays `Logger.Instance` entries)
9. **OffsetElevationsVM** - Offset value, target toposolids
10. **PurgeViewModel** - Purge category selection (line styles, fill patterns, materials)
11. **ResetSlabsVM** - Target slabs, level selection
12. **SearchReplaceViewModel** (128 LOC) - Search pattern, replace text, category filtering, live preview
13. **SexyRevitViewModel** - View beautification settings (display style, sun, hide categories)
14. **SimplifyPointsViewModel** - Tolerance value, target toposolids
15. **UpdateContoursViewModel** - Contour settings
16. **SelectionViewModel** (Component) - Reusable element selection component

### Registration Status

**Registered in DI** (3/18):
- `ResetSlabsVM`
- `ConvertFamilyViewModel`
- `ConvertCadViewModel`

**Not Registered** (15/18): All others created with `new` keyword

---

## Views Inventory

Views are WPF XAML UI definitions. Code-behind is minimal (good MVVM separation).

### Base View

#### LecgWindow
- **File**: `src/Views/Base/LecgWindow.cs`
- **Purpose**: Base window class for consistent behavior
- **Features**: Window styling, icon, owner window setup

### Feature Views (Alphabetical)

1. **AlignDashboardView** - Sub-menu for alignment commands (launched from HomeView)
2. **AlignEdgesView** - Align toposolid edges UI
3. **AlignElementsView** - Alignment direction selection
4. **AssignMaterialView** - Material browser and assignment UI
5. **ChangeLevelView** - Level selection and element filtering
6. **ConvertCadView** - CAD layer mapping UI
7. **ConvertFamilyView** - Family type selection UI
8. **FilterCopyView** - Complex filtering and copying UI
9. **HomeView** - Main launcher/home screen with tool grid
10. **LogView** - Log window with scrollable log entries
11. **OffsetElevationsView** - Offset value input
12. **PurgeView** - Purge options (checkboxes for categories)
13. **RenderAppearanceView** - Render appearance matching UI
14. **ResetSlabsView** - Slab selection and reset options
15. **SearchReplaceView** - Search/replace UI with live preview
16. **SexyRevitView** - View beautification settings
17. **SimplifyPointsView** - Tolerance input and preview
18. **UpdateContoursView** - Contour update settings
19. **SelectionControl** (Component) - Reusable element selection control

### Registration Status

**Registered in DI** (1/18):
- `ResetSlabsView`

**Not Registered** (17/18): All others created with `new` keyword

---

## Core Infrastructure Components

### Bootstrapper
- **File**: `src/Core/Bootstrapper.cs` (61 LOC)
- **Purpose**: Initialize DI container
- **Methods**:
  - `Initialize()` - Entry point, builds ServiceProvider
  - `ConfigureServices(IServiceCollection)` - Register services (13 registered)
  - `ConfigureViewModels(IServiceCollection)` - Register VMs (3/18 registered ⚠️)
  - `ConfigureViews(IServiceCollection)` - Register views (1/18 registered ⚠️)

### ServiceLocator
- **File**: `src/Core/ServiceLocator.cs` (37 LOC)
- **Purpose**: Static access to DI container
- **Properties**:
  - `ServiceProvider` (IServiceProvider?) - The DI container
- **Methods**:
  - `Initialize(IServiceProvider)` - Set provider
  - `GetService<T>()` - Resolve service (nullable)
  - `GetRequiredService<T>()` - Resolve service (throws if not found)

### RevitCommand
- **File**: `src/Core/RevitCommand.cs` (141 LOC)
- **Purpose**: Base class for all commands
- **Properties**:
  - `Doc` (Document) - Active document
  - `UIDoc` (UIDocument) - UI document
  - `TransactionName` (string?) - Optional auto-transaction name
- **Methods**:
  - `abstract Execute(UIDocument, Document)` - Override in derived classes
  - `Result Execute(ExternalCommandData, ref string, ElementSet)` - IExternalCommand implementation
  - `Log(string)` - Log to Logger.Instance
  - `UpdateProgress(double, string)` - Update log window progress
  - `ShowLogWindow(string)` - Show/initialize log window

### RibbonService
- **File**: `src/Core/Ribbon/RibbonService.cs` (278 LOC)
- **Interface**: `IRibbonService`
- **Purpose**: Initialize Revit ribbon UI
- **Methods**:
  - `InitializeRibbon(UIControlledApplication)` - Create tab and panels
  - `CreateHomePanel()`, `CreateHealthPanel()`, `CreateToposolidsPanel()`, etc. - Panel builders

### App (Entry Point)
- **File**: `src/App.cs` (63 LOC)
- **Purpose**: Plugin entry point (IExternalApplication)
- **Methods**:
  - `OnStartup(UIControlledApplication)` - Initialize plugin
  - `OnShutdown(UIControlledApplication)` - Cleanup (empty)
- **Startup Flow**:
  1. Assembly resolution handler (for DI version conflicts)
  2. `Bootstrapper.Initialize()`
  3. `RibbonService.InitializeRibbon()`

### Logger
- **File**: `src/Services/Logging/Logger.cs`
- **Pattern**: Singleton
- **Purpose**: Centralized logging with UI thread marshaling
- **Methods**:
  - `static Instance` - Get singleton instance
  - `Log(string)` - Add log entry
  - `Clear()` - Clear all entries
  - `SetDispatcher(Dispatcher)` - Set UI thread dispatcher
- **Properties**:
  - `Entries` (ObservableCollection<LogEntry>) - Bindable log entries

---

## Component Dependency Graph

### High-Level Dependencies

```
App (Entry Point)
 └─> Bootstrapper
     └─> ServiceCollection
         ├─> Services (13 Singletons)
         ├─> ViewModels (3 Transients, incomplete)
         └─> Views (1 Transient, incomplete)
     └─> ServiceLocator (static provider)
     └─> RibbonService
         └─> UIConstants, AppConstants, AppImages

Commands (19)
 ├─> RevitCommand (base class)
 ├─> ServiceLocator → Services
 ├─> SettingsManager → ViewModels (settings)
 └─> Views (new keyword or DI)

Services (16)
 ├─> Revit API (Document, Element, Transaction, etc.)
 ├─> Logger.Instance (logging)
 └─> Callbacks (Action<string>, Action<double, string>)

ViewModels (18)
 ├─> BaseViewModel (CommunityToolkit.Mvvm)
 └─> No direct Revit API dependencies ✅

Views (18)
 ├─> ViewModels (DataContext)
 └─> LecgWindow (base class)
```

### Command → Service Mappings

| Command | Service(s) | ViewModel | View |
|---------|-----------|-----------|------|
| SexyRevitCommand | ISexyRevitService | SexyRevitViewModel | SexyRevitView |
| PurgeCommand | IPurgeService | PurgeViewModel | PurgeView |
| SearchReplaceCommand | ISearchReplaceService | SearchReplaceViewModel | SearchReplaceView |
| AssignMaterialCommand | IMaterialService | AssignMaterialViewModel | AssignMaterialView |
| OffsetElevationsCommand | IOffsetService | OffsetElevationsVM | OffsetElevationsView |
| ResetSlabsCommand | ISlabService | ResetSlabsVM | ResetSlabsView |
| SimplifyPointsCommand | ISimplifyPointsService | SimplifyPointsViewModel | SimplifyPointsView |
| AlignEdgesCommand | IAlignEdgesService | AlignEdgesViewModel | AlignEdgesView |
| UpdateContoursCommand | IToposolidService | UpdateContoursViewModel | UpdateContoursView |
| ChangeLevelCommand | IChangeLevelService | ChangeLevelViewModel | ChangeLevelView |
| ConvertCadCommand | ICadConversionService | ConvertCadViewModel | ConvertCadView |
| ConvertFamilyCommand | IFamilyConversionService | ConvertFamilyViewModel | ConvertFamilyView |
| AlignCommands (8) | IAlignElementsService | AlignElementsViewModel | AlignElementsView |
| RenderAppearanceMatchCommand | IMaterialService | - | RenderAppearanceView |
| CleanSchemasCommand | ISchemaCleanerService | - | - |
| FilterCopyCommand | - | FilterCopyViewModel | FilterCopyView |
| ConvertSharedCommand | - | - | - |
| HomeCommand | None (router) | - | HomeView, AlignDashboardView |

---

## Service Responsibilities Matrix

| Service | Responsibility | Revit API Usage | Complexity |
|---------|---------------|-----------------|------------|
| **SexyRevitService** | View beautification | View.DisplayStyle, SunAndShadowSettings, Category hiding | Medium |
| **PurgeService** | Purge unused elements | PerformanceAdviser, Material, LineStyle, FillPattern collectors | High |
| **SearchReplaceService** | Search/replace in names | FilteredElementCollector, Element.Name | Low |
| **MaterialService** | Material assignment, render appearance | Material, MaterialFunctionAssignment, AppearanceAssetElement | High |
| **SlabService** | Reset slab elevations | Floor, Toposolid, Level.Elevation | Low |
| **OffsetService** | Offset toposolid points | Toposolid.GetPoints(), SetPoints() | Medium |
| **ToposolidService** | Toposolid operations | Toposolid, Subdivision, Contours | Medium |
| **SimplifyPointsService** | Point cloud simplification | Toposolid.GetPoints(), Douglas-Peucker algorithm | High |
| **AlignEdgesService** | Align toposolid edges | Toposolid edge curves, geometric calculations | High |
| **ChangeLevelService** | Change level association | Element.LevelId, Parameter.Set() | Low |
| **CadConversionService** | CAD → Revit conversion | ImportInstance, GeometryObject, DetailLine creation | Very High |
| **FamilyConversionService** | Family type conversion | FamilyInstance, FamilySymbol, ChangeTypeId() | Medium |
| **SchemaCleanerService** | Extensible storage cleanup | Schema, Entity, ExtensibleStorageUtils | Medium |
| **AlignElementsService** | Align 2D annotations | TextNote, Dimension, Tag, BoundingBoxXYZ | Medium |
| **RibbonService** | Ribbon UI creation | UIControlledApplication, RibbonPanel, PushButton | Low |

---

## ViewModel → View Pairings

| ViewModel | View | Purpose | Complexity |
|-----------|------|---------|------------|
| SexyRevitViewModel | SexyRevitView | View beautification settings | Low |
| PurgeViewModel | PurgeView | Purge category selection | Low |
| SearchReplaceViewModel | SearchReplaceView | Search/replace with preview | Medium |
| AssignMaterialViewModel | AssignMaterialView | Material browser and assignment | Medium |
| OffsetElevationsVM | OffsetElevationsView | Offset value input | Low |
| ResetSlabsVM | ResetSlabsView | Slab selection and reset | Low |
| SimplifyPointsViewModel | SimplifyPointsView | Tolerance and preview | Medium |
| AlignEdgesViewModel | AlignEdgesView | Edge alignment options | Low |
| UpdateContoursViewModel | UpdateContoursView | Contour update settings | Low |
| ChangeLevelViewModel | ChangeLevelView | Level selection | Low |
| ConvertCadViewModel | ConvertCadView | CAD layer mapping | High |
| ConvertFamilyViewModel | ConvertFamilyView | Family type selection | Medium |
| AlignElementsViewModel | AlignElementsView | Alignment direction | Low |
| FilterCopyViewModel | FilterCopyView | Complex filtering UI | Very High |
| LogViewModel | LogView | Log display | Low |
| - (no VM) | HomeView | Main launcher | Low |
| - (no VM) | AlignDashboardView | Align sub-menu | Low |
| - (no VM) | RenderAppearanceView | Render appearance matching | Low |

---

## Interface Locations (Standardized)

All service interfaces live in `src/Services/Interfaces/`.

**Namespace Rule**:
- Every service interface in that folder uses `LECG.Services.Interfaces`.
- Interface consumers import `using LECG.Services.Interfaces;`.

---

## Largest Components (by LOC)

### Commands
1. **ConvertCadCommand** - 191 LOC
2. **ResetSlabsCommand** - 123 LOC
3. **HomeCommand** - ~100 LOC (estimate, router pattern)

### Services
1. **CadConversionService** - 566 LOC (needs decomposition)
2. **MaterialService** - 403 LOC (needs decomposition)
3. **PurgeService** - 384 LOC
4. **AlignEdgesService** - 281 LOC
5. **RibbonService** - 278 LOC

### ViewModels
1. **FilterCopyViewModel** - 347 LOC (very complex filtering)
2. **ConvertCadViewModel** - 219 LOC
3. **SearchReplaceViewModel** - 128 LOC
4. **AlignElementsViewModel** - 116 LOC

---

## Missing Components (Gaps)

### 1. Interfaces Not in DI
- Interface locations are consolidated under `src/Services/Interfaces/`
- Keep new interfaces in `LECG.Services.Interfaces`

### 2. ViewModels Not Registered (15/18)
- Most ViewModels are not in DI container
- Commands use `new` keyword directly
- Inconsistent DI usage

### 3. Views Not Registered (17/18)
- Only `ResetSlabsView` is registered
- Commands use `new` keyword for views

### 4. Missing `.addin` Manifest
- No `.addin` file found in repository
- Required for Revit to load the plugin
- Should be added for deployment

### 5. Missing Tests
- No unit tests found
- Service layer is highly testable (interface-based)
- Recommended: Add xUnit or NUnit test project

---

## Component Naming Conventions

### Commands
- **Pattern**: `{Feature}Command.cs`
- **Examples**: `SexyRevitCommand`, `PurgeCommand`, `AlignEdgesCommand`
- **Special**: `AlignCommands.cs` (multiple commands in one file)

### Services
- **Pattern**: `{Feature}Service.cs`
- **Examples**: `SexyRevitService`, `MaterialService`, `PurgeService`
- **Interface Pattern**: `I{Feature}Service.cs`

### ViewModels
- **Pattern**: `{Feature}ViewModel.cs` or `{Feature}VM.cs`
- **Examples**: `SexyRevitViewModel`, `OffsetElevationsVM`, `ResetSlabsVM`
- **Inconsistency**: Some use `ViewModel`, some use `VM` suffix

### Views
- **Pattern**: `{Feature}View.xaml` + `{Feature}View.xaml.cs`
- **Examples**: `SexyRevitView`, `PurgeView`, `HomeView`
- **Base**: `LecgWindow.cs` (no XAML, pure C# base class)

---

## Conclusion

The LECG plugin consists of 77 well-organized components with clear separation of concerns. The component inventory shows:

**Strengths**:
- Clear command → service → Revit API layering
- Consistent naming conventions
- Interface-based services (testable)
- Reusable base classes (RevitCommand, BaseViewModel, LecgWindow)

**Areas for Improvement**:
- Complete DI registration (ViewModels and Views)
- Consolidate interface locations
- Decompose large services (CadConversionService, MaterialService)
- Add missing `.addin` manifest
- Add unit tests

**Component Complexity**:
- **Low**: Home, Purge, Search/Replace, Simple Align, Offset, Reset
- **Medium**: SexyRevit, Material, Toposolid, Simplify, ChangeLevl, Family Conversion
- **High**: CAD Conversion, FilterCopy, Advanced Alignment

For detailed implementation patterns and code examples, see `04-patterns.md`.
