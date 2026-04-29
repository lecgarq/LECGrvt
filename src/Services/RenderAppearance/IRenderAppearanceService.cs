using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IRenderAppearanceService
    {
        void SyncWithRenderAppearance(Document doc, Material mat, IProgressReporter reporter);
        void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null);
        void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, RenderAppearanceSettings settings, IProgressReporter reporter);
        void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, RenderAppearanceSettings settings, Action<string>? logCallback = null, Action<double, string>? progressCallback = null);
    }
}
