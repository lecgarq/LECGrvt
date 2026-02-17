using System;
using Autodesk.Revit.DB;

namespace LECG.Services.Interfaces
{
    public interface IRenderAppearanceSingleSyncService
    {
        void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null);
    }
}
