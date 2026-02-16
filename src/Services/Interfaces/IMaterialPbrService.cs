using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialPbrService
    {
        ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null);
    }
}
