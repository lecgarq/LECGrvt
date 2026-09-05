using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Batch.Handlers;
using LECG.Batch.ViewModels;
using LECG.Batch.Views;
using LECG.Core;

namespace LECG.Batch.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class BatchProcessCommand : RevitCommand
    {
        // Lazily created and reused for the entire session
        private static ExternalEvent? _externalEvent;
        private static RevitBatchJobHandler? _handler;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            EnsureExternalEvent();

            var vm = ServiceLocator.GetRequiredService<BatchProcessViewModel>();
            vm.RaiseExternalEvent = () => _externalEvent?.Raise();
            vm.SetHostRevitVersion(uiDoc.Application.Application.VersionNumber);
            vm.Initialize();

            var view = ServiceLocator.CreateWith<BatchProcessView>(vm);
            view.Show(); // Modeless — does not block Revit
        }

        private static void EnsureExternalEvent()
        {
            if (_externalEvent != null) return;

            _handler = ServiceLocator.GetRequiredService<RevitBatchJobHandler>();
            _externalEvent = ExternalEvent.Create(_handler);
            _handler.ExternalEvent = _externalEvent;
        }
    }
}
