using System.Text;
using System.Text.Json;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>One golden case's outcome: its name (pack/table/row/index), pass or fail, and why.</summary>
public sealed record GoldenCaseResult(string Name, bool Passed, string Detail)
{
    /// <inheritdoc/>
    public override string ToString() => $"{(Passed ? "PASS" : "FAIL")} {Name}: {Detail}";
}

/// <summary>A golden file's outcome: file-level problems (malformed file, coverage, orphans) and every case.</summary>
public sealed record GoldenFileResult(string File, ValueList<string> Problems, ValueList<GoldenCaseResult> Cases)
{
    /// <summary>Whether the file is well formed, covers every row, and every case passed.</summary>
    public bool Passed => Problems.Count == 0 && Cases.All(c => c.Passed);

    /// <summary>Everything that failed, one per line.</summary>
    public override string ToString()
        => string.Join(Environment.NewLine, Problems.Select(p => $"{File}: {p}").Concat(Cases.Where(c => !c.Passed).Select(c => c.ToString())));
}

/// <summary>
/// Runs golden files (design §8): each case builds a request from its inputs, runs the real
/// evaluator over the real composed pack, and asserts the result <em>and</em> the row its citation
/// names. A golden file is authored from the source, never exported from the pack; the runner
/// also fails a file that leaves a composed row without a hand-authored case, or names a row the
/// table does not have (a row renamed without the golden file being re-read).
/// </summary>
public static class GoldenRunner
{
    private static readonly IReadOnlyDictionary<string, OutOfScopeReason> Reasons =
        Enum.GetValues<OutOfScopeReason>().ToDictionary(r => r.ToString(), r => r, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, NoDataReason> NoDataReasons =
        Enum.GetValues<NoDataReason>().ToDictionary(r => r.ToString(), r => r, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, CitationLayer> Layers = new Dictionary<string, CitationLayer>(StringComparer.Ordinal)
    {
        ["model-code"] = CitationLayer.ModelCode,
        ["state-amendment"] = CitationLayer.StateAmendment,
        ["municipal-amendment"] = CitationLayer.MunicipalAmendment,
    };

    /// <summary>Runs a golden file on disk against the pack it names, loaded from a packs root on disk.</summary>
    public static GoldenFileResult Run(string packsRoot, string goldenFile)
    {
        string json = File.ReadAllText(goldenFile, Encoding.UTF8);
        string? packId = PeekPack(json);
        PackLoadResult? pack = packId is null ? null : PackLoader.Load(packsRoot, packId);
        return Run(pack, json, Path.GetFileName(goldenFile));
    }

    /// <summary>Reads the pack id a golden file names, or null when it cannot be read.</summary>
    public static string? PeekPack(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json, JsonFile.Options);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                   && doc.RootElement.TryGetProperty("pack", out JsonElement p)
                   && p.ValueKind == JsonValueKind.String
                ? p.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Runs golden JSON against an already-loaded pack (or a load failure, which fails the file).</summary>
    public static GoldenFileResult Run(PackLoadResult? pack, string json, string fileName)
    {
        ArgumentNullException.ThrowIfNull(json);
        ProblemList problems = new();
        Where where = new(fileName);
        List<GoldenCaseResult> results = [];
        List<string> fileProblems = [];

        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(json, JsonFile.Options);
        }
        catch (JsonException ex)
        {
            return new GoldenFileResult(fileName, ValueList.Of($"not valid JSON: {ex.Message}"), ValueList<GoldenCaseResult>.Empty);
        }

        using (document)
        {
            JsonObj? root = JsonObj.Create(document.RootElement, string.Empty, where, problems);
            if (root is null)
            {
                return Finish(fileName, problems, fileProblems, results);
            }

            root.MarkUsed("notes");
            string? packId = root.String("pack");
            string? tableName = root.String("table");
            root.String("source");
            JsonObj? transcriber = root.Obj("transcriber");
            if (transcriber is not null)
            {
                transcriber.String("who");
                transcriber.Date("on");
                transcriber.Done();
            }

            IReadOnlyList<JsonElement>? cases = root.Array("cases", minItems: 1);
            root.Done();
            if (problems.Count > 0 || packId is null || tableName is null || cases is null)
            {
                return Finish(fileName, problems, fileProblems, results);
            }

            if (pack is not PackLoadResult.Loaded { Pack: var loaded })
            {
                fileProblems.Add(pack is PackLoadResult.Invalid invalid ? invalid.ToString() : $"pack '{packId}' was not loaded.");
                return Finish(fileName, problems, fileProblems, results);
            }

            if (loaded.Manifest.Id != packId)
            {
                fileProblems.Add($"the file is for pack '{packId}' but was run against '{loaded.Manifest.Id}'.");
                return Finish(fileName, problems, fileProblems, results);
            }

            HeaderSizingTable? table = loaded.Tables.FirstOrDefault(t => t.Designation == tableName);
            HashSet<string> covered = new(StringComparer.Ordinal);
            IRulesEngine engine = RulesEngine.For(loaded);
            for (int i = 0; i < cases.Count; i++)
            {
                GoldenCaseResult? result = RunCase(engine, table, packId, tableName, cases[i], i, where, problems, covered, fileProblems);
                if (result is not null)
                {
                    results.Add(result);
                }
            }

            foreach (HeaderRow row in table?.Rows ?? ValueList<HeaderRow>.Empty)
            {
                if (!covered.Contains(row.Id))
                {
                    fileProblems.Add($"row '{row.Id}' of table {tableName} has no hand-authored golden case.");
                }
            }
        }

        return Finish(fileName, problems, fileProblems, results);
    }

    private static GoldenFileResult Finish(string fileName, ProblemList problems, List<string> fileProblems, List<GoldenCaseResult> results)
        => new(
            fileName,
            problems.Items.Select(p => (p.RowOrOperation is null ? string.Empty : $"({p.RowOrOperation}) ") + p.Message).Concat(fileProblems).ToValueList(),
            results.ToValueList());

    private static GoldenCaseResult? RunCase(
        IRulesEngine engine, HeaderSizingTable? table, string packId, string tableName, JsonElement element, int index,
        Where fileWhere, ProblemList problems, HashSet<string> covered, List<string> fileProblems)
    {
        string path = $"cases[{index}]";
        Where where = fileWhere with { Row = path };
        JsonObj? c = JsonObj.Create(element, path, where, problems);
        if (c is null)
        {
            return null;
        }

        int before = problems.Count;
        string? row = c.Has("row") ? c.String("row") : null;
        c.MarkUsed("row");
        string? generated = c.Has("generated") ? c.String("generated") : null;
        c.MarkUsed("generated");
        if (generated is null)
        {
            c.String("location");
        }
        else
        {
            c.MarkUsed("location");
        }

        WallKind? wallKind = c.Has("wallKind") ? c.Enum("wallKind", Vocabulary.WallKinds) : table?.WallKind;
        c.MarkUsed("wallKind");
        HeaderRequest? request = null;
        JsonObj? inputs = c.Obj("inputs");
        if (inputs is not null)
        {
            request = ReadRequest(inputs, wallKind, problems);
            inputs.Done();
        }

        JsonObj? expect = c.Obj("expect");
        Func<HeaderResult, string?>? check = expect is null ? null : ReadExpectation(expect, row, problems);
        expect?.Done();
        c.Done();

        string name = $"{packId}/{tableName}/{row ?? "-"}/{index}";
        if (row is not null && table is not null && !table.Rows.Any(r => r.Id == row))
        {
            fileProblems.Add($"{path}: row '{row}' is not in the composed table (renamed or deleted? re-read the source).");
        }

        if (row is not null && generated is null)
        {
            covered.Add(row);
        }

        if (problems.Count > before || request is null || check is null)
        {
            return null;
        }

        HeaderResult result = engine.SizeHeader(request);
        string? failure = check(result);
        return new GoldenCaseResult(name, failure is null, failure ?? $"got {result}");
    }

    private static HeaderRequest? ReadRequest(JsonObj inputs, WallKind? wallKind, ProblemList problems)
    {
        int before = problems.Count;
        Dictionary<string, CellValue> values = new(StringComparer.Ordinal);
        foreach (string name in inputs.Names.ToList())
        {
            if (!Vocabulary.HeaderInputs.TryGetValue(name, out ColumnType type))
            {
                continue; // reported as an unknown field by Done()
            }

            CellValue? value = TableReader.ReadCell(inputs.Get(name), type, inputs.Child(name), inputs.Where, problems);
            if (value is not null)
            {
                values[name] = value.Value;
            }
        }

        if (!values.ContainsKey(Vocabulary.HeaderSpan))
        {
            problems.Add(inputs.Where, $"{inputs.Child(Vocabulary.HeaderSpan)}: missing required field.");
        }

        if (wallKind is null)
        {
            problems.Add(inputs.Where, "wallKind: the table is not in the pack, so the case must name its wallKind.");
        }

        if (problems.Count > before)
        {
            return null;
        }

        Length? Len(string name) => values.TryGetValue(name, out CellValue v) ? new Length(v.Magnitude) : null;
        int? Whole(string name) => values.TryGetValue(name, out CellValue v) ? (int)v.Magnitude : null;
        string? Symbol(string name) => values.TryGetValue(name, out CellValue v) ? v.Symbol : null;

        SiteInputs site = new(Whole("groundSnowLoad"), Whole("ultimateWindSpeed"), Symbol("seismicDesignCategory"), Len("frostDepth"), Len("buildingWidth"), null);
        return new HeaderRequest(Symbol("supports") ?? string.Empty, wallKind!.Value, Len(Vocabulary.HeaderSpan)!.Value, site);
    }

    private static Func<HeaderResult, string?>? ReadExpectation(JsonObj expect, string? row, ProblemList problems)
    {
        string[] kinds = ["sized", "outOfScope", "inputMissing", "noData"];
        List<string> present = [.. kinds.Where(expect.Has)];
        if (present.Count != 1)
        {
            problems.Add(expect.Where, "expect: exactly one of sized, outOfScope, inputMissing, noData.");
            return null;
        }

        JsonObj? e = expect.Obj(present[0]);
        if (e is null)
        {
            return null;
        }

        int before = problems.Count;
        Func<HeaderResult, string?>? check = null;
        switch (present[0])
        {
            case "sized":
                JsonObj? h = e.Obj("header");
                int? plies = h?.Int("plies", min: 1);
                string? nominal = h?.String("nominal");
                h?.Done();
                int? jack = e.Int("jackStuds");
                int? king = e.Int("kingStuds");
                CitationLayer? layer = e.Has("layer") ? e.Enum("layer", Layers) : null;
                e.MarkUsed("layer");
                if (row is null)
                {
                    problems.Add(e.Where, "a sized case names the row it expects (\"row\").");
                }

                if (plies is not null && nominal is not null && jack is not null && king is not null && row is not null)
                {
                    MemberSpec member = new(plies.Value, nominal);
                    check = r => r is HeaderResult.Sized s
                        ? s.Header != member || s.JackStuds != jack || s.KingStuds != king
                            ? $"expected {member}, {jack} jack, {king} king; got {s}"
                            : s.Citation.RowId != row
                                ? $"right values but cites row '{s.Citation.RowId}', expected '{row}'"
                                : layer is not null && s.Citation.Layer != layer
                                    ? $"cites layer {s.Citation.Layer}, expected {layer}"
                                    : null
                        : $"expected sized {member} from row '{row}'; got {r}";
                }

                break;
            case "outOfScope":
                OutOfScopeReason? reason = e.Enum("reason", Reasons);
                string? limitRow = e.String("limitRow", nullable: true);
                if (reason is not null)
                {
                    check = r => r is HeaderResult.OutOfScope o
                        ? o.Reason != reason
                            ? $"expected {reason}; got {o.Reason}: {o.Explanation}"
                            : o.Limit.RowId != limitRow
                                ? $"right reason but cites limit row '{o.Limit.RowId}', expected '{limitRow}'"
                                : null
                        : $"expected out of scope ({reason}); got {r}";
                }

                break;
            case "inputMissing":
                IReadOnlyList<JsonElement>? names = e.Array("inputs", minItems: 1);
                List<string> expected = [.. (names ?? []).Select(n => JsonObj.ReadString(n, e.Child("inputs"), e.Where, problems) ?? string.Empty)];
                check = r => r is HeaderResult.InputMissing m
                    ? m.Inputs.SequenceEqual(expected) ? null : $"expected missing [{string.Join(", ", expected)}]; got {m.Inputs}"
                    : $"expected input missing; got {r}";
                break;
            default:
                NoDataReason? why = e.Enum("reason", NoDataReasons);
                check = r => r is HeaderResult.NoData d
                    ? d.Reason == why ? null : $"expected no data ({why}); got {d.Reason}"
                    : $"expected no data; got {r}";
                break;
        }

        e.Done();
        return problems.Count > before ? null : check;
    }
}
