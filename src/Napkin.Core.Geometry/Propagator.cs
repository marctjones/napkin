using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Which number of an entity an assignment is about.</summary>
internal enum ScalarKind
{
    /// <summary>A box's anchor X, or a node's X.</summary>
    X,

    /// <summary>A box's anchor Y, or a node's Y.</summary>
    Y,

    /// <summary>A box's width, along its local X.</summary>
    Width,

    /// <summary>A box's height, along its local Y.</summary>
    Height,
}

/// <summary>One number the propagator can assign.</summary>
internal readonly record struct ScalarKey(EntityId Entity, ScalarKind Kind);

/// <summary>A value the propagator decided on, and the chain of relationships it came through.</summary>
internal sealed record Assignment(Length Value, ImmutableList<RelationshipId> Via);

/// <summary>What the propagator worked out, or why it could not.</summary>
internal abstract record PropagationResult;

/// <summary>Every number the propagation settled on.</summary>
internal sealed record Propagated(ImmutableDictionary<ScalarKey, Assignment> Assignments) : PropagationResult;

/// <summary>Two derivations that cannot both be true.</summary>
internal sealed record Conflicted(ConflictReport Report) : PropagationResult;

/// <summary>
/// Worklist propagation over the relationship graph: exact, deterministic and small.
/// </summary>
/// <remarks>
/// <para>
/// This is the engine, on its own, as design &#xA7;4.4 asks. It takes a sketch, seed assignments
/// and the relationships to honour, and returns the assignment table or a conflict. It does
/// <em>not</em> check the rectilinear precondition — <see cref="DirectUpdater"/> does that before
/// calling it — because the solver's repair pass (&#xA7;5.2 step 4) calls this directly on a
/// rotated sketch with the exact-class relationships only. Without that split, #28's
/// exactness-repair pass would start with a refactor of #5.
/// </para>
/// <para>
/// <strong>Two phases.</strong> No positional relationship ever assigns a size: only
/// <see cref="ParamValue"/> and <see cref="EqualParam"/> do. So sizes settle first, and the
/// positional pass then computes every corner offset from final sizes. Without this a corner
/// offset could be computed from a size that a later relationship changes, and the stale
/// derivation would look like a contradiction.
/// </para>
/// </remarks>
internal sealed class Propagator
{
    private readonly Sketch _sketch;
    private readonly Dictionary<ScalarKey, Assignment> _assigned = [];
    private readonly Queue<RelationshipId> _queue = new();
    private readonly HashSet<RelationshipId> _inQueue = [];
    private readonly Dictionary<EntityId, List<Relationship>> _byEntity = [];

    private List<Relationship> _phase = [];
    private ConflictReport? _conflict;

    private Propagator(Sketch sketch) => _sketch = sketch;

    /// <summary>
    /// Propagates from <paramref name="seeds"/> through <paramref name="honoured"/>.
    /// </summary>
    /// <param name="sketch">The sketch to read current values from. Never modified.</param>
    /// <param name="seeds">
    /// The numbers the request sets directly. An empty seed set still propagates: every
    /// <see cref="ParamValue"/> asserts its value and every relationship is examined once, which
    /// is what the solver's repair pass needs.
    /// </param>
    /// <param name="honoured">The relationships to honour, in any order; they are sorted by id here.</param>
    internal static PropagationResult Run(
        Sketch sketch,
        IReadOnlyDictionary<ScalarKey, Length> seeds,
        IEnumerable<Relationship> honoured)
    {
        Propagator propagator = new(sketch);
        return propagator.Propagate(seeds, [.. honoured.OrderBy(relationship => relationship.Id)]);
    }

    /// <summary>The value a scalar has now: what was assigned, or what the sketch says.</summary>
    internal Length CurrentOf(ScalarKey key)
        => _assigned.TryGetValue(key, out Assignment? assignment) ? assignment.Value : StoredValueOf(_sketch, key);

    /// <summary>The value a scalar has in a sketch, before any assignment.</summary>
    internal static Length StoredValueOf(Sketch sketch, ScalarKey key) => sketch.Find(key.Entity) switch
    {
        Box box => key.Kind switch
        {
            ScalarKind.X => box.Anchor.X,
            ScalarKind.Y => box.Anchor.Y,
            ScalarKind.Width => box.Width,
            _ => box.Height,
        },
        Node node => key.Kind == ScalarKind.X ? node.Position.X : node.Position.Y,
        _ => Length.Zero,
    };

