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

    // -----------------------------------------------------------------------
    // R12 — the per-scope cache key.
    //
    // Neither the cache HIT nor the ScopeKey property can be exercised through
    // the VM: setting any scope property runs SetExclusiveScope -> RefreshScope,
    // whose body references ISearchReplaceService, and resolving that interface
    // loads RevitAPI. The JIT does that before the method's null guard runs, so
    // the runner throws FileNotFoundException regardless. Same root cause as the
    // five already-skipped Document-parametered tests.
    //
    // So the key is built by a pure static and tested there. That is the part
    // that could silently be wrong and serve one scope's elements for another.
    // The cache hit itself is a Revit smoke-test step.
    // -----------------------------------------------------------------------
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildScopeKey_is_distinct_for_every_single_scope()
    {
        var keys = new List<string>();
        for (int i = 0; i < 9; i++)
        {
            keys.Add(SearchReplaceViewModel.BuildScopeKey(
                i == 0, i == 1, i == 2, i == 3, i == 4, i == 5, i == 6, i == 7, i == 8));
        }

        keys.Should().HaveCount(9);
        keys.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildScopeKey_is_stable_for_the_same_flags()
    {
        // This is what makes a cache hit: leaving a scope and returning must produce
        // the identical key, or the collect is paid twice.
        string first = SearchReplaceViewModel.BuildScopeKey(true, false, false, false, false, false, false, false, false);
        string again = SearchReplaceViewModel.BuildScopeKey(true, false, false, false, false, false, false, false, false);

        again.Should().Be(first);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildScopeKey_distinguishes_combined_scopes()
    {
        // Guards the key against being under-specified if the scope control ever
        // becomes genuinely multi-select.
        string typesOnly = SearchReplaceViewModel.BuildScopeKey(true, false, false, false, false, false, false, false, false);
        string typesAndViews = SearchReplaceViewModel.BuildScopeKey(true, false, true, false, false, false, false, false, false);

        typesAndViews.Should().NotBe(typesOnly);
    }

    // -----------------------------------------------------------------------
    // Phase 2 — selection obeys the filter.
    // The bug these guard: SelectAll/SelectNone walked PreviewItems (everything)
    // instead of PreviewView (what you can see), so filtering to 12 rows and
    // hitting Select All ticked all four thousand.
    // -----------------------------------------------------------------------
    private static SearchReplaceViewModel WithRows(params ElementRowViewModel[] rows)
    {
        var vm = new SearchReplaceViewModel();
        vm.PreviewItems.ReplaceAll(rows);
        _ = vm.PreviewView;
        return vm;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectAll_ignores_rows_hidden_by_the_filter()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = false },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B", IsChecked = false });

        vm.FilterCategory = "Walls";
        vm.SelectAllCommand.Execute(null);

        vm.PreviewItems.Single(r => r.Name == "A").IsChecked.Should().BeTrue();
        vm.PreviewItems.Single(r => r.Name == "B").IsChecked.Should().BeFalse("it was hidden by the filter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectNone_ignores_rows_hidden_by_the_filter()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = true },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B", IsChecked = true });

        vm.FilterCategory = "Walls";
        vm.SelectNoneCommand.Execute(null);

        vm.PreviewItems.Single(r => r.Name == "A").IsChecked.Should().BeFalse();
        vm.PreviewItems.Single(r => r.Name == "B").IsChecked.Should().BeTrue("it was hidden by the filter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void SelectAll_never_ticks_a_row_ProcessPreview_marked_unrenameable()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = false },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B", IsChecked = false, IsRenameable = false });

        vm.SelectAllCommand.Execute(null);

        vm.PreviewItems.Single(r => r.Name == "A").IsChecked.Should().BeTrue();
        vm.PreviewItems.Single(r => r.Name == "B").IsChecked.Should().BeFalse("the grid disables its checkbox");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void InvertSelection_flips_only_visible_renameable_rows()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = true },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B", IsChecked = false },
            new ElementRowViewModel { Category = "Doors", Name = "C", OriginalValue = "C", IsChecked = true });

        vm.FilterCategory = "Walls";
        vm.InvertSelectionCommand.Execute(null);

        vm.PreviewItems.Single(r => r.Name == "A").IsChecked.Should().BeFalse();
        vm.PreviewItems.Single(r => r.Name == "B").IsChecked.Should().BeTrue();
        vm.PreviewItems.Single(r => r.Name == "C").IsChecked.Should().BeTrue("hidden rows are untouched");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Counts_track_the_active_filter()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = true },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B", IsChecked = false },
            new ElementRowViewModel { Category = "Doors", Name = "C", OriginalValue = "C", IsChecked = true });

        vm.VisibleCount.Should().Be(3);
        vm.CheckedCount.Should().Be(2);

        vm.FilterCategory = "Walls";

        vm.VisibleCount.Should().Be(2);
        vm.CheckedCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CheckedCount_follows_an_individual_checkbox_click()
    {
        var vm = new SearchReplaceViewModel();
        var raised = new List<string>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? "");

        // Rows must go through the rebuild path to get their notification wired up.
        vm.SetPreviewRows(new List<ElementRowViewModel>
        {
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = true }
        });
        raised.Clear();

        vm.PreviewItems[0].IsChecked = false;

        vm.CheckedCount.Should().Be(0);
        raised.Should().Contain(nameof(SearchReplaceViewModel.CheckedCount));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToggleRange_sets_every_row_between_the_two_in_view_order()
    {
        // Default sort is Category ascending, so view order is Doors(C), Walls(A), Walls(B).
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = false },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B", IsChecked = false },
            new ElementRowViewModel { Category = "Doors", Name = "C", OriginalValue = "C", IsChecked = false });

        var ordered = vm.PreviewView.Cast<ElementRowViewModel>().ToList();
        vm.ToggleRange(ordered[0], ordered[2], true);

        ordered.Should().OnlyContain(r => r.IsChecked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToggleRange_works_when_the_anchor_is_below_the_target()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = true },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B", IsChecked = true });

        var ordered = vm.PreviewView.Cast<ElementRowViewModel>().ToList();
        vm.ToggleRange(ordered[1], ordered[0], false);

        ordered.Should().OnlyContain(r => !r.IsChecked);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ToggleRange_skips_unrenameable_rows_inside_the_range()
    {
        var vm = WithRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A", IsChecked = false },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B", IsChecked = false, IsRenameable = false },
            new ElementRowViewModel { Category = "Walls", Name = "C", OriginalValue = "C", IsChecked = false });

        var ordered = vm.PreviewView.Cast<ElementRowViewModel>().ToList();
        vm.ToggleRange(ordered[0], ordered[2], true);

        vm.PreviewItems.Single(r => r.Name == "B").IsChecked.Should().BeFalse();
        vm.PreviewItems.Where(r => r.Name != "B").Should().OnlyContain(r => r.IsChecked);
    }

    // -----------------------------------------------------------------------
    // Phase 3 — filtering that reaches every column, and says what went wrong.
    // -----------------------------------------------------------------------
    private static SearchReplaceViewModel WithBoundRows(params ElementRowViewModel[] rows)
    {
        var vm = new SearchReplaceViewModel();
        _ = vm.PreviewView;
        vm.SetPreviewRows(rows.ToList());
        return vm;
    }

    // ---- R7: regex errors are named, not swallowed ----

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateReplaceRegex_names_the_pattern_error()
    {
        var vm = new SearchReplaceViewModel();
        vm.ReplaceRule.IsActive = true;
        vm.ReplaceRule.UseRegex = true;
        vm.ReplaceRule.FindText = "([unclosed";

        string? error = vm.ValidateReplaceRegex();

        error.Should().NotBeNull();
        error.Should().StartWith("Invalid regular expression:");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateReplaceRegex_passes_a_valid_pattern()
    {
        var vm = new SearchReplaceViewModel();
        vm.ReplaceRule.IsActive = true;
        vm.ReplaceRule.UseRegex = true;
        vm.ReplaceRule.FindText = "^W-[0-9]+$";

        vm.ValidateReplaceRegex().Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ValidateReplaceRegex_ignores_a_bad_pattern_when_regex_mode_is_off()
    {
        // "([unclosed" is a legitimate literal to search for when UseRegex is false.
        var vm = new SearchReplaceViewModel();
        vm.ReplaceRule.IsActive = true;
        vm.ReplaceRule.UseRegex = false;
        vm.ReplaceRule.FindText = "([unclosed";

        vm.ValidateReplaceRegex().Should().BeNull();
    }

    // ---- R8: per-column filters ----

    [Fact]
    [Trait("Category", "Unit")]
    public void Column_filters_narrow_the_view_and_AND_together()
    {
        // Only Original, New and Status carry per-column filters. Type was dropped because
        // scope is single-select, so every row holds the same value; Category was dropped
        // because the multi-select dropdown already filters it, with counts.
        var vm = WithBoundRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "W-A", NewValue = "X-A", Status = "ok" },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "W-B", NewValue = "Y-B", Status = "ok" },
            new ElementRowViewModel { Category = "Walls", Name = "C", OriginalValue = "Z-C", NewValue = "X-C", Status = "skipped" });

        vm.FilterColOriginal = "W-";
        vm.VisibleCount.Should().Be(2);

        vm.FilterColNew = "X-";
        vm.VisibleCount.Should().Be(1);
        vm.PreviewView.Cast<ElementRowViewModel>().Single().Name.Should().Be("A");

        vm.FilterColStatus = "skipped";
        vm.VisibleCount.Should().Be(0, "all three filters AND together");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Clearing_a_column_filter_restores_its_rows()
    {
        var vm = WithBoundRows(
            new ElementRowViewModel { Type = "Type", Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Type = "View", Category = "Walls", Name = "B", OriginalValue = "B" });

        vm.FilterColOriginal = "B";
        vm.VisibleCount.Should().Be(1);

        vm.FilterColOriginal = "";
        vm.VisibleCount.Should().Be(2);
    }

    // ---- R9 / R10: the category dropdown ----

    [Fact]
    [Trait("Category", "Unit")]
    public void Category_options_carry_a_row_count_each()
    {
        var vm = WithBoundRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Category = "Walls", Name = "B", OriginalValue = "B" },
            new ElementRowViewModel { Category = "Doors", Name = "C", OriginalValue = "C" });

        vm.CategoryOptions.Single(o => o.Name == "Walls").Count.Should().Be(2);
        vm.CategoryOptions.Single(o => o.Name == "Doors").Count.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Selecting_two_categories_shows_both()
    {
        var vm = WithBoundRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B" },
            new ElementRowViewModel { Category = "Roofs", Name = "C", OriginalValue = "C" });

        vm.CategoryOptions.Single(o => o.Name == "Walls").IsSelected = true;
        vm.CategoryOptions.Single(o => o.Name == "Doors").IsSelected = true;

        vm.VisibleCount.Should().Be(2);
        vm.CategoryFilterSummary.Should().Be("2 categories");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void No_category_selected_means_no_constraint()
    {
        var vm = WithBoundRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B" });

        vm.VisibleCount.Should().Be(2);
        vm.CategoryFilterSummary.Should().Be("All categories");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CategorySearch_narrows_the_option_list_without_filtering_rows()
    {
        var vm = WithBoundRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B" },
            new ElementRowViewModel { Category = "Wall Sweeps", Name = "C", OriginalValue = "C" });

        vm.CategorySearch = "wall";

        vm.VisibleCategoryOptions.Select(o => o.Name).Should().BeEquivalentTo("Walls", "Wall Sweeps");
        vm.VisibleCount.Should().Be(3, "typeahead narrows the dropdown, not the grid");
    }

    // ---- R11: clear everything ----

    [Fact]
    [Trait("Category", "Unit")]
    public void ClearFilters_restores_every_row_and_resets_the_indicator()
    {
        var vm = WithBoundRows(
            new ElementRowViewModel { Type = "Type", Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Type = "View", Category = "Doors", Name = "B", OriginalValue = "B" });

        vm.CategoryOptions.Single(o => o.Name == "Walls").IsSelected = true;
        vm.FilterColOriginal = "A";
        vm.HasActiveFilters.Should().BeTrue();
        vm.VisibleCount.Should().Be(1);

        vm.ClearFiltersCommand.Execute(null);

        vm.VisibleCount.Should().Be(2);
        vm.HasActiveFilters.Should().BeFalse();
        vm.CategoryFilterSummary.Should().Be("All categories");
        vm.CategoryOptions.Should().OnlyContain(o => !o.IsSelected);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void FilterCategory_shim_still_drives_the_selection_set()
    {
        // ToCriteria and older callers still write the single-string property.
        var vm = WithBoundRows(
            new ElementRowViewModel { Category = "Walls", Name = "A", OriginalValue = "A" },
            new ElementRowViewModel { Category = "Doors", Name = "B", OriginalValue = "B" });

        vm.FilterCategory = "Walls";
        vm.VisibleCount.Should().Be(1);
        vm.CategoryOptions.Single(o => o.Name == "Walls").IsSelected.Should().BeTrue();

        vm.FilterCategory = "All";
        vm.VisibleCount.Should().Be(2);
        vm.CategoryOptions.Should().OnlyContain(o => !o.IsSelected);
    }
}
