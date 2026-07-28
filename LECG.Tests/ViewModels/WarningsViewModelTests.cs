using System.Linq;
using FluentAssertions;
using LECG.Core.Warnings;
using LECG.Services;
using LECG.Services.Logging;
using LECG.ViewModels;
using Xunit;

namespace LECG.Tests.ViewModels;

public class WarningsViewModelTests
{
    private static WarningsViewModel CreateVm() => new(new WarningsService(new Logger()));

    private static WarningItem Item(string description, params long[] ids) =>
        new(description, "Warning", ids);

    [Fact]
    public void Load_PopulatesGroupsOrderedByCount_AndTotalCount()
    {
        var vm = CreateVm();

        vm.Load([Item("Rare", 1), Item("Common", 2), Item("Common", 3)]);

        vm.Groups.Select(g => g.Description).Should().Equal("Common", "Rare");
        vm.TotalCount.Should().Be(3);
        vm.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void Load_WhenEmpty_ReportsNoWarnings()
    {
        var vm = CreateVm();

        vm.Load([]);

        vm.Groups.Should().BeEmpty();
        vm.TotalCount.Should().Be(0);
        vm.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public void Load_ReplacesPreviousGroups()
    {
        var vm = CreateVm();
        vm.Load([Item("Old", 1)]);

        vm.Load([Item("New", 2)]);

        vm.Groups.Select(g => g.Description).Should().Equal("New");
        vm.TotalCount.Should().Be(1);
    }
}
