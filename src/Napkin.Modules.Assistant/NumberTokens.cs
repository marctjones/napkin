using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Assistant;

/// <summary>One number token in a piece of text: what was written, and the normalised key it is looked up by.</summary>
/// <param name="Text">The token as written, for the refusal sentence.</param>
/// <param name="Key">The normalised key: two ways of writing one value have the same key.</param>
public readonly record struct NumberToken(string Text, string Key);

/// <summary>
/// Finds every number token in a piece of text (docs/design/llm-assistant.md §4.2), for the answer
/// guard. The same reader runs over the answer and over the context pack, so a value is found in
/// the pack exactly when it is written there in any form the reader folds together.
/// </summary>
/// <remarks>
/// <para>
/// What is a token, and what folds together:
/// </para>
/// <list type="bullet">
/// <item>A table or section designation — <c>R602.7(1)</c>, <c>§R507.2</c>, <c>602.10.3</c> — keyed
/// upper-case with spaces and the section sign removed, so <c>r602.7(1)</c> is <c>R602.7(1)</c>.</item>
/// <item>A lumber name — <c>2x4</c>, <c>2 x 4</c>, <c>2×4s</c>, <c>5/4x6</c> — keyed lower-case with no
/// spaces and no plural. It matches only a lumber name: <c>2x6</c> is not supported by a <c>2</c> and a
/// <c>6</c> elsewhere.</item>
/// <item>A percentage — <c>12%</c>, <c>12 percent</c> — keyed by its number.</item>
/// <item>A length in any form <see cref="Length.TryParse"/> reads — <c>4'-0"</c>, <c>48″</c>,
/// <c>48 in</c>, <c>4 ft</c>, <c>3-foot</c> — and a bare number — <c>48</c>, <c>3/4</c>, <c>2.5</c>,
/// <c>1,240</c>, <c>2nd</c> — and a number word — <c>one</c> … <c>twenty</c>, the tens,
/// <c>twenty-four</c>, <c>hundred</c>, <c>thousand</c>, <c>dozen</c>: all keyed by the exact
/// length they come to in inches, printed in one canonical format (<see cref="Length.ToString"/>),
/// so <c>4'-0"</c>, <c>48"</c>, <c>48 in</c> and <c>48</c> are one token and <c>two</c> is <c>2</c>. A
/// number that is not on the 1/1024″ grid keys by its own text, so <c>0.2</c> never matches
/// <c>0.2001</c>.</item>
/// <item>Anything else with a digit in it — <c>10d</c>, a date, a digit in a word — keyed by its exact
/// text. Every digit in a sentence lands in some token: nothing with a number in it escapes the
/// guard by being unusual.</item>
/// </list>
/// <para>
/// Item references, <c>[5]</c> or <c>[5, 6]</c>, are not numbers: they are read separately and
/// blanked before the tokens are found, on both sides.
/// </para>
/// </remarks>
public static partial class NumberTokens
{
    /// <summary>
    /// The prefix a table or section designation's key starts with (<c>R602.7(1)</c>, <c>§R507</c>):
    /// the only kind of number token a help item may support in <see cref="AnswerGuard"/> (§14 item
    /// 13, decided option (b), issue #230) — a help section's own worked example (building.md's
    /// "Header (2) 2x10 …") must never stand in for a size, count or length napkin actually computed.
    /// </summary>
    public const string DesignationPrefix = "D:";

    private const string Mark = """(?:"|″|”|“|\s*(?:inches|inch|in)\b)""";
    private const string Fraction = @"(?:\.\d+|\s*-?\s*\d+/\d+|/\d+)";
    private const string Before = @"(?<![A-Za-z0-9./])";

