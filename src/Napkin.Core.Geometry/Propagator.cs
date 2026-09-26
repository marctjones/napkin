using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Which number of an entity an assignment is about.</summary>
internal enum ScalarKind
{
    /// <summary>A box's anchor X, or a node's X.</summary>
    X,

    /// <summary>A box's anchor Y, or a node's Y.</summary>
    Y,

    /// <summary>
    /// A box's anchor Z (docs/design/assembly-model.md &#xA7;3.1). A node has none: it is plan-plane
    /// construction geometry at Z = 0 (&#xA7;1.4), and no place a node owns fixes Z, so nothing ever
    /// asks for one.
    /// </summary>
    Z,

    /// <summary>A box's width, along its local X.</summary>
    Width,

    /// <summary>A box's height, along its local Y.</summary>
    Height,

    /// <summary>
    /// A box's depth, along its local Z (docs/design/assembly-model.md §1.2). Like the other two
    /// sizes, only <see cref="ParamValue"/> and <see cref="EqualParam"/> assign it; a feature's
    /// offset reads it on whichever world axis the box's orientation stands it along.
    /// </summary>
    Depth,

    /// <summary>A strut's <see cref="Strut.From"/> X (docs/design/assembly-model.md &#xA7;3a.5): each end is its own three scalars.</summary>
    FromX,

    /// <summary>A strut's <see cref="Strut.From"/> Y.</summary>
    FromY,

    /// <summary>A strut's <see cref="Strut.From"/> Z.</summary>
    FromZ,

    /// <summary>A strut's <see cref="Strut.To"/> X.</summary>
    ToX,

    /// <summary>A strut's <see cref="Strut.To"/> Y.</summary>
    ToY,

    /// <summary>A strut's <see cref="Strut.To"/> Z.</summary>
    ToZ,
}

/// <summary>One number the propagator can assign.</summary>
internal readonly record struct ScalarKey(EntityId Entity, ScalarKind Kind);

