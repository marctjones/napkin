using System.Globalization;
using System.Text;

namespace Napkin.Core.Geometry;

/// <summary>
/// How a <see cref="Length"/> is rendered as text. Feet-inch-fraction text is a <em>view</em> of a
/// length, never its representation (docs/design/geometry-model.md &#xA7;1.5).
/// </summary>
public abstract record LengthFormat
{
    private protected LengthFormat()
    {
    }

    /// <summary>Feet, inches and a fraction at 1/16&#x2033;, the usual drawing precision.</summary>
    public static readonly LengthFormat Default = new FeetInchesFormat(16);

    /// <summary>
    /// Inches and a fraction at 1/1024&#x2033;, which is always exact. Used by
    /// <see cref="Length.ToString"/> so that a failing assertion shows the stored value.
    /// </summary>
    public static readonly LengthFormat Exact = new InchesOnlyFormat((int)Length.UnitsPerInch);

    /// <summary>
    /// Validates a fraction denominator: a power of two that divides the 1/1024&#x2033; grid.
    /// </summary>
    /// <remarks>
    /// Design &#xA7;1.5 lists 1&#x2026;64, the precisions a tape measure shows. 1024 is also
    /// accepted because property P8 (&#xA7;7.2) asks for a lossless
    /// <c>Parse(Format(a, 1/1024))</c> round trip, which needs the full grid.
    /// </remarks>
    private protected static int ValidatedDenominator(int denominator)
    {
        if (denominator < 1 || denominator > Length.UnitsPerInch || Length.UnitsPerInch % denominator != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(denominator),
                denominator,
                $"The precision denominator must be a power of two from 1 to {Length.UnitsPerInch}.");
        }

        return denominator;
    }
}

/// <summary>Feet, inches and a fraction: <c>6'-3 5/16"</c>.</summary>
/// <param name="PrecisionDenominator">The fraction denominator, a power of two from 1 to 1024.</param>
public sealed record FeetInchesFormat(int PrecisionDenominator = 16) : LengthFormat
{
    /// <inheritdoc cref="FeetInchesFormat(int)"/>
    public int PrecisionDenominator { get; init; } = ValidatedDenominator(PrecisionDenominator);
}

/// <summary>Inches and a fraction, with no feet carry: <c>75 5/16"</c>.</summary>
/// <param name="PrecisionDenominator">The fraction denominator, a power of two from 1 to 1024.</param>
public sealed record InchesOnlyFormat(int PrecisionDenominator = 16) : LengthFormat
{
    /// <inheritdoc cref="InchesOnlyFormat(int)"/>
    public int PrecisionDenominator { get; init; } = ValidatedDenominator(PrecisionDenominator);
}

/// <summary>Decimal inches: <c>75.3125"</c>.</summary>
/// <param name="Digits">Digits after the decimal point, 0 to 18.</param>
public sealed record DecimalInchesFormat(int Digits = 4) : LengthFormat
{
    /// <inheritdoc cref="DecimalInchesFormat(int)"/>
    public int Digits { get; init; } = Digits is >= 0 and <= 18
        ? Digits
        : throw new ArgumentOutOfRangeException(nameof(Digits), Digits, "Digits must be between 0 and 18.");
}

/// <summary>
/// Text for a length, and whether that text is the whole truth.
/// </summary>
/// <remarks>
/// Nothing rounds silently: when <see cref="IsExact"/> is <see langword="false"/> the displayed
/// value is not the stored one, and the canvas (#11) marks it — with a leading
/// &#x2248;, for example — so that a cut list never shows "10&#x2033;" for a part that is
/// 10 1/1024&#x2033; without saying so (design &#xA7;1.4).
/// </remarks>
/// <param name="Text">The rendered text.</param>
/// <param name="IsExact">Whether the rendered text represents the stored value exactly.</param>
public readonly record struct FormattedLength(string Text, bool IsExact);

