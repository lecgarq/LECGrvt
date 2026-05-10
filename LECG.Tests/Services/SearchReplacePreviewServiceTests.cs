// Plan 03-04 GREEN: ProcessPreview now returns List<ElementRowViewModel>.
// Plan 04-02 GREEN: ProcessPreview populates Status/IsRenameable/IsChecked for
//   FamilyParameter skip conditions, side-effect counts, and cross-batch collisions.
// VALIDATION row: 3-W0-03. Targets Category propagation in
// `LECG.Services.SearchReplacePreviewService.ProcessPreview`.
//
// Plan 03-04 migrated the return type from List<ReplaceItem> to
// List<ElementRowViewModel>; ElementRowViewModel carries Category, ParamGroup,
// IsInstance, IsReadOnly. The Wave-0 anchor (ReplaceItem-shaped) was deleted by
// Plan 03-04 per its DisplayName marker.
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using LECG.Models;
using LECG.Services;
using LECG.Services.Interfaces;
using LECG.ViewModels;
using NSubstitute;

namespace LECG.Tests.Services;

/// <summary>
/// 3-W0-03 — GREEN. Asserts ElementRowViewModel shape post Plan 03-04 migration.
/// Plan 04-02 — GREEN. IsRenameable / Status / cross-batch collision propagation.
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

    // -----------------------------------------------------------------------
    // REQ-04 IsRenameable propagation — plan 04-02 (un-skipped + implemented)
    // -----------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Renaming")]
    public void ProcessPreview_FamilyParameterSkipReason_PopulatesStatus_WithReasonString()
    {
        // A read-only FamilyParameter (e.g. reporting/formula-driven) should surface
        // a non-empty Status explaining why it is skipped.
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var criteria = new SearchCriteria { ScopeFamilyParameterName = true };
        var candidates = new List<ElementData>
        {
            new ElementData
            {
                Id = 10,
                Name = "Height",
                Category = "MyFamily",
                Type = "FamilyParameter",
                OriginalValue = "Height",
                IsReadOnly = true
            }
        };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].Status.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Renaming")]
    public void ProcessPreview_FamilyParameterSkipReason_SetsIsRenameableFalse_AndIsCheckedFalse()
    {
        // A read-only FamilyParameter must be marked non-renameable and unchecked.
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var criteria = new SearchCriteria { ScopeFamilyParameterName = true };
        var candidates = new List<ElementData>
        {
            new ElementData
            {
                Id = 11,
                Name = "Depth",
                Category = "MyFamily",
                Type = "FamilyParameter",
                OriginalValue = "Depth",
                IsReadOnly = true
            }
        };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].IsRenameable.Should().BeFalse();
        rows[0].IsChecked.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Renaming")]
    public void ProcessPreview_SafeRenameRow_PopulatesStatus_WithSideEffectCount()
    {
        // A safe-rename FamilyParameter that is referenced in other formulas and is a
        // dimension label should have its Status set to the side-effect count string.
        // Setup: "Width" is referenced in "Area" and "Volume" formulas; Width is also a dimension label.
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var criteria = new SearchCriteria { ScopeFamilyParameterName = true };
        var candidates = new List<ElementData>
        {
            // The target parameter — not read-only, is a dimension label
            new ElementData
            {
                Id = 20,
                Name = "Width",
                Category = "MyFamily",
                Type = "FamilyParameter",
                OriginalValue = "Width",
                IsReadOnly = false,
                IsDimensionLabel = true,
                Formula = ""
            },
            // Another param whose formula references Width (formulaCount contributor)
            new ElementData
            {
                Id = 20,
                Name = "Area",
                Category = "MyFamily",
                Type = "FamilyParameter",
                OriginalValue = "Area",
                IsReadOnly = false,
                IsDimensionLabel = false,
                Formula = "Width * Depth"
            },
            // Another param whose formula also references Width
            new ElementData
            {
                Id = 20,
                Name = "Volume",
                Category = "MyFamily",
                Type = "FamilyParameter",
                OriginalValue = "Volume",
                IsReadOnly = false,
                IsDimensionLabel = false,
                Formula = "Width * Height * Depth"
            }
        };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        var widthRow = rows.First(r => r.OriginalValue == "Width");
        // Width has 2 formula references + 1 dimension label -> "+2 formulas, +1 dimension"
        widthRow.Status.Should().Be("+2 formulas, +1 dimension");
        widthRow.IsRenameable.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Renaming")]
    public void ProcessPreview_RenameableRow_LeavesIsRenameableTrue_AndKeepsUserCheckedState()
    {
        // A plain renameable FamilyParameter (no skip conditions, no side effects)
        // should have IsRenameable=true, Status="", and IsChecked untouched (default true).
        var sut = new SearchReplacePreviewService(MakePassthroughPipeline());
        var criteria = new SearchCriteria { ScopeFamilyParameterName = true };
        var candidates = new List<ElementData>
        {
            new ElementData
            {
                Id = 30,
                Name = "Label",
                Category = "MyFamily",
                Type = "FamilyParameter",
                OriginalValue = "Label",
                IsReadOnly = false,
                IsDimensionLabel = false,
                Formula = ""
            }
        };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(1);
        rows[0].IsRenameable.Should().BeTrue();
        rows[0].Status.Should().Be("");
        rows[0].IsChecked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Renaming")]
    public void ProcessPreview_CrossBatchNameCollision_FlipsSecondRowToSkip_WithReason()
    {
        // If two rows in the same batch would produce the same NewValue, the SECOND
        // occurrence must be flipped to skip with a collision reason message.
        // The pipeline renames "Param_A" -> "NewName" and "Param_B" -> "NewName".
        var pipeline = Substitute.For<IRenameRulePipelineService>();
        pipeline.ApplyRules("Param_A", Arg.Any<RenameRuleContext>(), Arg.Any<int>()).Returns("NewName");
        pipeline.ApplyRules("Param_B", Arg.Any<RenameRuleContext>(), Arg.Any<int>()).Returns("NewName");

        var sut = new SearchReplacePreviewService(pipeline);
        var criteria = new SearchCriteria { ScopeFamilyParameterName = true };
        var candidates = new List<ElementData>
        {
            new ElementData { Id = 40, Name = "Param_A", Category = "MyFamily", Type = "FamilyParameter", OriginalValue = "Param_A", IsReadOnly = false },
            new ElementData { Id = 40, Name = "Param_B", Category = "MyFamily", Type = "FamilyParameter", OriginalValue = "Param_B", IsReadOnly = false }
        };

        var rows = sut.ProcessPreview(candidates, criteria, MakeContext(criteria));

        rows.Should().HaveCount(2);
        // First occurrence keeps its NewValue intact
        rows[0].IsRenameable.Should().BeTrue();
        rows[0].IsChecked.Should().BeTrue();
        // Second occurrence is flipped to skip with a message containing the NewValue
        rows[1].IsRenameable.Should().BeFalse();
        rows[1].IsChecked.Should().BeFalse();
        rows[1].Status.Should().Contain("NewName");
    }
}
