using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// What a cut reads as at a bench: one golden per phrasing in
/// <c>docs/design/shaped-parts-model.md</c> §4.4 (test 14).
/// </summary>
/// <remarks>
/// <para>
/// The exact strings are pinned here rather than in the design document, which says so itself. If
/// one of these has to change, the question to ask is whether the new sentence is one a person
/// could follow holding the part the way they drew it.
/// </para>
/// <para>
/// Corners and edges are named by compass in the part's own frame, and every length is rendered by
/// <c>LengthFormat.Default</c> through <see cref="CutListCsv.Text"/> — so 16 1/4" reads as
/// <c>1'-4 1/4"</c> here, exactly as it does in the row's own length column.
/// </para>
/// </remarks>
public sealed class CutDescriptionTests
{
    /// <summary>A 48" x 24" x 3/4" top lying flat: length across X, width up Y.</summary>
    private static FinishedSize Top => new(new Length(49152), new Length(24576), new Length(768));

    private static PlanAxes Flat => new(PartDimension.Length, PartDimension.Width);

    /// <summary>A 48" x 5" x 3/4" board lying flat, whose 5" edge a mitre can take the whole of.</summary>
    private static FinishedSize Board => new(new Length(49152), new Length(5120), new Length(768));

    /// <summary>A 2 1/2" square leg 16 1/4" long, drawn as its footprint: the plan holds no length.</summary>
    private static FinishedSize Leg => new(new Length(16640), new Length(2560), new Length(2560));

    private static PlanAxes Footprint => new(PartDimension.Width, PartDimension.Thickness);

    [Fact]
    public void A_blank_with_nothing_cut_off_it_says_nothing()
        => Assert.Empty(CutDescription.Describe([], Top, Flat));

    [Fact]
    public void A_clipped_corner_names_the_two_marks_and_the_edges_they_are_on()
    {
        // Neither setback is the whole of its edge, so this is a corner clipped off: two marks and
        // a cut between them.
        Assert.Equal(
            [
                "Cut off the north-east corner: mark 3\" along the north edge and 5\" along the "
                + "east edge, and cut between the marks.",
            ],
            Describe(Top, Flat, new CornerCut(BoxCorner.NorthEast, new Length(3072), new Length(5120))));
    }

    [Fact]
    public void A_mitre_names_the_end_it_takes_and_the_angle_it_is_cut_at()
    {
        // The 5" east edge is gone entirely, which is what makes this a mitred end rather than a
        // clip: from a mark 3" along the north edge to the far corner. atan(3/5) = 30.96°, which
        // is 31° to the nearest half degree and is not exact, so it says so.
        Assert.Equal(
            [
                "Mitre the east end: from 3\" in along the north edge to the south-east corner "
                + "(≈31° off square).",
            ],
            Describe(Board, Flat, new CornerCut(BoxCorner.NorthEast, new Length(3072), new Length(5120))));
    }

    [Fact]
    public void Equal_setbacks_are_45_degrees_exactly_and_are_not_marked_approximate()
    {
        // The one angle two exact setbacks can land on exactly: 3" along a 3" end.
        FinishedSize board = new(new Length(49152), new Length(3072), new Length(768));

        Assert.Equal(
            [
                "Mitre the east end: from 3\" in along the north edge to the south-east corner "
                + "(45° off square).",
            ],
            Describe(board, Flat, new CornerCut(BoxCorner.NorthEast, new Length(3072), new Length(3072))));
    }

    [Fact]
    public void A_taper_takes_the_whole_of_a_long_edge_and_reads_as_a_mitre_too()
    {
        // The other way round from the golden above: here the setback along X is the whole of the
        // north edge, so the north end is what goes and the mark is on the east edge. A 1" mark
        // over 10" is atan(0.1) = 5.71°, which is 5.5° to the nearest half degree — the case the
        // angle is written with a decimal in.
        FinishedSize board = new(new Length(10240), new Length(3072), new Length(768));

        Assert.Equal(
            [
                "Mitre the north end: from 1\" in along the east edge to the north-west corner "
                + "(≈5.5° off square).",
            ],
            Describe(board, Flat, new CornerCut(BoxCorner.NorthEast, new Length(10240), new Length(1024))));
    }

