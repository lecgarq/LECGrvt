using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LECG.Models;
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

        public List<ReplaceItem> ProcessPreview(
            List<ElementData> candidates, 
            SearchCriteria criteria, 
            RenameRuleContext context,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentNullException.ThrowIfNull(context);

            List<ReplaceItem> results = new List<ReplaceItem>();

            foreach (ElementData el in candidates)
            {
                // Support cooperative cancellation
                ct.ThrowIfCancellationRequested();

                // 1. Scope Filtering
                if (el.Type == "Type" && !criteria.ScopeTypeName) continue;
                if (el.Type == "View" && !criteria.ScopeViewName) continue;
                if (el.Type == "Sheet" && !criteria.ScopeSheetName) continue;
                if (el.Type == "Family" && !criteria.ScopeFamilyName) continue;
                if (el.Type == "Material" && !criteria.ScopeMaterialName) continue;
                if (el.Type == "ObjectStyle" && !criteria.ScopeObjectStyleName) continue;
                if (el.Type == "LineStyle" && !criteria.ScopeLineStyleName) continue;
                if (el.Type == "FillPattern" && !criteria.ScopeFillPatternName) continue;
                if (el.Type == "FamilyParameter" && !criteria.ScopeFamilyParameterName) continue;

                // 2. Name Filtering
                if (!string.IsNullOrWhiteSpace(criteria.FilterName))
                {
                    bool match = false;
                    switch (criteria.SelectedFilterType)
                    {
                        case SearchFilterType.Contains:
                            match = el.Name.Contains(criteria.FilterName, StringComparison.OrdinalIgnoreCase);
                            break;
                        case SearchFilterType.BeginsWith:
                            match = el.Name.StartsWith(criteria.FilterName, StringComparison.OrdinalIgnoreCase);
                            break;
                        case SearchFilterType.EndsWith:
                            match = el.Name.EndsWith(criteria.FilterName, StringComparison.OrdinalIgnoreCase);
                            break;
                        case SearchFilterType.DoesNotContain:
                            match = !el.Name.Contains(criteria.FilterName, StringComparison.OrdinalIgnoreCase);
                            break;
                    }
                    if (!match) continue;
                }

                // 3. Category Filtering
                if (!string.IsNullOrWhiteSpace(criteria.FilterCategory) && !criteria.FilterCategory.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (!el.Category.Contains(criteria.FilterCategory, StringComparison.OrdinalIgnoreCase)) continue;
                }

                // 4. Advanced Filtering — Parameters
                if (el.Type == "FamilyParameter")
                {
                    if (!string.IsNullOrEmpty(criteria.FilterParamGroup) && !criteria.FilterParamGroup.Equals("All", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.Equals(el.ParamGroup, criteria.FilterParamGroup, StringComparison.OrdinalIgnoreCase)) continue;
                    }

                    if (criteria.FilterIsInstance.HasValue)
                    {
                        if (el.IsInstance != criteria.FilterIsInstance.Value) continue;
                    }

                    if (criteria.FilterIsReadOnly.HasValue)
                    {
                        if (el.IsReadOnly != criteria.FilterIsReadOnly.Value) continue;
                    }
                }

                // 5. Advanced Filtering — Views
                if (el.Type == "View" && !string.IsNullOrEmpty(criteria.FilterViewType) && !criteria.FilterViewType.Equals("All", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(el.Category, criteria.FilterViewType, StringComparison.OrdinalIgnoreCase)) continue;
                }

                // 6. Apply Rename Rules
                string currentName = _renameRulePipelineService.ApplyRules(el.Name, context, results.Count);

                results.Add(new ReplaceItem
                {
                    ElementId = el.Id,
                    ElementName = el.Name,
                    OriginalValue = el.Name,
                    NewValue = currentName,
                    IsChecked = true,
                    Type = el.Type
                });
            }

            return results;
        }
    }
}
