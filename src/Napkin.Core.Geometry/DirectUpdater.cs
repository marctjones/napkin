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
        typeof(Joint),
        typeof(StrutJoint),
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
            AddLayer addLayer => sketch.Layers.Any(layer => layer.Id == addLayer.Layer.Id)
                ? new Rejected(RejectionReason.DuplicateEntity)
                : new Solved(sketch.WithLayer(addLayer.Layer), ChangeSet.Empty),
            SetPart setPart => ApplySetPart(sketch, setPart),
            SetFastenerChoices choices => new Solved(sketch with { FastenerChoices = choices.Choices }, ChangeSet.Empty),
            SetSupplies supplies => new Solved(sketch with { Supplies = supplies.Supplies }, ChangeSet.Empty),
            SetCode code => new Solved(sketch with { Code = code.Code }, ChangeSet.Empty),
            SetSite site => new Solved(sketch with { Site = site.Site }, ChangeSet.Empty),
            SetWallInputs wall => ApplySetWallInputs(sketch, wall),
            SetPhase phase => sketch.Find(phase.Id) is { } phased
                ? new Solved(sketch.WithEntity(phased with { Phase = phase.Phase }), ChangeSet.Empty with { Modified = [phase.Id] })
                : new Rejected(RejectionReason.UnknownEntity),
            SetRoomInputs room => ApplySetRoomInputs(sketch, room),
            SetDeckInputs deck => ApplySetDeckInputs(sketch, deck),
            SetOpeningFill fill => sketch.Find(fill.Box) switch
            {
                Box box when box.WallInputs is null && box.Deck is null && box.Roof is null => new Solved(sketch.WithEntity(box with { Opening = fill.Fill }), ChangeSet.Empty with { Modified = [box.Id] }),
                Box => new Rejected(RejectionReason.DanglingReference),
                null => new Rejected(RejectionReason.UnknownEntity),
                _ => new Rejected(RejectionReason.DanglingReference),
            },
            SetStrutCuts cuts => ApplySetStrutCuts(sketch, cuts),
            SetNote note => sketch.Find(note.Id) switch
            {
                Note found => new Solved(sketch.WithEntity(found with { Text = note.Text, Symbol = note.Symbol }), ChangeSet.Empty with { Modified = [note.Id] }),
                null => new Rejected(RejectionReason.UnknownEntity),
                _ => new Rejected(RejectionReason.DanglingReference),
            },

            // A cut is in the blank's local frame and moves with it, so setting or removing one
            // moves no geometry and disturbs no relationship: structural, like a rename
            // (docs/design/shaped-parts-model.md §2.2).
            SetCut setCut => ApplySetCut(sketch, setCut),
            RemoveCut removeCut => ApplyRemoveCut(sketch, removeCut),

            // Geometry requests need the rectilinear precondition first.
            AddRelationship add => ApplyAddRelationship(sketch, add),
            SetParameter setParameter => ApplySetParameter(sketch, setParameter),
            SetPosition setPosition => ApplySetPosition(sketch, setPosition),
            SetOrientation setOrientation => ApplySetOrientation(sketch, setOrientation),
            Drag drag => ApplyDrag(sketch, drag),
            DragFace dragFace => ApplyDragFace(sketch, dragFace),
            SetStrutEnd setEnd => ApplySetStrutEnd(sketch, setEnd),
            DragStrutEnd dragEnd => ApplyDragStrutEnd(sketch, dragEnd),

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
            if (box.Width <= Length.Zero || box.Height <= Length.Zero || box.Depth <= Length.Zero)
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

        if (entity is Strut strut && StrutRefusal(strut) is { } broken)
        {
            return broken;
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

            // Invariant 13: a dimension lies in the plan, so one that would measure along world Z
            // has nowhere to be drawn (docs/design/assembly-model.md §7.3). The number is not lost:
            // it is a ParamValue or an AxisDistance, which needs no drawing.
            if (PlaceRules.MeasurandRefusal(sketch, dimension) is { } leaves)
            {
                return new Rejected(RejectionReason.UnsupportedRequest, leaves);
            }

            if (dimension.Drives is { } driving && !sketch.Relationships.ContainsKey(driving))
            {
                return new Rejected(RejectionReason.UnknownRelationship);
            }
        }

        return new Solved(sketch.WithEntity(entity), ChangeSet.Empty with { Added = [entity.Id] });
    }

    /// <summary>Invariants 14–17 of a strut as a refusal, or <see langword="null"/> when they hold.</summary>
    private static Rejected? StrutRefusal(Strut strut)
        => Sketch.StrutErrors(strut).FirstOrDefault() is { } broken
            ? new Rejected(
                broken.Kind switch
                {
                    ValidationErrorKind.StrutIsAxisAligned => RejectionReason.StrutIsAxisAligned,
                    ValidationErrorKind.NonPositiveSize => RejectionReason.NonPositiveSize,
                    ValidationErrorKind.StrutTooShortForItsCuts => RejectionReason.StrutTooShortForItsCuts,
                    _ => RejectionReason.InvalidStrut,
                },
                broken)
            : null;

    private static UpdateResult ApplySetStrutCuts(Sketch sketch, SetStrutCuts request)
    {
        if (sketch.Find<Strut>(request.Strut) is not { } strut)
        {
            return new Rejected(sketch.Find(request.Strut) is null ? RejectionReason.UnknownEntity : RejectionReason.DanglingReference);
        }

        Strut cut = strut with { FromCut = request.FromCut, ToCut = request.ToCut, Reference = request.Reference };
        if (cut == strut)
        {
            return new Solved(sketch, ChangeSet.Empty);
        }

        Sketch written = sketch.WithEntity(cut);
        return StrutsStillHold(written, [request.Strut]) is { } refusal
            ? refusal
            : new Solved(written, ChangeSet.Empty with { Modified = [request.Strut] });
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

    private static UpdateResult ApplySetWallInputs(Sketch sketch, SetWallInputs request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (entity is not Box box)
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        if (request.Inputs?.StudSpacing is { } spacing && spacing <= Length.Zero)
        {
            return new Rejected(RejectionReason.NonPositiveSize);
        }

        Box changed = box with { WallInputs = request.Inputs?.OrNull() };
        return new Solved(sketch.WithEntity(changed), ChangeSet.Empty with { Modified = [box.Id] });
    }

    private static UpdateResult ApplySetRoomInputs(Sketch sketch, SetRoomInputs request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (entity is not Box box)
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        if (request.Inputs is { } room && RoomRules.Refusal(room) is not null)
        {
            return new Rejected(RejectionReason.NonPositiveSize);
        }

        return new Solved(sketch.WithEntity(box with { Room = request.Inputs }), ChangeSet.Empty with { Modified = [box.Id] });
    }

    private static UpdateResult ApplySetDeckInputs(Sketch sketch, SetDeckInputs request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        if (entity is not Box box)
        {
            return new Rejected(RejectionReason.DanglingReference);
        }

        if (request.Inputs is { } deck && DeckRules.Refusal(deck) is not null)
        {
            return new Rejected(RejectionReason.NonPositiveSize);
        }

        return new Solved(sketch.WithEntity(box with { Deck = request.Inputs }), ChangeSet.Empty with { Modified = [box.Id] });
    }

    private static UpdateResult ApplySetPart(Sketch sketch, SetPart request)
    {
        if (sketch.Find(request.Box) is not { } entity)
        {
            return new Rejected(RejectionReason.UnknownEntity);
        }

        // A strut is a piece somebody cuts too, its derived dimension named length or width
        // (angled-parts invariant 16).
        if (entity is Strut strut)
        {
            Strut parted = strut with { Part = request.Part };
            return StrutRefusal(parted) is { } refused
                ? refused
                : new Solved(sketch.WithEntity(parted), ChangeSet.Empty with { Modified = [request.Box] });
        }

        // Only a box or a strut can be a piece somebody cuts: a node has no size and a dimension is
        // an annotation, so asking either to be a part is a mistake rather than a preference.
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

        // A strut's cuts are derived from its ends (assembly-model §3a.5): there is nothing to set.
        if (entity is Strut)
        {
            return new Rejected(RejectionReason.UnsupportedRequest);
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
            // canvas redraw for nothing — the no-op branch SetOrientation already has.
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

        if (entity is Strut)
        {
            return new Rejected(RejectionReason.UnsupportedRequest);
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

        // §2.3: a pairing that can never hold is refused, not stored and quietly violated. The
        // loader asks the same question through Sketch.Validate.
        if (PlaceRules.Refusal(sketch, relationship) is { } notComparable)
        {
            return new Rejected(RejectionReason.PlacesNotComparable, notComparable);
        }

        if (relationship is Joint joint && JointRules.Errors(joint).FirstOrDefault() is { } invalid)
        {
            return new Rejected(
                RejectionReason.InvalidJoint,
                new ValidationError(ValidationErrorKind.InvalidJoint, invalid));
        }

        if (relationship is StrutJoint strutJoint && StrutJoint.Errors(strutJoint).FirstOrDefault() is { } invalidOnStrut)
        {
            return new Rejected(
                RejectionReason.InvalidJoint,
                new ValidationError(ValidationErrorKind.InvalidJoint, invalidOnStrut));
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
        ParamValue { Param: BoxDepthRef depth } => new HashSet<ScalarKey> { new(depth.Box, ScalarKind.Depth) },
        ParamValue { Param: StrutHeightRef height } => new HashSet<ScalarKey> { new(height.Strut, ScalarKind.Height) },
        ParamValue { Param: StrutDepthRef depth } => new HashSet<ScalarKey> { new(depth.Strut, ScalarKind.Depth) },
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

        if (entity is Note placed)
        {
            // A note has no relationships and no Z: it goes where it is put (renovation §7).
            Point2 at = new(request.Anchor.X, request.Anchor.Y);
            return at == placed.Position
                ? new Solved(sketch, ChangeSet.Empty)
                : new Solved(sketch.WithEntity(placed with { Position = at }), ChangeSet.Empty with { Moved = [placed.Id] });
        }

        if (entity is Strut)
        {
            // A strut has no anchor: its ends are placed one at a time (SetStrutEnd), or together by Drag.
            return new Rejected(RejectionReason.UnsupportedRequest);
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

        if (entity is Box)
        {
            seeds[new ScalarKey(request.Id, ScalarKind.Z)] = request.Anchor.Z;
        }
        else if (request.Anchor.Z != Length.Zero)
        {
            // A node is plan-plane construction geometry at the plan datum (assembly-model §1.4,
            // §11 decision 16). Putting one above it is refused out loud, not flattened onto Z = 0.
            return new Rejected(RejectionReason.UnsupportedRequest);
        }

        return Propagate(sketch, seeds, ChangeSet.Empty);
    }

    private UpdateResult ApplySetStrutEnd(Sketch sketch, SetStrutEnd request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find<Strut>(request.Strut) is null)
        {
            return new Rejected(sketch.Find(request.Strut) is null ? RejectionReason.UnknownEntity : RejectionReason.DanglingReference);
        }

        // That end's three scalars, and nothing of the other end (assembly-model §3a.5): it moves
        // only if a relationship moves it. Not request-owned, so an Anchored strut refuses.
        Dictionary<ScalarKey, Length> seeds = [];
        foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
        {
            seeds[new ScalarKey(request.Strut, Propagator.EndKind(request.End, axis))] = request.At.Component(axis);
        }

        return Propagate(sketch, seeds, ChangeSet.Empty);
    }

    private UpdateResult ApplyDragStrutEnd(Sketch sketch, DragStrutEnd request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find<Strut>(request.Strut) is not { } strut)
        {
            return new Rejected(sketch.Find(request.Strut) is null ? RejectionReason.UnknownEntity : RejectionReason.DanglingReference);
        }

        // Best effort, per axis and in full or not at all, like Drag: each axis is tried on top of
        // the axes already allowed, and one that a relationship or the strut's own invariants refuse
        // goes nowhere. A drag is a question, never a conflict.
        Point3 start = strut.End(request.End);
        Vector3 applied = Vector3.Zero;
        Solved answer = new(sketch, ChangeSet.Empty);
        foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
        {
            Length step = request.Delta.Component(axis);
            if (step == Length.Zero)
            {
                continue;
            }

            Vector3 tried = applied.WithComponent(axis, step);
            if (ApplySetStrutEnd(sketch, new SetStrutEnd(request.Strut, request.End, start + tried)) is Solved solved)
            {
                applied = tried;
                answer = solved;
            }
        }

        return answer with { Changes = answer.Changes with { AppliedDelta = applied } };
    }

    private UpdateResult ApplySetOrientation(Sketch sketch, SetOrientation request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find<Box>(request.Box) is not { } box)
        {
            // A strut has no face-up: its ends say which way it runs.
            return new Rejected(sketch.Find(request.Box) is Strut ? RejectionReason.UnsupportedRequest : RejectionReason.UnknownEntity);
        }

        if (!Enum.IsDefined(request.FaceUp))
        {
            return new Rejected(RejectionReason.UnsupportedRequest);
        }

        if (!request.Rotation.IsRightAngleMultiple)
        {
            return new Rejected(RejectionReason.RotationNotSupported);
        }

        Box turned = box with { FaceUp = request.FaceUp, Rotation = request.Rotation };
        if (turned.Orientation == box.Orientation)
        {
            return new Solved(sketch, ChangeSet.Empty);
        }

        // Faces, edges and corners are named in the box's local frame, so turning a box that has a
        // Flush, Coincident, AxisDistance or Centered would silently turn a face-to-face
        // relationship into a face-to-edge one. Rather than guess what the user meant, say so and
        // let the canvas offer to remove them first (assembly-model §2.4, §11 decision 6). Sizes
        // and anchors are not named by place, so they turn with the box and mean what they meant.
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            if (relationship.References.Contains(request.Box)
                && relationship is not (Anchored or ParamValue or EqualParam))
            {
                return new Rejected(
                    RejectionReason.OrientationWithRelationships,
                    new ValidationError(
                        ValidationErrorKind.TurnWouldReinterpret,
                        $"{relationship.GetType().Name} {relationship.Id} holds {NameOf(box)} by a place in its own frame, "
                        + "and turning the box would change which place that is. Remove it first."));
            }
        }

        // Invariant 13 after the turn: a dimension on the box — a reference dimension has no
        // relationship above to catch it by — must still lie in the plan. Asked of the sketch the
        // turn would write, through the one rule the loader and AddEntity use.
        Sketch result = sketch.WithEntity(turned);
        foreach (Dimension dimension in result.Entities.Values.OfType<Dimension>().OrderBy(dimension => dimension.Id))
        {
            if (MeasurandEntities(dimension.Measures).Contains(request.Box)
                && PlaceRules.MeasurandRefusal(result, dimension) is { } leaves)
            {
                return new Rejected(RejectionReason.OrientationWithRelationships, leaves);
            }
        }

        AssertHolds(result);

        // A turn leaves the anchor where it is and moves everything else about the box, so it is
        // neither a move nor a resize: the canvas has to redraw it all the same.
        return new Solved(result, ChangeSet.Empty with { Modified = [request.Box] });
    }

    private static string NameOf(Entity entity) => entity.Name.Length > 0 ? entity.Name : entity.Id.ToString();

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

        if (entity is Note note)
        {
            // A note has no relationships to carry and no Z: it moves by the drag's plan part alone.
            Vector2 plan = new(request.Delta.Dx, request.Delta.Dy);
            return plan == Vector2.Zero
                ? new Solved(sketch, ChangeSet.Empty)
                : new Solved(sketch.WithEntity(note with { Position = note.Position + plan }), ChangeSet.Empty with { Moved = [note.Id] });
        }

        HashSet<EntityId> seed = entity switch
        {
            Segment segment => [segment.Start, segment.End],
            Box or Node or Strut => [entity.Id],
            _ => [],
        };

        if (seed.Count == 0)
        {
            // A dimension has no coordinates of its own; its placement is canvas data.
            return new Rejected(RejectionReason.DanglingReference);
        }

        // The rigid group is per axis, because a Flush on a vertical face blocks X and not Y or Z,
        // an AxisDistance along X blocks X only, and a Coincident blocks the axes its two places
        // share (design §4.4, assembly-model §10 step 4).
        HashSet<EntityId> alongX = RigidGroup(sketch, seed, Axis.X);
        HashSet<EntityId> alongY = RigidGroup(sketch, seed, Axis.Y);
        HashSet<EntityId> alongZ = RigidGroup(sketch, seed, Axis.Z);

        // A node has no Z to move: it is plan-plane construction geometry at the plan datum
        // (assembly-model §1.4). A group along Z that holds one — which only a drag of a node or a
        // segment starts, since no place a node owns fixes Z — goes nowhere along Z, the way a
        // group holding an anchored entity goes nowhere.
        Vector3 applied = new(
            Blocked(sketch, alongX) ? Length.Zero : request.Delta.Dx,
            Blocked(sketch, alongY) ? Length.Zero : request.Delta.Dy,
            Blocked(sketch, alongZ) || alongZ.Any(id => sketch.Find(id) is Node) ? Length.Zero : request.Delta.Dz);

        Sketch result = sketch;
        ImmutableHashSet<EntityId>.Builder moved = ImmutableHashSet.CreateBuilder<EntityId>();

        foreach (EntityId id in alongX.Union(alongY).Union(alongZ).OrderBy(id => id))
        {
            Vector3 shift = new(
                alongX.Contains(id) ? applied.Dx : Length.Zero,
                alongY.Contains(id) ? applied.Dy : Length.Zero,
                alongZ.Contains(id) ? applied.Dz : Length.Zero);

            if (shift == Vector3.Zero)
            {
                continue;
            }

            result = Translate(result, id, shift);
            moved.Add(id);
        }

        // A drag that goes nowhere is not a conflict: it is a question that got the answer "no".
        AssertHolds(result);
        return new Solved(
            result,
            ChangeSet.Empty with { Moved = moved.ToImmutable(), AppliedDelta = applied });
    }

    private static bool Blocked(Sketch sketch, HashSet<EntityId> group) => group.Any(id => IsAnchored(sketch, id));

    private UpdateResult ApplyDragFace(Sketch sketch, DragFace request)
    {
        if (GeometryPrecondition(sketch) is { } precondition)
        {
            return new Rejected(precondition);
        }

        if (sketch.Find<Box>(request.Box) is not { } box)
        {
            // A strut has no resizable face: its length is derived, its cross-section typed.
            return new Rejected(sketch.Find(request.Box) is Strut ? RejectionReason.UnsupportedRequest : RejectionReason.UnknownEntity);
        }

        // The size along the local axis normal to the face changes (assembly-model §2.4).
        (Axis localAxis, ScalarKind sizeKind, ParamRef size, bool atOrigin) = request.Face switch
        {
            BoxFace.West => (Axis.X, ScalarKind.Width, (ParamRef)new BoxWidthRef(request.Box), true),
            BoxFace.East => (Axis.X, ScalarKind.Width, new BoxWidthRef(request.Box), false),
            BoxFace.South => (Axis.Y, ScalarKind.Height, new BoxHeightRef(request.Box), true),
            BoxFace.North => (Axis.Y, ScalarKind.Height, new BoxHeightRef(request.Box), false),
            BoxFace.Bottom => (Axis.Z, ScalarKind.Depth, new BoxDepthRef(request.Box), true),
            BoxFace.Top => (Axis.Z, ScalarKind.Depth, new BoxDepthRef(request.Box), false),
            _ => default,
        };

        if (size is null)
        {
            return new Rejected(RejectionReason.UnsupportedRequest);
        }

        // A drag never silently overrides a number the user typed.
        if (sketch.Relationships.Values.Any(relationship => relationship is ParamValue driven && driven.Param == size))
        {
            return new Rejected(RejectionReason.DrivenSize);
        }

        // Shaped parts §2.3: best effort, as always — the delta is clamped so a side face is never
        // dragged past what the cuts on it claim, and the applied delta is what gets reported. A
        // blank cannot be dragged shorter than its cuts, the way it cannot be dragged through an
        // anchored neighbour. The floor is zero for a plain rectangle, and for the bottom and top,
        // which a cut never reaches: every cut is square through the cap (assembly-model §4.2).
        Length currentSize = box.Size(localAxis);
        Length floor = localAxis == Axis.Z ? Length.Zero : CutRules.SmallestFitting(box, localAxis);
        Length newSize = Length.Max(currentSize + request.Delta, floor);
        if (newSize <= Length.Zero)
        {
            return new Rejected(RejectionReason.NonPositiveSize);
        }

        Length delta = newSize - currentSize;

        // Grabbing a face at the local origin — west, south, bottom — moves the anchor; grabbing
        // the far face leaves it. Either way the opposite face stays put, so all three anchor
        // coordinates are seeded and pinned. The shift is the local one turned into the world by
        // the box's orientation, the 3D form of "rotated by the box's rotation" (§2.4), so its
        // sign is right for a box whose local origin face points up or east.
        Vector3 anchorShift = atOrigin
            ? box.Orientation.Apply(Vector3.Along(localAxis, -delta))
            : Vector3.Zero;

        Point3 anchor = box.Anchor + anchorShift;

        ScalarKey sizeKey = new(request.Box, sizeKind);
        Dictionary<ScalarKey, Length> seeds = new()
        {
            [sizeKey] = newSize,
            [new ScalarKey(request.Box, ScalarKind.X)] = anchor.X,
            [new ScalarKey(request.Box, ScalarKind.Y)] = anchor.Y,
            [new ScalarKey(request.Box, ScalarKind.Z)] = anchor.Z,
        };

        // The handle the user grabbed is what they are editing, so Anchored stands aside for
        // everything this request seeds: the size, and the anchor corner that an origin face's
        // handle necessarily drags with it. Without the anchor, the east handle of an anchored box
        // would work and the west one would silently refuse (Fable review of #35, finding 8).
        HashSet<ScalarKey> owned =
        [
            sizeKey,
            new ScalarKey(request.Box, ScalarKind.X),
            new ScalarKey(request.Box, ScalarKind.Y),
            new ScalarKey(request.Box, ScalarKind.Z),
        ];

        if (Propagator.Run(sketch, seeds, sketch.Relationships.Values, owned) is not Propagated propagated)
        {
            // Best effort: a Flush to an anchored box blocks the face entirely, and the face then
            // does not move at all.
            return new Solved(sketch, ChangeSet.Empty with { AppliedDelta = Vector3.Zero });
        }

        Sketch written = Write(sketch, propagated.Assignments, out ImmutableHashSet<EntityId> moved, out ImmutableHashSet<EntityId> resized);
        AssertHolds(written);

        // The clamp above covers the box the user grabbed. Another box this resized through an
        // EqualParam has cuts of its own, and a drag is a question rather than a demand, so a
        // refusal there is the same answer the blocked-propagation arm gives: the face does not
        // move at all. Nothing partial is ever handed back.
        if (CutsStillFit(written, resized) is not null)
        {
            return new Solved(sketch, ChangeSet.Empty with { AppliedDelta = Vector3.Zero });
        }

        Length outward = atOrigin ? -delta : delta;
        Vector3 applied = box.Orientation.Apply(Vector3.Along(localAxis, outward));

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

        // A strut an end moved or a size changed must still be one (invariants 14–17), and every
        // relationship on it must still be one napkin can hold: a face it was flush to must still
        // be square to its axis, across an even size. Judged on the written sketch, before the
        // checker, which would otherwise be asked about a face that is square to nothing.
        if (StrutsStillHold(written, moved.Union(resized)) is { } strutRefusal)
        {
            return strutRefusal;
        }

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

    private static Rejected? StrutsStillHold(Sketch written, ImmutableHashSet<EntityId> changed)
    {
        foreach (EntityId id in changed.OrderBy(id => id))
        {
            if (written.Find<Strut>(id) is not { } strut)
            {
                continue;
            }

            if (StrutRefusal(strut) is { } refusal)
            {
                return refusal;
            }

            foreach (Relationship relationship in written.RelationshipsInOrder.Where(relationship => relationship.References.Contains(id)))
            {
                if (PlaceRules.Refusal(written, relationship) is { } notComparable)
                {
                    return new Rejected(RejectionReason.PlacesNotComparable, notComparable);
                }
            }
        }

        return null;
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
                    Point3 anchor = new(Value(ScalarKind.X), Value(ScalarKind.Y), Value(ScalarKind.Z));
                    Length width = Value(ScalarKind.Width);
                    Length height = Value(ScalarKind.Height);
                    Length depth = Value(ScalarKind.Depth);

                    if (anchor != box.Anchor)
                    {
                        movedBuilder.Add(id);
                    }

                    bool sizeChanged = width != box.Width || height != box.Height || depth != box.Depth;
                    if (sizeChanged)
                    {
                        resizedBuilder.Add(id);
                    }

                    if (anchor != box.Anchor || sizeChanged)
                    {
                        result = result.WithEntity(box with { Anchor = anchor, Width = width, Height = height, Depth = depth });
                    }

                    break;
                }

                case Strut strut:
                {
                    Point3 from = new(Value(ScalarKind.FromX), Value(ScalarKind.FromY), Value(ScalarKind.FromZ));
                    Point3 to = new(Value(ScalarKind.ToX), Value(ScalarKind.ToY), Value(ScalarKind.ToZ));
                    Length height = Value(ScalarKind.Height);
                    Length depth = Value(ScalarKind.Depth);
                    bool endsMoved = from != strut.From || to != strut.To;
                    bool sizeChanged = height != strut.Height || depth != strut.Depth;

                    if (endsMoved)
                    {
                        movedBuilder.Add(id);
                    }

                    if (sizeChanged)
                    {
                        resizedBuilder.Add(id);
                    }

                    if (endsMoved || sizeChanged)
                    {
                        result = result.WithEntity(strut with { From = from, To = to, Height = height, Depth = depth });
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

    /// <summary>
    /// Whether the propagator can hold this relationship: a kind it knows, on places it reads.
    /// </summary>
    /// <remarks>
    /// Legal and holdable were two questions until docs/design/assembly-model.md &#xA7;10 step 4
    /// gave the propagator its Z scalar and read a tipped box's depth where it stands. Now whatever
    /// <see cref="PlaceRules"/> calls legal — any common set of axes, any axis, any of the 24
    /// orientations — the propagator holds (&#xA7;3.1). What is left here is the kinds it does not
    /// propagate at all, a size that is not one number (a segment's length), and a flush on a
    /// segment that has stopped being axis-aligned.
    /// </remarks>
    private static bool CanPropagate(Sketch sketch, Relationship relationship) => relationship switch
    {
        // A joint is held by nothing and holds nothing: it neither propagates nor is a reason to
        // refuse a request (joinery note §4.3).
        Anchored or Coincident or AxisDistance or Centered or Joint or StrutJoint => true,
        ParamValue paramValue => IsOneNumber(paramValue.Param),
        EqualParam equalParam => IsOneNumber(equalParam.A) && IsOneNumber(equalParam.B),
        Horizontal horizontal => horizontal.Edge is SegmentRef,
        Vertical vertical => vertical.Edge is SegmentRef,
        Flush flush => FlushNormalAxis(sketch, flush) is not null,
        _ => false,
    };

    // A size the propagator holds as one scalar: a box's three, a strut's cross-section two. A
    // segment's length is not one number, and a strut's length is derived, so neither is here.
    private static bool IsOneNumber(ParamRef param)
        => param is BoxWidthRef or BoxHeightRef or BoxDepthRef or StrutHeightRef or StrutDepthRef;

    /// <summary>The one axis both places of a flush fix, or null when they do not share exactly one.</summary>
    private static Axis? FlushNormalAxis(Sketch sketch, Flush flush)
        => sketch.PlaceOf(flush.A).Axes is [var first] && sketch.PlaceOf(flush.B).Axes is [var second] && first == second
            ? first
            : null;

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
    /// vertical face couples X and not Y or Z; an <see cref="AxisDistance"/> along X couples X
    /// only; a <see cref="Coincident"/> couples the axes its two places share — X and Y for a node
    /// on a plan upright, all three for two vertices (docs/design/assembly-model.md &#xA7;2.1).
    /// <see cref="Horizontal"/> and <see cref="Vertical"/> are here too, although design
    /// &#xA7;4.4's list omits them: they tie a segment's two nodes together along one axis just as
    /// firmly, and a drag that ignored them would break them.
    /// </summary>
    private static IEnumerable<EntityId> CoupledOn(Sketch sketch, Relationship relationship, Axis axis)
        => relationship switch
        {
            Coincident coincident when Place.Common(sketch.PlaceOf(coincident.A), sketch.PlaceOf(coincident.B)).Contains(axis)
                => Movable(sketch, coincident.A.Owner).Concat(Movable(sketch, coincident.B.Owner)),

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

    // A node is only ever in a group along Z that goes nowhere (see ApplyDrag), so its XY is all of it.
    private static Sketch Translate(Sketch sketch, EntityId id, Vector3 shift) => sketch.Find(id) switch
    {
        Box box => sketch.WithEntity(box with { Anchor = box.Anchor + shift }),

        // Under a drag a strut moves as a unit, both ends (assembly-model §3a.5).
        Strut strut => sketch.WithEntity(strut with { From = strut.From + shift, To = strut.To + shift }),
        Node node when shift.XY != Vector2.Zero => sketch.WithEntity(node with { Position = node.Position + shift.XY }),
        _ => sketch,
    };

    internal static Sketch DemoteDimensions(
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

    internal static IEnumerable<EntityId> MeasurandEntities(Measurand measurand) => measurand switch
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
        Anchored anchored => sketch.Find(anchored.Entity) is Box or Node or Strut,
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
        Joint joint => ReferenceResolves(sketch, joint.Receiving) && ReferenceResolves(sketch, joint.Inserted),
        StrutJoint strutJoint => ReferenceResolves(sketch, strutJoint.Receiving) && ReferenceResolves(sketch, strutJoint.Inserted),
        _ => relationship.References.All(id => sketch.Find(id) is not null),
    };

    // A feature naming no faces is not a feature (docs/design/assembly-model.md invariant 12), and
    // there is nothing there to refer to.
    private static bool ReferenceResolves(Sketch sketch, PlaceRef reference) => sketch.TryPlaceOf(reference) is not null;

    private static bool ReferenceResolves(Sketch sketch, ParamRef reference) => reference switch
    {
        BoxWidthRef width => sketch.Find<Box>(width.Box) is not null,
        BoxHeightRef height => sketch.Find<Box>(height.Box) is not null,
        BoxDepthRef depth => sketch.Find<Box>(depth.Box) is not null,
        StrutHeightRef or StrutDepthRef => sketch.Find<Strut>(reference.Owner) is not null,
        SegmentLengthRef length => ReferenceResolves(sketch, (PlaceRef)new SegmentRef(length.Segment)),
        _ => false,
    };
}
