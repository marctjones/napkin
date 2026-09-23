using System.Globalization;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Builds small sketches for the golden cases, with ids that are sequential and therefore
/// predictable in <see cref="RelationshipId"/> order — which is the order the propagator iterates
/// relationships in, so a golden case's outcome is reproducible rather than accidental.
/// </summary>
internal sealed class SketchBuilder
{
    // Ids start at 1 so that no id is Guid.Empty, which nothing real should ever equal.
    private int _nextEntity = 1;
    private int _nextRelationship = 1;

    /// <summary>The sketch built so far.</summary>
    public Sketch Sketch { get; private set; } = Sketch.Empty;

    /// <summary>The entity id this builder hands out at a given position, without handing it out.</summary>
    public static EntityId EntityIdAt(int index) => new(Id("0000", index));

    /// <summary>
    /// A relationship id at a given position in the same order this builder hands them out, for a
    /// relationship a test adds through the updater rather than through the builder.
    /// </summary>
    public static RelationshipId RelationshipIdAt(int index) => new(Id("0001", index));

    /// <summary>Adds a box, in whole inches, rotated by whole quarter turns.</summary>
    public EntityId AddBox(long x, long y, long width, long height, int quarterTurns = 0)
        => AddBox(Point2.Inches(x, y), Length.Inches(width), Length.Inches(height), Angle.Zero.Rotate90(quarterTurns));

    /// <summary>Adds a box.</summary>
    public EntityId AddBox(Point2 anchor, Length width, Length height, Angle rotation)
    {
        EntityId id = NextEntity();
        Sketch = Sketch.WithEntity(Box.AsDrawn(id, LayerId.Default, anchor, width, height, Box.DefaultDepth, rotation));
        return id;
    }

    /// <summary>Adds a blank with cuts on it, in whole inches, unrotated.</summary>
    public EntityId AddBlank(long x, long y, long width, long height, params Cut[] cuts)
    {
        EntityId id = NextEntity();
        Sketch = Sketch.WithEntity(
            Box.AsDrawn(id, LayerId.Default, Point2.Inches(x, y), Length.Inches(width), Length.Inches(height), Box.DefaultDepth, Angle.Zero) with
            {
                Cuts = [.. cuts],
            });
        return id;
    }

    /// <summary>Adds a node, in whole inches.</summary>
    public EntityId AddNode(long x, long y) => AddNode(Point2.Inches(x, y));

    /// <summary>Adds a node.</summary>
    public EntityId AddNode(Point2 position)
    {
        EntityId id = NextEntity();
        Sketch = Sketch.WithEntity(new Node(id, LayerId.Default, position));
        return id;
    }

    /// <summary>Adds a segment between two nodes.</summary>
    public EntityId AddSegment(EntityId start, EntityId end)
    {
        EntityId id = NextEntity();
        Sketch = Sketch.WithEntity(new Segment(id, LayerId.Default, start, end));
        return id;
    }

    /// <summary>Adds a dimension.</summary>
    public EntityId AddDimension(Measurand measures, RelationshipId? drives = null)
    {
        EntityId id = NextEntity();
        Sketch = Sketch.WithEntity(new Dimension(
            id,
            LayerId.Default,
            measures,
            drives,
            new DimensionPlacement(Length.Inches(8), DimensionSide.North)));
        return id;
    }

    /// <summary>Adds a relationship built from the id this method hands it.</summary>
    public RelationshipId Add(Func<RelationshipId, Relationship> build)
    {
        ArgumentNullException.ThrowIfNull(build);
        RelationshipId id = NextRelationship();
        Sketch = Sketch.WithRelationship(build(id));
        return id;
    }

    /// <summary>Anchors an entity.</summary>
    public RelationshipId Anchor(EntityId entity) => Add(id => new Anchored(id, entity));

    /// <summary>Holds two box edges flush.</summary>
    public RelationshipId Flush(EntityId a, BoxEdge edgeOfA, EntityId b, BoxEdge edgeOfB)
        => Add(id => new Flush(id, new BoxEdgeRef(a, edgeOfA), new BoxEdgeRef(b, edgeOfB)));

    /// <summary>Drives a box's width.</summary>
    public RelationshipId WidthIs(EntityId box, Length value)
        => Add(id => new ParamValue(id, new BoxWidthRef(box), value));

    /// <summary>Drives a box's height.</summary>
    public RelationshipId HeightIs(EntityId box, Length value)
        => Add(id => new ParamValue(id, new BoxHeightRef(box), value));

    /// <summary>Makes two box widths equal.</summary>
    public RelationshipId EqualWidths(EntityId a, EntityId b)
        => Add(id => new EqualParam(id, new BoxWidthRef(a), new BoxWidthRef(b)));

    /// <summary>The box with this id, which must be in the sketch.</summary>
    public Box BoxOf(EntityId id) => Sketch.Find<Box>(id) ?? throw new InvalidOperationException($"No box {id}.");

    /// <summary>The node with this id, which must be in the sketch.</summary>
    public Node NodeOf(EntityId id) => Sketch.Find<Node>(id) ?? throw new InvalidOperationException($"No node {id}.");

    private EntityId NextEntity() => new(Id("0000", _nextEntity++));

    private RelationshipId NextRelationship() => new(Id("0001", _nextRelationship++));

    private static Guid Id(string group, int index)
        => new($"00000000-0000-0000-{group}-{index.ToString("X12", CultureInfo.InvariantCulture)}");
}

/// <summary>Assertions that several test classes share.</summary>
internal static class SketchAssert
{
    /// <summary>The sketch is valid and every relationship in it holds.</summary>
    public static void IsConsistent(Sketch sketch, string? because = null)
    {
        ValidationResult validation = sketch.Validate();
        Assert.True(validation.IsValid, $"{because}{Environment.NewLine}{validation}");

        CheckReport check = RelationshipChecker.Check(sketch);
        Assert.True(check.AllHold, $"{because}{Environment.NewLine}{check}");
    }

    /// <summary>The box is exactly where and what it should be.</summary>
    public static void BoxIs(Sketch sketch, EntityId id, long x, long y, long width, long height)
    {
        Box box = sketch.Find<Box>(id) ?? throw new InvalidOperationException($"No box {id}.");

        Assert.Equal(Point3.Inches(x, y, 0), box.Anchor);
        Assert.Equal(Length.Inches(width), box.Width);
        Assert.Equal(Length.Inches(height), box.Height);
    }
}