    /// <summary>What an assignment to a scalar is about, for a conflict report.</summary>
    internal static AssignmentTarget TargetOf(Sketch sketch, ScalarKey key) => key.Kind switch
    {
        ScalarKind.Width => new ParamTarget(new BoxWidthRef(key.Entity)),
        ScalarKind.Height => new ParamTarget(new BoxHeightRef(key.Entity)),
        _ => new PointAxisTarget(
            sketch.Find(key.Entity) is Box
                ? new CornerRef(key.Entity, BoxCorner.SouthWest)
                : new NodeRef(key.Entity),
            key.Kind == ScalarKind.X ? Axis.X : Axis.Y),
    };

    private static ScalarKind KindOf(Axis axis) => axis == Axis.X ? ScalarKind.X : ScalarKind.Y;

    private PropagationResult Propagate(IReadOnlyDictionary<ScalarKey, Length> seeds, List<Relationship> honoured)
    {
        // Anchored entities' positions are pre-assigned to their current values with an Anchored
        // derivation, so any attempt to move them is a contradiction with a nameable cause.
        foreach (Relationship relationship in honoured)
        {
            if (relationship is Anchored anchored)
            {
                Assign(new ScalarKey(anchored.Entity, ScalarKind.X), CurrentOf(new ScalarKey(anchored.Entity, ScalarKind.X)), [anchored.Id]);
                Assign(new ScalarKey(anchored.Entity, ScalarKind.Y), CurrentOf(new ScalarKey(anchored.Entity, ScalarKind.Y)), [anchored.Id]);
            }
        }

        // The request's own seeds carry no relationship: they are the request itself.
        foreach (KeyValuePair<ScalarKey, Length> seed in seeds.OrderBy(entry => entry.Key.Entity).ThenBy(entry => entry.Key.Kind))
        {
            Assign(seed.Key, seed.Value, ImmutableList<RelationshipId>.Empty);
        }

        RunPhase(honoured.Where(IsSizeRelationship).ToList());
        RunPhase(honoured.Where(relationship => !IsSizeRelationship(relationship) && relationship is not Anchored).ToList());

        return _conflict is not null
            ? new Conflicted(_conflict)
            : new Propagated(_assigned.ToImmutableDictionary());
    }

    private static bool IsSizeRelationship(Relationship relationship) => relationship is ParamValue or EqualParam;

    private void RunPhase(List<Relationship> phase)
    {
        if (_conflict is not null)
        {
            return;
        }

        _phase = phase;
        _byEntity.Clear();
        foreach (Relationship relationship in phase)
        {
            foreach (EntityId entity in relationship.References.Distinct())
            {
                if (!_byEntity.TryGetValue(entity, out List<Relationship>? list))
                {
                    list = [];
                    _byEntity[entity] = list;
                }

                list.Add(relationship);
            }
        }

        _queue.Clear();
        _inQueue.Clear();
        foreach (Relationship relationship in phase)
        {
            Enqueue(relationship.Id);
        }

        while (_queue.Count > 0 && _conflict is null)
        {
            RelationshipId id = _queue.Dequeue();
            _inQueue.Remove(id);

            Relationship? relationship = _phase.Find(candidate => candidate.Id == id);
            if (relationship is not null)
            {
                Process(relationship);
            }
        }
    }

    private void Enqueue(RelationshipId id)
    {
        if (_inQueue.Add(id))
        {
            _queue.Enqueue(id);
        }
    }

    private void Process(Relationship relationship)
    {
        switch (relationship)
        {
            case Coincident coincident:
                Resolve(PointSide(coincident.A, Axis.X), PointSide(coincident.B, Axis.X), relationship);
                Resolve(PointSide(coincident.A, Axis.Y), PointSide(coincident.B, Axis.Y), relationship);
                break;

            case Horizontal { Edge: SegmentRef horizontal }:
                ResolveSegmentAlignment(horizontal, Axis.Y, relationship);
                break;

            case Vertical { Edge: SegmentRef vertical }:
                ResolveSegmentAlignment(vertical, Axis.X, relationship);
                break;

            case Flush flush:
                ResolveFlush(flush);
                break;

            case AxisDistance axisDistance:
                Resolve(
                    PointSide(axisDistance.From, axisDistance.Axis, axisDistance.Distance),
                    PointSide(axisDistance.To, axisDistance.Axis),
                    relationship);
                break;

            case ParamValue paramValue when ParamSide(paramValue.Param) is { } side:
                Assign(side.Bases[0], paramValue.Value, [paramValue.Id]);
                break;

            case EqualParam equalParam
                when ParamSide(equalParam.A) is { } first && ParamSide(equalParam.B) is { } second:
                Resolve(first, second, relationship);
                break;

            case Centered centered:
                ResolveCentered(centered);
                break;

            default:
                // Anchored is pre-assignment only; anything else is not propagated here, and
                // DirectUpdater refuses a sketch that contains it.
                break;
        }
    }

