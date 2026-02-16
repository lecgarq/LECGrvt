using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialAssignmentExecutionService
    {
        void AssignMaterialsToElements(Document doc, IList<Element> elements, Action<string>? logCallback, Action<double, string>? progressCallback);
    }
}
