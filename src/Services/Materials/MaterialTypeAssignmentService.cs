using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class MaterialTypeAssignmentService : IMaterialTypeAssignmentService
    {
        public bool AssignMaterialToType(Document doc, ElementType type, ElementId materialId, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(doc);
            ArgumentNullException.ThrowIfNull(type);
            ArgumentNullException.ThrowIfNull(materialId);

            if (type is HostObjAttributes hostType)
            {
                CompoundStructure? cs = hostType.GetCompoundStructure();
                if (cs == null)
                {
                    logCallback?.Invoke($"  WARNING No compound structure on: {type.Name}");
                    return false;
                }

                IList<CompoundStructureLayer> layers = cs.GetLayers();
                for (int i = 0; i < layers.Count; i++)
                {
                    cs.SetMaterialId(i, materialId);
                }

                hostType.SetCompoundStructure(cs);
                logCallback?.Invoke($"  OK Assigned material to layers in: {type.Name}");
                return true;
            }

            Parameter? structMatParam = type.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
            if (structMatParam != null && !structMatParam.IsReadOnly)
            {
                structMatParam.Set(materialId);
                logCallback?.Invoke($"  OK Assigned to: {type.Name} (Structural Material Param)");
                return true;
            }

            logCallback?.Invoke($"  WARNING Could not assign material to: {type.Name}");
            return false;
        }
    }
}
