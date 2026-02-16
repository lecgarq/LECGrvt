using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IMaterialCreationService
    {
        ElementId GetOrCreateMaterial(Document doc, string name, Color color, Action<string>? logCallback = null);
    }
}
