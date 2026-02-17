using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.Views;
using LECG.Services.Interfaces;
using System.Collections.Generic;



namespace LECG.Services
{
    public class ElementData
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Type { get; set; } = ""; // "Type", "View", "Sheet"
    }

    public class SearchReplaceService : ISearchReplaceService
    {
        private readonly ISearchReplacePreviewService _searchReplacePreviewService;
        private readonly IBatchRenameExecutionService _batchRenameExecutionService;
        private readonly IBaseElementCollectionService _baseElementCollectionService;

        public SearchReplaceService() : this(new SearchReplacePreviewService(new RenameRulePipelineService()), new BatchRenameExecutionService(), new BaseElementCollectionService())
        {
        }

        public SearchReplaceService(ISearchReplacePreviewService searchReplacePreviewService, IBatchRenameExecutionService batchRenameExecutionService, IBaseElementCollectionService baseElementCollectionService)
        {
            _searchReplacePreviewService = searchReplacePreviewService;
            _batchRenameExecutionService = batchRenameExecutionService;
            _baseElementCollectionService = baseElementCollectionService;
        }

        // 1. One-time Fetch of Grid Data
        public List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets)
        {
            return _baseElementCollectionService.CollectBaseElements(doc, types, families, views, sheets);
        }

        public List<string> GetUniqueCategories(List<ElementData> elements)
        {
            return _searchReplacePreviewService.GetUniqueCategories(elements);
        }

        // 2. pure Logic Transformation (Fast, In-Memory)
        public List<ReplaceItem> ProcessPreview(List<ElementData> candidates, SearchReplaceViewModel vm)
        {
            return _searchReplacePreviewService.ProcessPreview(candidates, vm);
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            return _batchRenameExecutionService.ExecuteBatchRename(doc, items, logger, onProgress);
        }
    }
}
