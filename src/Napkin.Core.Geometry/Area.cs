namespace Napkin.Core.Geometry;

/// <summary>
/// Exact products of lengths, in square units of the 1/1024&#x2033; grid
/// (<c>docs/design/geometry-model.md</c> §1.3).
/// </summary>
/// <remarks>
/// A small helper, not a geometry type. <see cref="Int128"/> holds the product of any two lengths
/// below 2&#x2076;&#xB3; units, so a takeoff or a polygon area is computed exactly and rounded once
/// at the end — never in <see cref="double"/>.
/// </remarks>
public static class Area
{
    /// <summary>The exact product of two lengths, in square units.</summary>
    public static Int128 Of(Length a, Length b) => (Int128)a.Units * b.Units;

    /// <summary>
    /// Twice the signed area of a closed polygon, exactly: the shoelace sum, which is positive
    /// when the vertices run counter-clockwise.
    /// </summary>
    /// <remarks>
    /// Twice the area rather than the area, because halving would be the one rounding this does
    /// not need: every use so far only asks about the sign or compares two of them.
    /// <c>docs/design/shaped-parts-model.md</c> §1.6 invariant 9 is <c>&gt; 0</c> on this.
    /// </remarks>
    /// <param name="vertices">The polygon's vertices in order; the last joins the first.</param>
    public static Int128 TwiceSignedPolygon(IReadOnlyList<Point2> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);

        if (vertices.Count < 3)
        {
            return Int128.Zero;
        }

        Int128 sum = Int128.Zero;
        for (int i = 0; i < vertices.Count; i++)
        {
            Point2 here = vertices[i];
            Point2 next = vertices[(i + 1) % vertices.Count];
            sum += Of(here.X, next.Y) - Of(next.X, here.Y);
        }

        return sum;
    }
}
