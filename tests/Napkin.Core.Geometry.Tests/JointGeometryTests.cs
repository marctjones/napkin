using Napkin.Core.Geometry;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// What a joint's two drawn boxes imply (docs/design/joinery-and-fasteners.md &#xA7;4.1, &#xA7;4.2),
/// on the DIY coffee table of &#xA7;2. Every box is built from the stated dimensions and every
/// expectation is worked out by hand in a comment, in inches; napkin's own output is never the source.
/// </summary>
public class JointGeometryTests
{
    private static int next;

    // Whole and fractional inches, exact on the 1/1024 grid: the test's own arithmetic, not napkin's.
    private static Length In(double inches) => new((long)(inches * 1024));

    private static Point3 At(double x, double y, double z) => new(In(x), In(y), In(z));

    private static PlanAxes Plan(PartDimension x, PartDimension y) => new(x, y);

    private static readonly PlanAxes LengthWidth = Plan(PartDimension.Length, PartDimension.Width);
    private static readonly PlanAxes LengthThickness = Plan(PartDimension.Length, PartDimension.Thickness);
    private static readonly PlanAxes ThicknessLength = Plan(PartDimension.Thickness, PartDimension.Length);
    private static readonly PlanAxes ThicknessWidth = Plan(PartDimension.Thickness, PartDimension.Width);
    private static readonly PlanAxes WidthThickness = Plan(PartDimension.Width, PartDimension.Thickness);
    private static readonly PlanAxes WidthLength = Plan(PartDimension.Width, PartDimension.Length);

    // A box lying as drawn (faceUp top), so its local axes are the world axes: x, y, z sizes and a part.
    private static Box Piece(Point3 anchor, double x, double y, double z, PlanAxes axes)
    {
        EntityId id = new(new Guid(++next, 0, 0, new byte[8]));
        return new Box(id, LayerId.Default, anchor, In(x), In(y), In(z), BoxFace.Top, Angle.Zero)
        {
            Part = new Part("stock", null, 1, axes),
        };
    }

    private static Sketch Sketched(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty;
        foreach (Box box in boxes)
        {
            sketch = sketch.WithEntity(box);
        }

        return sketch;
    }

    private static Joint Between(Box receiving, BoxFace receivingFace, Box inserted, BoxFace insertedFace, JointType type, Length? depth = null, Fastening? fastening = null)
        => new(
            new RelationshipId(new Guid(++next, 1, 0, new byte[8])),
            new FeatureRef(receiving.Id, BoxFeature.Face(receivingFace)),
            new FeatureRef(inserted.Id, BoxFeature.Face(insertedFace)),
            type,
            depth,
            fastening ?? Fastening.None,
            false);

