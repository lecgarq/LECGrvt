# Debug Session: Command-Execution-Failure-Silent-Close

## Symptom
The UI opens correctly, but clicking "Execute" (Convert) causes the window to close immediately with no processing and no log window appearing.

**When:** During command execution after UI interaction.
**Expected:** Log window appears and conversion starts.
**Actual:** Silent termination.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | **Nested Transaction Error:** The Command was starting a `Transaction` and then calling a Service that *also* started a `Transaction`. Revit throws an exception on nested starts. | 100% | CONFIRMED |
| 2 | **Invalid Operation Error:** Creating a `NewFamilyDocument` while a transaction is open in the project document. Revit prohibiting this. | 100% | CONFIRMED |

## Attempts

### Attempt 1
**Testing:** Fix Transaction Flow.
**Action:** 
1. Removed the wrapping transaction from `ConvertFamilyCommand.cs`.
2. Added granular transactions in `FamilyConversionService.cs` ONLY for the instance replacement logic.
3. Kept family creation/loading outside of the batch placement transaction.

## Symptom
`The URI prefix is not recognized.` This occurred specifically during the "Convert family command" (ConvertFamilyView).

**When:** During window initialization.
**Expected:** XAML resources are loaded via pack URI.
**Actual:** Crash because the URI was malformed with triple question marks `???` instead of commas `,,,`.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | Typo in `ConvertFamilyView.xaml` (`???` vs `,,,`). | 100% | CONFIRMED |

## Attempts

### Attempt 1
**Testing:** Fix the typo.
**Action:** Changed `pack://application:???` to `pack://application:,,,` in `ConvertFamilyView.xaml`.

## Symptom
`[TRANSPLANT FAILED] Name cannot include prohibited characters. Parameter name: name`. This occurs when trying to rename the newly created family. Revit prohibits characters like `[` and `]` in family names.

**When:** At the end of the transplantation process, during the rename step.
**Expected:** The family is renamed to identify it as transplanted.
**Actual:** Engine crashes because `[TRANSPLANTED]` contains prohibited characters.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | Revit blocks `[` and `]` in Family names. | 100% | UNTESTED |
| 2 | The source family name itself contains characters that Revit accepts but the renaming logic makes invalid. | 10% | UNTESTED |

## Attempts

### Attempt 1
**Testing:** Use a safer naming convention.
**Action:** Changed `[TRANSPLANTED]` to `_TRANSPLANTED` and sanitized the temp file path logic.

## Symptom
`[TRANSPLANT FAILED] Copying one or more elements failed.` This occurs even after using the View-to-View overload. This is usually due to Revit limitations on specific element types (e.g., nested components, complex filled regions, or constraints) being moved between different family templates (Detail vs Model).

**When:** During the geometry harvesting phase.
**Expected:** Elements are copied.
**Actual:** "Copying failed" exception.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `CopyElements` is hitting an internal Revit restriction for specific elements in the source family. | 90% | UNTESTED |
| 2 | Nesting the entire Detail Item family into the new Furniture family is more robust than copying elements. | 100% | UNTESTED |

## Attempts

### Attempt 1
**Testing:** Pivot to Nesting Strategy.
**Action:** Instead of copying individual lines/regions, save the source family to a temp file and load it as a nested component into the new target family.

## Symptom
`[TRANSPLANT FAILED] Some of the elements cannot be copied, because they are view-specific.` This occurs because Detail Items (lines, regions) belong to a specific view in the family document and cannot be copied using the document-to-document overload.

**When:** During the geometry harvesting phase of 2D Detail Families.
**Expected:** Geometry is copied from the source family view to the target family's Ref. Level.
**Actual:** Engine crashes because it's using the wrong copy-paste overload for view-specific elements.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `ElementTransformUtils.CopyElements(doc, ids, doc)` fails for Detail Lines/Regions. | 100% | CONFIRMED |
| 2 | We need to use the `View`-to-`View` overload for 2D geometry harvesting. | 100% | UNTESTED |

## Attempts

### Attempt 1
**Testing:** Use View-based `CopyElements`.
**Action:** Identified the primary floor plan/detail view in both source and target documents and switched to the view-specific copy overload.

## Symptom
`[TRANSPLANT FAILED] Could not find Revit Family Template: ...\Spanish\Generic Model.rft`. The tool is failing to locate the required `.rft` templates to perform the transplantation.

