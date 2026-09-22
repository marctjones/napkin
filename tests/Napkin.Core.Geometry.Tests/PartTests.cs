namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The part model: two finished dimensions in the plan and one out of it, named by
/// <see cref="PlanAxes"/> (docs/design/parts-and-cut-list.md §1.1).
/// </summary>
public class PartTests
{
    /// <summary>Units of 1/1024 inch, the way the file and the fixtures state a length.</summary>
    private static Box Box(long widthUnits, long heightUnits, int quarterTurns = 0) => new(
        EntityId.New(),
        LayerId.Default,
        Point2.Inches(10, 20),
        new Length(widthUnits),
        new Length(heightUnits),
        Angle.Zero.Rotate90(quarterTurns));

    [Trait("Feature", "CUT-002")]
    [Theory]
    // The four kinds of part in the coffee-table fixture, each on its own box. 1024 units is an
    // inch, so 49152 is 48", 2560 is 2 1/2", 16640 is 16 1/4", 3584 is 3 1/2" and 768 is 3/4".
    [InlineData(49152, 24576, PartDimension.Length, PartDimension.Width, 768, 49152, 24576, 768)]
    [InlineData(2560, 2560, PartDimension.Width, PartDimension.Thickness, 16640, 16640, 2560, 2560)]
    [InlineData(40960, 768, PartDimension.Length, PartDimension.Thickness, 3584, 40960, 3584, 768)]
    [InlineData(768, 16384, PartDimension.Thickness, PartDimension.Length, 3584, 16384, 3584, 768)]
    public void A_parts_three_dimensions_come_from_the_box_and_the_one_number_it_stores(
        long boxWidth,
        long boxHeight,
        PartDimension x,
        PartDimension y,
        long outOfPlane,
        long length,
        long width,
        long thickness)
    {
        Part part = new(null, null, 1, new Length(outOfPlane), new PlanAxes(x, y));

        FinishedSize size = part.SizeOn(Box(boxWidth, boxHeight));

        Assert.Equal(new Length(length), size.Length);
        Assert.Equal(new Length(width), size.Width);
        Assert.Equal(new Length(thickness), size.Thickness);
    }

    [Trait("Feature", "CUT-002")]
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void A_rotated_part_measures_what_was_typed(int quarterTurns)
    {
        // The whole point of reading the box's stored parameters rather than the distance between
        // its derived corners: turning a part does not change what it is.
        Part part = new(null, null, 1, Length.Inches(3), new PlanAxes(PartDimension.Length, PartDimension.Width));

        FinishedSize size = part.SizeOn(Box(10240, 4096, quarterTurns));

        Assert.Equal(Length.Inches(10), size.Length);
        Assert.Equal(Length.Inches(4), size.Width);
        Assert.Equal(Length.Inches(3), size.Thickness);
    }

    [Trait("Feature", "CUT-001")]
    [Theory]
    [InlineData(PartDimension.Length, PartDimension.Width, PartDimension.Thickness)]
    [InlineData(PartDimension.Width, PartDimension.Length, PartDimension.Thickness)]
    [InlineData(PartDimension.Length, PartDimension.Thickness, PartDimension.Width)]
    [InlineData(PartDimension.Thickness, PartDimension.Length, PartDimension.Width)]
    [InlineData(PartDimension.Width, PartDimension.Thickness, PartDimension.Length)]
    [InlineData(PartDimension.Thickness, PartDimension.Width, PartDimension.Length)]
    public void The_out_of_plane_dimension_is_the_one_neither_axis_claims(
        PartDimension x, PartDimension y, PartDimension expected)
        => Assert.Equal(expected, new PlanAxes(x, y).OutOfPlane);

    [Trait("Feature", "CUT-001")]
    [Theory]
    [InlineData(PartDimension.Length)]
    [InlineData(PartDimension.Width)]
    [InlineData(PartDimension.Thickness)]
    public void Two_plan_axes_cannot_name_one_dimension(PartDimension both)
    {
        // There would be no third name for the out-of-plane value to carry, so the part would have
        // two dimensions and a spare number.
        ArgumentException thrown = Assert.Throws<ArgumentException>(() => new PlanAxes(both, both));

        Assert.Contains(both.ToString(), thrown.Message, StringComparison.Ordinal);
    }

