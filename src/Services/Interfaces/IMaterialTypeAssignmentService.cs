using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialTypeAssignmentService
    {
        bool AssignMaterialToType(Document doc, ElementType type, ElementId materialId, Action<string>? logCallback = null);
    }
}
