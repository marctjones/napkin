using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Stretching one face of a part in the 3D view (<see cref="SpaceSnapResolver.ResolveFace"/>): which
/// face it catches, the tie rules when two could, and what the drop says. Also the small extent
/// helpers the resolver and the view share.
/// </summary>
/// <remarks>
/// The scene is a 2" x 2" x 10" leg standing at the origin, its top face at Z = 10", stretched
/// upward toward other parts. Every coordinate below is worked from those sizes by hand.
/// </remarks>
public class SpaceSnapResizeTests
{
    // Half an inch: Length is in 1/1024".
    static readonly Length Radius = new(512);

    static readonly Box Leg = new(EditingBuilder.Id(0), LayerId.Default, Point3.Inches(0, 0, 0), Length.Inches(2), Length.Inches(2), Length.Inches(10), BoxFace.Top, Angle.Zero);

    static Box Slab(int index, long x, long y, long z, long width, long height, long depth) =>
        new(EditingBuilder.Id(index), LayerId.Default, Point3.Inches(x, y, z), Length.Inches(width), Length.Inches(height), Length.Inches(depth), BoxFace.Top, Angle.Zero);

    static Sketch SketchOf(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty;
        foreach (Box box in boxes)
        {
            sketch = sketch.WithEntity(box);
        }

        return sketch;
    }

    static Length Inches(double inches) => Length.FromInches(inches, Rounding.HalfToEven);

