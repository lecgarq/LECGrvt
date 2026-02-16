using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderMaterialSyncCheckService : IRenderMaterialSyncCheckService
    {
        public bool IsMaterialSynced(Material mat, Color targetColor, ElementId solidId)
        {
            if (!ColorsEqual(mat.Color, targetColor)) return false;

            if (mat.SurfaceForegroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.SurfaceForegroundPatternColor, targetColor)) return false;

            if (mat.SurfaceBackgroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.SurfaceBackgroundPatternColor, targetColor)) return false;

            if (mat.CutForegroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.CutForegroundPatternColor, targetColor)) return false;

            if (mat.CutBackgroundPatternId != solidId) return false;
            if (!ColorsEqual(mat.CutBackgroundPatternColor, targetColor)) return false;

            return true;
        }

        private bool ColorsEqual(Color? c1, Color? c2)
        {
            if (c1 == null || c2 == null) return false;
            return c1.Red == c2.Red && c1.Green == c2.Green && c1.Blue == c2.Blue;
        }
    }
}
