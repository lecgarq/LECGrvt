using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Models;

namespace LECG.Services.Interfaces
{
    public interface IRenderAppearanceBatchSyncService
    {
        void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            RenderAppearanceSettings settings,
            IProgressReporter reporter);
        void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            RenderAppearanceSettings settings,
            Action<string>? logCallback = null,
            Action<double, string>? progressCallback = null);
    }
}
