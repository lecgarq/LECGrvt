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

    // -----------------------------------------------------------------------
    // R5 — a deselection is a decision and survives every rebuild, including a
    // filter round-trip that removes the row from the grid and brings it back.
    // Drives the three seam methods directly: the VM's parameterless ctor needs
    // no Revit, and UpdatePreviewAsync would need a Document.
    // -----------------------------------------------------------------------
    private static ElementRowViewModel Row(string type, long id, string original, bool renameable = true)
        => new ElementRowViewModel
        {
            Type = type,
            Id = id,
            Name = original,
            OriginalValue = original,
            Category = "Cat",
            IsRenameable = renameable
        };

    [Fact]
    [Trait("Category", "Unit")]
    public void CheckKey_distinguishes_FamilyParameter_rows_that_share_one_Id()
    {
        // BaseElementCollectionService.cs:264 sets Id = familyId for every parameter of a
        // family, so Id alone collides. OriginalValue is what separates them.
        var a = Row("FamilyParameter", 40, "Param_A");
        var b = Row("FamilyParameter", 40, "Param_B");

        SearchReplaceViewModel.CheckKey(a).Should().NotBe(SearchReplaceViewModel.CheckKey(b));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Unchecked_row_stays_unchecked_across_a_rebuild()
    {
        var sut = new SearchReplaceViewModel();
        sut.PreviewItems.ReplaceAll(new[] { Row("Type", 1, "A"), Row("Type", 2, "B") });

        sut.PreviewItems[0].IsChecked = false;   // user deselects A
        sut.HarvestCheckState();

        var rebuilt = new[] { Row("Type", 1, "A"), Row("Type", 2, "B") };
        sut.ApplyCheckState(rebuilt);

        rebuilt[0].IsChecked.Should().BeFalse();
        rebuilt[1].IsChecked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Unchecked_row_stays_unchecked_across_a_filter_round_trip()
    {
        var sut = new SearchReplaceViewModel();
        sut.PreviewItems.ReplaceAll(new[] { Row("Type", 1, "A"), Row("Type", 2, "B") });

        sut.PreviewItems[0].IsChecked = false;   // user deselects A
        sut.HarvestCheckState();

        // Filter narrows: A is gone from the rebuilt set entirely.
        var filtered = new[] { Row("Type", 2, "B") };
        sut.ApplyCheckState(filtered);
        sut.PreviewItems.ReplaceAll(filtered);
        sut.HarvestCheckState();                 // harvest while A is absent

        // Filter widens again: A comes back and must still be unchecked.
        var widened = new[] { Row("Type", 1, "A"), Row("Type", 2, "B") };
        sut.ApplyCheckState(widened);

        widened[0].IsChecked.Should().BeFalse();
        widened[1].IsChecked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rechecking_a_row_clears_the_remembered_deselection()
    {
        var sut = new SearchReplaceViewModel();
        sut.PreviewItems.ReplaceAll(new[] { Row("Type", 1, "A") });

        sut.PreviewItems[0].IsChecked = false;
        sut.HarvestCheckState();
        sut.PreviewItems[0].IsChecked = true;    // user changes their mind
        sut.HarvestCheckState();

        var rebuilt = new[] { Row("Type", 1, "A") };
        sut.ApplyCheckState(rebuilt);

        rebuilt[0].IsChecked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Non_renameable_rows_are_not_remembered_as_user_deselections()
    {
        // ProcessPreview unchecks rows it marks non-renameable
        // (SearchReplacePreviewService.cs:157,230). That is the service's decision, not the
        // user's — if it were harvested, the row would stay unchecked forever once a later
        // rule made it renameable again.
        var sut = new SearchReplaceViewModel();
        var skipped = Row("FamilyParameter", 40, "Param_A", renameable: false);
        skipped.IsChecked = false;
        sut.PreviewItems.ReplaceAll(new[] { skipped });

        sut.HarvestCheckState();

        var nowRenameable = Row("FamilyParameter", 40, "Param_A");
        sut.ApplyCheckState(new[] { nowRenameable });

        nowRenameable.IsChecked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ApplyCheckState_never_rechecks_a_row_ProcessPreview_unchecked()
    {
        // Only ever clears a check, never sets one.
        var sut = new SearchReplaceViewModel();
        var skipped = Row("Type", 1, "A", renameable: false);
        skipped.IsChecked = false;

        sut.ApplyCheckState(new[] { skipped });

        skipped.IsChecked.Should().BeFalse();
    }
}
