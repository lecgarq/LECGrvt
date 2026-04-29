using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderMaterialGraphicsApplyService
    {
        void Apply(Material mat, Color color, ElementId solidId, Action<string>? logCallback = null);
    }
}
