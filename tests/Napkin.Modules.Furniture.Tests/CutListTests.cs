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
            .WithEntity(Box.AsDrawn(
                EntityId.New(),
                LayerId.Default,
                Point2.Inches(0, 0),
                new Length(147456),
                new Length(5632),
                Box.DefaultDepth,
                Angle.Zero) with { Name = "Wall" });

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
            ("Wide", 10240, 512, Apron with { Depth = new Length(7168) }));

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
    [Fact]
    [Trait("Feature", "CUT-003")]
    public void A_duplicate_of_a_shaped_part_is_listed_with_its_source_as_one_row_of_two()
    {
        // docs/design/shaped-parts-model.md §9.1 test 11d, the cut-list half: four duplicates of
        // one gusset have the same blank, the same stock and the same cuts by value, so they group
        // (§2.6). The model half — that the copy equals its source in every field but id and
        // anchor and carries no relationships — is DirectUpdaterCutTests.Case11d over in
        // Napkin.Core.Geometry.Tests; this half is here so that the geometry test project does not
        // have to reference the furniture module for one assertion.
        //
        // The cuts are not in the group key yet: that is §10 step 5's work on CutList, and this
        // test stays true either way, because the copy's cuts are the source's by value.
        Sketch drawn = Design.WithParts(("Gusset", 6144, 6144, Apron));
        Box source = drawn.Entities.Values.OfType<Box>().Single() with
        {
            Cuts = [new CornerCut(BoxCorner.NorthEast, new Length(6144), new Length(6144))],
        };

        Box copy = source with
        {
            Id = EntityId.New(),
            Anchor = source.Anchor + new Vector3(Length.Inches(8), Length.Zero, Length.Zero),
        };

        Sketch both = Assert.IsType<Solved>(
            DirectUpdater.Instance.Apply(drawn.WithEntity(source), new AddEntity(copy))).Sketch;

        CutListRow row = Assert.Single(CutList.Of(both, Library));

        Assert.Equal(2, row.Quantity);
        Assert.Equal(2, row.Members.Length);
        Assert.Equal("Gusset", row.Label);
    }

    [Fact]
    [Trait("Feature", "CUT-002")]
    public void A_shaped_part_is_listed_at_the_size_of_the_blank_it_is_cut_from()
    {
        // Test 12 (shaped-parts-model.md §9.1): the three finished dimensions of a shaped part are
        // the dimensions of the blank you start from. No bounding box is computed and a taper never
        // shrinks the listed size — the CUT-002 rule, one level up.
        Cut[] cuts =
        [
            new CornerCut(BoxCorner.SouthEast, new Length(3072), new Length(5120)),
            new RoundedCorner(BoxCorner.NorthWest, new Length(1024)),
        ];

        Sketch sketch = Design.WithCutParts(("Top", 49152, 24576, Top, cuts));
        Box box = sketch.Entities.Values.OfType<Box>().Single();

        CutListRow row = Assert.Single(CutList.Of(sketch, Library));

        Assert.Equal(49152, row.Length.Units);
        Assert.Equal(24576, row.Width.Units);
        Assert.Equal(768, row.Thickness.Units);

        // And the row carries the cuts themselves, in the site order the box holds them in, so the
        // sentence is derived from the same data the canvas draws from.
        Assert.Equal(box.Cuts, row.Cuts);
        Assert.Equal([.. cuts], row.Cuts);
        Assert.Equal(2, row.CutText.Length);
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void The_cuts_are_part_of_what_makes_two_parts_one_row()
    {
        // Test 13, first half: four legs chamfered on the same corner are one piece, made four
        // times.
        CornerCut chamfer = new(BoxCorner.NorthEast, new Length(256), new Length(256));
        Sketch four = Design.WithCutParts(
            ("Leg, south-west", 2560, 2560, Leg, [chamfer]),
            ("Leg, south-east", 2560, 2560, Leg, [chamfer]),
            ("Leg, north-west", 2560, 2560, Leg, [chamfer]),
            ("Leg, north-east", 2560, 2560, Leg, [chamfer]));

        CutListRow row = Assert.Single(CutList.Of(four, Library));

        Assert.Equal("Leg", row.Label);
        Assert.Equal(4, row.Quantity);
        Assert.Equal(4, row.Members.Length);
        Assert.Equal(chamfer, Assert.Single(row.Cuts));

        // A leg is drawn as its footprint, so the chamfer runs the whole length of it and the one
        // sentence says so.
        Assert.Equal(
            "Cut off the north-east corner: mark 1/4\" along each edge from the corner, and cut "
            + "between the marks, for the full 1'-4 1/4\" length.",
            Assert.Single(row.CutText));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Chamfers_of_opposite_hand_are_two_rows_that_each_say_which_corner()
    {
        // Test 13, second half. A magazine would write "make two of each hand"; two honest rows
        // that each name their corner are better than one row that hides it (§4.3, §6).
        Sketch mirrored = Design.WithCutParts(
            ("Leg, south-west", 2560, 2560, Leg,
             [new CornerCut(BoxCorner.NorthEast, new Length(256), new Length(256))]),
            ("Leg, south-east", 2560, 2560, Leg,
             [new CornerCut(BoxCorner.NorthWest, new Length(256), new Length(256))]));

        ImmutableArray<CutListRow> rows = CutList.Of(mirrored, Library);

        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal(1, row.Quantity));

        // Each row names its own corner. Which of the two comes first is decided by the label
        // tie-break, which runs before the cuts do, so this does not assert an order.
        string[] sentences = [.. rows.Select(row => Assert.Single(row.CutText))];

        Assert.Contains(sentences, sentence => sentence.Contains("north-east", StringComparison.Ordinal));
        Assert.Contains(sentences, sentence => sentence.Contains("north-west", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void A_rectangle_and_the_same_rectangle_rounded_are_two_rows()
    {
        // Test 13, third part: they are not the same piece. Both are called "Shelf", so the label
        // and the material tie-breaks say nothing and the cut sequence is what decides — an empty
        // list before any cuts at all, whichever way round the boxes were drawn.
        RoundedCorner rounded = new(BoxCorner.NorthEast, new Length(128));

        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithCutParts(
                ("Shelf", 10240, 768, Apron, [rounded]),
                ("Shelf", 10240, 768, Apron, [])),
            Library);

        Assert.Equal(2, rows.Length);
        Assert.All(rows, row => Assert.Equal("Shelf", row.Label));
        Assert.Empty(rows[0].Cuts);
        Assert.Empty(rows[0].CutText);
        Assert.Equal(rounded, Assert.Single(rows[1].Cuts));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Two_rows_that_differ_only_in_their_cuts_come_out_the_same_way_round_every_time()
    {
        // The tie-break is the cut sequence itself: site first, then kind, then the values. Built
        // in one order and then the other, the list is the same both times.
        RoundedCorner south = new(BoxCorner.SouthWest, new Length(128));
        RoundedCorner north = new(BoxCorner.NorthEast, new Length(128));

        ImmutableArray<CutListRow> oneWay = CutList.Of(
            Design.WithCutParts(
                ("Shelf", 10240, 768, Apron, [north]),
                ("Shelf", 10240, 768, Apron, [south])),
            Library);

        ImmutableArray<CutListRow> theOther = CutList.Of(
            Design.WithCutParts(
                ("Shelf", 10240, 768, Apron, [south]),
                ("Shelf", 10240, 768, Apron, [north])),
            Library);

        Assert.Equal<Cut>(
            [south, north],
            oneWay.Select(row => Assert.Single(row.Cuts)));
        Assert.Equal(
            oneWay.Select(row => row.Cuts),
            theOther.Select(row => row.Cuts));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void Rows_that_differ_only_in_their_cuts_sort_by_site_then_kind_then_value()
    {
        // Every rung of the last tie-break, in one design: nine shelves of identical size and name
        // whose only difference is what has been cut off them. Drawn deliberately out of order, so
        // that the order that comes back is the comparison's and not the design's.
        Cut[][] cuts =
        [
            [new CurvedEdge(BoxEdge.North, Bow.Inward, new Length(256))],
            [new RoundedCorner(BoxCorner.SouthWest, new Length(512))],
            [new CornerCut(BoxCorner.SouthWest, new Length(512), new Length(512))],
            [],
            [new CurvedEdge(BoxEdge.North, Bow.Inward, new Length(128))],
            [new CornerCut(BoxCorner.NorthEast, new Length(256), new Length(256))],
            [new CornerCut(BoxCorner.SouthWest, new Length(256), new Length(256))],
            [new CurvedEdge(BoxEdge.North, Bow.Outward, new Length(128))],
            [new CornerCut(BoxCorner.SouthWest, new Length(512), new Length(256))],
            [new RoundedCorner(BoxCorner.SouthWest, new Length(256))],
        ];

        ImmutableArray<CutListRow> rows = CutList.Of(
            Design.WithCutParts([.. cuts.Select(shelf => ("Shelf", 10240L, 768L, Apron, shelf))]),
            Library);

        Assert.Equal(cuts.Length, rows.Length);

        // One blank whose cuts start with another's: everything they share is the same, so the
        // shorter list is the plainer blank and comes first.
        ImmutableArray<CutListRow> nested = CutList.Of(
            Design.WithCutParts(
                ("Shelf", 10240, 768, Apron,
                 [
                     new RoundedCorner(BoxCorner.SouthWest, new Length(256)),
                     new RoundedCorner(BoxCorner.NorthEast, new Length(256)),
                 ]),
                ("Shelf", 10240, 768, Apron, [new RoundedCorner(BoxCorner.SouthWest, new Length(256))])),
            Library);

        Assert.Equal([1, 2], nested.Select(row => row.Cuts.Length));

        Assert.Equal<IEnumerable<Cut>>(
            [
                // Nothing cut at all comes first: everything they share is the same, and the
                // shorter list is the plainer blank.
                [],

                // Then by site — south-west corner, north-east corner, north edge — and within a
                // site by kind, a straight cut before a rounding.
                [new CornerCut(BoxCorner.SouthWest, new Length(256), new Length(256))],
                [new CornerCut(BoxCorner.SouthWest, new Length(512), new Length(256))],
                [new CornerCut(BoxCorner.SouthWest, new Length(512), new Length(512))],
                [new RoundedCorner(BoxCorner.SouthWest, new Length(256))],
                [new RoundedCorner(BoxCorner.SouthWest, new Length(512))],
                [new CornerCut(BoxCorner.NorthEast, new Length(256), new Length(256))],
                [new CurvedEdge(BoxEdge.North, Bow.Outward, new Length(128))],
                [new CurvedEdge(BoxEdge.North, Bow.Inward, new Length(128))],
                [new CurvedEdge(BoxEdge.North, Bow.Inward, new Length(256))],
            ],
            rows.Select(row => row.Cuts.AsEnumerable()));
    }

    [Fact]
    [Trait("Feature", "CUT-003")]
    public void What_a_shopping_list_reads_is_untouched_by_the_cuts()
    {
        // Test 16 (§4.6). The shopping list consumes rows and reads the three blank dimensions and
        // the stock; a shaped part buys exactly the board its blank needs. There is no shopping
        // list in this build yet (#9, CUT-005), so this asserts the property it will rest on: the
        // same design with and without cuts gives rows that are identical in everything but the
        // cuts and the sentences derived from them.
        (string Name, long Width, long Height, Piece Part)[] parts =
        [
            ("Top", 49152, 24576, Top with { Stock = "1x6" }),
            ("Leg, south-west", 2560, 2560, Leg),
            ("Leg, south-east", 2560, 2560, Leg),
        ];

        Cut[] rounded = [new RoundedCorner(BoxCorner.NorthEast, new Length(1024))];

        ImmutableArray<CutListRow> plain = CutList.Of(
            Design.WithParts(parts),
            Library);

        ImmutableArray<CutListRow> shaped = CutList.Of(
            Design.WithCutParts(
                (parts[0].Name, parts[0].Width, parts[0].Height, parts[0].Part, rounded),
                (parts[1].Name, parts[1].Width, parts[1].Height, parts[1].Part, []),
                (parts[2].Name, parts[2].Width, parts[2].Height, parts[2].Part, [])),
            Library);

        Assert.Equal(plain.Length, shaped.Length);
        for (int i = 0; i < plain.Length; i++)
        {
            Assert.Equal(plain[i], shaped[i] with { Cuts = plain[i].Cuts });

            // Said one at a time, because these are the fields a shopping list adds up.
            Assert.Equal(plain[i].Length, shaped[i].Length);
            Assert.Equal(plain[i].Width, shaped[i].Width);
            Assert.Equal(plain[i].Thickness, shaped[i].Thickness);
            Assert.Equal(plain[i].Quantity, shaped[i].Quantity);
            Assert.Equal(plain[i].Material, shaped[i].Material);
            Assert.Equal(plain[i].Stock, shaped[i].Stock);
        }

        // The one difference is the one this step added.
        Assert.Empty(plain[0].CutText);
        Assert.Equal(["Round the north-east corner to a 1\" radius."], shaped[0].CutText);
    }

    /// <summary>A top lying flat: length across X, width up Y, 3/4" of thickness out of plane.</summary>
    private static Piece Top => new(
        null, null, 1, new Length(768), new PlanAxes(PartDimension.Length, PartDimension.Width));

    private static Piece Leg => new(
        null, null, 1, new Length(16640), new PlanAxes(PartDimension.Width, PartDimension.Thickness));

    /// <summary>An apron on edge: length across X, thickness up Y, 3 1/2" of face out of plane.</summary>
    private static Piece Apron => new(
        null, null, 1, new Length(3584), new PlanAxes(PartDimension.Length, PartDimension.Thickness));
}
