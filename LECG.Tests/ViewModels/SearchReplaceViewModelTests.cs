// Wave 0 RED scaffold for Phase 03 (REQ-01).
// VALIDATION row: 3-W0-04. Targets ICollectionView wiring in
// `LECG.ViewModels.SearchReplaceViewModel` (Plan 03-05).
//
// Today: PreviewItems is a plain ObservableCollection<ReplaceItem> with no
// CollectionViewSource sort/filter wiring. Plan 03-05 introduces:
//   - default Category-ascending SortDescription on the default ICollectionView
//   - AND-combined filter that intersects FilterCategory and per-column filters
//
// All assertions are Skip-gated until Plan 03-05 lands. One anchor test forces
// the type reference + parameterless constructor to keep compiling.
using FluentAssertions;
using LECG.ViewModels;

namespace LECG.Tests.ViewModels;

/// <summary>
/// 3-W0-04 — RED scaffold. Sort/filter assertions await Plan 03-05 ICollectionView wiring.
/// </summary>
public class SearchReplaceViewModelTests
{
    private const string SkipReason = "Awaiting Plan 03-05 — ICollectionView sort/filter wiring";

    [Fact]
    [Trait("Category", "Unit")]
    public void ViewModel_constructs_with_default_state()
    {
        // Anchor: parameterless constructor must keep working through Plan 03-05's refactor.
        var sut = new SearchReplaceViewModel();

        sut.Should().NotBeNull();
        sut.PreviewItems.Should().NotBeNull();
        sut.PreviewItems.Should().BeEmpty();
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void PreviewItems_default_sort_is_Category_ascending()
    {
        // Plan 03-05 contract: after the VM is constructed, the default ICollectionView
        // for PreviewItems must carry SortDescriptions[0] = (PropertyName="Category",
        // Direction=Ascending).
        //
        // Implementation hint: System.Windows.Data.CollectionViewSource.GetDefaultView(sut.PreviewItems)
        //   .SortDescriptions[0].PropertyName.Should().Be("Category");
    }

    [Fact(Skip = SkipReason)]
    [Trait("Category", "Unit")]
    public void PreviewItems_filter_combines_FilterCategory_and_per_column_filter_with_AND()
    {
        // Plan 03-05 contract: when both FilterCategory="Walls" AND a per-column
        // filter on Name="B" are active, only rows matching BOTH are visible.
        // Seed: {Category="Walls",Name="A"} + {Category="Doors",Name="A"} → 0 visible.
    }
}
