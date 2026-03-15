# Phase 4 Plan 1: Polish & Responsive Layout

**Objective**: Finalize the ultra-fast performance scaling and responsive layout boundaries for the entire LECG UI. Ensure absolute visual consistency across all 18 views by completing the Style Sweep.

## 1. Wave 1: Adaptive Layout Boundaries (Core Views)
**Goal**: De-rigidize the layout in primary windows to handle user resizing and DPI scaling without element clipping.

### Tasks:
- [ ] **SearchReplaceView Resize Boundaries**:
    - [ ] Update `SearchReplaceView.xaml` with `MinWidth="640"` and `MinHeight="500"`.
    - [ ] Ensure `DataGrid` columns use `*` or `Auto` where appropriate to prevent horizontal clipping.
- [ ] **HomeView Adaptive Grid**:
    - [ ] Replace `UniformGrid` with a more flexible `WrapPanel` or dynamically scaling `Grid`.
    - [ ] Update `HomeView.xaml` with `MinWidth="800"` and `MinHeight="600"`.
- [ ] **Global DPI Safety**:
    - [ ] Ensure `UseLayoutRounding="True"` and `SnapsToDevicePixels="True"` are set at the root of `LecgWindow` or the primary views.

## 2. Wave 2: The Style Sweep (Tertiary Views)
**Goal**: Finalize the replacement of legacy styles with global `LecgTheme.xaml` resources across all remaining windows.

### Tasks:
- [ ] **Legacy Style Audit**:
    - [ ] Scan all XAML files for `ModernTextBoxStyle`, `ModernButtonStyle`, or local `ResourceDictionary.MergedDictionaries`.
- [ ] **Bulk Style Migration**:
    - [ ] Update secondary views (e.g., `AlignView`, `AssignMaterialView`, `SexyRevitView`, `PurgeView`, `OffsetsView`, etc.) to use the global theme.
    - [ ] Replace hardcoded hex colors with `DynamicResource` tokens from `LecgColors.xaml`.
- [ ] **Semantic Helper Rollout**:
    - [ ] Adopt `SectionHeaderStyle` and `LinkButtonStyle` in secondary views where applicable.

## 3. Wave 3: Performance & Final Verification
**Goal**: Prove that the unified identity performs under load and correctly handles high-resolution displays.

### Tasks:
- [ ] **High-Load Performance Pass**:
    - [ ] Verify `LecgDataGrid` virtualization and thread-safe filtering under heavy model data.
- [ ] **Final Build & Test**:
    - [ ] Run `dotnet build` to ensure no XAML parser errors in migrated views.
    - [ ] Run `/verify` for Phase 4.

---

## Success Criteria (Wave Completion)
- All 18 views utilize `LecgTheme.xaml` resources exclusively.
- Primary views (`HomeView`, `SearchReplaceView`) have explicit resize boundaries to prevent UI crunching.
- The build is stable and high-performance.
