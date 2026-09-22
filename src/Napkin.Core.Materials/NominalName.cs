using System.Text;

namespace Napkin.Core.Materials;

/// <summary>
/// Turns what a person types into the one spelling the library looks up by.
/// </summary>
/// <remarks>
/// <para>
/// Nobody types a nominal size the same way twice. "2x4", "2 x 4", "2X4", "2×4" and "2 by 4" are
/// one item, and a lookup that only answered to the first of them would send a person to a search
/// box to guess at punctuation. The normalised form is lower case, has no spaces, and uses a plain
/// ASCII <c>x</c> between the two numbers.
/// </para>
/// <para>
/// What is deliberately *not* normalised away: the slash and the hyphen inside a fraction. "5/4x6"
/// decking and a "1-1/4" thickness both depend on them, so they are left alone — the normaliser
/// only touches whitespace, the multiplication signs, the word "by" between two digits, and case.
/// </para>
/// <para>
/// The normalised form is also the library's uniqueness key: two rows whose names normalise to the
/// same text are a duplicate and fail the load, across every table, so "2x4" can never be
/// ambiguous between two categories.
/// </para>
/// </remarks>
public static class NominalName
{
    /// <summary>The spelling the library stores and looks up by.</summary>
    /// <param name="name">What a person typed, or what a data file wrote.</param>
    /// <returns>
    /// The normalised name, which is empty when <paramref name="name"/> is null, empty or nothing
    /// but whitespace and separators.
    /// </returns>
    public static string Normalize(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        StringBuilder builder = new(name.Length);
        foreach (char c in name)
        {
            switch (c)
            {
                // Every multiplication sign a keyboard, an autocorrect or a catalogue produces.
                case 'X':
                case 'x':
                case '×': // MULTIPLICATION SIGN, what a word processor makes of "2x4"
                case '✕': // MULTIPLICATION X
                case '✖': // HEAVY MULTIPLICATION X
                case '*':
                    builder.Append('x');
                    break;

                // A double-prime or a straight quote after a number is an inch mark, not a name.
                case '"':
                case '″':
                case '’':
                    break;

                // An en or em dash typed where a hyphen was meant, inside "1-1/4".
                case '‐':
                case '‑':
                case '‒':
                case '–':
                case '—':
                    builder.Append('-');
                    break;

                default:
                    if (!char.IsWhiteSpace(c))
                    {
                        builder.Append(char.ToLowerInvariant(c));
                    }

                    break;
            }
        }

        return ReplaceWordBy(builder.ToString());
    }

    /// <summary>Whether two names mean the same item.</summary>
    public static bool AreSame(string? left, string? right)
        => Normalize(left) is { Length: > 0 } normalised && normalised == Normalize(right);

    /// <summary>
    /// "2by4" becomes "2x4". Whitespace is already gone by the time this runs, so the word is only
    /// replaced where it sits between two digits — never inside a name that happens to contain
    /// those letters.
    /// </summary>
    private static string ReplaceWordBy(string text)
    {
        int at = text.IndexOf("by", StringComparison.Ordinal);
        if (at <= 0 || at + 2 >= text.Length)
        {
            return text;
        }

        StringBuilder builder = new(text.Length);
        int from = 0;
        while (at > 0 && at + 2 < text.Length)
        {
            if (char.IsAsciiDigit(text[at - 1]) && char.IsAsciiDigit(text[at + 2]))
            {
                builder.Append(text, from, at - from).Append('x');
                from = at + 2;
            }

            at = text.IndexOf("by", at + 2, StringComparison.Ordinal);
            if (at < 0)
            {
                break;
            }
        }

        return builder.Append(text, from, text.Length - from).ToString();
    }
}
