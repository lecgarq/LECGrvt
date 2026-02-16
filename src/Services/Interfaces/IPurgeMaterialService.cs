using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IPurgeMaterialService
    {
        int PurgeUnusedMaterials(Document doc, Action<string>? logCallback = null);
    }
}
