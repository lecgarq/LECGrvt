---
phase: 8
plan: 2
wave: 2
---

# Plan 8.2: Premium UI Redesign

## Objective
Transform the Batch Rename tool's UI from a functional-but-flat layout into a premium, polished interface. The user wants to be **wowed** at first glance. This plan redesigns the XAML and adds necessary design tokens.

## Context
- src/Views/SearchReplaceView.xaml
- src/Resources/Base/Colors.xaml
- src/Resources/Base/Brushes.xaml
- src/Resources/Controls.xaml
- src/Resources/Containers.xaml

## Tasks

<task type="auto">
  <name>Enhance design tokens for premium visual system</name>
  <files>
    src/Resources/Base/Colors.xaml
    src/Resources/Base/Brushes.xaml
    src/Resources/Controls.xaml
  </files>
  <action>
    1. In Colors.xaml, add:
       - `ColorAccent` → A vibrant brand accent (e.g., `#4F46E5` indigo-600 or `#6366F1` indigo-500)
       - `ColorAccentLight` → Lighter tint for badges/highlights (`#EEF2FF`)
       - `ColorAccentHover` → Darker press state (`#4338CA`)
       - `ColorSurfaceElevated` → Slight elevation shade for cards (`#F8FAFC`)
       - `ColorDataGridRowAlt` → Alternating row color (`#F9FAFB`)
       - `ColorDataGridRowHover` → Row hover (`#F1F5F9`)
       - `ColorScopeActive` → Active badge bg (`#EEF2FF`)
       - `ColorScopeActiveText` → Active badge text (`#4F46E5`)
       
    2. In Brushes.xaml, create corresponding SolidColorBrush entries for all new colors.
    
    3. In Controls.xaml, add:
       - `SectionHeaderStyle` → A TextBlock style: FontSize 11, small-caps letter-spacing feel, bold, accent-colored, with left accent bar (via a Border wrapping a TextBlock in a Grid)
       - `PillCheckboxStyle` → A new checkbox template that renders as a rounded pill with the label centered inside. When checked: accent background + white text. When unchecked: subtle border + muted text. Hover: slightly brighter border.
       - `DataGridRowStyle` → Alternating row background, subtle hover, smooth transitions
       - `StatusBarTextStyle` → Muted small text for item counts
       
    - Do NOT break existing styles used by other views
    - All new styles must use design tokens, not hardcoded colors
  </action>
  <verify>dotnet build LECG.csproj -c Release 2>&1 | Select-String "error CS|Build succeeded"</verify>
  <done>Build succeeds. New design tokens and styles exist in resource files.</done>
</task>

<task type="auto">
  <name>Redesign SearchReplaceView.xaml with premium layout</name>
  <files>src/Views/SearchReplaceView.xaml</files>
  <action>
    Rewrite SearchReplaceView.xaml with these premium upgrades:
    
    **1. Header (Row 0)**
    - Keep existing chrome but add a subtle bottom accent line (1px gradient)
    
    **2. Scope Section (Row 1)**
    - Replace raw "Selection Scope" label with a section header using `SectionHeaderStyle`
    - Replace plain checkboxes with `PillCheckboxStyle` — compact pill badges in a horizontal WrapPanel
    - Keep all existing bindings (ScopeTypeName, ScopeFamilyName, etc.) exactly as-is
    
    **3. Filter Section (within Row 1)**
    - Section header: "FILTERS" with accent bar
    - Keep filter inputs and advanced expander exactly as-is functionally
    - Add custom chevron icon to the Expander header template using an accent-colored arrow
    
    **4. Operations Section (Row 2)**
    - Each operation card: add a subtle left accent bar (3px colored strip) when active
    - Add slight shadow/elevation on hover using a DropShadowEffect
    - Keep ALL existing operation bindings intact (Replace, Case, Remove, Add, Numbering, Extension)
    
    **5. DataGrid Section (Row 3)**
    - Section header: "PREVIEW" with item count (bind to PreviewItems.Count)
    - Move "Select All / Select None" to the right of the section header as small text links instead of buttons
    - Add alternating row colors using DataGridRowStyle
    - Add a "Type" tag column (colored pill showing the element type) between the checkbox and old name  
    - Show a status bar below the DataGrid: "{N} items · {M} selected"
    
    **6. Footer (Row 4)**
    - "Apply Rename" button: use accent color gradient background instead of flat primary
    - Add a subtle "⚡" or rename icon next to the text
    
    **CRITICAL BINDINGS TO PRESERVE (do NOT break any of these):**
    - All scope checkboxes: ScopeTypeName, ScopeFamilyName, ScopeViewName, ScopeSheetName, ScopeMaterialName, ScopeObjectStyleName, ScopeLineStyleName, ScopeFillPatternName, ScopeFamilyParameterName
    - Filter inputs: FilterName, FilterCategory, SelectedFilterType, AvailableCategories
    - Advanced filters: FilterParamGroup, AvailableParamGroups, FilterIsInstanceIndex, FilterIsReadOnlyIndex
    - Operations: ReplaceRule, CaseRule, RemoveRule, AddRule, NumberingRule
    - DataGrid: PreviewItems, IsChecked, OriginalValue, NewValue, ElementId
    - Commands: ApplyCommand, SelectAllCommand, SelectNoneCommand, ReplaceSpacesCommand, ReplaceSpacesInReplaceTextCommand
    - Window: CloseWindow click handler
    
    - Do NOT change any ViewModel code
    - Do NOT change any service code
  </action>
  <verify>dotnet build LECG.csproj -c Release 2>&1 | Select-String "error CS|Build succeeded"</verify>
  <done>Build succeeds. SearchReplaceView.xaml has premium styling with all existing bindings preserved.</done>
</task>

## Success Criteria
- [ ] Build succeeds with 0 errors
- [ ] Scope section shows pill-style badges instead of raw checkboxes
- [ ] Section headers have accent color bars and uppercase labels
- [ ] DataGrid has alternating rows, status bar, type tags
- [ ] Operation cards have left accent bars when active
- [ ] Footer has accent-gradient "Apply Rename" button
- [ ] ALL existing ViewModel bindings and behavior are preserved