    [Fact]
    public void A_mitre_at_the_other_end_of_an_edge_names_the_corner_at_the_far_end_of_it()
    {
        // The same cut at the south-west corner rather than the north-east: the west end goes, the
        // mark is on the south edge, and the corner the cut runs to is the north-west one — the
        // other end of the edge the cut takes the whole of. atan(1/3) = 18.43°, so 18.5°.
        FinishedSize board = new(new Length(10240), new Length(3072), new Length(768));

        Assert.Equal(
            [
                "Mitre the west end: from 1\" in along the south edge to the north-west corner "
                + "(≈18.5° off square).",
            ],
            Describe(board, Flat, new CornerCut(BoxCorner.SouthWest, new Length(1024), new Length(3072))));
    }

    [Fact]
    public void Both_setbacks_whole_is_the_diagonal_and_reads_corner_to_corner()
    {
        Assert.Equal(
            ["Cut corner to corner, from the north-west corner to the south-east corner."],
            Describe(Top, Flat, new CornerCut(BoxCorner.NorthEast, new Length(49152), new Length(24576))));
    }

    [Fact]
    public void A_rounded_corner_names_its_radius()
        => Assert.Equal(
            ["Round the north-east corner to a 1\" radius."],
            Describe(Top, Flat, new RoundedCorner(BoxCorner.NorthEast, new Length(1024))));

    [Fact]
    public void Like_cuts_at_several_sites_are_said_once()
    {
        // Two of them list both corners; all four of them say so (§4.4, "like cuts are said once").
        Assert.Equal(
            ["Round the north-east and north-west corners to a 1\" radius."],
            Describe(
                Top,
                Flat,
                new RoundedCorner(BoxCorner.NorthEast, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthWest, new Length(1024))));

        Assert.Equal(
            ["Round all four corners to a 1\" radius."],
            Describe(
                Top,
                Flat,
                new RoundedCorner(BoxCorner.SouthWest, new Length(1024)),
                new RoundedCorner(BoxCorner.SouthEast, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthEast, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthWest, new Length(1024))));

        // Three of them are listed with a comma and an "and", which is only "all four corners"
        // when it really is all four.
        Assert.Equal(
            ["Round the south-west, south-east and north-east corners to a 1\" radius."],
            Describe(
                Top,
                Flat,
                new RoundedCorner(BoxCorner.SouthWest, new Length(1024)),
                new RoundedCorner(BoxCorner.SouthEast, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthEast, new Length(1024))));

        // Not the same cut: two radii are two sentences, in site order.
        Assert.Equal(
            [
                "Round the south-west corner to a 1\" radius.",
                "Round the north-east corner to a 1/2\" radius.",
            ],
            Describe(
                Top,
                Flat,
                new RoundedCorner(BoxCorner.SouthWest, new Length(1024)),
                new RoundedCorner(BoxCorner.NorthEast, new Length(512))));
    }

    [Fact]
    public void A_square_clip_at_several_corners_is_one_sentence_too()
    {
        // With both setbacks the same there is no edge to name, so the sentence reads the same at
        // every corner and the sites collapse. An uneven clip has to say which edge carries which
        // mark, and so stands on its own — which the clipped-corner golden above pins.
        Assert.Equal(
            [
                "Cut off the north-east and north-west corners: mark 1/4\" along each edge from "
                + "the corner, and cut between the marks.",
            ],
            Describe(
                Top,
                Flat,
                new CornerCut(BoxCorner.NorthEast, new Length(256), new Length(256)),
                new CornerCut(BoxCorner.NorthWest, new Length(256), new Length(256))));
    }

    [Fact]
    public void An_outward_curve_marks_both_ends_and_gives_the_radius()
    {
        // A 4'-0" edge bowed 1" is a circle of exactly 288 1/2" — (48² + 4)/8 in inches — so the
        // radius carries no ≈: the text is the whole truth.
        Assert.Equal(
            [
                "Curve the north edge: mark 1\" in from each end on the east and west edges, draw "
                + "a fair curve from mark to mark through the middle of the north edge, and cut it "
                + "(24'-0 1/2\" radius).",
            ],
            Describe(Top, Flat, new CurvedEdge(BoxEdge.North, Bow.Outward, new Length(1024))));
    }

    [Fact]
    public void A_curve_on_an_end_marks_its_two_ends_on_the_north_and_south_edges()
    {
        // The edges a curve's marks go on are the ones it meets at its ends, so an end curve's
        // are the north and south edges rather than the east and west. The 2'-0" west edge bowed
        // 1" is a circle of exactly 72 1/2".
        Assert.Equal(
            [
                "Curve the west edge: mark 1\" in from each end on the north and south edges, draw "
                + "a fair curve from mark to mark through the middle of the west edge, and cut it "
                + "(6'-0 1/2\" radius).",
            ],
            Describe(Top, Flat, new CurvedEdge(BoxEdge.West, Bow.Outward, new Length(1024))));
    }

