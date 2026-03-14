---
phase: 2
plan: 1
wave: 1
---

# Plan 2.1: Decoupling and Async Filtering

## Objective
Refactor the renaming preview pipeline to support non-blocking, asynchronous filtering and decoupling of Services from the WPF ViewModels. This is the core engine work required for the "ultra-fast" requirement.

## Context
- .gsd/SPEC.md
- .gsd/ARCHITECTURE.md
- .gsd/DECISIONS.md
- src/ViewModels/SearchReplaceViewModel.cs
- src/Services/SearchReplacePreviewService.cs

## Tasks

<task type="auto">
  <name>Create SearchCriteria DTO and Refactor Interface</name>
  <files>src/Models/SearchCriteria.cs, src/Services/Interfaces/ISearchReplacePreviewService.cs</files>
  <action>
    - Create a POCO `SearchCriteria` class in `src/Models/SearchCriteria.cs` that encapsulates all filtering state (FilterName, Category, FilterType, etc.) currently in the ViewModel.
    - Update `ISearchReplacePreviewService.ProcessPreview` to accept `SearchCriteria` and `RenameRuleContext` instead of the full `SearchReplaceViewModel`.
    - This removes the cyclic dependency and WPF ViewModels from the Service layer.
  </action>
  <verify>dotnet build LECG.csproj</verify>
  <done>Service interface is clean and decoupled from the View layer.</done>
</task>

<task type="auto">
  <name>Implement Async Filtering with Debounce</name>
  <files>src/ViewModels/SearchReplaceViewModel.cs</files>
  <action>
    - Inject a `CancellationTokenSource` into `SearchReplaceViewModel` to manage search lifecycle.
    - Implement a `UpdatePreviewAsync()` method that handles `Task.Run` execution.
    - Add a simple debounce logic in the `partial void OnFilterNameChanged` handler (using `Task.Delay` and cancellation).
    - Ensure results are dispatched back to the `PreviewItems` collection securely on the UI thread.
  </action>
  <verify>Select-String -Path src/ViewModels/SearchReplaceViewModel.cs -Pattern "CancellationTokenSource"</verify>
  <done>ViewModel triggers background search on text change with debounce, ensuring UI remains responsive.</done>
</task>

<task type="auto">
  <name>Update Service Implementation for Async Support</name>
  <files>src/Services/SearchReplacePreviewService.cs</files>
  <action>
    - Update `SearchReplacePreviewService.cs` to match the new `ISearchReplacePreviewService` signature.
    - Add `CancellationToken` support to the `ProcessPreview` loop to bail out early if a newer search is triggered.
    - Optimize the inner loop to use `StringComparison.Ordinal` where possible for raw speed.
  </action>
  <verify>Get-Content "src/Services/SearchReplacePreviewService.cs" | Select-String "CancellationToken"</verify>
  <done>Search engine is optimized and cancellation-aware.</done>
</task>

## Success Criteria
- [ ] No WPF ViewModels are referenced in `src/Services/`.
- [ ] UI thread is never blocked during search even with 2000+ cached items.
- [ ] Rapid typing in the search box cancels previous tasks immediately.
