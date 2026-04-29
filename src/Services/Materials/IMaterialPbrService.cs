using System;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IMaterialPbrService
    {
        ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null);
        ElementId CreatePBRMaterial(Document doc, PbrMaterialCreateRequest request, Action<string>? logCallback = null);
    }
}
