using System.Linq;
using FluentAssertions;
using LECG.Core.Warnings;
using Xunit;

namespace LECG.Tests.Services;

public class WarningGroupingPolicyTests
{
    private static WarningItem Item(string description, params long[] ids) =>
        new(description, "Warning", ids);

    [Fact]
    public void Group_WhenEmpty_ReturnsEmpty()
    {
        WarningGroupingPolicy.Group([]).Should().BeEmpty();
    }

    [Fact]
    public void Group_GroupsByDescription_WithCounts()
    {
        var groups = WarningGroupingPolicy.Group(
        [
            Item("Overlap", 1),
            Item("Overlap", 2),
            Item("Duplicate", 3),
        ]);

        groups.Should().HaveCount(2);
        groups.Single(g => g.Description == "Overlap").Count.Should().Be(2);
        groups.Single(g => g.Description == "Duplicate").Count.Should().Be(1);
    }

    [Fact]
    public void Group_OrdersByCountDescending_ThenDescriptionAscending()
    {
        var groups = WarningGroupingPolicy.Group(
        [
            Item("B rare", 1),
            Item("A rare", 2),
            Item("Common", 3),
            Item("Common", 4),
        ]);

        groups.Select(g => g.Description).Should().Equal("Common", "A rare", "B rare");
    }

    [Fact]
    public void Group_CollectsDistinctElementIdsPerGroup()
    {
        var groups = WarningGroupingPolicy.Group(
        [
            Item("Overlap", 1, 2),
            Item("Overlap", 2, 3),
        ]);

        groups.Single().ElementIds.Should().BeEquivalentTo([1L, 2L, 3L]);
    }

    [Fact]
    public void Group_KeepsSeverityOfFirstItem()
    {
        var groups = WarningGroupingPolicy.Group([new WarningItem("Overlap", "Error", [1])]);
        groups.Single().Severity.Should().Be("Error");
    }
}
