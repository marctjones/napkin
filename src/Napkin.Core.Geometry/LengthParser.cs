namespace Napkin.Core.Geometry;

public readonly partial record struct Length
{
    /// <summary>
    /// Reads feet-inch-fraction text into an exact length.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Accepts <c>6'-3 5/16"</c>, <c>6' 3-5/16"</c>, <c>6ft 3in</c>, <c>75 5/16</c>,
    /// <c>75.3125</c>, <c>.75</c>, <c>3/4</c> and <c>-1/2</c>. Rejects units it does not know
    /// (mm, cm), empty text and malformed text, including a half-typed number like <c>5.</c>
    /// (design &#xA7;1.5). Metric never enters: per DESIGN.md &#xA7;10 a future metric layer is
    /// display-only.
    /// </para>
    /// <para>
    /// Parsing is exact rational arithmetic, never <see cref="double"/>. A value that does not
    /// land on the 1/1024&#x2033; grid is moved to the nearest unit, half away from zero, and
    /// <paramref name="wasRounded"/> says so.
    /// </para>
    /// </remarks>
    /// <param name="text">The text to read.</param>
    /// <param name="value">The parsed length, or <see cref="Zero"/> when parsing failed.</param>
    /// <param name="wasRounded">
    /// Whether the parsed value had to be moved onto the grid. Always <see langword="false"/> when
    /// this method returns <see langword="false"/>.
    /// </param>
    /// <returns>Whether the text was understood.</returns>
    public static bool TryParse(string? text, out Length value, out bool wasRounded)
    {
        try
        {
            return TryParseChecked(text, out value, out wasRounded);
        }
        catch (OverflowException)
        {
            // A numeral long enough to overflow Int128 is not a length; §1.3's promise is that
            // arithmetic is checked, so this is a refusal and never a wrapped-around answer.
            value = Zero;
            wasRounded = false;
            return false;
        }
    }

    private static bool TryParseChecked(string? text, out Length value, out bool wasRounded)
    {
        value = Zero;
        wasRounded = false;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string s = Normalize(text);
        int i = 0;

        int sign = 1;
        if (s[i] == '-')
        {
            sign = -1;
            i++;
        }
        else if (s[i] == '+')
        {
            i++;
        }

        SkipSpaces(s, ref i);

        // Inches accumulate as an exact rational so that 3.505" and 1/3" are handled by the same
        // code as 6'-3 5/16" and rounding happens once, at the end.
        Int128 numerator = 0;
        Int128 denominator = 1;

        if (!TryReadNumber(s, ref i, out Int128 firstNumerator, out Int128 firstDenominator, out bool firstIsInteger))
        {
            return false;
        }

        bool startedWithFeet = TryReadFeetMarker(s, ref i);
        if (startedWithFeet)
        {
            Add(ref numerator, ref denominator, firstNumerator * 12, firstDenominator);

            // One '-' or run of spaces (or both) may separate the feet from the inches.
            SkipSpaces(s, ref i);
            if (i < s.Length && s[i] == '-')
            {
                i++;
                SkipSpaces(s, ref i);
            }

            if (i == s.Length)
            {
                return Finish(sign, numerator, denominator, out value, out wasRounded);
            }

            if (!TryReadNumber(s, ref i, out firstNumerator, out firstDenominator, out firstIsInteger))
            {
                return false;
            }
        }

        if (!TryReadInchesPart(s, ref i, firstNumerator, firstDenominator, firstIsInteger, ref numerator, ref denominator))
        {
            return false;
        }

        TryReadInchMarker(s, ref i);
        SkipSpaces(s, ref i);

        // Anything left over — "5mm", "3 apples" — means the text was not understood.
        return i == s.Length && Finish(sign, numerator, denominator, out value, out wasRounded);
    }

    /// <summary>
    /// Reads feet-inch-fraction text into an exact length, throwing when it is not understood.
    /// </summary>
    /// <exception cref="FormatException">The text was not understood.</exception>
    public static Length Parse(string text)
        => TryParse(text, out Length value, out _)
            ? value
            : throw new FormatException($"'{text}' is not a length napkin understands.");

    /// <summary>
    /// The inches part: a whole number, a fraction, a whole number and a fraction, or a decimal.
    /// The first number has already been read.
    /// </summary>
    private static bool TryReadInchesPart(
        string s,
        ref int i,
        Int128 firstNumerator,
        Int128 firstDenominator,
        bool firstIsInteger,
        ref Int128 numerator,
        ref Int128 denominator)
    {
        // "3/4": the number just read is the numerator of a fraction.
        if (i < s.Length && s[i] == '/')
        {
            if (!firstIsInteger)
            {
                return false;
            }

            i++;
            if (!TryReadNumber(s, ref i, out Int128 fractionDenominator, out Int128 one, out bool isInteger)
                || !isInteger
                || one != 1
                || fractionDenominator <= 0)
            {
                return false;
            }

            Add(ref numerator, ref denominator, firstNumerator, fractionDenominator);
            return true;
        }

        Add(ref numerator, ref denominator, firstNumerator, firstDenominator);

        // "3 5/16" or "3-5/16": a separator, then a fraction. A whole number with no slash after
        // the separator ("6 3") is malformed.
        int save = i;
        bool sawSeparator = false;
        if (i < s.Length && (s[i] == ' ' || s[i] == '-'))
        {
            sawSeparator = true;
            i++;
            SkipSpaces(s, ref i);
        }

        if (!sawSeparator || i == s.Length || !char.IsAsciiDigit(s[i]))
        {
            i = save;
            return true;
        }

        if (!firstIsInteger)
        {
            return false;
        }

        if (!TryReadNumber(s, ref i, out Int128 fractionNumerator, out Int128 unit, out bool numeratorIsInteger)
            || !numeratorIsInteger
            || unit != 1
            || i == s.Length
            || s[i] != '/')
        {
            return false;
        }

        i++;
        if (!TryReadNumber(s, ref i, out Int128 fractionDenominator2, out Int128 one2, out bool denominatorIsInteger)
            || !denominatorIsInteger
            || one2 != 1
            || fractionDenominator2 <= 0)
        {
            return false;
        }

        Add(ref numerator, ref denominator, fractionNumerator, fractionDenominator2);
        return true;
    }

    /// <summary>Reads an unsigned integer or decimal as an exact rational.</summary>
    private static bool TryReadNumber(string s, ref int i, out Int128 numerator, out Int128 denominator, out bool isInteger)
    {
        numerator = 0;
        denominator = 1;
        isInteger = true;

        int start = i;
        while (i < s.Length && char.IsAsciiDigit(s[i]))
        {
            numerator = checked((numerator * 10) + (s[i] - '0'));
            i++;
        }

        // ".75" is a common keystroke, so a number may start at the point. "5." is not, and stays
        // malformed: a trailing point is a half-typed number, not a value.
        bool anyDigits = i > start;
        if (!anyDigits && (i == s.Length || s[i] != '.'))
        {
            return false;
        }

        if (i < s.Length && s[i] == '.')
        {
            int afterPoint = i + 1;
            if (afterPoint == s.Length || !char.IsAsciiDigit(s[afterPoint]))
            {
                return false;
            }

            isInteger = false;
            i = afterPoint;
            while (i < s.Length && char.IsAsciiDigit(s[i]))
            {
                numerator = checked((numerator * 10) + (s[i] - '0'));
                denominator = checked(denominator * 10);
                i++;
            }
        }

        return true;
    }

    private static bool TryReadFeetMarker(string s, ref int i)
    {
        int save = i;
        SkipSpaces(s, ref i);

        if (i < s.Length && s[i] == '\'')
        {
            i++;
            return true;
        }

        if (TryReadWord(s, ref i, "feet") || TryReadWord(s, ref i, "ft"))
        {
            return true;
        }

        i = save;
        return false;
    }

    private static void TryReadInchMarker(string s, ref int i)
    {
        int save = i;
        SkipSpaces(s, ref i);

        if (i < s.Length && s[i] == '"')
        {
            i++;
            return;
        }

        if (TryReadWord(s, ref i, "inches") || TryReadWord(s, ref i, "inch") || TryReadWord(s, ref i, "in"))
        {
            return;
        }

        i = save;
    }

    private static bool TryReadWord(string s, ref int i, string word)
    {
        if (i + word.Length > s.Length)
        {
            return false;
        }

        for (int k = 0; k < word.Length; k++)
        {
            if (char.ToLowerInvariant(s[i + k]) != word[k])
            {
                return false;
            }
        }

        // "ft" must not match the start of "ftx"; a unit word ends the token.
        int after = i + word.Length;
        if (after < s.Length && char.IsAsciiLetter(s[after]))
        {
            return false;
        }

        i = after;
        return true;
    }

    private static void SkipSpaces(string s, ref int i)
    {
        while (i < s.Length && s[i] == ' ')
        {
            i++;
        }
    }

    /// <summary>Adds <paramref name="n"/>/<paramref name="d"/> inches to the running total.</summary>
    private static void Add(ref Int128 numerator, ref Int128 denominator, Int128 n, Int128 d)
    {
        numerator = checked((numerator * d) + (n * denominator));
        denominator = checked(denominator * d);
    }

    private static bool Finish(int sign, Int128 numerator, Int128 denominator, out Length value, out bool wasRounded)
    {
        value = Zero;
        wasRounded = false;

        Int128 scaled = checked(numerator * UnitsPerInch);
        wasRounded = scaled % denominator != 0;
        Int128 units = checked(DivideRounded(scaled, denominator, Rounding.HalfAwayFromZero) * sign);

        if (units > long.MaxValue || units < long.MinValue)
        {
            wasRounded = false;
            return false;
        }

        value = new Length((long)units);
        return true;
    }

    /// <summary>
    /// Folds the typographic marks a user may paste — &#x2033;, &#x2032;, en and em dashes,
    /// non-breaking spaces — onto the ASCII the scanner expects, and collapses whitespace.
    /// </summary>
    private static string Normalize(string text)
    {
        char[] buffer = new char[text.Length];
        int length = 0;

        foreach (char c in text)
        {
            char folded = c switch
            {
                '″' or '“' or '”' => '"',
                '′' or '‘' or '’' => '\'',
                '–' or '—' or '−' => '-',
                _ => char.IsWhiteSpace(c) ? ' ' : c,
            };

            buffer[length++] = folded;
        }

        return new string(buffer, 0, length).Trim();
    }
}
