using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialAssignmentExecutionService : IMaterialAssignmentExecutionService
    {
        private readonly IMaterialElementGroupingService _materialElementGroupingService;
        private readonly IMaterialAssignmentProgressService _materialAssignmentProgressService;
        private readonly IMaterialElementTypeResolverService _materialElementTypeResolverService;
        private readonly IMaterialTypeAssignmentProcessService _materialTypeAssignmentProcessService;

        public MaterialAssignmentExecutionService(
            IMaterialElementGroupingService materialElementGroupingService,
            IMaterialAssignmentProgressService materialAssignmentProgressService,
            IMaterialElementTypeResolverService materialElementTypeResolverService,
            IMaterialTypeAssignmentProcessService materialTypeAssignmentProcessService)
        {
            _materialElementGroupingService = materialElementGroupingService;
            _materialAssignmentProgressService = materialAssignmentProgressService;
            _materialElementTypeResolverService = materialElementTypeResolverService;
            _materialTypeAssignmentProcessService = materialTypeAssignmentProcessService;
        }

        public void AssignMaterialsToElements(Document doc, IList<Element> elements, Action<string>? logCallback, Action<double, string>? progressCallback)
        {
            if (elements == null || !elements.Any()) return;

            logCallback?.Invoke("ANALYZING SELECTION");
            progressCallback?.Invoke(10, "Grouping by type...");

            Dictionary<ElementId, List<Element>> elementsByType = _materialElementGroupingService.GroupByType(elements);

            logCallback?.Invoke($"  Found {elementsByType.Count} unique types from {elements.Count} elements.");
            logCallback?.Invoke("");
            logCallback?.Invoke("CREATING/UPDATING MATERIALS");

            int processedTypes = 0;
            int totalTypes = elementsByType.Count;

            using (Transaction t = new Transaction(doc, "Assign Material by Type"))
            {
                t.Start();

                foreach (var kvp in elementsByType)
                {
                    processedTypes++;
                    double pct = _materialAssignmentProgressService.ToProgressPercent(processedTypes, totalTypes);

                    ElementType? elemType = _materialElementTypeResolverService.Resolve(doc, kvp.Key);
                    if (elemType == null) continue;

                    progressCallback?.Invoke(pct, $"Processing: {elemType.Name}");
                    _materialTypeAssignmentProcessService.TryProcess(doc, elemType, kvp.Value.Count, logCallback);
                }

                t.Commit();
            }

            logCallback?.Invoke("");
            logCallback?.Invoke("COMPLETE");
            logCallback?.Invoke($"Processed {processedTypes} types.");
            progressCallback?.Invoke(100, "Done");
        }
    }
}
