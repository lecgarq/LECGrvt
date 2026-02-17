using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderAppearanceBatchSyncService
    {
        void BatchSync(
            Document doc,
            IEnumerable<Material> materials,
            Action<string>? logCallback = null,
            Action<double, string>? progressCallback = null);
    }
}
