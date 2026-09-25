namespace Napkin.Core.Geometry;

/// <summary>
/// A statement the sketch holds true about its geometry.
/// </summary>
/// <remarks>
/// <para>
/// Relationships are stored, never inferred from position. Two corners at the same coordinates
/// are not coincident unless a <see cref="Coincident"/> says so; two equal widths are not
/// <see cref="EqualParam"/> unless one says so. The canvas (#10) creates relationships when the
/// user snaps, and shows them (design &#xA7;3.2).
/// </para>
/// <para>
/// Rectilinearity is not a relationship; it is a property of <see cref="Box.Rotation"/>. A box
/// rotated by a right-angle multiple has axis-aligned edges by construction.
/// </para>
/// </remarks>
/// <param name="Id">The relationship's identity, and the order relationships are iterated in.</param>
public abstract record Relationship(RelationshipId Id)
{
    /// <summary>
    /// Every entity this relationship refers to. The one place the reference shape of each kind
    /// is written down: validation, the remove cascade, the propagator's index, drag groups and
    /// conflict reports all read it from here.
    /// </summary>
    public abstract IEnumerable<EntityId> References { get; }

    /// <summary>
    /// Whether two relationships say the same thing about the same references, ignoring their
    /// ids. Invariant 4 (&#xA7;2.5) forbids two structurally identical relationships in a sketch.
    /// </summary>
    /// <remarks>
    /// The stated value counts: two <see cref="ParamValue"/>s on the same size with different
    /// values are <em>not</em> structurally identical, because that is a genuine contradiction the
    /// updater should report rather than a duplicate it should refuse. Two that agree are a
    /// duplicate — one owner per number (&#xA7;3.3). Symmetric kinds written the other way round
    /// (<c>Coincident(a, b)</c> against <c>Coincident(b, a)</c>) are not detected as duplicates.
    /// </remarks>
    public static bool AreStructurallyIdentical(Relationship a, Relationship b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return (a with { Id = default }) == (b with { Id = default });
    }
}

// ---------------------------------------------------------------------------------------------
// The rectilinear set: what the direct updater handles in the first beta (design §3.2, table 1).
// ---------------------------------------------------------------------------------------------

/// <summary>The entity does not move in response to other entities.</summary>
/// <remarks>
/// This pins the entity's position. It does not pin its size: see the implementation notes in
/// docs/design/geometry-model.md &#xA7;10.
/// </remarks>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Entity">The entity held in place.</param>
public sealed record Anchored(RelationshipId Id, EntityId Entity) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Entity];
}

/// <summary>
/// Two places are the same place: equal on every axis both fix — two vertices, two parallel edges,
/// a node and an upright edge, a centre and a vertex. Legal when the places have two or three axes
/// in common (<c>docs/design/assembly-model.md</c> &#xA7;2.3).
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first point.</param>
/// <param name="B">The second point.</param>
public sealed record Coincident(RelationshipId Id, PlaceRef A, PlaceRef B) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>A segment is axis-aligned along X.</summary>
/// <remarks>Box edges are axis-aligned by rotation; this is for segments.</remarks>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Edge">The edge held horizontal.</param>
public sealed record Horizontal(RelationshipId Id, PlaceRef Edge) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Edge.Owner];
}

/// <summary>A segment is axis-aligned along Y.</summary>
/// <remarks>Box edges are axis-aligned by rotation; this is for segments.</remarks>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Edge">The edge held vertical.</param>
public sealed record Vertical(RelationshipId Id, PlaceRef Edge) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Edge.Owner];
}

/// <summary>
/// Two places lie in the same plane — two faces coplanar, a shelf flush with a side, or a face and an
/// axis-aligned segment. Legal when both fix exactly one axis, the same one
/// (<c>docs/design/assembly-model.md</c> &#xA7;2.3).
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first edge.</param>
/// <param name="B">The second edge, which follows the first.</param>
public sealed record Flush(RelationshipId Id, PlaceRef A, PlaceRef B) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>
/// The signed distance between two places along X, Y or Z. This is what a driving linear dimension
/// between two places is. Legal when both places fix <see cref="Axis"/>
/// (<c>docs/design/assembly-model.md</c> &#xA7;2.3).
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="From">The place measured from.</param>
/// <param name="To">The place measured to, which follows.</param>
/// <param name="Axis">The axis measured along.</param>
/// <param name="Distance">The signed distance, <c>To - From</c> along <paramref name="Axis"/>.</param>
public sealed record AxisDistance(RelationshipId Id, PlaceRef From, PlaceRef To, Axis Axis, Length Distance)
    : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [From.Owner, To.Owner];
}

