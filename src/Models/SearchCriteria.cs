using LECG.ViewModels;

namespace LECG.Models
{
    /// <summary>
    /// Pure DTO for filtering criteria, decoupled from ViewModels and Revit API.
    /// </summary>
    public class SearchCriteria
    {
        public string FilterName { get; set; } = string.Empty;
        public string FilterCategory { get; set; } = "All";
        public SearchFilterType SelectedFilterType { get; set; } = SearchFilterType.Contains;

        // Advanced Filters (Parameters)
        public string FilterParamGroup { get; set; } = "All";
        public bool? FilterIsInstance { get; set; }
        public bool? FilterIsReadOnly { get; set; }

        // Advanced Filters (Views)
        public string FilterViewType { get; set; } = "All";

        // Scope Flags (Determines which ElementData to process)
        public bool ScopeTypeName { get; set; }
        public bool ScopeFamilyName { get; set; }
        public bool ScopeViewName { get; set; }
        public bool ScopeSheetName { get; set; }
        public bool ScopeMaterialName { get; set; }
        public bool ScopeObjectStyleName { get; set; }
        public bool ScopeLineStyleName { get; set; }
        public bool ScopeFillPatternName { get; set; }
        public bool ScopeFamilyParameterName { get; set; }
    }
}
