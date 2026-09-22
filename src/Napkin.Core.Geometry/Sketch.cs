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

    /// <summary>Where a referenced point is.</summary>
    /// <exception cref="InvalidOperationException">The reference dangles or names the wrong kind of entity.</exception>
    public Point2 PointOf(PointRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return reference switch
        {
            NodeRef node => Require<Node>(node.Node, reference).Position,
            CornerRef corner => Require<Box>(corner.Box, reference).Corner(corner.Corner),
            CenterRef centre => Require<Box>(centre.Box, reference).Center,
            _ => throw new InvalidOperationException($"Unknown point reference {reference}."),
        };
    }

    /// <summary>The two ends of a referenced edge.</summary>
    /// <exception cref="InvalidOperationException">The reference dangles or names the wrong kind of entity.</exception>
    public (Point2 From, Point2 To) EdgeOf(EdgeRef reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        switch (reference)
        {
            case SegmentRef segmentRef:
            {
                Segment segment = Require<Segment>(segmentRef.Segment, reference);
                return (Require<Node>(segment.Start, reference).Position, Require<Node>(segment.End, reference).Position);
            }

            case BoxEdgeRef boxEdge:
            {
                Box box = Require<Box>(boxEdge.Box, reference);
                (BoxCorner from, BoxCorner to) = Box.Ends(boxEdge.Edge);
                return (box.Corner(from), box.Corner(to));
            }

            default:
                throw new InvalidOperationException($"Unknown edge reference {reference}.");
        }
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

            case SegmentLengthRef length:
            {
                (Point2 from, Point2 to) = EdgeOf(new SegmentRef(length.Segment));
                return (to - from).Magnitude();
            }

            default:
                throw new InvalidOperationException($"Unknown size reference {reference}.");
        }
    }

    /// <summary>
    /// Checks referential integrity, positive sizes and duplicate relationships — invariants 1, 2
    /// and 4 of design &#xA7;2.5 — and a box's cuts against invariants 5 to 9 of
    /// <c>docs/design/shaped-parts-model.md</c> &#xA7;1.6. Invariant 3, that every relationship
    /// holds, is <see cref="RelationshipChecker.Check(Sketch)"/>.
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
                    foreach (ValidationError error in MeasurandErrors(dimension))
                    {
                        errors.Add(error);
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
            foreach (ValidationError error in ReferenceErrors(relationship))
            {
                errors.Add(error);
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

        return Layers.SequenceEqual(other.Layers);
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
    /// or an id that names the wrong kind of entity — a <see cref="CornerRef"/> on a node, say.
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

            // Radius names an arc, and there are no arcs yet; any entity it names must at least exist.
            _ => relationship.References.SelectMany(id => KindErrors(id, what, _ => true, "Entity")),
        };
    }

    private IEnumerable<ValidationError> ReferenceErrors(PointRef reference, string what) => reference switch
    {
        NodeRef node => KindErrors(node.Node, what, entity => entity is Node, nameof(Node)),
        CornerRef corner => KindErrors(corner.Box, what, entity => entity is Box, nameof(Box)),
        CenterRef centre => KindErrors(centre.Box, what, entity => entity is Box, nameof(Box)),
        _ => [],
    };

    private IEnumerable<ValidationError> ReferenceErrors(EdgeRef reference, string what) => reference switch
    {
        BoxEdgeRef boxEdge => KindErrors(boxEdge.Box, what, entity => entity is Box, nameof(Box)),
        SegmentRef segmentRef => KindErrors(segmentRef.Segment, what, entity => entity is Segment, nameof(Segment)),
        _ => [],
    };

    private IEnumerable<ValidationError> ReferenceErrors(ParamRef reference, string what) => reference switch
    {
        BoxWidthRef width => KindErrors(width.Box, what, entity => entity is Box, nameof(Box)),
        BoxHeightRef height => KindErrors(height.Box, what, entity => entity is Box, nameof(Box)),
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
