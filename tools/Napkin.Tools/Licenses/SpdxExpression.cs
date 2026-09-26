namespace Napkin.Tools.Licenses;

/// <summary>
/// Whether an SPDX license expression is satisfiable from an allowlist (#2): <c>A OR B</c> needs one
/// side, <c>A AND B</c> both, parentheses group, and <c>A WITH exception</c> is judged as <c>A</c>
/// (an exception only ever grants more). A malformed expression is never allowed.
/// </summary>
public static class SpdxExpression
{
    /// <summary>Whether <paramref name="expression"/> can be used under the allowlist.</summary>
    public static bool IsAllowed(string expression, IReadOnlySet<string> allowed)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(allowed);

        List<string> tokens = Tokens(expression);
        int at = 0;
        bool? result = Or(tokens, ref at, allowed);
        return result == true && at == tokens.Count;
    }

    static List<string> Tokens(string expression)
    {
        List<string> tokens = [];
        foreach (string word in expression.Replace("(", " ( ", StringComparison.Ordinal).Replace(")", " ) ", StringComparison.Ordinal)
                     .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            tokens.Add(word);
        }

        return tokens;
    }

    // or  := and ("OR" and)*
    static bool? Or(List<string> tokens, ref int at, IReadOnlySet<string> allowed)
    {
        bool? any = And(tokens, ref at, allowed);
        while (any is not null && at < tokens.Count && tokens[at] == "OR")
        {
            at++;
            bool? next = And(tokens, ref at, allowed);
            any = next is null ? null : any.Value || next.Value;
        }

        return any;
    }

    // and := term ("AND" term)*
    static bool? And(List<string> tokens, ref int at, IReadOnlySet<string> allowed)
    {
        bool? all = Term(tokens, ref at, allowed);
        while (all is not null && at < tokens.Count && tokens[at] == "AND")
        {
            at++;
            bool? next = Term(tokens, ref at, allowed);
            all = next is null ? null : all.Value && next.Value;
        }

        return all;
    }

    // term := "(" or ")" | id ["WITH" id]
    static bool? Term(List<string> tokens, ref int at, IReadOnlySet<string> allowed)
    {
        if (at >= tokens.Count)
        {
            return null;
        }

        string token = tokens[at++];
        if (token == "(")
        {
            bool? inner = Or(tokens, ref at, allowed);
            if (inner is null || at >= tokens.Count || tokens[at] != ")")
            {
                return null;
            }

            at++;
            return inner;
        }

        if (token is ")" or "AND" or "OR" or "WITH")
        {
            return null;
        }

        if (at < tokens.Count && tokens[at] == "WITH")
        {
            at++;
            if (at >= tokens.Count || tokens[at] is "(" or ")" or "AND" or "OR" or "WITH")
            {
                return null;
            }

            at++;
        }

        return allowed.Contains(token);
    }
}
