namespace Napkin.Core.Geometry;

/// <summary>
/// A length, stored exactly as an integer count of 1/1024 of an inch.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, exact and ordered. A power-of-two sub-inch grid represents every fraction a tape
/// measure or a lumber standard uses exactly — 1/2 &#x2026; 1/64, 23/32&#x2033; plywood,
/// 1&#xBD;&#x2033; &#xD7; 3&#xBD;&#x2033; for a 2&#xD7;4, 6&#x2032;-0&#x2033; header-span limits —
/// and it represents the midpoint of any 1/64&#x2033; value exactly, four halvings deep
/// (docs/design/geometry-model.md &#xA7;1.1).
/// </para>
/// <para>
/// All arithmetic is <c>checked</c>: an overflow throws <see cref="OverflowException"/> and is a
/// programming error, not a user-facing state. There is deliberately no <c>Length * double</c>
/// operator; the only entry from <see cref="double"/> is <see cref="FromInches"/>.
/// </para>
/// </remarks>
/// <param name="Units">The count of 1/1024-inch units.</param>
public readonly record struct Length(long Units) : IComparable<Length>
{
    /// <summary>Units in one inch. The grid is 1/1024&#x2033;.</summary>
    public const long UnitsPerInch = 1024;

    /// <summary>Units in one foot.</summary>
    public const long UnitsPerFoot = 12 * UnitsPerInch;

    /// <summary>The zero length.</summary>
    public static readonly Length Zero = new(0);

    /// <summary>
    /// An exact length in inches and a fraction of an inch, for example
    /// <c>Inches(3, 1, 2)</c> for 3&#xBD;&#x2033;.
    /// </summary>
    /// <remarks>
    /// Components are summed, so a negative length is written with negative components
    /// (<c>Inches(-3, -1, 2)</c> is -3&#xBD;&#x2033;). This constructor never rounds: a fraction
    /// that does not land on the grid — a third of an inch — throws. Use
    /// <see cref="FromInches"/> or <see cref="LengthText.TryParse(string, out Length, out bool)"/>
    /// for values that may need rounding.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The denominator is not positive.</exception>
    /// <exception cref="ArgumentException">The fraction does not land on the 1/1024&#x2033; grid.</exception>
    public static Length Inches(long whole, long numerator = 0, long denominator = 1)
    {
        if (denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator), denominator, "The denominator must be positive.");
        }

        Int128 scaled = (Int128)numerator * UnitsPerInch;
        if (scaled % denominator != 0)
        {
            throw new ArgumentException(
                $"{numerator}/{denominator} of an inch is not on the 1/{UnitsPerInch} inch grid.",
                nameof(numerator));
        }

        long fraction = checked((long)(scaled / denominator));
        return new Length(checked(whole * UnitsPerInch + fraction));
    }

    /// <summary>An exact length in whole feet.</summary>
    public static Length Feet(long feet) => new(checked(feet * UnitsPerFoot));

    /// <summary>
    /// An exact length in feet, inches and a fraction of an inch, for example
    /// <c>FeetInches(6, 3, 5, 16)</c> for 6&#x2032;-3 5/16&#x2033;.
    /// </summary>
    /// <remarks>Components are summed; a negative length is written with negative components.</remarks>
    public static Length FeetInches(long feet, long inches, long numerator = 0, long denominator = 1)
        => new(checked(feet * UnitsPerFoot + Inches(inches, numerator, denominator).Units));

    /// <summary>
    /// The only entry from <see cref="double"/>. Used by the solver boundary
    /// (docs/design/geometry-model.md &#xA7;5) and by decimal input.
    /// </summary>
    /// <exception cref="OverflowException">
    /// The value is not finite, or does not fit in a <see cref="long"/> count of units.
    /// </exception>
    public static Length FromInches(double inches, Rounding rounding)
    {
        double scaled = inches * UnitsPerInch;
        double rounded = Math.Round(scaled, ToMidpointRounding(rounding));
        return new Length(checked((long)rounded));
    }

    /// <summary>
    /// This length in inches. Exact for every value below 2&#x2075;&#xB3; units, which is what
    /// makes the fixed-point/double boundary lossless on the way in.
    /// </summary>
    public double ToInches() => (double)Units / UnitsPerInch;

    /// <summary>Divide by an integer, rounding as told.</summary>
    /// <exception cref="DivideByZeroException">The divisor is zero.</exception>
    public Length Divide(long divisor, Rounding rounding)
        => new(checked((long)DivideRounded(Units, divisor, rounding)));

    /// <summary>
    /// Divide by an integer only when the result lands on the grid.
    /// </summary>
    /// <returns><see langword="false"/> — with <paramref name="result"/> set to
    /// <see cref="Zero"/> — when the division is not exact or the divisor is zero.</returns>
    public bool TryDivideExact(long divisor, out Length result)
    {
        if (divisor == 0 || Units % divisor != 0)
        {
            result = Zero;
            return false;
        }

        result = new Length(checked(Units / divisor));
        return true;
    }

    /// <summary>Scale by the ratio <paramref name="numerator"/>/<paramref name="denominator"/>, rounding as told.</summary>
    /// <exception cref="DivideByZeroException">The denominator is zero.</exception>
    public Length Scale(long numerator, long denominator, Rounding rounding)
        => new(checked((long)DivideRounded((Int128)Units * numerator, denominator, rounding)));

    /// <summary>The absolute value.</summary>
    public static Length Abs(Length value) => new(checked(value.Units < 0 ? -value.Units : value.Units));

    /// <summary>The smaller of two lengths.</summary>
    public static Length Min(Length a, Length b) => a.Units <= b.Units ? a : b;

    /// <summary>The larger of two lengths.</summary>
    public static Length Max(Length a, Length b) => a.Units >= b.Units ? a : b;

    /// <inheritdoc/>
    public int CompareTo(Length other) => Units.CompareTo(other.Units);

    /// <summary>Exact sum.</summary>
    public static Length operator +(Length a, Length b) => new(checked(a.Units + b.Units));

    /// <summary>Exact difference.</summary>
    public static Length operator -(Length a, Length b) => new(checked(a.Units - b.Units));

    /// <summary>Exact negation.</summary>
    public static Length operator -(Length value) => new(checked(-value.Units));

    /// <summary>Exact multiplication by an integer.</summary>
    public static Length operator *(Length a, long factor) => new(checked(a.Units * factor));

    /// <summary>Exact multiplication by an integer.</summary>
    public static Length operator *(long factor, Length a) => new(checked(factor * a.Units));

    /// <summary>
    /// The ratio of two lengths, for display and proportion only. There is no
    /// <c>Length / long</c>; use <see cref="Divide"/>, which names its rounding.
    /// </summary>
    public static double operator /(Length a, Length b) => (double)a.Units / b.Units;

    /// <summary>Exact comparison.</summary>
    public static bool operator <(Length a, Length b) => a.Units < b.Units;

    /// <summary>Exact comparison.</summary>
    public static bool operator >(Length a, Length b) => a.Units > b.Units;

    /// <summary>Exact comparison.</summary>
    public static bool operator <=(Length a, Length b) => a.Units <= b.Units;

    /// <summary>Exact comparison.</summary>
    public static bool operator >=(Length a, Length b) => a.Units >= b.Units;

    internal static MidpointRounding ToMidpointRounding(Rounding rounding) => rounding switch
    {
        Rounding.HalfToEven => MidpointRounding.ToEven,
        Rounding.HalfAwayFromZero => MidpointRounding.AwayFromZero,
        _ => throw new ArgumentOutOfRangeException(nameof(rounding), rounding, "Unknown rounding mode."),
    };

    /// <summary>
    /// Integer division with an explicit rounding rule, exact for every input because it never
    /// goes through <see cref="double"/>.
    /// </summary>
    internal static Int128 DivideRounded(Int128 value, Int128 divisor, Rounding rounding)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException();
        }

        Int128 quotient = value / divisor;
        Int128 remainder = value - quotient * divisor;
        if (remainder == 0)
        {
            return quotient;
        }

        // The direction of "away from zero" for the true quotient.
        Int128 step = (value < 0) ^ (divisor < 0) ? (Int128)(-1) : (Int128)1;
        Int128 twiceRemainder = remainder < 0 ? -remainder : remainder;
        twiceRemainder *= 2;
        Int128 absDivisor = divisor < 0 ? -divisor : divisor;

        if (twiceRemainder > absDivisor)
        {
            return quotient + step;
        }

        if (twiceRemainder < absDivisor)
        {
            return quotient;
        }

        if (rounding == Rounding.HalfAwayFromZero)
        {
            return quotient + step;
        }

        return quotient % 2 == 0 ? quotient : quotient + step;
    }
}
