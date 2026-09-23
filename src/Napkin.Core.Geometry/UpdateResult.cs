using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>What an <see cref="IGeometryUpdater"/> did with a request.</summary>
/// <remarks>
/// DESIGN.md &#xA7;11 asks for three states — solved, under-constrained, over-constrained. Those
/// are the <em>geometric</em> states; <see cref="Rejected"/> is a fourth, for requests that are
/// not about geometry at all (an unknown id, a zero-width box, a relationship kind this updater
/// does not implement). Folding those into <see cref="OverConstrained"/> would make the conflict
/// report lie; letting them throw would make the UI's error path an exception handler
/// (design &#xA7;4.2).
/// </remarks>
public abstract record UpdateResult
{
    private protected UpdateResult()
    {
    }
}

/// <summary>
/// The request was applied, and every relationship holds — invariant 3 of design &#xA7;2.5. A
/// caller that only wants the sketch matches on this.
/// </summary>
/// <param name="Sketch">The new sketch.</param>
/// <param name="Changes">What changed.</param>
public abstract record Succeeded(Sketch Sketch, ChangeSet Changes) : UpdateResult;

/// <summary>
/// Applied; every relationship holds; and the updater either analysed freedom and found none
/// remaining, or did not analyse freedom at all — which the direct updater never does.
/// </summary>
/// <param name="Sketch">The new sketch.</param>
/// <param name="Changes">What changed.</param>
public sealed record Solved(Sketch Sketch, ChangeSet Changes) : Succeeded(Sketch, Changes);

/// <summary>
/// Applied; every relationship holds; and the updater analysed freedom and found that some
/// entities could still move without breaking any relationship.
/// </summary>
/// <remarks>
/// This is the NORMAL state of a drag-and-drop drawing, not a failure. The UI shows it, at most,
/// as a hint ("this part is not pinned to anything").
/// </remarks>
/// <param name="Sketch">The new sketch.</param>
/// <param name="Changes">What changed.</param>
/// <param name="Freedom">What is still free to move.</param>
public sealed record UnderConstrained(Sketch Sketch, ChangeSet Changes, FreedomReport Freedom)
    : Succeeded(Sketch, Changes);

/// <summary>
/// No geometry satisfies the request together with the existing relationships. The sketch is
/// unchanged. The report names what conflicts, in terms a non-CAD user can act on.
/// </summary>
/// <param name="Conflict">What could not all hold.</param>
public sealed record OverConstrained(ConflictReport Conflict) : UpdateResult;

/// <summary>
/// The request itself cannot be interpreted by this updater — not a geometric state. The sketch
/// is unchanged.
/// </summary>
/// <param name="Reason">Why the request was refused.</param>
/// <param name="Detail">
/// Which box and which site, for a refusal that has one — the cut refusals of
/// <c>docs/design/shaped-parts-model.md</c> §2.2 and §2.3, whose whole point is that the canvas can
/// say <em>which</em> cut does not fit and offer to remove it. <see langword="null"/> for every
/// other reason, where the reason is the whole story. This is the same <see cref="Rejected"/>, not
/// a fourth result type: §2.2 asks for new reasons and no new type.
/// </param>
public sealed record Rejected(RejectionReason Reason, ValidationError? Detail = null) : UpdateResult;

