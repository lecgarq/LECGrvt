using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Utilities;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CategoryChangerCommand : ExternalEventCommand<CategoryChangerEventHandler>
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<FamilyEditorService>();
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();
            var viewModel = ServiceLocator.GetRequiredService<CategoryChangerViewModel>();

            CategoryChangerEventHandler handler = GetOrCreateHandler();
            handler.Initialize(viewModel, service, transactionService);

            // Inject dependencies and callbacks
            viewModel.Doc = doc;
            viewModel.OnLog = (msg) => Log(msg);
            viewModel.OnShowLog = () => ShowLogWindow("Category Changer");

            // This is the key: ViewModel requests the operation, we raise the event
            viewModel.RequestRun = () =>
            {
                RaiseExternalEvent();
            };

            var preselectedRefs = SelectionSeedHelper.GetSelectedReferences(uiDoc, new AnyFamilyInstanceFilter());
            if (preselectedRefs.Count > 0)
            {
                viewModel.SetSelection(preselectedRefs, doc);
            }

            var view = ServiceLocator.CreateWith<CategoryChangerView>(viewModel);
            view.Initialize(uiDoc);

            view.Show();
        }
    }

    public class CategoryChangerEventHandler : IExternalEventHandler
    {
        private CategoryChangerViewModel? _viewModel;
        private FamilyEditorService? _service;
        private ITransactionService? _transactionService;

        public void Initialize(CategoryChangerViewModel vm, FamilyEditorService svc, ITransactionService transactionService)
        {
            _viewModel = vm;
            _service = svc;
            _transactionService = transactionService;
        }

        public void Execute(UIApplication app)
        {
            if (_viewModel == null || _service == null) return;
            if (_viewModel.Doc == null || _viewModel.SelectedRefs == null || _viewModel.SelectedCategory == null) return;

            try
            {
                _viewModel.OnShowLog?.Invoke();
                _viewModel.OnLog?.Invoke($"Processing {_viewModel.SelectedRefs.Count} elements...");

                var families = new HashSet<ElementId>();
                int successCount = 0;
                int failCount = 0;

                foreach (var r in _viewModel.SelectedRefs)
                {
                    try
                    {
                        Element el = _viewModel.Doc.GetElement(r);
                        if (el is FamilyInstance fi && fi.Symbol?.Family != null)
                        {
                            ElementId famId = fi.Symbol.Family.Id;
                            if (families.Add(famId))
                            {
                                _viewModel.OnLog?.Invoke($"Attempting to change family: {fi.Symbol.Family.Name}...");

                                try
                                {
                                    bool success = _service.ChangeCategory(fi.Symbol.Family, _viewModel.SelectedCategory);
                                    if (success)
                                    {
                                        _viewModel.OnLog?.Invoke($"  [SUCCESS] -> {_viewModel.SelectedCategory.Name}");
                                        successCount++;
                                    }
                                    else
                                    {
                                        _viewModel.OnLog?.Invoke($"  [FAILED]");
                                        failCount++;
                                    }
                                }
                                catch (Exception ex) when (IsPlatformLimitException(ex))
                                {
                                    _viewModel.OnLog?.Invoke("  [TRANSPLANTING] Revit blocks direct category change. Initiating Creator Engine...");

                                    try
                                    {
                                        Family sourceFamily = fi.Symbol.Family;
                                        Family newFamily = _service.RecreateAs(sourceFamily, _viewModel.SelectedCategory);

                                        if (newFamily != null)
                                        {
                                            _viewModel.OnLog?.Invoke($"  [SUCCESS] Created new 3D Family: {newFamily.Name}");

                                            // Optional: Swap instances in project
                                            SwapInstances(fi.Symbol.Family, newFamily);
                                            successCount++;
                                        }
                                    }
                                    catch (Exception transplantEx) when (IsExpectedCategoryChangerException(transplantEx))
                                    {
                                        _viewModel.OnLog?.Invoke($"  [TRANSPLANT FAILED] {transplantEx.Message}");
                                        failCount++;
                                    }
                                }
                                catch (Exception ex) when (IsExpectedCategoryChangerException(ex))
                                {
                                    _viewModel.OnLog?.Invoke($"  [ERROR] {ex.Message}");
                                    failCount++;
                                }
                            }
                        }
                    }
                    catch (Exception ex) when (IsExpectedCategoryChangerException(ex))
                    {
                        _viewModel.OnLog?.Invoke($"  [ERROR] {ex.Message}");
                    }
                }

                _viewModel.OnLog?.Invoke("------------------------------------");
                _viewModel.OnLog?.Invoke($"Completed. Families updated: {successCount}, Failed: {failCount}");

                // Close the dialog after completion (optional, could leave open)
                _viewModel.CloseAction?.Invoke();
            }
            catch (Exception ex) when (IsExpectedCategoryChangerException(ex))
            {
                _viewModel.OnLog?.Invoke($"[FATAL ERROR] {ex.Message}");
            }
        }

        private void SwapInstances(Family oldFamily, Family newFamily)
        {
            if (_viewModel?.Doc == null) return;
            if (_transactionService == null) return;

            Document doc = _viewModel.Doc;

            // 1. Get a symbol from the new family
            FamilySymbol? newSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Family.Id == newFamily.Id);

            if (newSymbol == null) return;

            // 2. Find all instances of the old family
            var oldInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol?.Family?.Id == oldFamily.Id)
                .ToList();

            _viewModel.OnLog?.Invoke($"  [SWAPPING] Found {oldInstances.Count} instances to update...");

            _transactionService.Run(doc, "Swap Transplanted Instances", _ =>
            {
                // PASS 1: scan all instances; log every unsupported one.
                // Refuse-all per CONTEXT.md §A: any unsupported instance aborts the batch.
                bool hasUnsupported = false;
                foreach (var fi in oldInstances)
                {
                    string? reason = GetUnsupportedReason(fi);
                    if (reason != null)
                    {
                        _viewModel.OnLog?.Invoke(
                            $"    [UNSUPPORTED] Instance {fi.Id} ({fi.Symbol?.Family?.Name}): {reason}");
                        hasUnsupported = true;
                    }
                }
                if (hasUnsupported)
                {
                    _viewModel.OnLog?.Invoke(
                        "    [REFUSED] Batch aborted — see [UNSUPPORTED] entries above. No instances were swapped.");
                    throw new InvalidOperationException(
                        "SwapInstances refused: one or more instances have unsupported location type. Batch rolled back.");
                }

                // PASS 2: swap (only reached if all instances are supported).
                if (!newSymbol.IsActive) newSymbol.Activate();

                foreach (var oldFi in oldInstances)
                {
                    try
                    {
                        // Pass 1 guarantees Location is LocationPoint, Host == null, HostFace == null.
                        LocationPoint locationPoint = (LocationPoint)oldFi.Location!;
                        XYZ pos = locationPoint.Point;
                        double rot = locationPoint.Rotation;
                        Level? level = doc.GetElement(oldFi.LevelId) as Level
                            ?? doc.ActiveView?.GenLevel
                            ?? new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault();

                        FamilyInstance? newFi = null;
                        try
                        {
                            if (level != null)
                            {
                                newFi = doc.Create.NewFamilyInstance(pos, newSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                            else
                            {
                                newFi = doc.Create.NewFamilyInstance(pos, newSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                            }
                        }
                        catch (Exception placementEx) when (level != null && IsExpectedCategoryChangerException(placementEx))
                        {
                            _viewModel.OnLog?.Invoke($"    [WARN] Level-based placement failed for {oldFi.Id}: {placementEx.Message}");
                            newFi = doc.Create.NewFamilyInstance(pos, newSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        }

                        if (newFi == null)
                        {
                            _viewModel.OnLog?.Invoke($"    [ERROR] Failed to create replacement instance for {oldFi.Id}.");
                            continue;
                        }

                        if (Math.Abs(rot) > 0.0001)
                        {
                            Line axis = Line.CreateBound(pos, pos + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, newFi.Id, axis, rot);
                        }

                        CopyWritableInstanceParameters(oldFi, newFi);

                        doc.Delete(oldFi.Id);
                    }
                    catch (Exception ex) when (IsExpectedCategoryChangerException(ex))
                    {
                        _viewModel.OnLog?.Invoke($"    [ERROR] Failed to swap instance {oldFi.Id}: {ex.Message}");
                    }
                }
            });
        }

        /// <summary>
        /// Best-effort copy of instance parameter values from the old instance to its
        /// replacement, matched by name and storage type. ElementId parameters (level/host
        /// references) are owned by placement, and elevation/offset built-ins are derived from
        /// the exact XYZ the replacement was placed at — copying either could move the instance.
        /// </summary>
        private void CopyWritableInstanceParameters(FamilyInstance source, FamilyInstance target)
        {
            foreach (Parameter sourceParam in source.Parameters)
            {
                try
                {
                    if (sourceParam.IsReadOnly || !sourceParam.HasValue) continue;
                    if (sourceParam.StorageType == StorageType.ElementId) continue;

                    var bip = (BuiltInParameter)sourceParam.Id.Value;
                    if (bip == BuiltInParameter.INSTANCE_ELEVATION_PARAM ||
                        bip == BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM)
                    {
                        continue;
                    }

                    Parameter? targetParam = target.LookupParameter(sourceParam.Definition.Name);
                    if (targetParam == null || targetParam.IsReadOnly || targetParam.StorageType != sourceParam.StorageType) continue;

                    switch (sourceParam.StorageType)
                    {
                        case StorageType.Double: targetParam.Set(sourceParam.AsDouble()); break;
                        case StorageType.Integer: targetParam.Set(sourceParam.AsInteger()); break;
                        case StorageType.String: targetParam.Set(sourceParam.AsString()); break;
                    }
                }
                catch (Exception ex) when (IsExpectedCategoryChangerException(ex))
                {
                    _viewModel?.OnLog?.Invoke($"    [WARN] Could not copy parameter '{sourceParam.Definition.Name}': {ex.Message}");
                }
            }
        }

        // Revit's ArgumentException derives from Autodesk.Revit.Exceptions.ApplicationException,
        // not System.ArgumentException — it must be listed explicitly or it aborts the handler.
        private static bool IsExpectedCategoryChangerException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.ArgumentException
                || ex is RevitExceptions.InvalidOperationException
                || ex is RevitExceptions.InvalidObjectException;
        }

        private static string? GetUnsupportedReason(FamilyInstance fi)
        {
            if (fi.Location is LocationCurve)
                return "curve-driven (LocationCurve)";
            if (fi.HostFace != null)
                return "face-hosted (HostFace non-null)";
            if (fi.Host != null)
                return $"hosted on element {fi.Host.Id}";
            if (fi.Location is not LocationPoint)
                return "unknown location type (not LocationPoint)";
            return null; // free-standing point — safe to swap
        }

        private static bool IsPlatformLimitException(Exception ex)
        {
            return IsExpectedCategoryChangerException(ex)
                && ex.Message.Contains("Platform Limit", StringComparison.Ordinal);
        }

        public string GetName() => "LECG Category Changer Handler";
    }
}
