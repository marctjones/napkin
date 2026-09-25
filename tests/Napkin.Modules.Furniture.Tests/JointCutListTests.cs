using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// What joinery does to the cut list (docs/design/joinery-and-fasteners.md &#xA7;6): finished sizes carry
/// the allowances, rows carry the fixed sentences, and the group key carries the joinery up to a
/// half-turn. The scenes are the coffee table of &#xA7;2 and every expectation is worked out by hand
/// in a comment, in inches then units (x 1024); none of it is napkin's own output.
/// </summary>
public class JointCutListTests
{
    // ------------------------------------------------------------------------------------------------
    // A small builder: boxes lying as drawn, given by their low and high corners in inches.
    // ------------------------------------------------------------------------------------------------

    private sealed class Scene
    {
        private int next;

        public Sketch Sketch { get; set; } = Sketch.Empty;

        private static Length In(double inches) => new((long)(inches * 1024));

        public Box Add(string name, (double X, double Y, double Z) low, (double X, double Y, double Z) high, PlanAxes axes)
        {
            Box box = new(
                new EntityId(new Guid(++next, 0, 0, new byte[8])),
                LayerId.Default,
                new Point3(In(low.X), In(low.Y), In(low.Z)),
                In(high.X - low.X),
                In(high.Y - low.Y),
                In(high.Z - low.Z),
                BoxFace.Top,
                Angle.Zero)
            {
                Name = name,
                Part = new Part(null, null, 1, axes),
            };
            Sketch = Sketch.WithEntity(box);
            return box;
        }

        public Joint Join(
            Box receiving,
            BoxFace receivingFace,
            Box inserted,
            BoxFace insertedFace,
            JointType type,
            double? depth = null,
            Fastening? fastening = null)
        {
            Joint joint = new(
                new RelationshipId(new Guid(++next, 1, 0, new byte[8])),
                new FeatureRef(receiving.Id, BoxFeature.Face(receivingFace)),
                new FeatureRef(inserted.Id, BoxFeature.Face(insertedFace)),
                type,
                depth is { } d ? In(d) : null,
                fastening ?? Fastening.None,
                false);
            Sketch = Sketch.WithRelationship(joint);
            return joint;
        }

        public ImmutableArray<CutListRow> Rows() => CutList.Of(Sketch, MaterialsLibrary.Shipped);
    }

    private static readonly PlanAxes ThicknessLength = new(PartDimension.Thickness, PartDimension.Length);
    private static readonly PlanAxes LengthThickness = new(PartDimension.Length, PartDimension.Thickness);
    private static readonly PlanAxes LengthWidth = new(PartDimension.Length, PartDimension.Width);
    private static readonly PlanAxes WidthThickness = new(PartDimension.Width, PartDimension.Thickness);

    private static Fastening Pocket(BoxFace from) => new(FasteningKind.PocketScrews, null, from);

    // ------------------------------------------------------------------------------------------------
    // One drawer box (note 2.2 items 8-12, joints J7, J8, J9), y 1.5..17.5, its interior 15 5/8 wide by 15 deep.
    //   left side   x 4..4.5     y 1.5..17.5  z 11..14.5   16 long, 3 1/2 wide, 1/2 thick
    //   right side  x 20.125..20.625 (same y, z)
    //   box front   x 4.5..20.125  y 1.5..2    z 11..14.5   15 5/8 long
    //   box back    x 4.5..20.125  y 17..17.5  z 11..14.5
    //   bottom      x 4.5..20.125  y 2..17     z 11.5..11.75  15 5/8 by 15 by 1/4, 1/2 up from the sides' bottom edge
    // ------------------------------------------------------------------------------------------------

    private static Scene Drawer()
    {
        Scene scene = new();
        Box left = scene.Add("Drawer side, left", (4, 1.5, 11), (4.5, 17.5, 14.5), ThicknessLength);
        Box right = scene.Add("Drawer side, right", (20.125, 1.5, 11), (20.625, 17.5, 14.5), ThicknessLength);
        Box front = scene.Add("Drawer box front", (4.5, 1.5, 11), (20.125, 2, 14.5), LengthThickness);
        Box back = scene.Add("Drawer box back", (4.5, 17, 11), (20.125, 17.5, 14.5), LengthThickness);
        Box bottom = scene.Add("Drawer bottom", (4.5, 2, 11.5), (20.125, 17, 11.75), LengthWidth);

        Fastening brads = new(FasteningKind.Brads, null, null);
        scene.Join(left, BoxFace.East, front, BoxFace.West, JointType.Rabbet, 0.25, brads);
        scene.Join(right, BoxFace.West, front, BoxFace.East, JointType.Rabbet, 0.25, brads);
        scene.Join(left, BoxFace.East, back, BoxFace.West, JointType.Butt, null, brads);
        scene.Join(right, BoxFace.West, back, BoxFace.East, JointType.Butt, null, brads);
        scene.Join(left, BoxFace.East, bottom, BoxFace.West, JointType.Groove, 0.25);
        scene.Join(right, BoxFace.West, bottom, BoxFace.East, JointType.Groove, 0.25);
        scene.Join(front, BoxFace.North, bottom, BoxFace.South, JointType.Groove, 0.25);
        scene.Join(back, BoxFace.South, bottom, BoxFace.North, JointType.Groove, 0.25);
        return scene;
    }