    private void ResolveSegmentAlignment(SegmentRef segmentRef, Axis mustMatch, Relationship relationship)
    {
        if (_sketch.Find<Segment>(segmentRef.Segment) is not { } segment)
        {
            return;
        }

        Resolve(
            PointSide(new NodeRef(segment.Start), mustMatch),
            PointSide(new NodeRef(segment.End), mustMatch),
            relationship);
    }

    private void ResolveFlush(Flush flush)
    {
        Axis? normal = CommonNormalAxis(flush);
        if (normal is not { } axis)
        {
            return;
        }

        Side? first = EdgeSide(flush.A, axis);
        Side? second = EdgeSide(flush.B, axis);
        if (first is not null && second is not null)
        {
            Resolve(first, second, flush);
        }
    }

    /// <summary>The axis both edges of a flush hold constant, or null when they do not share one.</summary>
    internal Axis? CommonNormalAxis(Flush flush)
    {
        Axis? first = NormalAxisOf(flush.A);
        return first is { } axis && NormalAxisOf(flush.B) == axis ? axis : null;
    }

    private Axis? NormalAxisOf(EdgeRef edge)
    {
        (Point2 from, Point2 to) = CurrentEdge(edge);
        bool sameX = from.X == to.X;
        bool sameY = from.Y == to.Y;

        if (sameX == sameY)
        {
            return null;
        }

        return sameX ? Axis.X : Axis.Y;
    }

    private void ResolveCentered(Centered centered)
    {
        Side middle = PointSide(centered.Middle, centered.Axis);
        Side first = PointSide(centered.A, centered.Axis);
        Side second = PointSide(centered.B, centered.Axis);

        Length target = RelationshipChecker.Midpoint(first.Value, second.Value);
        if (middle.Value == target)
        {
            return;
        }

        bool middleFree = Adjustable(middle);
        if (middleFree && (Driving(first) || Driving(second) || !Driving(middle)))
        {
            Adjust(middle, target, [.. Via(first), .. Via(second)], centered.Id);
            return;
        }

        if (Adjustable(first) && Adjustable(second))
        {
            // The midpoint is what moved: both ends move by the same delta, which keeps their own
            // midpoint exactly where the middle now is.
            Length delta = middle.Value - target;
            ImmutableList<RelationshipId> via = Via(middle);
            Adjust(first, first.Value + delta, via, centered.Id);
            Adjust(second, second.Value + delta, via, centered.Id);
            return;
        }

        if (middleFree)
        {
            Adjust(middle, target, [.. Via(first), .. Via(second)], centered.Id);
            return;
        }

        ReportConflict(middle, first, centered);
    }

    /// <summary>
    /// Makes the two sides of a constraint agree, by moving whichever side is free to move.
    /// </summary>
    /// <remarks>
    /// A side is <em>adjustable</em> when none of the position scalars it is built on has been
    /// assigned yet, and <em>driving</em> when any scalar it reads — position or size — has. That
    /// pair is the anchor rule of design &#xA7;4.4: a resized box keeps its anchor and moves its
    /// far side, unless a relationship pins the far side, in which case the anchor moves instead;
    /// and if both are pinned, the resize is a contradiction naming both pins. When neither side
    /// is driving, the second side follows the first — which makes
    /// <c>AddRelationship(Coincident(p, q))</c> move <c>q</c> onto <c>p</c>, and
    /// <c>SetParameter</c> on an <see cref="AxisDistance"/> move its <em>to</em> point.
    /// </remarks>
    private void Resolve(Side first, Side second, Relationship through)
    {
        if (first.Value == second.Value)
        {
            return;
        }

        bool firstFree = Adjustable(first);
        bool secondFree = Adjustable(second);

        if (firstFree && secondFree)
        {
            if (Driving(second) && !Driving(first))
            {
                Adjust(first, second.Value, Via(second), through.Id);
            }
            else
            {
                Adjust(second, first.Value, Via(first), through.Id);
            }

            return;
        }

        if (firstFree)
        {
            Adjust(first, second.Value, Via(second), through.Id);
            return;
        }

        if (secondFree)
        {
            Adjust(second, first.Value, Via(first), through.Id);
            return;
        }

        ReportConflict(first, second, through);
    }

