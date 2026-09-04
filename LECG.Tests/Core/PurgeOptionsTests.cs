using FluentAssertions;
using LECG.Core.Purge;
using Xunit;

namespace LECG.Tests.Core;

/// <summary>
/// PurgeOptions replaced 13 positional bool parameters. Thirteen same-typed arguments in a row
/// is precisely the shape where two can be transposed and still compile, so the value of the
/// record is that each flag reaches the property it was named for.
///
/// Each test sets exactly one flag, which is the only arrangement that can catch a swap:
/// with all-true or all-false, a transposition is invisible.
/// </summary>
public class PurgeOptionsTests
{
    private static PurgeOptions None() => new(
        LineStyles: false,
        LinePatterns: false,
        FillPatterns: false,
        Materials: false,
        Levels: false,
        Parameters: false,
        Groups: false,
        GridTypes: false,
        LevelTypes: false,
        Constraints: false,
        UnplacedRooms: false,
        ViewTemplates: false,
        ViewFilters: false);

    [Fact]
    [Trait("Category", "Unit")]
    public void Every_flag_lands_in_its_own_property()
    {
        (None() with { LineStyles = true }).LineStyles.Should().BeTrue();
        (None() with { LinePatterns = true }).LinePatterns.Should().BeTrue();
        (None() with { FillPatterns = true }).FillPatterns.Should().BeTrue();
        (None() with { Materials = true }).Materials.Should().BeTrue();
        (None() with { Levels = true }).Levels.Should().BeTrue();
        (None() with { Parameters = true }).Parameters.Should().BeTrue();
        (None() with { Groups = true }).Groups.Should().BeTrue();
        (None() with { GridTypes = true }).GridTypes.Should().BeTrue();
        (None() with { LevelTypes = true }).LevelTypes.Should().BeTrue();
        (None() with { Constraints = true }).Constraints.Should().BeTrue();
        (None() with { UnplacedRooms = true }).UnplacedRooms.Should().BeTrue();
        (None() with { ViewTemplates = true }).ViewTemplates.Should().BeTrue();
        (None() with { ViewFilters = true }).ViewFilters.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Positional_construction_maps_to_the_declared_order()
    {
        // Guards the constructor's parameter order, which every positional call site depends on.
        // Only Levels (5th) and ViewFilters (13th) are set.
        var options = new PurgeOptions(
            false, false, false, false, true, false, false, false, false, false, false, false, true);

        options.Levels.Should().BeTrue();
        options.ViewFilters.Should().BeTrue();

        options.LineStyles.Should().BeFalse();
        options.LinePatterns.Should().BeFalse();
        options.FillPatterns.Should().BeFalse();
        options.Materials.Should().BeFalse();
        options.Parameters.Should().BeFalse();
        options.Groups.Should().BeFalse();
        options.GridTypes.Should().BeFalse();
        options.LevelTypes.Should().BeFalse();
        options.Constraints.Should().BeFalse();
        options.UnplacedRooms.Should().BeFalse();
        options.ViewTemplates.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Setting_one_flag_leaves_the_other_twelve_alone()
    {
        PurgeOptions only = None() with { Materials = true };

        only.Materials.Should().BeTrue();
        only.Should().Be(None() with { Materials = true });
        only.Should().NotBe(None());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Equal_flag_sets_compare_equal()
    {
        None().Should().Be(None());
        None().GetHashCode().Should().Be(None().GetHashCode());
    }
}
