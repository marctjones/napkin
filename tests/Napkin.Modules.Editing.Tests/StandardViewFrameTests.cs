using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The standard views' pure facts (docs/design/standard-views.md §1.1, §5.3), written out again by
/// hand from the note's table rather than read back from the code: a person standing south of the
/// model has east on the right and up up; standing north, west on the right; and so on.
/// </summary>
public class StandardViewFrameTests
{
    static SignedAxis P(Axis axis) => new(axis, 1);

    static SignedAxis M(Axis axis) => new(axis, -1);

    public static TheoryData<StandardView, SignedAxis, SignedAxis, SignedAxis> Table11() => new()
    {
        { StandardView.Top, P(Axis.X), P(Axis.Y), P(Axis.Z) },
        { StandardView.Bottom, P(Axis.X), M(Axis.Y), M(Axis.Z) },
        { StandardView.Front, P(Axis.X), P(Axis.Z), M(Axis.Y) },
        { StandardView.Back, M(Axis.X), P(Axis.Z), P(Axis.Y) },
        { StandardView.Left, M(Axis.Y), P(Axis.Z), M(Axis.X) },
        { StandardView.Right, P(Axis.Y), P(Axis.Z), P(Axis.X) },
    };

    [Theory]
    [Trait("Feature", "VIEW-005")]
    [MemberData(nameof(Table11))]
    public void Each_view_has_its_table_axes(StandardView view, SignedAxis right, SignedAxis up, SignedAxis toward) =>
        Assert.Equal((right, up, toward), StandardViewFrame.Axes(view));

    [Theory]
    [Trait("Feature", "VIEW-005")]
    [MemberData(nameof(Table11))]
    public void Every_view_is_right_handed(StandardView view, SignedAxis right, SignedAxis up, SignedAxis toward)
    {
        _ = (right, up, toward);
        (SignedAxis r, SignedAxis u, SignedAxis t) = StandardViewFrame.Axes(view);
        int[] a = Unit(r), b = Unit(u), c = Unit(t);
        int[] cross = [(a[1] * b[2]) - (a[2] * b[1]), (a[2] * b[0]) - (a[0] * b[2]), (a[0] * b[1]) - (a[1] * b[0])];
        Assert.Equal(c, cross);
    }

    static int[] Unit(SignedAxis axis) => axis.Axis switch
    {
        Axis.X => [axis.Sign, 0, 0],
        Axis.Y => [0, axis.Sign, 0],
        _ => [0, 0, axis.Sign],
    };

    [Theory]
    [Trait("Feature", "VIEW-005")]
    [InlineData(StandardView.Top, "Top", false, Axis.X, Axis.Y)]
    [InlineData(StandardView.Bottom, "Bottom", false, Axis.X, Axis.Y)]
    [InlineData(StandardView.Front, "Front", true, Axis.X, Axis.Z)]
    [InlineData(StandardView.Back, "Back", true, Axis.X, Axis.Z)]
    [InlineData(StandardView.Left, "Left", true, Axis.Y, Axis.Z)]
    [InlineData(StandardView.Right, "Right", true, Axis.Y, Axis.Z)]
    public void Each_view_has_its_name_and_shows_two_coordinates(StandardView view, string name, bool elevation, Axis across, Axis upward)
    {
        Assert.Equal(name, StandardViewFrame.Name(view));
        Assert.Equal(elevation, StandardViewFrame.IsElevation(view));
        Assert.Equal((across, upward), StandardViewFrame.Readable(view));
    }

    [Fact]
    [Trait("Feature", "VIEW-005")]
    public void The_six_are_in_key_order_and_nothing_else_is_a_view()
    {
        Assert.Equal([StandardView.Top, StandardView.Bottom, StandardView.Front, StandardView.Back, StandardView.Left, StandardView.Right], StandardViewFrame.All);
        Assert.Equal([1, 2, 3, 4, 5, 6], StandardViewFrame.All.Select(view => (int)view));
        Assert.Throws<ArgumentOutOfRangeException>(() => StandardViewFrame.Axes((StandardView)7));
        Assert.Throws<ArgumentOutOfRangeException>(() => StandardViewFrame.Name((StandardView)0));
    }

    [Fact]
    [Trait("Feature", "VIEW-005")]
    public void A_signed_axis_reads_with_its_sign()
    {
        Assert.Equal("+X", P(Axis.X).ToString());
        Assert.Equal("−Z", M(Axis.Z).ToString());
    }
}
