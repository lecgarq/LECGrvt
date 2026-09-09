using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.ViewModels;
using LECG.Views;
using System;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ConvertCadCommand : ExternalEventCommand<ConvertCadEventHandler>
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<CadConversionService>();
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();
            var viewModel = ServiceLocator.GetRequiredService<ConvertCadViewModel>();

            ConvertCadEventHandler handler = GetOrCreateHandler();
            handler.Initialize(viewModel, service, transactionService, _logger);

            // Initialize ViewModel with Selection
            foreach (Element preselectedElement in SelectionSeedHelper.GetSelectedElements(uiDoc, null))
            {
                if (preselectedElement is ImportInstance)
                {
                    viewModel.SetSelection(preselectedElement);
                    break;
                }
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
            // Pass VM explicitly so command and view share the same instance
            var view = ServiceLocator.CreateWith<ConvertCadView>(viewModel);
            view.Initialize(uiDoc);
            view.Show();
        }
    }

    public enum CadOpType { None, Convert, Place }

    public class ConvertCadEventHandler : IExternalEventHandler
    {
        private ConvertCadViewModel? _viewModel;
        private CadConversionService? _service;
        private ITransactionService? _transactionService;
        private ILogger? _logger;
        private CadOpType _requestedOp = CadOpType.None;

        public void Initialize(ConvertCadViewModel vm, CadConversionService svc, ITransactionService transactionService, ILogger logger)
        {
            _viewModel = vm;
            _service = svc;
            _transactionService = transactionService;
            _logger = logger;
        }

        public void RequestOperation(CadOpType op) => _requestedOp = op;

        public void Execute(UIApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);

            if (_viewModel == null || _service == null || _transactionService == null || _logger == null || _requestedOp == CadOpType.None)
            {
                return;
            }

            UIDocument uiDoc = app.ActiveUIDocument
                ?? throw new InvalidOperationException("No active Revit document is available for CAD conversion.");
            Document doc = uiDoc.Document;

            try
            {
                if (_requestedOp == CadOpType.Convert)
                {
                    RunConversion(doc, _viewModel, _service);
                }
                else if (_requestedOp == CadOpType.Place)
                {
                    RunPlacement(uiDoc, doc, _viewModel, _transactionService);
                }
            }
            catch (Exception ex) when (IsExpectedCadOperationException(ex))
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

        private void RunConversion(Document doc, ConvertCadViewModel viewModel, CadConversionService service)
        {
            viewModel.AddLog("Starting conversion...");
            viewModel.Progress = 0;
            viewModel.IsBusy = true;

            var mColor = viewModel.LineColor;
            var rColor = new Autodesk.Revit.DB.Color(mColor.R, mColor.G, mColor.B);
            ElementId createdId = ElementId.InvalidElementId;
            var reporter = new RevitCommandProgressReporter(_logger!, (percentage, message) =>
            {
                viewModel.Progress = percentage;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    viewModel.AddLog(message);
                }
            });

            if (viewModel.UseSelectedImport)
            {
                Element? e = doc.GetElement(viewModel.SelectedElementId);
                if (e is not ImportInstance importInstance)
                {
                    throw new InvalidOperationException("The selected element is no longer a valid CAD import instance.");
                }

                createdId = service.ConvertCadToFamily(doc, importInstance, viewModel.NewFamilyName, viewModel.TemplatePath, viewModel.LineStyleName, rColor, viewModel.LineWeight, reporter);
            }
            else
            {
                createdId = service.ConvertDwgToFamily(doc, viewModel.DwgFilePath, viewModel.NewFamilyName, viewModel.TemplatePath, viewModel.LineStyleName, rColor, viewModel.LineWeight, reporter);
            }

            viewModel.CreatedFamilySymbolId = createdId;
            viewModel.Progress = 100;
            viewModel.IsFinished = true;
            viewModel.IsBusy = false;
            viewModel.AddLog("Success! Operation completed.");
        }

        private void RunPlacement(UIDocument uiDoc, Document doc, ConvertCadViewModel viewModel, ITransactionService transactionService)
        {
            if (viewModel.CreatedFamilySymbolId == null || viewModel.CreatedFamilySymbolId == ElementId.InvalidElementId)
            {
                return;
            }

            FamilySymbol? symbol = doc.GetElement(viewModel.CreatedFamilySymbolId) as FamilySymbol;
            if (symbol == null) return;

            // Detail items (2D) cannot be placed in 3D views.
            if (uiDoc.ActiveView.ViewType == ViewType.ThreeD)
            {
                viewModel.AddLog("Placement failed: detail items can only be placed in 2D views.");
                return;
            }

            // Ensure symbol is active
            if (!symbol.IsActive)
            {
                transactionService.Run(doc, "Activate Symbol", _ => symbol.Activate());
            }

            try
            {
                viewModel.AddLog("Starting placement... closing window.");
                viewModel.CloseAction?.Invoke();
                uiDoc.PromptForFamilyInstancePlacement(symbol);
            }
            catch (Exception ex) when (IsExpectedCadOperationException(ex))
            {
                viewModel.AddLog($"Placement failed: {ex.Message}");
            }
        }

        private static bool IsExpectedCadOperationException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is OperationCanceledException
                || ex is RevitExceptions.InvalidOperationException;
        }

        public string GetName() => "LECG CAD Conversion Handler";
    }
}
