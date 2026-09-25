using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// An exact fraction, always reduced, with a positive denominator. Interpolated spans are kept as
/// one of these in 1/1024″ units and compared exactly; never a floating-point number (design §1.5).
/// </summary>
public readonly record struct ExactFraction : IComparable<ExactFraction>
{
    /// <summary>Builds <paramref name="numerator"/>/<paramref name="denominator"/>, reduced.</summary>
    /// <exception cref="DivideByZeroException">A zero denominator.</exception>
    public ExactFraction(Int128 numerator, Int128 denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException("A fraction's denominator is not zero.");
        }

        if (denominator < 0)
        {
            numerator = -numerator;
            denominator = -denominator;
        }

        Int128 gcd = Gcd(Int128.Abs(numerator), denominator);
        Numerator = numerator / gcd;
        Denominator = denominator / gcd;
    }

    /// <summary>The numerator, sign included.</summary>
    public Int128 Numerator { get; }

    /// <summary>The denominator, positive.</summary>
    public Int128 Denominator { get; }

    /// <summary>A whole number.</summary>
    public static ExactFraction Whole(long value) => new(value, 1);

    /// <summary>The largest whole number not above this fraction.</summary>
    public Int128 Floor()
    {
        Int128 q = Numerator / Denominator;
        return Numerator < 0 && q * Denominator != Numerator ? q - 1 : q;
    }

    /// <inheritdoc/>
    public int CompareTo(ExactFraction other) => (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    /// <summary>Whether this is less than <paramref name="right"/>.</summary>
    public static bool operator <(ExactFraction left, ExactFraction right) => left.CompareTo(right) < 0;

    /// <summary>Whether this is greater than <paramref name="right"/>.</summary>
    public static bool operator >(ExactFraction left, ExactFraction right) => left.CompareTo(right) > 0;

    /// <summary>Whether this is at most <paramref name="right"/>.</summary>
    public static bool operator <=(ExactFraction left, ExactFraction right) => left.CompareTo(right) <= 0;

    /// <summary>Whether this is at least <paramref name="right"/>.</summary>
    public static bool operator >=(ExactFraction left, ExactFraction right) => left.CompareTo(right) >= 0;

    /// <summary>"1/40", or "3" for a whole number.</summary>
    public override string ToString() => Denominator == 1 ? $"{Numerator}" : $"{Numerator}/{Denominator}";

    private static Int128 Gcd(Int128 a, Int128 b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a == 0 ? 1 : a;
    }
}

/// <summary>
/// Linear interpolation of a header span between two table columns, exactly as a footnote that
/// permits it reads (design §4.4, decided 2026-09-25, Marc, #157). Pure arithmetic on exact
/// fractions; only called for an input strictly between the two columns a footnote declares.
/// </summary>
public static class SpanInterpolation
{
    /// <summary>1/16″ in 1/1024″ units: the step an interpolated span is shown rounded down to.</summary>
    public const long DisplayStep = Length.UnitsPerInch / 16;

    /// <summary>How far <paramref name="input"/> lies from <paramref name="lower"/> towards <paramref name="upper"/>: (input − lower) / (upper − lower).</summary>
    /// <exception cref="ArgumentException">The bounds are not increasing.</exception>
    public static ExactFraction Weight(ExactFraction input, long lower, long upper)
    {
        if (upper <= lower)
        {
            throw new ArgumentException("The upper column is above the lower one.", nameof(upper));
        }

        // (p/q − L) / (U − L) = (p − L·q) / (q·(U − L))
        return new ExactFraction(input.Numerator - (lower * input.Denominator), input.Denominator * (upper - lower));
    }

    /// <summary>
    /// The interpolated span in 1/1024″ units: lowerSpan + (upperSpan − lowerSpan) × weight, exact.
    /// </summary>
    public static ExactFraction Span(long lowerSpanUnits, long upperSpanUnits, ExactFraction weight)
        => new(
            (lowerSpanUnits * weight.Denominator) + ((upperSpanUnits - (Int128)lowerSpanUnits) * weight.Numerator),
            weight.Denominator);

    /// <summary>Whether an opening of <paramref name="opening"/> is at most the exact span: compared without rounding.</summary>
    public static bool Covers(ExactFraction spanUnits, Length opening)
        => opening.Units * spanUnits.Denominator <= spanUnits.Numerator;

    /// <summary>The span shown to a person: rounded DOWN to a whole 1/16″, never up.</summary>
    public static Length Shown(ExactFraction spanUnits)
    {
        Int128 floor = spanUnits.Floor();
        Int128 steps = floor >= 0 ? floor / DisplayStep : ((floor + 1) / DisplayStep) - 1;
        return new Length((long)(steps * DisplayStep));
    }
}
