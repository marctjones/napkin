using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// How a Parts view cell shows its piece, the one scale they share and what a cell says
/// (docs/design/parts-view.md §1.2 as amended, §2.1, §2.2). The scales are the note's own worked
/// numbers.
/// </summary>
public sealed class PartsPictureTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    private static ImmutableArray<PartsCell> Sheet(string fixture) =>
        PartsSheet.Of(CutList.Of(Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture))).Sketch, Library));

    private static ImmutableArray<PartsCell> Sheet(Sketch sketch) => PartsSheet.Of(CutList.Of(sketch, Library));

    private static PartsPicture Picture(ImmutableArray<PartsCell> cells, string label) =>
        PartsPicture.Of(cells.Single(cell => cell.Row.Label == label));

    [Fact]
    [Trait("Feature", "CUT-021")]
    public void The_coffee_table_draws_its_top_flat_its_aprons_on_edge_and_its_legs_face_on()
    {
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        PartsPicture top = Picture(cells, "Top"), apron = Picture(cells, "Apron, long"), leg = Picture(cells, "Leg");

        Assert.Equal((PartsPose.Flat, Length.Inches(48), Length.Inches(24), false), (top.Pose, top.Horizontal, top.Vertical, top.QuarterTurn));
        Assert.Equal(CutListCsv.Text(Length.Inches(0, 3, 4)) + " thick", top.Caption);

        // The apron stands on edge: 40" by 3/4" in the plan, its 3 1/2" width out of it.
        Assert.Equal((PartsPose.OnEdge, Length.Inches(40), Length.Inches(0, 3, 4)), (apron.Pose, apron.Horizontal, apron.Vertical));
        Assert.Equal(CutListCsv.Text(Length.Inches(3, 1, 2)) + " wide — shown on edge", apron.Caption);

        // The leg stands on end with no cuts: drawn 16 1/4" by 2 1/2", as the note's example has it.
        Assert.Equal((PartsPose.FaceOn, Length.Inches(16, 1, 4), Length.Inches(2, 1, 2), false), (leg.Pose, leg.Horizontal, leg.Vertical, leg.QuarterTurn));
        Assert.Equal(CutListCsv.Text(Length.Inches(2, 1, 2)) + " thick", leg.Caption);
        Assert.Equal(CutListCsv.Text(Length.Inches(16, 1, 4)), leg.HorizontalText);
        Assert.Equal(CutListCsv.Text(Length.Inches(2, 1, 2)), leg.VerticalText);
        Assert.Equal(4, leg.Outline.Vertices.Length);
    }

    [Fact]
    [Trait("Feature", "CUT-021")]
    public void The_coffee_tables_height_binds_so_the_top_fills_its_drawing_and_a_leg_is_to_scale()
    {
        // §2.1: 224 × 100 px of drawing; the top, 48" × 24", fits at min(224/48, 100/24) = 100/24.
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        double sheet = PartsScale.Sheet(cells.Select(PartsPicture.Of))!.Value;
        Assert.Equal(100.0 / 24, sheet, 9);

        (double topScale, bool topFlag) = PartsScale.Cell(Picture(cells, "Top"), sheet);
        Assert.Equal((100.0 / 24, false), (topScale, topFlag));
        Assert.Equal(PartsScale.DrawingHeight, 24 * topScale, 9);

        // The leg's 16 1/4" at that scale is 67.7 px, over the 48 px floor: drawn to scale.
        (double legScale, bool legFlag) = PartsScale.Cell(Picture(cells, "Leg"), sheet);
        Assert.Equal((sheet, false), (legScale, legFlag));
    }

    [Fact]
    [Trait("Feature", "CUT-021")]
    public void A_shim_beside_a_forty_foot_plate_is_drawn_at_the_floor_and_says_it_is_not_to_scale()
    {
        // §2.1's scale-extremes numbers: 224 / 480 px/in; the 6" shim would be 2.8 px long, so it is
        // drawn 48 px long at 8 px/in and flagged.
        ImmutableArray<PartsCell> cells = Sheet("scale-extremes");
        double sheet = PartsScale.Sheet(cells.Select(PartsPicture.Of))!.Value;
        Assert.Equal(224.0 / 480, sheet, 9);

        (double plateScale, bool plateFlag) = PartsScale.Cell(Picture(cells, "Plate"), sheet);
        (double shimScale, bool shimFlag) = PartsScale.Cell(Picture(cells, "Shim"), sheet);
        Assert.Equal((sheet, false), (plateScale, plateFlag));
        Assert.Equal((8.0, true), (shimScale, shimFlag));

        PartsCell shim = cells.Single(cell => cell.Row.Label == "Shim");
        Assert.EndsWith(" · not to scale", PartsCellText.Details(shim, Picture(cells, "Shim"), notToScale: true), StringComparison.Ordinal);
        Assert.DoesNotContain("not to scale", PartsCellText.Details(shim, Picture(cells, "Shim"), notToScale: false), StringComparison.Ordinal);
    }

    [Fact]
    public void A_part_drawn_long_way_up_is_turned_a_quarter_so_its_length_lies_across()
    {
        // A rail drawn 3 1/2" across and 30" up (its length is the plan's y): turned, 30" across.
        Sketch rail = Design.WithParts(("Rail", Length.Inches(3, 1, 2).Units, Length.Inches(30).Units,
            new Piece(null, null, 1, Length.Inches(0, 3, 4), new PlanAxes(PartDimension.Width, PartDimension.Length))));
        PartsPicture picture = PartsPicture.Of(Assert.Single(Sheet(rail)));

        Assert.Equal((PartsPose.Flat, true, Length.Inches(30), Length.Inches(3, 1, 2)), (picture.Pose, picture.QuarterTurn, picture.Horizontal, picture.Vertical));
    }

    [Fact]
    public void A_squat_piece_on_end_puts_its_wider_side_across()
    {
        // 2" long, 5" wide, 1" thick, standing on end: face-on it is 5" across and 2" up.
        Sketch block = Design.WithParts(("Block", Length.Inches(5).Units, Length.Inches(1).Units,
            new Piece(null, null, 1, Length.Inches(2), new PlanAxes(PartDimension.Width, PartDimension.Thickness))));
        PartsPicture picture = PartsPicture.Of(Assert.Single(Sheet(block)));

        Assert.Equal((PartsPose.FaceOn, Length.Inches(5), Length.Inches(2)), (picture.Pose, picture.Horizontal, picture.Vertical));
        Assert.Equal(CutListCsv.Text(Length.Inches(1)) + " thick", picture.Caption);
    }

    [Fact]
    [Trait("Feature", "CUT-021")]
    public void A_cut_piece_on_end_is_drawn_as_it_lies_because_its_cuts_are_only_there()
    {
        // A 2 1/2" square leg 16 1/4" long with a corner cut in its plan: the end view, with the cut.
        Sketch leg = Design.WithCutParts(("Leg", 2560, 2560,
            new Piece(null, null, 1, Length.Inches(16, 1, 4), new PlanAxes(PartDimension.Width, PartDimension.Thickness)),
            [new CornerCut(BoxCorner.NorthEast, new Length(512), new Length(512))]));
        PartsCell cell = Assert.Single(Sheet(leg));
        PartsPicture picture = PartsPicture.Of(cell);

        Assert.Equal(PartsPose.OnEnd, picture.Pose);
        Assert.Same(cell.Outline, picture.Outline);
        Assert.Equal(5, picture.Outline.Vertices.Length);
        Assert.Equal(CutListCsv.Text(Length.Inches(16, 1, 4)) + " long — shown on end", picture.Caption);
    }

    [Fact]
    public void Nothing_to_draw_has_no_scale_and_a_picture_with_no_extent_one_way_is_fitted_the_other()
    {
        Outline none = Box.AsDrawn(EntityId.New(), LayerId.New(), Point2.Origin, Length.Inches(1), Length.Inches(1), Length.Inches(1), Angle.Zero).Outline();
        PartsPicture flatLine = new(PartsPose.Flat, none, false, Length.Inches(20), Length.Zero, "x");
        PartsPicture upright = new(PartsPose.Flat, none, false, Length.Zero, Length.Inches(4), "x");
        PartsPicture point = new(PartsPose.Flat, none, false, Length.Zero, Length.Zero, "x");

        Assert.Null(PartsScale.Sheet([]));
        Assert.Null(PartsScale.Sheet([point]));
        Assert.Equal(PartsScale.DrawingWidth / 20, PartsScale.Sheet([flatLine])!.Value, 9);
        Assert.Equal(PartsScale.DrawingHeight / 4, PartsScale.Sheet([upright, point])!.Value, 9);
        Assert.Equal((3.0, false), PartsScale.Cell(point, 3));
    }

    [Fact]
    [Trait("Feature", "CUT-021")]
    public void A_cell_says_its_count_its_material_and_at_most_two_cuts_before_how_many_more()
    {
        ImmutableArray<PartsCell> bench = Sheet("stocked-bench");
        PartsCell leg = bench.Single(cell => cell.Row.Label == "Leg");
        Assert.Equal("×4", PartsCellText.Badge(leg));
        Assert.Equal(PartsPicture.Of(leg).Caption + " · 2x4", PartsCellText.Details(leg, PartsPicture.Of(leg), notToScale: false));

        // No stock: the caption alone.
        PartsCell top = Sheet("coffee-table").Single(cell => cell.Row.Label == "Top");
        Assert.Equal("×1", PartsCellText.Badge(top));
        Assert.Equal(PartsPicture.Of(top).Caption, PartsCellText.Details(top, PartsPicture.Of(top), notToScale: false));
        Assert.Empty(PartsCellText.Cuts(top));

        Sketch three = Design.WithCutParts(("Panel", Length.Inches(20).Units, Length.Inches(10).Units,
            new Piece(null, null, 1, Length.Inches(0, 3, 4), new PlanAxes(PartDimension.Length, PartDimension.Width)),
            [
                new CornerCut(BoxCorner.NorthEast, new Length(1024), new Length(1024)),
                new CornerCut(BoxCorner.SouthWest, new Length(2048), new Length(1024)),
                new CornerCut(BoxCorner.NorthWest, new Length(1024), new Length(3072)),
            ]));
        PartsCell panel = Assert.Single(Sheet(three));
        Assert.Equal(3, panel.Row.CutText.Length);
        Assert.Equal([panel.Row.CutText[0], panel.Row.CutText[1], "(+1 more)"], PartsCellText.Cuts(panel));
    }

    [Fact]
    public void Nothing_is_described_without_a_cell()
    {
        Assert.Throws<ArgumentNullException>(() => PartsPicture.Of(null!));
        Assert.Throws<ArgumentNullException>(() => PartsScale.Sheet(null!));
        Assert.Throws<ArgumentNullException>(() => PartsScale.Cell(null!, 1));
        Assert.Throws<ArgumentNullException>(() => PartsCellText.Badge(null!));
        Assert.Throws<ArgumentNullException>(() => PartsCellText.Cuts(null!));
        PartsCell top = Sheet("coffee-table")[0];
        Assert.Throws<ArgumentNullException>(() => PartsCellText.Details(null!, PartsPicture.Of(top), false));
        Assert.Throws<ArgumentNullException>(() => PartsCellText.Details(top, null!, false));
    }
}
