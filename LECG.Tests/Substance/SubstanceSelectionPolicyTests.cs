using FluentAssertions;
using LECG.Core.Substance;
using Xunit;

namespace LECG.Tests.Substance;

public class SubstanceSelectionPolicyTests
{
    private static readonly SubstanceRowState[] Rows =
    {
        new("Asphalt", "a1", "A One", true),
        new("Asphalt", "a2", "A Two", false),
        new("Wood", "w1", "Walnut", true),
        new("Wood", "w2", "Oak", true),
    };

    [Fact]
    public void CategoryState_Mixed_Null_All_True_None_False()
    {
        SubstanceSelectionPolicy.CategoryState(Rows, "Asphalt").Should().BeNull();
        SubstanceSelectionPolicy.CategoryState(Rows, "Wood").Should().BeTrue();
        SubstanceSelectionPolicy.CategoryState(Rows, "Glass").Should().BeFalse();
    }

    [Fact]
    public void SetCategory_OnlyTouchesThatCategory()
    {
        var r = SubstanceSelectionPolicy.SetCategory(Rows, "Asphalt", true);
        r.Where(x => x.Category == "Asphalt").Should().OnlyContain(x => x.IsSelected);
        r.Single(x => x.Slug == "w1").IsSelected.Should().BeTrue();
        var r2 = SubstanceSelectionPolicy.SetCategory(Rows, "Wood", false);
        r2.Where(x => x.Category == "Wood").Should().OnlyContain(x => !x.IsSelected);
        r2.Single(x => x.Slug == "a1").IsSelected.Should().BeTrue();
    }

    [Fact]
    public void SetAll_AppliesEverywhere()
    {
        SubstanceSelectionPolicy.SetAll(Rows, false).Should().OnlyContain(x => !x.IsSelected);
        SubstanceSelectionPolicy.SetAll(Rows, true).Should().OnlyContain(x => x.IsSelected);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("  ", true)]
    [InlineData("wal", true)]
    [InlineData("WOOD", true)]
    [InlineData("oak", false)]
    public void Matches_FilterOnNameOrCategory(string? filter, bool expected)
    {
        SubstanceSelectionPolicy.Matches(Rows[2], filter).Should().Be(expected);
    }

    [Fact]
    public void CategoryCounts_OrderedWithCounts()
    {
        SubstanceSelectionPolicy.CategoryCounts(Rows).Should().Equal(("Asphalt", 2), ("Wood", 2));
    }
}
