using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderAppearanceService : IRenderAppearanceService
    {
        private readonly IRenderAppearanceSingleSyncService _singleSyncService;
        private readonly IRenderAppearanceBatchSyncService _batchSyncService;

        public RenderAppearanceService(
            IRenderAppearanceSingleSyncService singleSyncService,
            IRenderAppearanceBatchSyncService batchSyncService)
        {
            _singleSyncService = singleSyncService;
            _batchSyncService = batchSyncService;
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, IProgressReporter reporter)
        {
            _singleSyncService.SyncWithRenderAppearance(doc, mat, reporter);
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            _singleSyncService.SyncWithRenderAppearance(doc, mat, new LegacyProgressReporter(logCallback: logCallback));
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, IProgressReporter reporter)
        {
            _batchSyncService.BatchSync(doc, materials, reporter);
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            _batchSyncService.BatchSync(doc, materials, new LegacyProgressReporter(progressCallback, logCallback));
        }
    }
}
