using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// The first implementation of <see cref="IGeometryUpdater"/>: exact, rectilinear, and no
/// guessing.
/// </summary>
/// <remarks>
/// <para>
/// A thin wrapper around <see cref="Propagator"/> (design &#xA7;4.4, "two components"). It
/// validates the request, checks the rectilinear precondition, hands the propagator the
/// relationships to honour, and writes the result into a new sketch.
/// </para>
/// <para>
/// It never rounds an on-grid value except the <see cref="Centered"/> midpoint of an odd span; it
/// never rotates by trigonometry; it never returns <see cref="UnderConstrained"/>, because it does
/// no degree-of-freedom analysis; and it never guesses. Anything outside the rectilinear set is
/// <see cref="Rejected"/> with the kind named, so the canvas can say "napkin can't hold that
/// relationship yet".
/// </para>
/// </remarks>
public sealed class DirectUpdater : IGeometryUpdater
{
    /// <summary>The updater. It has no state, so one instance serves everyone.</summary>
    public static readonly DirectUpdater Instance = new();

    private static readonly IReadOnlyDictionary<ScalarKey, Length> NoSeeds
        = new Dictionary<ScalarKey, Length>();

    /// <inheritdoc/>
    public ImmutableHashSet<Type> SupportedRelationships { get; } =
    [
        typeof(Anchored),
        typeof(Coincident),
        typeof(Horizontal),
        typeof(Vertical),
        typeof(Flush),
        typeof(AxisDistance),
        typeof(ParamValue),
        typeof(EqualParam),
        typeof(Centered),
    ];

    /// <inheritdoc/>
    public UpdateResult Apply(Sketch sketch, Request request)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(request);

