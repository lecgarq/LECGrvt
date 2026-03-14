#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using System;
using System.Linq;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertCadCommand : ExternalEventCommand<ConvertCadEventHandler>
    {
        protected override string? TransactionName => null; // Internal transactions used

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<ICadConversionService>();
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();
            var viewModel = ServiceLocator.GetRequiredService<ConvertCadViewModel>();

            ConvertCadEventHandler handler = GetOrCreateHandler();
            handler.Initialize(viewModel, service, transactionService);

            // Initialize ViewModel with Selection
            var selId = uiDoc.Selection.GetElementIds();
            if (selId.Count == 1)
            {
                Element e = doc.GetElement(selId.First());
                if (e is ImportInstance) viewModel.SetSelection(e);
            }

            // Attach Event triggers to ViewModel
            viewModel.RunOperation = () =>
            {
                handler.RequestOperation(CadOpType.Convert);
                RaiseExternalEvent();
            };

            viewModel.PlaceOperation = () =>
            {
                handler.RequestOperation(CadOpType.Place);
                RaiseExternalEvent();
            };

            // STEP 1: CONFIGURATION (Non-modal)
            var view = ServiceLocator.GetRequiredService<ConvertCadView>();
            view.Initialize(uiDoc);
            view.Show();
        }
    }

    public enum CadOpType { None, Convert, Place }

    public class ConvertCadEventHandler : IExternalEventHandler
    {
        private ConvertCadViewModel _viewModel;
        private ICadConversionService _service;
        private ITransactionService _transactionService;
        private CadOpType _requestedOp = CadOpType.None;

        public void Initialize(ConvertCadViewModel vm, ICadConversionService svc, ITransactionService transactionService)
        {
            _viewModel = vm;
            _service = svc;
            _transactionService = transactionService;
        }

        public void RequestOperation(CadOpType op) => _requestedOp = op;

        public void Execute(UIApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            if (_viewModel == null || _requestedOp == CadOpType.None) return;

            UIDocument uiDoc = app.ActiveUIDocument;
            Document doc = uiDoc.Document;

            try
            {
                if (_requestedOp == CadOpType.Convert)
                {
                    RunConversion(doc);
                }
                else if (_requestedOp == CadOpType.Place)
                {
                    RunPlacement(uiDoc, doc);
                }
            }
            catch (Exception ex)
            {
                _viewModel.AddLog($"FATAL ERROR [{ex.GetType().Name}]: {ex.Message}");
                _viewModel.IsBusy = false;
                _viewModel.AddLog("Operation failed. Review the log for details.");
            }
            finally
            {
                _requestedOp = CadOpType.None;
            }
        }

        private void RunConversion(Document doc)
        {
            _viewModel.AddLog("Starting conversion...");
            _viewModel.Progress = 0;
            _viewModel.IsBusy = true;
            
            var mColor = _viewModel.LineColor;
            var rColor = new Autodesk.Revit.DB.Color(mColor.R, mColor.G, mColor.B);
            ElementId createdId = ElementId.InvalidElementId;
            var reporter = new SimpleProgressReporter(report =>
            {
                _viewModel.Progress = report.Percentage;
                if (!string.IsNullOrWhiteSpace(report.Message))
                {
                    _viewModel.AddLog(report.Message);
                }
            });

            if (_viewModel.UseSelectedImport)
            {
                Element e = doc.GetElement(_viewModel.SelectedElementId);
                createdId = _service.ConvertCadToFamily(doc, (ImportInstance)e, _viewModel.NewFamilyName, _viewModel.TemplatePath, _viewModel.LineStyleName, rColor, _viewModel.LineWeight, reporter);
            }
            else
            {
                createdId = _service.ConvertDwgToFamily(doc, _viewModel.DwgFilePath, _viewModel.NewFamilyName, _viewModel.TemplatePath, _viewModel.LineStyleName, rColor, _viewModel.LineWeight, reporter);
            }

            _viewModel.CreatedFamilySymbolId = createdId;
            _viewModel.Progress = 100;
            _viewModel.IsFinished = true;
            _viewModel.IsBusy = false;
            _viewModel.AddLog("Success! Operation completed.");
        }

        private void RunPlacement(UIDocument uiDoc, Document doc)
        {
            if (_viewModel.CreatedFamilySymbolId == null || _viewModel.CreatedFamilySymbolId == ElementId.InvalidElementId)
                return;

            FamilySymbol symbol = doc.GetElement(_viewModel.CreatedFamilySymbolId) as FamilySymbol;
            if (symbol == null) return;

            // Detail items (2D) cannot be placed in 3D views.
            if (uiDoc.ActiveView.ViewType == ViewType.ThreeD)
            {
                _viewModel.AddLog("Placement failed: detail items can only be placed in 2D views.");
                return;
            }

            // Ensure symbol is active
            if (!symbol.IsActive)
            {
                _transactionService.Run(doc, "Activate Symbol", _ => symbol.Activate());
            }

            try 
            {
                _viewModel.AddLog("Starting placement... closing window.");
                _viewModel.CloseAction?.Invoke();
                uiDoc.PromptForFamilyInstancePlacement(symbol);
            }
            catch (Exception ex)
            {
                _viewModel.AddLog($"Placement failed: {ex.Message}");
            }
        }

        public string GetName() => "LECG CAD Conversion Handler";
    }
}
