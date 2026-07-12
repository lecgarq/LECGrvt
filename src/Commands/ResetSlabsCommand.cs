using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using LECG.Core;
using LECG.Views;
using LECG.ViewModels;
using LECG.Services;
using LECG.Services.Interfaces;
using RevitExceptions = Autodesk.Revit.Exceptions;

namespace LECG.Commands
{
    /// <summary>
    /// Command to reset slab shapes for Floor and Toposolid elements.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ResetSlabsCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            var slabService = ServiceLocator.GetRequiredService<ISlabService>();
            var transactionService = ServiceLocator.GetRequiredService<ITransactionService>();

            Log($"[{DateTime.Now}] Starting Reset Slabs Command...");

            // 1. Load Settings & UI
            var loadedSettings = SettingsManager.Load<ResetSlabsViewModel>("ResetSlabsSettings.json");
            var settings = ServiceLocator.GetRequiredService<ResetSlabsViewModel>();
            settings.DuplicateElements = loadedSettings.DuplicateElements;
            var preselectedRefs = SelectionSeedHelper.GetSelectedReferences(uiDoc, settings.Selection.Filter);
            if (preselectedRefs.Count > 0)
            {
                settings.SetSelection(preselectedRefs, doc);
            }

            // Pass VM explicitly so command and view share the same instance
            ResetSlabsView view = ServiceLocator.CreateWith<ResetSlabsView>(settings);
            view.Initialize(uiDoc);

            if (view.ShowDialog() != true || !settings.ShouldRun) return;

            SettingsManager.Save(settings, "ResetSlabsSettings.json");

            // 2. Select Elements - Already selected
            IList<Reference> refs = settings.SelectedRefs;
            if (refs == null || refs.Count == 0) return;

            Log($"Selected {refs.Count} elements.");

            bool duplicate = settings.DuplicateElements;

            // 3. Process
            int successCount = 0;
            List<ElementId> processedIds = new List<ElementId>();

            int processed = 0;
            transactionService.Run(doc, "Reset Slab Shapes", currentDoc =>
            {
                foreach (Reference r in refs)
                {
                    Element elem = currentDoc.GetElement(r);
                    if (elem == null) continue;

                    processed++;
                    UpdateProgress((double)processed / refs.Count * 95, $"Processing {processed} of {refs.Count}...");

                    Element targetElement = elem;
                    Log($"Processing ID: {elem.Id} ({elem.Category?.Name})");

                    if (duplicate)
                    {
                        try
                        {
                            var newElem = slabService.DuplicateElement(currentDoc, elem);
                            if (newElem != null)
                            {
                                targetElement = newElem;
                                Log($"  -> Duplicated to new ID: {targetElement.Id}");
                                processedIds.Add(targetElement.Id);
                            }
                        }
                        catch (Exception copyEx) when (IsExpectedResetSlabsException(copyEx))
                        {
                            Log($"  -> Copy Failed: {copyEx.Message}");
                            continue;
                        }
                    }
                    else
                    {
                        processedIds.Add(elem.Id);
                    }

                    // Reset
                    if (slabService.TryResetSlabShape(targetElement, out string msg))
                    {
                        successCount++;
                        Log($"  -> {msg}");
                    }
                    else
                    {
                        Log($"  -> {msg}");
                    }
                }
            });

            if (duplicate && processedIds.Count > 0)
            {
                uiDoc.Selection.SetElementIds(processedIds);
            }

            Log($"Finished. Successfully reset {successCount} slabs.");
            UpdateProgress(100, "Complete");

            // Show log for success
            ShowLogWindow("Reset Slabs");
        }

        private static bool IsExpectedResetSlabsException(Exception ex)
        {
            return ex is ArgumentException
                || ex is InvalidOperationException
                || ex is RevitExceptions.InvalidOperationException;
        }

    }
}
