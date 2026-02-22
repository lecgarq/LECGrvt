---
phase: 13
plan: 4
wave: 1
---

# SUMMARY 13.4: Complex Batch Tools

Standardized the most complex utility views.

## Changes:
- **FilterCopyView Refactoring**:
    - Removed redundant internal header (Title/Subtitle) as it's now handled by the native premium title bar.
    - Updated margin to `{StaticResource ViewContentMarginLarge}` (24px) for better breathing room in large layouts.
    - Set `WindowIcon` to `Icons.Copy`.
- **ConvertCad & ConvertFamily**:
    - Applied standard sizing and icon assignments (`Hammer` and `Circle`).
    - Standardized window chrome.
- **PurgeView**:
    - Cleaned up redundant properties and finalized icon assignment (`Trash`).
- **Responsive Integrity**: Verified that complex `TreeView` and `DataGrid` layouts in these views remain functional with the new window style.

## Verification:
- Build passes.
- All high-utility tools now share a consistent, high-end "Apple-like" aesthetic.