    [Fact]
    public void A_radius_that_is_not_exact_at_a_sixteenth_says_so()
    {
        // (49152² + 4 x 5120²) / (8 x 5120) = 61542.4 units, which is not a whole unit and is not
        // exact at 1/16" either: 60.0996" shown as 5'-0 1/8".
        Assert.Equal(
            [
                "Curve the north edge: mark 5\" in from each end on the east and west edges, draw "
                + "a fair curve from mark to mark through the middle of the north edge, and cut it "
                + "(≈5'-0 1/8\" radius).",
            ],
            Describe(Top, Flat, new CurvedEdge(BoxEdge.North, Bow.Outward, new Length(5120))));
    }

    [Fact]
    public void An_inward_curve_is_a_scallop_from_corner_to_corner()
    {
        // The corners stay and the middle goes in 2": (48² + 4 x 2²) / (8 x 2) = 145" = 12'-1".
        Assert.Equal(
            [
                "Scallop the north edge: mark 2\" in at the middle, draw a fair curve from corner "
                + "to corner through the mark, and cut it (12'-1\" radius).",
            ],
            Describe(Top, Flat, new CurvedEdge(BoxEdge.North, Bow.Inward, new Length(2048))));
    }

    [Fact]
    public void A_cut_that_does_not_go_through_the_thickness_says_how_far_it_runs()
    {
        // A leg drawn as its footprint: the plan holds the width and the thickness, so a rounded
        // corner is a roundover the whole 16 1/4" length and the sentence has to say so.
        Assert.Equal(
            ["Round the north-east corner to a 1/4\" radius, for the full 1'-4 1/4\" length."],
            Describe(Leg, Footprint, new RoundedCorner(BoxCorner.NorthEast, new Length(256))));

        // The same cut on a top lying flat goes through the thickness, which is what "cut" means,
        // so nothing is added.
        Assert.Equal(
            ["Round the north-east corner to a 1/4\" radius."],
            Describe(Top, Flat, new RoundedCorner(BoxCorner.NorthEast, new Length(256))));
    }

    [Fact]
    public void An_apron_drawn_on_edge_says_its_width_rather_than_its_length()
    {
        // The plan holds this apron's length and its thickness, so the dimension a cut runs
        // through is the 3 1/2" face it shows: an eased end, rounded the whole width of it. The
        // same rule as the leg's, said about a width instead of a length.
        FinishedSize apron = new(new Length(40960), new Length(3584), new Length(768));
        PlanAxes onEdge = new(PartDimension.Length, PartDimension.Thickness);

        Assert.Equal(
            ["Round the north-east corner to a 1/4\" radius, for the full 3 1/2\" width."],
            Describe(apron, onEdge, new RoundedCorner(BoxCorner.NorthEast, new Length(256))));
    }

    [Fact]
    public void A_radius_that_falls_between_two_units_is_rounded_to_the_nearer()
    {
        // (49152² + 4 x 1280²) / (8 x 1280) = 236569 units and 6144 left over, which is more than
        // half of 10240, so the radius is 236570 units — 231.025", shown at 1/16" as 19'-3".
        Assert.Equal(
            [
                "Curve the north edge: mark 1 1/4\" in from each end on the east and west edges, "
                + "draw a fair curve from mark to mark through the middle of the north edge, and "
                + "cut it (≈19'-3\" radius).",
            ],
            Describe(Top, Flat, new CurvedEdge(BoxEdge.North, Bow.Outward, new Length(1280))));
    }

    [Fact]
    public void A_corner_or_an_edge_the_compass_does_not_name_is_refused()
    {
        // Not reachable through a file, whose corner and edge names the reader validates, nor
        // through the canvas. It is reachable through the API, and a cut list that quietly said
        // "Round the 7 corner" would be worse than one that stopped.
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Describe(Top, Flat, new RoundedCorner((BoxCorner)7, new Length(1024))));

        Assert.Throws<ArgumentOutOfRangeException>(
            () => Describe(Top, Flat, new CurvedEdge((BoxEdge)7, Bow.Outward, new Length(1024))));
    }

    private static ImmutableArray<string> Describe(FinishedSize size, PlanAxes axes, params Cut[] cuts)
        => CutDescription.Describe([.. cuts], size, axes);
}
