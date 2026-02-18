using Autodesk.Revit.DB;
using LECG.Services.Interfaces;

namespace LECG.Services
{
    public class RenderMaterialSyncExecutionService : IRenderMaterialSyncExecutionService
    {
        private readonly IRenderMaterialSyncCheckService _syncCheckService;
        private readonly IRenderMaterialGraphicsApplyService _graphicsApplyService;

        public RenderMaterialSyncExecutionService(IRenderMaterialSyncCheckService syncCheckService, IRenderMaterialGraphicsApplyService graphicsApplyService)
        {
            _syncCheckService = syncCheckService;
            _graphicsApplyService = graphicsApplyService;
        }

        public bool TrySync(Material material, ElementId solidFillPatternId)
        {
            Color renderColor = material.Color;
            if (_syncCheckService.IsMaterialSynced(material, renderColor, solidFillPatternId))
            {
                return false;
            }

            _graphicsApplyService.Apply(material, renderColor, solidFillPatternId);
            return true;
        }
    }
}
