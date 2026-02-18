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
        private readonly IMaterialAssignmentExecutionService _materialAssignmentExecutionService;

        public MaterialService() : this(
            new RenderAppearanceService(),
            new MaterialTypeAssignmentService(),
            new MaterialCreationService(),
            new MaterialColorSequenceService(),
            new MaterialPbrService(),
            new MaterialAssignmentExecutionService(
                new MaterialElementGroupingService(),
                new MaterialColorSequenceService(),
                new MaterialCreationService(),
                new MaterialTypeAssignmentService(),
                new MaterialTypeEligibilityService()))
        {
        }

        public MaterialService(IRenderAppearanceService renderAppearanceService, IMaterialTypeAssignmentService materialTypeAssignmentService, IMaterialCreationService materialCreationService, IMaterialColorSequenceService materialColorSequenceService, IMaterialPbrService materialPbrService, IMaterialAssignmentExecutionService materialAssignmentExecutionService)
        {
            _renderAppearanceService = renderAppearanceService;
            _materialTypeAssignmentService = materialTypeAssignmentService;
            _materialCreationService = materialCreationService;
            _materialColorSequenceService = materialColorSequenceService;
            _materialPbrService = materialPbrService;
            _materialAssignmentExecutionService = materialAssignmentExecutionService;
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
            _materialAssignmentExecutionService.AssignMaterialsToElements(doc, elements, logCallback, progressCallback);
        }
    }
}
