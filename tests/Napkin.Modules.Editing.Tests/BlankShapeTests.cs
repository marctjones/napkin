using System.Collections.Immutable;
using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Which corners and edges of a blank a cut has taken away — what the canvas marks with a cross
/// and a faint line so a relationship never looks bound to nothing
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.1, &#xA7;7.2).
/// </summary>
public class BlankShapeTests
{
    static readonly LayerId Layer = LayerId.New();

    [Fact]
    public void A_plain_blank_has_no_virtual_corners_and_no_setbacks()
    {
        Box box = Blank();

        foreach (BoxCorner corner in (BoxCorner[])
                 [BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
        {
            Assert.False(BlankShape.IsVirtualCorner(box, corner));
            foreach (BoxEdge edge in BlankShape.EdgesAt(corner))
            {
                Assert.Equal(Length.Zero, BlankShape.SetbackAlong(box, corner, edge));
            }
        }
    }

    [Fact]
    public void A_corner_cut_claims_its_own_setback_on_each_edge()
    {
        Box box = Blank() with
        {
            Cuts = [new CornerCut(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(5))],
        };

        Assert.True(BlankShape.IsVirtualCorner(box, BoxCorner.NorthEast));
        Assert.False(BlankShape.IsVirtualCorner(box, BoxCorner.NorthWest));

        // The north edge runs along local X and the east edge along local Y.
        Assert.Equal(Length.Inches(3), BlankShape.SetbackAlong(box, BoxCorner.NorthEast, BoxEdge.North));
        Assert.Equal(Length.Inches(5), BlankShape.SetbackAlong(box, BoxCorner.NorthEast, BoxEdge.East));

        // The far end of the north edge still reaches its corner.
        Assert.Equal(Length.Zero, BlankShape.SetbackAlong(box, BoxCorner.NorthWest, BoxEdge.North));
    }

    [Fact]
    public void A_roundover_claims_its_radius_both_ways()
    {
        Box box = Blank() with { Cuts = [new RoundedCorner(BoxCorner.SouthWest, Length.Inches(2))] };

        Assert.True(BlankShape.IsVirtualCorner(box, BoxCorner.SouthWest));
        Assert.Equal(Length.Inches(2), BlankShape.SetbackAlong(box, BoxCorner.SouthWest, BoxEdge.South));
        Assert.Equal(Length.Inches(2), BlankShape.SetbackAlong(box, BoxCorner.SouthWest, BoxEdge.West));
    }

    [Fact]
    public void An_outward_curve_takes_both_its_corners_and_an_inward_one_keeps_them()
    {
        Box bowed = Blank() with { Cuts = [new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2))] };

        Assert.True(BlankShape.IsVirtualCorner(bowed, BoxCorner.NorthEast));
        Assert.True(BlankShape.IsVirtualCorner(bowed, BoxCorner.NorthWest));
        Assert.False(BlankShape.IsVirtualCorner(bowed, BoxCorner.SouthEast));

        // The curve starts two inches down the edges that meet its own, not along itself.
        Assert.Equal(Length.Inches(2), BlankShape.SetbackAlong(bowed, BoxCorner.NorthEast, BoxEdge.East));
        Assert.Equal(Length.Zero, BlankShape.SetbackAlong(bowed, BoxCorner.NorthEast, BoxEdge.North));

        Box scalloped = Blank() with { Cuts = [new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(2))] };
        Assert.False(BlankShape.IsVirtualCorner(scalloped, BoxCorner.NorthEast));
        Assert.Equal(Length.Zero, BlankShape.SetbackAlong(scalloped, BoxCorner.NorthEast, BoxEdge.East));
    }

    [Fact]
    public void A_relationship_names_the_corners_and_edges_it_holds()
    {
        EntityId a = EntityId.New();
        EntityId b = EntityId.New();

        Coincident coincident = new(
            RelationshipId.New(),
            LocalFeatures.Corner(a, BoxCorner.NorthEast),
            LocalFeatures.Corner(b, BoxCorner.NorthEast));

        Assert.Equal(
            [(a, BoxCorner.NorthEast), (b, BoxCorner.NorthEast)],
            RelationshipSites.CornersOf(coincident));
        Assert.Empty(RelationshipSites.EdgesOf(coincident));

        Flush flush = new(
            RelationshipId.New(),
            LocalFeatures.Edge(a, BoxEdge.East),
            LocalFeatures.Edge(b, BoxEdge.West));

        Assert.Equal(
            [(a, BoxEdge.East), (b, BoxEdge.West)],
            RelationshipSites.EdgesOf(flush));
        Assert.Empty(RelationshipSites.CornersOf(flush));

        // A relationship about a size holds no corner and no edge line.
        Assert.Empty(RelationshipSites.CornersOf(new Anchored(RelationshipId.New(), a)));
        Assert.Empty(RelationshipSites.EdgesOf(new Anchored(RelationshipId.New(), a)));
    }

    static Box Blank() => Box.AsDrawn(
        EntityId.New(),
        Layer,
        Point2.Origin,
        Length.Inches(12),
        Length.Inches(8),
        Box.DefaultDepth,
        Angle.Zero);
}

