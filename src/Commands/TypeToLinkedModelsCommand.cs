using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.Services.Logging;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Commands
{
    /// <summary>
    /// Separates model content by selected element types into individual Revit files.
    /// Each file preserves shared coordinates. The host model is cleaned to become
    /// a coordination container with the exported files linked back.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class TypeToLinkedModelsCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var exportService = ServiceLocator.GetRequiredService<ILinkedModelExportService>();
            var viewModel = ServiceLocator.GetRequiredService<TypeToLinkedModelsViewModel>();

            // Initialize ViewModel with document data
            viewModel.Initialize(doc);

            if (viewModel.TypeGroups.Count == 0)
            {
                Log("No element types found in the current document.");
                ShowLogWindow("Type to Linked Models");
                return;
            }

            // Show configuration dialog
            var view = ServiceLocator.CreateWith<TypeToLinkedModelsView>(viewModel);
            view.Initialize(uiDoc);
            bool? result = view.ShowDialog();

            if (result != true || !viewModel.ShouldRun) return;

            // Get selected groups
            Dictionary<string, List<ElementId>> selectedGroups = viewModel.GetSelectedGroups(doc);

            if (selectedGroups.Count == 0)
            {
                Log("No type groups selected for export.");
                return;
            }

            // Execute export and link
            ShowLogWindow("Type to Linked Models");
            Log($"[{DateTime.Now}] Starting Type to Linked Models...");
            Log($"Output folder: {viewModel.OutputFolder}");
            Log($"Selected {selectedGroups.Count} type groups for export.");
            Log("");

            var reporter = new RevitCommandProgressReporter(_logger, UpdateProgress);

            exportService.ExportAndLink(doc, selectedGroups, viewModel.OutputFolder, reporter);

            UpdateProgress(100, "Complete");
            Log("");
            Log("Type to Linked Models complete.");
        }
    }
}
