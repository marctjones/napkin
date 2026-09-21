namespace Napkin.Tools.Features;

/// <summary>What a token is, to the extent <see cref="TraitScanner"/> cares.</summary>
public enum TokenKind
{
    /// <summary>An identifier or keyword — the scanner does not distinguish them.</summary>
    Word,

    /// <summary>A string literal, already unescaped.</summary>
    Text,

    /// <summary>A single punctuation character.</summary>
    Punctuation,
}

/// <param name="Kind">What sort of token this is.</param>
/// <param name="Value">The identifier, the decoded string, or the punctuation character.</param>
/// <param name="Line">One-based source line, for messages.</param>
public readonly record struct Token(TokenKind Kind, string Value, int Line);

/// <summary>
/// A deliberately small C# tokenizer: enough to find attributes, type declarations and method
/// declarations, and nothing more.
/// </summary>
/// <remarks>
/// Why not Roslyn: `tools/Napkin.Tools` uses the BCL only, and a full parse buys nothing here.
/// What this must get right is comments and string literals — so that a `[Trait("Feature", ...)]`
/// inside a comment or a string is not mistaken for a real claim — and brace depth, so that a
/// method is told apart from a statement inside one. It does both; everything else it skips.
/// </remarks>
public static class CSharpTokenizer
{
    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        var line = 1;
        var index = 0;

        while (index < source.Length)
        {
            var current = source[index];

            if (current == '\n')
            {
                line++;
                index++;
                continue;
            }

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            if (current == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    while (index < source.Length && source[index] != '\n')
                    {
                        index++;
                    }

                    continue;
                }

                if (source[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/'))
                    {
                        if (source[index] == '\n')
                        {
                            line++;
                        }

                        index++;
                    }

                    index = Math.Min(index + 2, source.Length);
                    continue;
                }
            }

            if (current is '_' || char.IsLetter(current))
            {
                var start = index;
                while (index < source.Length && (source[index] == '_' || char.IsLetterOrDigit(source[index])))
                {
                    index++;
                }

                tokens.Add(new Token(TokenKind.Word, source[start..index], line));
                continue;
            }

            if (char.IsDigit(current))
            {
                // Numbers are never interesting; consume the run so `1.5f` is not seen as a dot.
                while (index < source.Length &&
                       (char.IsLetterOrDigit(source[index]) || source[index] is '.' or '_'))
                {
                    index++;
                }

                continue;
            }

            if (current == '\'')
            {
                index = SkipCharLiteral(source, index, ref line);
                continue;
            }

            if (TryReadString(source, ref index, ref line, out var text))
            {
                tokens.Add(new Token(TokenKind.Text, text, line));
                continue;
            }

            tokens.Add(new Token(TokenKind.Punctuation, current.ToString(), line));
            index++;
        }

        return tokens;
    }

    private static int SkipCharLiteral(string source, int index, ref int line)
    {
        index++;
        while (index < source.Length && source[index] != '\'')
        {
            if (source[index] == '\\')
            {
                index++;
            }
            else if (source[index] == '\n')
            {
                line++;
            }

            index++;
        }

        return Math.Min(index + 1, source.Length);
    }

    /// <summary>
    /// Reads a regular, verbatim, interpolated or raw string starting at <paramref name="index"/>.
    /// Returns false when there is no string there.
    /// </summary>
    private static bool TryReadString(string source, ref int index, ref int line, out string value)
    {
        value = string.Empty;
        var start = index;

        // Prefixes in any order: $, @, $@, @$, $$ ... — none of them change where the string ends
        // for our purposes except @ (verbatim) and """ (raw).
        var verbatim = false;
        while (index < source.Length && source[index] is '$' or '@')
        {
            verbatim |= source[index] == '@';
            index++;
        }

        if (index >= source.Length || source[index] != '"')
        {
            index = start;
            return false;
        }

        if (!verbatim && index + 2 < source.Length && source[index + 1] == '"' && source[index + 2] == '"')
        {
            return ReadRawString(source, ref index, ref line, out value);
        }

        index++;
        var builder = new System.Text.StringBuilder();
        while (index < source.Length)
        {
            var current = source[index];

            if (current == '"')
            {
                if (verbatim && index + 1 < source.Length && source[index + 1] == '"')
                {
                    builder.Append('"');
                    index += 2;
                    continue;
                }

                index++;
                value = builder.ToString();
                return true;
            }

            if (!verbatim && current == '\\' && index + 1 < source.Length)
            {
                builder.Append(Unescape(source[index + 1]));
                index += 2;
                continue;
            }

            if (current == '\n')
            {
                line++;
            }

            builder.Append(current);
            index++;
        }

        value = builder.ToString();
        return true;
    }

    private static bool ReadRawString(string source, ref int index, ref int line, out string value)
    {
        var quotes = 0;
        while (index < source.Length && source[index] == '"')
        {
            quotes++;
            index++;
        }

        var terminator = new string('"', quotes);
        var end = source.IndexOf(terminator, index, StringComparison.Ordinal);
        if (end < 0)
        {
            end = source.Length;
        }

        var body = source[index..end];
        line += body.Count(character => character == '\n');
        index = Math.Min(end + quotes, source.Length);

        // Raw strings are used for prose in this repository, never for a feature id; the value is
        // returned trimmed of its layout so a caller comparing it to an id still behaves sanely.
        value = body.Trim('\r', '\n');
        return true;
    }

    private static char Unescape(char escaped) => escaped switch
    {
        'n' => '\n',
        'r' => '\r',
        't' => '\t',
        '0' => '\0',
        _ => escaped,
    };
}
