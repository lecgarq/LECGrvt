using System;
using Autodesk.Revit.DB;

namespace LECG.Services
{
    public interface IMaterialService
    {
        Color GetNextColor();
        ElementId GetOrCreateMaterial(Document doc, string name, Color color, Action<string>? logCallback = null);
        ElementId CreatePBRMaterial(Document doc, string name, string folderPath, Action<string>? logCallback = null);
        bool AssignMaterialToType(Document doc, ElementType type, ElementId materialId, Action<string>? logCallback = null);
        void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null);
        void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
        void AssignMaterialsToElements(Document doc, IList<Element> elements, Action<string>? logCallback, Action<double, string>? progressCallback);
    }
}
