namespace Napkin.Core.Geometry;

/// <summary>
/// Which way a <see cref="CurvedEdge"/> bows.
/// </summary>
public enum Bow
{
    /// <summary>
    /// The middle of the edge stays; the two corners come in. The arc starts
    /// <see cref="CurvedEdge.Depth"/> from each corner along the two adjacent edges and passes
    /// through the middle of this edge — a bowed table-top end, a curved shelf front.
    /// </summary>
    Outward,

    /// <summary>
    /// The two corners stay; the middle goes in by <see cref="CurvedEdge.Depth"/> — a scalloped
    /// apron, a cut-out for a hand hold on a plain edge.
    /// </summary>
    Inward,
}

/// <summary>
/// Where a cut is made: one of the four corners or one of the four edges of the blank, named in
/// the box's own local frame before rotation.
/// </summary>
/// <remarks>
/// The ordering is fixed — south-west, south-east, north-east, north-west, then south, east,
/// north, west — because <see cref="Box.Cuts"/> is held in it, so that two boxes with the same
/// cuts are equal by value and a saved file has one spelling
/// (<c>docs/design/shaped-parts-model.md</c> §1.6 invariant 5).
/// </remarks>
public readonly record struct CutSite : IComparable<CutSite>
{
    private readonly int _order;

    private CutSite(int order) => _order = order;

    /// <summary>The site at a corner of the blank.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="corner"/> is not a corner.</exception>
    public static CutSite Corner(BoxCorner corner)
        => Enum.IsDefined(corner)
            ? new CutSite((int)corner)
            : throw new ArgumentOutOfRangeException(nameof(corner), corner, "Unknown corner.");

    /// <summary>The site along an edge of the blank.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="edge"/> is not an edge.</exception>
    public static CutSite Edge(BoxEdge edge)
        => Enum.IsDefined(edge)
            ? new CutSite(CornerCount + (int)edge)
            : throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown edge.");

    /// <summary>Where this site sorts: 0 to 3 for the corners, 4 to 7 for the edges.</summary>
    public int Order => _order;

    /// <summary>Whether this site is a corner rather than an edge.</summary>
    public bool IsCorner => _order < CornerCount;

    /// <summary>The corner this site names, or <see langword="null"/> when it names an edge.</summary>
    public BoxCorner? AsCorner => IsCorner ? (BoxCorner)_order : null;

    /// <summary>The edge this site names, or <see langword="null"/> when it names a corner.</summary>
    public BoxEdge? AsEdge => IsCorner ? null : (BoxEdge)(_order - CornerCount);

    private const int CornerCount = 4;

    /// <inheritdoc/>
    public int CompareTo(CutSite other) => _order.CompareTo(other._order);

    /// <summary>Sorts by <see cref="Order"/>.</summary>
    public static bool operator <(CutSite left, CutSite right) => left._order < right._order;

    /// <summary>Sorts by <see cref="Order"/>.</summary>
    public static bool operator >(CutSite left, CutSite right) => left._order > right._order;

    /// <summary>Sorts by <see cref="Order"/>.</summary>
    public static bool operator <=(CutSite left, CutSite right) => left._order <= right._order;

    /// <summary>Sorts by <see cref="Order"/>.</summary>
    public static bool operator >=(CutSite left, CutSite right) => left._order >= right._order;

    /// <inheritdoc/>
    public override string ToString()
        => IsCorner ? $"{(BoxCorner)_order} corner" : $"{(BoxEdge)(_order - CornerCount)} edge";
}

/// <summary>
/// What has been cut off a rectangular blank at one of its sites
/// (<c>docs/design/shaped-parts-model.md</c> §1.3).
/// </summary>
/// <remarks>
/// <para>
/// A cut is named in the blank's <em>local</em> frame, before rotation, exactly as
/// <see cref="BoxCorner"/> and <see cref="BoxEdge"/> already name things for references, and every
/// stored value is an exact <see cref="Length"/>. Setbacks are stored rather than angles, so that
/// every vertex of the derived outline lands on the grid and no trigonometry enters the kernel
/// (§1.3); an angle is an entry mode with one explicit rounding, which belongs to the editor.
/// </para>
/// <para>
/// Every cut is square through the plan: it runs the full out-of-plane dimension of the part, which
/// is what a circular saw, a table saw and a jigsaw all do. Bevels are not in the model (§1.4).
/// </para>
/// </remarks>
public abstract record Cut
{
    private protected Cut()
    {
    }

    /// <summary>The corner or edge this cut is made at.</summary>
    public abstract CutSite Site { get; }
}

/// <summary>
/// A straight cut across a corner: a clipped corner, a mitred end, a taper, a diagonal.
/// </summary>
/// <remarks>
/// From the point <paramref name="AlongX"/> from the corner on the edge that runs along local X, to
/// the point <paramref name="AlongY"/> from the corner on the edge that runs along local Y. The
/// triangle between them is removed.
/// </remarks>
/// <param name="Corner">The corner the cut is made at.</param>
/// <param name="AlongX">The setback along the corner's X-running edge.</param>
/// <param name="AlongY">The setback along the corner's Y-running edge.</param>
public sealed record CornerCut(BoxCorner Corner, Length AlongX, Length AlongY) : Cut
{
    /// <inheritdoc/>
    public override CutSite Site => CutSite.Corner(Corner);
}

/// <summary>
/// A quarter-circle of the given radius, tangent to both edges <paramref name="Radius"/> from the
/// corner.
/// </summary>
/// <param name="Corner">The corner that is rounded.</param>
/// <param name="Radius">The radius of the rounding.</param>
public sealed record RoundedCorner(BoxCorner Corner, Length Radius) : Cut
{
    /// <inheritdoc/>
    public override CutSite Site => CutSite.Corner(Corner);
}

/// <summary>
/// One whole edge replaced by a circular arc through three points on the grid: the one curve a
/// jigsaw and a thin batten produce.
/// </summary>
/// <remarks>
/// A curved edge is an operation on three sites — the edge and both of its corners — so nothing
/// else may be cut at either end of it (§1.6 invariant 6).
/// </remarks>
/// <param name="Edge">The edge the curve replaces.</param>
/// <param name="Bow">Which way the curve bows.</param>
/// <param name="Depth">
/// How far in the curve reaches: from each corner along the adjacent edges for
/// <see cref="Bow.Outward"/>, from the middle of the edge for <see cref="Bow.Inward"/>.
/// </param>
public sealed record CurvedEdge(BoxEdge Edge, Bow Bow, Length Depth) : Cut
{
    /// <inheritdoc/>
    public override CutSite Site => CutSite.Edge(Edge);
}
