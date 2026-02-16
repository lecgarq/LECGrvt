---
name: UI Standards Guide
description: Guidelines for styling, resources, and UX consistency.
---

# UI Standards

To maintain a premium "Sexy Revit" feel, all UI components must adhere to the design system defined in `src/Resources/Styles.xaml`.

## 1. Base Window

Always inherit Views from `base:LecgWindow`.

- **Namespace**: `LECG.Views.Base`
- **Features**: Custom chrome, icon integration, consistent padding/margins.

## 2. Styling Controls

Do **NOT** use default WPF styles. Use the specific resources defined in `Styles.xaml`.

| Control | Style Resource | Usage |
| :--- | :--- | :--- |
| **Button (Primary)** | `PrimaryButtonStyle` | The main action (e.g., "Convert", "Run"). |
| **Button (Secondary)** | `SecondaryButtonStyle` | Cancel, Close, or secondary options. |
| **TextBox** | `ModernTextBoxStyle` | All text inputs. |
| **CheckBox** | `ModernCheckboxStyle` | Options/Toggles. |
| **Cards** | `CardStyle` | Grouping related settings (border + padding). |
| **Card Header** | `CardHeaderStyle` | Title inside a card. |

## 3. Icons (`AppImages.cs`)

Do not embed images directly by path.

1. Add PNG to `src/Resources/Images`.
2. Register property in `src/Utils/AppImages.cs`.
3. Use in code: `AppImages.MyIcon`.

## 4. Text & Constants (`UIConstants.cs`)

Do not duplicate strings. Store user-facing text in `src/Configuration/UIConstants.cs`.

- Button Names
- Button Tooltips
- Label Text (where applicable for reuse)

## 5. Feedback / Progress

Use the `LogViewModel` / `LogView` system for long-running processes.

- Do not use `TaskDialog` for progress.
- call `Logger.Instance.Log("Message")` and `Logger.Instance.UpdateProgress(50, "Working...")`.