/// <summary>
/// A box width, height or depth, or a segment length, is a given value. This is what a driving dimension
/// on a part's size is, and it is the one owner of that number.
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Param">The size held.</param>
/// <param name="Value">The value it is held at.</param>
public sealed record ParamValue(RelationshipId Id, ParamRef Param, Length Value) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Param.Owner];
}

/// <summary>Two sizes are equal — four identical legs.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first size.</param>
/// <param name="B">The second size, which follows the first.</param>
public sealed record EqualParam(RelationshipId Id, ParamRef A, ParamRef B) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>
/// The first place is midway between the other two along an axis. Legal when all three fix that
/// axis (<c>docs/design/assembly-model.md</c> &#xA7;2.3).
/// </summary>
/// <remarks>
/// Exact when the span is an even number of units; otherwise the midpoint rounds half to even by
/// half a unit, and the checker measures against that same rounded midpoint.
/// </remarks>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Middle">The place held at the midpoint.</param>
/// <param name="A">One end of the span.</param>
/// <param name="B">The other end of the span.</param>
/// <param name="Axis">The axis the midpoint is taken along.</param>
public sealed record Centered(RelationshipId Id, PlaceRef Middle, PlaceRef A, PlaceRef B, Axis Axis)
    : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Middle.Owner, A.Owner, B.Owner];
}

// ---------------------------------------------------------------------------------------------
// Reserved for the solver (#28). These kinds exist from #5 so that the file format and the UI
// have names for them, but the direct updater returns Rejected(UnsupportedRelationship) for any
// request that adds one (design §3.2, table 2).
// ---------------------------------------------------------------------------------------------

/// <summary>Two edges that are not both axis-aligned run in the same direction. Reserved for the solver.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first edge.</param>
/// <param name="B">The second edge.</param>
public sealed record Parallel(RelationshipId Id, PlaceRef A, PlaceRef B) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>Two edges meet at a right angle. Reserved for the solver.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first edge.</param>
/// <param name="B">The second edge.</param>
public sealed record Perpendicular(RelationshipId Id, PlaceRef A, PlaceRef B) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>A miter: two edges meet at a stated angle. Reserved for the solver.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first edge.</param>
/// <param name="B">The second edge.</param>
/// <param name="Angle">The angle from <paramref name="A"/> to <paramref name="B"/>.</param>
public sealed record AngleBetween(RelationshipId Id, PlaceRef A, PlaceRef B, Angle Angle) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>Euclidean distance between two points, not along an axis. Reserved for the solver.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first point.</param>
/// <param name="B">The second point.</param>
/// <param name="Value">The distance between them.</param>
public sealed record Distance(RelationshipId Id, PlaceRef A, PlaceRef B, Length Value) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>A point lies somewhere on an edge. Reserved for the solver.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Point">The point.</param>
/// <param name="Edge">The edge it lies on.</param>
public sealed record PointOnEdge(RelationshipId Id, PlaceRef Point, PlaceRef Edge) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Point.Owner, Edge.Owner];
}

/// <summary>Two points are mirror images across an edge. Reserved for the solver.</summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first point.</param>
/// <param name="B">The second point.</param>
/// <param name="Mirror">The edge they are mirrored across.</param>
public sealed record Symmetric(RelationshipId Id, PlaceRef A, PlaceRef B, PlaceRef Mirror) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner, Mirror.Owner];
}

/// <summary>
/// Two edges touch without crossing. Reserved for the solver, and meaningful only once arcs
/// exist; see docs/design/geometry-model.md &#xA7;10.
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="A">The first edge.</param>
/// <param name="B">The second edge.</param>
public sealed record Tangent(RelationshipId Id, PlaceRef A, PlaceRef B) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [A.Owner, B.Owner];
}

/// <summary>
/// An arc has a stated radius. Reserved for the solver, and meaningful only once arcs exist; see
/// docs/design/geometry-model.md &#xA7;10.
/// </summary>
/// <param name="Id">The relationship's identity.</param>
/// <param name="Arc">The entity whose radius is stated.</param>
/// <param name="Value">The radius.</param>
public sealed record Radius(RelationshipId Id, EntityId Arc, Length Value) : Relationship(Id)
{
    /// <inheritdoc/>
    public override IEnumerable<EntityId> References => [Arc];
}
