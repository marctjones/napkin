using System.Collections.Immutable;
using System.Globalization;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Builds valid rectilinear sketches and requests against them, from a seed.
/// </summary>
/// <remarks>
/// <para>
/// Relationships are built by <em>deriving</em> positions, never by sampling positions and hoping
/// (design &#xA7;7.2): each candidate relationship first moves or resizes the entity that should
/// follow, and the whole attempt is rolled back if the sketch no longer validates or no longer
/// satisfies everything in it. So every sketch this hands out is valid and consistent.
/// </para>
/// <para>
/// Ids are sequential, from the generator rather than from <see cref="EntityId.New"/>, so a
/// printed seed reproduces a failure exactly — everything downstream iterates in id order.
/// </para>
/// </remarks>
internal sealed class SketchGenerator
{
    private readonly Random _random;
    private int _nextEntity = 1;
    private int _nextRelationship = 1;

    internal SketchGenerator(int seed) => _random = new Random(seed);

    /// <summary>A valid, consistent rectilinear sketch.</summary>
    internal Sketch NextSketch()
    {
        Sketch sketch = Sketch.Empty;

        int boxes = _random.Next(1, 7);
        for (int i = 0; i < boxes; i++)
        {
            sketch = sketch.WithEntity(new Box(
                NextEntityId(),
                LayerId.Default,
                new Point2(NextCoordinate(), NextCoordinate()),
                NextSize(),
                NextSize(),
                Angle.Zero.Rotate90(_random.Next(0, 4))));
        }

        int nodes = _random.Next(0, 4);
        for (int i = 0; i < nodes; i++)
        {
            sketch = sketch.WithEntity(new Node(
                NextEntityId(),
                LayerId.Default,
                new Point2(NextCoordinate(), NextCoordinate())));
        }

        int relationships = _random.Next(0, 9);
        for (int i = 0; i < relationships; i++)
        {
            sketch = TryDerive(sketch);
        }

        // Anchors go last: they hold entities still, and everything above moves entities to make
        // its relationship true.
        int anchors = _random.Next(0, 3);
        for (int i = 0; i < anchors; i++)
        {
            EntityId entity = PickEntity(sketch, anything: false);
            sketch = Keep(sketch, sketch.WithRelationship(new Anchored(NextRelationshipId(), entity)));
        }

        return sketch;
    }

    /// <summary>A request to put to the updater. It may well be one that cannot be satisfied.</summary>
    internal Request NextRequest(Sketch sketch)
    {
        List<Request> choices = [];

        EntityId entity = PickEntity(sketch, anything: true);
        choices.Add(new SetPosition(entity, new Point2(NextCoordinate(), NextCoordinate())));
        choices.Add(new Drag(entity, new Vector2(NextDelta(), NextDelta())));
        choices.Add(new SetLayer(entity, LayerId.Default));
        choices.Add(new RemoveEntity(entity));

        List<Box> allBoxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        if (allBoxes.Count > 0)
        {
            Box box = allBoxes[_random.Next(allBoxes.Count)];
            choices.Add(new SetRotation(box.Id, Angle.Zero.Rotate90(_random.Next(0, 4))));
            choices.Add(new DragEdge(box.Id, RandomEdge(), NextDelta()));
            choices.Add(new AddRelationship(new ParamValue(NextRelationshipId(), new BoxWidthRef(box.Id), NextSize())));
            choices.Add(new AddEntity(new Box(
                NextEntityId(), LayerId.Default, new Point2(NextCoordinate(), NextCoordinate()),
                NextSize(), NextSize(), Angle.Zero)));

            if (allBoxes.Count > 1)
            {
                Box other = allBoxes[(allBoxes.IndexOf(box) + 1) % allBoxes.Count];
                choices.Add(new AddRelationship(new Flush(
                    NextRelationshipId(), new BoxEdgeRef(box.Id, RandomEdge()), new BoxEdgeRef(other.Id, RandomEdge()))));
                choices.Add(new AddRelationship(new Coincident(
                    NextRelationshipId(), new CornerRef(box.Id, RandomCorner()), new CornerRef(other.Id, RandomCorner()))));
                choices.Add(new AddRelationship(new EqualParam(
                    NextRelationshipId(), new BoxWidthRef(box.Id), new BoxWidthRef(other.Id))));
            }
        }

        List<Relationship> all = [.. sketch.RelationshipsInOrder];
        if (all.Count > 0)
        {
            Relationship relationship = all[_random.Next(all.Count)];
            choices.Add(new RemoveRelationship(relationship.Id));

            List<Relationship> drivers = [.. all.Where(candidate => candidate is ParamValue or AxisDistance)];
            if (drivers.Count > 0)
            {
                choices.Add(new SetParameter(drivers[_random.Next(drivers.Count)].Id, NextSize()));
            }
        }

        return choices[_random.Next(choices.Count)];
    }

