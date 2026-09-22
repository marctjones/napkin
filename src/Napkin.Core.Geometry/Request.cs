using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// Something the user asked for, put to an <see cref="IGeometryUpdater"/>.
/// </summary>
/// <remarks>
/// Two semantics, chosen to match what the user meant (design &#xA7;4.1). <em>Exact</em> requests
/// — typing a dimension, adding a relationship — either produce a sketch in which the request
/// holds exactly, or <see cref="OverConstrained"/>: typing "30" and getting 29 15/16 is never
/// acceptable. <em>Best-effort</em> requests — dragging — move the entity as close to the target
/// as its relationships allow and report the delta actually applied.
/// </remarks>
public abstract record Request
{
    private protected Request()
    {
    }
}

/// <summary>Adds an entity. Exact.</summary>
/// <param name="Entity">The entity to add.</param>
public sealed record AddEntity(Entity Entity) : Request;

/// <summary>
/// Removes an entity, cascading to what referenced it. Exact.
/// </summary>
/// <remarks>
/// Relationships that reference the entity are removed; segments that used it as an endpoint are
/// removed, and then what referenced those; dimensions whose measurand references it are removed.
/// A dimension whose <em>driving</em> relationship was removed but whose measurand survives
/// becomes a reference dimension rather than disappearing (design &#xA7;4.4).
/// </remarks>
/// <param name="Id">The entity to remove.</param>
public sealed record RemoveEntity(EntityId Id) : Request;

/// <summary>
/// Adds a relationship, moving geometry to satisfy it. Exact.
/// </summary>
/// <remarks>
/// Typing a size on a box that nothing drives is a stated fact, so it is
/// <c>AddRelationship(ParamValue(BoxWidthRef b, v))</c> rather than poking the box's field. If a
/// <see cref="ParamValue"/> already drives that size, the same edit is <see cref="SetParameter"/>
/// on it (design &#xA7;4.1).
/// </remarks>
/// <param name="Relationship">The relationship to add.</param>
public sealed record AddRelationship(Relationship Relationship) : Request;

/// <summary>
/// Removes a relationship. Exact, and never moves anything: the geometry stays where it is, it is
/// simply no longer held there. A dimension the relationship drove becomes a reference dimension.
/// </summary>
/// <param name="Id">The relationship to remove.</param>
public sealed record RemoveRelationship(RelationshipId Id) : Request;

/// <summary>Moves an entity to another layer. Exact, and never moves geometry.</summary>
/// <param name="Id">The entity.</param>
/// <param name="Layer">The layer to put it on.</param>
public sealed record SetLayer(EntityId Id, LayerId Layer) : Request;

/// <summary>Renames an entity. Exact, and never moves geometry.</summary>
/// <remarks>
/// A name is not an id: nothing looks an entity up by one, two entities may share one, and an
/// empty name is a legal "unnamed". Renaming therefore cannot fail for any reason but the entity
/// not being there.
/// </remarks>
/// <param name="Id">The entity to rename.</param>
/// <param name="Name">What to call it. An empty string means unnamed.</param>
public sealed record SetName(EntityId Id, string Name) : Request;

/// <summary>
/// Makes a box a part, changes what kind of part it is, or stops it being one. Exact, and never
/// moves geometry.
/// </summary>
/// <remarks>
/// This is the one thing that can turn a box somebody drew into a piece somebody cuts. It changes
/// no dimension: the two in-plan dimensions are the box's own and stay exactly as they are, and
/// the part supplies only the third and the names for all three
/// (<c>docs/design/parts-and-cut-list.md</c> §1.1). Assigning a stock whose cross-section fixes an
/// in-plan dimension is a separate step, and a geometry request when it happens.
/// </remarks>
/// <param name="Box">The box.</param>
/// <param name="Part">What it is a piece of, or <see langword="null"/> to make it a plain box again.</param>
public sealed record SetPart(EntityId Box, Part? Part) : Request;

/// <summary>
/// Sets the number a driving relationship owns — what editing a driving dimension is. Exact.
/// </summary>
/// <param name="Driving">The <see cref="ParamValue"/> or <see cref="AxisDistance"/> that owns the number.</param>
/// <param name="Value">The new value.</param>
public sealed record SetParameter(RelationshipId Driving, Length Value) : Request;

/// <summary>Puts an entity at typed coordinates. Exact.</summary>
/// <param name="Id">The box or node to move.</param>
/// <param name="Anchor">Where its anchor — or, for a node, the node itself — goes.</param>
public sealed record SetPosition(EntityId Id, Point2 Anchor) : Request;

/// <summary>
/// Rotates a box about its anchor. Exact, and right-angle multiples only in the direct updater.
/// </summary>
/// <param name="Box">The box to rotate.</param>
/// <param name="Rotation">The new rotation.</param>
public sealed record SetRotation(EntityId Box, Angle Rotation) : Request;

/// <summary>
/// Moves an entity, and everything that moves with it, as far towards the target as its
/// relationships allow. Best effort: a drag is a question, not a demand, so it is never
/// <see cref="OverConstrained"/>.
/// </summary>
/// <param name="Id">The entity being dragged.</param>
/// <param name="Delta">Where the user wants it to go, relative to where it is.</param>
public sealed record Drag(EntityId Id, Vector2 Delta) : Request;

/// <summary>
/// Drags one edge of a box — a resize handle. Best effort.
/// </summary>
/// <remarks>
/// If a <see cref="ParamValue"/> drives the size that edge controls the request is
/// <see cref="RejectionReason.DrivenSize"/> and the canvas points at the dimension to edit
/// instead: a drag never silently overrides a number the user typed (design &#xA7;4.1).
/// </remarks>
/// <param name="Box">The box being resized.</param>
/// <param name="Edge">Which edge the user grabbed, in the box's local frame.</param>
/// <param name="Delta">
/// How far to move the edge along its outward normal. Positive grows the box; the opposite edge
/// stays where it is.
/// </param>
public sealed record DragEdge(EntityId Box, BoxEdge Edge, Length Delta) : Request;

/// <summary>
/// Several requests as one. Atomic: either all of them apply or none does. This is how the canvas
/// commits a multi-step edit as one undo step, and how the solver boundary applies a whole
/// solution.
/// </summary>
/// <param name="Requests">The requests, applied in order.</param>
public sealed record Batch(ImmutableList<Request> Requests) : Request
{
    /// <summary>A batch of the given requests, in order.</summary>
    public static Batch Of(params Request[] requests) => new([.. requests]);
}
