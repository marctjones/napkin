namespace Napkin.Tools.Features;

/// <summary>
/// One test method's claim on a feature.
/// </summary>
/// <param name="TypeName">
/// The fully-qualified type name as VSTest writes it, with `+` between nested types — so it joins
/// straight onto a TRX result's `className`.
/// </param>
/// <param name="MethodName">The method name without parameters; a theory's cases all share it.</param>
/// <param name="FeatureId">The value of one `[Trait("Feature", ...)]`.</param>
/// <param name="SourceFile">Absolute path to the file the claim was read from.</param>
/// <param name="Line">One-based line of the method declaration.</param>
public sealed record FeatureClaim(
    string TypeName,
    string MethodName,
    string FeatureId,
    string SourceFile,
    int Line)
{
    /// <summary>The key a TRX result is joined on.</summary>
    public string TestId => $"{TypeName}.{MethodName}";
}

/// <summary>
/// Finds `[Trait("Feature", "<ID>")]` in test source and maps it to the test it decorates.
/// </summary>
/// <remarks>
/// <para>
/// Why source and not the test results: a TRX file carries no traits. This was measured, not
/// assumed — a probe project with `[Trait("Feature", "GEO-LEN-001")]` on a passing fact produced
/// a TRX whose `&lt;TestDefinitions&gt;` hold only `className` and `name`; the string "Feature"
/// does not occur anywhere in the file. The VSTest TRX logger drops arbitrary traits, and xunit
/// 2.5.3 has no other supported way to get them into a result file.
/// </para>
/// <para>
/// So the mapping is recovered from the source tree and joined to the TRX on
/// `className` + method name. That also happens to be what `scorecard stubs` needs — it must know
/// which features a hand-written test already claims — so the repository has one mechanism rather
/// than two. The trait attribute stays exactly as issue #34 specifies; only the reader changes.
/// </para>
/// </remarks>
public static class TraitScanner
{
    /// <summary>The trait name that claims a feature.</summary>
    public const string TraitName = "Feature";

    /// <summary>
    /// The GUI suite's own claiming attribute (issue #33): `[GuiWorkflow("GUI-SHELL-01")]` takes
    /// the feature id as its first argument and publishes it as a `Feature` trait at run time.
    /// The scanner reads source, so it has to know the shorthand as well as the plain trait.
    /// </summary>
    public const string WorkflowAttributeName = "GuiWorkflow";

    /// <summary>Generated files are excluded when asking what a *hand-written* test claims.</summary>
    public const string GeneratedSuffix = ".g.cs";

