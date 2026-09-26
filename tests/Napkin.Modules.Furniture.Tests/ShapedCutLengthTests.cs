using System.Collections.Immutable;
using System.Text;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Issue #97: a shaped part's cut list gives the length a person marks on the board. For a mitred
/// piece that is the <em>long point</em> — the cut runs from a mark on one long edge to the far
/// corner of the end, so the other long edge keeps the whole blank — and it is the blank's stored
/// size, never a bounding box and never the short edge (<c>docs/design/shaped-parts-model.md</c>
/// §1.3, §4.1, §1.7).
/// </summary>
/// <remarks>
/// <para>
/// Every expected number here is worked out by hand in the comment beside it, from the rules in
/// the design document, in integer units of 1/1024" (<c>docs/design/geometry-model.md</c> §1.1).
/// None was copied from napkin's output (<c>CLAUDE.md</c>, "Data and citations").
/// </para>
/// <para>
/// A board 3 1/2" wide is 3.5 x 1024 = 3584 units wide. A 45° mitre on it is a
/// <see cref="CornerCut"/> with both setbacks 3584: the setback along the end is the whole end,
/// which is what makes it a mitre (§1.3), and equal setbacks are exactly 45°.
/// </para>
/// </remarks>
public sealed class ShapedCutLengthTests
{
    /// <summary>3 1/2" = 3.5 x 1024.</summary>
    private const long Width = 3584;

    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>A 3/4"-thick board lying flat: length across X, width up Y (3/4" = 768).</summary>
    private static Piece Flat => new(
        null, null, 1, new Length(768), new PlanAxes(PartDimension.Length, PartDimension.Width));

    /// <summary>A 2 1/2" square leg drawn as its footprint, 16 1/4" (16640) long out of plane.</summary>
    private static Piece Leg => new(
        null, null, 1, new Length(16640), new PlanAxes(PartDimension.Width, PartDimension.Thickness));

    private static Point2 At(long x, long y) => new(new Length(x), new Length(y));

    private static StraightSegment Line(long fromX, long fromY, long toX, long toY)
        => new(At(fromX, fromY), At(toX, toY));

    private static Box OnlyBox(Sketch sketch) => sketch.Entities.Values.OfType<Box>().Single();

    private static CornerCut Mitre(BoxCorner corner) => new(corner, new Length(Width), new Length(Width));

    // -----------------------------------------------------------------------------------------
    // (a) 45° mitres on a 3 1/2"-wide board.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void A_mitre_on_one_end_lists_the_long_point_which_is_the_short_edge_plus_the_width()
    {
        // The piece's short (inside) edge is to finish at 20 1/2" = 20.5 x 1024 = 20992. A 45°
        // mitre on one end adds the board's width to that edge at the other: long point =
        // 20992 + 3584 = 24576 = 24" = 2'-0". That is the blank drawn, 24" x 3 1/2", with the mitre
        // at its north-east corner so the north edge is the short one.
        Sketch sketch = Design.WithCutParts(("Rail", 24576, Width, Flat, [Mitre(BoxCorner.NorthEast)]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(24576, row.Length.Units);
        Assert.Equal("2'-0\"", CutListCsv.Text(row.Length));
        Assert.Equal(Width, row.Width.Units);

        // The outline, walked by hand per §1.5 (counter-clockwise from the south-west corner):
        // the south edge runs the whole blank, 0 -> 24576; the east edge is consumed entirely by
        // the whole-end setback and is omitted; the cut runs from the south-east corner
        // (24576, 0) to the mark 3584 back along the north edge, 24576 - 3584 = 20992; the north
        // edge runs back to x 0; the west edge closes it.
        Assert.Equal(
            [
                Line(0, 0, 24576, 0),
                Line(24576, 0, 20992, Width),
                Line(20992, Width, 0, Width),
                Line(0, Width, 0, 0),
            ],
            OutlineOf(sketch));

        // §4.4's mitre sentence: the whole setback (along Y, the east edge) names the end; the
        // other is the mark on the north edge; the far corner of the east end from north-east is
        // south-east; equal setbacks are exactly 45°, so no "≈".
        Assert.Equal(
            ["Mitre the east end: from 3 1/2\" in along the north edge to the south-east corner (45° off square)."],
            row.CutText);
    }

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void Mitres_on_both_ends_of_a_frame_piece_list_the_long_point_which_is_the_inside_edge_plus_twice_the_width()
    {
        // A frame piece: both mitres slope in towards the same (north, inside) edge, a trapezoid.
        // Inside edge 20" = 20480. Long point = 20480 + 2 x 3584 = 27648 = 27" = 2'-3".
        Sketch sketch = Design.WithCutParts(
            ("Rail", 27648, Width, Flat, [Mitre(BoxCorner.NorthWest), Mitre(BoxCorner.NorthEast)]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(27648, row.Length.Units);
        Assert.Equal("2'-3\"", CutListCsv.Text(row.Length));

        // South edge the whole blank; the north-east mitre from (27648, 0) to 27648 - 3584 = 24064
        // on the north edge; the north edge back to the north-west mark at 3584 (so the inside edge
        // is 24064 - 3584 = 20480 = 20"); the north-west mitre down to the south-west corner.
        // Both ends are consumed entirely and omitted.
        Assert.Equal(
            [
                Line(0, 0, 27648, 0),
                Line(27648, 0, 24064, Width),
                Line(24064, Width, Width, Width),
                Line(Width, Width, 0, 0),
            ],
            OutlineOf(sketch));

        // Cuts are said in site order (north-east before north-west, §1.6 invariant 5); the two
        // ends read differently, so they are two sentences rather than one.
        Assert.Equal(
            [
                "Mitre the east end: from 3 1/2\" in along the north edge to the south-east corner (45° off square).",
                "Mitre the west end: from 3 1/2\" in along the north edge to the south-west corner (45° off square).",
            ],
            row.CutText);
    }

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void Parallel_mitres_on_both_ends_list_the_long_point_which_is_either_edge_plus_the_width()
    {
        // Both mitres slope the same way (a parallelogram, as for a brace): the north-east and the
        // south-west corners come off. Each long edge is then 27648 - 3584 = 24064 = 23 1/2", and
        // the long point, tip to tip along the board, is that edge plus the width: 24064 + 3584 =
        // 27648, the blank.
        Sketch sketch = Design.WithCutParts(
            ("Brace", 27648, Width, Flat, [Mitre(BoxCorner.SouthWest), Mitre(BoxCorner.NorthEast)]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(27648, row.Length.Units);

        // Four sides: the two long edges and the two mitres; neither end survives. Where the walk
        // starts is §1.5's business, not this test's, so the sides are checked as a set.
        ImmutableArray<OutlineSegment> outline = OutlineOf(sketch);
        Assert.Equal(4, outline.Length);
        Assert.Contains(Line(Width, 0, 27648, 0), outline);          // south: 3584 .. 27648
        Assert.Contains(Line(27648, 0, 24064, Width), outline);      // north-east mitre
        Assert.Contains(Line(24064, Width, 0, Width), outline);      // north: 24064 .. 0
        Assert.Contains(Line(0, Width, Width, 0), outline);          // south-west mitre
    }

    // -----------------------------------------------------------------------------------------
    // (b) A chamfer and a clipped corner take nothing off the long point.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void A_chamfer_along_a_leg_leaves_its_length_and_section_as_drawn()
    {
        // A 1/2" x 1/2" (512 x 512) corner cut on the leg's footprint is a chamfer the whole length
        // of the leg (§1.4). The blank is still 16 1/4" x 2 1/2" x 2 1/2": 16640, 2560, 2560.
        Sketch sketch = Design.WithCutParts(
            ("Leg", 2560, 2560, Leg, [new CornerCut(BoxCorner.NorthEast, new Length(512), new Length(512))]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(16640, row.Length.Units);
        Assert.Equal(2560, row.Width.Units);
        Assert.Equal(2560, row.Thickness.Units);

        // The square clip sentence, ending "for the full ... length" because the out-of-plane
        // dimension is the length (16640 = 16 1/4" = 1'-4 1/4"), not the thickness.
        Assert.Equal(
            ["Cut off the north-east corner: mark 1/2\" along each edge from the corner, and cut between the marks, for the full 1'-4 1/4\" length."],
            row.CutText);
    }

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void A_clipped_corner_leaves_the_long_point_on_the_uncut_edge()
    {
        // 1" along the north edge (1024) and 2" down the east edge (2048) off a 24" x 3 1/2" board:
        // neither is a whole edge, so it is a clip, not a mitre. The south edge keeps the whole
        // 24576, so the long point is still the blank's 24".
        Sketch sketch = Design.WithCutParts(
            ("Rail", 24576, Width, Flat, [new CornerCut(BoxCorner.NorthEast, new Length(1024), new Length(2048))]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(24576, row.Length.Units);
        Assert.Equal(Width, row.Width.Units);

        // The east edge stops 2048 below the north-east corner, at y 3584 - 2048 = 1536, and the
        // clip runs from there to 24576 - 1024 = 23552 on the north edge.
        Assert.Equal(
            [
                Line(0, 0, 24576, 0),
                Line(24576, 0, 24576, 1536),
                Line(24576, 1536, 23552, Width),
                Line(23552, Width, 0, Width),
                Line(0, Width, 0, 0),
            ],
            OutlineOf(sketch));
    }

    // -----------------------------------------------------------------------------------------
    // (c) A rounded corner changes the outline and not the length.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void A_rounded_corner_changes_the_outline_but_not_the_long_point()
    {
        // A 1" (1024) radius at the north-east corner of the 24" x 3 1/2" board. The arc is
        // tangent to the east edge 1024 below the corner, at y 3584 - 1024 = 2560, and to the
        // north edge 1024 in from it, at x 24576 - 1024 = 23552; its centre is (23552, 2560).
        Sketch sketch = Design.WithCutParts(
            ("Rail", 24576, Width, Flat, [new RoundedCorner(BoxCorner.NorthEast, new Length(1024))]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(24576, row.Length.Units);
        Assert.Equal(Width, row.Width.Units);

        Assert.Equal(
            [
                Line(0, 0, 24576, 0),
                Line(24576, 0, 24576, 2560),
                new ArcByCenter(At(24576, 2560), At(23552, Width), At(23552, 2560)),
                Line(23552, Width, 0, Width),
                Line(0, Width, 0, 0),
            ],
            OutlineOf(sketch));

        Assert.Equal(["Round the north-east corner to a 1\" radius."], row.CutText);
    }

    // -----------------------------------------------------------------------------------------
    // (d) A notch. The model's three cut kinds (§1.3) have no square notch — that would be joinery,
    // which §1.7 keeps out — so the cut-out it does have, an inward curve ("a cut-out for a hand
    // hold on a plain edge"), is the case tested.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void A_scalloped_cut_out_in_an_edge_changes_the_outline_but_not_the_blank()
    {
        // A 1" (1024) scallop in the south edge of the 24" x 3 1/2" board. §1.5: an inward curve
        // runs the adjacent edges to their corners and replaces the edge with an arc from corner to
        // corner through the middle of the edge moved in by the depth: (24576 / 2, 0 + 1024) =
        // (12288, 1024). Both corners stay, so the long point is the whole 24".
        Sketch sketch = Design.WithCutParts(
            ("Rail", 24576, Width, Flat, [new CurvedEdge(BoxEdge.South, Bow.Inward, new Length(1024))]));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));
        Assert.Equal(24576, row.Length.Units);
        Assert.Equal(Width, row.Width.Units);

        Assert.Equal(
            [
                new ArcThrough(At(0, 0), At(12288, 1024), At(24576, 0)),
                Line(24576, 0, 24576, Width),
                Line(24576, Width, 0, Width),
                Line(0, Width, 0, 0),
            ],
            OutlineOf(sketch));
    }

    // -----------------------------------------------------------------------------------------
    // (e) A shape that would leave nothing is refused, never listed as a zero.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "GEO-008")]
    public void Frame_mitres_on_a_board_too_short_for_them_are_refused()
    {
        // Two inward 45° mitres on a 3 1/2"-wide board claim 3584 + 3584 = 7168 of the north edge
        // (§1.6 invariant 8). A 6" (6144) board has less than that, so the mitres would cross.
        Box tooShort = OnlyBox(Design.WithCutParts(
            ("Rail", 6144, Width, Flat, [Mitre(BoxCorner.NorthWest), Mitre(BoxCorner.NorthEast)])));

        Assert.Contains(ValidationErrorKind.CutDoesNotFit, Sketch.Empty.WithEntity(tooShort).Validate().Errors.Select(e => e.Kind));
        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(Sketch.Empty, new AddEntity(tooShort)));
        Assert.Equal(RejectionReason.CutDoesNotFit, rejected.Reason);

        // At exactly 7" (7168) the two mitres meet in a point on the north edge (equality is
        // allowed), what is left is a triangle of area 7168 x 3584 / 2 > 0, and it is listed at
        // its long point, the whole 7".
        Sketch justLongEnough = Design.WithCutParts(
            ("Rail", 7168, Width, Flat, [Mitre(BoxCorner.NorthWest), Mitre(BoxCorner.NorthEast)]));
        Assert.True(justLongEnough.Validate().IsValid, justLongEnough.Validate().ToString());
        Assert.Equal(7168, Assert.Single(CutList.Of(justLongEnough, Library)).Length.Units);
    }

    [Fact]
    [Trait("Feature", "GEO-008")]
    public void A_file_whose_mitres_leave_nothing_of_a_piece_is_refused_so_no_zero_row_can_be_listed()
    {
        // A 3 1/2" x 3 1/2" block with parallel 45° mitres at its south-west and north-east
        // corners: each is corner to corner, 3584 along both edges, so between them they take both
        // halves of the square. Every edge budget holds (3584 <= 3584, invariant 8), and the area
        // left is 3584 x 3584 - 2 x (3584 x 3584 / 2) = 0, which invariant 9 refuses. The loader
        // refuses a file containing such a box (§5), so the cut list never sees it.
        const string scene = """
            {
              "formatVersion": 12,
              "units": { "length": "inch/1024", "angle": "arcsecond" },
              "layers": [ { "id": "00000000-0000-0000-0000-000000000001", "name": "Default" } ],
              "entities": [
                { "id": "0192f1a0-0000-4000-8000-00000000000a", "type": "box", "layer": "00000000-0000-0000-0000-000000000001",
                  "name": "Block", "phase": "new",
                  "anchor": { "x": 0, "y": 0, "z": 0 }, "width": 3584, "height": 3584, "depth": 768, "faceUp": "top", "rotation": 0,
                  "part": { "stock": null, "species": null, "quantity": 1, "planAxes": { "x": "length", "y": "width" }, "hardware": [], "rough": false, "grain": null, "showFace": null },
                  "wall": null, "room": null, "cuts": [
                    { "kind": "cornerCut", "corner": "southWest", "alongX": 3584, "alongY": 3584 },
                    { "kind": "cornerCut", "corner": "northEast", "alongX": 3584, "alongY": 3584 }
                  ] }
              ],
              "fastenerChoices": [], "supplies": [], "code": null, "site": { "groundSnowLoad": null, "ultimateWindSpeed": null, "seismicDesignCategory": null, "frostDepth": null, "buildingWidth": null, "roofLiveLoad": null, "source": null },
              "relationships": []
            }
            """;

        using MemoryStream stream = new(Encoding.UTF8.GetBytes(scene));
        Refused refused = Assert.IsType<Refused>(SceneReader.Read(stream));
        Assert.NotEmpty(refused.Problems);

        // The same box built in memory: the updater refuses it for the same reason.
        Box block = OnlyBox(Design.WithCutParts(
            ("Block", Width, Width, Flat, [Mitre(BoxCorner.SouthWest), Mitre(BoxCorner.NorthEast)])));
        Assert.Equal(
            [ValidationErrorKind.NonPositiveArea],
            Sketch.Empty.WithEntity(block).Validate().Errors.Select(e => e.Kind));
    }

    // -----------------------------------------------------------------------------------------
    // The picture-frame sample (samples/picture-frame.*): its rows are held with the other
    // samples' by SampleCutListTests; this checks the one thing only a frame shows — that the four
    // mitred outlines, two of them turned half a turn, close around the opening they were drawn for.
    // -----------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void The_picture_frames_four_mitred_pieces_close_around_an_8_by_10_opening()
    {
        Sketch frame = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("picture-frame"))).Sketch;

        // An outline is in the box's own local frame (Outline's remarks); each segment is placed
        // in the plan through the box's own placement, as its corners are.
        List<(long, long, long, long)> Of(string name)
        {
            Box box = frame.Entities.Values.OfType<Box>().Single(box => box.Name == name);
            Point3 Place(Point2 local) => box.World(new Vector3(local.X, local.Y, Length.Zero));
            return
            [
                .. box.Outline().Segments.Select(segment => (Place(segment.From), Place(segment.To)))
                    .Select(line => (line.Item1.X.Units, line.Item1.Y.Units, line.Item2.X.Units, line.Item2.Y.Units)),
            ];
        }

        // The opening is x 1 1/2 .. 9 1/2 (1536 .. 9728, 8" = 8192 across) and y 1 1/2 .. 11 1/2
        // (1536 .. 11776, 10" = 10240 up), from the design's 1 1/2" moulding around 8" x 10".
        // Each piece's inside edge runs between its two mitre marks, 1 1/2" in from each end.
        //   Bottom rail, as drawn: local north edge from (11 - 1 1/2, 1 1/2) back to (1 1/2, 1 1/2).
        Assert.Contains((9728L, 1536L, 1536L, 1536L), Of("Rail, bottom"));
        //   Left stile, as drawn: local east edge from (1 1/2, 1 1/2) up to (1 1/2, 13 - 1 1/2).
        Assert.Contains((1536L, 1536L, 1536L, 11776L), Of("Stile, left"));
        //   Top rail, turned 180° about its anchor (11, 13): local (x, y) lands at (11 - x, 13 - y),
        //   so its north edge (9728, 1536) -> (1536, 1536) lands at (1536, 11776) -> (9728, 11776).
        Assert.Contains((1536L, 11776L, 9728L, 11776L), Of("Rail, top"));
        //   Right stile, turned likewise: its east edge (1536, 1536) -> (1536, 11776) lands at
        //   (9728, 11776) -> (9728, 1536).
        Assert.Contains((9728L, 11776L, 9728L, 1536L), Of("Stile, right"));

        // And at the bottom-right corner the two mitres are one line: the bottom rail's
        // north-east mitre runs from its corner (11, 0) to its mark (9 1/2, 1 1/2); the right
        // stile's local north-east mitre runs from its mark (1 1/2, 11 1/2) to its local corner
        // (0, 13), which land at (9 1/2, 1 1/2) and (11, 0) — the same line walked the other way.
        Assert.Contains((11264L, 0L, 9728L, 1536L), Of("Rail, bottom"));
        Assert.Contains((9728L, 1536L, 11264L, 0L), Of("Stile, right"));
    }

    private static ImmutableArray<OutlineSegment> OutlineOf(Sketch sketch) => OnlyBox(sketch).Outline().Segments;
}
