using Autodesk.Revit.DB;
using LECG.Services.Interfaces;
using System;

namespace LECG.Services
{
    public class MaterialTypeAssignmentProcessService : IMaterialTypeAssignmentProcessService
    {
        private readonly IMaterialColorSequenceService _materialColorSequenceService;
        private readonly IMaterialCreationService _materialCreationService;
        private readonly IMaterialTypeAssignmentService _materialTypeAssignmentService;
        private readonly IMaterialTypeEligibilityService _materialTypeEligibilityService;

        public MaterialTypeAssignmentProcessService(IMaterialColorSequenceService materialColorSequenceService, IMaterialCreationService materialCreationService, IMaterialTypeAssignmentService materialTypeAssignmentService, IMaterialTypeEligibilityService materialTypeEligibilityService)
        {
            _materialColorSequenceService = materialColorSequenceService;
            _materialCreationService = materialCreationService;
            _materialTypeAssignmentService = materialTypeAssignmentService;
            _materialTypeEligibilityService = materialTypeEligibilityService;
        }

        public bool TryProcess(Document doc, ElementType elemType, int elementCount, Action<string>? logCallback)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(elemType);

            if (_materialTypeEligibilityService.TryGetSkipReason(elemType, out string skipReason))
            {
                logCallback?.Invoke($"  SKIP: {skipReason}");
                return false;
            }

            string typeName = elemType.Name;
            logCallback?.Invoke("");
            logCallback?.Invoke($"TYPE: {typeName} ({elementCount} elements)");

            Color color = _materialColorSequenceService.GetNextColor();
            ElementId materialId = _materialCreationService.GetOrCreateMaterial(doc, typeName, color, logCallback);
            _materialTypeAssignmentService.AssignMaterialToType(doc, elemType, materialId, logCallback);
            return true;
        }
    }
}
