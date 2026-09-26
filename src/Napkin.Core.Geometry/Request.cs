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

/// <summary>
/// Adds a layer after the others. Structural: moves nothing. Rejected when a layer with that id is
/// already there. How a wall tool puts the first wall on a "Wall" layer a loaded design lacks (#18).
/// </summary>
/// <param name="Layer">The layer.</param>
public sealed record AddLayer(Layer Layer) : Request;

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

/// <summary>Replaces the project's typed fastener sizes (joinery note &#xA7;7.3). Exact, moves nothing.</summary>
/// <param name="Choices">All the choices, as the person now has them.</param>
public sealed record SetFastenerChoices(System.Collections.Immutable.ImmutableList<FastenerChoice> Choices) : Request;

/// <summary>Replaces the project's typed supplies checklist (joinery note &#xA7;8). Exact, moves nothing.</summary>
/// <param name="Supplies">All the lines, as the person now has them.</param>
public sealed record SetSupplies(System.Collections.Immutable.ImmutableList<SupplyLine> Supplies) : Request;

/// <summary>Chooses the project's adopted code, or clears the choice. Exact, moves nothing.</summary>
/// <param name="Code">The choice, or <see langword="null"/> for none.</param>
public sealed record SetCode(CodeChoice? Code) : Request;

/// <summary>Replaces the project's site values. Exact, moves nothing.</summary>
/// <param name="Site">The values, as the person now has them.</param>
public sealed record SetSite(SiteValues Site) : Request;

/// <summary>Sets what the person entered for a wall — what it supports, its stud spacing. Exact, moves nothing.</summary>
/// <param name="Box">The wall's box.</param>
/// <param name="Inputs">The inputs; <see langword="null"/>, or both fields null, for none.</param>
public sealed record SetWallInputs(EntityId Box, WallInputs? Inputs) : Request;

/// <summary>
/// Says whether an entity is already there, going in or coming out (renovation-sketches §6.1).
/// Exact, moves nothing; a wall's openings keep their own phase.
/// </summary>
/// <param name="Id">The entity.</param>
/// <param name="Phase">Its phase.</param>
public sealed record SetPhase(EntityId Id, Phase Phase) : Request;

/// <summary>Sets a room's finishes and measurements. Exact, moves nothing.</summary>
/// <param name="Box">The room's box.</param>
/// <param name="Inputs">The inputs, or <see langword="null"/> for none.</param>
public sealed record SetRoomInputs(EntityId Box, RoomInputs? Inputs) : Request;

/// <summary>Sets what fills an opening — glass, screen or solid (deck-and-porch §5.2). Exact, moves nothing.</summary>
/// <param name="Box">The opening's box.</param>
/// <param name="Fill">The fill, or <see langword="null"/> for none said.</param>
public sealed record SetOpeningFill(EntityId Box, OpeningFill? Fill) : Request;

/// <summary>Sets a deck's inputs (deck-and-porch §2.2). Exact, moves nothing.</summary>
/// <param name="Box">The deck's box.</param>
/// <param name="Inputs">The inputs, or <see langword="null"/> for none.</param>
public sealed record SetDeckInputs(EntityId Box, DeckInputs? Inputs) : Request;

/// <summary>Sets what a note says and the symbol it is drawn with. Exact, moves nothing.</summary>
/// <param name="Id">The note.</param>
/// <param name="Text">Its words.</param>
/// <param name="Symbol">Its symbol.</param>
public sealed record SetNote(EntityId Id, string Text, NoteSymbol Symbol) : Request;

/// <summary>
/// Sets the number a driving relationship owns — what editing a driving dimension is. Exact.
/// </summary>
/// <param name="Driving">The <see cref="ParamValue"/> or <see cref="AxisDistance"/> that owns the number.</param>
/// <param name="Value">The new value.</param>
public sealed record SetParameter(RelationshipId Driving, Length Value) : Request;

