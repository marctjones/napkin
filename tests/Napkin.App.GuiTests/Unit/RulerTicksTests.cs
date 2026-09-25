using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The marks on a ruler and the bar of a 3D scale (#115): pure functions, read without a window.
/// </summary>
public class RulerTicksTests
{
    [Fact]
    public void At_ten_pixels_an_inch_the_marks_are_every_three_inches_and_the_heavy_ones_every_foot()
    {
        // SnapGrid: 3" is the finest step at least 14 px apart at 10 px/in (30 px), and a foot the first
        // heavy one at least four times as far and 56 px wide (120 px).
        System.Collections.Immutable.ImmutableArray<RulerTick> ticks = RulerTicks.Ticks(0, 100, 10);

        Assert.Equal(Enumerable.Range(0, 34).Select(i => i * 3L * Length.UnitsPerInch), ticks.Select(t => t.Units));
        Assert.All(ticks, tick => Assert.Equal(tick.Units % (12 * Length.UnitsPerInch) == 0, tick.Major));

        // Only the heavy marks are written, each in the display format of a length.
        Assert.Equal(["0\"", "1'-0\"", "2'-0\"", "3'-0\"", "4'-0\"", "5'-0\"", "6'-0\"", "7'-0\"", "8'-0\""], ticks.Where(t => t.Label is not null).Select(t => t.Label));
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(37.5)]
    [InlineData(96)]
    [InlineData(400)]
    [InlineData(2000)]
    public void A_ruler_agrees_with_the_grid_the_plan_draws_at_every_zoom(double pixelsPerInch)
    {
        double minor = SnapGrid.StepInches(pixelsPerInch);
        double major = SnapGrid.CoarserStepInches(minor, pixelsPerInch);
        long minorUnits = SnapGrid.UnitsPerStep(minor);
        long majorUnits = major > 0 ? SnapGrid.UnitsPerStep(major) : 0;

        // A window of about 900 px, somewhere off the origin.
        double low = -250.3 / pixelsPerInch;
        double high = (900 - 250.3) / pixelsPerInch;
        System.Collections.Immutable.ImmutableArray<RulerTick> ticks = RulerTicks.Ticks(low, high, pixelsPerInch);

        Assert.NotEmpty(ticks);

        // Every mark is a whole step of the grid, exact to the unit, inside the window, and the next mark is one step on.
        Assert.All(ticks, tick =>
        {
            Assert.Equal(0, tick.Units % minorUnits);
            Assert.InRange(tick.Inches, low - 1e-9, high + 1e-9);
            Assert.Equal(majorUnits > 0 && tick.Units % majorUnits == 0, tick.Major);
        });
        Assert.All(ticks.Zip(ticks.Skip(1)), pair => Assert.Equal(minorUnits, pair.Second.Units - pair.First.Units));

        // No mark of the window is missing: one before the first would be outside it.
        Assert.True((ticks[0].Units - minorUnits) < low * Length.UnitsPerInch);
        Assert.True((ticks[^1].Units + minorUnits) > high * Length.UnitsPerInch);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(12.5)]
    [InlineData(40)]
    [InlineData(150)]
    public void Labels_never_sit_closer_than_the_gap_and_the_first_heavy_mark_is_written(double pixelsPerInch)
    {
        System.Collections.Immutable.ImmutableArray<RulerTick> ticks = RulerTicks.Ticks(-40, 40 + (900 / pixelsPerInch), pixelsPerInch);
        double[] written = [.. ticks.Where(t => t.Label is not null).Select(t => t.Units * pixelsPerInch / Length.UnitsPerInch)];

        Assert.NotEmpty(written);
        Assert.All(written.Zip(written.Skip(1)), pair => Assert.True(pair.Second - pair.First >= RulerTicks.DefaultLabelGapPixels - 1e-9, $"labels {pair.First} and {pair.Second} are too close."));
        Assert.True(ticks.First(t => t.Major).Label is not null, "the first heavy mark must be written.");
    }

    [Fact]
    public void Negative_values_read_with_a_minus_and_zero_reads_zero()
    {
        System.Collections.Immutable.ImmutableArray<RulerTick> ticks = RulerTicks.Ticks(-30, 30, 10);
        string[] labels = [.. ticks.Select(t => t.Label).OfType<string>()];

        Assert.Equal(["-2'-0\"", "-1'-0\"", "0\"", "1'-0\"", "2'-0\""], labels);
    }

    [Fact]
    public void Fine_marks_read_in_fractions_of_an_inch()
    {
        // 200 px/in: 1/4" marks (50 px) and heavy inches (200 px); the inches are written.
        System.Collections.Immutable.ImmutableArray<RulerTick> ticks = RulerTicks.Ticks(1.9, 3.3, 200);

        Assert.Equal(["2\"", "3\""], ticks.Where(t => t.Label is not null).Select(t => t.Label));
        Assert.Contains(ticks, tick => tick.Units == 2 * Length.UnitsPerInch + (Length.UnitsPerInch / 4) && !tick.Major);
        Assert.Equal("2 1/2\"", RulerTicks.Label((2 * Length.UnitsPerInch) + (Length.UnitsPerInch / 2)));
    }

    [Theory]
    [InlineData(5, 5, 10)]
    [InlineData(6, 5, 10)]
    [InlineData(0, 10, 0)]
    [InlineData(0, 10, -1)]
    [InlineData(double.NaN, 10, 10)]
    [InlineData(0, double.PositiveInfinity, 10)]
    public void A_ruler_that_cannot_be_drawn_is_empty_and_does_not_throw(double low, double high, double pixelsPerInch)
    {
        Assert.Empty(RulerTicks.Ticks(low, high, pixelsPerInch));
    }

    [Fact]
    public void A_range_too_large_to_be_a_ruler_returns_nothing_rather_than_a_million_marks()
    {
        Assert.Empty(RulerTicks.Ticks(0, 1e12, 10));
    }

    [Theory]
    [InlineData(0.05, "100'-0\"")]
    [InlineData(0.5, "12'-0\"")]
    [InlineData(10, "6\"")]
    [InlineData(96, "1\"")]
    [InlineData(400, "1/4\"")]
    public void The_scale_bar_is_the_shortest_grid_length_that_is_wide_enough(double pixelsPerInch, string label)
    {
        (double inches, double pixels, string got) = ScaleBar.Choose(pixelsPerInch);

        Assert.Equal(label, got);
        Assert.Equal(inches * pixelsPerInch, pixels, 9);
        Assert.True(pixels >= ScaleBar.MinimumPixels - 1e-9, ScaleBar.Describe(pixelsPerInch));
        Assert.Contains(inches, SnapGrid.Ladder);
    }

    [Fact]
    public void The_scale_bar_is_always_on_the_ladder_wide_enough_and_never_longer_as_you_zoom_in()
    {
        double previous = double.PositiveInfinity;
        for (double ppi = 0.03; ppi < 2400; ppi *= 1.07)
        {
            (double inches, double pixels, _) = ScaleBar.Choose(ppi);

            Assert.Contains(inches, SnapGrid.Ladder);
            Assert.True(pixels >= ScaleBar.MinimumPixels - 1e-9 || inches == SnapGrid.Ladder[^1], ScaleBar.Describe(ppi));
            Assert.True(inches <= previous, $"at {ppi} px/in the bar is {inches}\", longer than the {previous}\" before it.");
            previous = inches;
        }
    }
}
