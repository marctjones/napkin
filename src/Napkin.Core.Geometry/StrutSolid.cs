using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Which of a strut solid's six faces this is.</summary>
public enum StrutSolidFace
{
    /// <summary>The long face at local −Y.</summary>
    South,

    /// <summary>The long face at local +Y.</summary>
    North,

    /// <summary>The long face at local −Z.</summary>
    Bottom,

    /// <summary>The long face at local +Z.</summary>
    Top,

    /// <summary>The end face at the blank's west end, in the plane that end is cut to.</summary>
    WestEnd,

    /// <summary>The end face at the blank's east end.</summary>
    EastEnd,
}

/// <summary>One planar face of a strut's solid: its corners in inches, counter-clockwise seen from outside.</summary>
/// <param name="Face">Which face.</param>
/// <param name="Corners">The corners, in inches, wound so that the right-hand normal points out.</param>
public sealed record StrutSolidPolygon(StrutSolidFace Face, ImmutableArray<(double X, double Y, double Z)> Corners);

/// <summary>
/// A strut drawn: the prism on its cross-section clipped by the two planes its ends are cut to
/// (<c>docs/design/assembly-model.md</c> &#xA7;3a.7, <c>docs/design/angled-parts.md</c> &#xA7;4). Six
/// planar faces, every corner worked out in <see cref="double"/> from the exact ends, the exact
/// integer frame and the exact sizes.
/// </summary>
/// <remarks>
/// A derived view for the screen only, beside the cut list's blank rather than from it: the end
/// faces lie exactly in the planes through <see cref="Strut.From"/> and <see cref="Strut.To"/>, so a
/// foot sits flat on the floor in the picture even where the blank's rounded length is a hair off.
/// A plane cutting a rectangular prism leaves a planar polygon, so a compound end is one face too.
/// </remarks>
public static class StrutSolid
{
    /// <summary>The six faces of a strut that passes invariants 14–17: four long faces, then the two ends.</summary>
    public static ImmutableArray<StrutSolidPolygon> Of(Strut strut)
    {
        ArgumentNullException.ThrowIfNull(strut);

        StrutFrame frame = strut.Frame();
        (double X, double Y, double Z) d = Unit(frame.D);
        (double X, double Y, double Z) y = Unit(frame.Y);
        (double X, double Y, double Z) z = Unit(frame.Z);
        Point3 westPoint = frame.Reversed ? strut.To : strut.From;
        Point3 eastPoint = frame.Reversed ? strut.From : strut.To;
        (double X, double Y, double Z) west = Inches(westPoint);
        (double X, double Y, double Z) east = Inches(eastPoint);
        (double X, double Y, double Z)? westNormal = Normal(frame.Reversed ? strut.ToCut : strut.FromCut);
        (double X, double Y, double Z)? eastNormal = Normal(frame.Reversed ? strut.FromCut : strut.ToCut);
        double halfHeight = strut.Height.ToInches() / 2;
        double halfDepth = strut.Depth.ToInches() / 2;

        // Each long edge is a line along d through the centreline point offset by (a, b) in (y, z);
        // it ends where it meets each end's plane. A square end's plane is normal to d.
        (double X, double Y, double Z) Corner((double X, double Y, double Z) end, (double X, double Y, double Z)? cutNormal, int a, int b)
        {
            (double X, double Y, double Z) offset = Add(Scale(y, a * halfHeight), Scale(z, b * halfDepth));
            (double X, double Y, double Z) n = cutNormal ?? d;
            double along = -Dot(n, offset) / Dot(n, d);
            return Add(Add(end, offset), Scale(d, along));
        }

        // Corners by (south/north, bottom/top) at each end.
        (double X, double Y, double Z) W(int a, int b) => Corner(west, westNormal, a, b);
        (double X, double Y, double Z) E(int a, int b) => Corner(east, eastNormal, a, b);

        // The frame (d, y, z) is right-handed, which makes each winding below outward: the south
        // face, for one, runs (d, z) counter-clockwise, and d × z = −y. The tests hold every face to it.
        return
        [
            new(StrutSolidFace.South, [W(-1, -1), E(-1, -1), E(-1, 1), W(-1, 1)]),
            new(StrutSolidFace.North, [W(1, -1), W(1, 1), E(1, 1), E(1, -1)]),
            new(StrutSolidFace.Bottom, [W(-1, -1), W(1, -1), E(1, -1), E(-1, -1)]),
            new(StrutSolidFace.Top, [W(-1, 1), E(-1, 1), E(1, 1), W(1, 1)]),
            new(StrutSolidFace.WestEnd, [W(-1, -1), W(-1, 1), W(1, 1), W(1, -1)]),
            new(StrutSolidFace.EastEnd, [E(-1, -1), E(1, -1), E(1, 1), E(-1, 1)]),
        ];
    }

