using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The grid's levels over the whole zoom range (#114): never more than two adjacent levels of the snap's
/// ladder, each drawn one far enough apart to read and near enough to help, the minor one fading in.
/// </summary>
public class GridLevelsTests
{
    /// <summary>Zooms from 0.5 % to 20 000 % of 96 px to the inch, in steps of about 2 %.</summary>
    static IEnumerable<double> Zooms()
    {
        for (double zoom = 0.005; zoom <= 200; zoom *= 1.02)
        {
            yield return 96 * zoom;
        }
    }

    [Fact]
    [Trait("Feature", "VIEW-014")]
    public void Every_level_drawn_is_between_ten_and_two_hundred_pixels_apart()
    {
        foreach (double ppi in Zooms())
        {
            // The ladder ends at a quarter inch and at a thousand feet: zoomed in past the one, its
            // lines only open further apart; zoomed out past the other, they close up. Between the
            // two, which is every zoom a person works at, both levels are in the band.
            var levels = GridLevels.Of(ppi);
            if (levels[0].StepInches == SnapGrid.Ladder[0] || levels.Length < 2)
            {
                continue;
            }

            foreach (GridLevel level in levels)
            {
                Assert.InRange(level.SpacingPixels(ppi), 10, 200);
            }
        }
    }

    [Fact]
    [Trait("Feature", "VIEW-014")]
    public void Exactly_two_levels_are_drawn_below_the_top_of_the_ladder_and_they_are_the_snap_step_and_a_step_above()
    {
        foreach (double ppi in Zooms())
        {
            var levels = GridLevels.Of(ppi);
            double minor = SnapGrid.StepInches(ppi);
            Assert.Equal(minor, levels[0].StepInches);
            Assert.False(levels[0].Major);
            if (SnapGrid.CoarserStepInches(minor, ppi) > 0)
            {
                Assert.Equal(2, levels.Length);
                Assert.True(levels[1].Major);
                Assert.True(levels[1].StepInches > minor);
                Assert.Contains(levels[1].StepInches, SnapGrid.Ladder);
            }
            else
            {
                Assert.Single(levels);
            }
        }
    }

    [Fact]
    [Trait("Feature", "VIEW-014")]
    public void Zooming_in_never_coarsens_a_level()
    {
        double? lastMinor = null, lastMajor = null;
        foreach (double ppi in Zooms())
        {
            var levels = GridLevels.Of(ppi);
            if (lastMinor is { } minor)
            {
                Assert.True(levels[0].StepInches <= minor);
            }

            if (lastMajor is { } major && levels.Length == 2)
            {
                Assert.True(levels[1].StepInches <= major);
            }

            lastMinor = levels[0].StepInches;
            lastMajor = levels.Length == 2 ? levels[1].StepInches : lastMajor;
        }
    }

    [Theory]
    [Trait("Feature", "VIEW-014")]
    // At the closest the ladder allows, 14 px: faint, never gone.
    [InlineData(14, 0.35)]
    // Halfway to 28 px: halfway to full.
    [InlineData(21, 0.675)]
    [InlineData(28, 1)]
    [InlineData(40, 1)]
    // Closer than the ladder ever draws is still the faintest, not less.
    [InlineData(5, 0.35)]
    public void The_minor_level_fades_in_as_its_lines_open_up(double spacing, double expected) =>
        Assert.Equal(expected, GridLevels.Fade(spacing), 9);

    [Fact]
    [Trait("Feature", "VIEW-014")]
    public void The_major_level_is_always_full_and_the_minor_never_stronger()
    {
        foreach (double ppi in Zooms())
        {
            var levels = GridLevels.Of(ppi);
            Assert.InRange(levels[0].Opacity, GridLevels.FaintestOpacity, 1);
            if (levels.Length == 2)
            {
                Assert.Equal(1, levels[1].Opacity);
            }
        }
    }

    [Theory]
    [Trait("Feature", "VIEW-014")]
    [InlineData(0, 100, 1)]
    [InlineData(50, 100, 0.675)]
    [InlineData(-100, 100, 0.35)]
    [InlineData(250, 100, 0.35)]
    [InlineData(10, 0, 1)]
    public void A_ground_line_fades_with_its_distance_from_the_middle(double distance, double radius, double expected) =>
        Assert.Equal(expected, GridLevels.GroundFade(distance, radius), 9);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void No_zoom_draws_no_levels(double ppi) => Assert.Empty(GridLevels.Of(ppi));
}
