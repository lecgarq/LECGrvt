# RESEARCH Phase 12: Window Management & Persistence

## 1. Window State Persistence
We have a global `SettingsManager` that handles JSON serialization. 
- **Goal**: Save `Left`, `Top`, `Width`, `Height` on `Closing` or `LocationChanged`.
- **Constraint**: Must handle multi-monitor setups. If a monitor is disconnected, the window shouldn't manifest "off-screen".
- **Implementation**: Create a `WindowSettings` model and a helper method in `LecgWindow` to serialize/deserialize.

## 2. DPI Awareness in Revit
Revit 2024 and 2026 are Per-Monitor DPI aware. 
- **Problem**: `AllowsTransparency="True"` can cause blurring or incorrect sizing when moving windows across screens.
- **Solution**: Use `VisualTreeHelper` to get the current DPI scale and adjust calculations in `CenterOnParent`.
- **Note**: `CanResizeWithGrip` in the style should be validated for hit-testing in the chromeless template.

## 3. Responsive Layout Bugs
The user reported "layout collapse" and "internal components not filling space".
- **Diagnosis**: Many views use `SizeToContent="Height"`. If the window is manually resized larger, the content grid might be stuck at its "Auto" height, leaving dead space at the bottom.
- **Fix**: 
    - Change `LecgWindow` default `SizeToContent` to `Manual` if a saved size exists.
    - Ensure the internal ContentPresenter uses `VerticalAlignment="Stretch"` and `HorizontalAlignment="Stretch"`.
    - Set reasonable `MinHeight` in the `Styles.xaml` or base style.

## 4. Window State Model
```csharp
public class LecgWindowSettings
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public WindowState State { get; set; }
}
```
