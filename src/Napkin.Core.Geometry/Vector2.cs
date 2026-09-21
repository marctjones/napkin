namespace Napkin.Core.Geometry;

/// <summary>Which of the two axes a relationship or a measurement runs along.</summary>
public enum Axis
{
    /// <summary>The horizontal axis; X increases to the right.</summary>
    X,

    /// <summary>The vertical axis; Y increases upward (the CAD, DXF and PDF convention).</summary>
    Y,
}

/// <summary>
/// A displacement in the plan view: the difference of two <see cref="Point2"/>s.
/// </summary>
/// <param name="Dx">The displacement along X.</param>
/// <param name="Dy">The displacement along Y.</param>
public readonly record struct Vector2(Length Dx, Length Dy)
{
    /// <summary>The zero displacement.</summary>
    public static readonly Vector2 Zero = new(Length.Zero, Length.Zero);

    /// <summary>A displacement along one axis only.</summary>
    public static Vector2 Along(Axis axis, Length distance)
        => axis == Axis.X ? new Vector2(distance, Length.Zero) : new Vector2(Length.Zero, distance);

    /// <summary>The component along <paramref name="axis"/>.</summary>
    public Length Component(Axis axis) => axis == Axis.X ? Dx : Dy;

    /// <summary>This displacement with the component along <paramref name="axis"/> replaced.</summary>
    public Vector2 WithComponent(Axis axis, Length value)
        => axis == Axis.X ? this with { Dx = value } : this with { Dy = value };

    /// <summary>
    /// Rotates this displacement.
    /// </summary>
    /// <remarks>
    /// Exact for right-angle multiples, by swapping and negating coordinates rather than by
    /// trigonometry — that is what keeps the first beta's rectilinear geometry exact (§1.6). Any
    /// other angle goes through <see cref="double"/> and returns through
    /// <see cref="Length.FromInches"/> with <see cref="Rounding.HalfToEven"/>; that path is only
    /// reachable from the solver (§2.1).
    /// </remarks>
    public Vector2 Rotate(Angle angle)
    {
        if (angle.IsRightAngleMultiple)
        {
            return angle.QuarterTurns switch
            {
                0 => this,
                1 => new Vector2(-Dy, Dx),
                2 => new Vector2(-Dx, -Dy),
                _ => new Vector2(Dy, -Dx),
            };
        }

        double radians = angle.ToRadians();
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double x = Dx.ToInches();
        double y = Dy.ToInches();

        return new Vector2(
            Length.FromInches(x * cos - y * sin, Rounding.HalfToEven),
            Length.FromInches(x * sin + y * cos, Rounding.HalfToEven));
    }

    /// <summary>
    /// The length of this displacement. Euclidean, so it goes through <see cref="double"/> and is
    /// exact only for axis-aligned displacements; the tolerance-class residuals in §5.3 use it.
    /// </summary>
    public Length Magnitude(Rounding rounding = Rounding.HalfToEven)
    {
        if (Dx == Length.Zero)
        {
            return Length.Abs(Dy);
        }

        if (Dy == Length.Zero)
        {
            return Length.Abs(Dx);
        }

        double dx = Dx.ToInches();
        double dy = Dy.ToInches();
        return Length.FromInches(Math.Sqrt(dx * dx + dy * dy), rounding);
    }

    /// <summary>Exact sum.</summary>
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.Dx + b.Dx, a.Dy + b.Dy);

    /// <summary>Exact difference.</summary>
    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.Dx - b.Dx, a.Dy - b.Dy);

    /// <summary>Exact negation.</summary>
    public static Vector2 operator -(Vector2 value) => new(-value.Dx, -value.Dy);

    /// <summary>Exact multiplication by an integer.</summary>
    public static Vector2 operator *(Vector2 a, long factor) => new(a.Dx * factor, a.Dy * factor);

    /// <inheritdoc/>
    public override string ToString() => $"({Dx}, {Dy})";
}
