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

        public RenderAppearanceService() : this(
            new RenderAppearanceSingleSyncService(new RenderSolidFillPatternService(), new RenderMaterialGraphicsApplyService()),
            new RenderAppearanceBatchSyncService(new RenderAppearanceRefreshService(), new RenderSolidFillPatternService(), new RenderMaterialSyncExecutionService(new RenderMaterialSyncCheckService(), new RenderMaterialGraphicsApplyService()), new RenderBatchProgressService()))
        {
        }

        public RenderAppearanceService(
            IRenderAppearanceSingleSyncService singleSyncService,
            IRenderAppearanceBatchSyncService batchSyncService)
        {
            _singleSyncService = singleSyncService;
            _batchSyncService = batchSyncService;
        }

        public void SyncWithRenderAppearance(Document doc, Material mat, Action<string>? logCallback = null)
        {
            _singleSyncService.SyncWithRenderAppearance(doc, mat, logCallback);
        }

        public void BatchSyncWithRenderAppearance(Document doc, IEnumerable<Material> materials, Action<string>? logCallback = null, Action<double, string>? progressCallback = null)
        {
            _batchSyncService.BatchSync(doc, materials, logCallback, progressCallback);
        }
    }
}