public readonly partial record struct Length
{
    /// <summary>
    /// Renders this length as text, reporting whether the text is exact.
    /// </summary>
    /// <remarks>
    /// Fractions are reduced (8/16 becomes 1/2), 12&#x2033; carries into feet in
    /// <see cref="FeetInchesFormat"/>, negatives keep their sign, and rounding to the requested
    /// precision is half away from zero — how a tape measure is read (design &#xA7;1.4).
    /// </remarks>
    public FormattedLength Format(LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        return format switch
        {
            FeetInchesFormat feetInches => FormatFraction(feetInches.PrecisionDenominator, carryFeet: true),
            InchesOnlyFormat inchesOnly => FormatFraction(inchesOnly.PrecisionDenominator, carryFeet: false),
            DecimalInchesFormat decimalInches => FormatDecimal(decimalInches.Digits),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown length format."),
        };
    }

    /// <summary>This length in exact inches and 1024ths, for diagnostics and assertion messages.</summary>
    public override string ToString() => Format(LengthFormat.Exact).Text;

    private FormattedLength FormatFraction(int denominator, bool carryFeet)
    {
        Int128 unitsPerTick = UnitsPerInch / denominator;
        Int128 ticks = DivideRounded(Units, unitsPerTick, Rounding.HalfAwayFromZero);
        Int128 roundedUnits = ticks * unitsPerTick;
        bool isExact = roundedUnits == Units;

        bool negative = ticks < 0;
        Int128 absTicks = negative ? -ticks : ticks;

        Int128 wholeInches = absTicks / denominator;
        Int128 remainderTicks = absTicks % denominator;

        long fractionNumerator = (long)remainderTicks;
        long fractionDenominator = denominator;
        if (fractionNumerator != 0)
        {
            long divisor = GreatestCommonDivisor(fractionNumerator, fractionDenominator);
            fractionNumerator /= divisor;
            fractionDenominator /= divisor;
        }

        StringBuilder text = new();
        if (negative)
        {
            text.Append('-');
        }

        Int128 feet = 0;
        if (carryFeet)
        {
            feet = wholeInches / 12;
            wholeInches %= 12;
        }

        if (feet != 0)
        {
            text.Append(Digits(feet)).Append("'-");
            // With feet shown, the whole inches are always shown too: 6'-0 5/16", not 6'-5/16".
            text.Append(Digits(wholeInches));
        }
        else if (wholeInches != 0 || fractionNumerator == 0)
        {
            text.Append(Digits(wholeInches));
        }

        if (fractionNumerator != 0)
        {
            if (text.Length > 0 && text[^1] != '-')
            {
                text.Append(' ');
            }

            text.Append(fractionNumerator.ToString(CultureInfo.InvariantCulture))
                .Append('/')
                .Append(fractionDenominator.ToString(CultureInfo.InvariantCulture));
        }

        text.Append('"');
        return new FormattedLength(text.ToString(), isExact);
    }

    private FormattedLength FormatDecimal(int digits)
    {
        Int128 powerOfTen = 1;
        for (int i = 0; i < digits; i++)
        {
            powerOfTen *= 10;
        }

        Int128 scaled = (Int128)Units * powerOfTen;
        bool isExact = scaled % UnitsPerInch == 0;
        Int128 rounded = DivideRounded(scaled, UnitsPerInch, Rounding.HalfAwayFromZero);

        bool negative = rounded < 0;
        Int128 absolute = negative ? -rounded : rounded;

        StringBuilder text = new();
        if (negative)
        {
            text.Append('-');
        }

        text.Append(Digits(absolute / powerOfTen));
        if (digits > 0)
        {
            text.Append('.').Append(Digits(absolute % powerOfTen).PadLeft(digits, '0'));
        }

        text.Append('"');
        return new FormattedLength(text.ToString(), isExact);
    }

    private static string Digits(Int128 value) => value.ToString(CultureInfo.InvariantCulture);

    private static long GreatestCommonDivisor(long a, long b)
    {
        while (b != 0)
        {
            (a, b) = (b, a % b);
        }

        return a;
    }
}
