using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// The one way geometry changes.
/// </summary>
/// <remarks>
/// <see cref="DirectUpdater"/> is the first implementation; the solver (<c>SolverUpdater</c>, in
/// <c>Napkin.Core.Solver</c>, #28) is the second. Callers — the canvas, the building module,
/// tests — hold an <see cref="IGeometryUpdater"/> and nothing else, so swapping implementations
/// changes no caller (design &#xA7;4.3).
/// </remarks>
public interface IGeometryUpdater
{
    /// <summary>
    /// Which relationship kinds <see cref="AddRelationship"/> will accept. The canvas only offers
    /// these.
    /// </summary>
    ImmutableHashSet<Type> SupportedRelationships { get; }

    /// <summary>
    /// Applies a request.
    /// </summary>
    /// <remarks>
    /// Pure: never mutates the input sketch. Deterministic: same inputs, same output — which is
    /// why relationships are iterated in <see cref="RelationshipId"/> order and never in
    /// dictionary order.
    /// </remarks>
    /// <param name="sketch">The sketch to apply it to. Unchanged by this call.</param>
    /// <param name="request">What the user asked for.</param>
    /// <returns>A new sketch, or a report of why there is not one.</returns>
    UpdateResult Apply(Sketch sketch, Request request);
}
