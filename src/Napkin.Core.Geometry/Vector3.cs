namespace Napkin.Core.Geometry;

/// <summary>
/// A displacement in space: the difference of two <see cref="Point3"/>s
/// (docs/design/assembly-model.md &#xA7;1.4). Every operation is exact integer arithmetic on the
/// three components; nothing here goes through <see cref="double"/>.
/// </summary>
/// <param name="Dx">The displacement along X.</param>
/// <param name="Dy">The displacement along Y.</param>
/// <param name="Dz">The displacement along Z.</param>
public readonly record struct Vector3(Length Dx, Length Dy, Length Dz)
{
    /// <summary>The zero displacement.</summary>
    public static readonly Vector3 Zero = new(Length.Zero, Length.Zero, Length.Zero);

    /// <summary>A displacement along one axis only.</summary>
    public static Vector3 Along(Axis axis, Length distance) => axis switch
    {
        Axis.X => new Vector3(distance, Length.Zero, Length.Zero),
        Axis.Y => new Vector3(Length.Zero, distance, Length.Zero),
        Axis.Z => new Vector3(Length.Zero, Length.Zero, distance),
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The component along <paramref name="axis"/>.</summary>
    public Length Component(Axis axis) => axis switch
    {
        Axis.X => Dx,
        Axis.Y => Dy,
        Axis.Z => Dz,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>This displacement with the component along <paramref name="axis"/> replaced.</summary>
    public Vector3 WithComponent(Axis axis, Length value) => axis switch
    {
        Axis.X => this with { Dx = value },
        Axis.Y => this with { Dy = value },
        Axis.Z => this with { Dz = value },
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The plan projection: X and Y, with Z dropped.</summary>
    public Vector2 XY => new(Dx, Dy);

    /// <summary>Exact sum.</summary>
    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.Dx + b.Dx, a.Dy + b.Dy, a.Dz + b.Dz);

    /// <summary>Exact difference.</summary>
    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.Dx - b.Dx, a.Dy - b.Dy, a.Dz - b.Dz);

    /// <summary>Exact negation.</summary>
    public static Vector3 operator -(Vector3 value) => new(-value.Dx, -value.Dy, -value.Dz);

    /// <summary>Exact multiplication by an integer.</summary>
    public static Vector3 operator *(Vector3 a, long factor) => new(a.Dx * factor, a.Dy * factor, a.Dz * factor);

    /// <inheritdoc/>
    public override string ToString() => $"({Dx}, {Dy}, {Dz})";
}
