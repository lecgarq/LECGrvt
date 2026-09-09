using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using LECG.Core;
using LECG.Services.Interfaces;
using LECG.Utilities;
using LECG.Views.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LECG.Commands
{
    /// <summary>
    /// Bulk-converts the shared parameters of families (in the open project) into ordinary
    /// non-shared family parameters, keeping the same name and behaviour. Operates on the
    /// families behind the current selection, or — when nothing is selected — optionally on
    /// every editable family in the project.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SharedToFamilyParameterCommand : RevitCommand
    {
        public override void Execute(UIDocument uiDoc, Document doc)
        {
            ArgumentNullException.ThrowIfNull(uiDoc);
            ArgumentNullException.ThrowIfNull(doc);

            if (doc.IsFamilyDocument)
            {
                LecgDialog.Show(
                    "Shared → Family Parameter",
                    "This tool runs in a project. Open the project that hosts the families, then run it again.");
                return;
            }

            var service = ServiceLocator.GetRequiredService<ISharedToFamilyParameterService>();

            List<Family>? families = ResolveTargetFamilies(uiDoc, doc);
            if (families == null)
            {
                // User cancelled selection.
                return;
            }

            if (families.Count == 0)
            {
                LecgDialog.Show(
                    "Shared → Family Parameter",
                    "No families were found to process.");
                return;
            }

            ShowLogWindow("Shared → Family Parameter");
            Log("--- Shared Parameter to Family Parameter ---");
            Log($"Processing {families.Count} unique family(ies).");
            Log("Existing instance/type values are preserved (overwriteParameterValues = false).");

            SharedToFamilyParameterResult result = service.ConvertFamilies(doc, families);

            Log("------------------------------------");
            Log($"Families opened:     {result.FamiliesProcessed}");
            Log($"Families modified:   {result.FamiliesModified}");
            Log($"Families skipped:    {result.FamiliesSkipped}");
            Log($"Families failed:     {result.FamiliesFailed}");
            Log($"Parameters converted:{result.ParametersConverted}");
            Log("--- Completed ---");
        }

        /// <summary>
        /// Determines which families to process. Returns <c>null</c> if the user cancelled.
        /// </summary>
        private List<Family>? ResolveTargetFamilies(UIDocument uiDoc, Document doc)
        {
            // 1. Pre-selected family instances win.
            var preselected = SelectionSeedHelper.GetSelectedReferences(uiDoc, new AnyFamilyInstanceFilter());
            if (preselected.Count > 0)
            {
                return FamiliesFromReferences(doc, preselected);
            }

            // 2. Nothing selected — let the user pick instances or process the whole project.
            int choice = LecgDialog.ShowOptions(
                "Shared → Family Parameter",
                "No family instances are selected. How do you want to choose families to convert?",
                "Pick family instances in the view",
                "Process ALL editable families in the project");

            switch (choice)
            {
                case 0:
                    return FamiliesFromPick(uiDoc, doc);
                case 1:
                    return AllEditableComponentFamilies(doc);
                default:
                    return null; // cancelled
            }
        }

        private List<Family>? FamiliesFromPick(UIDocument uiDoc, Document doc)
        {
            try
            {
                var refs = uiDoc.Selection.PickObjects(
                    Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new AnyFamilyInstanceFilter(),
                    "Select family instances whose shared parameters should become family parameters.");
                return FamiliesFromReferences(doc, refs);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        private static List<Family> FamiliesFromReferences(Document doc, IList<Reference> refs)
        {
            return refs
                .Select(r => doc.GetElement(r) as FamilyInstance)
                .Where(fi => fi?.Symbol?.Family != null)
                .Select(fi => fi!.Symbol.Family)
                .GroupBy(f => f.Id)
                .Select(g => g.First())
                .ToList();
        }

        private static List<Family> AllEditableComponentFamilies(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .Where(f => f.IsEditable)
                .GroupBy(f => f.Id)
                .Select(g => g.First())
                .ToList();
        }
    }
}
