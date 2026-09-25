namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The part model: a box's three sizes named by <see cref="PlanAxes"/>
/// (docs/design/parts-and-cut-list.md §1.1; the third is the box's depth since
/// docs/design/assembly-model.md §1.2).
/// </summary>
public class PartTests
{
    /// <summary>Units of 1/1024 inch, the way the file and the fixtures state a length.</summary>
    private static Box Box(long widthUnits, long heightUnits, int quarterTurns = 0, long depthUnits = 768)
        => Napkin.Core.Geometry.Box.AsDrawn(
            EntityId.New(),
            LayerId.Default,
            Point2.Inches(10, 20),
            new Length(widthUnits),
            new Length(heightUnits),
            new Length(depthUnits),
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
        long depth,
        long length,
        long width,
        long thickness)
    {
        // §9.1 test 19: SizeOn reads the box's Depth for the name neither plan axis claims.
        Part part = new(null, null, 1, new PlanAxes(x, y));

        FinishedSize size = part.SizeOn(Box(boxWidth, boxHeight, depthUnits: depth));

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
        Part part = new(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));

        FinishedSize size = part.SizeOn(Box(10240, 4096, quarterTurns, depthUnits: 3072));

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

        Assert.Throws<ArgumentOutOfRangeException>(() => new Part(null, null, 0, axes));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Part(null, null, -1, axes));

        // Four legs drawn once.
        Assert.Equal(4, new Part(null, null, 4, axes).Quantity);

        // The thickness is the box's depth now, and it is the box that must have one
        // (assembly-model §1.6 invariant 10): the thinnest thing a tape shows is fine, nothing is not.
        Box thin = Box(10240, 4096, depthUnits: 1);
        Assert.True(Sketch.Empty.WithEntity(thin).Validate().IsValid);
        ValidationResult flat = Sketch.Empty.WithEntity(thin with { Depth = Length.Zero }).Validate();
        Assert.Equal(ValidationErrorKind.NonPositiveSize, Assert.Single(flat.Errors).Kind);
        Assert.Equal(
            RejectionReason.NonPositiveSize,
            Assert.IsType<Rejected>(new DirectUpdater().Apply(Sketch.Empty, new AddEntity(thin with { Depth = Length.Inches(-1) }))).Reason);
    }

    [Trait("Feature", "CUT-001")]
    [Fact]
    public void A_part_is_a_value_and_carries_what_it_was_given()
    {
        PlanAxes axes = new(PartDimension.Width, PartDimension.Thickness);
        Part part = new("2x4", "Douglas fir", 4, axes);

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
        Part part = new("2x4", "Douglas fir", 4, new PlanAxes(PartDimension.Length, PartDimension.Width));

        Sketch sketch = builder.Sketch.WithEntity(
            ((Box)builder.Sketch.Find(id)!) with { Name = "Leg, south-west", Part = part });

        DirectUpdater updater = new();
        foreach (Request request in new Request[]
        {
            Drag.InPlan(id, new Vector2(Length.Inches(3), Length.Inches(2))),
            new DragFace(id, BoxFace.East, Length.Inches(2)),
            new SetOrientation(id, BoxFace.Top, Angle.Zero.Rotate90(1)),
            SetPosition.InPlan(id, Point2.Inches(20, 30)),
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
        Assert.Equal(Point3.Inches(20, 30, 0), final.Anchor);
        Assert.Equal(Length.Inches(12), final.Width);
    }

    [Trait("Feature", "CUT-001")]
    [Fact]
    public void A_box_becomes_a_part_and_stops_being_one_through_the_updater()
    {
        // Making a box a piece somebody cuts is the one thing that turns a drawing into a cut
        // list, and it changes no dimension: the two in-plan dimensions are the box's own.
        SketchBuilder builder = new();
        EntityId id = builder.AddBox(0, 0, 10, 4);
        DirectUpdater updater = new();
        Part part = new("2x4", null, 2, new PlanAxes(PartDimension.Length, PartDimension.Width));

        Solved named = Assert.IsType<Solved>(updater.Apply(builder.Sketch, new SetName(id, "Rail, front")));
        Assert.Equal("Rail, front", named.Sketch.Find(id)!.Name);
        Assert.Equal([id], named.Changes.Modified);

        Solved made = Assert.IsType<Solved>(updater.Apply(named.Sketch, new SetPart(id, part)));
        Box box = Assert.IsType<Box>(made.Sketch.Find(id));
        Assert.Equal(part, box.Part);
        Assert.Equal("Rail, front", box.Name);
        Assert.Equal(Length.Inches(10), box.Width);
        Assert.Equal(Length.Inches(4), box.Height);

        // And back to a plain box, which is what an opening drawn with the same tool is.
        Solved plain = Assert.IsType<Solved>(updater.Apply(made.Sketch, new SetPart(id, null)));
        Assert.Null(Assert.IsType<Box>(plain.Sketch.Find(id)).Part);

        // An empty name is a legal "unnamed", not a refusal.
        Solved unnamed = Assert.IsType<Solved>(updater.Apply(plain.Sketch, new SetName(id, string.Empty)));
        Assert.Empty(unnamed.Sketch.Find(id)!.Name);
    }

    [Trait("Feature", "CUT-001")]
    [Fact]
    public void Only_a_box_that_is_there_can_be_named_or_made_a_part()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        EntityId node = builder.AddNode(0, 0);
        EntityId missing = EntityId.New();
        DirectUpdater updater = new();
        Part part = new(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));

        Assert.Equal(
            RejectionReason.UnknownEntity,
            Assert.IsType<Rejected>(updater.Apply(builder.Sketch, new SetName(missing, "Nothing"))).Reason);
        Assert.Equal(
            RejectionReason.UnknownEntity,
            Assert.IsType<Rejected>(updater.Apply(builder.Sketch, new SetPart(missing, part))).Reason);

        // A node has no size and a dimension is an annotation, so neither is a piece to cut.
        Assert.Equal(
            RejectionReason.DanglingReference,
            Assert.IsType<Rejected>(updater.Apply(builder.Sketch, new SetPart(node, part))).Reason);

        // A node can still be named — a name is on every entity, not only on a box.
        Assert.Equal(
            "West end",
            Assert.IsType<Solved>(updater.Apply(builder.Sketch, new SetName(node, "West end"))).Sketch.Find(node)!.Name);

        Assert.IsType<Solved>(updater.Apply(builder.Sketch, new SetPart(box, part)));
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

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void Typed_fastener_sizes_and_supplies_replace_the_projects_lists_and_move_nothing()
    {
        SketchBuilder builder = new();
        EntityId id = builder.AddBox(0, 0, 10, 4);
        DirectUpdater updater = new();

        FastenerChoice choice = new(FastenerKind.Brad, new Length(512), "18 ga x 1", 1000);
        Solved sized = Assert.IsType<Solved>(updater.Apply(builder.Sketch, new SetFastenerChoices([choice])));
        Solved supplied = Assert.IsType<Solved>(updater.Apply(sized.Sketch, new SetSupplies([new SupplyLine("Finish", "one quart")])));

        Assert.Equal([choice], supplied.Sketch.FastenerChoices);
        Assert.Equal([new SupplyLine("Finish", "one quart")], supplied.Sketch.Supplies);
        Assert.Equal(builder.Sketch.Find(id), supplied.Sketch.Find(id));
        Assert.Empty(supplied.Changes.Modified);

        // A second request replaces the first list rather than adding to it.
        Solved cleared = Assert.IsType<Solved>(updater.Apply(supplied.Sketch, new SetFastenerChoices([])));
        Assert.Empty(cleared.Sketch.FastenerChoices);
        Assert.Single(cleared.Sketch.Supplies);
    }
}
