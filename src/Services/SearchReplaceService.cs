using Autodesk.Revit.DB;
using LECG.Models;
using LECG.ViewModels;
using LECG.Views;
using LECG.Services.Interfaces;
using System.Collections.Generic;
using System.Threading;



namespace LECG.Services
{
    public class ElementData
    {
        public long Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Type { get; set; } = ""; // "Type", "View", "Sheet"
        public string OriginalValue { get; set; } = "";

        // Advanced Filter Properties (for Family Parameters)
        public string ParamGroup { get; set; } = "";
        public bool IsInstance { get; set; }
        public bool IsReadOnly { get; set; } // Tracks if it has formula or is read-only
    }

    public class SearchReplaceService : ISearchReplaceService
    {
        private readonly ISearchReplacePreviewService _searchReplacePreviewService;
        private readonly IBatchRenameExecutionService _batchRenameExecutionService;
        private readonly IBaseElementCollectionService _baseElementCollectionService;

        public SearchReplaceService(ISearchReplacePreviewService searchReplacePreviewService, IBatchRenameExecutionService batchRenameExecutionService, IBaseElementCollectionService baseElementCollectionService)
        {
            _searchReplacePreviewService = searchReplacePreviewService;
            _batchRenameExecutionService = batchRenameExecutionService;
            _baseElementCollectionService = baseElementCollectionService;
        }

        // 1. One-time Fetch of Grid Data
        public List<ElementData> CollectBaseElements(Document doc, bool types, bool families, bool views, bool sheets, bool materials, bool objectStyles, bool lineStyles, bool fillPatterns, bool familyParameters)
        {
            return _baseElementCollectionService.CollectBaseElements(doc, types, families, views, sheets, materials, objectStyles, lineStyles, fillPatterns, familyParameters);
        }

        public List<string> GetUniqueCategories(List<ElementData> elements)
        {
            return _searchReplacePreviewService.GetUniqueCategories(elements);
        }

        // 2. pure Logic Transformation (Fast, In-Memory)
        public List<ReplaceItem> ProcessPreview(
            List<ElementData> candidates,
            SearchCriteria criteria,
            RenameRuleContext context,
            CancellationToken ct = default)
        {
            return _searchReplacePreviewService.ProcessPreview(candidates, criteria, context, ct);
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            return _batchRenameExecutionService.ExecuteBatchRename(doc, items, logger, onProgress);
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, IProgressReporter reporter)
        {
            return _batchRenameExecutionService.ExecuteBatchRename(doc, items, logger, reporter);
        }
    }
}