/// <summary>Puts an entity at typed coordinates in space. Exact.</summary>
/// <remarks>
/// A node lies at the plan datum (docs/design/assembly-model.md &#xA7;1.4), so a position for one
/// with a Z other than zero is <see cref="RejectionReason.UnsupportedRequest"/>.
/// </remarks>
/// <param name="Id">The box or node to move.</param>
/// <param name="Anchor">Where its anchor — or, for a node, the node itself — goes.</param>
public sealed record SetPosition(EntityId Id, Point3 Anchor) : Request
{
    /// <summary>A position in the plan, at Z = 0: where a node goes, or a box on the plan datum.</summary>
    /// <param name="id">The box or node to move.</param>
    /// <param name="anchor">Where it goes in the plan.</param>
    public static SetPosition InPlan(EntityId id, Point2 anchor) => new(id, new Point3(anchor.X, anchor.Y, Length.Zero));
}

/// <summary>
/// Turns a box to one of the 24 orientations: which local face points up, then a spin about world
/// Z (docs/design/assembly-model.md &#xA7;1.3, &#xA7;2.4). Exact, about the anchor, which stays
/// where it is — so the box is in <see cref="ChangeSet.Modified"/>, neither moved nor resized.
/// </summary>
/// <remarks>
/// <para>
/// One request rather than a face-up beside a rotation, because a quarter turn about world X
/// changes both fields at once and the canvas must never be able to land a box between two
/// spellings.
/// </para>
/// <para>
/// <see cref="RejectionReason.RotationNotSupported"/> when <paramref name="Rotation"/> is not a
/// quarter turn. <see cref="RejectionReason.OrientationWithRelationships"/> when the box has a
/// <see cref="Coincident"/>, <see cref="Flush"/>, <see cref="AxisDistance"/> or
/// <see cref="Centered"/> — each names a place in the box's own frame, which the turn would move —
/// or a <see cref="Dimension"/> whose measurand would stand along world Z after the turn (invariant
/// 13). The <see cref="Rejected.Detail"/> names which. <see cref="Anchored"/>,
/// <see cref="ParamValue"/> and <see cref="EqualParam"/> turn with the box and mean what they meant.
/// </para>
/// </remarks>
/// <param name="Box">The box to turn.</param>
/// <param name="FaceUp">The local face that is to point to world +Z.</param>
/// <param name="Rotation">The spin about world Z; right-angle multiples only in the direct updater.</param>
public sealed record SetOrientation(EntityId Box, BoxFace FaceUp, Angle Rotation) : Request
{
    /// <summary>The same turn, spelled as an <see cref="Geometry.Orientation"/>.</summary>
    /// <param name="box">The box to turn.</param>
    /// <param name="orientation">Where it is to end up.</param>
    public static SetOrientation To(EntityId box, Orientation orientation) => new(box, orientation.FaceUp, orientation.Rotation);
}

/// <summary>
/// Adds a cut to a blank, or replaces the cut already at the same site. Exact, and never moves
/// geometry.
/// </summary>
/// <remarks>
/// <see cref="RejectionReason.CutSiteTaken"/> when a curved edge claims the site — or when the cut
/// <em>is</em> a curved edge and something is already cut at one of its two corners (invariant 6);
/// <see cref="RejectionReason.CutDoesNotFit"/> when invariants 7 to 9 fail on the box as it would
/// be; otherwise <see cref="Solved"/>, with the box in <see cref="ChangeSet.Modified"/> — a cut is
/// neither a move nor a resize (<c>docs/design/shaped-parts-model.md</c> §2.2).
/// </remarks>
/// <param name="Box">The blank to cut.</param>
/// <param name="Cut">What to take off it.</param>
public sealed record SetCut(EntityId Box, Cut Cut) : Request;

/// <summary>
/// Removes the cut at a corner or an edge. Exact, and never moves geometry.
/// </summary>
/// <remarks>
/// <see cref="RejectionReason.NoSuchCut"/> when there is nothing at the site. Removing a cut can
/// only give the blank back area, so it can never break an invariant.
/// </remarks>
/// <param name="Box">The blank.</param>
/// <param name="Site">Which corner or edge to un-cut.</param>
public sealed record RemoveCut(EntityId Box, CutSite Site) : Request;

