using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IRenderMaterialSyncExecutionService
    {
        RenderMaterialSyncResult TrySync(Material material, ElementId solidFillPatternId, RenderAppearanceSettings settings, Action<string>? logCallback = null);
    }
}