/// <summary>What an update changed, so the canvas can redraw and explain only that.</summary>
/// <param name="Added">Entities added.</param>
/// <param name="Removed">Entities removed, including everything the cascade took with them.</param>
/// <param name="Moved">Entities whose position changed.</param>
/// <param name="Resized">Entities whose size changed.</param>
/// <param name="Modified">
/// Entities that changed in some other way: a layer, a rotation, or a dimension that stopped
/// driving and became a reference dimension. The canvas has to redraw these too, and a canvas
/// that could not tell a dimension had been demoted would draw it wrong.
/// </param>
/// <param name="RelationshipsAdded">Relationships added.</param>
/// <param name="RelationshipsRemoved">Relationships removed.</param>
/// <param name="AppliedDelta">
/// For a drag, what actually happened, in the world; <see langword="null"/> otherwise. A plan-canvas
/// drag's has a zero Z (docs/design/assembly-model.md &#xA7;2.4).
/// </param>
public sealed record ChangeSet(
    ImmutableHashSet<EntityId> Added,
    ImmutableHashSet<EntityId> Removed,
    ImmutableHashSet<EntityId> Moved,
    ImmutableHashSet<EntityId> Resized,
    ImmutableHashSet<EntityId> Modified,
    ImmutableHashSet<RelationshipId> RelationshipsAdded,
    ImmutableHashSet<RelationshipId> RelationshipsRemoved,
    Vector3? AppliedDelta)
{
    /// <summary>Nothing changed.</summary>
    public static readonly ChangeSet Empty = new(
        ImmutableHashSet<EntityId>.Empty,
        ImmutableHashSet<EntityId>.Empty,
        ImmutableHashSet<EntityId>.Empty,
        ImmutableHashSet<EntityId>.Empty,
        ImmutableHashSet<EntityId>.Empty,
        ImmutableHashSet<RelationshipId>.Empty,
        ImmutableHashSet<RelationshipId>.Empty,
        AppliedDelta: null);

    /// <summary>
    /// Whether nothing changed. <see cref="AppliedDelta"/> is not part of this: a drag that goes
    /// nowhere reports a zero delta and an empty change set.
    /// </summary>
    public bool IsEmpty
        => Added.IsEmpty
           && Removed.IsEmpty
           && Moved.IsEmpty
           && Resized.IsEmpty
           && Modified.IsEmpty
           && RelationshipsAdded.IsEmpty
           && RelationshipsRemoved.IsEmpty;

    /// <summary>The two change sets together, for a <see cref="Batch"/>.</summary>
    public ChangeSet Merge(ChangeSet other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new ChangeSet(
            Added.Union(other.Added),
            Removed.Union(other.Removed),
            Moved.Union(other.Moved),
            Resized.Union(other.Resized),
            Modified.Union(other.Modified),
            RelationshipsAdded.Union(other.RelationshipsAdded),
            RelationshipsRemoved.Union(other.RelationshipsRemoved),
            other.AppliedDelta ?? AppliedDelta);
    }
}

/// <summary>
/// What is still free to move. The direct updater never produces this: it does no
/// degree-of-freedom analysis, so a success from it is always <see cref="Solved"/> — which, by
/// <see cref="Solved"/>'s definition, claims nothing about freedom. That is DESIGN.md &#xA7;11's
/// "the direct updater only ever returns solved", made precise.
/// </summary>
/// <param name="FreeEntities">The entities that could still move.</param>
/// <param name="FreeDegrees">How many degrees of freedom remain.</param>
public sealed record FreedomReport(ImmutableHashSet<EntityId> FreeEntities, int FreeDegrees);

/// <summary>Why a request had no solution.</summary>
public enum ConflictKind
{
    /// <summary>Two things the sketch says cannot both be true.</summary>
    Contradictory,

    /// <summary>A solution exists but cannot be written onto the grid. Only a solver produces this.</summary>
    NoRepresentableSolution,

    /// <summary>The solver did not converge. Only a solver produces this.</summary>
    NotConverged,
}

/// <summary>
/// What could not all hold, in terms a non-CAD user can act on.
/// </summary>
/// <param name="Kind">What sort of failure it was.</param>
/// <param name="Relationships">The smallest set found that cannot all hold.</param>
/// <param name="Entities">The entities those relationships touch.</param>
/// <param name="Derivations">How each side of the contradiction was reached.</param>
/// <param name="Summary">
/// A sentence naming the conflict, for example "Leg A's width is set to 30&#x2033; by dimension D1
/// but equal to Leg B's width, anchored at 28&#x2033;".
/// </param>
public sealed record ConflictReport(
    ConflictKind Kind,
    ImmutableList<RelationshipId> Relationships,
    ImmutableList<EntityId> Entities,
    ImmutableList<Derivation> Derivations,
    string Summary);