    /// <summary>
    /// Every `*.cs` under <paramref name="directory"/>, skipping build output. Sorted, so a scan
    /// is reproducible.
    /// </summary>
    public static IReadOnlyList<string> FindSourceFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory
            .EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path, directory))
            .ToList();
        files.Sort(StringComparer.Ordinal);
        return files;
    }

    private static bool IsBuildOutput(string path, string root)
    {
        var relative = Path.GetRelativePath(root, path);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part =>
            part.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True for a file <c>scorecard stubs</c> must ignore when asking what exists already.</summary>
    public static bool IsGenerated(string path) =>
        Path.GetFileName(path).EndsWith(GeneratedSuffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>Scans the given files, in order.</summary>
    public static IReadOnlyList<FeatureClaim> Scan(IEnumerable<string> files)
    {
        var claims = new List<FeatureClaim>();
        foreach (var file in files)
        {
            claims.AddRange(ScanSource(File.ReadAllText(file), file));
        }

        return claims;
    }

    /// <summary>Scans one file's text. <paramref name="path"/> is only used for reporting.</summary>
    public static IReadOnlyList<FeatureClaim> ScanSource(string source, string path)
    {
        var tokens = CSharpTokenizer.Tokenize(source);
        var claims = new List<FeatureClaim>();
        var scopes = new List<Scope>();
        var pendingIds = new List<string>();
        Scope? pendingScope = null;
        var depth = 0;

        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];

            if (token.Kind == TokenKind.Punctuation)
            {
                switch (token.Value)
                {
                    case "[":
                        var close = MatchBracket(tokens, index);
                        pendingIds.AddRange(ReadFeatureIds(tokens, index + 1, close));
                        index = close;
                        continue;

                    case "{":
                        depth++;
                        if (pendingScope is not null)
                        {
                            scopes.Add(pendingScope with { EntryDepth = depth });
                            pendingScope = null;
                        }

                        pendingIds.Clear();
                        continue;

                    case "}":
                        depth--;
                        while (scopes.Count > 0 && scopes[^1].EntryDepth > depth)
                        {
                            scopes.RemoveAt(scopes.Count - 1);
                        }

                        pendingIds.Clear();
                        continue;

                    case ";":
                        // A file-scoped namespace has no brace; it owns the rest of the file.
                        if (pendingScope is { EntryDepth: -1, IsNamespace: true })
                        {
                            scopes.Add(pendingScope);
                        }

                        pendingScope = null;
                        pendingIds.Clear();
                        continue;

                    default:
                        continue;
                }
            }

            if (token.Kind != TokenKind.Word)
            {
                continue;
            }

            if (token.Value == "namespace")
            {
                var name = ReadDottedName(tokens, index + 1, out var next);
                pendingScope = new Scope(name, IsNamespace: true, [], EntryDepth: -1);
                index = next - 1;
                continue;
            }

            if (IsTypeKeyword(token.Value) && !IsGenericConstraint(tokens, index))
            {
                var nameIndex = index + 1;
                // `record class X` / `record struct X`: step over the second keyword.
                if (nameIndex < tokens.Count && IsTypeKeyword(tokens[nameIndex].Value))
                {
                    nameIndex++;
                }

                if (nameIndex < tokens.Count && tokens[nameIndex].Kind == TokenKind.Word)
                {
                    pendingScope = new Scope(
                        tokens[nameIndex].Value,
                        IsNamespace: false,
                        [.. pendingIds],
                        EntryDepth: -1);
                    pendingIds.Clear();
                    index = nameIndex;
                }

                continue;
            }

            // A method declaration: an identifier followed by `(` at the type's own brace depth.
            // Inside a method body the depth is deeper, so statements never match.
            if (scopes.Count > 0 && !scopes[^1].IsNamespace && scopes[^1].EntryDepth == depth &&
                IsMethodName(tokens, index))
            {
                var ids = pendingIds
                    .Concat(scopes.Where(scope => !scope.IsNamespace).SelectMany(scope => scope.FeatureIds))
                    .Distinct(StringComparer.Ordinal);
                var typeName = TypeName(scopes);
                foreach (var id in ids)
                {
                    claims.Add(new FeatureClaim(typeName, token.Value, id, path, token.Line));
                }

                pendingIds.Clear();
            }
        }

        return claims;
    }

    private static bool IsTypeKeyword(string word) =>
        word is "class" or "struct" or "record" or "interface";

    /// <summary>`where T : class` is not a type declaration.</summary>
    private static bool IsGenericConstraint(IReadOnlyList<Token> tokens, int index) =>
        index > 0 && tokens[index - 1] is { Kind: TokenKind.Punctuation, Value: ":" or "," };

    private static bool IsMethodName(IReadOnlyList<Token> tokens, int index)
    {
        var next = index + 1;
        if (next < tokens.Count && tokens[next] is { Kind: TokenKind.Punctuation, Value: "<" })
        {
            next = MatchAngle(tokens, next) + 1;
        }

        return next < tokens.Count && tokens[next] is { Kind: TokenKind.Punctuation, Value: "(" };
    }

    private static string TypeName(IReadOnlyList<Scope> scopes)
    {
        var namespaces = scopes.Where(scope => scope.IsNamespace).Select(scope => scope.Name);
        var types = scopes.Where(scope => !scope.IsNamespace).Select(scope => scope.Name);
        var prefix = string.Join(".", namespaces);
        var nested = string.Join("+", types);
        return prefix.Length == 0 ? nested : $"{prefix}.{nested}";
    }

    private static string ReadDottedName(IReadOnlyList<Token> tokens, int index, out int next)
    {
        var parts = new List<string>();
        while (index < tokens.Count && tokens[index].Kind == TokenKind.Word)
        {
            parts.Add(tokens[index].Value);
            index++;
            if (index < tokens.Count && tokens[index] is { Kind: TokenKind.Punctuation, Value: "." })
            {
                index++;
                continue;
            }

            break;
        }

        next = index;
        return string.Join(".", parts);
    }

    /// <summary>
    /// Pulls every claim out of one attribute group — which may hold several attributes, as in
    /// `[Fact, Trait("Feature", "X")]`, and may repeat the claim. Two forms are recognised:
    /// `Trait("Feature", "<ID>")` and the GUI suite's `GuiWorkflow("<ID>")`.
    /// </summary>
    private static IEnumerable<string> ReadFeatureIds(IReadOnlyList<Token> tokens, int start, int end)
    {
        for (var index = start; index < end; index++)
        {
            if (tokens[index].Kind != TokenKind.Word)
            {
                continue;
            }

            var idIndex = tokens[index].Value switch
            {
                "Trait" or "TraitAttribute" => TraitArgument(tokens, index, end),
                WorkflowAttributeName or WorkflowAttributeName + "Attribute" =>
                    WorkflowArgument(tokens, index, end),
                _ => -1,
            };

            if (idIndex < 0)
            {
                continue;
            }

            var id = tokens[idIndex].Value.Trim();
            if (id.Length > 0)
            {
                yield return id;
            }

            index = idIndex;
        }
    }

    /// <summary>`Trait("Feature", "<ID>")`: the id is the second argument, after the trait name.</summary>
    private static int TraitArgument(IReadOnlyList<Token> tokens, int index, int end) =>
        index + 4 < end &&
        tokens[index + 1] is { Kind: TokenKind.Punctuation, Value: "(" } &&
        tokens[index + 2] is { Kind: TokenKind.Text, Value: TraitName } &&
        tokens[index + 3] is { Kind: TokenKind.Punctuation, Value: "," } &&
        tokens[index + 4].Kind == TokenKind.Text
            ? index + 4
            : -1;

    /// <summary>`GuiWorkflow("<ID>")`: the id is the first argument.</summary>
    private static int WorkflowArgument(IReadOnlyList<Token> tokens, int index, int end) =>
        index + 2 < end &&
        tokens[index + 1] is { Kind: TokenKind.Punctuation, Value: "(" } &&
        tokens[index + 2].Kind == TokenKind.Text
            ? index + 2
            : -1;

    private static int MatchBracket(IReadOnlyList<Token> tokens, int open) =>
        Match(tokens, open, "[", "]");

    private static int MatchAngle(IReadOnlyList<Token> tokens, int open) =>
        Match(tokens, open, "<", ">");

    private static int Match(IReadOnlyList<Token> tokens, int open, string opener, string closer)
    {
        var depth = 0;
        for (var index = open; index < tokens.Count; index++)
        {
            if (tokens[index].Kind != TokenKind.Punctuation)
            {
                continue;
            }

            if (tokens[index].Value == opener)
            {
                depth++;
            }
            else if (tokens[index].Value == closer)
            {
                depth--;
                if (depth == 0)
                {
                    return index;
                }
            }
        }

        return tokens.Count - 1;
    }

    private sealed record Scope(
        string Name,
        bool IsNamespace,
        IReadOnlyList<string> FeatureIds,
        int EntryDepth);
}