        return request switch
        {
            // Structural requests move nothing, so they are allowed on any sketch — including one
            // holding a relationship kind this updater cannot propagate, which is the only way to
            // get such a relationship out again.
            AddEntity add => ApplyAddEntity(sketch, add),
            RemoveEntity remove => ApplyRemoveEntity(sketch, remove),
            RemoveRelationship remove => ApplyRemoveRelationship(sketch, remove),
            SetLayer setLayer => ApplySetLayer(sketch, setLayer),
            SetName setName => ApplySetName(sketch, setName),
            SetPart setPart => ApplySetPart(sketch, setPart),

            // A cut is in the blank's local frame and moves with it, so setting or removing one
            // moves no geometry and disturbs no relationship: structural, like a rename
            // (docs/design/shaped-parts-model.md §2.2).
            SetCut setCut => ApplySetCut(sketch, setCut),
            RemoveCut removeCut => ApplyRemoveCut(sketch, removeCut),

            // Geometry requests need the rectilinear precondition first.
            AddRelationship add => ApplyAddRelationship(sketch, add),
            SetParameter setParameter => ApplySetParameter(sketch, setParameter),
            SetPosition setPosition => ApplySetPosition(sketch, setPosition),
            SetRotation setRotation => ApplySetRotation(sketch, setRotation),
            Drag drag => ApplyDrag(sketch, drag),
            DragEdge dragEdge => ApplyDragEdge(sketch, dragEdge),

            Batch batch => ApplyBatch(sketch, batch),

            // Request's constructor is private protected, so nothing outside this assembly can
            // add a kind; this arm exists so that adding one here and forgetting to handle it is
            // a result rather than a crash.
            _ => new Rejected(RejectionReason.UnsupportedRequest),
        };
    }

    // ---------------------------------------------------------------------------------------
    // Structural requests
    // ---------------------------------------------------------------------------------------

    private static UpdateResult ApplyAddEntity(Sketch sketch, AddEntity request)
    {
        Entity entity = request.Entity;

        if (sketch.Entities.ContainsKey(entity.Id))
        {
            return new Rejected(RejectionReason.DuplicateEntity);
        }

        if (!sketch.Layers.Any(layer => layer.Id == entity.Layer))
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        if (entity is Box box)
        {
            if (box.Width <= Length.Zero || box.Height <= Length.Zero)
            {
                return new Rejected(RejectionReason.NonPositiveSize);
            }

            if (!box.Rotation.IsRightAngleMultiple)
            {
                return new Rejected(RejectionReason.RotationNotSupported);
            }

            // A box arrives with its cuts already on it, so it is validated here rather than by a
            // later SetCut (docs/design/shaped-parts-model.md §2.2).
            if (CutsRefuse(box) is { } refusal)
            {
                return refusal;
            }
        }

        if (entity is Segment segment
            && (sketch.Find<Node>(segment.Start) is null || sketch.Find<Node>(segment.End) is null))
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        if (entity is Dimension dimension)
        {
            if (!MeasurandResolves(sketch, dimension.Measures))
            {
                return new Rejected(RejectionReason.DanglingReference);
            }

            if (dimension.Drives is { } driving && !sketch.Relationships.ContainsKey(driving))
            {
                return new Rejected(RejectionReason.UnknownRelationship);
            }
        }

        return new Solved(sketch.WithEntity(entity), ChangeSet.Empty with { Added = [entity.Id] });
    }

    private static UpdateResult ApplyRemoveEntity(Sketch sketch, RemoveEntity request)
    {
        if (sketch.Find(request.Id) is null)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        // The cascade: segments that used the entity as an endpoint go too, and then whatever
        // referenced those; dimensions whose measurand references anything removed go with it.
        HashSet<EntityId> removed = [request.Id];
        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (Entity entity in sketch.Entities.Values.OrderBy(entity => entity.Id))
            {
                if (removed.Contains(entity.Id))
                {
                    continue;
                }

                bool touched = entity switch
                {
                    Segment segment => removed.Contains(segment.Start) || removed.Contains(segment.End),
                    Dimension dimension => MeasurandEntities(dimension.Measures).Any(removed.Contains),
                    _ => false,
                };

                if (touched)
                {
                    removed.Add(entity.Id);
                    grew = true;
                }
            }
        }

        HashSet<RelationshipId> removedRelationships = [];
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (relationship.References.Any(removed.Contains))
            {
                removedRelationships.Add(relationship.Id);
            }
        }

        Sketch result = sketch;
        foreach (EntityId id in removed.OrderBy(id => id))
        {
            result = result.WithoutEntity(id);
        }

        foreach (RelationshipId id in removedRelationships.OrderBy(id => id))
        {
            result = result.WithoutRelationship(id);
        }

        result = DemoteDimensions(result, removedRelationships.Contains, out ImmutableHashSet<EntityId> demoted);

        return new Solved(
            result,
            ChangeSet.Empty with
            {
                Removed = [.. removed],
                Modified = demoted,
                RelationshipsRemoved = [.. removedRelationships],
            });
    }

    private static UpdateResult ApplyRemoveRelationship(Sketch sketch, RemoveRelationship request)
    {
        if (!sketch.Relationships.ContainsKey(request.Id))
        {
            return new Rejected(RejectionReason.UnknownRelationship);
        }

        // Removing a relationship never moves anything: the geometry stays where it is, it is
        // simply no longer held there. A dimension it drove becomes a reference dimension.
        Sketch result = DemoteDimensions(
            sketch.WithoutRelationship(request.Id),
            id => id == request.Id,
            out ImmutableHashSet<EntityId> demoted);

        return new Solved(
            result,
            ChangeSet.Empty with { Modified = demoted, RelationshipsRemoved = [request.Id] });
    }

    private static UpdateResult ApplySetLayer(Sketch sketch, SetLayer request)
    {
        if (sketch.Find(request.Id) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (!sketch.Layers.Any(layer => layer.Id == request.Layer))
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        return new Solved(
            sketch.WithEntity(entity.OnLayer(request.Layer)),
            ChangeSet.Empty with { Modified = [request.Id] });
    }

    private static UpdateResult ApplySetName(Sketch sketch, SetName request)
    {
        if (sketch.Find(request.Id) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        return new Solved(
            sketch.WithEntity(entity with { Name = request.Name }),
            ChangeSet.Empty with { Modified = [request.Id] });
    }

    private static UpdateResult ApplySetPart(Sketch sketch, SetPart request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        // Only a box can be a piece somebody cuts: a node has no size and a dimension is an
        // annotation, so asking either to be a part is a mistake rather than a preference.
        if (entity is not Box box)
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        return new Solved(
            sketch.WithEntity(box with { Part = request.Part }),
            ChangeSet.Empty with { Modified = [request.Box] });
    }

    private static UpdateResult ApplySetCut(Sketch sketch, SetCut request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        // Only a box is a blank. A node has no edges to cut and a dimension is an annotation.
        if (entity is not Box box)
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        // "Adds a cut, or replaces the cut at the same site" (§2.2): one operation per site, so
        // what was there goes and the new one takes its place. Box.Cuts's initialiser re-sorts.
        Box candidate = box with
        {
            Cuts = box.Cuts.RemoveAll(existing => existing.Site == request.Cut.Site).Add(request.Cut),
        };

        if (CutsRefuse(candidate) is { } refusal)
        {
            return refusal;
        }

        if (candidate == box)
        {
            // The same cut again is not a change, and a change set that claimed one would make the
            // canvas redraw for nothing — the no-op branch SetRotation already has.
            return new Solved(sketch, ChangeSet.Empty);
        }

        return new Solved(
            sketch.WithEntity(candidate),
            ChangeSet.Empty with { Modified = [request.Box] });
    }

    private static UpdateResult ApplyRemoveCut(Sketch sketch, RemoveCut request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (entity is not Box box)
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        ImmutableList<Cut> left = box.Cuts.RemoveAll(cut => cut.Site == request.Site);
        if (left.Count == box.Cuts.Count)
        {
            // No Detail: nothing about the sketch is wrong, and the request already names the box
            // and the site the caller asked about.
            return new Rejected(RejectionReason.NoSuchCut);
        }

        // Taking a cut off only gives the blank area back, so no invariant can break here.
        return new Solved(
            sketch.WithEntity(box with { Cuts = left }),
            ChangeSet.Empty with { Modified = [request.Box] });
    }

    /// <summary>
    /// The refusal a box's cuts earn it, or <see langword="null"/> when they are fine — the one
    /// mapping from an invariant 5-to-9 failure to a <see cref="RejectionReason"/>, shared by
    /// <see cref="AddEntity"/>, <see cref="SetCut"/> and the post-write check of &#xA7;2.3, so that
    /// the same broken box is refused for the same reason whichever door it came in by.
    /// </summary>
    private static Rejected? CutsRefuse(Box box)
        => CutRules.FirstError(box) is { } error
            ? new Rejected(
                error.Kind is ValidationErrorKind.CutDoesNotFit or ValidationErrorKind.NonPositiveArea
                    ? RejectionReason.CutDoesNotFit
                    : RejectionReason.CutSiteTaken,
                error)
            : null;

    // ---------------------------------------------------------------------------------------
    // Exact geometry requests
    // ---------------------------------------------------------------------------------------

    private UpdateResult ApplyAddRelationship(Sketch sketch, AddRelationship request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        Relationship relationship = request.Relationship;

        if (!SupportedRelationships.Contains(relationship.GetType()))
        {
            return new Rejected(RejectionReason.UnsupportedRelationship);
        }

        if (sketch.Relationships.ContainsKey(relationship.Id)
            || sketch.Relationships.Values.Any(existing => Relationship.AreStructurallyIdentical(existing, relationship)))
        {
            return new Rejected(RejectionReason.DuplicateRelationship);
        }

        if (!ReferencesResolve(sketch, relationship))
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        if (relationship is ParamValue paramValue && paramValue.Value <= Length.Zero)
        {
            return new Rejected(RejectionReason.NonPositiveSize);
        }

        Sketch target = sketch.WithRelationship(relationship);
        if (!CanPropagate(target, relationship))
        {
            return new Rejected(RejectionReason.UnsupportedRelationship);
        }

        return Propagate(
            target,
            NoSeeds,
            ChangeSet.Empty with { RelationshipsAdded = [relationship.Id] },
            SizeOwnedBy(relationship));
    }

    private UpdateResult ApplySetParameter(Sketch sketch, SetParameter request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find(request.Driving) is not { } relationship)
        {
            // A reference dimension has no driving relationship, so the canvas cannot form this
            // request for one at all; see docs/design/geometry-model.md §10.
            return new Rejected(RejectionReason.UnknownRelationship);
        }

        Relationship updated;
        switch (relationship)
        {
            case ParamValue paramValue:
                if (request.Value <= Length.Zero)
                {
                    return new Rejected(RejectionReason.NonPositiveSize);
                }

                updated = paramValue with { Value = request.Value };
                break;

            case AxisDistance axisDistance:
                updated = axisDistance with { Distance = request.Value };
                break;

            default:
                return new Rejected(RejectionReason.UnsupportedRelationship);
        }

        return Propagate(sketch.WithRelationship(updated), NoSeeds, ChangeSet.Empty, SizeOwnedBy(updated));
    }

    /// <summary>
    /// The size scalar a request is directly editing, which <see cref="Anchored"/> stands aside
    /// for: setting a dimension on an anchored part is not the part resizing "in response to other
    /// entities" (design &#xA7;3.2).
    /// </summary>
    private static IReadOnlySet<ScalarKey> SizeOwnedBy(Relationship relationship) => relationship switch
    {
        ParamValue { Param: BoxWidthRef width } => new HashSet<ScalarKey> { new(width.Box, ScalarKind.Width) },
        ParamValue { Param: BoxHeightRef height } => new HashSet<ScalarKey> { new(height.Box, ScalarKind.Height) },
        _ => ImmutableHashSet<ScalarKey>.Empty,
    };

    private UpdateResult ApplySetPosition(Sketch sketch, SetPosition request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find(request.Id) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (entity is not (Box or Node))
        {
            // Only a box or a node carries coordinates of its own. This is a reference of the
            // wrong kind, not a relationship this updater cannot hold.
            return new Rejected(RejectionReason.DanglingReference);
        }

        Dictionary<ScalarKey, Length> seeds = new()
        {
            [new ScalarKey(request.Id, ScalarKind.X)] = request.Anchor.X,
            [new ScalarKey(request.Id, ScalarKind.Y)] = request.Anchor.Y,
        };

        return Propagate(sketch, seeds, ChangeSet.Empty);
    }

    private UpdateResult ApplySetRotation(Sketch sketch, SetRotation request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find<Box>(request.Box) is not { } box)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (!request.Rotation.IsRightAngleMultiple)
        {
            return new Rejected(RejectionReason.RotationNotSupported);
        }

        // Edge and corner references are in the box's local frame, so rotating a box that has a
        // Flush, Coincident, AxisDistance or Centered would turn a relationship between parallel
        // edges into one between perpendicular edges. Rather than guess what the user meant, say
        // so and let the canvas offer to remove them first (design §4.4).
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (!relationship.References.Contains(request.Box))
            {
                continue;
            }

            if (relationship is not (Anchored or ParamValue or EqualParam))
            {
                return new Rejected(RejectionReason.RotationWithRelationships);
            }
        }

        if (box.Rotation == request.Rotation)
        {
            return new Solved(sketch, ChangeSet.Empty);
        }

        Sketch result = sketch.WithEntity(box with { Rotation = request.Rotation });
        AssertHolds(result);

        // A rotation leaves the anchor where it is and moves everything else about the box, so it
        // is neither a move nor a resize: the canvas has to redraw it all the same.
        return new Solved(result, ChangeSet.Empty with { Modified = [request.Box] });
    }

    // ---------------------------------------------------------------------------------------
    // Best-effort geometry requests
    // ---------------------------------------------------------------------------------------

    private UpdateResult ApplyDrag(Sketch sketch, Drag request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find(request.Id) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        HashSet<EntityId> seed = entity switch
        {
            Segment segment => [segment.Start, segment.End],
            Box or Node => [entity.Id],
            _ => [],
        };

        if (seed.Count == 0)
        {
            // A dimension has no coordinates of its own; its placement is canvas data.
            return new Rejected(RejectionReason.DanglingReference);
        }

        // The rigid group is per axis, because a Flush on a vertical edge blocks X and not Y, and
        // an AxisDistance along X blocks X and not Y (design §4.4).
        HashSet<EntityId> alongX = RigidGroup(sketch, seed, Axis.X);
        HashSet<EntityId> alongY = RigidGroup(sketch, seed, Axis.Y);

        Vector2 applied = new(
            alongX.Any(id => IsAnchored(sketch, id)) ? Length.Zero : request.Delta.Dx,
            alongY.Any(id => IsAnchored(sketch, id)) ? Length.Zero : request.Delta.Dy);

        Sketch result = sketch;
        ImmutableHashSet<EntityId>.Builder moved = ImmutableHashSet.CreateBuilder<EntityId>();

        foreach (EntityId id in alongX.Union(alongY).OrderBy(id => id))
        {
            Vector2 shift = new(
                alongX.Contains(id) ? applied.Dx : Length.Zero,
                alongY.Contains(id) ? applied.Dy : Length.Zero);

            if (shift == Vector2.Zero)
            {
                continue;
            }

            result = Translate(result, id, shift);
            moved.Add(id);
        }

        // A drag that goes nowhere is not a conflict: it is a question that got the answer "no".
        AssertHolds(result);
        return new Solved(result, ChangeSet.Empty with { Moved = moved.ToImmutable(), AppliedDelta = applied });
    }

    private UpdateResult ApplyDragEdge(Sketch sketch, DragEdge request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find<Box>(request.Box) is not { } box)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        bool alongWidth = request.Edge is BoxEdge.East or BoxEdge.West;
        Axis localAxis = alongWidth ? Axis.X : Axis.Y;
        ParamRef size = alongWidth ? new BoxWidthRef(request.Box) : new BoxHeightRef(request.Box);

        // A drag never silently overrides a number the user typed.
        if (sketch.Relationships.Values.Any(relationship => relationship is ParamValue driven && driven.Param == size))
        {
            return new Rejected(RejectionReason.DrivenSize);
        }

        // §2.3: best effort, as always — the delta is clamped so the edge is never dragged past
        // what the cuts on it claim, and the applied delta is what gets reported. A blank cannot be
        // dragged shorter than its cuts, the way it cannot be dragged through an anchored
        // neighbour. The floor is zero for a plain rectangle, so nothing here changes for one.
        Length currentSize = alongWidth ? box.Width : box.Height;
        Length newSize = Length.Max(currentSize + request.Delta, CutRules.SmallestFitting(box, localAxis));
        if (newSize <= Length.Zero)
        {
            return new Rejected(RejectionReason.NonPositiveSize);
        }

        Length delta = newSize - currentSize;

        // Grabbing the anchor's own edge moves the anchor; grabbing the far edge leaves it. Either
        // way the opposite edge stays put, so both anchor coordinates are seeded and pinned.
        Vector2 anchorShift = request.Edge is BoxEdge.West or BoxEdge.South
            ? Vector2.Along(localAxis, -delta).Rotate(box.Rotation)
            : Vector2.Zero;
        Point2 anchor = box.Anchor + anchorShift;

        ScalarKey sizeKey = new(request.Box, alongWidth ? ScalarKind.Width : ScalarKind.Height);
        Dictionary<ScalarKey, Length> seeds = new()
        {
            [sizeKey] = newSize,
            [new ScalarKey(request.Box, ScalarKind.X)] = anchor.X,
            [new ScalarKey(request.Box, ScalarKind.Y)] = anchor.Y,
        };

        // The handle the user grabbed is what they are editing, so Anchored stands aside for
        // everything this request seeds: the size, and the anchor corner that a west or south
        // handle necessarily drags with it. Without the anchor, the east handle of an anchored box
        // would work and the west one would silently refuse (Fable review of #35, finding 8).
        HashSet<ScalarKey> owned =
        [
            sizeKey,
            new ScalarKey(request.Box, ScalarKind.X),
            new ScalarKey(request.Box, ScalarKind.Y),
        ];

        if (Propagator.Run(sketch, seeds, sketch.Relationships.Values, owned) is not Propagated propagated)
        {
            // Best effort: a Flush to an anchored box blocks the edge entirely, and the edge then
            // does not move at all.
            return new Solved(sketch, ChangeSet.Empty with { AppliedDelta = Vector2.Zero });
        }

        Sketch written = Write(sketch, propagated.Assignments, out ImmutableHashSet<EntityId> moved, out ImmutableHashSet<EntityId> resized);
        AssertHolds(written);

        // The clamp above covers the box the user grabbed. Another box this resized through an
        // EqualParam has cuts of its own, and a drag is a question rather than a demand, so a
        // refusal there is the same answer the blocked-propagation arm gives: the edge does not
        // move at all. Nothing partial is ever handed back.
        if (CutsStillFit(written, resized) is not null)
        {
            return new Solved(sketch, ChangeSet.Empty with { AppliedDelta = Vector2.Zero });
        }

        Length outward = request.Edge is BoxEdge.West or BoxEdge.South ? -delta : delta;
        Vector2 applied = Vector2.Along(localAxis, outward).Rotate(box.Rotation);

        return new Solved(
            written,
            ChangeSet.Empty with { Moved = moved, Resized = resized, AppliedDelta = applied });
    }

    private UpdateResult ApplyBatch(Sketch sketch, Batch request)
    {
        Sketch current = sketch;
        ChangeSet changes = ChangeSet.Empty;

        foreach (Request inner in request.Requests)
        {
            UpdateResult result = Apply(current, inner);
            if (result is not Succeeded succeeded)
            {
                // Atomic: nothing is applied, and the report is the one from the request that
                // failed. The caller still holds the original sketch.
                return result;
            }

            current = succeeded.Sketch;
            changes = changes.Merge(succeeded.Changes);
        }

        return new Solved(current, changes);
    }

    // ---------------------------------------------------------------------------------------
    // The seam to the propagator
    // ---------------------------------------------------------------------------------------

    private static UpdateResult Propagate(
        Sketch target,
        IReadOnlyDictionary<ScalarKey, Length> seeds,
        ChangeSet changes,
        IReadOnlySet<ScalarKey>? requestOwns = null)
    {
        PropagationResult propagation
            = Propagator.Run(target, seeds, target.Relationships.Values, requestOwns);
        if (propagation is not Propagated propagated)
        {
            // The working table was never written back, so the caller's sketch is untouched by
            // construction.
            return new OverConstrained(((Conflicted)propagation).Report);
        }

        Sketch written = Write(target, propagated.Assignments, out ImmutableHashSet<EntityId> moved, out ImmutableHashSet<EntityId> resized);
        AssertHolds(written);

        // §2.3's seam, "after the write, before the return": the propagator knows nothing about
        // cuts and should not, so a typed dimension, an EqualParam from another box, a stock
        // assignment or any future solver-produced write could otherwise shrink a blank below what
        // its cuts need and nothing would catch it. `written` is a value nobody else has seen, so
        // dropping it here leaves the caller's sketch untouched by construction — the same way the
        // conflict arm above never wrote the working table back.
        if (CutsStillFit(written, resized) is { } refusal)
        {
            return refusal;
        }

        return new Solved(
            written,
            changes with { Moved = changes.Moved.Union(moved), Resized = changes.Resized.Union(resized) });
    }

    /// <summary>
    /// Invariants 7 to 9 on every box a write resized, in id order so that the box named is the
    /// same one whatever order the sketch's dictionaries were built in
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.3).
    /// </summary>
    /// <remarks>
    /// Only the resized boxes: a cut is in the blank's local frame, so moving or rotating a box
    /// carries its cuts along unchanged, and a box whose size did not change cannot have stopped
    /// fitting them.
    /// </remarks>
    private static Rejected? CutsStillFit(Sketch written, ImmutableHashSet<EntityId> resized)
    {
        foreach (EntityId id in resized.OrderBy(id => id))
        {
            if (written.Find<Box>(id) is { } box && CutsRefuse(box) is { } refusal)
            {
                return refusal;
            }
        }

        return null;
    }

    private static Sketch Write(
        Sketch sketch,
        ImmutableDictionary<ScalarKey, Assignment> assignments,
        out ImmutableHashSet<EntityId> moved,
        out ImmutableHashSet<EntityId> resized)
    {
        ImmutableHashSet<EntityId>.Builder movedBuilder = ImmutableHashSet.CreateBuilder<EntityId>();
        ImmutableHashSet<EntityId>.Builder resizedBuilder = ImmutableHashSet.CreateBuilder<EntityId>();
        Sketch result = sketch;

        foreach (EntityId id in assignments.Keys.Select(key => key.Entity).Distinct().OrderBy(entity => entity))
        {
            EntityId entityId = id;
            Length Value(ScalarKind kind)
            {
                ScalarKey key = new(entityId, kind);
                return assignments.TryGetValue(key, out Assignment? assignment)
                    ? assignment.Value
                    : Propagator.StoredValueOf(sketch, key);
            }

            switch (sketch.Find(id))
            {
                case Box box:
                {
                    Point2 anchor = new(Value(ScalarKind.X), Value(ScalarKind.Y));
                    Length width = Value(ScalarKind.Width);
                    Length height = Value(ScalarKind.Height);

                    if (anchor != box.Anchor)
                    {
                        movedBuilder.Add(id);
                    }

                    if (width != box.Width || height != box.Height)
                    {
                        resizedBuilder.Add(id);
                    }

                    if (anchor != box.Anchor || width != box.Width || height != box.Height)
                    {
                        result = result.WithEntity(box with { Anchor = anchor, Width = width, Height = height });
                    }

                    break;
                }

                case Node node:
                {
                    Point2 position = new(Value(ScalarKind.X), Value(ScalarKind.Y));
                    if (position != node.Position)
                    {
                        movedBuilder.Add(id);
                        result = result.WithEntity(node with { Position = position });
                    }

                    break;
                }
            }
        }

        moved = movedBuilder.ToImmutable();
        resized = resizedBuilder.ToImmutable();
        return result;
    }

    /// <summary>
    /// The post-write assertion of design &#xA7;4.4 step 5: the sketch the updater produced must
    /// satisfy every relationship in it. A failure here is a bug in this class, not a result, so
    /// it is thrown rather than returned — and thrown rather than asserted, so that a CI failure
    /// says what went wrong instead of killing the test host.
    /// </summary>
    private static void AssertHolds(Sketch sketch)
    {
        CheckReport report = RelationshipChecker.Check(sketch);
        if (!report.AllHold)
        {
            throw new InvalidOperationException(
                "The direct updater produced a sketch that does not satisfy its own relationships. "
                + $"This is a bug in DirectUpdater.{Environment.NewLine}{report}");
        }
    }

    // ---------------------------------------------------------------------------------------
    // Preconditions and shared queries
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Whether this sketch is inside the direct updater's domain: every box rotated by a
    /// right-angle multiple, and every relationship one it can propagate. Such a sketch can only
    /// come from a solver-written file; the app picks the updater that can handle the file.
    /// </summary>
    private static RejectionReason? GeometryPrecondition(Sketch sketch)
    {
        foreach (Entity entity in sketch.Entities.Values)
        {
            if (entity is Box box && !box.Rotation.IsRightAngleMultiple)
            {
                return RejectionReason.RotationNotSupported;
            }
        }

        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (!ReferencesResolve(sketch, relationship))
            {
                return RejectionReason.DanglingReference;
            }

            if (!CanPropagate(sketch, relationship))
            {
                return RejectionReason.UnsupportedRelationship;
            }
        }

        return null;
    }

    private static bool CanPropagate(Sketch sketch, Relationship relationship) => relationship switch
    {
        Anchored or Coincident or AxisDistance or Centered => true,
        ParamValue paramValue => paramValue.Param is BoxWidthRef or BoxHeightRef,
        EqualParam equalParam => equalParam.A is BoxWidthRef or BoxHeightRef
                                 && equalParam.B is BoxWidthRef or BoxHeightRef,
        Horizontal horizontal => horizontal.Edge is SegmentRef,
        Vertical vertical => vertical.Edge is SegmentRef,
        Flush flush => FlushNormalAxis(sketch, flush) is not null,
        _ => false,
    };

    private static Axis? FlushNormalAxis(Sketch sketch, Flush flush)
    {
        Axis? first = NormalAxisOf(sketch.EdgeOf(flush.A));
        return first is { } axis && NormalAxisOf(sketch.EdgeOf(flush.B)) == axis ? axis : null;
    }

    private static Axis? NormalAxisOf((Point2 From, Point2 To) edge)
    {
        bool sameX = edge.From.X == edge.To.X;
        bool sameY = edge.From.Y == edge.To.Y;

        if (sameX == sameY)
        {
            return null;
        }

        return sameX ? Axis.X : Axis.Y;
    }

    private static HashSet<EntityId> RigidGroup(Sketch sketch, HashSet<EntityId> seed, Axis axis)
    {
        HashSet<EntityId> group = [.. seed];

        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (Relationship relationship in sketch.RelationshipsInOrder)
            {
                List<EntityId> coupled = [.. CoupledOn(sketch, relationship, axis)];
                if (coupled.Count == 0 || !coupled.Any(group.Contains))
                {
                    continue;
                }

                foreach (EntityId id in coupled)
                {
                    grew |= group.Add(id);
                }
            }
        }

        return group;
    }

    /// <summary>
    /// The entities a relationship makes move together along one axis. A <see cref="Flush"/> on a
    /// vertical edge couples X and not Y; an <see cref="AxisDistance"/> along X couples X and not
    /// Y. <see cref="Horizontal"/> and <see cref="Vertical"/> are here too, although design
    /// &#xA7;4.4's list omits them: they tie a segment's two nodes together along one axis just as
    /// firmly, and a drag that ignored them would break them.
    /// </summary>
    private static IEnumerable<EntityId> CoupledOn(Sketch sketch, Relationship relationship, Axis axis)
        => relationship switch
        {
            Coincident coincident => Movable(sketch, coincident.A.Owner).Concat(Movable(sketch, coincident.B.Owner)),

            Flush flush when FlushNormalAxis(sketch, flush) == axis
                => Movable(sketch, flush.A.Owner).Concat(Movable(sketch, flush.B.Owner)),

            AxisDistance distance when distance.Axis == axis
                => Movable(sketch, distance.From.Owner).Concat(Movable(sketch, distance.To.Owner)),

            Centered centered when centered.Axis == axis
                => Movable(sketch, centered.Middle.Owner)
                    .Concat(Movable(sketch, centered.A.Owner))
                    .Concat(Movable(sketch, centered.B.Owner)),

            Horizontal horizontal when axis == Axis.Y => Movable(sketch, horizontal.Edge.Owner),
            Vertical vertical when axis == Axis.X => Movable(sketch, vertical.Edge.Owner),

            _ => [],
        };

    /// <summary>
    /// The entities that actually carry coordinates. A segment has none of its own: moving it
    /// means moving its two nodes.
    /// </summary>
    private static IEnumerable<EntityId> Movable(Sketch sketch, EntityId entity)
        => sketch.Find(entity) is Segment segment ? [segment.Start, segment.End] : [entity];

    private static bool IsAnchored(Sketch sketch, EntityId entity)
        => sketch.Relationships.Values.Any(relationship => relationship is Anchored anchored && anchored.Entity == entity);

    private static Sketch Translate(Sketch sketch, EntityId id, Vector2 shift) => sketch.Find(id) switch
    {
        Box box => sketch.WithEntity(box with { Anchor = box.Anchor + shift }),
        Node node => sketch.WithEntity(node with { Position = node.Position + shift }),
        _ => sketch,
    };

    private static Sketch DemoteDimensions(
        Sketch sketch,
        Func<RelationshipId, bool> wasRemoved,
        out ImmutableHashSet<EntityId> demoted)
    {
        Sketch result = sketch;
        ImmutableHashSet<EntityId>.Builder builder = ImmutableHashSet.CreateBuilder<EntityId>();

        foreach (Entity entity in sketch.Entities.Values.OrderBy(entity => entity.Id))
        {
            if (entity is Dimension dimension && dimension.Drives is { } driving && wasRemoved(driving))
            {
                result = result.WithEntity(dimension with { Drives = null });
                builder.Add(dimension.Id);
            }
        }

        demoted = builder.ToImmutable();
        return result;
    }

    private static IEnumerable<EntityId> MeasurandEntities(Measurand measurand) => measurand switch
    {
        ParamMeasurand param => [param.Param.Owner],
        AxisMeasurand axis => [axis.From.Owner, axis.To.Owner],
        _ => [],
    };

    private static bool MeasurandResolves(Sketch sketch, Measurand measurand) => measurand switch
    {
        ParamMeasurand param => ReferenceResolves(sketch, param.Param),
        AxisMeasurand axis => ReferenceResolves(sketch, axis.From) && ReferenceResolves(sketch, axis.To),
        _ => false,
    };

    private static bool ReferencesResolve(Sketch sketch, Relationship relationship) => relationship switch
    {
        // Only a box or a node has a position of its own to hold still. Anchoring a segment or a
        // dimension would be inert, and an anchor that silently does nothing is worse than a
        // refusal (Fable review of #35, finding 3).
        Anchored anchored => sketch.Find(anchored.Entity) is Box or Node,
        Coincident coincident => ReferenceResolves(sketch, coincident.A) && ReferenceResolves(sketch, coincident.B),
        Horizontal horizontal => ReferenceResolves(sketch, horizontal.Edge),
        Vertical vertical => ReferenceResolves(sketch, vertical.Edge),
        Flush flush => ReferenceResolves(sketch, flush.A) && ReferenceResolves(sketch, flush.B),
        AxisDistance distance => ReferenceResolves(sketch, distance.From) && ReferenceResolves(sketch, distance.To),
        ParamValue paramValue => ReferenceResolves(sketch, paramValue.Param),
        EqualParam equalParam => ReferenceResolves(sketch, equalParam.A) && ReferenceResolves(sketch, equalParam.B),
        Centered centered => ReferenceResolves(sketch, centered.Middle)
                             && ReferenceResolves(sketch, centered.A)
                             && ReferenceResolves(sketch, centered.B),
        _ => relationship.References.All(id => sketch.Find(id) is not null),
    };

    private static bool ReferenceResolves(Sketch sketch, PointRef reference) => reference switch
    {
        NodeRef node => sketch.Find<Node>(node.Node) is not null,
        CornerRef corner => sketch.Find<Box>(corner.Box) is not null,
        CenterRef centre => sketch.Find<Box>(centre.Box) is not null,
        _ => false,
    };

    private static bool ReferenceResolves(Sketch sketch, EdgeRef reference) => reference switch
    {
        SegmentRef segmentRef => sketch.Find<Segment>(segmentRef.Segment) is { } segment
                                 && sketch.Find<Node>(segment.Start) is not null
                                 && sketch.Find<Node>(segment.End) is not null,
        BoxEdgeRef boxEdge => sketch.Find<Box>(boxEdge.Box) is not null,
        _ => false,
    };

    private static bool ReferenceResolves(Sketch sketch, ParamRef reference) => reference switch
    {
        BoxWidthRef width => sketch.Find<Box>(width.Box) is not null,
        BoxHeightRef height => sketch.Find<Box>(height.Box) is not null,
        SegmentLengthRef length => ReferenceResolves(sketch, new SegmentRef(length.Segment)),
        _ => false,
    };
}
