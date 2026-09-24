using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>What is wrong with a sketch.</summary>
public enum ValidationErrorKind
{
    /// <summary>An id referenced by a segment, dimension or relationship is not in the sketch.</summary>
    DanglingReference,

    /// <summary>A reference names an entity of the wrong kind — a corner of a node, say.</summary>
    WrongEntityKind,

    /// <summary>A box has a width or height that is not greater than zero.</summary>
    NonPositiveSize,

    /// <summary>Two relationships say the same thing about the same references.</summary>
    DuplicateRelationship,

    /// <summary>An entity is on a layer the sketch does not have.</summary>
    UnknownLayer,

    /// <summary>
    /// A box has more than one cut at one site — shaped-parts invariant 5.
    /// </summary>
    DuplicateCutSite,

    /// <summary>
    /// A curved edge claims a site another cut is at, or two adjacent edges are both curved —
    /// shaped-parts invariant 6.
    /// </summary>
    CutSiteTaken,

    /// <summary>
    /// A cut's value is not positive, does not fit its edge, or leaves no room for what is on the
    /// other end of its edge or across the blank — shaped-parts invariants 7 and 8.
    /// </summary>
    CutDoesNotFit,

    /// <summary>
    /// A box's cuts leave nothing of the blank — shaped-parts invariant 9.
    /// </summary>
    NonPositiveArea,

    /// <summary>
    /// A relationship pairs places that do not fix the axes it needs — a <see cref="Flush"/> between
    /// a face pointing up and one pointing north, a <see cref="Coincident"/> between a face and a
    /// vertex (<c>docs/design/assembly-model.md</c> &#xA7;2.3).
    /// </summary>
    PlacesNotComparable,

    /// <summary>A <see cref="Joint"/>'s own fields break a rule of the joinery note (&#xA7;3.3, &#xA7;4.4).</summary>
    InvalidJoint,

    /// <summary>
    /// A <see cref="FeatureRef"/> names no faces — <c>default(BoxFeature)</c> — which is not one,
    /// two or three mutually adjacent faces (<c>docs/design/assembly-model.md</c> invariant 12).
    /// </summary>
    NotAFeature,

    /// <summary>
    /// A dimension measures something that does not lie in the plan once the owning box's
    /// orientation is applied — a box's depth on a box lying as drawn, a width standing vertical, a
    /// span along Z (<c>docs/design/assembly-model.md</c> invariant 13, &#xA7;7.3).
    /// </summary>
    MeasurandLeavesThePlan,

    /// <summary>
    /// A relationship names a place of a box in the box's own frame, and turning the box would
    /// change which place in the world that is (<c>docs/design/assembly-model.md</c> &#xA7;2.4).
    /// Never a fault of a sketch, so <see cref="Sketch.Validate"/> never reports it: it is the
    /// <see cref="Rejected.Detail"/> of <see cref="RejectionReason.OrientationWithRelationships"/>,
    /// naming the relationship to remove, the way a cut refusal's detail names the cut.
    /// </summary>
    TurnWouldReinterpret,
}

/// <summary>One thing wrong with a sketch.</summary>
/// <param name="Kind">What kind of problem it is.</param>
/// <param name="Message">A description naming the ids involved.</param>
public sealed record ValidationError(ValidationErrorKind Kind, string Message);

/// <summary>The result of <see cref="Sketch.Validate"/>.</summary>
/// <param name="Errors">Everything wrong with the sketch; empty when it is valid.</param>
public sealed record ValidationResult(ImmutableList<ValidationError> Errors)
{
    /// <summary>A result with nothing wrong.</summary>
    public static readonly ValidationResult Valid = new(ImmutableList<ValidationError>.Empty);

    /// <summary>Whether the sketch is valid.</summary>
    public bool IsValid => Errors.IsEmpty;

    /// <summary>The errors, one per line, for an assertion or a log.</summary>
    public override string ToString()
        => Errors.IsEmpty ? "valid" : string.Join(Environment.NewLine, Errors.Select(e => $"{e.Kind}: {e.Message}"));
}

