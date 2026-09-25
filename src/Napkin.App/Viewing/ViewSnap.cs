using Avalonia;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// The quick view-snap controls of the 3D view (#132): the camera angles that look along a world axis,
/// the "again for the other side" flip, and the cycle through a part's six faces. Pure, so it is
/// testable without a window; <see cref="ModelView"/> only applies the result. It sets angles and
/// framing and nothing else, so editing stays fully available and the user can orbit away.
/// </summary>
public static class ViewSnap
{
    /// <summary>How close to a direction the camera must already look for a press to count as "already there", in degrees.</summary>
    public const double AlignedToleranceDegrees = 0.5;

    /// <summary>
    /// The camera angles whose <see cref="Camera.TowardViewer"/> is the given direction (from the
    /// design toward the eye). Straight up or down give azimuth 0, the plan's own.
    /// </summary>
    public static (double AzimuthDegrees, double ElevationDegrees) AnglesFor(Vector3d towardViewer)
    {
        double length = Math.Sqrt(Vector3d.Dot(towardViewer, towardViewer));
        Vector3d n = new(towardViewer.X / length, towardViewer.Y / length, towardViewer.Z / length);
        double elevation = Math.Asin(Math.Clamp(n.Z, -1, 1)) * 180.0 / Math.PI;
        if (Math.Abs(n.Z) > 1 - 1e-12)
        {
            return (0, elevation);
        }

        double azimuth = Math.Atan2(n.X, -n.Y) * 180.0 / Math.PI;
        if (azimuth < 0)
        {
            azimuth += 360;
        }

        return (azimuth, elevation);
    }

    /// <summary>Whether the camera already looks along the direction (from that side), within <see cref="AlignedToleranceDegrees"/>.</summary>
    public static bool LooksAlong(Camera camera, Vector3d towardViewer)
    {
        double length = Math.Sqrt(Vector3d.Dot(towardViewer, towardViewer));
        double cosine = Vector3d.Dot(camera.TowardViewer, towardViewer) / length;
        return cosine >= Math.Cos(AlignedToleranceDegrees * Math.PI / 180.0);
    }

    /// <summary>
    /// The direction toward the eye for the X, Y or Z button: positive is Z from above, Y from the
    /// south (looking north), X from the east (looking west); the other side is below, north, west.
    /// </summary>
    public static Vector3d AxisDirection(Axis axis, bool positive)
    {
        double sign = positive ? 1 : -1;
        return axis switch
        {
            Axis.X => new Vector3d(sign, 0, 0),
            Axis.Y => new Vector3d(0, -sign, 0),
            Axis.Z => new Vector3d(0, 0, sign),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
        };
    }

    /// <summary>
    /// The camera after a press of an axis button: looking along the axis from the positive side, or
    /// from the other side when it already does. Projection, centre and scale are the camera's own.
    /// </summary>
    public static Camera LookAlong(Camera camera, Axis axis)
    {
        bool flip = LooksAlong(camera, AxisDirection(axis, positive: true));
        return Facing(camera, AxisDirection(axis, positive: !flip));
    }

    /// <summary>The camera looking from the direction (toward the eye), everything else kept.</summary>
    public static Camera Facing(Camera camera, Vector3d towardViewer)
    {
        (double azimuth, double elevation) = AnglesFor(towardViewer);
        return camera with { AzimuthDegrees = azimuth, ElevationDegrees = elevation };
    }

    /// <summary>The order the surface button steps through a part's faces (and the axis directions with nothing selected).</summary>
    public static IReadOnlyList<BoxFace> FaceOrder { get; } =
        [BoxFace.Top, BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom];

    /// <summary>A face's outward normal in the world (the direction toward an eye looking at it square-on).</summary>
    public static Vector3d OutwardNormal(Box box, BoxFace face)
    {
        (Axis axis, bool positive) = box.Orientation.Normal(face);
        double sign = positive ? 1 : -1;
        return axis switch
        {
            Axis.X => new Vector3d(sign, 0, 0),
            Axis.Y => new Vector3d(0, sign, 0),
            _ => new Vector3d(0, 0, sign),
        };
    }

    /// <summary>
    /// With nothing selected, step <paramref name="step"/> (wrapping) of the six axis directions:
    /// +Z (above), south, east, north, west, then below, the order of <see cref="FaceOrder"/> on an upright part.
    /// </summary>
    public static Vector3d AxisStep(int step) => Wrap(step) switch
    {
        0 => new Vector3d(0, 0, 1),
        1 => new Vector3d(0, -1, 0),
        2 => new Vector3d(1, 0, 0),
        3 => new Vector3d(0, 1, 0),
        4 => new Vector3d(-1, 0, 0),
        _ => new Vector3d(0, 0, -1),
    };

    /// <summary>The camera looking square-on at a face from outside, framed on the face, which is at the middle of the view (so the part is centred on it).</summary>
    public static Camera FaceOn(Camera camera, Box box, BoxFace face, double coveredRight = 0, double coveredTop = 0)
    {
        Camera facing = Facing(camera, OutwardNormal(box, face));
        // Frame the face itself, which lies flat to the view: the part's depth would put a near end
        // of it in front of a perspective eye and make the fit back away from it.
        Bounds3 bounds = Bounds3.Empty;
        foreach (Point3 corner in ModelHandles.CornersOf(box, face))
        {
            bounds = bounds.Including(corner);
        }

        return facing.FitTo(bounds, camera.Viewport, coveredRight: coveredRight, coveredTop: coveredTop);
    }

    /// <summary>The face at a step of the cycle, wrapping.</summary>
    public static BoxFace FaceAt(int step) => FaceOrder[Wrap(step)];

    static int Wrap(int step) => ((step % 6) + 6) % 6;
}
