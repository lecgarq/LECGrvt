using System.ComponentModel;
using FluentAssertions;
using LECG.ViewModels;
using LECG.ViewModels.Components;
using Xunit;

namespace LECG.Tests.ViewModels;

public class ViewModelBasicsTests
{
    [Fact]
    public void BaseViewModel_DefaultTitle()
    {
        var vm = new BaseViewModel();
        vm.Title.Should().Be("LECG Tool");
    }

    [Fact]
    public void BaseViewModel_RaisesPropertyChangedForTitle()
    {
        var vm = new BaseViewModel();
        ShouldRaise(vm, nameof(BaseViewModel.Title), () => vm.Title = "Changed");
    }

    [Fact]
    public void SelectionViewModel_DefaultSelectionStatus()
    {
        var vm = new SelectionViewModel();
        vm.SelectionStatus.Should().Be("No items selected");
    }

    [Fact]
    public void SelectionViewModel_RaisesPropertyChangedForSelectionCount()
    {
        var vm = new SelectionViewModel();
        ShouldRaise(vm, nameof(SelectionViewModel.SelectionCount), () => vm.SelectionCount = 2);
    }

    [Fact]
    public void SexyRevitViewModel_DefaultFlags()
    {
        var vm = new SexyRevitViewModel();
        vm.UseConsistentColors.Should().BeTrue();
        vm.UseDetailFine.Should().BeTrue();
    }

    [Fact]
    public void SexyRevitViewModel_RaisesPropertyChangedForUseConsistentColors()
    {
        var vm = new SexyRevitViewModel();
        ShouldRaise(vm, nameof(SexyRevitViewModel.UseConsistentColors), () => vm.UseConsistentColors = false);
    }

    [Fact]
    public void PurgeViewModel_DefaultFlags()
    {
        var vm = new PurgeViewModel();
        vm.PurgeLineStyles.Should().BeTrue();
        vm.PurgeLevels.Should().BeFalse();
    }

    [Fact]
    public void PurgeViewModel_RaisesPropertyChangedForPurgeLineStyles()
    {
        var vm = new PurgeViewModel();
        ShouldRaise(vm, nameof(PurgeViewModel.PurgeLineStyles), () => vm.PurgeLineStyles = false);
    }

    [Fact]
    public void RenderAppearanceViewModel_DefaultState()
    {
        var vm = new RenderAppearanceViewModel();
        vm.Title.Should().Be("SYNC RENDER APPEARANCE");
        vm.ShouldRun.Should().BeFalse();
    }

    [Fact]
    public void RenderAppearanceViewModel_RaisesPropertyChangedForTitle()
    {
        var vm = new RenderAppearanceViewModel();
        ShouldRaise(vm, nameof(RenderAppearanceViewModel.Title), () => vm.Title = "Updated");
    }

    [Fact]
    public void UpdateContoursViewModel_DefaultIntervals()
    {
        var vm = new UpdateContoursViewModel();
        vm.PrimaryInterval.Should().Be(1.0);
        vm.SecondaryInterval.Should().Be(0.25);
    }

    [Fact]
    public void UpdateContoursViewModel_RaisesPropertyChangedForPrimaryInterval()
    {
        var vm = new UpdateContoursViewModel();
        ShouldRaise(vm, nameof(UpdateContoursViewModel.PrimaryInterval), () => vm.PrimaryInterval = 2.0);
    }

    private static void ShouldRaise(INotifyPropertyChanged vm, string propertyName, Action change)
    {
        var raised = false;
        vm.PropertyChanged += (_, args) => raised |= args.PropertyName == propertyName;
        change();
        raised.Should().BeTrue();
    }
}
