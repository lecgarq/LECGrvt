---
phase: 10
plan: 2
wave: 2
depends_on: [10.1]
---

# Plan 10.2: Wire Purge Parameters into Pipeline & UI

## Objective

Integrate `PurgeParameterService` into the existing purge infrastructure: ViewModel toggle, XAML checkbox, DI registration, execution coordinator, pass execution, and summary reporting.

## Context

- `src/ViewModels/PurgeViewModel.cs` — Add `_purgeParameters` toggle
- `src/Views/PurgeView.xaml` — Add checkbox for "Unused Family Parameters"
- `src/Services/PurgeService.cs` — Add parameter purge method + wire into `PurgeAll`
- `src/Services/PurgePassExecutionService.cs` — Add parameter pass
- `src/Services/PurgeExecutionCoordinatorService.cs` — Expand tuple to include parameters
- `src/Services/Interfaces/IPurgeService.cs` — Update `PurgeAll` signature
- `src/Services/Interfaces/IPurgePassExecutionService.cs` — Update return type
- `src/Services/Interfaces/IPurgeExecutionCoordinatorService.cs` — Update return type
- `src/Services/PurgeSummaryService.cs` — Add parameters to summary
- `src/Commands/PurgeCommand.cs` — Pass new flag to PurgeAll
- `src/Core/Bootstrapper.cs` — Register DI

## Tasks

### Task 1: ViewModel + UI

**Files**: `src/ViewModels/PurgeViewModel.cs`, `src/Views/PurgeView.xaml`

1. Add to PurgeViewModel:
```csharp
[ObservableProperty] private bool _purgeParameters = false;
```

2. Add to PurgeView.xaml before the Separator:
```xml
<CheckBox Content="Unused Family Parameters ⚠" 
          IsChecked="{Binding PurgeParameters, Mode=TwoWay}" 
          Style="{DynamicResource ModernCheckboxStyle}" 
          Margin="{StaticResource CheckboxSpacing}" 
          ToolTip="Opens each family and removes parameters with no formula, no constraint, and no association. This is a destructive and slow operation."/>
```

### Task 2: Pipeline Integration

**Files**: All purge service + interface files

Follow the existing pattern for each category. The changes cascade:

1. **Interfaces**: Add `parameters` bool to `PurgeAll`, `Execute`, `ExecutePass` signatures. Expand return tuples to include `parametersDeleted`.

2. **`IPurgeService`**: Add `int PurgeUnusedParameters(Document doc, Action<string>? logCallback = null);` + update `PurgeAll` sig.

3. **`PurgeService`**: Inject `IPurgeParameterService`. Add `PurgeUnusedParameters` delegation. Update `PurgeAll` to accept + forward `parameters` flag.

4. **`PurgePassExecutionService`**: Inject `IPurgeParameterService`. Add parameters pass block (like materials block). Return expanded tuple.

5. **`PurgeExecutionCoordinatorService`**: Update tuple handling. Accumulate `parametersDeleted`.

6. **`PurgeSummaryService`**: Add parameters line to report.

7. **`PurgeCommand.cs`**: Load/save `settings.PurgeParameters`. Pass to `PurgeAll`. Add to "nothing selected" check.

8. **`Bootstrapper.cs`**: Register `IPurgeParameterService` → `PurgeParameterService`.

### Verification

- Build: `dotnet build LECG.csproj -c Release` → Build succeeded
- "Unused Family Parameters" checkbox appears in Purge dialog
- DI resolves correctly (no startup crash)

### Done Criteria

- `PurgeParameters` toggle in ViewModel, saved/loaded
- Checkbox visible in PurgeView.xaml with warning icon + tooltip
- All purge interfaces updated with `parameters` flag
- All purge services updated with cascading parameter support
- Bootstrapper registers `IPurgeParameterService`
- Build succeeds with zero errors