    [Fact]
    public void A_top_stretched_up_under_a_table_top_meets_its_underside_and_says_flush()
    {
        // The table top is 1" thick, underside at 12", covering the leg's whole plan.
        Box top = Slab(1, -5, -5, 12, 20, 20, 1);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, top), Leg, BoxFace.Top, Inches(11.75), Radius);

        Assert.Equal(Leg.Anchor, plan.Anchor);
        SpaceSnapHit hit = Assert.Single(plan.Hits);
        Assert.Equal(new SpaceSnapHit(Axis.Z, Length.Inches(12), SnapKind.Edge, top.Id, BoxFace.Bottom, BoxFace.Top), hit);
        Flush flush = Assert.IsType<Flush>(Assert.Single(plan.Relationships));
        Assert.Equal(new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom)), flush.A);
        Assert.Equal(new FeatureRef(Leg.Id, BoxFeature.Face(BoxFace.Top)), flush.B);
        Assert.True(plan.CaughtSomething);
    }

    [Fact]
    public void Nothing_within_reach_leaves_the_face_where_the_pointer_put_it_on_the_grid()
    {
        Box top = Slab(1, -5, -5, 12, 20, 20, 1);

        // 11" is a whole inch below the underside: out of a half-inch reach.
        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, top), Leg, BoxFace.Top, Length.Inches(11), Radius);

        Assert.Equal(new SpaceSnapHit(Axis.Z, Length.Inches(11), SnapKind.Grid, null, null, null), Assert.Single(plan.Hits));
        Assert.Empty(plan.Relationships);
        Assert.False(plan.CaughtSomething);
    }

    [Fact]
    public void A_face_at_the_right_height_but_off_to_one_side_is_not_caught()
    {
        // Underside at 12" but 30" east: nowhere over the leg.
        Box shelf = Slab(1, 30, 0, 12, 10, 10, 1);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, shelf), Leg, BoxFace.Top, Inches(11.75), Radius);

        Assert.Equal(SnapKind.Grid, Assert.Single(plan.Hits).Kind);
    }

    [Fact]
    public void The_nearest_face_wins()
    {
        // Two slabs over the leg, undersides at 12" and 11". Wanted 11.25": 1/4" from 11, 3/4" from 12.
        Box far = Slab(1, -5, -5, 12, 20, 20, 1);
        Box near = Slab(2, -5, -5, 11, 20, 20, 1);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, far, near), Leg, BoxFace.Top, Inches(11.25), Radius);

        SpaceSnapHit hit = Assert.Single(plan.Hits);
        Assert.Equal(near.Id, hit.Target);
        Assert.Equal(Length.Inches(11), hit.Coordinate);
    }

    [Fact]
    public void At_the_same_distance_a_face_to_meet_beats_a_face_to_be_level_with()
    {
        // Both faces are at 12": a slab's underside (the leg's top would meet it) and another slab's
        // top (the leg's top would be level with it). The level one has the lower id, so it is seen
        // first and has to be displaced.
        Box level = Slab(1, 1, 0, 11, 4, 2, 1);
        Box meet = Slab(2, -5, -5, 12, 20, 20, 1);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, level, meet), Leg, BoxFace.Top, Length.Inches(12), Radius);

        SpaceSnapHit hit = Assert.Single(plan.Hits);
        Assert.Equal(meet.Id, hit.Target);
        Assert.Equal(BoxFace.Bottom, hit.TargetFace);
    }

    [Fact]
    public void The_face_to_meet_is_kept_when_a_level_face_comes_later_at_the_same_distance()
    {
        Box meet = Slab(1, -5, -5, 12, 20, 20, 1);
        Box level = Slab(2, 1, 0, 11, 4, 2, 1);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, meet, level), Leg, BoxFace.Top, Length.Inches(12), Radius);

        Assert.Equal(meet.Id, Assert.Single(plan.Hits).Target);
    }

    [Fact]
    public void Between_two_faces_to_meet_at_the_same_distance_the_one_overlapping_more_wins()
    {
        // Undersides both at 12". The first covers only x 1..2 of the leg's 0..2 (1" x 2" shared);
        // the second covers all of it (2" x 2").
        Box sliver = Slab(1, 1, 0, 12, 10, 2, 1);
        Box whole = Slab(2, -5, -5, 12, 20, 20, 1);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, sliver, whole), Leg, BoxFace.Top, Length.Inches(12), Radius);
        Assert.Equal(whole.Id, Assert.Single(plan.Hits).Target);

        // And the bigger overlap is kept when it is seen first.
        Box wholeFirst = Slab(1, -5, -5, 12, 20, 20, 1);
        Box sliverLater = Slab(2, 1, 0, 12, 10, 2, 1);
        plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, wholeFirst, sliverLater), Leg, BoxFace.Top, Length.Inches(12), Radius);
        Assert.Equal(wholeFirst.Id, Assert.Single(plan.Hits).Target);
    }

    [Fact]
    public void A_nearer_face_seen_second_displaces_a_farther_one()
    {
        Box far = Slab(1, -5, -5, 12, 20, 20, 1);
        Box near = Slab(2, -5, -5, 11, 20, 20, 1);

        // Wanted 11.5": exactly 1/2" from both, so each is within reach, and the tie goes to the
        // face to meet; both are faces to meet, so the overlap decides and they tie there too:
        // the first seen stays. Moving the pointer to 11.4" makes the second the nearer.
        SpaceSnapPlan tie = SpaceSnapResolver.ResolveFace(SketchOf(Leg, far, near), Leg, BoxFace.Top, Inches(11.5), Radius);
        Assert.Equal(far.Id, Assert.Single(tie.Hits).Target);

        SpaceSnapPlan nearer = SpaceSnapResolver.ResolveFace(SketchOf(Leg, far, near), Leg, BoxFace.Top, Inches(11.375), Radius);
        Assert.Equal(near.Id, Assert.Single(nearer.Hits).Target);
    }

    [Fact]
    public void A_part_off_the_quarter_turns_is_not_caught()
    {
        Box skewedTop = Slab(1, -5, -5, 12, 20, 20, 1) with { Rotation = Angle.Degrees(30) };

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, skewedTop), Leg, BoxFace.Top, Inches(11.75), Radius);

        Assert.Equal(SnapKind.Grid, Assert.Single(plan.Hits).Kind);
    }

    [Fact]
    public void A_side_face_stretched_east_catches_a_neighbour_s_west_face()
    {
        // A post whose west face is at x = 6"; the leg's east face (at 2") is pulled to 5.8".
        Box post = Slab(1, 6, 0, 0, 2, 2, 10);

        SpaceSnapPlan plan = SpaceSnapResolver.ResolveFace(SketchOf(Leg, post), Leg, BoxFace.East, Inches(5.75), Radius);

        Assert.Equal(new SpaceSnapHit(Axis.X, Length.Inches(6), SnapKind.Edge, post.Id, BoxFace.West, BoxFace.East), Assert.Single(plan.Hits));
    }

    [Fact]
    public void A_face_s_coordinate_is_the_end_of_the_extent_it_faces()
    {
        Assert.Equal(Length.Inches(10), SpaceSnapResolver.FaceCoordinate(Leg, BoxFace.Top));
        Assert.Equal(Length.Zero, SpaceSnapResolver.FaceCoordinate(Leg, BoxFace.Bottom));
        Assert.Equal(Length.Inches(2), SpaceSnapResolver.FaceCoordinate(Leg, BoxFace.East));

        // Lying on its south face: the turn that brings local -Y up to +Z takes local +Z to +Y and
        // local +Y to -Z. So local z in [0, 10] becomes Y in [0, 10], local y in [0, 2] becomes
        // Z in [-2, 0] (assembly-model §7.1's "South spans [-H, 0] in Z"), and the top is at Y = 10".
        Box lying = Leg with { FaceUp = BoxFace.South };
        Assert.Equal((Axis.Y, true), lying.Orientation.Normal(BoxFace.Top));
        Assert.Equal(Length.Inches(10), SpaceSnapResolver.FaceCoordinate(lying, BoxFace.Top));
        Assert.Equal(Length.Zero, SpaceSnapResolver.FaceCoordinate(lying, BoxFace.Bottom));
        Assert.Equal(Length.Inches(-2), SpaceSnapResolver.FaceCoordinate(lying, BoxFace.North));
    }

    [Fact]
    public void The_face_facing_a_way_is_found_for_every_turned_box()
    {
        Box lying = Leg with { FaceUp = BoxFace.South };

        Assert.Equal(BoxFace.South, SpaceSnapResolver.FaceFacing(lying, Axis.Z, true));
        Assert.Equal(BoxFace.Top, SpaceSnapResolver.FaceFacing(lying, Axis.Y, true));
        Assert.Equal(BoxFace.Bottom, SpaceSnapResolver.FaceFacing(lying, Axis.Y, false));
        Assert.Equal(BoxFace.North, SpaceSnapResolver.FaceFacing(lying, Axis.Z, false));
        Assert.Equal(BoxFace.Top, SpaceSnapResolver.FaceFacing(Leg, Axis.Z, true));
    }

    [Fact]
    public void The_helpers_need_their_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => SpaceSnapResolver.ResolveFace(null!, Leg, BoxFace.Top, Length.Zero, Radius));
        Assert.Throws<ArgumentNullException>(() => SpaceSnapResolver.ResolveFace(Sketch.Empty, null!, BoxFace.Top, Length.Zero, Radius));
        Assert.Throws<ArgumentNullException>(() => SpaceSnapResolver.FaceCoordinate(null!, BoxFace.Top));
        Assert.Throws<ArgumentNullException>(() => SpaceSnapResolver.FaceFacing(null!, Axis.Z, true));
        Assert.Throws<ArgumentNullException>(() => SpaceSnapResolver.Extent(null!));
    }

    [Fact]
    public void A_skewed_part_refuses_a_face_resize_instead_of_throwing_from_Normal()
    {
        // Not a right-angle multiple: the guard (checked before any Orientation.Normal call) must
        // catch this itself and name the part, rather than letting Normal's RequireExact throw
        // InvalidOperationException first (#183).
        Box skewed = Leg with { Rotation = Angle.Degrees(30) };

        ArgumentException thrown = Assert.Throws<ArgumentException>(
            () => SpaceSnapResolver.ResolveFace(SketchOf(skewed), skewed, BoxFace.Top, Inches(11.75), Radius));
        Assert.Equal("atPress", thrown.ParamName);
        Assert.Contains(skewed.Id.ToString(), thrown.Message);
    }
}
