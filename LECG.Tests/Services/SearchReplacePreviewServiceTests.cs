// Wave 0 RED scaffold for Phase 03 (REQ-01).
// VALIDATION row: 3-W0-03. Targets Category propagation in
// `LECG.Services.SearchReplacePreviewService.ProcessPreview` (Plan 03-04).
//
// Today: ProcessPreview returns List<ReplaceItem> which has no Category field.
// Plan 03-04 migrates the return type to List<ElementRowViewModel> which carries
// Category. The new-shape assertions are Skip-gated until 03-04 lands.
//
// One non-skipped anchor test asserts today's ReplaceItem-shaped behavior so the
// fixture compiles and exercises the construction path. The anchor is marked for
// deletion in Plan 03-04.
using System.Collections.Generic;
using FluentAssertions;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using NSubstitute;

namespace LECG.Tests.Services;

/// <summary>
/// 3-W0-03 — RED scaffold. New-shape assertions await Plan 03-04 ElementRowViewModel migration.
/// </summary>
public class SearchReplacePreviewServiceTests
{
    private const string SkipReason = "Awaiting Plan 03-04 — ProcessPreview returns ReplaceItem today";

    private static RenameRuleContext MakeContext(SearchCriteria criteria) => new RenameRuleContext(
        new ReplaceRule(), new RemoveRule(), new AddRule(), new NumberingRule(), new CaseRule(),
        ScopeTypeName: criteria.ScopeTypeName,
        ScopeFamilyName: criteria.ScopeFamilyName,
        ScopeViewName: criteria.ScopeViewName,
        ScopeSheetName: criteria.ScopeSheetName,
        ScopeMaterialName: criteria.ScopeMaterialName,
        ScopeObjectStyleName: criteria.ScopeObjectStyleName,
        ScopeLineStyleName: criteria.ScopeLineStyleName,
        ScopeFillPatternName: criteria.ScopeFillPatternName,
        ScopeFamilyParameterName: criteria.ScopeFamilyParameterName,
        FilterName: criteria.FilterName,
        FilterCategory: criteria.FilterCategory,
        SelectedFilterType: criteria.SelectedFilterType,
        FilterParamGroup: criteria.FilterParamGroup,
        FilterIsInstance: criteria.FilterIsInstance,
        FilterIsReadOnly: criteria.FilterIsReadOnly,
        FilterViewType: criteria.FilterViewType);

    [Fact(DisplayName = "anchor — delete in 03-04")]
    [Trait("Category", "Unit")]
    public void Anchor_ProcessPreview_returns_ReplaceItem_for_typed_element()
    {
        // Today's behavior: ProcessPreview returns List<ReplaceItem>. This anchor proves
        // the construction path works (NSubstitute pipeline + criteria + ElementData).
        // Plan 03-04 deletes this test when migrating the return type.
        var pipeline = Substitute.For<IRenameRulePipelineService>();
        pipeline.ApplyRules(Arg.Any<string>(), Arg.Any<RenameRuleContext>(), Arg.Any<int>())
            .Returns(ci => ci.ArgAt<string>(0));

        var sut = new SearchReplacePreviewService(pipeline);
        var candidates = new List<ElementData>
        {
            new ElementData { Id = 1, Name = "W-100", Category = "Walls", Type = "Type", OriginalValue = "W-100" }
        };
        var criteria = new SearchCriteria { ScopeTypeName = true };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].ElementName.Should().Be("W-100");
        rows[0].Type.Should().Be("Type");
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void ProcessPreview_returns_rows_with_non_blank_Category_when_ElementData_Category_is_set()
    {
        // Plan 03-04: rows[0].Category should equal "Walls" — ElementRowViewModel carries Category.
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void ProcessPreview_returns_rows_with_fallback_Category_when_ElementData_Category_is_blank()
    {
        // Plan 03-04 + Plan 03-03 fallback: when ElementData.Category is "" the row's Category
        // must remain non-empty (ElementLabelService fallback to ClrTypeName).
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void ProcessPreview_propagates_ParamGroup_IsInstance_IsReadOnly_into_row()
    {
        // Plan 03-04: ElementRowViewModel must carry ParamGroup, IsInstance, IsReadOnly
        // through from ElementData unchanged.
    }
}
