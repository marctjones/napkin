using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Where a strut's end sits on what receives it: the plane's axis and the overlap's long side, rounded once.</summary>
/// <param name="Normal">The world axis the two faces are square to.</param>
/// <param name="JointLength">The overlap's longest edge, rounded once to the grid; never proven exact, so read with ≈.</param>
public sealed record StrutContact(Axis Normal, Length JointLength);

/// <summary>
/// The geometry a <see cref="StrutJoint"/> implies (<c>docs/design/angled-parts.md</c> &#xA7;5): the
/// strut's end face, a planar polygon whose corners are irrational, overlapped with the face it sits on.
/// </summary>
/// <remarks>
/// <strong>Satisfied</strong> is judged exactly where it can be — the end's plane and the receiving face
/// fix the same axis at the same coordinate, as a <see cref="Flush"/> is — and in <see cref="double"/>
/// where it cannot: the two faces overlap. The overlap's long side is the joint length the fastener
/// recipes read; a count of pocket screws does not care about the 1/1024″.
/// </remarks>
public static class StrutJointGeometry
{
    /// <summary>The contact, or <see langword="null"/> when the faces are not on one plane or do not overlap.</summary>
    public static StrutContact? Contact(Sketch sketch, StrutJoint joint)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(joint);

        if (EndFace(sketch, joint.Inserted) is not { } inserted
            || Receiving(sketch, joint.Receiving) is not { } receiving
            || inserted.Axis != receiving.Axis
            || inserted.At != receiving.At)
        {
            return null;
        }

        List<(double U, double V)> overlap = Clip(inserted.Outline, receiving.Outline);
        if (overlap.Count < 3)
        {
            return null;
        }

        // The long side is the overlap's longest edge, measured along the polygon itself: a two-way
        // lean's end is turned in the plane, so a box around it along the world axes is too wide.
        double longest = 0;
        for (int i = 0; i < overlap.Count; i++)
        {
            (double U, double V) a = overlap[i], b = overlap[(i + 1) % overlap.Count];
            longest = Math.Max(longest, Math.Sqrt(((b.U - a.U) * (b.U - a.U)) + ((b.V - a.V) * (b.V - a.V))));
        }

        return new StrutContact(inserted.Axis, Length.FromInches(longest, Rounding.HalfToEven));
    }

    /// <summary>Whether the strut's end still sits on what receives it.</summary>
    public static bool IsSatisfied(Sketch sketch, StrutJoint joint) => Contact(sketch, joint) is not null;

    /// <summary>
    /// A face in its plane: the axis it is square to, its exact coordinate on that axis, and its outline
    /// in the other two axes (in inches, in the order <see cref="Plane"/> gives them), or
    /// <see langword="null"/> when it is square to nothing.
    /// </summary>
    static (Axis Axis, Length At, List<(double U, double V)> Outline)? EndFace(Sketch sketch, StrutEndFaceRef end)
    {
        if (sketch.Find<Strut>(end.Strut) is not { } strut || sketch.PlaceOf(end) is not { Count: 1 } place)
        {
            return null;
        }

        Axis axis = place.Axes[0];
        StrutSolidFace which = strut.Frame().Reversed == (end.End == StrutEnd.From) ? StrutSolidFace.EastEnd : StrutSolidFace.WestEnd;
        StrutSolidPolygon face = StrutSolid.Of(strut).Single(polygon => polygon.Face == which);
        (Axis u, Axis v) = Plane(axis);
        return (axis, strut.End(end.End).Component(axis), [.. face.Corners.Select(corner => (Pick(corner, u), Pick(corner, v)))]);
    }

    static (Axis Axis, Length At, List<(double U, double V)> Outline)? Receiving(Sketch sketch, PlaceRef reference)
    {
        switch (reference)
        {
            case StrutEndFaceRef other:
                return EndFace(sketch, other);

            case FeatureRef face when sketch.Find<Box>(face.Box) is { Orientation.IsExact: true } box
                                      && sketch.PlaceOf(face) is { Count: 1 } place:
            {
                // A box's face on the 24 orientations covers the box's whole extent in the other two axes.
                Axis axis = place.Axes[0];
                (Axis u, Axis v) = Plane(axis);
                (Point3 low, Point3 high) = BoxExtent(box);
                double u0 = low.Component(u).ToInches(), u1 = high.Component(u).ToInches();
                double v0 = low.Component(v).ToInches(), v1 = high.Component(v).ToInches();
                return (axis, place.Coordinate(axis)!.Value, [(u0, v0), (u1, v0), (u1, v1), (u0, v1)]);
            }

            default:
                return null;
        }
    }

    static (Point3 Low, Point3 High) BoxExtent(Box box)
    {
        IEnumerable<Point3> corners = new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }
            .SelectMany(corner => new[] { box.Vertex(corner, BoxLevel.Bottom), box.Vertex(corner, BoxLevel.Top) })
            .ToList();
        Point3 Extreme(Func<Length, Length, Length> pick) => corners.Aggregate((a, b) => new Point3(pick(a.X, b.X), pick(a.Y, b.Y), pick(a.Z, b.Z)));
        return (Extreme(Length.Min), Extreme(Length.Max));
    }

    /// <summary>The two axes a face square to <paramref name="normal"/> lies along.</summary>
    static (Axis U, Axis V) Plane(Axis normal) => normal switch
    {
        Axis.X => (Axis.Y, Axis.Z),
        Axis.Y => (Axis.X, Axis.Z),
        _ => (Axis.X, Axis.Y),
    };

    static double Pick((double X, double Y, double Z) point, Axis axis) => axis switch
    {
        Axis.X => point.X,
        Axis.Y => point.Y,
        _ => point.Z,
    };

    // Sutherland–Hodgman: the subject clipped by each edge of a convex clip polygon in turn. Both are
    // convex (a plane through a rectangular prism; a box face, or another such section), and either
    // winding is taken by testing against the clip's own orientation.
    static List<(double U, double V)> Clip(List<(double U, double V)> subject, List<(double U, double V)> clip)
    {
        double orientation = Math.Sign(Area(clip));
        List<(double U, double V)> output = subject;
        for (int i = 0; i < clip.Count && output.Count > 0; i++)
        {
            (double U, double V) a = clip[i], b = clip[(i + 1) % clip.Count];
            double Side((double U, double V) p) => orientation * (((b.U - a.U) * (p.V - a.V)) - ((b.V - a.V) * (p.U - a.U)));

            List<(double U, double V)> input = output;
            output = [];
            for (int j = 0; j < input.Count; j++)
            {
                (double U, double V) current = input[j], previous = input[(j + input.Count - 1) % input.Count];
                double sc = Side(current), sp = Side(previous);
                if (sc >= -1e-12)
                {
                    if (sp < -1e-12)
                    {
                        output.Add(Cross(previous, current, sp, sc));
                    }

                    output.Add(current);
                }
                else if (sp >= -1e-12)
                {
                    output.Add(Cross(previous, current, sp, sc));
                }
            }
        }

        return output;
    }

    static (double U, double V) Cross((double U, double V) p, (double U, double V) q, double sp, double sq)
    {
        double t = sp / (sp - sq);
        return (p.U + ((q.U - p.U) * t), p.V + ((q.V - p.V) * t));
    }

    static double Area(List<(double U, double V)> polygon)
    {
        double twice = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            (double U, double V) a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            twice += (a.U * b.V) - (b.U * a.V);
        }

        return twice / 2;
    }
}