    /// <summary>
    /// The strut seen from above: the convex outline of its eight corners in the plan, in inches,
    /// counter-clockwise — what the plan view draws and hit-tests (assembly-model &#xA7;3a.7).
    /// </summary>
    public static ImmutableArray<(double X, double Y)> PlanOutline(Strut strut)
    {
        List<(double X, double Y)> points = [.. Of(strut).SelectMany(face => face.Corners).Select(corner => (corner.X, corner.Y))];
        return Hull(points);
    }

    /// <summary>Whether a plan point, in inches, is inside a strut's outline from above or within <paramref name="reach"/> of it.</summary>
    public static bool PlanContains(Strut strut, double x, double y, double reach)
    {
        ImmutableArray<(double X, double Y)> outline = PlanOutline(strut);
        bool inside = true;
        double nearest = double.PositiveInfinity;
        for (int i = 0; i < outline.Length; i++)
        {
            (double X, double Y) a = outline[i];
            (double X, double Y) b = outline[(i + 1) % outline.Length];
            double cross = ((b.X - a.X) * (y - a.Y)) - ((b.Y - a.Y) * (x - a.X));
            inside &= cross >= 0;
            nearest = Math.Min(nearest, DistanceToSegment(x, y, a, b));
        }

        return inside || nearest <= reach;
    }

    // Andrew's monotone chain: the plan outline of a convex solid is the hull of its corners.
    static ImmutableArray<(double X, double Y)> Hull(List<(double X, double Y)> points)
    {
        List<(double X, double Y)> sorted = [.. points.Distinct().OrderBy(point => point.X).ThenBy(point => point.Y)];
        if (sorted.Count < 3)
        {
            return [.. sorted];
        }

        static double Turn((double X, double Y) o, (double X, double Y) a, (double X, double Y) b)
            => ((a.X - o.X) * (b.Y - o.Y)) - ((a.Y - o.Y) * (b.X - o.X));

        List<(double X, double Y)> hull = [];
        foreach (IEnumerable<(double X, double Y)> pass in new[] { sorted, Enumerable.Reverse(sorted) })
        {
            int start = hull.Count;
            foreach ((double X, double Y) point in pass)
            {
                while (hull.Count >= start + 2 && Turn(hull[^2], hull[^1], point) <= 1e-12)
                {
                    hull.RemoveAt(hull.Count - 1);
                }

                hull.Add(point);
            }

            hull.RemoveAt(hull.Count - 1);
        }

        return [.. hull];
    }

    static double DistanceToSegment(double x, double y, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double lengthSquared = (dx * dx) + (dy * dy);
        double t = lengthSquared > 0 ? Math.Clamp((((x - a.X) * dx) + ((y - a.Y) * dy)) / lengthSquared, 0, 1) : 0;
        double ex = a.X + (t * dx) - x;
        double ey = a.Y + (t * dy) - y;
        return Math.Sqrt((ex * ex) + (ey * ey));
    }

    static (double X, double Y, double Z)? Normal(EndCut cut) => cut switch
    {
        EndCut.X => (1, 0, 0),
        EndCut.Y => (0, 1, 0),
        EndCut.Z => (0, 0, 1),
        _ => null,
    };

    static (double X, double Y, double Z) Unit(IntegerVector3 v)
    {
        double x = (double)v.X, y = (double)v.Y, z = (double)v.Z;
        double length = Math.Sqrt((x * x) + (y * y) + (z * z));
        return (x / length, y / length, z / length);
    }

    static (double X, double Y, double Z) Inches(Point3 point) => (point.X.ToInches(), point.Y.ToInches(), point.Z.ToInches());

    static (double X, double Y, double Z) Add((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    static (double X, double Y, double Z) Scale((double X, double Y, double Z) a, double k) => (a.X * k, a.Y * k, a.Z * k);

    static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
}