/// <summary>
/// The drawing: entities, the relationships that hold them together, and layers.
/// </summary>
/// <remarks>
/// <para>
/// The sketch is a value. Every update produces a new sketch; the previous one is untouched. That
/// decides three things at once: undo/redo (#11) is a stack of sketch values, the update
/// interface is a pure function, and a solver works on a copy by construction (design &#xA7;2.4).
/// </para>
/// <para>
/// Equality is structural — two sketches holding equal entities, relationships and layers are
/// equal — so that #6's <c>Load(Save(s)) == s</c> round trip is the one-line assertion the design
/// promises. Record equality alone would compare the immutable collections by reference.
/// </para>
/// </remarks>
/// <param name="Entities">Every entity, by id.</param>
/// <param name="Relationships">Every relationship, by id.</param>
/// <param name="Layers">The layers, in the order the UI shows them.</param>
public sealed record Sketch(
    ImmutableDictionary<EntityId, Entity> Entities,
    ImmutableDictionary<RelationshipId, Relationship> Relationships,
    ImmutableList<Layer> Layers)
{
    /// <summary>An empty sketch with one layer, <see cref="Layer.Default"/>.</summary>
    public static readonly Sketch Empty = new(
        ImmutableDictionary<EntityId, Entity>.Empty,
        ImmutableDictionary<RelationshipId, Relationship>.Empty,
        ImmutableList.Create(Layer.Default));

    /// <summary>The builder's typed fastener sizes (joinery note &#xA7;7.3), in the order typed.</summary>
    public ImmutableList<FastenerChoice> FastenerChoices { get; init; } = [];

    /// <summary>The builder's typed supplies checklist (&#xA7;8), in the order typed.</summary>
    public ImmutableList<SupplyLine> Supplies { get; init; } = [];

    /// <summary>
    /// Relationships in id order — never in dictionary order — so that anything iterating them is
    /// reproducible (design &#xA7;4.4 step 3).
    /// </summary>
    public IEnumerable<Relationship> RelationshipsInOrder
        => Relationships.Values.OrderBy(relationship => relationship.Id);

    /// <summary>This sketch with an entity added or replaced.</summary>
    public Sketch WithEntity(Entity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return this with { Entities = Entities.SetItem(entity.Id, entity) };
    }

    /// <summary>This sketch without an entity. Does not cascade; see the direct updater.</summary>
    public Sketch WithoutEntity(EntityId id) => this with { Entities = Entities.Remove(id) };

    /// <summary>This sketch with a relationship added or replaced.</summary>
    public Sketch WithRelationship(Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        return this with { Relationships = Relationships.SetItem(relationship.Id, relationship) };
    }

    /// <summary>This sketch without a relationship.</summary>
    public Sketch WithoutRelationship(RelationshipId id)
        => this with { Relationships = Relationships.Remove(id) };

    /// <summary>This sketch with a layer appended.</summary>
    public Sketch WithLayer(Layer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        return this with { Layers = Layers.Add(layer) };
    }

    /// <summary>The entity with this id, or <see langword="null"/>.</summary>
    public Entity? Find(EntityId id) => Entities.TryGetValue(id, out Entity? entity) ? entity : null;

    /// <summary>The entity with this id when it is a <typeparamref name="T"/>, or <see langword="null"/>.</summary>
    public T? Find<T>(EntityId id)
        where T : Entity
        => Find(id) as T;

    /// <summary>The relationship with this id, or <see langword="null"/>.</summary>
    public Relationship? Find(RelationshipId id)
        => Relationships.TryGetValue(id, out Relationship? relationship) ? relationship : null;

    /// <summary>
    /// What a reference fixes: a coordinate on each world axis it speaks about
    /// (<c>docs/design/assembly-model.md</c> &#xA7;2.2).
    /// </summary>
    /// <remarks>
    /// <para>
    /// A <see cref="FeatureRef"/> is read through the box's orientation: each face of the feature is
    /// perpendicular to the world axis <see cref="Orientation.Normal"/> gives it, and its coordinate
    /// there is the anchor's plus, for a face at the far end of its local axis
    /// (<see cref="BoxFace.East"/>, <see cref="BoxFace.North"/>, <see cref="BoxFace.Top"/>), the size
    /// along that axis with the sign the orientation gives (&#xA7;2.1). An anchor component plus or
    /// minus a stored size: exact for all 24 orientations. The feature is the blank's, so a cut never
    /// moves it (&#xA7;2.5).
    /// </para>
    /// <para>
    /// A box whose rotation is not a quarter turn — reachable only from a solver-written file — has
    /// side faces that are not axis-aligned. Its top and bottom as tipped still fix Z; an edge
    /// standing vertical still fixes X and Y, at a point that rounds as <see cref="Box.Vertex"/>
    /// does; one side face alone fixes nothing, because it is a slanted plane. That is the reading
    /// the checker's tolerance class needs, and the direct updater refuses such a sketch before it
    /// would ask.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The reference dangles, names the wrong kind of entity, or names no feature.</exception>
    public Place PlaceOf(PlaceRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        switch (reference)
        {
            case NodeRef nodeRef:
            {
                Point2 position = Require<Node>(nodeRef.Node, reference).Position;
                return new Place(position.X, position.Y, null);
            }

            case SegmentRef segmentRef:
            {
                (Point2 from, Point2 to) = SegmentEnds(segmentRef, reference);
                bool sameX = from.X == to.X;
                bool sameY = from.Y == to.Y;
                return (sameX, sameY) switch
                {
                    (true, false) => Place.On(Axis.X, from.X),
                    (false, true) => Place.On(Axis.Y, from.Y),

                    // A diagonal is not axis-aligned, and a segment with no length has no direction.
                    _ => default,
                };
            }

            case CenterRef centre:
            {
                Point3 at = Require<Box>(centre.Box, reference).Center;
                return new Place(at.X, at.Y, at.Z);
            }

            case FeatureRef featureRef:
                return FeaturePlace(Require<Box>(featureRef.Box, reference), featureRef.Feature, reference);

            default:
                throw new InvalidOperationException($"Unknown place reference {reference}.");
        }
    }

    /// <summary>
    /// Where a place is in the plan, when it fixes both X and Y — a node, a centre, a vertex, an edge
    /// standing vertical — or <see langword="null"/> when it does not. What the solver-reserved kinds
    /// that measure between plan points (<see cref="Distance"/>, <see cref="Symmetric"/>) read.
    /// </summary>
    internal Point2? PlanPointOf(PlaceRef reference)
        => PlaceOf(reference) is { X: { } x, Y: { } y } ? new Point2(x, y) : null;

    /// <summary>
    /// The line a place draws in the plan, as two points on it — a segment's two nodes, or the two
    /// footprint corners a side face is seen between from above — or <see langword="null"/> when it
    /// draws none. What the checker's tolerance class measures a slanted <see cref="Flush"/> and the
    /// solver-reserved angular kinds against, where a <see cref="Place"/> has nothing to say.
    /// </summary>
    internal (Point2 From, Point2 To)? PlanLineOf(PlaceRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        switch (reference)
        {
            case SegmentRef segmentRef:
                return SegmentEnds(segmentRef, reference);

            case FeatureRef featureRef when featureRef.Feature.Faces is [var face]:
            {
                Footprint footprint = Require<Box>(featureRef.Box, reference).Footprint();
                if (footprint.SideOf(face) is not { } side)
                {
                    return null;
                }

                (BoxCorner from, BoxCorner to) = Box.Ends(side);
                return (footprint.Corner(from), footprint.Corner(to));
            }

            default:
                return null;
        }
    }

    private (Point2 From, Point2 To) SegmentEnds(SegmentRef segmentRef, object reference)
    {
        Segment segment = Require<Segment>(segmentRef.Segment, reference);
        return (Require<Node>(segment.Start, reference).Position, Require<Node>(segment.End, reference).Position);
    }

    private static Place FeaturePlace(Box box, BoxFeature feature, PlaceRef reference)
    {
        ImmutableArray<BoxFace> faces = feature.Faces;
        if (faces.IsEmpty)
        {
            throw new InvalidOperationException(
                $"{reference} names no faces, which is not a feature (assembly-model invariant 12). "
                + "Validate the sketch before evaluating its geometry.");
        }

        // One local point on every face of the feature: at the far end of a local axis for East,
        // North and Top, at the near end otherwise. The local axes the feature does not fix stay at
        // zero, and a signed permutation never carries them onto an axis it does.
        Vector3 local = Vector3.Zero;
        foreach (BoxFace face in faces)
        {
            local = face switch
            {
                BoxFace.East => local.WithComponent(Axis.X, box.Width),
                BoxFace.North => local.WithComponent(Axis.Y, box.Height),
                BoxFace.Top => local.WithComponent(Axis.Z, box.Depth),
                _ => local,
            };
        }

        Point3 world = box.World(local);
        Place place = default;

        if (box.Orientation.IsExact)
        {
            foreach (BoxFace face in faces)
            {
                Axis axis = box.Orientation.Normal(face).Axis;
                place = place.With(axis, world.Component(axis));
            }

            return place;
        }

        // Off the quarter turns: the tip is still exact and the spin is about Z, so a face whose
        // tipped normal is vertical still fixes Z, and two side faces together are an edge standing
        // vertical, which fixes X and Y at a rounded point. One side face alone is a slanted plane.
        Orientation tip = new(box.FaceUp, Angle.Zero);
        int sides = 0;
        foreach (BoxFace face in faces)
        {
            if (tip.Normal(face).Axis == Axis.Z)
            {
                place = place.With(Axis.Z, world.Z);
            }
            else
            {
                sides++;
            }
        }

        return sides == 2 ? place.With(Axis.X, world.X).With(Axis.Y, world.Y) : place;
    }

    /// <summary>The current value of a referenced size.</summary>
    /// <remarks>
    /// A segment's length is Euclidean, so it is exact only when the segment is axis-aligned.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The reference dangles or names the wrong kind of entity.</exception>
    public Length ValueOf(ParamRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        switch (reference)
        {
            case BoxWidthRef width:
                return Require<Box>(width.Box, reference).Width;

            case BoxHeightRef height:
                return Require<Box>(height.Box, reference).Height;

            case BoxDepthRef depth:
                return Require<Box>(depth.Box, reference).Depth;

            case SegmentLengthRef length:
            {
                (Point2 from, Point2 to) = SegmentEnds(new SegmentRef(length.Segment), reference);
                return (to - from).Magnitude();
            }

            default:
                throw new InvalidOperationException($"Unknown size reference {reference}.");
        }
    }

    /// <summary>
    /// Checks referential integrity, positive sizes and duplicate relationships — invariants 1, 2
    /// and 4 of design &#xA7;2.5 — a box's cuts against invariants 5 to 9 of
    /// <c>docs/design/shaped-parts-model.md</c> &#xA7;1.6, and invariants 10, 12 and 13 of
    /// <c>docs/design/assembly-model.md</c> &#xA7;1.6 with the legality of each relationship's
    /// places (&#xA7;2.3, <see cref="PlaceRules"/>). Invariant 3, that every relationship holds, is
    /// <see cref="RelationshipChecker.Check(Sketch)"/>.
    /// </summary>
    public ValidationResult Validate()
    {
        ImmutableList<ValidationError>.Builder errors = ImmutableList.CreateBuilder<ValidationError>();
        HashSet<LayerId> layerIds = [.. Layers.Select(layer => layer.Id)];

        foreach (Entity entity in Entities.Values.OrderBy(entity => entity.Id))
        {
            if (!layerIds.Contains(entity.Layer))
            {
                errors.Add(new ValidationError(
                    ValidationErrorKind.UnknownLayer,
                    $"Entity {entity.Id} is on layer {entity.Layer}, which the sketch does not have."));
            }

            switch (entity)
            {
                case Box box:
                    if (box.Width <= Length.Zero || box.Height <= Length.Zero)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorKind.NonPositiveSize,
                            $"Box {box.Id} is {box.Width} by {box.Height}; both must be greater than zero."));
                    }

                    // docs/design/assembly-model.md §1.6, invariant 10.
                    if (box.Depth <= Length.Zero)
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorKind.NonPositiveSize,
                            $"Box {box.Id} is {box.Depth} deep; its depth must be greater than zero."));
                    }

                    foreach (ValidationError error in CutRules.Errors(box))
                    {
                        errors.Add(error);
                    }

                    break;

                case Segment segment:
                    RequireEntity<Node>(segment.Start, $"Segment {segment.Id} starts at", errors);
                    RequireEntity<Node>(segment.End, $"Segment {segment.Id} ends at", errors);
                    break;

                case Dimension dimension:
                    List<ValidationError> measurandErrors = [.. MeasurandErrors(dimension)];
                    foreach (ValidationError error in measurandErrors)
                    {
                        errors.Add(error);
                    }

                    // Invariant 13, judged only on a measurand that resolves.
                    if (measurandErrors.Count == 0 && PlaceRules.MeasurandRefusal(this, dimension) is { } leaves)
                    {
                        errors.Add(leaves);
                    }

                    if (dimension.Drives is { } driving && !Relationships.ContainsKey(driving))
                    {
                        errors.Add(new ValidationError(
                            ValidationErrorKind.DanglingReference,
                            $"Dimension {dimension.Id} is driven by relationship {driving}, which the sketch does not have."));
                    }

                    break;
            }
        }

        List<Relationship> inOrder = [.. RelationshipsInOrder];
        foreach (Relationship relationship in inOrder)
        {
            List<ValidationError> referenceErrors = [.. ReferenceErrors(relationship)];
            foreach (ValidationError error in referenceErrors)
            {
                errors.Add(error);
            }

            // §2.3's legality. A dangling reference has already been reported and has no place to
            // compare; the rule itself skips any place it cannot read.
            if (referenceErrors.Count == 0 && PlaceRules.Refusal(this, relationship) is { } refusal)
            {
                errors.Add(refusal);
            }

            if (relationship is Joint joint)
            {
                foreach (string problem in JointRules.Errors(joint))
                {
                    errors.Add(new ValidationError(ValidationErrorKind.InvalidJoint, problem));
                }
            }
        }

        for (int i = 0; i < inOrder.Count; i++)
        {
            for (int j = i + 1; j < inOrder.Count; j++)
            {
                if (Relationship.AreStructurallyIdentical(inOrder[i], inOrder[j]))
                {
                    errors.Add(new ValidationError(
                        ValidationErrorKind.DuplicateRelationship,
                        $"Relationships {inOrder[i].Id} and {inOrder[j].Id} say the same thing."));
                }
            }
        }

        return errors.Count == 0 ? ValidationResult.Valid : new ValidationResult(errors.ToImmutable());
    }

    /// <inheritdoc/>
    public bool Equals(Sketch? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null
            || Entities.Count != other.Entities.Count
            || Relationships.Count != other.Relationships.Count
            || Layers.Count != other.Layers.Count)
        {
            return false;
        }

        foreach (KeyValuePair<EntityId, Entity> entry in Entities)
        {
            if (!other.Entities.TryGetValue(entry.Key, out Entity? theirs) || !entry.Value.Equals(theirs))
            {
                return false;
            }
        }

        foreach (KeyValuePair<RelationshipId, Relationship> entry in Relationships)
        {
            if (!other.Relationships.TryGetValue(entry.Key, out Relationship? theirs) || !entry.Value.Equals(theirs))
            {
                return false;
            }
        }

        return Layers.SequenceEqual(other.Layers)
               && FastenerChoices.SequenceEqual(other.FastenerChoices)
               && Supplies.SequenceEqual(other.Supplies);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = Entities.Count * 397 ^ Relationships.Count;
        foreach (KeyValuePair<EntityId, Entity> entry in Entities)
        {
            hash ^= entry.Value.GetHashCode();
        }

        foreach (KeyValuePair<RelationshipId, Relationship> entry in Relationships)
        {
            hash ^= entry.Value.GetHashCode();
        }

        foreach (Layer layer in Layers)
        {
            hash ^= layer.GetHashCode();
        }

        return hash;
    }

    private IEnumerable<ValidationError> MeasurandErrors(Dimension dimension)
    {
        string what = $"Dimension {dimension.Id} measures";

        return dimension.Measures switch
        {
            ParamMeasurand param => ReferenceErrors(param.Param, what),
            AxisMeasurand axis => ReferenceErrors(axis.From, what).Concat(ReferenceErrors(axis.To, what)),
            _ => [],
        };
    }

    /// <summary>
    /// Every way a relationship's references can fail to resolve: an id the sketch does not have,
    /// or an id that names the wrong kind of entity — a <see cref="FeatureRef"/> on a node, say.
    /// Both are referential integrity, and #6's loader needs both, because
    /// <see cref="RelationshipChecker"/> runs straight after <see cref="Validate"/> and would
    /// throw rather than report.
    /// </summary>
    private IEnumerable<ValidationError> ReferenceErrors(Relationship relationship)
    {
        string what = $"Relationship {relationship.Id} references";

        return relationship switch
        {
            // Only a box or a node has a position of its own to hold still.
            Anchored anchored => KindErrors(anchored.Entity, what, entity => entity is Box or Node, "Box or Node"),
            Coincident coincident => ReferenceErrors(coincident.A, what).Concat(ReferenceErrors(coincident.B, what)),
            Horizontal horizontal => ReferenceErrors(horizontal.Edge, what),
            Vertical vertical => ReferenceErrors(vertical.Edge, what),
            Flush flush => ReferenceErrors(flush.A, what).Concat(ReferenceErrors(flush.B, what)),
            AxisDistance distance => ReferenceErrors(distance.From, what).Concat(ReferenceErrors(distance.To, what)),
            ParamValue paramValue => ReferenceErrors(paramValue.Param, what),
            EqualParam equalParam => ReferenceErrors(equalParam.A, what).Concat(ReferenceErrors(equalParam.B, what)),
            Centered centered => ReferenceErrors(centered.Middle, what)
                .Concat(ReferenceErrors(centered.A, what))
                .Concat(ReferenceErrors(centered.B, what)),
            Parallel parallel => ReferenceErrors(parallel.A, what).Concat(ReferenceErrors(parallel.B, what)),
            Perpendicular perpendicular => ReferenceErrors(perpendicular.A, what).Concat(ReferenceErrors(perpendicular.B, what)),
            AngleBetween angle => ReferenceErrors(angle.A, what).Concat(ReferenceErrors(angle.B, what)),
            Distance distance => ReferenceErrors(distance.A, what).Concat(ReferenceErrors(distance.B, what)),
            PointOnEdge onEdge => ReferenceErrors(onEdge.Point, what).Concat(ReferenceErrors(onEdge.Edge, what)),
            Symmetric symmetric => ReferenceErrors(symmetric.A, what)
                .Concat(ReferenceErrors(symmetric.B, what))
                .Concat(ReferenceErrors(symmetric.Mirror, what)),
            Tangent tangent => ReferenceErrors(tangent.A, what).Concat(ReferenceErrors(tangent.B, what)),
            Joint joint => ReferenceErrors(joint.Receiving, what).Concat(ReferenceErrors(joint.Inserted, what)),

            // Radius names an arc, and there are no arcs yet; any entity it names must at least exist.
            _ => relationship.References.SelectMany(id => KindErrors(id, what, _ => true, "Entity")),
        };
    }

    private IEnumerable<ValidationError> ReferenceErrors(PlaceRef reference, string what) => reference switch
    {
        NodeRef node => KindErrors(node.Node, what, entity => entity is Node, nameof(Node)),
        SegmentRef segmentRef => KindErrors(segmentRef.Segment, what, entity => entity is Segment, nameof(Segment)),
        CenterRef centre => KindErrors(centre.Box, what, entity => entity is Box, nameof(Box)),
        FeatureRef feature => KindErrors(feature.Box, what, entity => entity is Box, nameof(Box))
            .Concat(FeatureErrors(feature, what)),
        _ => [],
    };

    /// <summary>
    /// What a reference fixes, or <see langword="null"/> when it cannot be read — it dangles, names
    /// the wrong kind of entity, or names no feature. For the legality rules, which run inside
    /// <see cref="Validate"/> and must report rather than throw.
    /// </summary>
    internal Place? TryPlaceOf(PlaceRef reference)
    {
        bool resolves = reference switch
        {
            NodeRef node => Find<Node>(node.Node) is not null,
            SegmentRef segmentRef => Find<Segment>(segmentRef.Segment) is { } segment
                                     && Find<Node>(segment.Start) is not null
                                     && Find<Node>(segment.End) is not null,
            CenterRef centre => Find<Box>(centre.Box) is not null,
            FeatureRef feature => Find<Box>(feature.Box) is not null && !feature.Feature.Faces.IsEmpty,
            _ => false,
        };

        return resolves ? PlaceOf(reference) : null;
    }

    /// <summary>
    /// Invariant 12 (<c>docs/design/assembly-model.md</c> &#xA7;1.6): a feature names one, two or
    /// three mutually adjacent faces, in <see cref="BoxFace"/> order. <see cref="BoxFeature"/>'s
    /// factories cannot build anything else, and its one field is the set of faces, which it lists
    /// in that order, so the only feature that can reach a sketch and break the invariant is
    /// <c>default(BoxFeature)</c>, which names none.
    /// </summary>
    private static IEnumerable<ValidationError> FeatureErrors(FeatureRef reference, string what)
    {
        if (reference.Feature.Faces.IsEmpty)
        {
            yield return new ValidationError(
                ValidationErrorKind.NotAFeature,
                $"{what} a feature of box {reference.Box} that names no faces; a feature is one face, two adjacent faces or three.");
        }
    }

    private IEnumerable<ValidationError> ReferenceErrors(ParamRef reference, string what) => reference switch
    {
        BoxWidthRef width => KindErrors(width.Box, what, entity => entity is Box, nameof(Box)),
        BoxHeightRef height => KindErrors(height.Box, what, entity => entity is Box, nameof(Box)),
        BoxDepthRef depth => KindErrors(depth.Box, what, entity => entity is Box, nameof(Box)),
        SegmentLengthRef length => KindErrors(length.Segment, what, entity => entity is Segment, nameof(Segment)),
        _ => [],
    };

    private IEnumerable<ValidationError> KindErrors(EntityId id, string what, Func<Entity, bool> expected, string kind)
    {
        if (!Entities.TryGetValue(id, out Entity? entity))
        {
            yield return new ValidationError(
                ValidationErrorKind.DanglingReference,
                $"{what} entity {id}, which the sketch does not have.");
        }
        else if (!expected(entity))
        {
            yield return new ValidationError(
                ValidationErrorKind.WrongEntityKind,
                $"{what} entity {id} as a {kind}, but it is a {entity.GetType().Name}.");
        }
    }

    private void RequireEntity<T>(EntityId id, string what, ImmutableList<ValidationError>.Builder errors)
        where T : Entity
    {
        if (!Entities.TryGetValue(id, out Entity? entity))
        {
            errors.Add(new ValidationError(
                ValidationErrorKind.DanglingReference,
                $"{what} entity {id}, which the sketch does not have."));
        }
        else if (entity is not T)
        {
            errors.Add(new ValidationError(
                ValidationErrorKind.WrongEntityKind,
                $"{what} entity {id}, which is a {entity.GetType().Name} and not a {typeof(T).Name}."));
        }
    }

    private T Require<T>(EntityId id, object reference)
        where T : Entity
        => Find<T>(id)
           ?? throw new InvalidOperationException(
               $"{reference} names entity {id}, which is missing or is not a {typeof(T).Name}. "
               + "Validate the sketch before evaluating its geometry.");
}
