using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using LECG.Models;
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

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, RenderAppearanceSettings settings, IProgressReporter reporter)
        {
            _batchSyncService.BatchSync(doc, materials, settings, reporter);
        }

    }
}
