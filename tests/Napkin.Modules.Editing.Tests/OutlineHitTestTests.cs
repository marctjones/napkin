using Napkin.Modules.Editing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// What a click lands on when the part it lands near has had something cut off it
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.4).
/// </summary>
/// <remarks>
/// Every case here is a point a person could put the pointer on, asked of the same function the
/// canvas asks. The shapes are chosen so that the answer is obvious by eye — a gusset's missing
/// half, a scallop's bite, a bow's belly — rather than a hair either side of a boundary.
/// </remarks>
public class OutlineHitTestTests
{
    static readonly LayerId Layer = LayerId.New();

    [Fact]
    public void A_plain_rectangle_is_all_there()
    {
        Box box = Blank(12, 8);

        Assert.True(BoxGeometry.ContainsShape(box, Point2.Inches(6, 4)));
        Assert.True(BoxGeometry.ContainsShape(box, Point2.Inches(11, 7)));
        Assert.False(BoxGeometry.ContainsShape(box, Point2.Inches(13, 4)));
    }

    [Fact]
    public void A_corner_cut_off_holds_nothing()
    {
        // The gusset of §2.5: a 6" square cut corner to corner, leaving the triangle below the
        // diagonal from the north-west corner to the south-east one.
        Box gusset = Blank(6, 6) with
        {
            Cuts = [new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(6))],
        };

        Assert.True(BoxGeometry.ContainsShape(gusset, Point2.Inches(1, 1)));
        Assert.False(
            BoxGeometry.ContainsShape(gusset, Point2.Inches(5, 5)),
            "the north-east corner was cut away, so a click there is a click on the paper.");

        // And the blank still holds it, which is what keeps grips and snap targets where they are.
        Assert.True(BoxGeometry.Contains(gusset, Point2.Inches(5, 5)));
    }

    [Fact]
    public void A_rounded_corner_keeps_what_is_inside_the_arc()
    {
        // A 12" x 8" top with a 4" roundover at the north-east corner. Its centre is (8", 4"),
        // so (11", 7") is 5" from the centre and gone, while (10", 5") is well inside.
        Box top = Blank(12, 8) with
        {
            Cuts = [new RoundedCorner(BoxCorner.NorthEast, Length.Inches(4))],
        };

        Assert.True(BoxGeometry.ContainsShape(top, Point2.Inches(10, 5)));
        Assert.True(BoxGeometry.ContainsShape(top, Point2.Inches(11, 3)));
        Assert.False(BoxGeometry.ContainsShape(top, Point2.Inches(11, 7)));
        Assert.False(BoxGeometry.ContainsShape(top, Point2.Inches(12, 8)));
    }

    [Fact]
    public void A_scallop_takes_a_bite_out_of_the_blank()
    {
        // An inward curve on the north edge, 3" deep: the middle of that edge is now 3" lower.
        Box apron = Blank(12, 8) with
        {
            Cuts = [new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(3))],
        };

        Assert.False(BoxGeometry.ContainsShape(apron, Point2.Inches(6, 7)));
        Assert.True(BoxGeometry.ContainsShape(apron, Point2.Inches(6, 4)));

        // The bite is a fair curve corner to corner, not a notch in the middle: a 12" chord with
        // a 3" sag has a 7 1/2" radius, so at an inch in from the west end the edge has already
        // dropped past 7" and (1", 7") is gone while (1", 6") is still material.
        Assert.False(BoxGeometry.ContainsShape(apron, Point2.Inches(1, 7)));
        Assert.True(BoxGeometry.ContainsShape(apron, Point2.Inches(1, 6)));
    }

    [Fact]
    public void A_bow_adds_the_belly_and_takes_the_corners()
    {
        // An outward curve on the north edge, 2" deep: the edge's ends are pulled 2" down the
        // east and west edges and the middle stays where it was, so the corners are gone and the
        // belly of the curve is material.
        Box rail = Blank(12, 8) with
        {
            Cuts = [new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2))],
        };

        Assert.True(BoxGeometry.ContainsShape(rail, Point2.Inches(6, 7)));
        Assert.False(BoxGeometry.ContainsShape(rail, Point2.Inches(0, 8)));
        Assert.False(BoxGeometry.ContainsShape(rail, Point2.Inches(12, 8)));
    }

    [Fact]
    public void A_rotated_part_is_cut_where_it_is_drawn()
    {
        // The same gusset turned a quarter turn: the cut corner is the one the part carries, so
        // it moves with the part rather than staying north-east on the paper.
        Box gusset = Blank(6, 6) with
        {
            Rotation = Angle.Degrees(90),
            Cuts = [new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(6))],
        };

        // Rotating about the anchor puts the blank over x in [-6", 0"], y in [0", 6"], and the
        // local north-east corner at (-6", 6").
        Assert.True(BoxGeometry.ContainsShape(gusset, Point2.Inches(-1, 1)));
        Assert.False(BoxGeometry.ContainsShape(gusset, Point2.Inches(-5, 5)));
    }

    [Fact]
    public void The_grab_tolerance_reaches_across_a_cut_edge_but_not_across_the_room()
    {
        Box gusset = Blank(6, 6) with
        {
            Cuts = [new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(6))],
        };

        // A point half an inch past the diagonal, with half an inch of slop, is a grab.
        Point2 justOutside = new(Length.Inches(3, 1, 2), Length.Inches(3));
        Assert.False(BoxGeometry.ContainsShape(gusset, justOutside));
        Assert.True(BoxGeometry.IsWithinShape(gusset, justOutside, Length.Inches(1)));
        Assert.False(BoxGeometry.IsWithinShape(gusset, justOutside, Length.Inches(0, 1, 8)));
    }

    [Fact]
    public void A_body_press_in_a_cut_corner_grips_nothing()
    {
        Box gusset = Blank(6, 6) with
        {
            Cuts = [new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(6))],
        };

        Length tolerance = Length.Inches(0, 1, 16);
        Assert.Equal(BoxGrip.Body, BoxGeometry.GripAt(gusset, Point2.Inches(1, 1), tolerance));
        Assert.Null(BoxGeometry.GripAt(gusset, Point2.Inches(5, 5), tolerance));

        // The corner handle is still on the blank's corner, cut or not (§2.4).
        Assert.Equal(
            BoxGrip.NorthEast,
            BoxGeometry.GripAt(gusset, Point2.Inches(6, 6), tolerance));
    }

    static Box Blank(long width, long height) => Box.AsDrawn(
        EntityId.New(),
        Layer,
        Point2.Origin,
        Length.Inches(width),
        Length.Inches(height),
        Box.DefaultDepth,
        Angle.Zero);
}
