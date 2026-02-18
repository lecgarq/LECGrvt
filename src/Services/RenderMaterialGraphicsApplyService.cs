using System;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderMaterialGraphicsApplyService : IRenderMaterialGraphicsApplyService
    {
        public void Apply(Material mat, Color color, ElementId solidId, Action<string>? logCallback = null)
        {
            ArgumentNullException.ThrowIfNull(mat);
            ArgumentNullException.ThrowIfNull(color);
            ArgumentNullException.ThrowIfNull(solidId);

            mat.Color = color;
            mat.SurfaceForegroundPatternId = solidId;
            mat.SurfaceForegroundPatternColor = color;
            mat.SurfaceBackgroundPatternId = solidId;
            mat.SurfaceBackgroundPatternColor = color;
            mat.CutForegroundPatternId = solidId;
            mat.CutForegroundPatternColor = color;
            mat.CutBackgroundPatternId = solidId;
            mat.CutBackgroundPatternColor = color;
            logCallback?.Invoke($"    -> Color: RGB({color.Red}, {color.Green}, {color.Blue})");
        }
    }
}
