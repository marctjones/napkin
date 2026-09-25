using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// A standard view's rulers and grid as numbers (docs/design/standard-views.md §5.2), worked by hand:
/// a view axis that runs against its world axis counts down, and the floor is heavy in an elevation.
/// </summary>
public class ViewGridTests
{
    [Fact]
    [Trait("Feature", "VIEW-011")]
    public void A_mirrored_axis_shows_the_same_world_span_counted_the_other_way()
    {
        // Back's screen right is −X: view coordinates −10 to −2 are world x 2 to 10.
        Assert.Equal((2.0, 10.0), ViewGrid.WorldSpan(new SignedAxis(Axis.X, -1), -10, -2));
        Assert.Equal((2.0, 10.0), ViewGrid.WorldSpan(new SignedAxis(Axis.X, -1), -2, -10));
        Assert.Equal((-10.0, -2.0), ViewGrid.WorldSpan(new SignedAxis(Axis.X, 1), -10, -2));
        Assert.Equal(-6.5, ViewGrid.InView(new SignedAxis(Axis.X, -1), 6.5));
        Assert.Equal(6.5, ViewGrid.InView(new SignedAxis(Axis.Z, 1), 6.5));
    }

    [Fact]
    [Trait("Feature", "VIEW-011")]
    public void Lines_are_on_the_plans_ladder_heavy_on_the_next_step_up()
    {
        // At 10 px/in the fine step is 3" (30 px) and the heavy one 12" (≥ 4 steps and ≥ 56 px).
        IReadOnlyList<GridLine> lines = ViewGrid.Lines(StandardView.Front, Axis.X, -1, 25, 10);
        Assert.Equal([0.0, 3, 6, 9, 12, 15, 18, 21, 24], lines.Select(line => line.World));
        Assert.Equal([0.0, 12, 24], lines.Where(line => line.Major).Select(line => line.World));
    }

    [Theory]
    [Trait("Feature", "VIEW-011")]
    [InlineData(StandardView.Front, Axis.Z, true)]
    [InlineData(StandardView.Left, Axis.Z, true)]
    [InlineData(StandardView.Front, Axis.X, false)]
    [InlineData(StandardView.Top, Axis.Y, false)]
    public void Only_an_elevations_floor_is_heavy_when_the_ladder_has_no_heavier_step(StandardView view, Axis axis, bool heavy)
    {
        // So far out that the fine step is the ladder's last (12000") and nothing is heavier.
        GridLine zero = Assert.Single(ViewGrid.Lines(view, axis, -1, 1, 0.0001));
        Assert.Equal(0, zero.World);
        Assert.Equal(heavy, zero.Major);
    }

    [Theory]
    [Trait("Feature", "VIEW-011")]
    [InlineData(double.NaN, 1, 10)]
    [InlineData(0, double.PositiveInfinity, 10)]
    [InlineData(5, 1, 10)]
    [InlineData(0, 1, 0)]
    [InlineData(0, 1e9, 10)]
    public void No_lines_for_a_span_that_is_not_one_or_too_many_to_draw(double low, double high, double pixelsPerInch) =>
        Assert.Empty(ViewGrid.Lines(StandardView.Front, Axis.X, low, high, pixelsPerInch));
}
