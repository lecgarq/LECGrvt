using Autodesk.Revit.DB;
using LECG.ViewModels;
using LECG.Configuration;
using LECG.Views;
using LECG.Services.Interfaces;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;



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
        private readonly IRenameRulePipelineService _renameRulePipelineService;
        private readonly IBatchRenameExecutionService _batchRenameExecutionService;
        private readonly IBaseElementCollectionService _baseElementCollectionService;

        public SearchReplaceService() : this(new RenameRulePipelineService(), new BatchRenameExecutionService(), new BaseElementCollectionService())
        {
        }

        public SearchReplaceService(IRenameRulePipelineService renameRulePipelineService, IBatchRenameExecutionService batchRenameExecutionService, IBaseElementCollectionService baseElementCollectionService)
        {
            _renameRulePipelineService = renameRulePipelineService;
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
            return elements.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
        }

        // 2. pure Logic Transformation (Fast, In-Memory)
        public List<ReplaceItem> ProcessPreview(List<ElementData> candidates, SearchReplaceViewModel vm)
        {
            List<ReplaceItem> results = new List<ReplaceItem>();

            foreach (var el in candidates)
            {
                // Filter by Scope (redundant if candidates pre-filtered, but safe)
                if (el.Type == "Type" && !vm.ScopeTypeName) continue;
                if (el.Type == "View" && !vm.ScopeViewName) continue;
                if (el.Type == "Sheet" && !vm.ScopeSheetName) continue;

                // Filter by Name
                if (!string.IsNullOrWhiteSpace(vm.FilterName))
                {
                    if (el.Name.IndexOf(vm.FilterName, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                // Filter by Category (Exact Match from ComboBox usually, or Contains)
                if (!string.IsNullOrWhiteSpace(vm.FilterCategory) && !vm.FilterCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    // If user selects specific category, we match exact or contains
                    // Assuming partial match for flexibility
                    if (el.Category.IndexOf(vm.FilterCategory, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                // Transformation
                string currentName = _renameRulePipelineService.ApplyRules(el.Name, vm, results.Count); 
                
                // Add to results
                bool changed = !string.Equals(currentName, el.Name, StringComparison.Ordinal);
                
                results.Add(new ReplaceItem
                {
                    ElementId = el.Id,
                    ElementName = el.Name,
                    OriginalValue = el.Name,
                    NewValue = currentName,
                    IsChecked = true
                });
            }
            
            return results;
        }

        public int ExecuteBatchRename(Document doc, List<ReplaceItem> items, Services.Logging.ILogger logger, Action<double, string>? onProgress = null)
        {
            return _batchRenameExecutionService.ExecuteBatchRename(doc, items, logger, onProgress);
        }
    }
}