/// <summary>
/// Moves an entity, and everything that moves with it, as far towards the target as its
/// relationships allow. Best effort: a drag is a question, not a demand, so it is never
/// <see cref="OverConstrained"/>.
/// </summary>
/// <remarks>
/// The delta is in space (docs/design/assembly-model.md &#xA7;2.4); a plan-canvas drag is one with
/// a zero Z. Each axis is applied in full or not at all: the rigid group is worked out per axis,
/// and an axis along which the group reaches an anchored entity goes nowhere. A node has no Z, so
/// a drag of a node or a segment applies none.
/// </remarks>
/// <param name="Id">The entity being dragged.</param>
/// <param name="Delta">Where the user wants it to go, relative to where it is.</param>
public sealed record Drag(EntityId Id, Vector3 Delta) : Request
{
    /// <summary>A drag in the plan: the delta's X and Y, with no Z.</summary>
    /// <param name="id">The entity being dragged.</param>
    /// <param name="delta">The displacement in the plan.</param>
    public static Drag InPlan(EntityId id, Vector2 delta) => new(id, new Vector3(delta.Dx, delta.Dy, Length.Zero));
}

/// <summary>
/// Drags one face of a box — a resize handle. Best effort.
/// </summary>
/// <remarks>
/// <para>
/// The size along the local axis normal to the face changes; the opposite face stays where it
/// is, so grabbing a face at the local origin — <see cref="BoxFace.West"/>,
/// <see cref="BoxFace.South"/>, <see cref="BoxFace.Bottom"/> — moves the anchor, the way the
/// orientation turns that local axis (docs/design/assembly-model.md &#xA7;2.4).
/// </para>
/// <para>
/// If a <see cref="ParamValue"/> drives the size that face controls the request is
/// <see cref="RejectionReason.DrivenSize"/> and the canvas points at the dimension to edit
/// instead: a drag never silently overrides a number the user typed (design &#xA7;4.1). A side
/// face is clamped so the blank is never dragged shorter than its cuts claim
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.3); the bottom and top are clamped by nothing,
/// because a cut never reaches them, and a drag through the opposite face is
/// <see cref="RejectionReason.NonPositiveSize"/>.
/// </para>
/// </remarks>
/// <param name="Box">The box being resized.</param>
/// <param name="Face">Which face the user grabbed, in the box's local frame.</param>
/// <param name="Delta">
/// How far to move the face along its outward normal. Positive grows the box; the opposite face
/// stays where it is.
/// </param>
public sealed record DragFace(EntityId Box, BoxFace Face, Length Delta) : Request;

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

/// <summary>
/// Puts one end of a strut at a point (<c>docs/design/assembly-model.md</c> &#xA7;3a.5). That end's
/// three scalars are set and propagation runs as for <see cref="SetPosition"/>; the other end is not
/// set, and moves only if a relationship moves it.
/// </summary>
/// <remarks>
/// <see cref="RejectionReason.StrutIsAxisAligned"/> or <see cref="RejectionReason.StrutTooShortForItsCuts"/>
/// when the written strut would break invariant 14 or 17, judged after propagation. An
/// <see cref="Anchored"/> strut does not stand aside for this: asking a held part to move is what
/// anchoring refuses.
/// </remarks>
/// <param name="Strut">The strut.</param>
/// <param name="End">Which end.</param>
/// <param name="At">Where that end goes.</param>
public sealed record SetStrutEnd(EntityId Strut, StrutEnd End, Point3 At) : Request;

/// <summary>
/// Drags one end of a strut: the strut's analogue of <see cref="DragFace"/>. Best effort, never
/// <see cref="OverConstrained"/>: the end moves by as much of the delta as its relationships and
/// the strut's own invariants allow, each axis in full or not at all, and the applied delta is reported.
/// </summary>
/// <param name="Strut">The strut.</param>
/// <param name="End">Which end.</param>
/// <param name="Delta">How far the person wants the end to go.</param>
public sealed record DragStrutEnd(EntityId Strut, StrutEnd End, Vector3 Delta) : Request;

/// <summary>
/// Sets the planes a strut's ends are cut to and the axis its wide face keeps parallel to
/// (<c>docs/design/angled-parts.md</c> &#xA7;1.2). Structural: no end moves, so nothing propagates;
/// the strut's derived board changes. Refused when the strut would break its invariants, or when a
/// relationship on it could no longer hold — a flush to a face that the new reference turns.
/// </summary>
/// <param name="Strut">The strut.</param>
/// <param name="FromCut">The plane its From end is cut to.</param>
/// <param name="ToCut">The plane its To end is cut to.</param>
/// <param name="Reference">The world axis its wide face stays parallel to.</param>
public sealed record SetStrutCuts(EntityId Strut, EndCut FromCut, EndCut ToCut, Axis Reference) : Request;
