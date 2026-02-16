#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8618
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialService : IMaterialService
    {
        private readonly IRenderAppearanceService _renderAppearanceService;
        private readonly IMaterialTypeAssignmentService _materialTypeAssignmentService;
        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialColorSequenceService _materialColorSequenceService;
        private readonly IMaterialPbrService _materialPbrService;
        private readonly IMaterialElementGroupingService _materialElementGroupingService;

        public MaterialService() : this(new RenderAppearanceService(), new MaterialTypeAssignmentService(), new MaterialCreationService(), new MaterialColorSequenceService(), new MaterialPbrService(), new MaterialElementGroupingService()) { }

        public MaterialService(IRenderAppearanceService renderAppearanceService, IMaterialTypeAssignmentService materialTypeAssignmentService, IMaterialCreationService materialCreationService, IMaterialColorSequenceService materialColorSequenceService, IMaterialPbrService materialPbrService, IMaterialElementGroupingService materialElementGroupingService)
        {
            _renderAppearanceService = renderAppearanceService;
            _materialTypeAssignmentService = materialTypeAssignmentService;
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
            _materialPbrService = materialPbrService;
            _materialElementGroupingService = materialElementGroupingService;
        }

        public Color GetNextColor()
        {
            return _materialColorSequenceService.GetNextColor();
        }

        public ElementId GetOrCreateMaterial(Document doc, string name, Color color, Action<string>? logCallback = null)
        {
            return _materialCreationService.GetOrCreateMaterial(doc, name, color, logCallback);
        }

        public bool AssignMaterialToType(Document doc, ElementType type, ElementId materialId, Action<string>? logCallback = null)
        {
            return _materialTypeAssignmentService.AssignMaterialToType(doc, type, materialId, logCallback);
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            _renderAppearanceService.SyncWithRenderAppearance(doc, mat, logCallback);
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            _renderAppearanceService.BatchSyncWithRenderAppearance(doc, materials, logCallback, progressCallback);
        }

        public ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null)
        {
            return _materialPbrService.CreatePBRMaterial(doc, name, folderPath, logCallback);
        }

        public void AssignMaterialsToElements(Document doc, IList<Element> elements, Action<string>? logCallback, Action<double, string>? progressCallback)
        {
            if (elements == null || !elements.Any()) return;

            logCallback?.Invoke("ANALYZING SELECTION");
            progressCallback?.Invoke(10, "Grouping by type...");

            Dictionary<ElementId, List<Element>> elementsByType = _materialElementGroupingService.GroupByType(elements);

            logCallback?.Invoke($"  Found {elementsByType.Count} unique types from {elements.Count} elements.");

            // Process each Type
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
                    
                    // CHECK FOR MULTIPLE LAYERS
                    if (elemType is HostObjAttributes hostType)
                    {
                        var cs = hostType.GetCompoundStructure();
                        if (cs != null && cs.GetLayers().Count > 1)
                        {
                            logCallback?.Invoke($"  SKIP: {typeName} has {cs.GetLayers().Count} layers. Cannot assign single material.");
                            continue;
                        }
                    }

                    progressCallback?.Invoke(pct, $"Processing: {typeName}");
                    logCallback?.Invoke($"");
                    logCallback?.Invoke($"TYPE: {typeName} ({kvp.Value.Count} elements)");

                    // Get a color for this type
                    Color color = GetNextColor();

                    // Get or create material
                    ElementId materialId = GetOrCreateMaterial(doc, typeName, color, logCallback);

                    // Assign to type
                    AssignMaterialToType(doc, elemType, materialId, logCallback);
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