    /// <summary>A request that sets a number a relationship owns, or null when nothing owns one.</summary>
    internal SetParameter? NextSetParameter(Sketch sketch)
    {
        List<Relationship> drivers = [.. sketch.RelationshipsInOrder.Where(r => r is ParamValue or AxisDistance)];
        return drivers.Count == 0
            ? null
            : new SetParameter(drivers[_random.Next(drivers.Count)].Id, NextSize());
    }

    /// <summary>A drag of something that can carry coordinates.</summary>
    internal Drag NextDrag(Sketch sketch)
        => new(PickEntity(sketch, anything: false), new Vector2(NextDelta(), NextDelta()));

    /// <summary>The same sketch with its dictionaries built in a different insertion order.</summary>
    internal Sketch Shuffle(Sketch sketch)
    {
        ImmutableDictionary<EntityId, Entity> entities = ImmutableDictionary<EntityId, Entity>.Empty;
        foreach (Entity entity in Shuffled(sketch.Entities.Values))
        {
            entities = entities.Add(entity.Id, entity);
        }

        ImmutableDictionary<RelationshipId, Relationship> relationships
            = ImmutableDictionary<RelationshipId, Relationship>.Empty;
        foreach (Relationship relationship in Shuffled(sketch.Relationships.Values))
        {
            relationships = relationships.Add(relationship.Id, relationship);
        }

        return sketch with { Entities = entities, Relationships = relationships };
    }

    private List<T> Shuffled<T>(IEnumerable<T> source)
    {
        List<T> items = [.. source];
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }

