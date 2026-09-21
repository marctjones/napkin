namespace Napkin.Core.Geometry;

/// <summary>
/// A rotation, stored exactly as an integer count of arcseconds and normalised to
/// [0&#xB0;, 360&#xB0;).
/// </summary>
/// <remarks>
/// <para>
/// Arcseconds because a survey plat gives bearings in degrees-minutes-seconds
/// ("N 45&#xB0;30&#x2032;15&#x2033; E"), which the site plan (#21) must store exactly; and because
/// every common miter angle (45&#xB0;, 30&#xB0;, 22.5&#xB0;, 15&#xB0;, 7.5&#xB0;, 11.25&#xB0;) is
/// an exact integer of arcseconds (docs/design/geometry-model.md &#xA7;1.6).
/// </para>
/// <para>
/// Right-angle multiples are recognised exactly, and rotation by them is done by swapping and
/// negating coordinates, never by trigonometry — that is what keeps the first beta's rectilinear
/// geometry exact.
/// </para>
/// </remarks>
/// <param name="Arcseconds">The count of arcseconds; normalised into [0, 1 296 000).</param>
public readonly record struct Angle(long Arcseconds) : IComparable<Angle>
{
    /// <summary>Arcseconds in one degree.</summary>
    public const long ArcsecondsPerDegree = 3600;

    /// <summary>Arcseconds in a full turn.</summary>
    public const long FullTurn = 360 * ArcsecondsPerDegree;

    /// <summary>Arcseconds in a right angle.</summary>
    public const long RightAngleArcseconds = 90 * ArcsecondsPerDegree;

    /// <summary>Zero rotation.</summary>
    public static readonly Angle Zero = new(0);

    /// <summary>A right angle, 90&#xB0;.</summary>
    public static readonly Angle Right = new(RightAngleArcseconds);

    /// <summary>A half turn, 180&#xB0;.</summary>
    public static readonly Angle Straight = new(2 * RightAngleArcseconds);

    /// <summary>The count of arcseconds, always in [0, <see cref="FullTurn"/>).</summary>
    public long Arcseconds { get; } = Normalize(Arcseconds);

    /// <summary>An exact angle in degrees, minutes and seconds.</summary>
    public static Angle Degrees(long deg, long min = 0, long sec = 0)
        => new(checked(deg * ArcsecondsPerDegree + min * 60 + sec));

    /// <summary>
    /// The entry from <see cref="double"/>, used by the solver boundary
    /// (docs/design/geometry-model.md &#xA7;5.2 step 3).
    /// </summary>
    /// <exception cref="OverflowException">The value is not finite, or does not fit in a <see cref="long"/>.</exception>
    public static Angle FromDegrees(double degrees, Rounding rounding)
    {
        double scaled = degrees * ArcsecondsPerDegree;
        double rounded = Math.Round(scaled, Length.ToMidpointRounding(rounding));
        return new Angle(checked((long)rounded));
    }

    /// <summary>This angle in degrees.</summary>
    public double ToDegrees() => (double)Arcseconds / ArcsecondsPerDegree;

    /// <summary>This angle in radians. Only the solver path and non-right rotations need this.</summary>
    public double ToRadians() => ToDegrees() * Math.PI / 180.0;

    /// <summary>
    /// Whether this angle is 0&#xB0;, 90&#xB0;, 180&#xB0; or 270&#xB0;. A box at such a rotation
    /// has axis-aligned edges and exact corners.
    /// </summary>
    public bool IsRightAngleMultiple => Arcseconds % RightAngleArcseconds == 0;

    /// <summary>
    /// How many quarter turns this angle is, 0 to 3. Meaningful only when
    /// <see cref="IsRightAngleMultiple"/>; otherwise it is the truncated count.
    /// </summary>
    public int QuarterTurns => (int)(Arcseconds / RightAngleArcseconds);

    /// <summary>Rotate by whole quarter turns. Exact.</summary>
    public Angle Rotate90(int quarterTurns)
        => new(checked(Arcseconds + quarterTurns * RightAngleArcseconds));

    /// <inheritdoc/>
    public int CompareTo(Angle other) => Arcseconds.CompareTo(other.Arcseconds);

    /// <summary>Exact sum, normalised.</summary>
    public static Angle operator +(Angle a, Angle b) => new(checked(a.Arcseconds + b.Arcseconds));

    /// <summary>Exact difference, normalised.</summary>
    public static Angle operator -(Angle a, Angle b) => new(checked(a.Arcseconds - b.Arcseconds));

    /// <summary>Exact negation, normalised.</summary>
    public static Angle operator -(Angle value) => new(checked(-value.Arcseconds));

    /// <summary>Exact multiplication by an integer, normalised.</summary>
    public static Angle operator *(Angle a, long factor) => new(checked(a.Arcseconds * factor));

    /// <summary>Exact multiplication by an integer, normalised.</summary>
    public static Angle operator *(long factor, Angle a) => new(checked(factor * a.Arcseconds));

    private static long Normalize(long arcseconds)
    {
        long remainder = arcseconds % FullTurn;
        return remainder < 0 ? remainder + FullTurn : remainder;
    }
}
