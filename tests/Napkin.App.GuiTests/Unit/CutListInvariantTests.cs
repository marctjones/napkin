using System.Collections.Immutable;
using BigInteger = System.Numerics.BigInteger;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What editing must and must not do to the cut list (#96).
/// </summary>
/// <remarks>
/// A cut list is trustworthy only if the operations that should not change what is cut do not,
/// and those that should change it do so by exactly the right amount. They live here, not with the
/// cut list's own tests, because the operations are the editor's (<see cref="DesignEditor"/>,
/// <see cref="SelectionCommands"/>, <see cref="SelectionTurn"/>) and the Furniture tests know nothing
/// of an editor. Every case runs on the coffee table's real parts. Randomised runs use fixed seeds.
/// </remarks>
public class CutListInvariantTests
{
    static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    static Design Table() => SampleExpectations.Sample("coffee-table").Load();

    /// <summary>The coffee table's boxes with no relationships or dimensions: nothing to refuse a move.</summary>
    static Design Loose()
    {
        Design table = Table();
        Sketch sketch = Sketch.Empty;
        foreach (Box box in table.Sketch.Entities.Values.OfType<Box>().OrderBy(b => b.Id))
        {
            sketch = sketch.WithEntity(box);
        }

        return table with { Sketch = sketch };
    }

    static DesignEditor Open(Design design)
    {
        DesignEditor editor = new();
        editor.Open(design);
        return editor;
    }

    static ImmutableArray<CutListRow> Rows(DesignEditor editor) => CutList.Of(editor.Sketch, Library);

    static bool Same(ImmutableArray<CutListRow> a, ImmutableArray<CutListRow> b) => a.SequenceEqual(b);

    static Box Named(DesignEditor editor, string name) =>
        editor.Sketch.Entities.Values.OfType<Box>().Single(box => box.Name == name);

    /// <summary>Total volume of everything the list says to cut, in cubic units, from the rows alone.</summary>
    static BigInteger VolumeOfRows(IEnumerable<CutListRow> rows) => rows.Aggregate(
        BigInteger.Zero,
        (sum, row) => sum + (row.Quantity * (BigInteger)row.Length.Units * row.Width.Units * row.Thickness.Units));

    /// <summary>
    /// The same volume straight from the scene: each part's extents in the world, multiplied. It
    /// does not read the cut list's length/width/thickness naming at all, so it can disagree with it.
    /// </summary>
    static BigInteger VolumeOfScene(Sketch sketch) => sketch.Entities.Values.OfType<Box>()
        .Where(box => box.Part is not null)
        .Aggregate(BigInteger.Zero, (sum, box) =>
        {
            List<Point3> corners = [.. Enum.GetValues<BoxCorner>().SelectMany(c => new[] { box.Vertex(c, BoxLevel.Bottom), box.Vertex(c, BoxLevel.Top) })];
            BigInteger x = corners.Max(c => c.X.Units) - corners.Min(c => c.X.Units);
            BigInteger y = corners.Max(c => c.Y.Units) - corners.Min(c => c.Y.Units);
            BigInteger z = corners.Max(c => c.Z.Units) - corners.Min(c => c.Z.Units);
            return sum + (box.Part!.Quantity * x * y * z);
        });

    [Fact]
    public void Moving_a_part_never_changes_a_row()
    {
        DesignEditor editor = Open(Loose());
        ImmutableArray<CutListRow> before = Rows(editor);

        foreach (Box box in editor.Sketch.Entities.Values.OfType<Box>().ToList())
        {
            Point3 anchor = Named(editor, box.Name).Anchor;
            UpdateResult result = editor.Apply(new SetPosition(box.Id, anchor + new Vector3(Length.Inches(7, 3, 8), Length.Inches(-2), Length.Inches(1, 1, 16))), "Move");
            Assert.IsAssignableFrom<Succeeded>(result);
        }

        Assert.True(Same(before, Rows(editor)));
    }

    [Theory]
    [InlineData(Axis.X)]
    [InlineData(Axis.Y)]
    [InlineData(Axis.Z)]
    public void Turning_a_part_never_changes_a_row(Axis axis)
    {
        DesignEditor editor = Open(Loose());
        ImmutableArray<CutListRow> before = Rows(editor);

        foreach (Box box in editor.Sketch.Entities.Values.OfType<Box>().OrderBy(b => b.Id).ToList())
        {
            editor.Select(box.Id);
            Assert.IsAssignableFrom<Succeeded>(SelectionTurn.Turn(editor, axis, 1));
        }

        Assert.True(Same(before, Rows(editor)), $"a quarter turn about {axis} of every part changed the cut list.");
    }

    [Fact]
    public void Duplicating_a_leg_adds_one_to_its_row_and_touches_no_other_row()
    {
        DesignEditor editor = Open(Table());
        ImmutableArray<CutListRow> before = Rows(editor);
        CutListRow legs = Assert.Single(before, row => row.Label == "Leg");

        editor.Select(Named(editor, "Leg, south-west").Id);
        Assert.NotNull(SelectionCommands.Duplicate(editor, gridStepInches: 1));

        ImmutableArray<CutListRow> after = Rows(editor);
        CutListRow legsAfter = Assert.Single(after, row => row.Label == "Leg");
        Assert.Equal(legs.Quantity + 1, legsAfter.Quantity);
        Assert.Equal(legs.Members.Length + 1, legsAfter.Members.Length);
        Assert.Equal((legs.Length, legs.Width, legs.Thickness), (legsAfter.Length, legsAfter.Width, legsAfter.Thickness));

        // Every other row is exactly as it was, in the same place in the order.
        Assert.True(Same([.. before.Where(r => r.Label != "Leg")], [.. after.Where(r => r.Label != "Leg")]));
        Assert.Equal(before.Length, after.Length);
    }

    [Fact]
    public void Mirroring_a_leg_adds_one_piece_of_the_same_size()
    {
        DesignEditor editor = Open(Table());
        CutListRow legs = Assert.Single(Rows(editor), row => row.Label == "Leg");

        editor.Select(Named(editor, "Leg, south-west").Id);
        Assert.NotNull(SelectionCommands.Mirror(editor, Axis.X));

        CutListRow after = Assert.Single(Rows(editor), row => row.Label == "Leg");
        Assert.Equal(legs.Quantity + 1, after.Quantity);
        Assert.Equal((legs.Length, legs.Width, legs.Thickness), (after.Length, after.Width, after.Thickness));
    }

    [Fact]
    public void A_part_that_stands_for_several_pieces_counts_all_of_them_in_its_row_and_in_the_volume()
    {
        // Every part in the sample is quantity 1, where counting parts and summing quantities look
        // the same. One box standing for three pieces is where they differ.
        DesignEditor editor = Open(Table());
        Box leg = Named(editor, "Leg, south-west");
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new SetPart(leg.Id, leg.Part! with { Quantity = 3 }), "Three pieces"));

        CutListRow legs = Assert.Single(Rows(editor), row => row.Label == "Leg");

        // Three from the one box, one each from the other three legs.
        Assert.Equal(6, legs.Quantity);
        Assert.Equal(4, legs.Members.Length);
        Assert.Equal(VolumeOfScene(editor.Sketch), VolumeOfRows(Rows(editor)));
    }

    [Fact]
    public void Two_parts_share_a_row_only_when_every_size_is_equal_and_one_unit_of_difference_splits_them()
    {
        Design table = Table();
        Box leg = table.Sketch.Entities.Values.OfType<Box>().First(b => b.Name == "Leg, south-west");
        Box nearlyLeg = leg with { Id = new EntityId(Guid.Parse("20000000-0000-4000-8000-000000000001")), Name = "Leg, spare", Depth = leg.Depth + new Length(1) };

        ImmutableArray<CutListRow> rows = CutList.Of(table.Sketch.WithEntity(nearlyLeg), Library);

        // 1/1024" is the grid, and no tolerance is allowed to merge it away.
        Assert.Equal(2, rows.Count(row => row.Label.StartsWith("Leg", StringComparison.Ordinal)));
        Assert.Equal(1, rows.Single(row => row.Members.Contains(nearlyLeg.Id)).Quantity);
        Assert.Equal(4, rows.Single(row => row.Members.Contains(leg.Id)).Quantity);
    }

    [Fact]
    public void Deleting_a_part_and_undoing_it_restores_the_list_row_for_row_in_the_same_order()
    {
        DesignEditor editor = Open(Table());
        ImmutableArray<CutListRow> before = Rows(editor);

        foreach (Box box in editor.Sketch.Entities.Values.OfType<Box>().OrderBy(b => b.Id).ToList())
        {
            editor.Select(box.Id);
            SelectionCommands.Delete(editor);
            Assert.False(Same(before, Rows(editor)), $"deleting {box.Name} left the list alone.");

            Assert.True(editor.Undo());
            Assert.True(Same(before, Rows(editor)), $"undoing the delete of {box.Name} did not restore the list.");
        }
    }

    [Fact]
    public void The_list_does_not_depend_on_the_order_the_parts_were_added()
    {
        Design table = Table();
        Box[] boxes = [.. table.Sketch.Entities.Values.OfType<Box>().OrderBy(b => b.Id)];
        ImmutableArray<CutListRow> expected = CutList.Of(table.Sketch, Library);

        foreach (int seed in new[] { 1, 2, 3 })
        {
            Random random = new(seed);
            Sketch shuffled = Sketch.Empty;
            foreach (Box box in boxes.OrderBy(_ => random.Next()))
            {
                shuffled = shuffled.WithEntity(box);
            }

            Assert.True(Same(expected, CutList.Of(shuffled, Library)), $"seed {seed}: insertion order changed the list.");
        }
    }

    [Fact]
    public void The_volume_the_rows_add_up_to_is_the_volume_of_the_parts_in_the_scene()
    {
        // The rows' arithmetic (quantity x length x width x thickness) and the scene's (world extents
        // of each box) are two independent routes to the same board volume.
        Design table = Table();

        Assert.Equal(VolumeOfScene(table.Sketch), VolumeOfRows(CutList.Of(table.Sketch, Library)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void After_any_run_of_edits_the_volume_still_matches_the_scene_and_undoing_them_all_restores_the_list(int seed)
    {
        Random random = new(seed);
        DesignEditor editor = Open(Table());
        ImmutableArray<CutListRow> original = Rows(editor);
        BigInteger originalVolume = VolumeOfRows(original);
        int copies = 0;

        for (int step = 0; step < 30; step++)
        {
            Box[] boxes = [.. editor.Sketch.Entities.Values.OfType<Box>().Where(b => b.Part is not null).OrderBy(b => b.Id)];
            Box pick = boxes[random.Next(boxes.Length)];
            editor.Select(pick.Id);

            switch (random.Next(4))
            {
                case 0:
                    copies += SelectionCommands.Duplicate(editor, gridStepInches: 1) is null ? 0 : 1;
                    break;
                case 1:
                    copies += SelectionCommands.Mirror(editor, random.Next(2) == 0 ? Axis.X : Axis.Y) is null ? 0 : 1;
                    break;
                case 2:
                    SelectionTurn.Turn(editor, (Axis)random.Next(3), random.Next(1, 4));
                    break;
                default:
                    editor.Apply(new SetPosition(pick.Id, pick.Anchor + new Vector3(Length.Inches(random.Next(-6, 7)), Length.Inches(random.Next(-6, 7)), Length.Zero)), "Move");
                    break;
            }

            Assert.Equal(VolumeOfScene(editor.Sketch), VolumeOfRows(Rows(editor)));
        }

        // The run must really have edited: refused copies would make every check above vacuous.
        Assert.True(copies >= 3, $"seed {seed}: only {copies} copies were made.");
        Assert.True(VolumeOfRows(Rows(editor)) > originalVolume, "duplicating and mirroring only ever add pieces.");

        while (editor.Undo())
        {
        }

        Assert.True(Same(original, Rows(editor)), $"seed {seed}: undoing every edit did not restore the cut list.");
    }
}