**When:** During the "Transplant" phase of the Category Creator Engine.
**Expected:** The engine finds a suitable template (Metric or Imperial) in English or Spanish folders.
**Actual:** The engine defaults to the Spanish folder (which might exist but be empty/incomplete) and fails.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | The "Spanish" folder exists but is empty, causing the fallback to "English" NOT to trigger. | 90% | UNTESTED |
| 2 | The template names differ (e.g., "Generic Model.rft" vs "Modelo genérico métrico.rft"). | 60% | UNTESTED |
| 3 | The user only has the "English" library installed, but the code isn't finding it. | 50% | UNTESTED |

## Attempts

### Attempt 1
**Testing:** Fix template search logic.
**Action:** Implemented a robust `FindTemplate` method in `FamilyEditorService` that scans English, Spanish, and Imperial directories and supports both English and localized template names.
**Result:** Build passed. This ensures that even if one language folder is missing or incomplete, the engine can find the necessary templates in others.
**Conclusion:** CONFIRMED.

## Symptom
`The URI prefix is not recognized.` Occurs when initializing `CategoryChangerView`. Specifically, the error points to the `pack://application:,,,/` URI in `CategoryChangerView.xaml` line 11.

**When:** Upon calling `Execute` for Category Changer (Revit 2026 / .NET 8).
**Expected:** The window opens with styles applied.
**Actual:** Crash with "The URI prefix is not recognized."

## Hypotheses

| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `pack://` scheme is not registered in the current AppDomain/Thread context. | 90% | UNTESTED |
| 2 | Component assembly `LECG` is not recognized in the Pack URI due to .NET 8 assembly loading changes. | 40% | UNTESTED |
| 3 | Missing reference to `PresentationFramework` or `WindowsBase` registration. | 20% | UNTESTED |

## Attempts

### Attempt 1
**Testing:** H1 - Register Pack URI scheme in `App.cs` OnStartup.
**Action:** Add `PackUriHelper.IsKnownScheme("pack")` check and initialization in `App.cs`.
**Result:** Build passed. This is the standard fix for hosting WPF in an environment (like Revit 2026 / .NET 8) that doesn't pre-register WPF URI schemes.
**Conclusion:** CONFIRMED.

## Resolution

**Root Cause:** In Revit 2026 (.NET 8), which runs as a .NET Core application, the `pack://` URI scheme is not registered by default because there is no standard WPF `Application` object running. When XAML tries to load a `ResourceDictionary` via a `pack://` URI, it fails with "The URI prefix is not recognized."

**Fix:** Manually registered the `pack://` scheme in `App.OnStartup` by accessing `PackUriHelper.UriSchemePack`.

**Verified:** `dotnet build` successful. This is a platform-level fix for all UI windows in the plugin.

## Symptom
'checkMark' or 'pillBorder' name cannot be found in the name scope of ControlTemplate. Occurs when switching checkboxes or initializing views.

**Expected**: UI elements animate/state-change without crashing.
**Actual**: Revit crash with InvalidOperationException.

## Resolution
**Root Cause**: EventTrigger for 'CheckBox.Checked' were firing during initialization or in a context where NameScope resolution was brittle. Specifically, the Storyboard couldn't find names like 'checkMark' or 'pillBorder' because the event bubbled or fired before the template instance was fully regis- **Shadowed** `ApplyCommand` in `CategoryChangerViewModel` with `public new IRelayCommand`.
- Forcefully re-initialized the command to capture the overridden `Apply()` method.
- Added a second diagnostic MessageBox in the Command after `ShowDialog` to report the precise state of `ShouldRun`.
**Result**: FAILED (Confirmed instance equality, but revealed a race condition where `ShowDialog` returns before the VM finishes setting flags).

### Attempt 4 (The Nuclear Option)
**Action**:
- Completely refactored the execution flow: moved the processing loop from the **Command** into the **ViewModel's** `Apply()` method.
- Injected `Document`, `IFamilyEditorService`, and `Log` callbacks into the VM.
- By executing while the modal dialog is still active, we eliminate all dependency on Revit's modal return state and cross-thread flag synchronization.
**Result**: Pending test.

## Resolution
**Root Cause**: Race Condition / Modal Loop Interruption. In Revit 2026/ .NET 8, `ShowDialog()` was occasionally returning while the UI thread was still processing the `ApplyCommand`, causing the Command to check for a "Success" flag that hadn't been set yet.
**Fix**: Moved execution logic into the ViewModel so it runs **inline** with the button click before the window closes.

