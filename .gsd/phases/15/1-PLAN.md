---
phase: 15
plan: 1
wave: 1
---

# PLAN 15.1: XAML Stability Audit

Perform a deep "compiler-level" audit of XAML scaling and resource resolution.

## Tasks

### 1. DPI Scaling Verification
<task>
- Audit `LecgWindow.cs` and `Containers.xaml` to ensure `SizeToContent` and `ResizeMode` interactions are stable.
- Verify that `WindowPadding` and `ViewContentMargin` use `DynamicResource` to allow for potential runtime scaling adjustments.
</task>

### 2. Complex View Stress Test
<task>
- Focus on `BatchRenameView.xaml` and `ConvertCadView.xaml`.
- Verify `ColumnDefinition` behavior: ensure `Auto` columns don't collapse when text is empty.
- Add `MinHeight` to `DataGrid` rows to prevent overlapping at high DPI.
</task>

### 3. Final Build & Dependency Check
<task>
- Run a full clean build.
- Manually review `Styles.xaml` for any "orphaned" resources (unused brushes/colors).
</task>

## Verification
- Final `dotnet build` with zero warnings (warnings treated as errors for this phase).
