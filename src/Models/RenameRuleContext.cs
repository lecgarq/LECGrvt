using LECG.Services;
using LECG.ViewModels;

namespace LECG.Models
{
    public sealed record RenameRuleContext(
        ReplaceRule ReplaceRule,
        RemoveRule RemoveRule,
        AddRule AddRule,
        NumberingRule NumberingRule,
        CaseRule CaseRule,
        bool ScopeTypeName,
        bool ScopeFamilyName,
        bool ScopeViewName,
        bool ScopeSheetName,
        bool ScopeMaterialName,
        bool ScopeObjectStyleName,
        bool ScopeLineStyleName,
        bool ScopeFillPatternName,
        bool ScopeFamilyParameterName,
        string FilterName,
        string FilterCategory,
        SearchFilterType SelectedFilterType,
        string FilterParamGroup,
        bool? FilterIsInstance,
        bool? FilterIsReadOnly,
        string FilterViewType);
}