/// <summary>
/// The compass words a cut site is named by, checked against the words the rest of the canvas
/// uses for the same corner and side of a part as drawn, and which two edges meet at each corner.
/// </summary>
public class BlankShapeWordTests
{
    static readonly Box AsDrawn = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(0, 0), Length.Inches(10), Length.Inches(4), Length.Inches(1), Angle.Zero);

    public static TheoryData<BoxCorner> Corners => new() { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest };

    public static TheoryData<BoxEdge, Axis, bool> Edges => new()
    {
        { BoxEdge.South, Axis.Y, false },
        { BoxEdge.East, Axis.X, true },
        { BoxEdge.North, Axis.Y, true },
        { BoxEdge.West, Axis.X, false },
    };

    [Theory]
    [MemberData(nameof(Corners))]
    public void A_corner_site_is_named_as_the_canvas_names_that_upright_corner(BoxCorner corner) =>
        Assert.Equal(WorldWords.Feature(AsDrawn, BoxFeature.LocalUpright(corner)), BlankShape.Words(CutSite.Corner(corner)));

    [Theory]
    [MemberData(nameof(Edges))]
    public void An_edge_is_named_by_the_way_it_faces_on_a_part_as_drawn(BoxEdge edge, Axis axis, bool positive)
    {
        Assert.Equal(WorldWords.Facing(axis, positive), BlankShape.Compass(edge));
        Assert.Equal($"{BlankShape.Compass(edge)} edge", BlankShape.Words(CutSite.Edge(edge)));
    }

    [Theory]
    [MemberData(nameof(Corners))]
    public void The_two_edges_at_a_corner_are_the_two_its_name_is_made_of(BoxCorner corner)
    {
        ImmutableArray<BoxEdge> edges = BlankShape.EdgesAt(corner);
        string name = BlankShape.Words(CutSite.Corner(corner));

        Assert.Equal(2, edges.Distinct().Count());
        Assert.All(edges, edge => Assert.Contains(BlankShape.Compass(edge), name, StringComparison.Ordinal));
    }

    [Fact]
    public void Values_outside_the_four_corners_and_edges_are_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BlankShape.Compass((BoxEdge)42));
        Assert.Throws<ArgumentOutOfRangeException>(() => BlankShape.EdgesAt((BoxCorner)42));
    }

    [Fact]
    public void The_shape_queries_need_a_box()
    {
        Assert.Throws<ArgumentNullException>(() => BlankShape.CutAt(null!, CutSite.Edge(BoxEdge.South)));
        Assert.Throws<ArgumentNullException>(() => BlankShape.IsVirtualCorner(null!, BoxCorner.SouthWest));
        Assert.Throws<ArgumentNullException>(() => BlankShape.SetbackAlong(null!, BoxCorner.SouthWest, BoxEdge.South));
    }
}
