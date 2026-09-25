using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// An axis-aligned box in model space, in exact <see cref="Length"/>s: what the 3D view's
/// zoom-to-fit frames. <see cref="WorldBounds"/> with a third axis.
/// </summary>
/// <remarks>
/// Exact for the same reason <see cref="WorldBounds"/> is: it is accumulated from model
/// coordinates, and only the camera is allowed to leave the integer grid.
/// </remarks>
public readonly record struct Bounds3
{
    Bounds3(Point3 min, Point3 max, bool isEmpty)
    {
        Min = min;
        Max = max;
        IsEmpty = isEmpty;
    }

    /// <summary>Bounds containing nothing. Growing them by a point gives that point.</summary>
    public static readonly Bounds3 Empty = new(Point3.Origin, Point3.Origin, isEmpty: true);

    /// <summary>The corner with the least coordinate on every axis.</summary>
    public Point3 Min { get; }

    /// <summary>The corner with the greatest coordinate on every axis.</summary>
    public Point3 Max { get; }

    /// <summary>Whether nothing has been added yet.</summary>
    public bool IsEmpty { get; }

    /// <summary>The bounds of one point.</summary>
    public static Bounds3 Of(Point3 point) => new(point, point, isEmpty: false);

    /// <summary>
    /// Everything a sketch draws in space: every vertex of every box, and every node and segment
    /// end at the plan datum, Z = 0 (<c>docs/design/assembly-model.md</c> &#xA7;1.4).
    /// </summary>
    public static Bounds3 Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        Bounds3 bounds = Empty;
        foreach (Entity entity in sketch.Entities.Values)
        {
            switch (entity)
            {
                case Box box:
                    foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
                    {
                        bounds = bounds.Including(box.Vertex(corner, BoxLevel.Bottom));
                        bounds = bounds.Including(box.Vertex(corner, BoxLevel.Top));
                    }

                    break;

                case Node node:
                    bounds = bounds.Including(new Point3(node.Position.X, node.Position.Y, Length.Zero));
                    break;
            }
        }

        return bounds;
    }

    /// <summary>These bounds grown to take in a point.</summary>
    public Bounds3 Including(Point3 point) => IsEmpty
        ? Of(point)
        : new Bounds3(
            new Point3(Length.Min(Min.X, point.X), Length.Min(Min.Y, point.Y), Length.Min(Min.Z, point.Z)),
            new Point3(Length.Max(Max.X, point.X), Length.Max(Max.Y, point.Y), Length.Max(Max.Z, point.Z)),
            isEmpty: false);

    /// <summary>The eight corners, in inches, for projecting.</summary>
    public IEnumerable<Vector3d> CornersInInches()
    {
        if (IsEmpty)
        {
            yield break;
        }

        Vector3d low = Vector3d.From(Min);
        Vector3d high = Vector3d.From(Max);
        foreach (double x in (double[])[low.X, high.X])
        {
            foreach (double y in (double[])[low.Y, high.Y])
            {
                foreach (double z in (double[])[low.Z, high.Z])
                {
                    yield return new Vector3d(x, y, z);
                }
            }
        }
    }

    /// <summary>The middle, in inches.</summary>
    public Vector3d CenterInInches => IsEmpty
        ? Vector3d.Zero
        : (Vector3d.From(Min) + Vector3d.From(Max)) / 2;
}
