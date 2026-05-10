using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LECG.Core.Rename;
using LECG.Models;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using LECG.ViewModels.Components;
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

        public List<ElementRowViewModel> ProcessPreview(
            List<ElementData> candidates,
            SearchCriteria criteria,
            RenameRuleContext context,
            CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(candidates);
            ArgumentNullException.ThrowIfNull(criteria);
            ArgumentNullException.ThrowIfNull(context);

            List<ElementRowViewModel> results = new List<ElementRowViewModel>();

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

                // 3. Category Filtering — moved to SearchReplaceViewModel.PreviewView
                //    (ICollectionView.Filter) per Plan 03-05 / RESEARCH §Open Question 3.
                //    Post-collection filtering avoids re-running ProcessPreview on every
                //    FilterCategory keystroke and lets the dropdown predicate AND-combine
                //    with per-column filters.

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

                ElementRowViewModel row = new ElementRowViewModel
                {
                    Id = el.Id,
                    Name = el.Name,
                    Category = el.Category,
                    OriginalValue = el.Name,
                    NewValue = currentName,
                    IsChecked = true,
                    Type = el.Type,
                    ParamGroup = el.ParamGroup,
                    IsInstance = el.IsInstance,
                    IsReadOnly = el.IsReadOnly
                };

                // 7. FamilyParameter skip-reason and side-effect population
                if (el.Type == "FamilyParameter")
                {
                    PopulateFamilyParameterStatus(row, el, candidates);
                }

                results.Add(row);
            }

            // 8. Cross-batch name-collision pass (per-family-scope)
            ApplyCrossBatchCollisionCheck(results);

            return results;
        }

        /// <summary>
        /// For a FamilyParameter row, populate Status + IsRenameable + IsChecked based on:
        /// - Skip conditions (read-only/reporting)
        /// - Side-effect counts (formula references, dimension labels)
        /// </summary>
        private static void PopulateFamilyParameterStatus(
            ElementRowViewModel row,
            ElementData el,
            List<ElementData> allCandidates)
        {
            // Check remaining skip conditions (built-in params are filtered at collection;
            // read-only covers reporting parameters and formula-driven params)
            if (el.IsReadOnly)
            {
                row.Status = "read-only parameter (cannot rename)";
                row.IsRenameable = false;
                row.IsChecked = false;
                return;
            }

            // Safe-rename path: compute side-effect counts
            int formulaCount = 0;
            int dimensionCount = el.IsDimensionLabel ? 1 : 0;

            // Count parameters in the same family whose formula references this parameter's name
            foreach (ElementData other in allCandidates)
            {
                if (other == el) continue;
                if (other.Type != "FamilyParameter") continue;
                if (other.Id != el.Id) continue; // same family scope only
                if (string.IsNullOrEmpty(other.Formula)) continue;
                if (FormulaNameUpdater.ContainsReference(other.Formula, el.OriginalValue))
                    formulaCount++;
            }

            // Format side-effect Status string
            row.Status = FormatSideEffectStatus(formulaCount, dimensionCount);
            row.IsRenameable = true;
            // IsChecked stays at its default (true) — user may have changed it; don't override
        }

        /// <summary>
        /// Format the side-effect count string per the plan's interface specification.
        /// Returns empty string when both counts are zero.
        /// </summary>
        private static string FormatSideEffectStatus(int formulaCount, int dimensionCount)
        {
            if (formulaCount > 0 && dimensionCount > 0)
                return $"+{formulaCount} formula{(formulaCount == 1 ? "" : "s")}, +{dimensionCount} dimension{(dimensionCount == 1 ? "" : "s")}";
            if (formulaCount > 0)
                return $"+{formulaCount} formula{(formulaCount == 1 ? "" : "s")}";
            if (dimensionCount > 0)
                return $"+{dimensionCount} dimension{(dimensionCount == 1 ? "" : "s")}";
            return "";
        }

        /// <summary>
        /// Walk all rows and detect cross-batch name collisions within each family scope.
        /// The FIRST row claiming a NewValue is kept; subsequent rows with the same NewValue
        /// in the same family scope are flipped to skip.
        /// Uses ordinal StringComparer per plan spec.
        /// </summary>
        private static void ApplyCrossBatchCollisionCheck(List<ElementRowViewModel> rows)
        {
            // Group by family scope: for FamilyParameter rows, scope = (Type="FamilyParameter", Id)
            // For standard items, scope = (Type) — collisions within the same element type scope
            // Only checked rows participate in the claimed-name set
            var claimedByScope = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (ElementRowViewModel row in rows)
            {
                if (!row.IsRenameable) continue; // already skipped rows don't claim names
                if (!row.IsChecked) continue;     // unchecked rows don't claim names

                string scopeKey = row.Type == "FamilyParameter"
                    ? $"FamilyParameter:{row.Id}"
                    : row.Type;

                if (!claimedByScope.TryGetValue(scopeKey, out HashSet<string>? claimed))
                {
                    claimed = new HashSet<string>(StringComparer.Ordinal);
                    claimedByScope[scopeKey] = claimed;
                }

                if (!claimed.Add(row.NewValue))
                {
                    // Collision: this NewValue was already claimed by an earlier row
                    row.Status = $"name '{row.NewValue}' already claimed by another row in this batch";
                    row.IsRenameable = false;
                    row.IsChecked = false;
                }
            }
        }
    }
}
