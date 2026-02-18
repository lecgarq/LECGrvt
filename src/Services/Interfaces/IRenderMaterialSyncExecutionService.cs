using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderMaterialSyncExecutionService
    {
        bool TrySync(Material material, ElementId solidFillPatternId);
    }
}
