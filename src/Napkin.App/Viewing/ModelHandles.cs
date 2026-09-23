using System.Collections.Immutable;
using Avalonia;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>Which of the 3D view's two kinds of handle.</summary>
public enum ModelHandleKind
{
    /// <summary>An arrow at the part's anchor along a world axis: a move along that axis.</summary>
    Move,

    /// <summary>A square at the centre of a face: a resize of the part across that face.</summary>
    Face,
}

/// <summary>
/// One handle on the selected part in the 3D view (<c>docs/design/assembly-model.md</c> &#xA7;8.3).
/// </summary>
/// <param name="Kind">A move arrow or a face handle.</param>
/// <param name="Axis">The world axis it drags along: the arrow's, or the face's normal's.</param>
/// <param name="Positive">Whether it points the positive way along <paramref name="Axis"/>.</param>
/// <param name="Face">For a face handle, the face of the box, in its own frame.</param>
/// <param name="At">Where it is grabbed, in the view's pixels: the arrow's tip, the face handle's square.</param>
/// <param name="Base">Where it is drawn from: the part's centre for an arrow, the face's centre for a face handle's stalk.</param>
public sealed record ModelHandle(ModelHandleKind Kind, Axis Axis, bool Positive, BoxFace? Face, Point At, Point Base)
{
    /// <summary>The world direction a drag on this handle is measured along.</summary>
    public Vector3d Direction => Vector3d.Along(Axis, Positive);
}

/// <summary>
/// Where the selected part's handles are in the 3D view, and which one a point has hold of.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Per-axis move arrows</strong> from the centre of the part, along world X, Y and Z — the
/// one gesture that says which axis a pointer drag means, which is the depth ambiguity they exist to
/// remove (&#xA7;8.3). They used to start at the anchor, the local south-west-bottom corner: the far
/// end of a long apron, or wherever that corner went after a turn, often behind the part (#83). An
/// arrow is a fixed number of pixels long, so it can be reached at any zoom; an axis pointing almost
/// straight at the eye has no length on the screen to drag along, and its arrow is left off rather
/// than offered and useless.
/// </para>
/// <para>
/// <strong>A face handle for each face the eye can see</strong>: a resize across that face. It
/// stands a little way out from the face's centre, along the face's outward normal as the screen
/// shows it, on a thin stalk (#84) — at the centre itself, the handles of a 3/4" board's two big
/// faces and its edge were a few pixels apart, and on the body a drag grabs to move the part. No
/// handle lands within two grab distances of another or of an arrow's tip: one that would is stood
/// further out until it does not. A face turned away is behind the part, and its handle with it;
/// orbiting brings it round.
/// </para>
/// </remarks>
public static class ModelHandles
{
    /// <summary>How long a move arrow is drawn, in pixels.</summary>
    public const double ArrowPixels = 56;

    /// <summary>
    /// How many pixels an inch along an axis has to cover, as a fraction of the scale, for the axis
    /// to be draggable: below this it points too nearly at the eye.
    /// </summary>
    public const double ShortestUsableFraction = 0.15;

    /// <summary>How far a face handle stands out from its face, in pixels, for a face seen edge-on.</summary>
    public const double FaceHandleStandOff = 16;

    /// <summary>How far apart any two handles are kept, in pixels: two grab distances.</summary>
    public const double Separation = 2 * ModelView.HandleGrabPixels;

    static readonly Axis[] Axes = [Axis.X, Axis.Y, Axis.Z];
    static readonly BoxFace[] Faces = [BoxFace.South, BoxFace.East, BoxFace.North, BoxFace.West, BoxFace.Bottom, BoxFace.Top];

    /// <summary>Every handle a box shows under a camera, arrows first.</summary>
    public static ImmutableArray<ModelHandle> Of(Box box, Camera camera)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (!box.Orientation.IsExact)
        {
            return [];
        }

        ImmutableArray<ModelHandle>.Builder handles = ImmutableArray.CreateBuilder<ModelHandle>();
        Point middle = camera.Project(Centre(box));
        foreach (Axis axis in Axes)
        {
            Vector along = camera.ProjectDirection(Vector3d.Along(axis));
            double pixels = along.Length;
            if (pixels < ShortestUsableFraction * camera.PixelsPerInch)
            {
                continue;
            }

            handles.Add(new ModelHandle(ModelHandleKind.Move, axis, true, null, middle + (along / pixels * ArrowPixels), middle));
        }