    [Trait("Feature", "CUT-007")]
    [Fact]
    public void A_drawers_finished_sizes_carry_the_rabbet_and_groove_allowances_and_a_side_stays_as_drawn()
    {
        ImmutableArray<CutListRow> rows = Drawer().Rows();

        // Box front: drawn 15 5/8 = 16000 units; a 1/4 (256) rabbet at each end -> 16000 + 256 + 256 = 16512 = 16 1/8.
        CutListRow front = Assert.Single(rows, row => row.Label == "Drawer box front");
        Assert.Equal(16512, front.Length.Units);
        Assert.Equal(3584, front.Width.Units);          // 3 1/2 unchanged
        Assert.Equal(512, front.Thickness.Units);       // 1/2 unchanged
        Assert.Equal(16000, front.Drawn!.Value.Length.Units);

        // Bottom: drawn 15 5/8 x 15 = 16000 x 15360; four 1/4 grooves, two on the length and two on the width:
        // 16000 + 512 = 16512 by 15360 + 512 = 15872 (16 1/8 x 15 1/2).
        CutListRow bottom = Assert.Single(rows, row => row.Label == "Drawer bottom");
        Assert.Equal((16512, 15872, 256), (bottom.Length.Units, bottom.Width.Units, bottom.Thickness.Units));

        // A side is grooved and rabbeted, not inserted, and the back only butts: no allowance.
        Assert.Equal(16384, Assert.Single(rows, row => row.Label == "Drawer side, left").Length.Units);   // 16 x 1024
        Assert.Equal(16000, Assert.Single(rows, row => row.Label == "Drawer box back").Length.Units);
    }

