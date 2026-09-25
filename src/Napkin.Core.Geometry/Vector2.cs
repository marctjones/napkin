namespace Napkin.Core.Geometry;

/// <summary>Which axis a relationship or a measurement runs along.</summary>
/// <remarks>
/// <see cref="Z"/> exists for the value types of docs/design/assembly-model.md &#xA7;1.4
/// (<see cref="Vector3"/>, <see cref="Point3"/>, <see cref="Orientation"/>) and for a box's
/// <see cref="Box.Depth"/>. The plan-view types — <see cref="Point2"/>, <see cref="Vector2"/> — have
/// no Z and throw when handed it, rather than quietly answering with Y; the propagator and the
/// checker are taught about it in &#xA7;10 steps 3 and 4.
/// </remarks>
public enum Axis
{
    /// <summary>The horizontal axis; X increases to the right.</summary>
    X,

    /// <summary>The vertical axis of the plan; Y increases upward (the CAD, DXF and PDF convention).</summary>
    Y,

    /// <summary>
    /// The axis out of the plan; Z increases toward the viewer of the plan view, so that X, Y, Z
    /// is right-handed (docs/design/assembly-model.md &#xA7;1.4).
    /// </summary>
    Z,
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is <see cref="Axis.Z"/>, which the plan does not have.</exception>
    public static Vector2 Along(Axis axis, Length distance) => axis switch
    {
        Axis.X => new Vector2(distance, Length.Zero),
        Axis.Y => new Vector2(Length.Zero, distance),
        _ => throw NotInThePlan(axis),
    };

    /// <summary>The component along <paramref name="axis"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is <see cref="Axis.Z"/>, which the plan does not have.</exception>
    public Length Component(Axis axis) => axis switch
    {
        Axis.X => Dx,
        Axis.Y => Dy,
        _ => throw NotInThePlan(axis),
    };

    /// <summary>This displacement with the component along <paramref name="axis"/> replaced.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is <see cref="Axis.Z"/>, which the plan does not have.</exception>
    public Vector2 WithComponent(Axis axis, Length value) => axis switch
    {
        Axis.X => this with { Dx = value },
        Axis.Y => this with { Dy = value },
        _ => throw NotInThePlan(axis),
    };

    /// <summary>
    /// The error a plan-only type gives when asked about an axis the plan does not have. Before
    /// <see cref="Axis.Z"/> existed these members were two-way tests, and a Z would have been read
    /// as Y with no complaint; now it is refused out loud.
    /// </summary>
    internal static ArgumentOutOfRangeException NotInThePlan(Axis axis) => new(
        nameof(axis),
        axis,
        axis == Axis.Z
            ? "The plan has only X and Y; Z is out of the plan, so a plan point or displacement has no component along it."
            : "Not an axis.");

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