    private bool Adjustable(Side side)
        => side.Bases.Length > 0 && !side.Bases.Any(_assigned.ContainsKey);

    private bool Driving(Side side)
        => side.Bases.Any(_assigned.ContainsKey) || side.SizeDeps.Any(_assigned.ContainsKey);

    private ImmutableList<RelationshipId> Via(Side side)
    {
        List<RelationshipId> chain = [];
        foreach (ScalarKey key in side.Bases.Concat(side.SizeDeps))
        {
            if (_assigned.TryGetValue(key, out Assignment? assignment))
            {
                foreach (RelationshipId id in assignment.Via)
                {
                    if (!chain.Contains(id))
                    {
                        chain.Add(id);
                    }
                }
            }
        }

        return [.. chain];
    }

    private void Adjust(Side side, Length value, IEnumerable<RelationshipId> via, RelationshipId through)
    {
        List<RelationshipId> chain = [.. via];
        if (!chain.Contains(through))
        {
            chain.Add(through);
        }

        ImmutableList<RelationshipId> derivation = [.. chain];
        foreach (ScalarKey key in side.Bases)
        {
            Assign(key, value - side.Offset, derivation);
        }
    }

    private void Assign(ScalarKey key, Length value, ImmutableList<RelationshipId> via)
    {
        if (_conflict is not null)
        {
            return;
        }

        if (_assigned.TryGetValue(key, out Assignment? existing))
        {
            if (existing.Value != value)
            {
                _conflict = BuildConflict(
                    new Derivation(TargetOf(_sketch, key), existing.Value, existing.Via),
                    new Derivation(TargetOf(_sketch, key), value, via),
                    [.. existing.Via, .. via]);
            }

            return;
        }

        _assigned[key] = new Assignment(value, via);

        if (_byEntity.TryGetValue(key.Entity, out List<Relationship>? affected))
        {
            foreach (Relationship relationship in affected.OrderBy(candidate => candidate.Id))
            {
                Enqueue(relationship.Id);
            }
        }
    }

    private void ReportConflict(Side first, Side second, Relationship through)
    {
        _conflict ??= BuildConflict(
            new Derivation(first.Target, first.Value, Via(first)),
            new Derivation(second.Target, second.Value, Via(second)),
            [.. Via(first), .. Via(second), through.Id]);
    }

    private ConflictReport BuildConflict(
        Derivation first,
        Derivation second,
        IEnumerable<RelationshipId> involved)
    {
        List<RelationshipId> relationships = [];
        foreach (RelationshipId id in involved)
        {
            if (!relationships.Contains(id))
            {
                relationships.Add(id);
            }
        }

        relationships.Sort();

        List<EntityId> entities = [];
        foreach (RelationshipId id in relationships)
        {
            if (_sketch.Find(id) is not { } relationship)
            {
                continue;
            }

            foreach (EntityId entity in relationship.References)
            {
                if (!entities.Contains(entity))
                {
                    entities.Add(entity);
                }
            }
        }

        entities.Sort();

        return new ConflictReport(
            ConflictKind.Contradictory,
            [.. relationships],
            [.. entities],
            [first, second],
            $"{Describe(first.Target)} is {first.Value} by one route and {second.Value} by another; "
            + "these cannot both be true.");
    }

    private static string Describe(AssignmentTarget target) => target switch
    {
        ParamTarget param => param.Param switch
        {
            BoxWidthRef width => $"The width of {width.Box}",
            BoxHeightRef height => $"The height of {height.Box}",
            SegmentLengthRef length => $"The length of {length.Segment}",
            _ => "A size",
        },
        PointAxisTarget point => $"{(point.Axis == Axis.X ? "The X" : "The Y")} of {point.Point.Owner}",
        _ => "A value",
    };

    // -----------------------------------------------------------------------------------------
    // Sides: a value expressed as "base + offset", where the offset comes from sizes and the base
    // is the position scalar a constraint can move.
    // -----------------------------------------------------------------------------------------

    private Side PointSide(PointRef point, Axis axis) => PointSide(point, axis, Length.Zero);

    private Side PointSide(PointRef point, Axis axis, Length extraOffset)
    {
        ScalarKey baseKey = new(point.Owner, KindOf(axis));
        AssignmentTarget target = new PointAxisTarget(point, axis);

        switch (point)
        {
            case CornerRef corner:
            {
                Length offset = CornerOffset(corner.Box, corner.Corner).Component(axis) + extraOffset;
                return new Side([baseKey], offset, SizesOf(corner.Box), CurrentOf(baseKey) + offset, target);
            }

            case CenterRef centre:
            {
                Length offset = CenterOffset(centre.Box).Component(axis) + extraOffset;
                return new Side([baseKey], offset, SizesOf(centre.Box), CurrentOf(baseKey) + offset, target);
            }

            default:
                return new Side([baseKey], extraOffset, [], CurrentOf(baseKey) + extraOffset, target);
        }
    }