    [Trait("Feature", "CUT-007")]
    [Fact]
    public void A_drawers_rows_read_the_way_the_note_lists_them_in_size_order_with_their_sentences()
    {
        ImmutableArray<CutListRow> rows = Drawer().Rows();

        // Length descending (16512, 16512, 16384, 16384, 16000), then width descending, then label.
        Assert.Equal(
            ["Drawer bottom", "Drawer box front", "Drawer side, left", "Drawer side, right", "Drawer box back"],
            rows.Select(row => row.Label));

        Assert.Equal(
            ["Length includes 1/4\" into a groove at each end; width includes 1/4\" into a groove at each edge."],
            rows[0].JointText);
        Assert.Equal(
            [
                "Length includes 1/4\" into a rabbet at each end.",
                "Groove the north face: 1/4\" wide, 1/4\" deep, 1/2\" from the bottom edge, full length.",
            ],
            rows[1].JointText);
        Assert.Equal(
            [
                "Rabbet the south end on the east face: 1/2\" wide, 1/4\" deep.",
                "Groove the east face: 1/4\" wide, 1/4\" deep, 1/2\" from the bottom edge, full length.",
            ],
            rows[2].JointText);
        Assert.Equal(
            [
                "Rabbet the south end on the west face: 1/2\" wide, 1/4\" deep.",
                "Groove the west face: 1/4\" wide, 1/4\" deep, 1/2\" from the bottom edge, full length.",
            ],
            rows[3].JointText);
        Assert.Equal(
            ["Groove the south face: 1/4\" wide, 1/4\" deep, 1/2\" from the bottom edge, full length."],
            rows[4].JointText);
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void The_left_and_right_drawer_sides_are_two_rows_because_no_half_turn_makes_one_the_other()
    {
        // Left: rabbet the south end on the east face, groove from the bottom edge of the east face.
        // Right: the same on the west face. About x the rabbet goes south -> north; about y the groove goes
        // bottom -> top (and east -> west); about z the rabbet goes south -> north. None gives the other.
        ImmutableArray<CutListRow> rows = Drawer().Rows();

        CutListRow left = Assert.Single(rows, row => row.Label == "Drawer side, left");
        CutListRow right = Assert.Single(rows, row => row.Label == "Drawer side, right");

        Assert.Equal(1, left.Quantity);
        Assert.Equal(1, right.Quantity);
        Assert.False(JointDescription.SameJoinery(left.Joinery, right.Joinery));
    }

    // ------------------------------------------------------------------------------------------------
    // The leg frame (note 2.1, 2.3 J1, J2, J11): four legs, a back apron, two side aprons, and the top.
    //   legs 1 1/2 square, 16 1/4 tall: SW x 1.5..3, y 1.5..3; NW y 19..20.5; SE x 39..40.5; NE likewise
    //   back apron x 3..39, y 19.75..20.5, z 10.75..16.25    (36 x 5 1/2 x 3/4)
    //   west apron x 1.5..2.25, y 3..19; east apron x 39.75..40.5, y 3..19    (16 x 5 1/2 x 3/4)
    //   top x 0..42, y 0..22, z 16.25..17
    // ------------------------------------------------------------------------------------------------

    private static Scene Frame(bool withTop = true)
    {
        Scene scene = new();
        Box top = scene.Add("Top", (0, 0, 16.25), (42, 22, 17), LengthWidth);
        Box sw = scene.Add("Leg, south-west", (1.5, 1.5, 0), (3, 3, 16.25), WidthThickness);
        Box nw = scene.Add("Leg, north-west", (1.5, 19, 0), (3, 20.5, 16.25), WidthThickness);
        Box se = scene.Add("Leg, south-east", (39, 1.5, 0), (40.5, 3, 16.25), WidthThickness);
        Box ne = scene.Add("Leg, north-east", (39, 19, 0), (40.5, 20.5, 16.25), WidthThickness);
        Box back = scene.Add("Apron, back", (3, 19.75, 10.75), (39, 20.5, 16.25), LengthThickness);
        Box west = scene.Add("Apron, side, west", (1.5, 3, 10.75), (2.25, 19, 16.25), ThicknessLength);
        Box east = scene.Add("Apron, side, east", (39.75, 3, 10.75), (40.5, 19, 16.25), ThicknessLength);

        // J1: the back apron's ends into the north legs' inside faces, holes from the inside (south) face.
        scene.Join(nw, BoxFace.East, back, BoxFace.West, JointType.Butt, null, Pocket(BoxFace.South));
        scene.Join(ne, BoxFace.West, back, BoxFace.East, JointType.Butt, null, Pocket(BoxFace.South));

        // J2: the side aprons' ends into the legs; holes from each apron's inside face.
        scene.Join(sw, BoxFace.North, west, BoxFace.South, JointType.Butt, null, Pocket(BoxFace.East));
        scene.Join(nw, BoxFace.South, west, BoxFace.North, JointType.Butt, null, Pocket(BoxFace.East));
        scene.Join(se, BoxFace.North, east, BoxFace.South, JointType.Butt, null, Pocket(BoxFace.West));
        scene.Join(ne, BoxFace.South, east, BoxFace.North, JointType.Butt, null, Pocket(BoxFace.West));

        if (withTop)
        {
            // J11: the top's underside, with the aprons' top edges held to it with clips.
            Fastening clips = new(FasteningKind.Clips, null, null);
            scene.Join(top, BoxFace.Bottom, back, BoxFace.Top, JointType.Tabletop, null, clips);
            scene.Join(top, BoxFace.Bottom, west, BoxFace.Top, JointType.Tabletop, null, clips);
            scene.Join(top, BoxFace.Bottom, east, BoxFace.Top, JointType.Tabletop, null, clips);
        }

        return scene;
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void Four_legs_are_one_row_and_the_two_side_aprons_are_one_row_because_a_half_turn_swaps_them()
    {
        Scene frame = Frame();
        ImmutableArray<CutListRow> rows = frame.Rows();

        // Top 42 x 22 x 3/4 = 43008 x 22528 x 768; back apron 36 x 5 1/2 x 3/4 = 36864 x 5632 x 768;
        // legs 16 1/4 = 16640 x 1536 x 1536; side aprons 16 x 5 1/2 x 3/4 = 16384 x 5632 x 768. Length descending.
        Assert.Equal(["Top", "Apron, back", "Leg", "Apron, side"], rows.Select(row => row.Label));
        Assert.Equal([1, 1, 4, 2], rows.Select(row => row.Quantity));
        Assert.Equal(8, rows.Sum(row => row.Quantity));
        Assert.Equal([43008, 36864, 16640, 16384], rows.Select(row => row.Length.Units));

        // Nothing is cut into a leg or the top, and nothing is longer than drawn.
        Assert.Empty(rows[0].JointText);
        Assert.Empty(rows[2].JointText);
        Assert.Empty(rows[2].Joinery);

        // The west apron is the first member, so its sentences print. Contact with a leg: 3/4 x 5 1/2, so the pocket
        // count is max(2, ceil(5.5 / 2)) = 3 at each end; the clip count is max(2, ceil(16 / 12)) = 2 on the east (inside) face.
        Assert.Equal(
            [
                "Drill 3 pocket holes in the south end and 3 in the north end, from the east face.",
                "Fit 2 tabletop clips along the top edge on the east face (slot or recess per the clip's instructions).",
            ],
            rows[3].JointText);
        Assert.Equal(2, rows[3].Members.Length);

        // The back apron: 3 pocket holes at each end (5 1/2 / 2 = 2.75 -> 3) from the south face, and ceil(36 / 12) = 3 clips.
        Assert.Equal(
            [
                "Drill 3 pocket holes in the west end and 3 in the east end, from the south face.",
                "Fit 3 tabletop clips along the top edge on the south face (slot or recess per the clip's instructions).",
            ],
            rows[1].JointText);
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void Two_parts_alike_in_everything_but_a_groove_depth_are_two_rows()
    {
        // Two 1/2 thick boards, each with a 1/4 panel edge in a groove; one 1/8 deep, the other 3/16.
        Scene scene = new();
        Box first = scene.Add("Board", (0, 0, 0), (0.5, 10, 3), ThicknessLength);
        Box panelA = scene.Add("Panel", (0.5, 0, 1), (5, 10, 1.25), LengthWidth);
        Box second = scene.Add("Board", (20, 0, 0), (20.5, 10, 3), ThicknessLength);
        Box panelB = scene.Add("Panel", (20.5, 0, 1), (25, 10, 1.25), LengthWidth);
        scene.Join(first, BoxFace.East, panelA, BoxFace.West, JointType.Groove, 0.125);
        scene.Join(second, BoxFace.East, panelB, BoxFace.West, JointType.Groove, 0.1875);

        ImmutableArray<CutListRow> rows = scene.Rows();

        // Boards: the same size and stock, but 1/8 and 3/16 deep grooves: two rows. Panels: 1/8 and 3/16 in each: two rows too.
        Assert.Equal(2, rows.Count(row => row.Label == "Board"));
        Assert.Equal(2, rows.Count(row => row.Label == "Panel"));
        Assert.All(rows, row => Assert.Equal(1, row.Quantity));
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void Equal_joinery_groups_and_the_order_of_rows_is_the_same_whatever_order_the_boxes_were_added_in()
    {
        Scene scene = Frame();
        ImmutableArray<CutListRow> once = scene.Rows();
        ImmutableArray<CutListRow> again = scene.Rows();

        Assert.Equal(once, again);
        Assert.Equal(once.Select(row => row.GetHashCode()), again.Select(row => row.GetHashCode()));
    }

    // ------------------------------------------------------------------------------------------------
    // Every sentence form, once, verbatim (note 6.2), built from structure.
    // ------------------------------------------------------------------------------------------------

    private static Length Inch(double inches) => new((long)(inches * 1024));

    [Trait("Feature", "CUT-007")]
    [Fact]
    public void Every_sentence_form_reads_the_way_the_note_fixes_it()
    {
        ImmutableArray<JointFact> facts =
        [
            new(JointFactKind.Groove, BoxFace.North, BoxFace.Bottom, Direction: GrooveDirection.AlongLength, Width: Inch(0.25), Depth: Inch(0.25), Offset: Inch(0.5)),
            new(JointFactKind.Groove, BoxFace.East, BoxFace.Bottom, Direction: GrooveDirection.AcrossWidth, Width: Inch(0.75), Depth: Inch(0.25), Offset: Inch(10)),
            new(JointFactKind.Rabbet, BoxFace.West, BoxFace.South, Width: Inch(0.5), Depth: Inch(0.25)),
            new(JointFactKind.HalfLap, BoxFace.Top, BoxFace.West, Depth: Inch(0.375), Long: Inch(1.5)),
            new(JointFactKind.HalfLap, BoxFace.Bottom, BoxFace.East, Depth: Inch(0.375), Long: Inch(1.5), Offset: Inch(11)),
        ];

        // Order: by face (south, east, north, west, bottom, top), then by offset, then rabbet before groove.
        Assert.Equal(
            [
                "Dado the east face: 3/4\" wide, 1/4\" deep, 10\" from the bottom end, across the width.",
                "Groove the north face: 1/4\" wide, 1/4\" deep, 1/2\" from the bottom edge, full length.",
                "Rabbet the south end on the west face: 1/2\" wide, 1/4\" deep.",
                "Half-lap the bottom face 11\" from the east end: 1 1/2\" long, 3/8\" deep, across the width.",
                "Half-lap the top face at the west end: 1 1/2\" long, 3/8\" deep, across the width.",
            ],
            JointDescription.Describe(facts));
    }

    [Trait("Feature", "CUT-007")]
    [Fact]
    public void Pocket_holes_from_one_face_are_one_sentence_and_clips_and_allowances_have_their_own_forms()
    {
        // One end only: no comma before "from". One hole or clip: singular.
        Assert.Equal(
            ["Drill 1 pocket hole in the north end from the west face."],
            JointDescription.Describe([new(JointFactKind.PocketHoles, BoxFace.West, BoxFace.North, Count: 1)]));

        // Three ends from one face: the biggest run first, then the lower ends (south before east here).
        Assert.Equal(
            ["Drill 3 pocket holes in the north end, 2 in the south end and 2 in the east end, from the west face."],
            JointDescription.Describe(
            [
                new(JointFactKind.PocketHoles, BoxFace.West, BoxFace.East, Count: 2),
                new(JointFactKind.PocketHoles, BoxFace.West, BoxFace.North, Count: 3),
                new(JointFactKind.PocketHoles, BoxFace.West, BoxFace.South, Count: 2),
            ]));

        // Holes from two faces are two sentences.
        Assert.Equal(
            [
                "Drill 2 pocket holes in the south end from the east face.",
                "Drill 2 pocket holes in the north end from the west face.",
            ],
            JointDescription.Describe(
            [
                new(JointFactKind.PocketHoles, BoxFace.West, BoxFace.North, Count: 2),
                new(JointFactKind.PocketHoles, BoxFace.East, BoxFace.South, Count: 2),
            ]));

        Assert.Equal(
            ["Fit 1 tabletop clip along the top edge on the north face (slot or recess per the clip's instructions)."],
            JointDescription.Describe([new(JointFactKind.TabletopClips, BoxFace.North, Count: 1)]));

        // A rabbet at one end only, a groove at each edge, a groove at one edge: each its own clause.
        Assert.Equal(
            ["Length includes 1/4\" into a rabbet at the north end."],
            JointDescription.Describe([Allowance(PartDimension.Length, JointType.Rabbet, BoxFace.North, 0.25)]));
        Assert.Equal(
            ["Length includes 1/4\" into a rabbet at each end; width includes 1/4\" into a groove at each edge; thickness includes 1/8\" into a groove at the top face."],
            JointDescription.Describe(
            [
                Allowance(PartDimension.Length, JointType.Rabbet, BoxFace.East, 0.25),
                Allowance(PartDimension.Length, JointType.Rabbet, BoxFace.West, 0.25),
                Allowance(PartDimension.Width, JointType.Groove, BoxFace.South, 0.25),
                Allowance(PartDimension.Width, JointType.Groove, BoxFace.North, 0.25),
                Allowance(PartDimension.Thickness, JointType.Groove, BoxFace.Top, 0.125),
            ]));

        Assert.Empty(JointDescription.Describe([]));
        Assert.Empty(JointDescription.Describe(default));
    }

    private static JointFact Allowance(PartDimension dimension, JointType type, BoxFace face, double depth)
        => new(JointFactKind.Allowance, Face: face, Dimension: dimension, Type: type, Depth: Inch(depth));

    [Trait("Feature", "CUT-007")]
    [Fact]
    public void A_length_that_is_not_on_the_sixteenth_carries_the_approximately_marker()
    {
        // 1000 units is 0.9766 in: not a sixteenth, so it is written the way the rest of the list writes it.
        string sentence = Assert.Single(JointDescription.Describe(
        [
            new(JointFactKind.Groove, BoxFace.North, BoxFace.Bottom, Direction: GrooveDirection.AlongLength, Width: Inch(0.25), Depth: Inch(0.25), Offset: new Length(1000)),
        ]));

        Assert.Equal("Groove the north face: 1/4\" wide, 1/4\" deep, ≈1\" from the bottom edge, full length.", sentence);
    }

    // ------------------------------------------------------------------------------------------------
    // A part stood on its edge names its faces in its own frame (risk 3 of the note).
    // ------------------------------------------------------------------------------------------------

    [Trait("Feature", "CUT-007")]
    [Fact]
    public void A_part_stood_on_its_edge_words_its_joinery_in_its_own_frame_not_the_worlds()
    {
        // A side stood with its north face up: local x 1/2 (thickness), y 16 (length, lying along world z), z 3 1/2 (width,
        // lying along world -y), anchored at (0, 3.5, 0): world x 0..0.5, y 0..3.5, z 0..16. A 1/4 sheet laid flat into it at
        // z 1..1.25, x 0.5..15.5, y 0.5..3. Contact y 0.5..3 by z 1..1.25: long side along y, and the side's length is world z,
        // so it is a dado across the width; 1 from world z = 0, which is the side's local south end.
        Scene scene = new();
        Box side = new(new EntityId(new Guid(1000, 0, 0, new byte[8])), LayerId.Default, new Point3(Length.Zero, new Length(3584), Length.Zero), new Length(512), new Length(16384), new Length(3584), BoxFace.North, Angle.Zero)
        {
            Name = "Side",
            Part = new Part(null, null, 1, ThicknessLength),
        };
        scene.Sketch = scene.Sketch.WithEntity(side);
        Box sheet = scene.Add("Sheet", (0.5, 0.5, 1), (15.5, 3, 1.25), LengthWidth);
        scene.Join(side, BoxFace.East, sheet, BoxFace.West, JointType.Groove, 0.25);

        CutListRow row = Assert.Single(scene.Rows(), r => r.Label == "Side");

        Assert.Equal(
            ["Dado the east face: 1/4\" wide, 1/4\" deep, 1\" from the south end, across the width."],
            row.JointText);
    }

    // ------------------------------------------------------------------------------------------------
    // Unsatisfied joints (note 4.3, 6.1): the allowance stays and the row is flagged; a slot is no allowance.
    // ------------------------------------------------------------------------------------------------

    [Trait("Feature", "CUT-009")]
    [Fact]
    public void A_joint_whose_parts_have_moved_apart_keeps_its_allowance_says_nothing_geometric_and_flags_the_row()
    {
        Scene drawer = Drawer();
        Box front = drawer.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Drawer box front");
        drawer.Sketch = drawer.Sketch.WithEntity(front with { Anchor = front.Anchor + new Vector3(Length.Inches(1), Length.Zero, Length.Zero) });

        CutListRow row = Assert.Single(drawer.Rows(), r => r.Label == "Drawer box front");

        // Both rabbets are still 1/4 deep: 16000 + 256 + 256 = 16512. The front no longer touches either side, so
        // no rabbet sentence can be measured; the joint on the bottom is unaffected in what it says of the front.
        Assert.Equal(16512, row.Length.Units);
        Assert.True(row.JointsUnsatisfied);
        Assert.Equal(["joint not satisfied"], row.Flags);
        Assert.DoesNotContain(row.JointText, sentence => sentence.StartsWith("Rabbet", StringComparison.Ordinal));

        // The other rows: the front slid along its own length still touches the bottom (its north face still meets the
        // bottom's south edge over x 5.5..20.125) and the back is untouched: neither is flagged.
        Assert.False(Assert.Single(drawer.Rows(), r => r.Label == "Drawer bottom").JointsUnsatisfied);
        Assert.False(Assert.Single(drawer.Rows(), r => r.Label == "Drawer box back").JointsUnsatisfied);

        // With the front's rabbets unmeasurable the two sides differ only by a groove on the east or the west face from the
        // bottom edge, and a half-turn about z makes one the other: they list as one row, flagged. The rabbet is what told them apart.
        CutListRow sides = Assert.Single(drawer.Rows(), r => r.Label == "Drawer side");
        Assert.Equal(2, sides.Quantity);
        Assert.True(sides.JointsUnsatisfied);
    }

    [Trait("Feature", "CUT-009")]
    [Fact]
    public void A_depth_that_is_not_less_than_the_thickness_is_a_slot_and_adds_no_allowance()
    {
        // The drawer's sides are 1/2 thick: a 1/2 rabbet is a slot through the side, not a rabbet.
        Scene scene = new();
        Box side = scene.Add("Side", (4, 1.5, 11), (4.5, 17.5, 14.5), ThicknessLength);
        Box front = scene.Add("Front", (4.5, 1.5, 11), (20.125, 2, 14.5), LengthThickness);
        scene.Join(side, BoxFace.East, front, BoxFace.West, JointType.Rabbet, 0.5);

        CutListRow row = Assert.Single(scene.Rows(), r => r.Label == "Front");

        Assert.Equal(16000, row.Length.Units);          // 15 5/8: nothing added
        Assert.True(row.JointsUnsatisfied);
        Assert.Empty(row.JointText);
        Assert.Empty(row.Joinery);
    }

    [Trait("Feature", "CUT-009")]
    [Fact]
    public void A_design_with_no_joints_has_no_joinery_and_no_flags()
    {
        CutListRow row = Assert.Single(Frame(withTop: false).Rows(), r => r.Label == "Top");

        Assert.Empty(row.Joinery);
        Assert.Empty(row.Flags);
        Assert.False(row.JointsUnsatisfied);
    }

    // ------------------------------------------------------------------------------------------------
    // The CSV: a Joinery column after Cuts, and the new header line.
    // ------------------------------------------------------------------------------------------------

    [Trait("Feature", "CUT-010")]
    [Fact]
    public void The_csv_has_the_new_header_and_a_joinery_column_that_parses_back_to_the_rows()
    {
        ImmutableArray<CutListRow> rows = Frame().Rows();

        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse(CutListCsv.ToCsv(rows));

        Assert.Equal("Cut list: finished sizes: joinery allowances included; before saw kerf (#138).", Assert.Single(lines[0]));
        Assert.Equal(["Label", "Quantity", "Length", "Width", "Thickness", "Material", "Cuts", "Joinery"], lines[1]);
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Equal(8, lines[i + 2].Length);
            Assert.Equal(string.Join("; ", rows[i].JointText), lines[i + 2][7]);
        }

        // The back apron's two sentences share one quoted field.
        Assert.Equal(
            "Drill 3 pocket holes in the west end and 3 in the east end, from the south face.; Fit 3 tabletop clips along the top edge on the south face (slot or recess per the clip's instructions).",
            lines[3][7]);
    }

    [Trait("Feature", "CUT-010")]
    [Fact]
    public void A_flagged_row_says_so_in_the_last_column()
    {
        Scene scene = Drawer();
        Box front = scene.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Drawer box front");
        scene.Sketch = scene.Sketch.WithEntity(front with { Anchor = front.Anchor + new Vector3(Length.Inches(1), Length.Zero, Length.Zero) });

        ImmutableArray<ImmutableArray<string>> lines = CutListCsv.Parse(CutListCsv.ToCsv(scene.Rows()));

        Assert.Contains(lines.Skip(2), line => line[0] == "Drawer box front" && line[7].EndsWith("joint not satisfied", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------------------------------
    // Recipes (note 7.2): the counts the sentences read.
    // ------------------------------------------------------------------------------------------------

    [Trait("Feature", "CUT-011")]
    [Theory]
    [InlineData(FasteningKind.PocketScrews, 1.5, 2)]     // ceil(0.75) = 1, least 2
    [InlineData(FasteningKind.PocketScrews, 5.5, 3)]     // ceil(2.75) = 3
    [InlineData(FasteningKind.PocketScrews, 16, 8)]      // 16 / 2 = 8
    [InlineData(FasteningKind.Screws, 16, 3)]            // ceil(2.67) = 3
    [InlineData(FasteningKind.Screws, 15.625, 3)]        // ceil(2.6) = 3
    [InlineData(FasteningKind.Brads, 3.5, 3)]            // ceil(2.33) = 3
    [InlineData(FasteningKind.Brads, 1, 2)]              // least 2
    [InlineData(FasteningKind.Nails, 16, 4)]             // 16 / 4 = 4
    [InlineData(FasteningKind.Nails, 3, 2)]              // least 2
    [InlineData(FasteningKind.Dowels, 16, 4)]
    [InlineData(FasteningKind.Dowels, 5.5, 2)]           // ceil(1.375) = 2
    [InlineData(FasteningKind.Biscuits, 16, 2)]          // 16 / 8 = 2
    [InlineData(FasteningKind.Biscuits, 1.5, 1)]         // least 1
    [InlineData(FasteningKind.Clips, 36, 3)]             // 36 / 12 = 3
    [InlineData(FasteningKind.Clips, 16, 2)]             // ceil(1.33) = 2
    [InlineData(FasteningKind.Clips, 5.5, 2)]            // least 2
    [InlineData(FasteningKind.None, 16, 0)]
    public void The_recipe_counts_follow_the_note(FasteningKind kind, double inches, int expected)
    {
        Assert.Equal(expected, Recipes.Recipe(kind, Inch(inches)));
    }

    [Trait("Feature", "CUT-011")]
    [Fact]
    public void A_typed_count_overrides_the_recipe()
    {
        Scene scene = new();
        Box a = scene.Add("A", (0, 0, 0), (1, 1, 1), ThicknessLength);
        Box b = scene.Add("B", (1, 0, 0), (2, 1, 1), ThicknessLength);
        Joint recipe = scene.Join(a, BoxFace.East, b, BoxFace.West, JointType.Butt, null, new Fastening(FasteningKind.Screws, null, null));
        Joint typed = recipe with { Fastening = new Fastening(FasteningKind.Screws, 4, null) };

        // A 16 in joint would take 3 from the recipe; the joint that says 4 takes 4 (note 2.3, J10).
        Assert.Equal(3, Recipes.Count(recipe, Inch(16)));
        Assert.Equal(4, Recipes.Count(typed, Inch(16)));
    }

    // ------------------------------------------------------------------------------------------------
    // The three half-turns, each on its own (note 6.3): about x south and north swap, and bottom and top;
    // about y east and west swap, and bottom and top; about z south and north swap, and east and west.
    // ------------------------------------------------------------------------------------------------

    private static JointFact Groove(BoxFace face, BoxFace from)
        => new(JointFactKind.Groove, face, from, Direction: GrooveDirection.AlongLength, Width: Inch(0.25), Depth: Inch(0.25), Offset: Inch(0.5));

    private static JointFact Pocket(BoxFace from, BoxFace end, int count)
        => new(JointFactKind.PocketHoles, from, end, Count: count);

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void A_half_turn_about_x_swaps_south_with_north_and_bottom_with_top()
    {
        // Pocket holes at the south end from the top face and a groove in the east face from the bottom edge:
        // about x, south -> north and top -> bottom; the east face and its groove keep their face, and lose "bottom".
        ImmutableArray<JointFact> asDrawn = [Pocket(BoxFace.Top, BoxFace.South, 2), Groove(BoxFace.East, BoxFace.Bottom)];
        ImmutableArray<JointFact> turned = [Pocket(BoxFace.Bottom, BoxFace.North, 2), Groove(BoxFace.East, BoxFace.Top)];
        ImmutableArray<JointFact> notTurned = [Pocket(BoxFace.Bottom, BoxFace.North, 2), Groove(BoxFace.East, BoxFace.Bottom)];

        Assert.True(JointDescription.SameJoinery(asDrawn, turned));
        Assert.False(JointDescription.SameJoinery(asDrawn, notTurned));
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void A_half_turn_about_y_swaps_east_with_west_and_bottom_with_top()
    {
        // About y: east <-> west and bottom <-> top; south and north stay.
        ImmutableArray<JointFact> asDrawn = [Pocket(BoxFace.East, BoxFace.South, 2), Groove(BoxFace.North, BoxFace.Bottom)];
        ImmutableArray<JointFact> turned = [Pocket(BoxFace.West, BoxFace.South, 2), Groove(BoxFace.North, BoxFace.Top)];
        ImmutableArray<JointFact> notTurned = [Pocket(BoxFace.West, BoxFace.North, 2), Groove(BoxFace.North, BoxFace.Top)];

        Assert.True(JointDescription.SameJoinery(asDrawn, turned));
        Assert.False(JointDescription.SameJoinery(asDrawn, notTurned));
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void A_half_turn_about_z_swaps_south_with_north_and_east_with_west_and_leaves_bottom_and_top()
    {
        // About z: south <-> north and east <-> west; bottom and top stay.
        ImmutableArray<JointFact> asDrawn = [Pocket(BoxFace.East, BoxFace.South, 2), Groove(BoxFace.North, BoxFace.Bottom)];
        ImmutableArray<JointFact> turned = [Pocket(BoxFace.West, BoxFace.North, 2), Groove(BoxFace.South, BoxFace.Bottom)];
        ImmutableArray<JointFact> notTurned = [Pocket(BoxFace.West, BoxFace.North, 2), Groove(BoxFace.South, BoxFace.Top)];

        Assert.True(JointDescription.SameJoinery(asDrawn, turned));
        Assert.False(JointDescription.SameJoinery(asDrawn, notTurned));
    }

    [Trait("Feature", "CUT-008")]
    [Fact]
    public void Joinery_is_compared_as_a_sorted_multiset_not_as_a_sequence_and_an_empty_set_equals_only_itself()
    {
        ImmutableArray<JointFact> ends = [Pocket(BoxFace.East, BoxFace.South, 3), Pocket(BoxFace.East, BoxFace.North, 2)];
        ImmutableArray<JointFact> reversed = [Pocket(BoxFace.East, BoxFace.North, 2), Pocket(BoxFace.East, BoxFace.South, 3)];

        Assert.True(JointDescription.SameJoinery(ends, reversed));
        Assert.True(JointDescription.SameJoinery([], []));
        Assert.False(JointDescription.SameJoinery(ends, []));
        Assert.False(JointDescription.SameJoinery(ends, [Pocket(BoxFace.East, BoxFace.South, 3)]));
        Assert.False(JointDescription.SameJoinery(ends, [Pocket(BoxFace.East, BoxFace.South, 3), Pocket(BoxFace.East, BoxFace.North, 3)]));
    }
}
