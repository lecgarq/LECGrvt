using Autodesk.Revit.DB;
using System;

namespace LECG.Services.Interfaces
{
    public interface IMaterialTypeAssignmentProcessService
    {
        bool TryProcess(Document doc, ElementType elemType, int elementCount, Action<string>? logCallback);
    }
}
