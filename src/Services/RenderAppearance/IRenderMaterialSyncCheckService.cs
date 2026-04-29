using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderMaterialSyncCheckService
    {
        bool IsMaterialSynced(Material mat, Color targetColor, ElementId solidId);
    }
}
