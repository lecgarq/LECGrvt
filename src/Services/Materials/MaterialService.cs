using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Visual;
using LECG.Models;
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

        public void SyncWithRenderAppearance(Document doc, Material mat, IProgressReporter reporter)
        {
            _renderAppearanceService.SyncWithRenderAppearance(doc, mat, reporter);
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, RenderAppearanceSettings settings, IProgressReporter reporter)
        {
            _renderAppearanceService.BatchSyncWithRenderAppearance(doc, materials, settings, reporter);
        }

        public ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null)
        {
            return _materialPbrService.CreatePBRMaterial(doc, name, folderPath, logCallback);
        }

        public ElementId CreatePBRMaterial(Document doc, PbrMaterialCreateRequest request, Action<string>? logCallback = null)
        {
            return _materialPbrService.CreatePBRMaterial(doc, request, logCallback);
        }

        public void AssignMaterialsToElements(Document doc, IList<Element> elements, IProgressReporter reporter)
        {
            _materialAssignmentExecutionService.AssignMaterialsToElements(doc, elements, reporter);
        }

    }
}
