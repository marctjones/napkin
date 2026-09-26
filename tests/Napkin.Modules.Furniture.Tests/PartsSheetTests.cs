using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The Parts view's model is the cut list (docs/design/parts-view.md §1, §7.1): one cell per row, a
/// count that is the row's quantity, and a blank in the piece's own frame.
/// </summary>
public sealed class PartsSheetTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>Every sample shipped beside the tests.</summary>
    public static IEnumerable<object[]> Samples =>
        Directory.GetFiles(ExpectedFixture.Directory, "*.scene.json")
            .Select(path => Path.GetFileName(path)[..^".scene.json".Length])
            .Order(StringComparer.Ordinal)
            .Select(name => new object[] { name });

    private static Sketch Read(string fixture) =>
        Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture))).Sketch;

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Feature", "CUT-019")]
    public void The_sheet_is_the_cut_list_one_cell_per_row_in_its_order(string fixture)
    {
        ImmutableArray<CutListRow> rows = CutList.Of(Read(fixture), Library);
        ImmutableArray<PartsCell> cells = PartsSheet.Of(rows);

        Assert.Equal(rows.Length, cells.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Same(rows[i], cells[i].Row);
            Assert.Equal(rows[i].Quantity, cells[i].Count);
            Assert.Equal(rows[i].Members, cells[i].Members);
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Feature", "CUT-020")]
    public void The_counts_add_up_to_the_pieces_not_to_the_boxes(string fixture)
    {
        Sketch sketch = Read(fixture);
        int pieces = sketch.Entities.Values.OfType<Box>().Where(box => box.Part is not null).Sum(box => box.Part!.Quantity);

        Assert.Equal(pieces, PartsSheet.Of(CutList.Of(sketch, Library)).Sum(cell => cell.Count));
    }

    [Fact]
    [Trait("Feature", "CUT-020")]
    public void The_coffee_tables_four_legs_are_one_cell_and_each_apron_length_one_cell_of_two()
    {
        ImmutableArray<PartsCell> cells = PartsSheet.Of(CutList.Of(Read("coffee-table"), Library));

        Assert.Equal(["Top", "Apron, long", "Leg", "Apron, short"], cells.Select(cell => cell.Row.Label));
        Assert.Equal([1, 2, 4, 2], cells.Select(cell => cell.Count));
    }

    [Fact]
    public void A_duplicated_leg_raises_the_legs_count_and_a_leg_a_1024th_longer_is_a_cell_of_its_own()
    {
        Sketch table = Read("coffee-table");
        Box leg = table.Entities.Values.OfType<Box>().First(box => box.Name == "Leg, south-west");

        Sketch fiveLegs = table.WithEntity(leg with { Id = EntityId.New() });
        ImmutableArray<PartsCell> five = PartsSheet.Of(CutList.Of(fiveLegs, Library));
        Assert.Equal(4, five.Length);
        Assert.Equal(5, five.Single(cell => cell.Row.Label == "Leg").Count);

        // The leg's length is whichever stored dimension its plan axes do not name as width or
        // thickness; one 1/1024" more there splits it off from the other three.
        Sketch longer = table.WithEntity(Lengthened(leg));
        ImmutableArray<CutListRow> rows = CutList.Of(longer, Library);
        ImmutableArray<PartsCell> cells = PartsSheet.Of(rows);
        Assert.Equal(5, rows.Length);
        Assert.Equal(rows, cells.Select(cell => cell.Row));
        Assert.Equal([1, 3], cells.Where(cell => cell.Row.Label.StartsWith("Leg", StringComparison.Ordinal)).Select(cell => cell.Count).Order());
    }

    private static Box Lengthened(Box leg)
    {
        PlanAxes axes = leg.Part!.PlanAxes;
        Length more = new(1);
        return axes.X == PartDimension.Length ? leg with { Width = leg.Width + more }
            : axes.Y == PartDimension.Length ? leg with { Height = leg.Height + more }
            : leg with { Depth = leg.Depth + more };
    }

    [Theory]
    [InlineData("stud")]
    [InlineData("board")]
    [InlineData("edge")]
    public void The_blank_is_the_piece_in_its_own_frame_whichever_of_the_24_ways_it_is_turned(string part)
    {
        (Sketch sketch, Length length, Length width, Length thickness, PlanAxes axes) = Part(part);
        Box box = sketch.Entities.Values.OfType<Box>().Single();

        foreach (BoxFace face in Enum.GetValues<BoxFace>())
        {
            for (int quarter = 0; quarter < 4; quarter++)
            {
                Sketch turned = sketch.WithEntity(box with { FaceUp = face, Rotation = Angle.Zero.Rotate90(quarter), Anchor = new Point3(Length.Inches(12), Length.Inches(-7), Length.Inches(3)) });
                PartsCell cell = Assert.Single(PartsSheet.Of(CutList.Of(turned, Library)));

                Assert.Equal(Point3.Origin, cell.Blank.Anchor);
                Assert.Equal(Angle.Zero, cell.Blank.Rotation);
                Assert.Equal(BoxFace.Top, cell.Blank.FaceUp);
                Assert.Equal(Size(axes.X, length, width, thickness), cell.Blank.Width);
                Assert.Equal(Size(axes.Y, length, width, thickness), cell.Blank.Height);
                Assert.Equal(Size(axes.OutOfPlane, length, width, thickness), cell.Blank.Depth);
            }
        }
    }

    private static Length Size(PartDimension dimension, Length length, Length width, Length thickness) => dimension switch
    {
        PartDimension.Length => length,
        PartDimension.Width => width,
        _ => thickness,
    };

    /// <summary>
    /// A 2x4 stud 29 1/4" long drawn on edge (1 1/2" × 3 1/2" is the library's), a 3/4" board
    /// 48" × 5 1/2" drawn flat, and a 3/4" apron 30" × 3 1/2" drawn on edge (length × thickness in plan).
    /// </summary>
    private static (Sketch, Length Length, Length Width, Length Thickness, PlanAxes) Part(string name) => name switch
    {
        "stud" => (
            Design.WithParts(("Stud", Length.Inches(29, 1, 4).Units, Length.Inches(1, 1, 2).Units,
                new Piece("2x4", null, 1, Length.Inches(3, 1, 2), new PlanAxes(PartDimension.Length, PartDimension.Thickness)))),
            Length.Inches(29, 1, 4), Length.Inches(3, 1, 2), Length.Inches(1, 1, 2), new PlanAxes(PartDimension.Length, PartDimension.Thickness)),
        "board" => (
            Design.WithParts(("Board", Length.Inches(48).Units, Length.Inches(5, 1, 2).Units,
                new Piece(null, null, 1, Length.Inches(0, 3, 4), new PlanAxes(PartDimension.Length, PartDimension.Width)))),
            Length.Inches(48), Length.Inches(5, 1, 2), Length.Inches(0, 3, 4), new PlanAxes(PartDimension.Length, PartDimension.Width)),
        _ => (
            Design.WithParts(("Apron", Length.Inches(30).Units, Length.Inches(0, 3, 4).Units,
                new Piece(null, null, 1, Length.Inches(3, 1, 2), new PlanAxes(PartDimension.Length, PartDimension.Thickness)))),
            Length.Inches(30), Length.Inches(3, 1, 2), Length.Inches(0, 3, 4), new PlanAxes(PartDimension.Length, PartDimension.Thickness)),
    };

    [Fact]
    public void A_shaped_part_draws_the_rows_arcs_on_a_blank_of_the_rows_size()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(Read("rounded-corner-table"), Library);
        PartsCell shaped = PartsSheet.Of(rows).First(cell => !cell.Row.Cuts.IsEmpty);

        Assert.Contains(shaped.Outline.Segments, segment => segment is ArcByCenter or ArcThrough);
        Assert.Equal(shaped.Row.Cuts, shaped.Blank.Cuts);
        Assert.Equal(
            new[] { shaped.Row.Length, shaped.Row.Width, shaped.Row.Thickness }.Order(),
            new[] { shaped.Blank.Width, shaped.Blank.Height, shaped.Blank.Depth }.Order());
        Assert.Same(shaped.Outline, shaped.Outline);
        Assert.Same(shaped.Solid, shaped.Solid);
    }

    [Fact]
    public void Groups_are_by_stock_known_first_in_order_of_appearance_then_unknown_names_then_no_stock()
    {
        // The stocked bench's rows are Top (3/4 plywood), Apron (1x4), Stretcher (2x4), Leg (2x4),
        // End rail (1x4). Add the largest piece of all in a stock the library does not know, and a
        // small one with no stock: known stocks still come first, the unknown name next, no stock last.
        Sketch bench = Read("stocked-bench");
        Box unknown = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, Length.Inches(96), Length.Inches(24), Length.Inches(1), Angle.Zero) with
        {
            Name = "Slab",
            Part = new Part("Unobtainium slab", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
        Box loose = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, Length.Inches(3), Length.Inches(2), Length.Inches(1), Angle.Zero) with
        {
            Name = "Cleat",
            Part = new Part(null, null, 2, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
        ImmutableArray<PartsCell> cells = PartsSheet.Of(CutList.Of(bench.WithEntity(unknown).WithEntity(loose), Library));
        Assert.Equal("Slab", cells[0].Row.Label);

        ImmutableArray<PartsGroup> groups = PartsSheet.GroupedByStock(cells);

        Assert.Equal(
            ["3/4 plywood", "1x4", "2x4", CutListRow.UnresolvedText("Unobtainium slab"), PartsSheet.NoStockTitle],
            groups.Select(group => group.Title));
        Assert.Equal(["Apron", "End rail"], groups[1].Cells.Select(cell => cell.Row.Label));
        Assert.Equal(["Stretcher", "Leg"], groups[2].Cells.Select(cell => cell.Row.Label));
        Assert.Equal(["Cleat"], groups[4].Cells.Select(cell => cell.Row.Label));
        // Every cell in exactly one group: together they are the sheet, nothing twice, nothing left out.
        Assert.Equal(cells, groups.SelectMany(group => group.Cells).OrderBy(cell => cells.IndexOf(cell)));
    }

    [Fact]
    public void The_same_rows_give_equal_cells_and_a_cell_is_equal_to_another_for_the_same_row_only()
    {
        ImmutableArray<CutListRow> rows = CutList.Of(Read("coffee-table"), Library);
        ImmutableArray<PartsCell> once = PartsSheet.Of(rows), again = PartsSheet.Of(rows);

        Assert.Equal(once, again);
        Assert.Equal(once[0].GetHashCode(), again[0].GetHashCode());
        Assert.NotEqual(once[0], once[1]);
        Assert.False(once[0].Equals(null));
        Assert.False(once[0].Equals((object)"Top"));
        Assert.NotEqual(once[0].Blank.Id, again[0].Blank.Id);
        Assert.Throws<ArgumentNullException>(() => new PartsCell(null!));
        Assert.Throws<ArgumentNullException>(() => PartsCell.BlankFor(null!));
        Assert.Throws<ArgumentNullException>(() => PartsSheet.Of(null!));
    }
}