        foreach (BoxFace face in Faces)
        {
            (Axis axis, bool positive) = box.Orientation.Normal(face);
            if (Vector3d.Dot(Vector3d.Along(axis, positive), camera.TowardViewer) <= 1e-6)
            {
                continue;
            }

            // Out along the face's normal as the screen shows it: a face seen edge-on stands its
            // handle the full stand-off away, one facing the eye hardly at all — it is large on
            // the screen, and its centre is clear of everything else already.
            Point centre = camera.Project(CentreOf(box, face));
            Vector outward = camera.ProjectDirection(Vector3d.Along(axis, positive)) / Math.Max(camera.PixelsPerInch, 1e-9);
            Point at = centre + (outward * FaceHandleStandOff);
            Vector unit = outward.Length > 1e-6 ? outward / outward.Length : new Vector(0, -1);
            for (int step = 0; step < 16 && TooNear(at, handles); step++)
            {
                at += unit * (Separation / 2);
            }

            handles.Add(new ModelHandle(ModelHandleKind.Face, axis, positive, face, at, centre));
        }

        return handles.ToImmutable();
    }

    /// <summary>The middle of a box's extent, in inches: where its move arrows start.</summary>
    public static Vector3d Centre(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        Vector3d sum = Vector3d.Zero;
        foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.NorthEast])
        {
            sum += Vector3d.From(box.Vertex(corner, corner == BoxCorner.SouthWest ? BoxLevel.Bottom : BoxLevel.Top));
        }

        return sum / 2;
    }

    static bool TooNear(Point at, IEnumerable<ModelHandle> others) =>
        others.Any(other => Distance(other.At, at) < Separation);

    /// <summary>
    /// Only the move arrows, from the centre of a box — for several parts selected at once, whose
    /// combined extent this box stands for (#87): they move together, and no one face of theirs is
    /// the one to resize.
    /// </summary>
    public static ImmutableArray<ModelHandle> Arrows(Box box, Camera camera) =>
        [.. Of(box, camera).Where(handle => handle.Kind == ModelHandleKind.Move)];

    /// <summary>
    /// The handle a point has hold of: the nearest one within the tolerance, arrows winning a tie
    /// because a move is the more common gesture and the arrow the smaller target.
    /// </summary>
    public static ModelHandle? At(Box box, Camera camera, Point point, double tolerance) =>
        Nearest(Of(box, camera), point, tolerance);

    /// <summary>Of some handles, the nearest to a point within the tolerance; the first wins a tie.</summary>
    public static ModelHandle? Nearest(IEnumerable<ModelHandle> handles, Point point, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(handles);

        ModelHandle? best = null;
        double nearest = double.PositiveInfinity;
        foreach (ModelHandle handle in handles)
        {
            double distance = Distance(handle.At, point);
            if (distance <= tolerance && distance < nearest - 1e-9)
            {
                best = handle;
                nearest = distance;
            }
        }

        return best;
    }

    /// <summary>The four corners of one face of the blank, in the world, exact.</summary>
    public static ImmutableArray<Point3> CornersOf(Box box, BoxFace face)
    {
        ArgumentNullException.ThrowIfNull(box);

        Length w = box.Width, h = box.Height, d = box.Depth, o = Length.Zero;
        (Length X, Length Y, Length Z)[] local = face switch
        {
            BoxFace.South => [(o, o, o), (w, o, o), (w, o, d), (o, o, d)],
            BoxFace.North => [(w, h, o), (o, h, o), (o, h, d), (w, h, d)],
            BoxFace.West => [(o, h, o), (o, o, o), (o, o, d), (o, h, d)],
            BoxFace.East => [(w, o, o), (w, h, o), (w, h, d), (w, o, d)],
            BoxFace.Bottom => [(o, o, o), (o, h, o), (w, h, o), (w, o, o)],
            _ => [(o, o, d), (w, o, d), (w, h, d), (o, h, d)],
        };

        return [.. local.Select(corner => box.World(new Vector3(corner.X, corner.Y, corner.Z)))];
    }

    /// <summary>The centre of one face of the blank, in inches.</summary>
    public static Vector3d CentreOf(Box box, BoxFace face)
    {
        Vector3d sum = Vector3d.Zero;
        foreach (Point3 corner in CornersOf(box, face))
        {
            sum += Vector3d.From(corner);
        }

        return sum / 4;
    }

    /// <summary>The size of the box across a face: the local size along the axis the face is perpendicular to.</summary>
    public static Length SizeAcross(Box box, BoxFace face)
    {
        ArgumentNullException.ThrowIfNull(box);

        return face switch
        {
            BoxFace.East or BoxFace.West => box.Width,
            BoxFace.North or BoxFace.South => box.Height,
            _ => box.Depth,
        };
    }

    static double Distance(Point a, Point b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
