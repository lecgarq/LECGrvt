using System;
using FluentAssertions;
using LECG.Core.Purge;
using Xunit;

namespace LECG.Tests.Core;

/// <summary>
/// PurgeResult exists to replace a 13-int tuple that used to be threaded through the purge
/// service stack, where two transposed values would compile and silently misreport.
///
/// These tests use a distinct power of two per component on purpose: any field that reads or
/// writes the wrong slot changes the arithmetic, so the assertion fails instead of passing on
/// a coincidental sum.
/// </summary>
public class PurgeResultTests
{
    // 13 components, one bit each — every subset sums to a unique value.
    private static PurgeResult Distinct() => new(
        LineStyles: 1,
        LinePatterns: 2,
        FillPatterns: 4,
        Materials: 8,
        Levels: 16,
        Parameters: 32,
        Groups: 64,
        GridTypes: 128,
        LevelTypes: 256,
        Constraints: 512,
        UnplacedRooms: 1024,
        ViewTemplates: 2048,
        ViewFilters: 4096);

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_is_all_zeros()
    {
        PurgeResult empty = PurgeResult.Empty;

        empty.Total.Should().Be(0);
        empty.Should().Be(new PurgeResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Total_sums_every_component_exactly_once()
    {
        // 2^13 - 1. Any omitted or double-counted component gives a different number.
        Distinct().Total.Should().Be(8191);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Each_constructor_argument_lands_in_its_own_property()
    {
        PurgeResult r = Distinct();

        r.LineStyles.Should().Be(1);
        r.LinePatterns.Should().Be(2);
        r.FillPatterns.Should().Be(4);
        r.Materials.Should().Be(8);
        r.Levels.Should().Be(16);
        r.Parameters.Should().Be(32);
        r.Groups.Should().Be(64);
        r.GridTypes.Should().Be(128);
        r.LevelTypes.Should().Be(256);
        r.Constraints.Should().Be(512);
        r.UnplacedRooms.Should().Be(1024);
        r.ViewTemplates.Should().Be(2048);
        r.ViewFilters.Should().Be(4096);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_sums_component_wise_without_crossing_fields()
    {
        PurgeResult sum = Distinct().Add(Distinct());

        sum.Should().Be(new PurgeResult(2, 4, 8, 16, 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192));
        sum.Total.Should().Be(16382);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_with_Empty_is_a_no_op()
    {
        PurgeResult r = Distinct();

        r.Add(PurgeResult.Empty).Should().Be(r);
        PurgeResult.Empty.Add(r).Should().Be(r);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_accumulates_across_several_passes()
    {
        // The intended use: one result per purge pass, accumulated into a running total.
        PurgeResult running = PurgeResult.Empty
            .Add(new PurgeResult(1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .Add(new PurgeResult(0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0))
            .Add(new PurgeResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 3));

        running.LineStyles.Should().Be(1);
        running.LinePatterns.Should().Be(2);
        running.ViewFilters.Should().Be(3);
        running.Total.Should().Be(6);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Add_rejects_null()
    {
        Action act = () => Distinct().Add(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Equal_component_values_compare_equal()
    {
        Distinct().Should().Be(Distinct());
        Distinct().GetHashCode().Should().Be(Distinct().GetHashCode());
        Distinct().Should().NotBe(Distinct() with { Materials = 99 });
    }
}
