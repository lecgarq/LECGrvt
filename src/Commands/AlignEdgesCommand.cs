using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;
using LECG.Views.Base;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignEdgesCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var service = ServiceLocator.GetRequiredService<AlignEdgesService>();

            var vm = ServiceLocator.GetRequiredService<AlignEdgesViewModel>();
            var preselectedSources = SelectionSeedHelper.GetSelectedReferences(uiDoc, new SelectionFilters.SlabFilter());
            if (preselectedSources.Count > 0)
            {
                vm.SetTargets(preselectedSources, doc);
            }

            var view = ServiceLocator.CreateWith<AlignEdgesView>(vm);
            view.Initialize(uiDoc);

            bool? result = view.ShowDialog();

            if (result == true && vm.ShouldRun)
            {
                IReadOnlyList<AlignEdgesSourceResult> sourceResults = vm.FindMyEdge
                    ? service.AlignEdgesFindMyEdge(doc, vm.TargetRefs)
                    : service.AlignEdges(doc, vm.TargetRefs, vm.ReferenceRefs);

                LecgDialog.Show("Align Edges", BuildCompletionMessage(sourceResults));
            }
        }

        private static string BuildCompletionMessage(IReadOnlyList<AlignEdgesSourceResult> sourceResults)
        {
            int alignedCount = sourceResults.Count(r => r.Status == AlignEdgesSourceStatus.Aligned);
            int partialCount = sourceResults.Count(r => r.Status == AlignEdgesSourceStatus.PartiallyAligned);
            int noTargetCount = sourceResults.Count(r => r.Status == AlignEdgesSourceStatus.NoValidTargets);
            int failedCount = sourceResults.Count(r => r.Status == AlignEdgesSourceStatus.Failed);

            return $"Processed {sourceResults.Count} source slabs.\n"
                + $"Aligned: {alignedCount}\n"
                + $"Partial: {partialCount}\n"
                + $"No valid targets: {noTargetCount}\n"
                + $"Failed: {failedCount}";
        }
    }
}
