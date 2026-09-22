namespace Napkin.Core.Geometry;

/// <summary>
/// A point in space. X and Y are the plan view's axes (<see cref="Point2"/>); Z is up, out of the
/// plan, so that X, Y, Z is right-handed (docs/design/assembly-model.md &#xA7;1.4).
/// </summary>
/// <param name="X">The coordinate along X.</param>
/// <param name="Y">The coordinate along Y.</param>
/// <param name="Z">The coordinate along Z.</param>
public readonly record struct Point3(Length X, Length Y, Length Z)
{
    /// <summary>The origin.</summary>
    public static readonly Point3 Origin = new(Length.Zero, Length.Zero, Length.Zero);

    /// <summary>A point from inch values, for readable call sites in tests and fixtures.</summary>
    public static Point3 Inches(long x, long y, long z) => new(Length.Inches(x), Length.Inches(y), Length.Inches(z));

    /// <summary>The coordinate along <paramref name="axis"/>.</summary>
    public Length Component(Axis axis) => axis switch
    {
        Axis.X => X,
        Axis.Y => Y,
        Axis.Z => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>This point with the coordinate along <paramref name="axis"/> replaced.</summary>
    public Point3 WithComponent(Axis axis, Length value) => axis switch
    {
        Axis.X => this with { X = value },
        Axis.Y => this with { Y = value },
        Axis.Z => this with { Z = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The plan projection: X and Y, with Z dropped.</summary>
    public Point2 XY => new(X, Y);

    /// <summary>The displacement from <paramref name="b"/> to <paramref name="a"/>.</summary>
    public static Vector3 operator -(Point3 a, Point3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    /// <summary>This point displaced.</summary>
    public static Point3 operator +(Point3 point, Vector3 displacement)
        => new(point.X + displacement.Dx, point.Y + displacement.Dy, point.Z + displacement.Dz);

    /// <summary>This point displaced backwards.</summary>
    public static Point3 operator -(Point3 point, Vector3 displacement)
        => new(point.X - displacement.Dx, point.Y - displacement.Dy, point.Z - displacement.Dz);

    /// <inheritdoc/>
    public override string ToString() => $"({X}, {Y}, {Z})";
}
