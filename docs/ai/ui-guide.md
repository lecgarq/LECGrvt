# UI Guide

How LECG's WPF UI works, and the one rule that constrains every UI decision.

## The rule: never style outside our own windows

A Revit add-in shares a process with Revit and every other loaded add-in. `Application.Current` **is Revit's WPF application**. Anything merged into `Application.Current.Resources` applies process-wide.

`LecgTheme.xaml` carries implicit styles (`TargetType` with no `x:Key`) for `Button`, `TextBox`, `ComboBox`, `RadioButton`, `ProgressBar`, `TreeView`, `TreeViewItem`. Merged globally, those restyle Revit's own dialogs and other vendors' add-ins.

**So the theme is merged per-window, in `LecgWindow.ApplyTheme()`.** Every LECG view derives from `LecgWindow`, so every window is themed and nothing outside is touched. `App.InitializeGlobalWpfDictionaries` only ensures a WPF `Application` exists (its `Dispatcher` is used across the add-in) — it must never merge resources again.

Checks before any UI change:

- Nothing merged into `Application.Current.Resources`. Ever.
- New implicit styles go in `LecgTheme.xaml` (window-scoped), never in a globally merged dictionary.
- New windows derive from `LecgWindow`. A raw `Window` will be unstyled *and* unscoped.

Both rules are enforced by `LECG.Tests/Views/ThemeScopingTests.cs`.

### The trap: XAML `Resources` replaces, it does not merge

`LecgWindow.ApplyTheme()` populates `Resources` in the constructor, but a view declaring

```xml
<base:LecgWindow.Resources>
    <ResourceDictionary> ... </ResourceDictionary>
</base:LecgWindow.Resources>
```

**replaces** that dictionary when `InitializeComponent()` runs. The constructor's merge is discarded, and `StaticResource` lookups then fail at parse time with `Cannot find resource named '...'` — a runtime error the compiler and the test suite cannot see.

So **every view merges `LecgTheme.xaml` in its own XAML**, exactly as all 25 now do. The constructor merge is only a fallback for a window that declares no `Resources` block at all.

This is not theoretical: `HomeView` and `SearchReplaceView` declared their own dictionaries without the merge and broke on `DashboardCardStyle` the first time the theme was scoped.

## Design tokens

Defined in `src/Resources/`, layered:

| File | Holds |
|------|-------|
| `Base/Colors.xaml`, `Themes/LecgColors.xaml` | Raw colors |
| `Base/Brushes.xaml` | Brushes over those colors |
| `Base/Fonts.xaml`, `Base/Sizes.xaml` | Type scale, spacing |
| `Buttons.xaml`, `Containers.xaml`, `Controls.xaml` | Control styles |
| `Themes/LecgTheme.xaml` | Aggregate + implicit base-control styles |

Use the tokens. A literal `#RRGGBB` or a hardcoded margin in a view is a bug — it will not follow a theme change.

## Third-party control libraries

Evaluated 2026-07-25. **Recommendation: adopt none of them as dependencies.** Mine them for design decisions instead.

| Library | Verdict | Why |
|---------|---------|-----|
| **WPF-UI** (lepoco) | Reference only | Closest fit — Fluent look, and `ApplicationThemeManager.Apply(element)` can scope to one element. But it restyles base controls (`Page`, `ToggleButton`, `List`) and expects `App.xaml` wiring the add-in does not own. |
| **HandyControl** | No | Setup is a `SkinDefault.xaml` merge into `App.xaml` — exactly the global merge that leaks. Dozens of controls we do not need. |
| **MaterialDesignInXAML** | No | Requires `BundledTheme` + `MaterialDesign*.Defaults.xaml` in `Application.Resources` explicitly so every WPF control is restyled automatically. Directly contradicts the rule above. Material's language also clashes with Revit's native look. |
| **WinUI 3 / Windows App SDK** | Not viable | A different UI stack with its own app model and bootstrapper, not a WPF control library. Cannot be dropped into a WPF window hosted inside Revit. Rewriting LECG's UI on it is not a WPF migration, it is a new application. |

The pattern: all four apply themselves through application-level implicit styles. That is the correct design for an app that owns its process, and the wrong one for an add-in that does not.

We already have a working design system. The gap was never a missing library — it was scoping, now fixed.

### What to mine instead

When a rewrite or a new view needs direction, take **decisions**, not dependencies:

- **WPF-UI** — Fluent spacing, elevation, and accent handling; how it structures light/dark token sets.
- **MaterialDesignInXAML** — elevation/shadow scale and ripple timing as motion reference.
- **HandyControl** — interaction patterns for controls we lack (property grid, color picker) if we ever build our own.
- **WinUI 3** — Microsoft's current desktop visual language, the thing Revit itself is drifting toward.

Copy a token, a measurement, or an interaction rule into `src/Resources/`. Do not copy a dependency.

## If a rewrite is ever proposed

Reasonable: restructure `src/Resources/` into a cleaner token layer, consolidate the implicit styles, adopt WPF-UI *visual decisions* by hand.

Not reasonable without a written case: adding any of the four as a NuGet dependency, or moving off WPF. Both change what loads into Revit's process, and the blast radius is every user's Revit session.