/// <summary>A value the propagator decided on, and the chain of relationships it came through.</summary>
/// <param name="Value">The value the scalar now has.</param>
/// <param name="Via">The relationships the value came through, in the order it came through them.</param>
/// <param name="Changed">
/// Whether this differs from what the sketch already said. An assignment that changed nothing —
/// a <see cref="ParamValue"/> restating the size a box already has, or an <see cref="Anchored"/>
/// holding an entity where it already is — still <em>pins</em> the scalar, but it is not a reason
/// for anything else to move and it does not belong in a conflict report.
/// </param>
internal sealed record Assignment(Length Value, ImmutableList<RelationshipId> Via, bool Changed);

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
/// <para>
/// <strong>Pins travel; nothing else drives.</strong> A scalar is <em>pinned</em> once it has
/// been assigned. A relationship that already holds passes its pin on — the side held by
/// something assigned assigns the other side to where it already is — so pinning reaches all the
/// way along a chain of parts rather than one relationship deep (issue #49). A relationship whose
/// two sides are both still undecided moves nothing at all: it waits. When the queue empties with
/// such a chain still unsatisfied, <see cref="RunPhase"/> gives that chain its one reference
/// scalar and runs the queue again. This is what makes the answer independent of relationship ids
/// and of which way round each relationship was written.
/// </para>
/// <para>
/// <strong>Three axes, one algorithm</strong> (docs/design/assembly-model.md &#xA7;3.1). A box has
/// six scalars — its anchor's X, Y and Z and its three sizes — and every coordinate a relationship
/// reads is one anchor component plus or minus one size, chosen by the feature's faces and turned
/// by the box's orientation (<see cref="FeatureOffset"/>). So a <see cref="Side"/> is still
/// "one base scalar plus an offset from sizes", whichever axis it is on and whichever size stands
/// along it, and nothing above is told how many axes there are. The two phases are unchanged:
/// sizes settle first, all three of them, and then every position along every axis is worked out
/// from final sizes.
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
    /// <param name="requestOwns">
    /// The scalars the user is editing directly. <see cref="Anchored"/> means "does not move or
    /// resize <em>in response to other entities</em>" (design &#xA7;3.2), so it pins an anchored
    /// entity against propagation but stands aside for what the request itself is setting: the
    /// size behind a dimension being typed, or the size and anchor corner behind a resize handle
    /// being dragged. A caller passes nothing to keep an anchored entity completely still, which
    /// is what <see cref="SetPosition"/> does — asking an anchored entity to move somewhere is
    /// exactly what <see cref="Anchored"/> refuses.
    /// </param>
    internal static PropagationResult Run(
        Sketch sketch,
        IReadOnlyDictionary<ScalarKey, Length> seeds,
        IEnumerable<Relationship> honoured,
        IReadOnlySet<ScalarKey>? requestOwns = null)
    {
        Propagator propagator = new(sketch);
        return propagator.Propagate(
            seeds,
            [.. honoured.OrderBy(relationship => relationship.Id)],
            requestOwns ?? ImmutableHashSet<ScalarKey>.Empty);
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
            ScalarKind.Z => box.Anchor.Z,
            ScalarKind.Width => box.Width,
            ScalarKind.Height => box.Height,
            ScalarKind.Depth => box.Depth,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key.Kind, "Not a scalar kind."),
        },
        Node node => key.Kind switch
        {
            ScalarKind.X => node.Position.X,
            ScalarKind.Y => node.Position.Y,
            _ => throw new ArgumentOutOfRangeException(nameof(key), key.Kind, "A node has only an X and a Y."),
        },
        Strut strut => key.Kind switch
        {
            ScalarKind.Height => strut.Height,
            ScalarKind.Depth => strut.Depth,
            _ when EndOf(key.Kind) is (var end, var axis) => strut.End(end).Component(axis),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key.Kind, "A strut has two ends and a cross-section."),
        },
        _ => Length.Zero,
    };

    /// <summary>What an assignment to a scalar is about, for a conflict report.</summary>
    internal static AssignmentTarget TargetOf(Sketch sketch, ScalarKey key) => key.Kind switch
    {
        _ when EndOf(key.Kind) is (var end, var axis) => new PointAxisTarget(new StrutEndRef(key.Entity, end), axis),
        ScalarKind.Height when sketch.Find(key.Entity) is Strut => new ParamTarget(new StrutHeightRef(key.Entity)),
        ScalarKind.Depth when sketch.Find(key.Entity) is Strut => new ParamTarget(new StrutDepthRef(key.Entity)),
        ScalarKind.Width => new ParamTarget(new BoxWidthRef(key.Entity)),
        ScalarKind.Height => new ParamTarget(new BoxHeightRef(key.Entity)),
        ScalarKind.Depth => new ParamTarget(new BoxDepthRef(key.Entity)),
        ScalarKind.X or ScalarKind.Y => new PointAxisTarget(
            sketch.Find(key.Entity) is Box
                ? new FeatureRef(key.Entity, BoxFeature.LocalUpright(BoxCorner.SouthWest))
                : new NodeRef(key.Entity),
            key.Kind == ScalarKind.X ? Axis.X : Axis.Y),

        // The anchor is the blank's south-west-bottom vertex; its local upright, which names the
        // anchor's X and Y above, fixes Z only when the box is tipped, so Z names the vertex.
        ScalarKind.Z => new PointAxisTarget(
            new FeatureRef(key.Entity, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Bottom)),
            Axis.Z),
        _ => throw new ArgumentOutOfRangeException(nameof(key), key.Kind, "Not a scalar kind."),
    };

    /// <summary>The scalar of one strut end along one world axis.</summary>
    internal static ScalarKind EndKind(StrutEnd end, Axis axis) => (end, axis) switch
    {
        (StrutEnd.From, Axis.X) => ScalarKind.FromX,
        (StrutEnd.From, Axis.Y) => ScalarKind.FromY,
        (StrutEnd.From, _) => ScalarKind.FromZ,
        (_, Axis.X) => ScalarKind.ToX,
        (_, Axis.Y) => ScalarKind.ToY,
        _ => ScalarKind.ToZ,
    };

    /// <summary>The strut end and axis a scalar is, or <see langword="null"/> for a scalar that is not a strut end's.</summary>
    internal static (StrutEnd End, Axis Axis)? EndOf(ScalarKind kind) => kind switch
    {
        ScalarKind.FromX => (StrutEnd.From, Axis.X),
        ScalarKind.FromY => (StrutEnd.From, Axis.Y),
        ScalarKind.FromZ => (StrutEnd.From, Axis.Z),
        ScalarKind.ToX => (StrutEnd.To, Axis.X),
        ScalarKind.ToY => (StrutEnd.To, Axis.Y),
        ScalarKind.ToZ => (StrutEnd.To, Axis.Z),
        _ => null,
    };

    /// <summary>A strut's eight scalars: its two ends and its cross-section.</summary>
    internal static IEnumerable<ScalarKind> StrutKinds =>
        [ScalarKind.FromX, ScalarKind.FromY, ScalarKind.FromZ, ScalarKind.ToX, ScalarKind.ToY, ScalarKind.ToZ, ScalarKind.Height, ScalarKind.Depth];

    /// <summary>The position scalar along a world axis.</summary>
    internal static ScalarKind KindOf(Axis axis) => axis switch
    {
        Axis.X => ScalarKind.X,
        Axis.Y => ScalarKind.Y,
        Axis.Z => ScalarKind.Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    private PropagationResult Propagate(
        IReadOnlyDictionary<ScalarKey, Length> seeds,
        List<Relationship> honoured,
        IReadOnlySet<ScalarKey> requestOwns)
    {
        // An anchored entity's scalars are pre-assigned to their current values with an Anchored
        // derivation, so any attempt to move or resize it in response to another entity is a
        // contradiction with a nameable cause. The one size the request is itself editing is
        // exempt: that is not a response to another entity.
        foreach (Relationship relationship in honoured)
        {
            if (relationship is not Anchored anchored)
            {
                continue;
            }

            // A box has six scalars to hold (assembly-model §2.3); a node has its X and Y; a strut its
            // two ends and its cross-section, eight (§3a.5).
            Entity? held = _sketch.Find(anchored.Entity);
            IEnumerable<ScalarKind> kinds = held switch
            {
                Strut => StrutKinds,
                Box => [ScalarKind.X, ScalarKind.Y, ScalarKind.Z, ScalarKind.Width, ScalarKind.Height, ScalarKind.Depth],
                _ => [ScalarKind.X, ScalarKind.Y],
            };

            foreach (ScalarKind kind in kinds)
            {
                ScalarKey key = new(anchored.Entity, kind);
                if (requestOwns.Contains(key))
                {
                    continue;
                }

                Assign(key, CurrentOf(key), [anchored.Id]);
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

        Drain();

        // Whatever is still unsatisfied is a chain no pin reached: every relationship in it saw
        // two undecided sides and waited. Such a chain has one degree of freedom left along the
        // axis — a translation — and §4.4's anchor rule says who gives it up: the part closest to
        // what the request changed keeps its anchor, and the rest of the chain follows. Assigning
        // that one scalar is enough; the queue carries it outward. Repeat until nothing is left,
        // because deciding one chain can leave another one to decide.
        while (_conflict is null)
        {
            List<Relationship> unsatisfied = [.. _phase.Where(IsUnsatisfied)];
            if (unsatisfied.Count == 0)
            {
                return;
            }

            int assignedBefore = _assigned.Count;

            if (ReferenceScalarOf(unsatisfied) is { } reference)
            {
                Assign(reference, CurrentOf(reference), ImmutableList<RelationshipId>.Empty);
            }
            else
            {
                // Only Centered relationships are left. They carry their own rule for which of
                // the three points moves (§3.2's half-unit midpoint), so they need no reference.
                foreach (Relationship relationship in unsatisfied)
                {
                    Enqueue(relationship.Id);
                }
            }

            Drain();

            if (_assigned.Count == assignedBefore)
            {
                // No progress is possible. This cannot happen for a sketch that held together
                // before the request — every unsatisfied rigid relationship has an unassigned
                // base to use as a reference — and stopping beats spinning. DirectUpdater's
                // post-write check reports it as the bug it would be.
                return;
            }
        }
    }

    private void Drain()
    {
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

    /// <summary>Whether this relationship does not hold for the values worked out so far.</summary>
    private bool IsUnsatisfied(Relationship relationship) => relationship switch
    {
        // A ParamValue is an assignment rather than a coupling: the queue has already made it
        // true or reported why it cannot be.
        ParamValue paramValue => ParamSide(paramValue.Param) is { } side && side.Value != paramValue.Value,

        Centered centered => !RelationshipChecker.IsCentred(
            PointSide(centered.Middle, centered.Axis).Value,
            PointSide(centered.A, centered.Axis).Value,
            PointSide(centered.B, centered.Axis).Value),

        // A joint says nothing about where anything is (joinery note §4.3).
        Joint => false,

        _ => PairsOf(relationship).Any(pair => pair.First.Value != pair.Second.Value),
    };

    /// <summary>
    /// The one scalar of an undecided chain that stays where it is, so that everything else in the
    /// chain can be worked out from it.
    /// </summary>
    /// <remarks>
    /// The choice is design &#xA7;4.4's anchor rule, generalised from one box to a chain: the part
    /// whose size the request changed <em>most directly</em> keeps its anchor, and the parts that
    /// were resized only because an <see cref="EqualParam"/> passed the change on give way to it;
    /// a part whose size did not change at all gives way to both. "Most directly" is the number of
    /// relationships the size change came through, which does not depend on ids or on argument
    /// order. Only when that is a tie does the order of the sketch decide, and then it decides
    /// between candidates the request cannot tell apart: the first side of the lowest-numbered
    /// unsatisfied relationship stays, which is what makes
    /// <c>AddRelationship(Coincident(p, q))</c> move <c>q</c> onto <c>p</c> and an
    /// <see cref="AxisDistance"/>'s <em>to</em> point follow its <em>from</em> point.
    /// </remarks>
    private ScalarKey? ReferenceScalarOf(List<Relationship> unsatisfied)
    {
        ScalarKey? best = null;
        int bestRank = int.MaxValue;

        foreach (Relationship relationship in unsatisfied)
        {
            if (relationship is ParamValue or Centered)
            {
                continue;
            }

            foreach ((Side first, Side second) in PairsOf(relationship))
            {
                if (first.Value == second.Value)
                {
                    continue;
                }

                foreach (Side side in new[] { first, second })
                {
                    foreach (ScalarKey key in side.Bases)
                    {
                        if (_assigned.ContainsKey(key))
                        {
                            continue;
                        }

                        int distance = DistanceFromTheRequest(key);
                        if (best is null || distance < bestRank)
                        {
                            bestRank = distance;
                            best = key;
                        }
                    }
                }
            }
        }

        return best;
    }

    /// <summary>
    /// How many relationships a change in this entity's size came through: nothing for a size the
    /// request set itself, one more for each <see cref="EqualParam"/> that passed it on, and
    /// <see cref="int.MaxValue"/> when neither of its sizes changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// All three sizes count, not the one along the axis in hand: a box turned by a quarter turn
    /// has its width along Y, and a box tipped on its east face has its depth along X
    /// (docs/design/assembly-model.md &#xA7;3.1 — the rule is per scalar and does not care which
    /// local size sits on which world axis).
    /// </para>
    /// <para>
    /// The depth counts since &#xA7;10 step 4, which is what gives the anchor rule its meaning along
    /// Z: four legs flush under a top with nothing anchored, one leg's depth typed and the others
    /// following through <see cref="EqualParam"/>, is a chain along Z whose sides are all free, and
    /// the typed leg keeping its anchor is what makes the top rise rather than the legs sink
    /// (&#xA7;9.1 case 10). A depth that changed on a box lying as drawn moves only Z coordinates,
    /// so it never unsettles a chain along X or Y, and the plan's tie-breaks are the ones they were.
    /// </para>
    /// </remarks>
    private int DistanceFromTheRequest(ScalarKey key)
    {
        int best = int.MaxValue;

        foreach (ScalarKind kind in new[] { ScalarKind.Width, ScalarKind.Height, ScalarKind.Depth })
        {
            if (_assigned.TryGetValue(new ScalarKey(key.Entity, kind), out Assignment? assignment)
                && assignment.Changed
                && assignment.Via.Count < best)
            {
                best = assignment.Via.Count;
            }
        }

        return best;
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
            case ParamValue paramValue when ParamSide(paramValue.Param) is { } side:
                Assign(side.Bases[0], paramValue.Value, [paramValue.Id]);
                break;

            case Centered centered:
                ResolveCentered(centered);
                break;

            default:
                // Anchored is pre-assignment only, and anything PairsOf does not know is not
                // propagated here: DirectUpdater refuses a sketch that contains one.
                foreach ((Side first, Side second) in PairsOf(relationship))
                {
                    Resolve(first, second, relationship);
                }

                break;
        }
    }

    /// <summary>
    /// The pairs of values a relationship holds equal, one per axis it speaks about. Every kind
    /// but <see cref="ParamValue"/> (an assignment rather than a coupling) and
    /// <see cref="Centered"/> (a midpoint, which is not a pair) reduces to these, so the queue,
    /// the "does this hold?" question and the search for a reference all read the same model.
    /// </summary>
    private IEnumerable<(Side First, Side Second)> PairsOf(Relationship relationship)
    {
        switch (relationship)
        {
            case Coincident coincident:
                // Equal on every axis both places fix (docs/design/assembly-model.md §2.1): for
                // two plan uprights, or a node and an upright, that is X and Y, as it always was.
                foreach (Axis axis in AxesOf(coincident.A).Intersect(AxesOf(coincident.B)).Order())
                {
                    yield return (PointSide(coincident.A, axis), PointSide(coincident.B, axis));
                }

                break;

            case Horizontal { Edge: SegmentRef horizontal }:
                if (SegmentAlignment(horizontal, Axis.Y) is { } alongY)
                {
                    yield return alongY;
                }

                break;

            case Vertical { Edge: SegmentRef vertical }:
                if (SegmentAlignment(vertical, Axis.X) is { } alongX)
                {
                    yield return alongX;
                }

                break;

            case Flush flush:
                if (CommonNormalAxis(flush) is { } normal)
                {
                    // The one coupling of a strut's two ends (angled-parts §3.2): its face is square to
                    // the axis only while both ends agree on it, so the flush holds them equal there.
                    // Moving one end alone off the axis would end the lean being one-way, and is a
                    // contradiction naming this flush. Yielded first, and resolved before the face
                    // pair is built: the face's value is read from the From end, so a To end that moved
                    // must have carried From with it before the face is compared with its partner.
                    foreach (PlaceRef place in new[] { flush.A, flush.B })
                    {
                        if (place is StrutFaceRef face)
                        {
                            yield return (
                                PointSide(new StrutEndRef(face.Strut, StrutEnd.From), normal),
                                PointSide(new StrutEndRef(face.Strut, StrutEnd.To), normal));
                        }
                    }

                    if (EdgeSide(flush.A, normal) is { } firstEdge && EdgeSide(flush.B, normal) is { } secondEdge)
                    {
                        yield return (firstEdge, secondEdge);
                    }
                }

                break;

            case AxisDistance axisDistance:
                yield return (
                    PointSide(axisDistance.From, axisDistance.Axis, axisDistance.Distance),
                    PointSide(axisDistance.To, axisDistance.Axis));
                break;

            case EqualParam equalParam
                when ParamSide(equalParam.A) is { } first && ParamSide(equalParam.B) is { } second:
                yield return (first, second);
                break;

            default:
                break;
        }
    }

    private (Side First, Side Second)? SegmentAlignment(SegmentRef segmentRef, Axis mustMatch)
        => _sketch.Find<Segment>(segmentRef.Segment) is { } segment
            ? (PointSide(new NodeRef(segment.Start), mustMatch), PointSide(new NodeRef(segment.End), mustMatch))
            : null;

    /// <summary>The one axis both places of a flush fix, or null when they do not share exactly one.</summary>
    internal Axis? CommonNormalAxis(Flush flush)
        => AxesOf(flush.A) is [var first] && AxesOf(flush.B) is [var second] && first == second ? first : null;

    /// <summary>
    /// The world axes a place fixes, for the values worked out so far. Which axes a box's feature
    /// fixes depends only on its orientation, never on a size or a position; a segment's depends on
    /// where its nodes are now.
    /// </summary>
    private ImmutableArray<Axis> AxesOf(PlaceRef place)
    {
        switch (place)
        {
            case NodeRef:
                return [Axis.X, Axis.Y];

            case StrutEndRef:
                return [Axis.X, Axis.Y, Axis.Z];

            // Read from the stored strut, as a feature's axes are from the stored orientation; see CurrentFace.
            case StrutFaceRef face when _sketch.Find<Strut>(face.Strut) is not null:
                return CurrentFace(face) is { } plane ? [plane.Axis] : [];

            // Read from the stored sketch: only the axes are wanted, and they do not move with the
            // values being worked out. This is also the one reading off the quarter turns, which the
            // solver's repair pass will reach.
            case CenterRef or FeatureRef when _sketch.Find<Box>(place.Owner) is not null:
                return _sketch.PlaceOf(place).Axes;

            case SegmentRef segmentRef when _sketch.Find<Segment>(segmentRef.Segment) is { } segment:
            {
                Point2 from = CurrentPosition(segment.Start);
                Point2 to = CurrentPosition(segment.End);
                return (from.X == to.X, from.Y == to.Y) switch
                {
                    (true, false) => [Axis.X],
                    (false, true) => [Axis.Y],
                    _ => [],
                };
            }

            default:
                return [];
        }
    }

    private void ResolveCentered(Centered centered)
    {
        Side middle = PointSide(centered.Middle, centered.Axis);
        Side first = PointSide(centered.A, centered.Axis);
        Side second = PointSide(centered.B, centered.Axis);

        // Already centred to within the half unit design §3.2 allows on an odd span: leave it
        // alone. Insisting on the half-to-even midpoint here would "correct" a middle that is
        // perfectly good, and would undo an odd-unit translation on the next unrelated request.
        // Nothing moves, but the pin travels as it does in Resolve: two of the three points
        // decided decide the third, and it stays exactly where it is.
        if (RelationshipChecker.IsCentred(middle.Value, first.Value, second.Value))
        {
            PassThePinOn(centered, middle, first, second);
            return;
        }

        Length target = RelationshipChecker.Midpoint(first.Value, second.Value);
        bool middleFree = !Pinned(middle);
        if (middleFree && (Driving(first) || Driving(second) || !Driving(middle)))
        {
            Adjust(middle, target, [.. Via(first), .. Via(second)], centered.Id);
            return;
        }

        if (!Pinned(first) && !Pinned(second))
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

        // Neither the middle nor both ends can move. Name the end that is actually blocked, so the
        // report carries whatever is holding it — an Anchored on that end, say — rather than the
        // free end, which is not why this failed (Fable review of #35, finding 7).
        ReportConflict(middle, !Pinned(first) ? second : first, centered);
    }

    /// <summary>
    /// Makes the two sides of a constraint agree, by moving whichever side is free to move.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A side is <em>pinned</em> when a position scalar it is built on has been assigned, and
    /// <em>free</em> when none has. A pinned side does not move; a free side follows it. If both
    /// are pinned and they disagree, the request is a contradiction and the report names both
    /// pins. This is the anchor rule of design &#xA7;4.4: a resized box keeps its anchor and moves
    /// its far side, unless something pins the far side, in which case the anchor moves instead.
    /// </para>
    /// <para>
    /// Two things make that rule reach along a chain rather than one relationship deep (#49). A
    /// relationship that <em>already holds</em> still passes its pin on, so being held by an
    /// anchor two parts away counts. And a relationship with two free sides moves nothing: with no
    /// pin on either side there is no reason to prefer one, and guessing is what made the answer
    /// depend on ids and on argument order. Such a chain is decided by
    /// <see cref="ReferenceScalarOf"/> once the queue has run.
    /// </para>
    /// </remarks>
    private void Resolve(Side first, Side second, Relationship through)
    {
        bool firstPinned = Pinned(first);
        bool secondPinned = Pinned(second);

        if (first.Value == second.Value)
        {
            PassThePinOn(first, second, firstPinned, secondPinned, through);
            return;
        }

        if (firstPinned && secondPinned)
        {
            ReportConflict(first, second, through);
            return;
        }

        if (firstPinned)
        {
            Adjust(second, first.Value, Via(first), through.Id);
            return;
        }

        if (secondPinned)
        {
            Adjust(first, second.Value, Via(second), through.Id);
        }

        // Neither side is decided, so neither is a reason for the other to move: this
        // relationship waits for a pin to reach it, or for its chain to be given a reference.
    }

    /// <summary>
    /// A relationship that holds moves nothing, but it still carries the pin: the side held by
    /// something already assigned holds the other side exactly where it is. Without this, pinning
    /// stops at the first relationship and a chain of three parts cannot be solved (#49).
    /// </summary>
    private void PassThePinOn(Side first, Side second, bool firstPinned, bool secondPinned, Relationship through)
    {
        if (firstPinned && !secondPinned)
        {
            Adjust(second, second.Value, Via(first), through.Id);
        }
        else if (secondPinned && !firstPinned)
        {
            Adjust(first, first.Value, Via(second), through.Id);
        }
    }

    /// <summary>The same, for the three points of a <see cref="Centered"/> that already holds.</summary>
    private void PassThePinOn(Centered centered, Side middle, Side first, Side second)
    {
        Side[] sides = [middle, first, second];
        Side[] free = [.. sides.Where(side => !Pinned(side))];

        // Two of the three decided decide the third; one decided leaves a degree of freedom, and
        // the point stays free.
        if (free.Length != 1)
        {
            return;
        }

        List<RelationshipId> via = [];
        foreach (Side side in sides.Where(Pinned))
        {
            via.AddRange(Via(side));
        }

        Adjust(free[0], free[0].Value, via, centered.Id);
    }

    /// <summary>
    /// Whether this side is held: one of the position scalars it is built on has been assigned.
    /// Pinning is about being assigned at all, not about having changed — an
    /// <see cref="Anchored"/> entity is held where it already is.
    /// </summary>
    private bool Pinned(Side side) => side.Bases.Any(_assigned.ContainsKey);

    /// <summary>
    /// Whether this side is a <em>reason</em> for the other one to move: something it reads has
    /// actually changed. An assignment that restated a value the sketch already had — a
    /// <see cref="ParamValue"/> on a size nobody edited — pins its scalar but drives nothing, so
    /// it must not decide which of two otherwise-free sides follows the other.
    /// </summary>
    private bool Driving(Side side)
        => side.Bases.Any(HasChanged) || side.SizeDeps.Any(HasChanged);

    private bool HasChanged(ScalarKey key)
        => _assigned.TryGetValue(key, out Assignment? assignment) && assignment.Changed;

    /// <summary>
    /// How this side's value was arrived at. A position scalar's derivation always counts, so an
    /// <see cref="Anchored"/> that is holding something still is named in a conflict; a size
    /// scalar's counts only when the size changed, so an unrelated dimension on a neighbour is
    /// never blamed for a move.
    /// </summary>
    private ImmutableList<RelationshipId> Via(Side side)
    {
        List<RelationshipId> chain = [];

        foreach (ScalarKey key in side.Bases)
        {
            AddVia(key, chain);
        }

        foreach (ScalarKey key in side.SizeDeps)
        {
            if (HasChanged(key))
            {
                AddVia(key, chain);
            }
        }

        return [.. chain];
    }

    private void AddVia(ScalarKey key, List<RelationshipId> chain)
    {
        if (!_assigned.TryGetValue(key, out Assignment? assignment))
        {
            return;
        }

        foreach (RelationshipId id in assignment.Via)
        {
            if (!chain.Contains(id))
            {
                chain.Add(id);
            }
        }
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

        _assigned[key] = new Assignment(value, via, value != StoredValueOf(_sketch, key));

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
            BoxDepthRef depth => $"The depth of {depth.Box}",
            StrutHeightRef height => $"The height of {height.Strut}",
            StrutDepthRef depth => $"The depth of {depth.Strut}",
            SegmentLengthRef length => $"The length of {length.Segment}",
            _ => "A size",
        },
        PointAxisTarget { Point: StrutEndRef end } point => $"The {point.Axis} of {end.Strut}'s {(end.End == StrutEnd.From ? "from" : "to")} end",
        PointAxisTarget point => $"The {point.Axis} of {point.Point.Owner}",
        _ => "A value",
    };

    // -----------------------------------------------------------------------------------------
    // Sides: a value expressed as "base + offset", where the offset comes from sizes and the base
    // is the position scalar a constraint can move.
    // -----------------------------------------------------------------------------------------

    private Side PointSide(PlaceRef point, Axis axis) => PointSide(point, axis, Length.Zero);

    private Side PointSide(PlaceRef point, Axis axis, Length extraOffset)
    {
        ScalarKey baseKey = new(point.Owner, KindOf(axis));
        AssignmentTarget target = new PointAxisTarget(point, axis);

        switch (point)
        {
            case FeatureRef feature:
            {
                Length offset = FeatureOffset(feature.Box, feature.Feature).Component(axis) + extraOffset;
                return new Side([baseKey], offset, SizesOf(feature.Box), CurrentOf(baseKey) + offset, target);
            }

            case CenterRef centre:
            {
                Length offset = CenterOffset(centre.Box).Component(axis) + extraOffset;
                return new Side([baseKey], offset, SizesOf(centre.Box), CurrentOf(baseKey) + offset, target);
            }

            // The per-end rule (assembly-model §3a.5): a relationship on an end reaches that end's
            // scalar and nothing else, so the other end keeps its value unless something moves it.
            case StrutEndRef end:
            {
                ScalarKey endKey = new(end.Strut, EndKind(end.End, axis));
                return new Side([endKey], extraOffset, [], CurrentOf(endKey) + extraOffset, target);
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

            case BoxDepthRef depth:
                key = new ScalarKey(depth.Box, ScalarKind.Depth);
                break;

            case StrutHeightRef strutHeight:
                key = new ScalarKey(strutHeight.Strut, ScalarKind.Height);
                break;

            case StrutDepthRef strutDepth:
                key = new ScalarKey(strutDepth.Strut, ScalarKind.Depth);
                break;

            default:
                // A segment's length is not one number the propagator can assign; DirectUpdater
                // refuses a sketch that makes one a ParamValue or EqualParam (see §10).
                return null;
        }

        return new Side([key], Length.Zero, [], CurrentOf(key), new ParamTarget(param));
    }

    private Side? EdgeSide(PlaceRef edge, Axis normalAxis)
    {
        switch (edge)
        {
            case FeatureRef feature:
                return PointSide(feature, normalAxis);

            // Both ends' coordinate on the face's axis, as a segment's flush is both its nodes': the
            // face is the centreline there plus half the size across it (angled-parts §3.2).
            case StrutFaceRef face when CurrentFace(face) is { } plane && plane.Axis == normalAxis:
            {
                ScalarKey from = new(face.Strut, EndKind(StrutEnd.From, normalAxis));
                ScalarKey to = new(face.Strut, EndKind(StrutEnd.To, normalAxis));
                return new Side(
                    [from, to],
                    plane.Offset,
                    [plane.Size],
                    CurrentOf(from) + plane.Offset,
                    new PointAxisTarget(face, normalAxis));
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

    // Every size a feature's offset can read: under a tip, any of the three lands on any world axis.
    private static ImmutableArray<ScalarKey> SizesOf(EntityId box)
        => [new ScalarKey(box, ScalarKind.Width), new ScalarKey(box, ScalarKind.Height), new ScalarKey(box, ScalarKind.Depth)];

    /// <summary>
    /// Where a feature is from the box's anchor, in the world, for the sizes worked out so far: one
    /// local point on every face of the feature — the far end of a local axis for East, North and
    /// Top — turned by the box's orientation (docs/design/assembly-model.md &#xA7;2.1). A side reads
    /// only the component along the axis it is about, which the feature fixes; the others are the
    /// feature's own business.
    /// </summary>
    private Vector3 FeatureOffset(EntityId boxId, BoxFeature feature)
    {
        if (_sketch.Find<Box>(boxId) is not { } box)
        {
            return Vector3.Zero;
        }

        Vector3 local = Vector3.Zero;
        foreach (BoxFace face in feature.Faces)
        {
            local = face switch
            {
                BoxFace.East => local.WithComponent(Axis.X, CurrentOf(new ScalarKey(boxId, ScalarKind.Width))),
                BoxFace.North => local.WithComponent(Axis.Y, CurrentOf(new ScalarKey(boxId, ScalarKind.Height))),
                BoxFace.Top => local.WithComponent(Axis.Z, CurrentOf(new ScalarKey(boxId, ScalarKind.Depth))),
                _ => local,
            };
        }

        return box.Orientation.Apply(local);
    }

    private Vector3 CenterOffset(EntityId boxId)
    {
        if (_sketch.Find<Box>(boxId) is not { } box)
        {
            return Vector3.Zero;
        }

        Length width = CurrentOf(new ScalarKey(boxId, ScalarKind.Width));
        Length height = CurrentOf(new ScalarKey(boxId, ScalarKind.Height));
        Length depth = CurrentOf(new ScalarKey(boxId, ScalarKind.Depth));

        return box.Orientation.Apply(new Vector3(
            width.Divide(2, Rounding.HalfToEven),
            height.Divide(2, Rounding.HalfToEven),
            depth.Divide(2, Rounding.HalfToEven)));
    }

    /// <summary>
    /// A strut face's axis, its offset from the centreline and the size that offset reads;
    /// <see langword="null"/> when it is square to nothing or the size across it is odd.
    /// </summary>
    /// <remarks>
    /// Which axis the face is square to is read from the stored strut, the way a box feature's axes
    /// are read from its stored orientation, and not from the ends worked out so far: a request that
    /// seeds one end off the axis would otherwise turn the face into nothing before the flush's
    /// coupling could either carry the other end along or report the contradiction (angled-parts §3.2).
    /// The size is read as worked out, so a typed cross-section moves the face.
    /// </remarks>
    private (Axis Axis, Length Offset, ScalarKey Size)? CurrentFace(StrutFaceRef face)
    {
        if (_sketch.Find<Strut>(face.Strut) is not { } strut
            || Strut.FaceNormal(strut.Direction, strut.Reference, face.Face) is not (var axis, var sign))
        {
            return null;
        }

        ScalarKey size = new(face.Strut, face.Face is StrutFace.South or StrutFace.North ? ScalarKind.Height : ScalarKind.Depth);
        return CurrentOf(size).TryDivideExact(2, out Length half) ? (axis, sign * half, size) : null;
    }

    private Point2 CurrentPosition(EntityId entity)
        => new(CurrentOf(new ScalarKey(entity, ScalarKind.X)), CurrentOf(new ScalarKey(entity, ScalarKind.Y)));

    private sealed record Side(
        ImmutableArray<ScalarKey> Bases,
        Length Offset,
        ImmutableArray<ScalarKey> SizeDeps,
        Length Value,
        AssignmentTarget Target);
}
