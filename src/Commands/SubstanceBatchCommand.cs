using System;
using System.Collections.Generic;
using System.Windows.Threading;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Core.Substance;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class SubstanceBatchCommand : RevitCommand
    {
        protected override string? TransactionName => null;

        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var createService = ServiceLocator.GetRequiredService<ISubstanceMaterialCreateService>();
            var viewModel = ServiceLocator.GetRequiredService<SubstanceBatchViewModel>();
            viewModel.SetExistingNames(createService.ExistingMaterialNames(doc));

            var view = ServiceLocator.CreateWith<SubstanceBatchView>(viewModel);
            view.Initialize(uiDoc);
            bool? dialogResult = view.ShowDialog();
            if (dialogResult != true || !viewModel.ShouldRun)
            {
                return;
            }

            IReadOnlyList<SubstanceMaterialEntry> entries = viewModel.SelectedEntries();
            SubstanceBatchSettings settings = viewModel.CurrentSettings();
            string outputRoot = string.IsNullOrWhiteSpace(settings.OutputRoot)
                ? BakeOutputPaths.DefaultOutputRoot(settings.LibraryRoot)
                : settings.OutputRoot;

            var options = new SubstanceBatchOptions(
                new BakeOptions(outputRoot, settings.TargetSize, settings.ForceRebake),
                TextureTransform.Uniform(settings.SizeMillimeters),
                settings.OverwriteExisting);

            ShowLogWindow($"Substance Batch: {entries.Count} material{(entries.Count == 1 ? "" : "s")}");
            if (_logViewModel != null) _logViewModel.CanCancel = true;

            foreach (string warning in viewModel.Warnings)
            {
                Log($"SCAN WARNING: {warning}");
            }

            var result = new SubstanceBatchResult();
            for (int i = 0; i < entries.Count; i++)
            {
                if (_logViewModel?.IsCancelRequested == true)
                {
                    Log($"CANCELLED after {i} of {entries.Count}.");
                    break;
                }

                SubstanceMaterialEntry entry = entries[i];
                UpdateProgress((double)i / entries.Count * 100, $"{i + 1}/{entries.Count}  {entry.DisplayName}");
                Log($"[{i + 1}/{entries.Count}] {entry.Category} / {entry.DisplayName}");

                result.Add(createService.Create(doc, entry, options, Log));
                PumpUi();
            }

            if (_logViewModel != null) _logViewModel.CanCancel = false;
            UpdateProgress(100, "Complete");
            Log($"COMPLETE: {result.Summary}");

            int shown = 0;
            foreach (SubstanceMaterialReport r in result.Reports)
            {
                if (r.Outcome != SubstanceMaterialOutcome.Failed) continue;
                Log($"  FAILED {r.DisplayName}: {r.Error}");
                if (++shown >= 10) { Log("  ... more failures omitted"); break; }
            }
        }

        /// <summary>Lets the log window repaint and receive the Cancel click while this command owns the UI thread.</summary>
        private static void PumpUi()
        {
            Dispatcher? dispatcher = System.Windows.Application.Current?.Dispatcher;
            dispatcher?.Invoke(() => { }, DispatcherPriority.Background);
        }
    }
}
