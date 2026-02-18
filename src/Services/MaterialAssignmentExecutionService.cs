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
        private readonly IMaterialColorSequenceService _materialColorSequenceService;
        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialTypeAssignmentService _materialTypeAssignmentService;
        private readonly IMaterialTypeEligibilityService _materialTypeEligibilityService;

        public MaterialAssignmentExecutionService(
            IMaterialElementGroupingService materialElementGroupingService,
            IMaterialColorSequenceService materialColorSequenceService,
            IMaterialCreationService materialCreationService,
            IMaterialTypeAssignmentService materialTypeAssignmentService,
            IMaterialTypeEligibilityService materialTypeEligibilityService)
        {
            _materialElementGroupingService = materialElementGroupingService;
            _materialColorSequenceService = materialColorSequenceService;
            _materialCreationService = materialCreationService;
            _materialTypeAssignmentService = materialTypeAssignmentService;
            _materialTypeEligibilityService = materialTypeEligibilityService;
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
                    double pct = 20 + (processedTypes * 70.0 / totalTypes);

                    ElementType? elemType = doc.GetElement(kvp.Key) as ElementType;
                    if (elemType == null) continue;

                    string typeName = elemType.Name;

                    if (_materialTypeEligibilityService.TryGetSkipReason(elemType, out string skipReason))
                    {
                        logCallback?.Invoke($"  SKIP: {skipReason}");
                        continue;
                    }

                    progressCallback?.Invoke(pct, $"Processing: {typeName}");
                    logCallback?.Invoke("");
                    logCallback?.Invoke($"TYPE: {typeName} ({kvp.Value.Count} elements)");

                    Color color = _materialColorSequenceService.GetNextColor();
                    ElementId materialId = _materialCreationService.GetOrCreateMaterial(doc, typeName, color, logCallback);
                    _materialTypeAssignmentService.AssignMaterialToType(doc, elemType, materialId, logCallback);
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
