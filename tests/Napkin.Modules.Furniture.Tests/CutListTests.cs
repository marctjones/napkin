using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The cut list, stated in <c>docs/design/parts-and-cut-list.md</c> §3: what it collects, how it
/// names three dimensions from two, how it groups, and what order it puts the rows in.
/// </summary>
public sealed class CutListTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Four_identical_legs_are_one_row_of_four()
    {
        Sketch sketch = Design.WithParts(
            ("Leg, south-west", 2560, 2560, Leg),
            ("Leg, south-east", 2560, 2560, Leg),
            ("Leg, north-west", 2560, 2560, Leg),
            ("Leg, north-east", 2560, 2560, Leg));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Equal("Leg", row.Label);
        Assert.Equal(4, row.Quantity);
        Assert.Equal(16640, row.Length.Units);
        Assert.Equal(2560, row.Width.Units);
        Assert.Equal(2560, row.Thickness.Units);
        Assert.Equal(4, row.Members.Length);

        // Every row carries final dimensions, quantity and material, which is the third half of
        // the issue's grouping criterion.
        Assert.Empty(row.Material);
        Assert.False(row.Unresolved);
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Parts_of_different_sizes_are_different_rows_however_they_are_named()
    {
        // Exact integer equality, no tolerance: a 10" part and a 10 1/64" part are two parts, and
        // a tolerance would quietly merge them.
        Sketch sketch = Design.WithParts(
            ("Rail", 10240, 768, Apron),
            ("Rail", 10256, 768, Apron));

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, Library);

        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal(1, row.Quantity));
        Assert.Equal(10256, rows[0].Length.Units);
        Assert.Equal(10240, rows[1].Length.Units);
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Parts_of_one_size_in_different_species_are_different_rows()
    {
        Sketch sketch = Design.WithParts(
            ("Rail", 10240, 768, Apron with { Species = "white oak" }),
            ("Rail", 10240, 768, Apron with { Species = "walnut" }));

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, Library);

        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal("Rail", row.Label));
    }

    [Theory]
    [Trait("Feature", "CUT-002")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_rotated_part_lists_the_size_that_was_typed(int quarterTurns)
    {
        Sketch sketch = Design.WithRotatedPart("Shelf", 10240, 4096, Apron, quarterTurns);

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Equal(10240, row.Length.Units);
        Assert.Equal(3584, row.Width.Units);
        Assert.Equal(4096, row.Thickness.Units);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_part_declared_as_a_2x4_reports_the_librarys_name_and_carries_its_item()
    {
        // The acceptance criterion of #8: a 2x4 resolves through the materials library, and the
        // row carries the item so that the shopping list (#9) can aggregate it.
        Sketch sketch = Design.WithParts(("Stud", 1536, 3584, Apron with { Stock = "2 X 4" }));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Equal("2x4", row.Material);
        Assert.Equal("2x4", row.MaterialText);
        Assert.False(row.Unresolved);

        LumberStock stud = Assert.IsType<LumberStock>(row.Stock);
        Assert.Equal(1536, stud.Thickness.Units);
        Assert.Equal(3584, stud.Width.Units);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_stock_name_that_does_not_resolve_survives_as_a_row_that_says_so()
    {
        Sketch sketch = Design.WithParts(("Shelf", 10240, 768, Apron with { Stock = "9x17 unobtainium" }));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.True(row.Unresolved);
        Assert.Null(row.Stock);
        Assert.Equal("9x17 unobtainium", row.Material);
        Assert.Contains("not in this build's materials library", row.MaterialText, StringComparison.Ordinal);

        // Never silently dropped and never guessed at: the piece is still in the list, at the size
        // the design states.
        Assert.Equal(10240, row.Length.Units);
    }

    [Fact]
    [Trait("Feature", "CUT-001")]
    public void A_part_with_no_stock_at_all_lists_with_an_empty_material()
    {
        Sketch sketch = Design.WithParts(("Shelf", 10240, 768, Apron));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Empty(row.Material);
        Assert.Empty(row.MaterialText);
        Assert.False(row.Unresolved);
        Assert.Null(row.Stock);
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Two_spellings_of_one_stock_are_one_row()
    {
        // The library is keyed by the normalised nominal name, so "2x4" and "2 X 4" are one stock
        // and two parts cut from them are one row.
        Sketch sketch = Design.WithParts(
            ("Stud", 1536, 3584, Apron with { Stock = "2x4" }),
            ("Stud", 1536, 3584, Apron with { Stock = "2 X 4" }));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Equal(2, row.Quantity);
        Assert.Equal("2x4", row.Material);
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void A_box_that_is_not_a_part_is_not_in_the_cut_list()
    {
        // A wall and an opening are boxes nobody cuts. That is not an error and not an empty row.
        Sketch sketch = Design.WithParts(("Shelf", 10240, 768, Apron))
            .WithEntity(new Box(
                EntityId.New(),
                LayerId.Default,
                Point2.Inches(0, 0),
                new Length(147456),
                new Length(5632),
                Angle.Zero) { Name = "Wall" });

        Assert.Single(CutList.Of(sketch, Library));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void A_design_with_nothing_to_cut_has_an_empty_cut_list()
        => Assert.Empty(CutList.Of(Sketch.Empty, Library));

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Rows_come_out_largest_first()
    {
        Sketch sketch = Design.WithParts(
            ("Short", 8192, 768, Apron),
            ("Long", 40960, 768, Apron),
            ("Middle", 16384, 768, Apron));

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, Library);

        Assert.Equal(["Long", "Middle", "Short"], rows.Select(row => row.Label));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Rows_of_one_length_are_ordered_by_width_then_thickness_then_label()
    {
        Sketch sketch = Design.WithParts(
            ("Thin", 10240, 512, Apron),
            ("Thick", 10240, 1024, Apron),
            ("Wide", 10240, 512, Apron with { OutOfPlane = new Length(7168) }));

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, Library);

        // Width first (7168 before 3584), then thickness (1024 before 512), then the label.
        Assert.Equal(["Wide", "Thick", "Thin"], rows.Select(row => row.Label));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void The_same_design_gives_the_same_list_every_time()
    {
        Sketch sketch = Design.WithParts(
            ("Leg, south-west", 2560, 2560, Leg),
            ("Apron, long, south", 40960, 768, Apron),
            ("Apron, long, north", 40960, 768, Apron));

        Assert.Equal(CutList.Of(sketch, Library), CutList.Of(sketch, Library));
    }

    [Theory]
    [Trait("Feature", "CUT-003")]
    // The design doc's two worked examples.
    [InlineData("Leg", "Leg, south-west", "Leg, north-east")]
    [InlineData("Apron, long", "Apron, long, south", "Apron, long, north")]
    // A prefix that stops in the middle of a word is no label at all, so the first member's own
    // name stands in rather than "Leg".
    [InlineData("Legacy rail", "Legacy rail", "Legend rail")]
    // Trailing punctuation and whitespace come off whatever is left.
    [InlineData("Drawer front", "Drawer front — upper", "Drawer front — lower")]
    // Identical names are their own label.
    [InlineData("Slat", "Slat", "Slat")]
    // An unnamed part stays unnamed rather than being given a name by the grouping.
    [InlineData("", "", "")]
    public void A_rows_label_is_what_its_members_have_in_common(string expected, string first, string second)
    {
        Sketch sketch = Design.WithParts((first, 10240, 768, Apron), (second, 10240, 768, Apron));

        Assert.Equal(expected, Assert.Single(CutList.Of(sketch, Library)).Label);
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void A_label_falls_back_to_the_first_member_in_id_order()
    {
        // No common prefix at all. "In id order" is what makes the fallback a property of the
        // design rather than of a dictionary's enumeration order.
        Sketch sketch = Design.WithParts(("Zebra", 10240, 768, Apron), ("Aardvark", 10240, 768, Apron));

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Equal(row.Members[0], Assert.Single(
            sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id).Take(1)).Id);
        Assert.Equal("Zebra", row.Label);
    }

    [Fact]
    public void The_cut_list_rejects_nonsense_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => CutList.Of(null!, Library));
        Assert.Throws<ArgumentNullException>(() => CutList.Of(Sketch.Empty, null!));
    }

    /// <summary>A leg: 2 1/2" square in plan, 16 1/4" long out of it.</summary>
    private static Part Leg => new(
        null, null, 1, new Length(16640), new PlanAxes(PartDimension.Width, PartDimension.Thickness));

    /// <summary>An apron on edge: length across X, thickness up Y, 3 1/2" of face out of plane.</summary>
    private static Part Apron => new(
        null, null, 1, new Length(3584), new PlanAxes(PartDimension.Length, PartDimension.Thickness));
}
