// Plan 03-05 GREEN: ICollectionView wiring on SearchReplaceViewModel.PreviewItems.
// VALIDATION row: 3-W0-04. Targets default Category-ascending sort + AND-combined
// filter (FilterCategory dropdown ∧ per-column predicates) in
// `LECG.ViewModels.SearchReplaceViewModel`.
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using FluentAssertions;
using LECG.ViewModels;
using LECG.ViewModels.Components;

namespace LECG.Tests.ViewModels;

/// <summary>
/// 3-W0-04 — GREEN. Asserts ICollectionView contract introduced by Plan 03-05.
/// </summary>
public class SearchReplaceViewModelTests
{
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

    [Fact]
    [Trait("Category", "Unit")]
    public void PreviewItems_default_sort_is_Category_ascending()
    {
        // Plan 03-05 contract: PreviewView's default ICollectionView must carry
        // SortDescriptions[0] = (PropertyName="Category", Direction=Ascending).
        var sut = new SearchReplaceViewModel();

        // Touch PreviewView to materialize the wiring (lazy).
        var view = sut.PreviewView;

        view.Should().NotBeNull();
        view.SortDescriptions.Should().NotBeEmpty();
        view.SortDescriptions[0].PropertyName.Should().Be(nameof(ElementRowViewModel.Category));
        view.SortDescriptions[0].Direction.Should().Be(ListSortDirection.Ascending);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PreviewItems_filter_combines_FilterCategory_and_per_column_filter_with_AND()
    {
        // Plan 03-05 contract: when both FilterCategory="Walls" AND a per-column
        // filter on Name="B" are active, only rows matching BOTH are visible.
        var sut = new SearchReplaceViewModel();
        sut.PreviewItems.Add(new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" });
        sut.PreviewItems.Add(new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B" });
        sut.PreviewItems.Add(new ElementRowViewModel { Category = "Doors", Name = "A", OriginalValue = "A" });
        sut.PreviewItems.Add(new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B" });

        // Apply both filters
        sut.FilterCategory = "Walls";
        sut.SetColumnFilter("Name", row => row.Name == "B");

        var visible = sut.PreviewView.Cast<ElementRowViewModel>().ToList();

        visible.Should().HaveCount(1);
        visible[0].Category.Should().Be("Walls");
        visible[0].Name.Should().Be("B");
    }

    // -----------------------------------------------------------------------
    // R13 — the preview rebuild must cost one collection notification, not one
    // per row. Clear() + N x Add() made the bound ICollectionView re-filter and
    // re-sort once per row; at a few thousand rows that was the freeze.
    // -----------------------------------------------------------------------
    [Fact]
    [Trait("Category", "Unit")]
    public void PreviewItems_ReplaceAll_raises_a_single_Reset()
    {
        var sut = new SearchReplaceViewModel();
        sut.PreviewItems.Add(new ElementRowViewModel { Category = "Old", Name = "old", OriginalValue = "old" });

        var events = new List<NotifyCollectionChangedEventArgs>();
        sut.PreviewItems.CollectionChanged += (_, e) => events.Add(e);

        sut.PreviewItems.ReplaceAll(new[]
        {
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B" },
            new ElementRowViewModel { Category = "Doors", Name = "C", OriginalValue = "C" }
        });

        events.Should().HaveCount(1);
        events[0].Action.Should().Be(NotifyCollectionChangedAction.Reset);
        sut.PreviewItems.Should().HaveCount(3);
        sut.PreviewItems.Select(r => r.Name).Should().Equal("A", "B", "C");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void PreviewItems_ReplaceAll_with_empty_sequence_clears()
    {
        var sut = new SearchReplaceViewModel();
        sut.PreviewItems.Add(new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" });

        sut.PreviewItems.ReplaceAll(new ElementRowViewModel[0]);

        sut.PreviewItems.Should().BeEmpty();
    }
}
