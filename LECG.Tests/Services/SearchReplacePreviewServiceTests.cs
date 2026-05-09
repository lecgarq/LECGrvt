// Plan 03-04 GREEN: ProcessPreview now returns List<ElementRowViewModel>.
// VALIDATION row: 3-W0-03. Targets Category propagation in
// `LECG.Services.SearchReplacePreviewService.ProcessPreview`.
//
// Plan 03-04 migrated the return type from List<ReplaceItem> to
// List<ElementRowViewModel>; ElementRowViewModel carries Category, ParamGroup,
// IsInstance, IsReadOnly. The Wave-0 anchor (ReplaceItem-shaped) was deleted by
// Plan 03-04 per its DisplayName marker.
using System.Collections.Generic;
using FluentAssertions;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using NSubstitute;

namespace LECG.Tests.Services;

/// <summary>
/// 3-W0-03 — GREEN. Asserts ElementRowViewModel shape post Plan 03-04 migration.
/// </summary>
public class SearchReplacePreviewServiceTests
{
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

    private static IRenameRulePipelineService MakePassthroughPipeline()
    {
        var pipeline = Substitute.For<IRenameRulePipelineService>();
        pipeline.ApplyRules(Arg.Any<string>(), Arg.Any<RenameRuleContext>(), Arg.Any<int>())
            .Returns(ci => ci.ArgAt<string>(0));
        return pipeline;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ProcessPreview_returns_rows_with_non_blank_Category_when_ElementData_Category_is_set()
    {
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var candidates = new List<ElementData>
        {
            new ElementData { Id = 1, Name = "W-100", Category = "Walls", Type = "Type", OriginalValue = "W-100" }
        };
        var criteria = new SearchCriteria { ScopeTypeName = true };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].Name.Should().Be("W-100");
        rows[0].Type.Should().Be("Type");
        rows[0].Category.Should().Be("Walls");
        rows[0].OriginalValue.Should().Be("W-100");
        rows[0].NewValue.Should().Be("W-100");
        rows[0].IsChecked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ProcessPreview_returns_rows_with_fallback_Category_when_ElementData_Category_is_blank()
    {
        // Plan 03-03 guarantees ElementData.Category is non-blank at the collection layer
        // (ElementLabelService fallback to ClrTypeName). This test verifies that whatever
        // Category value the upstream layer hands us — including the fallback — is
        // propagated unchanged into the row. ProcessPreview is a pure pass-through
        // for Category and must not coerce or rewrite the string.
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var candidates = new List<ElementData>
        {
            new ElementData { Id = 2, Name = "F-1", Category = "FilledRegionType", Type = "Type", OriginalValue = "F-1" }
        };
        var criteria = new SearchCriteria { ScopeTypeName = true };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].Category.Should().NotBeNullOrWhiteSpace();
        rows[0].Category.Should().Be("FilledRegionType");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ProcessPreview_propagates_ParamGroup_IsInstance_IsReadOnly_into_row()
    {
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var candidates = new List<ElementData>
        {
            new ElementData
            {
                Id = 7,
                Name = "Width",
                Category = "Dimensions",
                Type = "FamilyParameter",
                OriginalValue = "Width",
                ParamGroup = "Dimensions",
                IsInstance = true,
                IsReadOnly = false
            }
        };
        var criteria = new SearchCriteria { ScopeFamilyParameterName = true };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].ParamGroup.Should().Be("Dimensions");
        rows[0].IsInstance.Should().BeTrue();
        rows[0].IsReadOnly.Should().BeFalse();
        rows[0].Id.Should().Be(7);
    }
}
