using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderAppearanceService
    {
        void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null);
        void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