    // ---- J1: the back apron's west end against the north-west leg's east face ----
    // Leg, north-west: 1 1/2 x 1 1/2 x 16 1/4, x 1.5..3, y 19..20.5, z 0..16.25 (its width x, thickness y, length z).
    // Back apron: 36 x 3/4 x 5 1/2 drawn as x 36 (length), y 3/4 (thickness), z 5 1/2 (width), against the outside face
    // y 20.5: x 3..39, y 19.75..20.5, z 10.75..16.25.
    private static (Sketch Sketch, Box Leg, Box Apron) J1()
    {
        Box leg = Piece(At(1.5, 19, 0), 1.5, 1.5, 16.25, WidthThickness);
        Box apron = Piece(At(3, 19.75, 10.75), 36, 0.75, 5.5, LengthThickness);
        return (Sketched(leg, apron), leg, apron);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J1_an_apron_end_on_a_leg_face_touches_over_three_quarters_by_five_and_a_half()
    {
        (Sketch sketch, Box leg, Box apron) = J1();
        Joint joint = Between(leg, BoxFace.East, apron, BoxFace.West, JointType.Butt, fastening: new Fastening(FasteningKind.PocketScrews, null, BoxFace.South));

        JointShape shape = JointGeometry.Of(sketch.WithRelationship(joint), joint)!;

        // Plane x = 3. y: leg 19..20.5 meets apron 19.75..20.5 -> 19.75..20.5 (3/4). z: leg 0..16.25 meets apron 10.75..16.25 -> 5 1/2.
        Assert.Equal(Axis.X, shape.Contact.Normal);
        Assert.Equal(At(3, 19.75, 10.75), shape.Contact.Low);
        Assert.Equal(At(3, 20.5, 16.25), shape.Contact.High);
        Assert.Equal(In(5.5), shape.JointLength);
        Assert.Equal(In(0.75), shape.JointWidth);

        // Pocket screws go into the apron's end that is in the contact: its west end (its length is along x).
        Assert.Equal(BoxFace.West, shape.PocketEnd);
        Assert.Null(shape.Groove);
        Assert.Null(shape.Rabbet);
        Assert.Null(shape.Lap);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J1_the_apron_is_the_inserted_part_because_its_contact_face_is_an_end_and_the_leg_is_not_asked_about()
    {
        (Sketch sketch, Box leg, Box apron) = J1();
        FeatureRef legFace = new(leg.Id, BoxFeature.Face(BoxFace.East));
        FeatureRef apronFace = new(apron.Id, BoxFeature.Face(BoxFace.West));

        // Whichever order the person picked them in.
        foreach ((FeatureRef first, FeatureRef second) in new[] { (legFace, apronFace), (apronFace, legFace) })
        {
            JointRoles roles = JointGeometry.ProposeRoles(sketch, JointType.Butt, first, second)!;

            Assert.Equal(legFace, roles.Receiving);
            Assert.Equal(apronFace, roles.Inserted);
            Assert.False(roles.Ambiguous);
        }
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void Two_parts_touch_on_exactly_one_pair_of_faces_and_two_far_apart_touch_on_none()
    {
        (Sketch sketch, Box leg, Box apron) = J1();
        Box far = Piece(At(100, 100, 0), 1, 1, 1, LengthWidth);
        sketch = sketch.WithEntity(far);

        TouchingFaces pair = Assert.Single(JointGeometry.TouchingFaces(sketch, leg.Id, apron.Id));

        Assert.Equal(BoxFace.East, pair.OfFirst);
        Assert.Equal(BoxFace.West, pair.OfSecond);
        Assert.Equal(In(5.5), pair.Contact.JointLength);
        Assert.Empty(JointGeometry.TouchingFaces(sketch, leg.Id, far.Id));
        Assert.Empty(JointGeometry.TouchingFaces(sketch, leg.Id, leg.Id));
        Assert.Empty(JointGeometry.TouchingFaces(sketch, leg.Id, new EntityId(Guid.NewGuid())));
    }

    // ---- J6: the slide cleat's face on the west side apron's inside face ----
    // Side apron, west: 16 x 3/4 x 5 1/2 drawn as x 3/4 (thickness), y 16 (length), z 5 1/2 (width): x 1.5..2.25, y 3..19, z 10.75..16.25.
    // Cleat: 1x2, 16 x 1 1/2 x 3/4: x 2.25..3 (thickness), y 3..19 (length), z 12..13.5 (width).
    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J6_a_cleat_face_on_an_apron_face_touches_over_sixteen_by_one_and_a_half_and_asks_which_receives()
    {
        Box apron = Piece(At(1.5, 3, 10.75), 0.75, 16, 5.5, ThicknessLength);
        Box cleat = Piece(At(2.25, 3, 12), 0.75, 16, 1.5, ThicknessLength);
        Sketch sketch = Sketched(apron, cleat);
        Joint joint = Between(apron, BoxFace.East, cleat, BoxFace.West, JointType.Butt);

        JointShape shape = JointGeometry.Of(sketch.WithRelationship(joint), joint)!;

        // y: both 3..19 -> 16. z: apron 10.75..16.25 meets cleat 12..13.5 -> 1 1/2.
        Assert.Equal(In(16), shape.JointLength);
        Assert.Equal(In(1.5), shape.JointWidth);
        Assert.Null(shape.PocketEnd);   // no pocket screws, and no end is in the contact anyway

        // Neither contact face is an end (both are long faces): the rule cannot decide, so the tool asks,
        // proposing the larger part (the apron, 3/4 x 16 x 5 1/2, against the cleat's 3/4 x 16 x 1 1/2) as receiving.
        JointRoles roles = JointGeometry.ProposeRoles(sketch, JointType.Butt, joint.Inserted, joint.Receiving)!;
        Assert.True(roles.Ambiguous);
        Assert.Equal(joint.Receiving, roles.Receiving);
        Assert.Equal(joint.Inserted, roles.Inserted);
    }

    // ---- J9: the drawer bottom's edge in the drawer side's groove ----
    // Drawer side, left, A: 16 x 3 1/2 x 1/2 drawn as x 1/2 (thickness), y 16 (length), z 3 1/2 (width): x 4..4.5, y 3..19, z 11..14.5.
    // Drawer bottom: 15 5/8 x 15 x 1/4: x 4.5..20.125, y 3.5..18.5, z 11.5..11.75.
    private static (Sketch Sketch, Box Side, Box Bottom) J9()
    {
        Box side = Piece(At(4, 3, 11), 0.5, 16, 3.5, ThicknessLength);
        Box bottom = Piece(At(4.5, 3.5, 11.5), 15.625, 15, 0.25, LengthWidth);
        return (Sketched(side, bottom), side, bottom);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J9_a_panel_edge_in_a_side_is_a_groove_along_the_length_half_an_inch_from_the_bottom_edge_a_quarter_wide()
    {
        (Sketch sketch, Box side, Box bottom) = J9();
        Joint joint = Between(side, BoxFace.East, bottom, BoxFace.West, JointType.Groove, In(0.25));

        JointShape shape = JointGeometry.Of(sketch.WithRelationship(joint), joint)!;

        // Contact: y 3.5..18.5 (15) by z 11.5..11.75 (1/4) on the plane x = 4.5.
        Assert.Equal(In(15), shape.JointLength);
        Assert.Equal(In(0.25), shape.JointWidth);
        GrooveShape groove = shape.Groove!;

        // The long side (15) is along y, which is the side's length -> a groove, not a dado.
        Assert.Equal(GrooveDirection.AlongLength, groove.Direction);
        Assert.Equal(In(0.25), groove.Width);                // the panel's thickness: 1/4
        Assert.Equal(In(0.25), groove.Depth);
        Assert.Equal(In(0.5), groove.Offset);                // 11.5 - 11: nearer the bottom (2 3/4 from the top)
        Assert.Equal(BoxFace.Bottom, groove.OffsetFrom);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J9_the_groove_offset_follows_the_bottom_when_it_is_resized_or_moved_and_the_near_edge_switches_sides()
    {
        (Sketch sketch, Box side, Box bottom) = J9();
        Joint joint = Between(side, BoxFace.East, bottom, BoxFace.West, JointType.Groove, In(0.25));

        // Raised to z 13..13.25 the panel is 13 - 11 = 2 from the bottom and 14.5 - 13.25 = 1 1/4 from the top: the top is nearer.
        Sketch raised = sketch.WithEntity(bottom with { Anchor = At(4.5, 3.5, 13) }).WithRelationship(joint);
        GrooveShape groove = JointGeometry.Of(raised, joint)!.Groove!;

        Assert.Equal(In(1.25), groove.Offset);
        Assert.Equal(BoxFace.Top, groove.OffsetFrom);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J9_the_thinner_part_is_the_inserted_panel_and_two_equal_thicknesses_ask()
    {
        (Sketch sketch, Box side, Box bottom) = J9();
        FeatureRef sideFace = new(side.Id, BoxFeature.Face(BoxFace.East));
        FeatureRef bottomFace = new(bottom.Id, BoxFeature.Face(BoxFace.West));

        // Thickness: the side 1/2 (part thickness), the bottom 1/4: the bottom is inserted whichever way round.
        JointRoles roles = JointGeometry.ProposeRoles(sketch, JointType.Groove, bottomFace, sideFace)!;
        Assert.Equal(sideFace, roles.Receiving);
        Assert.Equal(bottomFace, roles.Inserted);
        Assert.False(roles.Ambiguous);

        // Two panels of equal thickness: undecided; the larger by volume receives.
        Box twin = Piece(At(0, 0, 0), 15.625, 15, 0.25, LengthWidth);
        JointRoles tie = JointGeometry.ProposeRoles(sketched(twin, bottom), JointType.Groove, new FeatureRef(twin.Id, BoxFeature.Face(BoxFace.East)), bottomFace)!;
        Assert.True(tie.Ambiguous);

        static Sketch sketched(params Box[] boxes) => Sketched(boxes);
    }

    // ---- A dado: a shelf into a side ----
    // Side: 3/4 x 12 x 30 tall, drawn x 3/4 (thickness), y 12 (width), z 30 (length): x 0..0.75, y 0..12, z 0..30.
    // Shelf: 24 x 12 x 3/4: x 0.75..24.75, y 0..12, z 10..10.75.
    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_shelf_into_a_tall_side_is_a_dado_across_the_width_ten_inches_from_the_bottom_end()
    {
        Box side = Piece(At(0, 0, 0), 0.75, 12, 30, ThicknessWidth);
        Box shelf = Piece(At(0.75, 0, 10), 24, 12, 0.75, LengthWidth);
        Joint joint = Between(side, BoxFace.East, shelf, BoxFace.West, JointType.Groove, In(0.25));

        GrooveShape groove = JointGeometry.Of(Sketched(side, shelf).WithRelationship(joint), joint)!.Groove!;

        // Contact y 0..12 (12) by z 10..10.75 (3/4). The side's length is z, the long side is y: across the width.
        Assert.Equal(GrooveDirection.AcrossWidth, groove.Direction);
        Assert.Equal(In(0.75), groove.Width);
        Assert.Equal(In(10), groove.Offset);             // 10 from the bottom end, 30 - 10.75 = 19 1/4 from the top
        Assert.Equal(BoxFace.Bottom, groove.OffsetFrom);
    }

    // ---- J7: the drawer box front's end in a rabbet at each end of the sides ----
    // Drawer side, left: x 4..4.5, y 3..19, z 11..14.5 (length along y). Box front: 15 5/8 x 1/2 x 3 1/2, x 4.5..20.125,
    // y 3..3.5, z 11..14.5; the box back the same at y 18.5..19.
    [Trait("Feature", "GEO-017")]
    [Fact]
    public void J7_the_box_front_and_back_ends_are_rabbeted_into_the_south_and_north_ends_of_the_side_a_half_inch_wide()
    {
        Box side = Piece(At(4, 3, 11), 0.5, 16, 3.5, ThicknessLength);
        Box front = Piece(At(4.5, 3, 11), 15.625, 0.5, 3.5, LengthThickness);
        Box back = Piece(At(4.5, 18.5, 11), 15.625, 0.5, 3.5, LengthThickness);
        Sketch sketch = Sketched(side, front, back);
        Joint atFront = Between(side, BoxFace.East, front, BoxFace.West, JointType.Rabbet, In(0.25));
        Joint atBack = Between(side, BoxFace.East, back, BoxFace.West, JointType.Rabbet, In(0.25));
        sketch = sketch.WithRelationship(atFront).WithRelationship(atBack);

        JointShape shapeFront = JointGeometry.Of(sketch, atFront)!;
        JointShape shapeBack = JointGeometry.Of(sketch, atBack)!;

        // Contact y 3..3.5 (1/2) by z 11..14.5 (3 1/2): 1/2 x 3 1/2, as the note's J7.
        Assert.Equal(In(3.5), shapeFront.JointLength);
        Assert.Equal(In(0.5), shapeFront.JointWidth);

        // Front: 0 from the south end of the side (y 3), 15 1/2 from the north end -> the south end.
        Assert.Equal(new RabbetShape(In(0.5), In(0.25), BoxFace.South), shapeFront.Rabbet);
        Assert.Equal(new RabbetShape(In(0.5), In(0.25), BoxFace.North), shapeBack.Rabbet);

        // The box front's contact face is its west end: it is the inserted part.
        JointRoles roles = JointGeometry.ProposeRoles(sketch, JointType.Rabbet, atFront.Inserted, atFront.Receiving)!;
        Assert.Equal(atFront.Receiving, roles.Receiving);
        Assert.False(roles.Ambiguous);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_rabbet_on_an_end_face_has_no_end_to_be_at()
    {
        // The receiving face is itself the end (perpendicular to the length): nothing to cut a step into.
        Box post = Piece(At(0, 0, 0), 1.5, 1.5, 30, WidthThickness);
        Box cap = Piece(At(0, 0, 30), 1.5, 1.5, 0.75, WidthThickness);
        Joint joint = Between(post, BoxFace.Top, cap, BoxFace.Bottom, JointType.Rabbet, In(0.25));

        JointShape shape = JointGeometry.Of(Sketched(post, cap).WithRelationship(joint), joint)!;

        Assert.Null(shape.Rabbet);
    }

    // ---- A half-lap: two 1x2 rails crossing ----
    // Rail A along x: 24 x 1 1/2 x 3/4, x 0..24, y 10..11.5, z 0..0.75. Rail B along y: x 11..12.5, y 0..24, z 0..0.75.
    [Trait("Feature", "GEO-017")]
    [Fact]
    public void Two_crossing_rails_half_lap_in_a_one_and_a_half_inch_square_and_each_loses_three_eighths()
    {
        Box railA = Piece(At(0, 10, 0), 24, 1.5, 0.75, LengthWidth);
        Box railB = Piece(At(11, 0, 0), 1.5, 24, 0.75, WidthLength);
        Sketch sketch = Sketched(railA, railB);
        Joint joint = Between(railA, BoxFace.Top, railB, BoxFace.Top, JointType.HalfLap);

        JointShape shape = JointGeometry.Of(sketch.WithRelationship(joint), joint)!;
        LapShape lap = shape.Lap!;

        // Overlap: x 11..12.5, y 10..11.5, z 0..0.75.
        Assert.Equal(At(11, 10, 0), lap.Low);
        Assert.Equal(At(12.5, 11.5, 0.75), lap.High);
        Assert.Equal(In(0.375), lap.Depth);                          // half of 3/4

        // On A (length x): 1.5 long, 11 from the west end (24 - 12.5 = 11.5 from the east).
        Assert.Equal(new LapPart(In(1.5), In(11), BoxFace.West), lap.Receiving);
        // On B (length y): 1.5 long, 10 from the south end (24 - 11.5 = 12.5 from the north).
        Assert.Equal(new LapPart(In(1.5), In(10), BoxFace.South), lap.Inserted);

        // The order does not matter: the lower id first as receiving, and no question is asked.
        JointRoles roles = JointGeometry.ProposeRoles(sketch, JointType.HalfLap, joint.Inserted, joint.Receiving)!;
        Assert.Equal(joint.Receiving, roles.Receiving);
        Assert.False(roles.Ambiguous);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_half_lap_at_the_end_of_a_part_is_zero_from_that_end()
    {
        // A short stub, x 11..17, laid over rail A (x 0..24) at the same y and z.
        Box railA = Piece(At(0, 10, 0), 24, 1.5, 0.75, LengthWidth);
        Box stub = Piece(At(11, 10, 0), 6, 1.5, 0.75, LengthWidth);
        Joint joint = Between(railA, BoxFace.Top, stub, BoxFace.Top, JointType.HalfLap);

        LapShape lap = JointGeometry.Of(Sketched(railA, stub).WithRelationship(joint), joint)!.Lap!;

        // Overlap x 11..17 (6 long): the whole stub, so 0 from its west end (a tie between its two ends goes to the west);
        // on rail A, x 0..24, it is 11 from the west end and 24 - 17 = 7 from the east: the east end is nearer.
        Assert.Equal(new LapPart(In(6), Length.Zero, BoxFace.West), lap.Inserted);
        Assert.Equal(new LapPart(In(6), In(7), BoxFace.East), lap.Receiving);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void Two_boards_stacked_face_to_face_are_not_a_half_lap_because_their_overlap_has_no_volume()
    {
        // A: z 0..0.75; B on top of it: z 0.75..1.5. A.top and B.bottom are both the plane z = 0.75 and overlap in plan,
        // and the thicknesses agree, but the boxes only touch: nothing is lapped.
        Box a = Piece(At(0, 0, 0), 6, 1.5, 0.75, LengthWidth);
        Box b = Piece(At(3, 0, 0.75), 6, 1.5, 0.75, LengthWidth);
        Joint joint = Between(a, BoxFace.Top, b, BoxFace.Bottom, JointType.HalfLap);
        Sketch sketch = Sketched(a, b).WithRelationship(joint);

        Assert.NotNull(JointGeometry.Contact(sketch, joint));
        Assert.False(JointGeometry.IsSatisfied(sketch, joint));
        Assert.Null(JointGeometry.Of(sketch, joint)!.Lap);
    }

    // ---- The tabletop joint: the apron's top edge against the top's bottom face ----
    // Top: 42 x 22 x 3/4 at z 16.25..17 (bottom face z 16.25). Back apron top edge: x 3..39, y 19.75..20.5 at z 16.25.
    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_tabletop_joint_makes_the_top_the_receiving_part_because_its_bottom_face_is_in_the_contact()
    {
        Box top = Piece(At(0, 0, 16.25), 42, 22, 0.75, LengthWidth);
        Box apron = Piece(At(3, 19.75, 10.75), 36, 0.75, 5.5, LengthThickness);
        Sketch sketch = Sketched(top, apron);
        FeatureRef topFace = new(top.Id, BoxFeature.Face(BoxFace.Bottom));
        FeatureRef apronFace = new(apron.Id, BoxFeature.Face(BoxFace.Top));

        Joint joint = new(RelationshipId.New(), topFace, apronFace, JointType.Tabletop, null, new Fastening(FasteningKind.Clips, null, null), false);
        JointShape shape = JointGeometry.Of(sketch.WithRelationship(joint), joint)!;

        // Contact x 3..39 (36) by y 19.75..20.5 (3/4) on the plane z = 16.25: the note's 36 long.
        Assert.Equal(In(36), shape.JointLength);
        Assert.Equal(In(0.75), shape.JointWidth);

        foreach ((FeatureRef first, FeatureRef second) in new[] { (topFace, apronFace), (apronFace, topFace) })
        {
            JointRoles roles = JointGeometry.ProposeRoles(sketch, JointType.Tabletop, first, second)!;
            Assert.Equal(topFace, roles.Receiving);
            Assert.Equal(apronFace, roles.Inserted);
            Assert.False(roles.Ambiguous);
        }

        // Neither face a bottom face, or both: undecided.
        Assert.True(JointGeometry.ProposeRoles(sketch, JointType.Tabletop, apronFace, apronFace with { Box = top.Id })!.Ambiguous);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void Faces_that_are_not_one_face_of_a_box_in_the_sketch_get_no_roles_and_no_shape()
    {
        (Sketch sketch, Box leg, Box apron) = J1();
        FeatureRef edge = new(leg.Id, BoxFeature.Edge(BoxFace.East, BoxFace.North));
        FeatureRef face = new(apron.Id, BoxFeature.Face(BoxFace.West));
        FeatureRef gone = new(new EntityId(Guid.NewGuid()), BoxFeature.Face(BoxFace.West));

        Assert.Null(JointGeometry.ProposeRoles(sketch, JointType.Butt, edge, face));
        Assert.Null(JointGeometry.ProposeRoles(sketch, JointType.Butt, face, edge));
        Assert.Null(JointGeometry.ProposeRoles(sketch, JointType.Butt, gone, face));
        Assert.Null(JointGeometry.ProposeRoles(sketch, JointType.Butt, face, gone));

        // A joint whose parts are apart has a contact of none and so no shape.
        Joint apart = Between(leg, BoxFace.East, apron with { Anchor = At(5, 19.75, 10.75) }, BoxFace.West, JointType.Butt);
        Assert.Null(JointGeometry.Of(sketch.WithEntity(apron with { Anchor = At(5, 19.75, 10.75) }).WithRelationship(apart), apart));
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_part_without_a_part_record_is_as_long_as_its_longest_size_and_as_thick_as_its_smallest()
    {
        // Two bare boxes: a 30 x 1 x 2 plank whose end (west) meets a 4 x 4 x 4 block's east face. The plank's length
        // is its longest size (x, 30), so its west face is an end; the block, a cube, takes x first.
        Box plank = new(new EntityId(Guid.NewGuid()), LayerId.Default, At(4, 0, 0), In(30), In(1), In(2), BoxFace.Top, Angle.Zero);
        Box block = new(new EntityId(Guid.NewGuid()), LayerId.Default, At(0, 0, 0), In(4), In(4), In(4), BoxFace.Top, Angle.Zero);
        Sketch sketch = Sketched(plank, block);
        FeatureRef plankEnd = new(plank.Id, BoxFeature.Face(BoxFace.West));
        FeatureRef blockSide = new(block.Id, BoxFeature.Face(BoxFace.East));

        // The block's east face is perpendicular to its length axis x (a cube's longest, x first): also an end. Both ends: ask.
        Assert.True(JointGeometry.ProposeRoles(sketch, JointType.Butt, plankEnd, blockSide)!.Ambiguous);

        // Thickness of a bare box is its smallest size: the plank is 1, the block 4 -> for a groove the plank is inserted.
        JointRoles groove = JointGeometry.ProposeRoles(sketch, JointType.Groove, blockSide, plankEnd)!;
        Assert.Equal(plankEnd, groove.Inserted);
        Assert.False(groove.Ambiguous);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_part_stood_on_its_edge_is_measured_in_the_world_and_worded_in_its_own_frame()
    {
        // A side stood with its north face up (assembly-model 1.3: local +x -> world +x, local +y -> world +z,
        // local +z -> world -y). Local sizes x 1/2 (thickness), y 16 (length), z 3 1/2 (width), anchored at (0, 3.5, 0):
        // world x 0..0.5, y 0..3.5 (local z runs to -y), z 0..16 (local y runs up).
        Box side = new(new EntityId(Guid.NewGuid()), LayerId.Default, At(0, 3.5, 0), In(0.5), In(16), In(3.5), BoxFace.North, Angle.Zero)
        {
            Part = new Part("stock", null, 1, ThicknessLength),
        };

        // A sheet laid flat into it: x 0.5..15.5, y 0.5..3, z 1..1.25. Its west face meets the side's east face (x = 0.5).
        Box sheet = Piece(At(0.5, 0.5, 1), 15, 2.5, 0.25, LengthWidth);
        Sketch sketch = Sketched(side, sheet);
        Joint joint = Between(side, BoxFace.East, sheet, BoxFace.West, JointType.Groove, In(0.25));
        sketch = sketch.WithRelationship(joint);

        TouchingFaces pair = Assert.Single(JointGeometry.TouchingFaces(sketch, side.Id, sheet.Id));
        Assert.Equal(BoxFace.East, pair.OfFirst);

        // Contact: y 0.5..3 (2.5) by z 1..1.25 (1/4). The long side is y; the side's length lies along world z,
        // so the slot is across its width, a dado. Its offset is measured along z: 1 from z = 0, 14.75 from z = 16.
        // World z = 0 is the side's local south end (local -y maps to world -z): the offset is "from the south end".
        GrooveShape groove = JointGeometry.Of(sketch, joint)!.Groove!;
        Assert.Equal(GrooveDirection.AcrossWidth, groove.Direction);
        Assert.Equal(In(0.25), groove.Width);
        Assert.Equal(In(1), groove.Offset);
        Assert.Equal(BoxFace.South, groove.OffsetFrom);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void A_groove_exactly_in_the_middle_is_measured_from_the_lower_edge_and_a_square_contact_runs_along_the_length()
    {
        // A side x 0..0.5, y 0..16 (length), z 0..3 (width); a panel 1/4 thick centred in z: 1.375..1.625, so 1.375 from each edge.
        Box side = Piece(At(0, 0, 0), 0.5, 16, 3, ThicknessLength);
        Box panel = Piece(At(0.5, 0, 1.375), 4, 16, 0.25, LengthWidth);
        Joint centred = Between(side, BoxFace.East, panel, BoxFace.West, JointType.Groove, In(0.25));

        GrooveShape groove = JointGeometry.Of(Sketched(side, panel).WithRelationship(centred), centred)!.Groove!;

        Assert.Equal(In(1.375), groove.Offset);
        Assert.Equal(BoxFace.Bottom, groove.OffsetFrom);    // a tie is measured from the lower edge

        // A contact 1 in square has no long side: the slot then follows the receiving part's length (y here).
        Box rail = Piece(At(0, 0, 0), 2, 16, 1, WidthLength);
        Box peg = Piece(At(0, 5, 1), 1, 1, 1, WidthLength);
        Joint square = Between(rail, BoxFace.Top, peg, BoxFace.Bottom, JointType.Groove, In(0.25));

        Assert.Equal(GrooveDirection.AlongLength, JointGeometry.Of(Sketched(rail, peg).WithRelationship(square), square)!.Groove!.Direction);
    }

    [Trait("Feature", "GEO-017")]
    [Fact]
    public void Pocket_holes_are_only_reported_when_the_joint_is_held_with_pocket_screws()
    {
        (Sketch sketch, Box leg, Box apron) = J1();
        Joint glued = Between(leg, BoxFace.East, apron, BoxFace.West, JointType.Butt);

        Assert.Null(JointGeometry.Of(sketch.WithRelationship(glued), glued)!.PocketEnd);
    }
}