        return items;
    }

    // -----------------------------------------------------------------------------------------
    // Deriving relationships: move the entity that should follow, then keep the attempt only if
    // the sketch is still valid and everything in it still holds.
    // -----------------------------------------------------------------------------------------

    private Sketch TryDerive(Sketch sketch)
    {
        List<Box> boxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        List<Node> nodes = [.. sketch.Entities.Values.OfType<Node>().OrderBy(node => node.Id)];

        return _random.Next(0, 6) switch
        {
            0 when boxes.Count > 0 => DeriveParamValue(sketch, boxes),
            1 when boxes.Count > 1 => DeriveEqualParam(sketch, boxes),
            2 when boxes.Count > 1 => DeriveFlush(sketch, boxes),
            3 when boxes.Count > 0 && nodes.Count > 0 => DeriveCoincident(sketch, boxes, nodes),
            4 when boxes.Count > 1 => DeriveAxisDistance(sketch, boxes),
            5 when nodes.Count > 2 => DeriveCentered(sketch, nodes),
            _ => sketch,
        };
    }

    private Sketch DeriveParamValue(Sketch sketch, List<Box> boxes)
    {
        Box box = boxes[_random.Next(boxes.Count)];
        bool width = _random.Next(2) == 0;

        ParamRef param = width ? new BoxWidthRef(box.Id) : new BoxHeightRef(box.Id);
        Length value = width ? box.Width : box.Height;

        return Keep(sketch, sketch.WithRelationship(new ParamValue(NextRelationshipId(), param, value)));
    }

    private Sketch DeriveEqualParam(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);

        // Derive: the second box takes the first's width, then the relationship is true.
        Sketch derived = sketch
            .WithEntity(second with { Width = first.Width })
            .WithRelationship(new EqualParam(NextRelationshipId(), new BoxWidthRef(first.Id), new BoxWidthRef(second.Id)));

        return Keep(sketch, derived);
    }

    private Sketch DeriveFlush(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);
        BoxEdgeRef edgeOfFirst = new(first.Id, RandomEdge());
        BoxEdgeRef edgeOfSecond = new(second.Id, RandomEdge());

        Axis? axis = SharedNormalAxis(sketch, edgeOfFirst, edgeOfSecond);
        if (axis is not { } normal)
        {
            return sketch;
        }

        Length gap = sketch.EdgeOf(edgeOfFirst).From.Component(normal)
                     - sketch.EdgeOf(edgeOfSecond).From.Component(normal);

        Sketch derived = sketch
            .WithEntity(second with { Anchor = second.Anchor + Vector2.Along(normal, gap) })
            .WithRelationship(new Flush(NextRelationshipId(), edgeOfFirst, edgeOfSecond));

        return Keep(sketch, derived);
    }

    private Sketch DeriveCoincident(Sketch sketch, List<Box> boxes, List<Node> nodes)
    {
        Box box = boxes[_random.Next(boxes.Count)];
        Node node = nodes[_random.Next(nodes.Count)];
        CornerRef corner = new(box.Id, RandomCorner());

        Sketch derived = sketch
            .WithEntity(node with { Position = sketch.PointOf(corner) })
            .WithRelationship(new Coincident(NextRelationshipId(), corner, new NodeRef(node.Id)));

        return Keep(sketch, derived);
    }

    private Sketch DeriveAxisDistance(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);
        Axis axis = _random.Next(2) == 0 ? Axis.X : Axis.Y;
        CornerRef from = new(first.Id, RandomCorner());
        CornerRef to = new(second.Id, RandomCorner());

        Length distance = sketch.PointOf(to).Component(axis) - sketch.PointOf(from).Component(axis);

        return Keep(sketch, sketch.WithRelationship(
            new AxisDistance(NextRelationshipId(), from, to, axis, distance)));
    }

    private Sketch DeriveCentered(Sketch sketch, List<Node> nodes)
    {
        List<Node> chosen = Shuffled(nodes).Take(3).ToList();
        Axis axis = _random.Next(2) == 0 ? Axis.X : Axis.Y;

        Length target = RoundedMidpoint(
            sketch.PointOf(new NodeRef(chosen[1].Id)).Component(axis),
            sketch.PointOf(new NodeRef(chosen[2].Id)).Component(axis));

        Sketch derived = sketch
            .WithEntity(chosen[0] with { Position = chosen[0].Position.WithComponent(axis, target) })
            .WithRelationship(new Centered(
                NextRelationshipId(),
                new NodeRef(chosen[0].Id),
                new NodeRef(chosen[1].Id),
                new NodeRef(chosen[2].Id),
                axis));

        return Keep(sketch, derived);
    }

    /// <summary>The midpoint the propagator and the checker both use.</summary>
    private static Length RoundedMidpoint(Length a, Length b) => (a + b).Divide(2, Rounding.HalfToEven);

    private static Axis? SharedNormalAxis(Sketch sketch, EdgeRef first, EdgeRef second)
    {
        Axis? a = NormalAxis(sketch.EdgeOf(first));
        return a is { } axis && NormalAxis(sketch.EdgeOf(second)) == axis ? axis : null;
    }

    private static Axis? NormalAxis((Point2 From, Point2 To) edge)
    {
        bool sameX = edge.From.X == edge.To.X;
        bool sameY = edge.From.Y == edge.To.Y;
        return sameX == sameY ? null : sameX ? Axis.X : Axis.Y;
    }

    /// <summary>Keeps the attempt only if it left a sketch that is valid and holds together.</summary>
    private static Sketch Keep(Sketch before, Sketch attempt)
        => attempt.Validate().IsValid && RelationshipChecker.Check(attempt).AllHold ? attempt : before;

    private (Box First, Box Second) TwoOf(List<Box> boxes)
    {
        int first = _random.Next(boxes.Count);
        int second = (first + 1 + _random.Next(boxes.Count - 1)) % boxes.Count;
        return (boxes[first], boxes[second]);
    }

    private EntityId PickEntity(Sketch sketch, bool anything)
    {
        List<EntityId> candidates =
        [
            .. sketch.Entities.Values
                .Where(entity => anything || entity is Box or Node)
                .OrderBy(entity => entity.Id)
                .Select(entity => entity.Id),
        ];

        return candidates.Count == 0 ? NextEntityId() : candidates[_random.Next(candidates.Count)];
    }

    private BoxEdge RandomEdge() => (BoxEdge)_random.Next(0, 4);

    private BoxCorner RandomCorner() => (BoxCorner)_random.Next(0, 4);

    /// <summary>A coordinate on a 1/16&#x2033; grid, within a few feet of the origin.</summary>
    private Length NextCoordinate() => new(_random.Next(-96, 97) * (Length.UnitsPerInch / 16));

    /// <summary>A size from 1&#x2033; to 40&#x2033;, on a 1/16&#x2033; grid.</summary>
    private Length NextSize() => new(_random.Next(16, 641) * (Length.UnitsPerInch / 16));

    /// <summary>A movement of up to a foot either way, on a 1/16&#x2033; grid.</summary>
    private Length NextDelta() => new(_random.Next(-192, 193) * (Length.UnitsPerInch / 16));

    private EntityId NextEntityId() => new(Id("0000", _nextEntity++));

    private RelationshipId NextRelationshipId() => new(Id("0001", _nextRelationship++));

    private static Guid Id(string group, int index)
        => new($"00000000-0000-0000-{group}-{index.ToString("X12", CultureInfo.InvariantCulture)}");
}
