using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The shape workshop's words and its reading of the cut fields
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;1.3, &#xA7;7.2), without a window: a corner cut
/// taken as two setbacks or as an angle with one rounding, a radius, a depth, and text that is not
/// a length refused with the reason and no change.
/// </summary>
public class CutEntryTests
{
    static readonly LengthFormat Format = new FeetInchesFormat(16);

    static readonly Box Blank = Box.AsDrawn(
        EntityId.New(), LayerId.New(), Point2.Origin, Length.Inches(12), Length.Inches(8), Box.DefaultDepth, Angle.Zero) with
    {
        Name = "Apron",
    };

    static readonly CornerCut Clip = new(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(2));

    [Theory]
    [InlineData(BoxCorner.SouthWest, BoxEdge.South, BoxEdge.West)]
    [InlineData(BoxCorner.SouthEast, BoxEdge.South, BoxEdge.East)]
    [InlineData(BoxCorner.NorthEast, BoxEdge.North, BoxEdge.East)]
    [InlineData(BoxCorner.NorthWest, BoxEdge.North, BoxEdge.West)]
    public void Each_corner_names_its_x_and_y_running_edges(BoxCorner corner, BoxEdge x, BoxEdge y)
    {
        Assert.Equal(x, CutEntry.XEdge(corner));
        Assert.Equal(y, CutEntry.YEdge(corner));
    }

    [Fact]
    public void The_list_writes_each_kind_of_cut_with_its_numbers()
    {
        Assert.Equal("north-east corner — clip 3\" × 2\"", CutEntry.Summary(Clip, Format));
        Assert.Equal("south-west corner — round, 1 1/2\" radius", CutEntry.Summary(new RoundedCorner(BoxCorner.SouthWest, Length.Inches(1, 1, 2)), Format));
        Assert.Equal("south edge — scallop 1\" deep", CutEntry.Summary(new CurvedEdge(BoxEdge.South, Bow.Inward, Length.Inches(1)), Format));
        Assert.Equal("west edge — curve 2\" deep", CutEntry.Summary(new CurvedEdge(BoxEdge.West, Bow.Outward, Length.Inches(2)), Format));
    }

    [Fact]
    public void A_length_off_the_sixteenth_grid_is_marked_approximately()
    {
        Length third = Length.FromInches(1.0 / 3, Rounding.HalfAwayFromZero);

        Assert.StartsWith(CutAngle.Approximately, CutEntry.Text(third, Format));
        Assert.Equal("3\"", CutEntry.Text(Length.Inches(3), Format));
    }

    [Fact]
    public void Headline_hint_and_count_words()
    {
        Assert.Equal("Shaping Apron — 1'-0\" × 8\" blank", CutEntry.Headline("Apron", Blank, Format));
        Assert.Equal("Cuts — 0", CutEntry.CutsHeadline(0));
        Assert.Equal("Cuts — 1", CutEntry.CutsHeadline(1));
        Assert.Equal(CutEntry.DefaultHint, CutEntry.Hint(string.Empty));
        Assert.Equal("Release to clip the corner.", CutEntry.Hint("Release to clip the corner"));
    }

    [Fact]
    public void A_corner_cut_shows_both_setbacks_named_by_edge_and_the_angle_it_comes_out_at()
    {
        CutFieldsText fields = CutEntry.Fields(Clip, Format);

        Assert.True(fields.IsCorner);
        Assert.Equal("Cut at the north-east corner", fields.Headline);
        Assert.Equal("north edge", fields.FirstCaption);
        Assert.Equal("3\"", fields.First);
        Assert.Equal("east edge", fields.SecondCaption);
        Assert.Equal("2\"", fields.Second);
        Assert.Equal(CutAngle.Text(Length.Inches(2), Length.Inches(3)), fields.Angle);
        Assert.StartsWith("Two marks", fields.Readout, StringComparison.Ordinal);
    }