    [Trait("Feature", "CUT-003")]
    [Fact]
    public void A_part_stands_for_at_least_one_piece_of_a_positive_thickness()
    {
        PlanAxes axes = new(PartDimension.Length, PartDimension.Width);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Part(null, null, 0, Length.Inches(1), axes));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Part(null, null, -1, Length.Inches(1), axes));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Part(null, null, 1, Length.Zero, axes));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Part(null, null, 1, Length.Inches(-1), axes));

        // Four legs drawn once, and the thinnest thing a tape shows.
        Assert.Equal(4, new Part(null, null, 4, new Length(1), axes).Quantity);
    }

    [Trait("Feature", "CUT-001")]
    [Fact]
    public void A_part_is_a_value_and_carries_what_it_was_given()
    {
        PlanAxes axes = new(PartDimension.Width, PartDimension.Thickness);
        Part part = new("2x4", "Douglas fir", 4, Length.Inches(16), axes);

        Assert.Equal("2x4", part.Stock);
        Assert.Equal("Douglas fir", part.Species);
        Assert.Equal(axes, part.PlanAxes);
        Assert.Equal(part, part with { });
        Assert.NotEqual(part, part with { Stock = "2x6" });

        // A stock nobody stocks is a legal part: the library resolves names, the model does not.
        Assert.Equal("9x17 unobtainium", (part with { Stock = "9x17 unobtainium" }).Stock);
        Assert.Null((part with { Stock = null }).Stock);

        Assert.Throws<ArgumentNullException>(() => part.SizeOn(null!));
    }

    [Trait("Feature", "CUT-002")]
    [Fact]
    public void Editing_a_part_keeps_it_a_part()
    {
        // Every edit rebuilds an entity with a `with` expression rather than a constructor call,
        // which is what makes the name and the part survive a drag, a resize and a turn without
        // anything having to remember to carry them. If this fails, a cut list would empty out
        // the first time somebody moved a leg.
        SketchBuilder builder = new();
        EntityId id = builder.AddBox(0, 0, 10, 4);
        Part part = new("2x4", "Douglas fir", 4, Length.Inches(16), new PlanAxes(PartDimension.Length, PartDimension.Width));

        Sketch sketch = builder.Sketch.WithEntity(
            ((Box)builder.Sketch.Find(id)!) with { Name = "Leg, south-west", Part = part });

        DirectUpdater updater = new();
        foreach (Request request in new Request[]
        {
            new Drag(id, new Vector2(Length.Inches(3), Length.Inches(2))),
            new DragEdge(id, BoxEdge.East, Length.Inches(2)),
            new SetRotation(id, Angle.Zero.Rotate90(1)),
            new SetPosition(id, Point2.Inches(20, 30)),
            new SetLayer(id, LayerId.Default),
        })
        {
            Solved solved = Assert.IsType<Solved>(updater.Apply(sketch, request));
            Box edited = Assert.IsType<Box>(solved.Sketch.Find(id));

            Assert.Equal("Leg, south-west", edited.Name);
            Assert.Equal(part, edited.Part);

            sketch = solved.Sketch;
        }

        // The box really did change under all that: this is not a test of a no-op.
        Box final = Assert.IsType<Box>(sketch.Find(id));
        Assert.Equal(Point2.Inches(20, 30), final.Anchor);
        Assert.Equal(Length.Inches(12), final.Width);
    }

    [Trait("Feature", "CUT-001")]
    [Fact]
    public void A_box_with_no_part_is_a_box_as_it_always_was()
    {
        Box plain = Box(10240, 4096);

        Assert.Null(plain.Part);
        Assert.Empty(plain.Name);

        // Naming a box or making it a part changes its value, and nothing else about it.
        Box named = plain with { Name = "Shelf" };
        Assert.NotEqual(plain, named);
        Assert.Equal(plain.Width, named.Width);
        Assert.Equal(plain.Corner(BoxCorner.NorthEast), named.Corner(BoxCorner.NorthEast));

        // A name survives a move to another layer, because the layer is all that OnLayer changes.
        Entity moved = named.OnLayer(new LayerId(Guid.NewGuid()));
        Assert.Equal("Shelf", moved.Name);
    }
}