/// <summary>What an assignment landed on: a size, or one axis of a point.</summary>
public abstract record AssignmentTarget
{
    private protected AssignmentTarget()
    {
    }
}

/// <summary>A size.</summary>
/// <param name="Param">Which size.</param>
public sealed record ParamTarget(ParamRef Param) : AssignmentTarget;

/// <summary>One axis of a point.</summary>
/// <param name="Point">Which place.</param>
/// <param name="Axis">Which axis of it.</param>
public sealed record PointAxisTarget(PlaceRef Point, Axis Axis) : AssignmentTarget;

/// <summary>How a value was arrived at: the chain of relationships back to the request.</summary>
/// <param name="Target">What was assigned.</param>
/// <param name="Value">What it was assigned.</param>
/// <param name="Via">The relationships the value came through, in the order it came through them.</param>
public sealed record Derivation(AssignmentTarget Target, Length Value, ImmutableList<RelationshipId> Via);

/// <summary>Why a request was refused outright.</summary>
public enum RejectionReason
{
    /// <summary>An entity id in the request is not in the sketch.</summary>
    UnknownEntity,

    /// <summary>A relationship id in the request is not in the sketch.</summary>
    UnknownRelationship,

    /// <summary>A relationship kind this updater does not implement.</summary>
    UnsupportedRelationship,

    /// <summary>The request would leave a box with a width or height that is not greater than zero.</summary>
    NonPositiveSize,

    /// <summary>
    /// The dimension has no driving relationship, so there is no number to edit. The canvas offers
    /// to make it driving instead (design &#xA7;3.3).
    /// </summary>
    ReferenceDimension,

    /// <summary>The sketch already says this, about the same references.</summary>
    DuplicateRelationship,

    /// <summary>The sketch already has an entity with that id. See docs/design/geometry-model.md &#xA7;10.</summary>
    DuplicateEntity,

    /// <summary>The request references something that is not there, or is of the wrong kind.</summary>
    DanglingReference,

    /// <summary>The rotation is not a right-angle multiple, which this updater cannot handle.</summary>
    RotationNotSupported,

    /// <summary>
    /// The box has relationships that rotating it would reinterpret. The canvas offers to remove
    /// them first (design &#xA7;4.4).
    /// </summary>
    RotationWithRelationships,

    /// <summary>A <see cref="ParamValue"/> drives that size, so a drag must not override it.</summary>
    DrivenSize,

    /// <summary>
    /// Another cut is already at that site, or a curved edge claims it — shaped-parts invariants
    /// 5 and 6 (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.2).
    /// </summary>
    CutSiteTaken,

    /// <summary>
    /// A cut does not fit the blank it is on — shaped-parts invariants 7, 8 and 9. Either the cut
    /// being set is too big for the blank, or a resize has made the blank too small for a cut it
    /// already carries (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.3).
    /// </summary>
    CutDoesNotFit,

    /// <summary>
    /// There is no cut at the site a <see cref="RemoveCut"/> names
    /// (<c>docs/design/shaped-parts-model.md</c> &#xA7;2.2).
    /// </summary>
    NoSuchCut,

    /// <summary>
    /// This updater does not implement that kind of request at all. See
    /// docs/design/geometry-model.md &#xA7;10.
    /// </summary>
    UnsupportedRequest,

    /// <summary>
    /// The relationship pairs places that do not fix the axes it needs — a <see cref="Flush"/>
    /// between a face pointing up and one pointing north, a <see cref="Coincident"/> between a face
    /// and a vertex — so it could never hold (<c>docs/design/assembly-model.md</c> &#xA7;2.3). The
    /// <see cref="Rejected.Detail"/> names both places and the axes each fixes.
    /// </summary>
    PlacesNotComparable,
}
