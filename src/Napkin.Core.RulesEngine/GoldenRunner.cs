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

    private static readonly IReadOnlyDictionary<string, BracingNoDataReason> BracingNoDataReasons =
        Enum.GetValues<BracingNoDataReason>().ToDictionary(r => r.ToString(), r => r, StringComparer.Ordinal);

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
            bool bracing = root.Has("section");
            string? tableName = bracing ? root.String("section") : root.String("table");
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

            if (bracing)
            {
                RunBracing(loaded, packId, tableName, cases, where, problems, fileProblems, results);
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
            if (!Vocabulary.HeaderInputs.TryGetValue(name, out ColumnType type) && !Vocabulary.ConditionInputs.TryGetValue(name, out type))
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

        SiteInputs site = new(
            Whole("groundSnowLoad"), Whole("ultimateWindSpeed"), Symbol("seismicDesignCategory"), Len("frostDepth"), Len("buildingWidth"), Whole(Vocabulary.RoofLiveLoad), null);
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

    /// <summary>
    /// A wall-bracing golden file (<c>"section"</c> instead of <c>"table"</c>): each case is a wall
    /// line and a site, and expects passes/fails with the required and provided lengths and the
    /// factors applied, or out of scope, input missing or no data. Every base row and every factor
    /// needs a hand-authored case.
    /// </summary>
    private static void RunBracing(
        LoadedPack loaded, string packId, string section, IReadOnlyList<JsonElement> cases, Where fileWhere, ProblemList problems,
        List<string> fileProblems, List<GoldenCaseResult> results)
    {
        BracingProvisions? provisions = loaded.Bracing is { } b && b.Section == section ? b : null;
        HashSet<string> rows = new(StringComparer.Ordinal);
        HashSet<string> factors = new(StringComparer.Ordinal);
        IRulesEngine engine = RulesEngine.For(loaded);
        for (int index = 0; index < cases.Count; index++)
        {
            string path = $"cases[{index}]";
            Where where = fileWhere with { Row = path };
            JsonObj? c = JsonObj.Create(cases[index], path, where, problems);
            if (c is null)
            {
                continue;
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

            JsonObj? inputs = c.Obj("inputs");
            BracingRequest? request = inputs is null ? null : ReadBracingRequest(inputs, problems);
            inputs?.Done();
            JsonObj? expect = c.Obj("expect");
            (Func<BracingResult, string?>? check, List<string> expectedFactors) = expect is null ? (null, []) : ReadBracingExpectation(expect, row, problems);
            expect?.Done();
            c.Done();

            if (row is not null && provisions is not null && !provisions.Required.Any(r => r.Id == row))
            {
                fileProblems.Add($"{path}: row '{row}' is not in section {section}'s base rows (renamed or deleted? re-read the source).");
            }

            if (generated is null)
            {
                if (row is not null)
                {
                    rows.Add(row);
                }

                factors.UnionWith(expectedFactors);
            }

            if (problems.Count > before || request is null || check is null)
            {
                continue;
            }

            BracingResult result = engine.CheckBracing(request);
            string? failure = check(result);
            results.Add(new GoldenCaseResult($"{packId}/{section}/{row ?? "-"}/{index}", failure is null, failure ?? $"got {result}"));
        }

        foreach (BracingRequiredRow row in provisions?.Required ?? ValueList<BracingRequiredRow>.Empty)
        {
            if (!rows.Contains(row.Id))
            {
                fileProblems.Add($"row '{row.Id}' of section {section} has no hand-authored golden case.");
            }
        }

        foreach (BracingFactor factor in provisions?.Factors ?? ValueList<BracingFactor>.Empty)
        {
            if (!factors.Contains(factor.Id))
            {
                fileProblems.Add($"factor '{factor.Id}' of section {section} is applied by no hand-authored golden case.");
            }
        }
    }

    private static BracingRequest? ReadBracingRequest(JsonObj inputs, ProblemList problems)
    {
        int before = problems.Count;
        Dictionary<string, CellValue> values = new(StringComparer.Ordinal);
        foreach (string name in inputs.Names.ToList())
        {
            if (name == Vocabulary.WallHeight || !Vocabulary.BracingInputs.TryGetValue(name, out ColumnType type))
            {
                continue; // wallHeight, lineLength and segments are read below; anything else is an unknown field
            }

            CellValue? value = TableReader.ReadCell(inputs.Get(name), type, inputs.Child(name), inputs.Where, problems);
            if (value is not null)
            {
                values[name] = value.Value;
            }
        }

        Length? line = ReadLength(inputs, "lineLength", problems);
        Length? height = ReadLength(inputs, Vocabulary.WallHeight, problems);
        List<BracedSegment> segments = [];
        IReadOnlyList<JsonElement>? items = inputs.Array("segments");
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            JsonObj? s = JsonObj.Create(items[i], inputs.Child($"segments[{i}]"), inputs.Where, problems);
            if (s is null)
            {
                continue;
            }

            Length? length = ReadLength(s, "length", problems);
            string? method = s.String("method", nullable: true);
            s.Done();
            if (length is { } l && l > Length.Zero)
            {
                segments.Add(new BracedSegment($"segment {i + 1}", l, method));
            }
            else if (length is not null)
            {
                problems.Add(s.Where, $"{s.Child("length")}: a segment is longer than zero.");
            }
        }

        if (problems.Count > before || line is null || height is null)
        {
            return null;
        }

        if (line.Value <= Length.Zero || height.Value <= Length.Zero || segments.Aggregate(Length.Zero, (a, x) => a + x.Length) > line.Value)
        {
            problems.Add(inputs.Where, $"{inputs.Child("lineLength")}: the line and wall height are positive and the segments fit in the line.");
            return null;
        }

        Length? Len(string name) => values.TryGetValue(name, out CellValue v) ? new Length(v.Magnitude) : null;
        int? Whole(string name) => values.TryGetValue(name, out CellValue v) ? (int)v.Magnitude : null;
        string? Symbol(string name) => values.TryGetValue(name, out CellValue v) ? v.Symbol : null;
        SiteInputs site = new(Whole("groundSnowLoad"), Whole("ultimateWindSpeed"), Symbol("seismicDesignCategory"), null, Len("buildingWidth"), null, null);
        return new BracingRequest(new BracedWallLine(line.Value, height.Value, segments.ToValueList()), site);
    }

    private static Length? ReadLength(JsonObj o, string name, ProblemList problems)
    {
        JsonElement? e = o.Get(name);
        return e is null ? null : JsonObj.ReadLength(e.Value, o.Child(name), o.Where, problems);
    }

    private static (Func<BracingResult, string?>? Check, List<string> Factors) ReadBracingExpectation(JsonObj expect, string? row, ProblemList problems)
    {
        string[] kinds = ["passes", "fails", "outOfScope", "inputMissing", "noData"];
        List<string> present = [.. kinds.Where(expect.Has)];
        if (present.Count != 1)
        {
            problems.Add(expect.Where, "expect: exactly one of passes, fails, outOfScope, inputMissing, noData.");
            return (null, []);
        }

        JsonObj? e = expect.Obj(present[0]);
        if (e is null)
        {
            return (null, []);
        }

        int before = problems.Count;
        Func<BracingResult, string?>? check = null;
        List<string> factors = [];
        switch (present[0])
        {
            case "passes":
            case "fails":
                bool fails = present[0] == "fails";
                Length? required = ReadLength(e, "required", problems);
                Length? provided = ReadLength(e, "provided", problems);
                Length? shortfall = fails ? ReadLength(e, "shortfall", problems) : null;
                IReadOnlyList<JsonElement>? ids = e.Array("factors");
                factors = [.. (ids ?? []).Select(n => JsonObj.ReadString(n, e.Child("factors"), e.Where, problems) ?? string.Empty)];
                if (row is null)
                {
                    problems.Add(e.Where, "a passes or fails case names the base row it expects (\"row\").");
                }

                List<string> expectedFactors = factors;
                if (required is not null && provided is not null && (!fails || shortfall is not null) && row is not null)
                {
                    check = r => (r, fails) switch
                    {
                        (BracingResult.Passes p, false) => Compare(p.Required, p.Provided, null, p.Citation, p.Working),
                        (BracingResult.Fails f, true) => Compare(f.Required, f.Provided, f.Shortfall, f.Citation, f.Working),
                        _ => $"expected {(fails ? "fails" : "passes")}; got {r}",
                    };
                }

                string? Compare(Length req, Length prov, Length? shortBy, Citation citation, BracingWorking working)
                    => req != required || prov != provided || shortBy != shortfall
                        ? $"expected required {CellValue.Of(required!.Value)}, provided {CellValue.Of(provided!.Value)}{(shortfall is { } sf ? $", short {CellValue.Of(sf)}" : string.Empty)}; got required {CellValue.Of(req)}, provided {CellValue.Of(prov)}"
                        : citation.RowId != row
                            ? $"right lengths but cites row '{citation.RowId}', expected '{row}'"
                            : !working.Factors.Select(f => f.Id).SequenceEqual(expectedFactors)
                                ? $"applied factors [{string.Join(", ", working.Factors.Select(f => f.Id))}], expected [{string.Join(", ", expectedFactors)}]"
                                : null;
                break;
            case "outOfScope":
                OutOfScopeReason? reason = e.Enum("reason", Reasons);
                string? limitRow = e.String("limitRow", nullable: true);
                if (reason is not null)
                {
                    check = r => r is BracingResult.OutOfScope o
                        ? o.Reason != reason
                            ? $"expected {reason}; got {o.Reason}: {o.Explanation}"
                            : o.Limit.RowId != limitRow ? $"right reason but cites '{o.Limit.RowId}', expected '{limitRow}'" : null
                        : $"expected out of scope ({reason}); got {r}";
                }

                break;
            case "inputMissing":
                IReadOnlyList<JsonElement>? names = e.Array("inputs", minItems: 1);
                List<string> expected = [.. (names ?? []).Select(n => JsonObj.ReadString(n, e.Child("inputs"), e.Where, problems) ?? string.Empty)];
                check = r => r is BracingResult.InputMissing m
                    ? m.Inputs.SequenceEqual(expected) ? null : $"expected missing [{string.Join(", ", expected)}]; got {m.Inputs}"
                    : $"expected input missing; got {r}";
                break;
            default:
                BracingNoDataReason? why = e.Enum("reason", BracingNoDataReasons);
                check = r => r is BracingResult.NoData d
                    ? d.Reason == why ? null : $"expected no data ({why}); got {d.Reason}"
                    : $"expected no data; got {r}";
                break;
        }

        e.Done();
        return (problems.Count > before ? null : check, factors);
    }
}
