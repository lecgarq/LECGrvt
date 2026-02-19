using System;
using System.Collections.Generic;
using System.Linq;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.Views;

namespace LECG.Services
{
    public class SearchReplacePreviewService : ISearchReplacePreviewService
    {
        private readonly IRenameRulePipelineService _renameRulePipelineService;

        public SearchReplacePreviewService(IRenameRulePipelineService renameRulePipelineService)
        {
            _renameRulePipelineService = renameRulePipelineService;
        }

        public List<string> GetUniqueCategories(List<ElementData> elements)
        {
            return elements.Select(x => x.Category).Distinct().OrderBy(x => x).ToList();
        }

        public List<ReplaceItem> ProcessPreview(List<ElementData> candidates, SearchReplaceViewModel vm)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(vm);

            List<ReplaceItem> results = new List<ReplaceItem>();

            foreach (ElementData el in candidates)
            {
                if (el.Type == "Type" && !vm.ScopeTypeName) continue;
                if (el.Type == "View" && !vm.ScopeViewName) continue;
                if (el.Type == "Sheet" && !vm.ScopeSheetName) continue;

                if (el.Type == "Family" && !vm.ScopeFamilyName) continue;
                if (el.Type == "Material" && !vm.ScopeMaterialName) continue;
                if (el.Type == "ObjectStyle" && !vm.ScopeObjectStyleName) continue;
                if (el.Type == "LineStyle" && !vm.ScopeLineStyleName) continue;
                if (el.Type == "FillPattern" && !vm.ScopeFillPatternName) continue;
                if (el.Type == "FamilyParameter" && !vm.ScopeFamilyParameterName) continue;

                if (!string.IsNullOrWhiteSpace(vm.FilterName))
                {
                    bool match = false;
                    switch (vm.SelectedFilterType)
                    {
                        case SearchFilterType.Contains:
                            match = el.Name.IndexOf(vm.FilterName, StringComparison.OrdinalIgnoreCase) >= 0;
                            break;
                        case SearchFilterType.BeginsWith:
                            match = el.Name.StartsWith(vm.FilterName, StringComparison.OrdinalIgnoreCase);
                            break;
                        case SearchFilterType.EndsWith:
                            match = el.Name.EndsWith(vm.FilterName, StringComparison.OrdinalIgnoreCase);
                            break;
                        case SearchFilterType.DoesNotContain:
                            match = el.Name.IndexOf(vm.FilterName, StringComparison.OrdinalIgnoreCase) < 0;
                            break;
                    }
                    if (!match) continue;
                }

                if (!string.IsNullOrWhiteSpace(vm.FilterCategory) && !vm.FilterCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (el.Category.IndexOf(vm.FilterCategory, StringComparison.OrdinalIgnoreCase) < 0) continue;
                }

                string currentName = _renameRulePipelineService.ApplyRules(el.Name, vm, results.Count);

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
    }
}