    private Side? ParamSide(ParamRef param)
    {
        ScalarKey key;
        switch (param)
        {
            case BoxWidthRef width:
                key = new ScalarKey(width.Box, ScalarKind.Width);
                break;

            case BoxHeightRef height:
                key = new ScalarKey(height.Box, ScalarKind.Height);
                break;

            default:
                // A segment's length is not one number the propagator can assign; DirectUpdater
                // refuses a sketch that makes one a ParamValue or EqualParam (see §10).
                return null;
        }

        return new Side([key], Length.Zero, [], CurrentOf(key), new ParamTarget(param));
    }

    private Side? EdgeSide(EdgeRef edge, Axis normalAxis)
    {
        switch (edge)
        {
            case BoxEdgeRef boxEdge:
            {
                (BoxCorner from, _) = Box.Ends(boxEdge.Edge);
                return PointSide(new CornerRef(boxEdge.Box, from), normalAxis);
            }

            case SegmentRef segmentRef when _sketch.Find<Segment>(segmentRef.Segment) is { } segment:
            {
                ScalarKey start = new(segment.Start, KindOf(normalAxis));
                ScalarKey end = new(segment.End, KindOf(normalAxis));
                return new Side(
                    [start, end],
                    Length.Zero,
                    [],
                    CurrentOf(start),
                    new PointAxisTarget(new NodeRef(segment.Start), normalAxis));
            }

            default:
                return null;
        }
    }

    private ImmutableArray<ScalarKey> SizesOf(EntityId box)
        => [new ScalarKey(box, ScalarKind.Width), new ScalarKey(box, ScalarKind.Height)];

    private Vector2 CornerOffset(EntityId boxId, BoxCorner corner)
    {
        if (_sketch.Find<Box>(boxId) is not { } box)
        {
            return Vector2.Zero;
        }

        Length width = CurrentOf(new ScalarKey(boxId, ScalarKind.Width));
        Length height = CurrentOf(new ScalarKey(boxId, ScalarKind.Height));

        Vector2 local = corner switch
        {
            BoxCorner.SouthWest => Vector2.Zero,
            BoxCorner.SouthEast => new Vector2(width, Length.Zero),
            BoxCorner.NorthEast => new Vector2(width, height),
            _ => new Vector2(Length.Zero, height),
        };

        return local.Rotate(box.Rotation);
    }

    private Vector2 CenterOffset(EntityId boxId)
    {
        if (_sketch.Find<Box>(boxId) is not { } box)
        {
            return Vector2.Zero;
        }

        Length width = CurrentOf(new ScalarKey(boxId, ScalarKind.Width));
        Length height = CurrentOf(new ScalarKey(boxId, ScalarKind.Height));

        return new Vector2(
            width.Divide(2, Rounding.HalfToEven),
            height.Divide(2, Rounding.HalfToEven)).Rotate(box.Rotation);
    }

    private (Point2 From, Point2 To) CurrentEdge(EdgeRef edge)
    {
        switch (edge)
        {
            case BoxEdgeRef boxEdge:
            {
                (BoxCorner from, BoxCorner to) = Box.Ends(boxEdge.Edge);
                return (CurrentCorner(boxEdge.Box, from), CurrentCorner(boxEdge.Box, to));
            }

            case SegmentRef segmentRef when _sketch.Find<Segment>(segmentRef.Segment) is { } segment:
                return (CurrentPosition(segment.Start), CurrentPosition(segment.End));

            default:
                return (Point2.Origin, Point2.Origin);
        }
    }

    private Point2 CurrentCorner(EntityId boxId, BoxCorner corner)
        => CurrentPosition(boxId) + CornerOffset(boxId, corner);

    private Point2 CurrentPosition(EntityId entity)
        => new(CurrentOf(new ScalarKey(entity, ScalarKind.X)), CurrentOf(new ScalarKey(entity, ScalarKind.Y)));

    private sealed record Side(
        ImmutableArray<ScalarKey> Bases,
        Length Offset,
        ImmutableArray<ScalarKey> SizeDeps,
        Length Value,
        AssignmentTarget Target);
}
