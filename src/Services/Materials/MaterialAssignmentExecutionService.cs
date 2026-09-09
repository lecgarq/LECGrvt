using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialAssignmentExecutionService
    {
        private readonly MaterialElementGroupingService _materialElementGroupingService;
        private readonly MaterialTypeAssignmentProcessService _materialTypeAssignmentProcessService;
        private readonly ITransactionService _transactionService;

        public MaterialAssignmentExecutionService(
            MaterialElementGroupingService materialElementGroupingService,
            MaterialTypeAssignmentProcessService materialTypeAssignmentProcessService,
            ITransactionService transactionService)
        {
            _materialElementGroupingService = materialElementGroupingService;
            _materialTypeAssignmentProcessService = materialTypeAssignmentProcessService;
            _transactionService = transactionService;
        }

        public void AssignMaterialsToElements(Document doc, IList<Element> elements, IProgressReporter reporter)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(reporter);

            if (elements == null || !elements.Any()) return;

            reporter.Log("ANALYZING SELECTION");
            reporter.Report("Grouping by type...", 10);

            Dictionary<ElementId, List<Element>> elementsByType = _materialElementGroupingService.GroupByType(elements);

            reporter.Log($"Found {elementsByType.Count} unique types from {elements.Count} elements.");
            reporter.Log("");
            reporter.Log("CREATING/UPDATING MATERIALS");

            int processedTypes = 0;
            int totalTypes = elementsByType.Count;

            _transactionService.Run(doc, "Assign Material by Type", currentDoc =>
            {
                foreach (var kvp in elementsByType)
                {
                    processedTypes++;
                    double pct = 20 + processedTypes * 70.0 / totalTypes;

                    ElementType? elemType = currentDoc.GetElement(kvp.Key) as ElementType;
                    if (elemType == null) continue;

                    reporter.Report($"Processing: {elemType.Name}", pct);
                    _materialTypeAssignmentProcessService.TryProcess(currentDoc, elemType, kvp.Value.Count, reporter.Log);
                }
            });

            reporter.Log("");
            reporter.Log("COMPLETE");
            reporter.Log($"Processed {processedTypes} types.");
            reporter.Report("Done", 100);
        }
    }
}
