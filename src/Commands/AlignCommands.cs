using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using System.Linq;
using System.Collections.Generic;

namespace LECG.Commands
{
    // Base class for shared logic? No, just direct implementation for simplicity as requested.

    // --- Align Horizontal ---
    [Transaction(TransactionMode.Manual)]
    public class AlignLeftCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.Left;
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignCenterCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.Center;
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignRightCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.Right;
    }

    // --- Align Vertical ---
    [Transaction(TransactionMode.Manual)]
    public class AlignTopCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.Top;
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignMiddleCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.Middle;
    }

    [Transaction(TransactionMode.Manual)]
    public class AlignBottomCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.Bottom;
    }

    // --- Distribute ---
    [Transaction(TransactionMode.Manual)]
    public class DistributeHorizontallyCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.DistributeHorizontally;
    }

    [Transaction(TransactionMode.Manual)]
    public class DistributeVerticallyCommand : AlignCommandBase
    {
        public override AlignMode Mode => AlignMode.DistributeVertically;
    }

    // --- Base Abstract Command ---
    [Transaction(TransactionMode.Manual)]
    public abstract class AlignCommandBase : RevitCommand
    {
        public abstract AlignMode Mode { get; }

        private bool IsDistributeMode => Mode == AlignMode.DistributeHorizontally || Mode == AlignMode.DistributeVertically;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<IAlignElementsService>();

            // Distribute shortcut: if >= 3 elements already selected, skip the dialog
            if (IsDistributeMode)
            {
                ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();
                if (selectedIds.Count >= 3)
                {
                    List<Element> elements = selectedIds
                        .Select(id => doc.GetElement(id))
                        .Where(e => e != null)
                        .ToList();

                    service.Distribute(doc, elements, Mode);
                    return;
                }
            }

            var vm = ServiceLocator.GetRequiredService<AlignElementsViewModel>();
            vm.Initialize(Mode);
            if (vm.IsDistributeMode)
            {
                var preselectedTargets = SelectionSeedHelper.GetSelectedReferences(uiDoc, null);
                if (preselectedTargets.Count > 0)
                {
                    vm.SetTargets(preselectedTargets, doc);
                }
            }
            else
            {
                Reference? preselectedReference = SelectionSeedHelper.GetSelectedReferences(uiDoc, null, maxCount: 1).FirstOrDefault();
                if (preselectedReference != null)
                {
                    vm.SetReference(preselectedReference, doc);
                }
            }

            // Pass VM explicitly so command and view share the same instance
            var view = ServiceLocator.CreateWith<AlignElementsView>(vm);
            view.Initialize(uiDoc);

            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                if (vm.IsDistributeMode)
                {
                    // Convert References to Elements
                    List<Element> elements = vm.SelectedTargets
                       .Select(r => doc.GetElement(r))
                       .Where(e => e != null)
                       .ToList();

                    service.Distribute(doc, elements, Mode);
                }
                else
                {
                    // Align Mode
                    if (vm.SelectedReference == null) return;

                    Element refElem = doc.GetElement(vm.SelectedReference);
                    List<Element> targets = vm.SelectedTargets
                        .Select(r => doc.GetElement(r))
                        .Where(e => e != null)
                        .ToList();

                    service.Align(doc, refElem, targets, Mode);
                }
            }
        }
    }
}
