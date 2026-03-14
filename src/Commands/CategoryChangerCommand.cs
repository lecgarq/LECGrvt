using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CategoryChangerCommand : ExternalEventCommand<CategoryChangerEventHandler>
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<IFamilyEditorService>();
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

            var view = ServiceLocator.GetRequiredService<CategoryChangerView>();
            view.Initialize(uiDoc);

            view.Show();
        }
    }

    public class CategoryChangerEventHandler : IExternalEventHandler
    {
        private CategoryChangerViewModel? _viewModel;
        private IFamilyEditorService? _service;
        private ITransactionService? _transactionService;

        public void Initialize(CategoryChangerViewModel vm, IFamilyEditorService svc, ITransactionService transactionService)
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
                                catch (Exception ex) when (ex.Message.Contains("Platform Limit"))
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
                                    catch (Exception transplantEx)
                                    {
                                        _viewModel.OnLog?.Invoke($"  [TRANSPLANT FAILED] {transplantEx.Message}");
                                        failCount++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _viewModel.OnLog?.Invoke($"  [ERROR] {ex.Message}");
                                    failCount++;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _viewModel.OnLog?.Invoke($"  [ERROR] {ex.Message}");
                    }
                }

                _viewModel.OnLog?.Invoke("------------------------------------");
                _viewModel.OnLog?.Invoke($"Completed. Families updated: {successCount}, Failed: {failCount}");
                
                // Close the dialog after completion (optional, could leave open)
                _viewModel.CloseAction?.Invoke();
            }
            catch (Exception ex)
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
            FamilySymbol newSymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(s => s.Family.Id == newFamily.Id)!;

            if (newSymbol == null) return;

            // 2. Find all instances of the old family
            var oldInstances = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Where(fi => fi.Symbol.Family.Id == oldFamily.Id)
                .ToList();

            _viewModel.OnLog?.Invoke($"  [SWAPPING] Found {oldInstances.Count} instances to update...");

            _transactionService.Run(doc, "Swap Transplanted Instances", _ =>
            {
                if (!newSymbol.IsActive) newSymbol.Activate();

                foreach (var oldFi in oldInstances)
                {
                    try
                    {
                        LocationPoint lp = (oldFi.Location as LocationPoint)!;
                        if (lp == null) continue;

                        XYZ pos = lp.Point;
                        double rot = lp.Rotation;
                        Level? level = doc.GetElement(oldFi.LevelId) as Level;

                        // Create new instance
                        FamilyInstance newFi = doc.Create.NewFamilyInstance(pos, newSymbol, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        
                        // Apply rotation if needed
                        if (Math.Abs(rot) > 0.0001)
                        {
                            Line axis = Line.CreateBound(pos, pos + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, newFi.Id, axis, rot);
                        }

                        // Delete old
                        doc.Delete(oldFi.Id);
                    }
                    catch (Exception ex)
                    {
                        _viewModel.OnLog?.Invoke($"    [ERROR] Failed to swap instance {oldFi.Id}: {ex.Message}");
                    }
                }
            });
        }

        public string GetName() => "LECG Category Changer Handler";
    }
}
