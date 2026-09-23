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
    /// <remarks>
    /// <para>
    /// One sketch in four is a row of parts rather than a scattering of them. A row is the shape
    /// issue #49 is about, and the scattered sketches reach one only by accident: the properties
    /// would pass without ever propagating along a chain. <see cref="LongestRow"/> is how the
    /// outcome guard proves they do.
    /// </para>
    /// <para>
    /// One sketch in two carries cuts, so that P1 covers invariants 5 to 9 as shaped parts §9.2
    /// asks. That is only safe now that the updater has the post-write fit check of §2.3: before
    /// it, a <see cref="SetParameter"/> that shrank a box below its cuts succeeded and handed back
    /// a sketch those invariants refuse. The other half stays plain, so nothing that used to be
    /// covered stops being.
    /// </para>
    /// </remarks>
    internal Sketch NextSketch()
    {
        Sketch sketch = _random.Next(4) == 0 ? NextRow() : NextScattering();
        return _random.Next(2) == 0 ? WithCuts(sketch) : sketch;
    }

    /// <summary>
    /// The same sketch with cuts derived from each blank's own sizes — the shaped half of
    /// <see cref="NextSketch"/>, and the one side of P12's pairing.
    /// </summary>
    /// <remarks>
    /// Cuts reference nothing outside their own box and move with it, so decorating a consistent
    /// sketch with cuts that fit leaves it consistent: no relationship reads one, and
    /// <see cref="NextCuts"/> derives every value from the blank it is going on.
    /// </remarks>
    internal Sketch WithCuts(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        Sketch result = sketch;
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            result = result.WithEntity(box with { Cuts = NextCuts(box.Width, box.Height) });
        }

        return result;
    }

    /// <summary>The same sketch with every cut stripped from every box — P12's other side.</summary>
    internal static Sketch WithoutCuts(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        Sketch result = sketch;
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            if (!box.Cuts.IsEmpty)
            {
                result = result.WithEntity(box with { Cuts = [] });
            }
        }

        return result;
    }

    /// <summary>
    /// A row of 3 to 6 parts, each flush with the next along one axis: a bookcase, a run of
    /// cabinets, a wall of studs — or, along Z, a stack: a top on legs on a plinth.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Everything about the row that could decide the answer by accident is randomised: the axis,
    /// which part is anchored (or none), which part carries the driving dimension, whether the
    /// sizes along the row are tied together with <see cref="EqualParam"/>, the order the
    /// relationships are added in — which is the order their ids come in — and which way round
    /// each one is written.
    /// </para>
    /// <para>
    /// The parts lie as drawn and unrotated, so that "the next one to the east" or "the next one
    /// up" is unambiguous; turned and tipped boxes are covered by the scattered sketches. Sizes and
    /// coordinates come from the same generators as everything else, so a row can start at a
    /// negative coordinate and can have odd-unit sizes.
    /// </para>
    /// </remarks>
    internal Sketch NextRow()
    {
        int parts = _random.Next(3, 7);
        Axis axis = (Axis)_random.Next(3);
        bool tied = _random.Next(3) == 0;
        Length common = NextSize();

        // -1 anchors nothing, which is the free-floating row of #49's comment.
        int anchored = _random.Next(-1, parts);
        int dimensioned = _random.Next(parts);

        Sketch sketch = Sketch.Empty;
        List<EntityId> row = [];
        List<Length> sizes = [];
        Length along = NextCoordinate();

        for (int i = 0; i < parts; i++)
        {
            Length size = tied ? common : NextSize();
            EntityId id = NextEntityId();

            sizes.Add(size);
            row.Add(id);

            Point3 anchor = new Point3(NextCoordinate(), NextCoordinate(), NextCoordinate()).WithComponent(axis, along);
            Vector3 sizes3 = new Vector3(NextSize(), NextSize(), NextSize()).WithComponent(axis, size);
            sketch = sketch.WithEntity(new Box(
                id, LayerId.Default, anchor, sizes3.Dx, sizes3.Dy, sizes3.Dz, BoxFace.Top, Angle.Zero));

            along += size;
        }

        (BoxFace leading, BoxFace trailing) = axis switch
        {
            Axis.X => (BoxFace.East, BoxFace.West),
            Axis.Y => (BoxFace.North, BoxFace.South),
            _ => (BoxFace.Top, BoxFace.Bottom),
        };
        ParamRef Size(int i) => SizeRef(row[i], axis);
        FeatureRef Face(int i, BoxFace face) => new(row[i], BoxFeature.Face(face));

        List<Func<Sketch, Sketch>> adds = [];
        for (int i = 0; i < parts - 1; i++)
        {
            int index = i;
            bool flipped = _random.Next(2) == 0;
            adds.Add(current => current.WithRelationship(flipped
                ? new Flush(NextRelationshipId(), Face(index + 1, trailing), Face(index, leading))
                : new Flush(NextRelationshipId(), Face(index, leading), Face(index + 1, trailing))));

            if (tied)
            {
                bool backwards = _random.Next(2) == 0;
                adds.Add(current => current.WithRelationship(backwards
                    ? new EqualParam(NextRelationshipId(), Size(index + 1), Size(index))
                    : new EqualParam(NextRelationshipId(), Size(index), Size(index + 1))));
            }
        }

        adds.Add(current => current.WithRelationship(
            new ParamValue(NextRelationshipId(), Size(dimensioned), sizes[dimensioned])));

        if (anchored >= 0)
        {
            adds.Add(current => current.WithRelationship(new Anchored(NextRelationshipId(), row[anchored])));
        }

        foreach (Func<Sketch, Sketch> add in Shuffled(adds))
        {
            sketch = Keep(sketch, add(sketch));
        }

        return sketch;
    }

    /// <summary>
    /// The longest run of boxes any chain of <see cref="Flush"/> relationships links together,
    /// which is how a test tells that it is looking at a row.
    /// </summary>
    internal static int LongestRow(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        Dictionary<EntityId, HashSet<EntityId>> linked = [];
        foreach (Flush flush in sketch.RelationshipsInOrder.OfType<Flush>())
        {
            Link(linked, flush.A.Owner, flush.B.Owner);
            Link(linked, flush.B.Owner, flush.A.Owner);
        }

        static void Link(Dictionary<EntityId, HashSet<EntityId>> linked, EntityId from, EntityId to)
        {
            if (!linked.TryGetValue(from, out HashSet<EntityId>? neighbours))
            {
                neighbours = [];
                linked[from] = neighbours;
            }

            neighbours.Add(to);
        }

        int longest = 0;
        HashSet<EntityId> seen = [];
        foreach (EntityId start in linked.Keys.Order())
        {
            if (!seen.Add(start))
            {
                continue;
            }

            int size = 1;
            Queue<EntityId> frontier = new([start]);
            while (frontier.Count > 0)
            {
                foreach (EntityId next in linked[frontier.Dequeue()].Order())
                {
                    if (seen.Add(next))
                    {
                        size++;
                        frontier.Enqueue(next);
                    }
                }
            }

            longest = Math.Max(longest, size);
        }

        return longest;
    }

    private Sketch NextScattering()
    {
        Sketch sketch = Sketch.Empty;

        int boxes = _random.Next(1, 7);
        for (int i = 0; i < boxes; i++)
        {
            sketch = sketch.WithEntity(NextBox());
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

    /// <summary>
    /// Unrelated boxes with valid cuts on them, at every rotation
    /// (docs/design/shaped-parts-model.md §9.2).
    /// </summary>
    /// <remarks>
    /// Cuts with no relationships around them, for the properties that are about the cut model
    /// itself — the invariants and the outline — rather than about propagation. The properties
    /// that need both use <see cref="NextSketch"/>, which carries cuts too now that the updater
    /// has §2.3's post-write fit check.
    /// </remarks>
    internal Sketch NextShapedSketch()
    {
        Sketch sketch = Sketch.Empty;

        int boxes = _random.Next(1, 5);
        for (int i = 0; i < boxes; i++)
        {
            Length width = NextSize();
            Length height = NextSize();

            sketch = sketch.WithEntity(Box.AsDrawn(
                NextEntityId(),
                LayerId.Default,
                new Point2(NextCoordinate(), NextCoordinate()),
                width,
                height,
                Box.DefaultDepth,
                Angle.Zero.Rotate90(_random.Next(0, 4))) with
            {
                Cuts = NextCuts(width, height),
            });
        }

        return sketch;
    }

    /// <summary>
    /// Cuts that fit the blank they are on, <em>derived</em> from its sizes rather than sampled
    /// and hoped for (design §7.2, shaped parts §9.2).
    /// </summary>
    /// <remarks>
    /// No value is more than a quarter of the size it is measured against, so the two claims on
    /// any edge — and a curve's claim against whatever faces it across the blank — come to at most
    /// half of it, and invariants 7, 8 and 9 hold by construction. A curved edge claims both of
    /// its corners, so the corners it takes are left alone, and two curves are only ever opposite
    /// each other.
    /// </remarks>
    internal ImmutableList<Cut> NextCuts(Length width, Length height)
    {
        Length alongX = new(width.Units / 4);
        Length alongY = new(height.Units / 4);
        if (alongX <= Length.Zero || alongY <= Length.Zero)
        {
            return [];
        }

        List<Cut> cuts = [];
        HashSet<BoxCorner> claimed = [];

        List<BoxEdge> curved = _random.Next(4) switch
        {
            0 => [RandomEdge()],
            1 => _random.Next(2) == 0 ? [BoxEdge.South, BoxEdge.North] : [BoxEdge.East, BoxEdge.West],
            _ => [],
        };

        foreach (BoxEdge edge in curved)
        {
            bool alongTheXAxis = edge is BoxEdge.South or BoxEdge.North;
            cuts.Add(new CurvedEdge(
                edge,
                _random.Next(2) == 0 ? Bow.Outward : Bow.Inward,
                UpTo(alongTheXAxis ? alongY : alongX)));

            (BoxCorner from, BoxCorner to) = Box.Ends(edge);
            claimed.Add(from);
            claimed.Add(to);
        }

        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            if (claimed.Contains(corner))
            {
                continue;
            }

            switch (_random.Next(3))
            {
                case 0:
                    cuts.Add(new CornerCut(corner, UpTo(alongX), UpTo(alongY)));
                    break;

                case 1:
                    cuts.Add(new RoundedCorner(corner, UpTo(Length.Min(alongX, alongY))));
                    break;
            }
        }

        return [.. cuts];
    }

    /// <summary>A positive length of at most <paramref name="limit"/>, on the raw unit grid.</summary>
    private Length UpTo(Length limit) => new(_random.NextInt64(1, limit.Units + 1));

    /// <summary>A request to put to the updater. It may well be one that cannot be satisfied.</summary>
    internal Request NextRequest(Sketch sketch)
    {
        List<Request> choices = [];

        EntityId entity = PickEntity(sketch, anything: true);

        // A node lies at the plan datum, so its Z is zero — except now and then, to ask the
        // updater the question it refuses.
        Length z = sketch.Find(entity) is Node && _random.Next(4) != 0 ? Length.Zero : NextCoordinate();
        choices.Add(new SetPosition(entity, new Point3(NextCoordinate(), NextCoordinate(), z)));
        choices.Add(new Drag(entity, NextDelta3()));
        choices.Add(new SetLayer(entity, LayerId.Default));
        choices.Add(new RemoveEntity(entity));

        List<Box> allBoxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        if (allBoxes.Count > 0)
        {
            Box box = allBoxes[_random.Next(allBoxes.Count)];
            choices.Add(new SetOrientation(box.Id, RandomFace(), Angle.Zero.Rotate90(_random.Next(0, 4))));
            choices.Add(new DragFace(box.Id, RandomFace(), NextDelta()));
            choices.Add(new AddRelationship(new ParamValue(NextRelationshipId(), SizeRef(box.Id, RandomAxis()), NextSize())));
            choices.Add(new AddEntity(NextBox()));

            if (allBoxes.Count > 1)
            {
                Box other = allBoxes[(allBoxes.IndexOf(box) + 1) % allBoxes.Count];
                choices.Add(new AddRelationship(new Flush(
                    NextRelationshipId(), FaceOf(box.Id), FaceOf(other.Id))));
                choices.Add(new AddRelationship(new Coincident(
                    NextRelationshipId(), PlanUpright(box), PlanUpright(other))));
                choices.Add(new AddRelationship(new Coincident(
                    NextRelationshipId(), VertexOf(box.Id), VertexOf(other.Id))));
                choices.Add(new AddRelationship(new AxisDistance(
                    NextRelationshipId(), VertexOf(box.Id), VertexOf(other.Id), RandomAxis(), NextDelta())));
                choices.Add(new AddRelationship(new EqualParam(
                    NextRelationshipId(), SizeRef(box.Id, RandomAxis()), SizeRef(other.Id, RandomAxis()))));
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

    /// <summary>
    /// A request whose whole business is changing a size — what P13 is about: the three ways a
    /// blank gets resized in the direct updater (a typed number, a stated one, a resize handle).
    /// </summary>
    internal Request? NextResize(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        List<Box> boxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        if (boxes.Count == 0)
        {
            return null;
        }

        Box box = boxes[_random.Next(boxes.Count)];
        List<Request> choices = [new DragFace(box.Id, RandomFace(), NextDelta())];

        choices.Add(new AddRelationship(new ParamValue(NextRelationshipId(), SizeRef(box.Id, RandomAxis()), NextSize())));

        if (NextSetParameter(sketch) is { } typed)
        {
            choices.Add(typed);
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

    /// <summary>A drag of something that can carry coordinates, in space.</summary>
    internal Drag NextDrag(Sketch sketch)
        => new(PickEntity(sketch, anything: false), NextDelta3());

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
    // the sketch is still valid and everything in it still holds. Every place is read through
    // Sketch.PlaceOf, so a derivation is right for any of the 24 orientations and along Z.
    // -----------------------------------------------------------------------------------------

    private Sketch TryDerive(Sketch sketch)
    {
        List<Box> boxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        List<Node> nodes = [.. sketch.Entities.Values.OfType<Node>().OrderBy(node => node.Id)];

        return _random.Next(0, 8) switch
        {
            0 when boxes.Count > 0 => DeriveParamValue(sketch, boxes),
            1 when boxes.Count > 1 => DeriveEqualParam(sketch, boxes),
            2 when boxes.Count > 1 => DeriveFlush(sketch, boxes),
            3 when boxes.Count > 0 && nodes.Count > 0 => DeriveCoincident(sketch, boxes, nodes),
            4 when boxes.Count > 1 => DeriveAxisDistance(sketch, boxes),
            5 when nodes.Count > 2 => DeriveCentered(sketch, nodes),
            6 when boxes.Count > 1 => DeriveBoxCoincident(sketch, boxes),
            7 when boxes.Count > 2 => DeriveBoxCentered(sketch, boxes),
            _ => sketch,
        };
    }

    private Sketch DeriveParamValue(Sketch sketch, List<Box> boxes)
    {
        Box box = boxes[_random.Next(boxes.Count)];
        Axis axis = RandomAxis();

        return Keep(sketch, sketch.WithRelationship(new ParamValue(NextRelationshipId(), SizeRef(box.Id, axis), box.Size(axis))));
    }

    private Sketch DeriveEqualParam(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);
        Axis ofFirst = RandomAxis();
        Axis ofSecond = _random.Next(2) == 0 ? ofFirst : RandomAxis();

        // Derive: the second box takes the first's size, then the relationship is true — a leg's
        // depth equal to another's, or to an apron's width.
        Box resized = ofSecond switch
        {
            Axis.X => second with { Width = first.Size(ofFirst) },
            Axis.Y => second with { Height = first.Size(ofFirst) },
            _ => second with { Depth = first.Size(ofFirst) },
        };

        Sketch derived = sketch
            .WithEntity(resized)
            .WithRelationship(new EqualParam(NextRelationshipId(), SizeRef(first.Id, ofFirst), SizeRef(second.Id, ofSecond)));

        return Keep(sketch, derived);
    }

    private Sketch DeriveFlush(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);
        FeatureRef faceOfFirst = FaceOf(first.Id);
        FeatureRef faceOfSecond = FaceOf(second.Id);

        // Two faces are in one plane only when they are perpendicular to the same world axis.
        if (sketch.PlaceOf(faceOfFirst).Axes is not [var normal] || sketch.PlaceOf(faceOfSecond).Axes is not [var other] || other != normal)
        {
            return sketch;
        }

        Length gap = sketch.PlaceOf(faceOfFirst)[normal] - sketch.PlaceOf(faceOfSecond)[normal];

        Sketch derived = sketch
            .WithEntity(second with { Anchor = second.Anchor + Vector3.Along(normal, gap) })
            .WithRelationship(new Flush(NextRelationshipId(), faceOfFirst, faceOfSecond));

        return Keep(sketch, derived);
    }

    private Sketch DeriveCoincident(Sketch sketch, List<Box> boxes, List<Node> nodes)
    {
        Box box = boxes[_random.Next(boxes.Count)];
        Node node = nodes[_random.Next(nodes.Count)];

        // A node meets a plan upright — whichever local edge stands vertical at that plan corner.
        FeatureRef corner = PlanUpright(box);

        Sketch derived = sketch
            .WithEntity(node with { Position = sketch.PlanPoint(corner) })
            .WithRelationship(new Coincident(NextRelationshipId(), corner, new NodeRef(node.Id)));

        return Keep(sketch, derived);
    }

    /// <summary>
    /// Two boxes held at a common place: two vertices on all three axes, or two plan uprights on X
    /// and Y — a leg's top corner under a top's, a stud at a plate's corner.
    /// </summary>
    private Sketch DeriveBoxCoincident(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);
        bool vertices = _random.Next(2) == 0;
        FeatureRef ofFirst = vertices ? VertexOf(first.Id) : PlanUpright(first);
        FeatureRef ofSecond = vertices ? VertexOf(second.Id) : PlanUpright(second);

        Place a = sketch.PlaceOf(ofFirst);
        Place b = sketch.PlaceOf(ofSecond);
        Vector3 shift = Vector3.Zero;
        foreach (Axis axis in Place.Common(a, b))
        {
            shift = shift.WithComponent(axis, a[axis] - b[axis]);
        }

        Sketch derived = sketch
            .WithEntity(second with { Anchor = second.Anchor + shift })
            .WithRelationship(new Coincident(NextRelationshipId(), ofFirst, ofSecond));

        return Keep(sketch, derived);
    }

    private Sketch DeriveAxisDistance(Sketch sketch, List<Box> boxes)
    {
        (Box first, Box second) = TwoOf(boxes);
        Axis axis = RandomAxis();
        PlaceRef from = NextPlaceOn(first);
        PlaceRef to = NextPlaceOn(second);

        if (sketch.PlaceOf(from).Coordinate(axis) is not { } start || sketch.PlaceOf(to).Coordinate(axis) is not { } end)
        {
            return sketch;
        }

        return Keep(sketch, sketch.WithRelationship(
            new AxisDistance(NextRelationshipId(), from, to, axis, end - start)));
    }

    private Sketch DeriveCentered(Sketch sketch, List<Node> nodes)
    {
        List<Node> chosen = Shuffled(nodes).Take(3).ToList();
        Axis axis = _random.Next(2) == 0 ? Axis.X : Axis.Y;

        Length target = RoundedMidpoint(
            sketch.PlanPoint(new NodeRef(chosen[1].Id)).Component(axis),
            sketch.PlanPoint(new NodeRef(chosen[2].Id)).Component(axis));

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

    /// <summary>
    /// A box's centre midway between a face of each of two others, along any axis — a shelf
    /// centred between a bottom and a lid, a stretcher between two legs.
    /// </summary>
    private Sketch DeriveBoxCentered(Sketch sketch, List<Box> boxes)
    {
        List<Box> chosen = Shuffled(boxes).Take(3).ToList();
        Axis axis = RandomAxis();

        if (FaceFixing(sketch, chosen[1], axis) is not { } a || FaceFixing(sketch, chosen[2], axis) is not { } b)
        {
            return sketch;
        }

        CenterRef middle = new(chosen[0].Id);
        Length target = RoundedMidpoint(sketch.PlaceOf(a)[axis], sketch.PlaceOf(b)[axis]);
        Length shift = target - sketch.PlaceOf(middle)[axis];

        Sketch derived = sketch
            .WithEntity(chosen[0] with { Anchor = chosen[0].Anchor + Vector3.Along(axis, shift) })
            .WithRelationship(new Centered(NextRelationshipId(), middle, a, b, axis));

        return Keep(sketch, derived);
    }

    /// <summary>The midpoint the propagator and the checker both use.</summary>
    private static Length RoundedMidpoint(Length a, Length b) => (a + b).Divide(2, Rounding.HalfToEven);

    /// <summary>One of the two faces of a box perpendicular to a world axis, picked at random.</summary>
    private FeatureRef? FaceFixing(Sketch sketch, Box box, Axis axis)
    {
        foreach (BoxFace face in Shuffled(Enum.GetValues<BoxFace>()))
        {
            FeatureRef reference = new(box.Id, BoxFeature.Face(face));
            if (sketch.PlaceOf(reference).Axes is [var only] && only == axis)
            {
                return reference;
            }
        }

        return null;
    }

    /// <summary>A place on a box that fixes some axes: a vertex, a face, a plan upright or the centre.</summary>
    private PlaceRef NextPlaceOn(Box box) => _random.Next(4) switch
    {
        0 => VertexOf(box.Id),
        1 => FaceOf(box.Id),
        2 => PlanUpright(box),
        _ => new CenterRef(box.Id),
    };

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

    private BoxFace RandomFace() => (BoxFace)_random.Next(0, 6);

    private Axis RandomAxis() => (Axis)_random.Next(0, 3);

    /// <summary>
    /// A box in space: half of them lying as drawn and half tipped onto a random face, at any
    /// quarter-turn spin, at the plan datum or above or below it, with a depth that is the default
    /// or anything else (docs/design/assembly-model.md &#xA7;9.2).
    /// </summary>
    private Box NextBox() => new(
        NextEntityId(),
        LayerId.Default,
        new Point3(NextCoordinate(), NextCoordinate(), _random.Next(3) == 0 ? Length.Zero : NextCoordinate()),
        NextSize(),
        NextSize(),
        _random.Next(3) == 0 ? Box.DefaultDepth : NextSize(),
        _random.Next(2) == 0 ? BoxFace.Top : RandomFace(),
        Angle.Zero.Rotate90(_random.Next(0, 4)));

    /// <summary>The size of a box along one of its local axes, as a reference.</summary>
    private static ParamRef SizeRef(EntityId box, Axis local) => local switch
    {
        Axis.X => new BoxWidthRef(box),
        Axis.Y => new BoxHeightRef(box),
        _ => new BoxDepthRef(box),
    };

    private FeatureRef FaceOf(EntityId box) => new(box, BoxFeature.Face(RandomFace()));

    private FeatureRef VertexOf(EntityId box)
        => new(box, BoxFeature.Vertex(RandomCorner(), _random.Next(2) == 0 ? BoxLevel.Bottom : BoxLevel.Top));

    /// <summary>The edge that stands vertical at a random corner of a box's footprint: fixes X and Y.</summary>
    private FeatureRef PlanUpright(Box box) => new(box.Id, box.Footprint().UprightAt(RandomCorner()));

    /// <summary>A movement in space: along Z half the time, in the plan otherwise.</summary>
    private Vector3 NextDelta3() => new(NextDelta(), NextDelta(), _random.Next(2) == 0 ? Length.Zero : NextDelta());

    private BoxCorner RandomCorner() => (BoxCorner)_random.Next(0, 4);

    /// <summary>A coordinate within a few feet of the origin.</summary>
    private Length NextCoordinate() => new((_random.Next(-96, 97) * (Length.UnitsPerInch / 16)) + Jitter());

    /// <summary>A size from 1&#x2033; to 40&#x2033;.</summary>
    private Length NextSize() => new((_random.Next(16, 641) * (Length.UnitsPerInch / 16)) + Jitter());

    /// <summary>A movement of up to a foot either way.</summary>
    private Length NextDelta() => new((_random.Next(-192, 193) * (Length.UnitsPerInch / 16)) + Jitter());

    /// <summary>
    /// A few raw units on top of the 1/16&#x2033; grid, some of the time.
    /// </summary>
    /// <remarks>
    /// Without this, every coordinate, size and delta is a multiple of 64 units, no span is ever
    /// an odd number of units, and nothing the generator produces can reach the
    /// <see cref="Centered"/> half-unit case at all — which is how a real defect hid from P1 and
    /// P6. Decimal entry (3.505&#x2033; is 3589 units) and the centre of an odd-width box both
    /// make odd values ordinary in practice.
    /// </remarks>
    private long Jitter() => _random.Next(4) == 0 ? _random.Next(-3, 4) : 0;

    private EntityId NextEntityId() => new(Id("0000", _nextEntity++));

    private RelationshipId NextRelationshipId() => new(Id("0001", _nextRelationship++));

    private static Guid Id(string group, int index)
        => new($"00000000-0000-0000-{group}-{index.ToString("X12", CultureInfo.InvariantCulture)}");
}
