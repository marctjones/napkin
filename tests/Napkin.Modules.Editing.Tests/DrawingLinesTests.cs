using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The one table of line kinds (#134): visible heavier than hidden, hidden heavier than a dimension,
/// hidden the only dashed-and-light one among the drawn lines, and every weight in pixels.
/// </summary>
public class DrawingLinesTests
{
    [Fact]
    [Trait("Feature", "VIEW-008")]
    public void Visible_is_heavier_than_hidden_which_is_heavier_than_a_dimension()
    {
        double visible = DrawingLines.Of(LineKind.Visible).Pixels;
        double hidden = DrawingLines.Of(LineKind.Hidden).Pixels;
        double dimension = DrawingLines.Of(LineKind.Dimension).Pixels;
        Assert.True(visible > hidden, $"visible {visible} is not heavier than hidden {hidden}");
        Assert.True(hidden > dimension, $"hidden {hidden} is not heavier than a dimension {dimension}");
        Assert.Equal(dimension, DrawingLines.Of(LineKind.Extension).Pixels);
    }

    [Fact]
    [Trait("Feature", "VIEW-008")]
    public void Hidden_edges_are_the_notes_light_dashes_and_the_rest_are_solid_but_the_centre_chain()
    {
        // standard-views §2.3: the stroke at 0.35, dash [3, 3], one pixel.
        LineStyle hidden = DrawingLines.Of(LineKind.Hidden);
        Assert.Equal((1.0, 0.35), (hidden.Pixels, hidden.Opacity));
        Assert.Equal([3.0, 3.0], hidden.Dashes);
        Assert.True(hidden.IsDashed);

        foreach (LineKind kind in (LineKind[])[LineKind.Visible, LineKind.Dimension, LineKind.Extension])
        {
            Assert.False(DrawingLines.Of(kind).IsDashed, $"{kind} is dashed");
            Assert.Equal(1, DrawingLines.Of(kind).Opacity);
        }

        // A chain: long, gap, short, gap.
        LineStyle centre = DrawingLines.Of(LineKind.Centre);
        Assert.Equal(4, centre.Dashes.Count);
        Assert.True(centre.Dashes[0] > centre.Dashes[2]);
    }

    [Fact]
    [Trait("Feature", "VIEW-008")]
    public void Every_kind_has_a_line_and_nothing_else_does()
    {
        Assert.Equal(Enum.GetValues<LineKind>(), DrawingLines.All);
        Assert.All(DrawingLines.All, kind => Assert.True(DrawingLines.Of(kind).Pixels > 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DrawingLines.Of((LineKind)99));
    }
}