    [Fact]
    public void A_rounded_corner_and_a_curve_show_one_field()
    {
        CutFieldsText round = CutEntry.Fields(new RoundedCorner(BoxCorner.SouthEast, Length.Inches(1)), Format);
        CutFieldsText scallop = CutEntry.Fields(new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(1)), Format);
        CutFieldsText bow = CutEntry.Fields(new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(1)), Format);

        Assert.False(round.IsCorner);
        Assert.Equal(("Radius", "1\"", null, null, null), (round.FirstCaption, round.First, round.SecondCaption, round.Second, round.Angle));
        Assert.StartsWith("A quarter circle", round.Readout, StringComparison.Ordinal);
        Assert.Equal(("Depth", "Cut at the north edge"), (scallop.FirstCaption, scallop.Headline));
        Assert.StartsWith("A scallop", scallop.Readout, StringComparison.Ordinal);
        Assert.StartsWith("A bow", bow.Readout, StringComparison.Ordinal);
        Assert.False(bow.IsCorner);
    }

    [Fact]
    public void Two_setbacks_typed_are_taken_as_typed_when_the_angle_is_untouched()
    {
        string angle = CutEntry.Fields(Clip, Format).Angle!;

        CutEdit edit = Assert.IsType<CutEdit>(CutEntry.Read(Blank, Clip, new TypedCut("4", "1 1/2", $" {angle} "), Format));

        Assert.Equal(Clip with { AlongX = Length.Inches(4), AlongY = Length.Inches(1, 1, 2) }, edit.Cut);
        Assert.Equal($"Set Apron's {Clip.Site} to 4\" by 1 1/2\"", edit.What);
    }

    [Fact]
    public void An_edited_angle_keeps_the_longer_setback_and_works_out_the_shorter_once()
    {
        CutEdit edit = Assert.IsType<CutEdit>(CutEntry.Read(Blank, Clip, new TypedCut("3", "2", "45"), Format));

        Assert.Equal(Clip with { AlongY = Length.Inches(3) }, edit.Cut);
        Assert.Equal($"Set Apron's {Clip.Site} to 45° off square", edit.What);
    }

    [Fact]
    public void When_the_longer_setback_is_along_y_the_angle_changes_x()
    {
        CornerCut tall = new(BoxCorner.SouthWest, Length.Inches(1), Length.Inches(4));

        CutEdit edit = Assert.IsType<CutEdit>(CutEntry.Read(Blank, tall, new TypedCut("1", "4", "45°"), Format));

        Assert.Equal(tall with { AlongX = Length.Inches(4) }, edit.Cut);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("90")]
    [InlineData("-10")]
    public void An_angle_that_is_not_between_square_and_ninety_is_refused(string angle) =>
        Assert.Equal(
            new CutEntryProblem("A cut is made at more than 0° and less than 90° off square."),
            CutEntry.Read(Blank, Clip, new TypedCut("3", "2", angle), Format));

    [Fact]
    public void An_angle_that_does_not_read_falls_back_to_the_setbacks()
    {
        CutEdit edit = Assert.IsType<CutEdit>(CutEntry.Read(Blank, Clip, new TypedCut("5", "1", "steep"), Format));

        Assert.Equal(Clip with { AlongX = Length.Inches(5), AlongY = Length.Inches(1) }, edit.Cut);
    }

    [Theory]
    [InlineData("three", "2")]
    [InlineData("3", "")]
    [InlineData("0", "2")]
    [InlineData("3", null)]
    public void A_setback_that_is_not_a_positive_length_changes_nothing_and_says_why(string? first, string? second) =>
        Assert.Equal(
            new CutEntryProblem("A setback has to be a length greater than zero, like 3/4\" or 1' 4 1/4\"."),
            CutEntry.Read(Blank, Clip, new TypedCut(first, second, null), Format));

    [Fact]
    public void A_radius_and_a_depth_are_read_from_the_first_field()
    {
        RoundedCorner round = new(BoxCorner.SouthEast, Length.Inches(1));
        CurvedEdge curve = new(BoxEdge.North, Bow.Inward, Length.Inches(1));

        CutEdit radius = Assert.IsType<CutEdit>(CutEntry.Read(Blank, round, new TypedCut("2", null, null), Format));
        CutEdit depth = Assert.IsType<CutEdit>(CutEntry.Read(Blank, curve, new TypedCut("3/4", null, null), Format));

        Assert.Equal(round with { Radius = Length.Inches(2) }, radius.Cut);
        Assert.Equal($"Set Apron's {round.Site} to a 2\" radius", radius.What);
        Assert.Equal(curve with { Depth = Length.Inches(0, 3, 4) }, depth.Cut);
        Assert.Equal($"Set Apron's {curve.Site} to 3/4\" deep", depth.What);
    }

    [Fact]
    public void A_radius_or_depth_that_does_not_read_says_which()
    {
        Assert.Equal(
            new CutEntryProblem("A radius has to be a length greater than zero, like 3/4\" or 1' 4 1/4\"."),
            CutEntry.Read(Blank, new RoundedCorner(BoxCorner.SouthEast, Length.Inches(1)), new TypedCut("-1", null, null), Format));
        Assert.Equal(
            new CutEntryProblem("A depth has to be a length greater than zero, like 3/4\" or 1' 4 1/4\"."),
            CutEntry.Read(Blank, new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(1)), new TypedCut("x", null, null), Format));
    }

    [Fact]
    public void A_full_mitre_takes_the_blanks_narrower_side_at_both_setbacks()
    {
        CutEdit mitre = CutEntry.FullMitre(Blank, BoxCorner.NorthWest, Format);

        Assert.Equal(new CornerCut(BoxCorner.NorthWest, Length.Inches(8), Length.Inches(8)), mitre.Cut);
        Assert.Equal($"Mitred Apron's {mitre.Cut.Site} the full 8\"", mitre.What);
    }
}
