using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// A point or a direction in model space, in inches, in <see cref="double"/>: the 3D view's own
/// arithmetic type (<c>docs/design/assembly-model.md</c> &#xA7;8.1).
/// </summary>
/// <remarks>
/// <para>
/// <strong>UI only.</strong> This is not <see cref="Vector3"/>, and nothing here is ever turned back
/// into one: the model's points are exact integers on the 1/1024&#x2033; grid, and this type exists
/// so that projection, picking and painting — which are inherently fractional — can happen at the
/// render edge without a <see cref="Length"/> ever being constructed from their results. It is to
/// the 3D view what the <c>(double X, double Y)</c> pairs of <see cref="ViewTransform"/> are to the
/// plan.
/// </para>
/// <para>
/// <see cref="System.Numerics.Vector3"/> is single precision, which is not enough for a building
/// site measured in inches, so this is a small record of its own.
/// </para>
/// </remarks>
/// <param name="X">Along world X, in inches.</param>
/// <param name="Y">Along world Y, in inches.</param>
/// <param name="Z">Along world Z, in inches.</param>
public readonly record struct Vector3d(double X, double Y, double Z)
{
    /// <summary>The origin, or no displacement.</summary>
    public static readonly Vector3d Zero = new(0, 0, 0);

    /// <summary>World +X.</summary>
    public static readonly Vector3d UnitX = new(1, 0, 0);

    /// <summary>World +Y.</summary>
    public static readonly Vector3d UnitY = new(0, 1, 0);

    /// <summary>World +Z.</summary>
    public static readonly Vector3d UnitZ = new(0, 0, 1);

    /// <summary>An exact model point, in inches.</summary>
    public static Vector3d From(Point3 point) => new(point.X.ToInches(), point.Y.ToInches(), point.Z.ToInches());

    /// <summary>An exact model displacement, in inches.</summary>
    public static Vector3d From(Vector3 displacement) =>
        new(displacement.Dx.ToInches(), displacement.Dy.ToInches(), displacement.Dz.ToInches());

    /// <summary>The unit vector along a world axis, pointing the positive or the negative way.</summary>
    public static Vector3d Along(Axis axis, bool positive = true)
    {
        double sign = positive ? 1 : -1;
        return axis switch
        {
            Axis.X => new Vector3d(sign, 0, 0),
            Axis.Y => new Vector3d(0, sign, 0),
            Axis.Z => new Vector3d(0, 0, sign),
            _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
        };
    }

    /// <summary>The component along a world axis.</summary>
    public double Component(Axis axis) => axis switch
    {
        Axis.X => X,
        Axis.Y => Y,
        Axis.Z => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The length.</summary>
    public double Length => Math.Sqrt(Dot(this, this));

    /// <summary>The same direction with length one, or zero when this has no direction.</summary>
    public Vector3d Normalized()
    {
        double length = Length;
        return length > 0 ? this / length : Zero;
    }

    /// <summary>The world axis this points most nearly along, and whether it points the positive way.</summary>
    public (Axis Axis, bool Positive) DominantAxis()
    {
        double x = Math.Abs(X), y = Math.Abs(Y), z = Math.Abs(Z);
        if (z >= x && z >= y)
        {
            return (Axis.Z, Z >= 0);
        }

        return x >= y ? (Axis.X, X >= 0) : (Axis.Y, Y >= 0);
    }

    /// <summary>The dot product.</summary>
    public static double Dot(Vector3d a, Vector3d b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

    /// <summary>The cross product, right-handed.</summary>
    public static Vector3d Cross(Vector3d a, Vector3d b) => new(
        (a.Y * b.Z) - (a.Z * b.Y),
        (a.Z * b.X) - (a.X * b.Z),
        (a.X * b.Y) - (a.Y * b.X));

    /// <summary>The sum.</summary>
    public static Vector3d operator +(Vector3d a, Vector3d b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>The difference.</summary>
    public static Vector3d operator -(Vector3d a, Vector3d b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>The negation.</summary>
    public static Vector3d operator -(Vector3d a) => new(-a.X, -a.Y, -a.Z);

    /// <summary>Scaled.</summary>
    public static Vector3d operator *(Vector3d a, double factor) => new(a.X * factor, a.Y * factor, a.Z * factor);

    /// <summary>Scaled.</summary>
    public static Vector3d operator *(double factor, Vector3d a) => a * factor;

    /// <summary>Divided.</summary>
    public static Vector3d operator /(Vector3d a, double divisor) => new(a.X / divisor, a.Y / divisor, a.Z / divisor);
}