    private static readonly Regex Tokens = new(
        string.Join(
            "|",
            // Table and section designations.
            @"(?<des>§\s*[A-Za-z]{0,3}\d+(?:\.\d+)*(?:\([0-9A-Za-z]{1,3}\))*"
            + @"|(?<![A-Za-z0-9])[A-Za-z]{1,3}\d+(?:\.\d+)*(?:\([0-9A-Za-z]{1,3}\))*(?![A-Za-z0-9])"
            + @"|(?<![A-Za-z0-9.])\d+(?:\.\d+){2,}(?:\([0-9A-Za-z]{1,3}\))*"
            + @"|(?<![A-Za-z0-9.])\d+\.\d+(?:\([0-9A-Za-z]{1,3}\))+)",

            // Lumber names: 2x4, 2 x 4, 2×4s, 5/4x6, 4x8.
            $@"(?<lum>{Before}\d+(?:/\d+)?\s*[x×]\s*\d+(?:/\d+)?(?:\s*[x×]\s*\d+(?:/\d+)?)?(?:['’]?s)?(?![A-Za-z0-9]))",

            // Percentages.
            $@"(?<pct>{Before}\d+(?:\.\d+)?\s*(?:%|percent\b))",

            // Feet, with or without inches: 4'-0", 6' 3 5/16", 4 ft, 3-foot, 6 ft 3 in. An
            // apostrophe before "s" is a possessive ("Wall 1's"), not feet.
            $@"(?<ft>{Before}\d+(?:\.\d+)?(?:['′’](?!s\b)|\s*-?\s*(?:feet|foot|ft)\b)"
            + $@"(?:\s*-\s*\d+{Fraction}?(?:{Mark})?|\s*\d+{Fraction}?{Mark})?)",

            // Inches with a mark: 48", 3 1/2″, 3-5/16", 3/4 in, .75".
            $@"(?<inch>{Before}(?:\d+{Fraction}?|\.\d+){Mark})",

            // Bare numbers: 1,240; 2 3/4; 3/4; .75; 2.5; 48; 2nd.
            $@"(?<num>{Before}(?:\d{{1,3}}(?:,\d{{3}})+(?:\.\d+)?|\d+\s+\d+/\d+|\d+/\d+|\d*\.\d+|\d+)(?:st|nd|rd|th)?(?![A-Za-z0-9]))",

            // Number words.
            @"(?<word>\b(?:(?:twenty|thirty|forty|fifty|sixty|seventy|eighty|ninety)(?:[\s-]+(?:one|two|three|four|five|six|seven|eight|nine)\b)?"
            + @"|zero|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|fourteen|fifteen|sixteen|seventeen|eighteen|nineteen"
            + @"|hundred|thousand|dozen)\b)",

            // Anything else with a digit in it.
            @"(?<other>[A-Za-z0-9]*\d[A-Za-z0-9]*)"),
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex References = new(@"\[\s*(\d+(?:\s*,\s*\d+)*)\s*\]", RegexOptions.CultureInvariant);

    private static readonly Regex UnitAfterHyphen = new(@"(\d)\s*-\s*(feet|foot|ft|inches|inch|in)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly ImmutableDictionary<string, int> Words = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["zero"] = 0, ["one"] = 1, ["two"] = 2, ["three"] = 3, ["four"] = 4, ["five"] = 5, ["six"] = 6, ["seven"] = 7,
        ["eight"] = 8, ["nine"] = 9, ["ten"] = 10, ["eleven"] = 11, ["twelve"] = 12, ["thirteen"] = 13, ["fourteen"] = 14,
        ["fifteen"] = 15, ["sixteen"] = 16, ["seventeen"] = 17, ["eighteen"] = 18, ["nineteen"] = 19, ["twenty"] = 20,
        ["thirty"] = 30, ["forty"] = 40, ["fifty"] = 50, ["sixty"] = 60, ["seventy"] = 70, ["eighty"] = 80, ["ninety"] = 90,
        ["hundred"] = 100, ["thousand"] = 1000, ["dozen"] = 12,
    }.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The vulgar fractions a model may write, as the digits they stand for.</summary>
    private static readonly ImmutableDictionary<char, string> Vulgar = new Dictionary<char, string>
    {
        ['¼'] = " 1/4", ['½'] = " 1/2", ['¾'] = " 3/4", ['⅛'] = " 1/8", ['⅜'] = " 3/8", ['⅝'] = " 5/8", ['⅞'] = " 7/8",
        ['⅓'] = " 1/3", ['⅔'] = " 2/3", ['⅕'] = " 1/5", ['⅙'] = " 1/6", ['⅒'] = " 1/10", ['⅟'] = " 1/",
    }.ToImmutableDictionary();

    /// <summary>The item numbers a piece of text refers to, <c>[5]</c> and <c>[5, 6]</c>, in order, repeats kept.</summary>
    /// <param name="text">The text.</param>
    public static ImmutableArray<int> ReferencesIn(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<int> found = [];
        foreach (Match match in References.Matches(text))
        {
            foreach (string part in match.Groups[1].Value.Split(','))
            {
                // A reference too long for an int refers to no item; it is kept as one that does not exist.
                found.Add(int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : int.MaxValue);
            }
        }

        return [.. found];
    }

    /// <summary>Every number token in a piece of text, in order, item references excluded.</summary>
    /// <param name="text">The text.</param>
    public static ImmutableArray<NumberToken> In(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string plain = Unvulgar(References.Replace(text, match => new string(' ', match.Length)));
        List<NumberToken> tokens = [];
        bool[] covered = new bool[plain.Length];
        foreach (Match match in Tokens.Matches(plain))
        {
            tokens.Add(new NumberToken(match.Value.Trim(), KeyOf(match)));
            for (int i = match.Index; i < match.Index + match.Length; i++)
            {
                covered[i] = true;
            }
        }

        // The net under the net: a numeric character the patterns above did not reach (a digit from
        // another script, a superscript) is a token of its own, keyed by its exact text.
        for (int i = 0; i < plain.Length; i++)
        {
            if (!covered[i] && IsNumeric(plain[i]))
            {
                int start = i;
                while (i < plain.Length && !covered[i] && (IsNumeric(plain[i]) || char.IsLetter(plain[i])))
                {
                    i++;
                }

                string run = plain[start..i];
                tokens.Add(new NumberToken(run, "T:" + run.ToUpperInvariant()));
                i--;
            }
        }

        return [.. tokens];
    }

    /// <summary>Every key in a piece of text: what the pack side of the guard looks answers up in.</summary>
    /// <param name="text">The text.</param>
    public static IEnumerable<string> KeysIn(string text) => In(text).Select(token => token.Key);

    private static bool IsNumeric(char c)
        => char.IsDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.OtherNumber or UnicodeCategory.LetterNumber;

    private static string Unvulgar(string text)
    {
        if (!text.Any(Vulgar.ContainsKey))
        {
            return text;
        }

        StringBuilder folded = new(text.Length + 8);
        foreach (char c in text)
        {
            folded.Append(Vulgar.TryGetValue(c, out string? digits) ? digits : c.ToString());
        }

        return folded.ToString();
    }

    private static string KeyOf(Match match)
    {
        string text = match.Value.Trim();
        if (match.Groups["des"].Success)
        {
            return DesignationPrefix + Squeeze(text).Replace("§", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        }

        if (match.Groups["lum"].Success)
        {
            string lumber = Squeeze(text).Replace('×', 'x').Replace('X', 'x').ToLowerInvariant();
            lumber = lumber.EndsWith("'s", StringComparison.Ordinal) || lumber.EndsWith("’s", StringComparison.Ordinal) ? lumber[..^2]
                : lumber.EndsWith('s') ? lumber[..^1]
                : lumber;
            return "L:" + lumber;
        }

        if (match.Groups["pct"].Success)
        {
            string digits = PercentDigits().Match(text).Value;
            return decimal.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal percent)
                ? "P:" + percent.ToString("G29", CultureInfo.InvariantCulture)
                : "T:" + text.ToUpperInvariant();
        }

        if (match.Groups["word"].Success)
        {
            int value = text
                .Split([' ', '-', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Sum(word => Words[word]);
            return LengthKey(new Length(value * Length.UnitsPerInch));
        }

        if (match.Groups["other"].Success)
        {
            return "T:" + text.ToUpperInvariant();
        }

        // A length or a bare number: read by napkin's own length reader, a bare number as inches.
        string folded = text
            .Replace("″", "\"", StringComparison.Ordinal).Replace("”", "\"", StringComparison.Ordinal).Replace("“", "\"", StringComparison.Ordinal)
            .Replace("′", "'", StringComparison.Ordinal).Replace("’", "'", StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        folded = UnitAfterHyphen.Replace(folded, "$1 $2");
        folded = Foot().Replace(folded, "ft");
        folded = Ordinal().Replace(folded, "$1");
        if (Length.TryParse(folded, out Length length, out bool wasRounded))
        {
            return wasRounded ? "X:" + Squeeze(folded).ToLowerInvariant() : LengthKey(length);
        }

        return "T:" + Squeeze(folded).ToUpperInvariant();
    }

    private static string LengthKey(Length length) => "N:" + Length.Abs(length);

    private static string Squeeze(string text) => Whitespace().Replace(text, string.Empty);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\bfoot\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Foot();

    [GeneratedRegex(@"(\d)(?:st|nd|rd|th)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Ordinal();

    [GeneratedRegex(@"\d+(?:\.\d+)?")]
    private static partial Regex PercentDigits();
}