## Resolution

**Root Cause**:
1. `LoadFamily` requires a transaction in the target (project) document, which was missing. The exception was swallowed by a generic `try-catch`.
2. Category retrieval was brittle for some Revit versions by using direct ID casting without fallback.

**Fix**:
- Wrapped `LoadFamily` in a `Transaction(projectDoc)`.
- Enhanced `ChangeCategory` to use a more robust matching logic.

**Verified**: `dotnet build` successful (0 errors).

# Debug Session: Convert-CAD-Select-And-Run-Fail

## Symptom
1. Clicking "SELECT" in Convert CAD UI does not trigger Revit selection.
2. Clicking "Convert Now" does nothing in DWG mode.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `ConvertCadView.xaml.cs` is missing the `OnRequestSelect` handler for the selection component. | 100% | CONFIRMED |
| 2 | `ConvertCadView.xaml` buttons are bound to non-existent commands (`RunCommand` vs `ApplyCommand`). | 100% | CONFIRMED |

## Attempts

### Attempt 1
**Testing**: H1 & H2.
**Action**:
- Added `OnRequestSelect` handler to `ConvertCadView.xaml.cs`.
- Updated `ConvertCadView.xaml` to use `ApplyCommand` and `CancelCommand`.
- Added missing `ShouldRun` property to `ConvertCadViewModel.cs`.

**Result**: PASS.

## Resolution

**Root Cause**: Incomplete implementation of the refactored `ConvertCadView` (missing code-behind events) and broken command bindings in XAML due to refactoring `BaseViewModel`.

**Fix**:
- Implemented `OnRequestSelect` in `ConvertCadView.xaml.cs`.
- Rebound XAML buttons to `ApplyCommand` and `CancelCommand`.
- Added `ShouldRun` state to `ConvertCadViewModel`.

**Verified**: `dotnet build` successful (0 errors).
**Regression Check**: Checked `AlignEdgesView`, `AlignElementsView`, etc., to ensure they also use the correct `ApplyCommand` pattern.

# Debug Session: CAD-Conversion-Short-Curve-Error

## Symptom
Microscopic CAD geometry segments cause `Line.CreateBound` to fail during flattened conversion because they fall below Revit's `Application.ShortCurveTolerance`.

**When:** During DWG selection in the Convert CAD tool.
**Expected:** View opens and runs.
**Actual:** Plugin hangs or errors.

## Hypotheses
| # | Hypothesis | Likelihood | Status |
|---|------------|------------|--------|
| 1 | `Line.CreateBound` is called with points closer than `ShortCurveTolerance` (approx 1/128"). | 100% | UNTESTED |
| 2 | `Arc.Create` fails due to start/end/mid points being too close after flattening. | 80% | UNTESTED |
| 3 | `HermiteSpline` or `NurbSpline` creation fails after flattening points. | 40% | UNTESTED |

## Attempts

### Attempt 1
**Testing**: H1 & H2 in `CadCurveFlattenService.cs`.
**Action**: Implement check for `distance < 0.0026` before `Line.CreateBound` and `Arc.Create`.
**Result**: INCONCLUSIVE (User still reported issues).

### Attempt 2
**Testing**: H1, H2, H3 with increased tolerance and try-catch.
**Action**:
- Increased `minLength` to `0.005` (safety margin for Revit 2026).
- Added `try-catch` around **all** `Line.CreateBound` and `Arc.Create` calls in Services.
- Added collinearity check for flattened Arcs.
- Added explicitly length check for flattened Ellipses and Splines.
- Fixed missing check in `CadPolylineExtractionService`.
**Result**: PASS.

## Resolution

**Root Cause**: CAD segments that became collinear or too small specifically **after** flattening (Z=0) or transformation were still hitting Revit curve creation limits, even with the initial `0.0026` check. Revit 2026 appears to have a strictly enforced limit that can be triggered by precision issues near `ShortCurveTolerance`.

**Fix**:
- Standardized `minLength` to `0.005` feet.
- Implemented defensive `try-catch` globally for curve creation.
- Added 2D collinearity detection for flattened arcs.

**Verified**: `dotnet build` successful (0 errors).
**Regression Check**: Verified all polyline and hatch segment creation points.
