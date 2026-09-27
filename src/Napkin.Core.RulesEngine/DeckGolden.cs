using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Deck golden files (<c>"deck"</c> instead of <c>"table"</c>, deck-guide-pack §4 item 4): each case is a
/// deck lookup's inputs and the answer the source prints, run through the real evaluator over the real
/// composed pack. Every row, and every scope limit of the table and its guide, needs a hand-authored case;
/// the boundary pairs are generated from the rows' own bounds and committed, and a file whose committed
/// pairs are not the generator's fails (<see cref="GoldenRunner.DeckBoundaries"/> prints them).
/// </summary>
internal static class DeckGolden
{
    static readonly JsonSerializerOptions Canonical = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static readonly string[] Kinds = ["passes", "short", "sized", "outOfScope", "inputMissing", "noData"];

    /// <summary>
    /// What a case expects, read strictly: the check, the limit it covers when it expects one, and the allowed
    /// length a passes or short case names (for a cantilever case, whether the row's own overhang governs).
    /// </summary>
    sealed record Expectation(Func<DeckResult, string?> Check, string? Limit, Length? Allowed = null);

    public static void Run(
        LoadedPack pack, string packId, string name, IReadOnlyList<JsonElement> cases, Where fileWhere, ProblemList problems, List<string> fileProblems, List<GoldenCaseResult> results)
    {
        if (pack.Deck.Tables.FirstOrDefault(table => table.Designation == name) is not { } table)
        {
            fileProblems.Add($"deck table '{name}' is not in pack '{packId}' (renamed or removed? re-read the source).");
            return;
        }

        List<ScopeLimit> limits = [.. table.Guide?.Limits ?? ValueList<ScopeLimit>.Empty, .. table.Limits];
        HashSet<string> rows = new(StringComparer.Ordinal), covered = new(StringComparer.Ordinal), overhangs = new(StringComparer.Ordinal);
        bool factorCovered = false;
        List<string> committed = [];
        for (int index = 0; index < cases.Count; index++)
        {
            string path = $"cases[{index}]";
            JsonObj? c = JsonObj.Create(cases[index], path, fileWhere with { Row = path }, problems);
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
                committed.Add(Canonicalize(cases[index]));
            }

            JsonObj? inputs = c.Obj("inputs");
            bool cantilever = inputs?.Has("cantilever") == true;
            bool factored = false;
            Func<DeckResult>? request = inputs is null ? null : ReadRequest(inputs, pack, table, problems, out factored);
            inputs?.Done();
            JsonObj? expect = c.Obj("expect");
            Expectation? expectation = expect is null ? null : ReadExpectation(expect, row, problems);
            expect?.Done();
            c.Done();

            if (row is not null && table.Rows.All(r => r.Id != row))
            {
                fileProblems.Add($"{path}: row '{row}' is not in table {name} (renamed or deleted? re-read the source).");
            }

            if (expectation?.Limit is { } limit && limits.All(l => l.Id != limit))
            {
                fileProblems.Add($"{path}: '{limit}' is not a scope limit of table {name} or its guide.");
            }

            if (generated is null)
            {
                // A span case covers its row's span; a cantilever case covers the row's overhang only when it
                // expects exactly that overhang, so the transcribed value is what governs (deck-guide-pack §3.1).
                if (row is not null && !cantilever)
                {
                    rows.Add(row);
                }
                else if (row is not null && table.Rows.FirstOrDefault(r => r.Id == row) is { Overhang: { } own } && expectation?.Allowed == own)
                {
                    overhangs.Add(row);
                }

                if (expectation?.Limit is { } id)
                {
                    covered.Add(id);
                }

                // A case that asks a centre post under a continuous beam exercises the table's centre-post factor.
                factorCovered |= factored && table.CenterPostFactor is not null && row is not null;
            }

            if (problems.Count > before || request is null || expectation is null)
            {
                continue;
            }

            DeckResult result = request();
            string? failure = expectation.Check(result);
            results.Add(new GoldenCaseResult($"{packId}/{name}/{row ?? "-"}/{index}", failure is null, failure ?? $"got {result}"));
        }

        fileProblems.AddRange(table.Rows.Where(row => !rows.Contains(row.Id)).Select(row => $"row '{row.Id}' of table {name} has no hand-authored golden case."));
        fileProblems.AddRange(table.Rows.Where(row => row.Overhang is not null && !overhangs.Contains(row.Id)).Select(row =>
            $"row '{row.Id}' of table {name} has no hand-authored cantilever case expecting its own overhang ({CellValue.Of(row.Overhang!.Value)}); ask with a span long enough that it governs."));
        fileProblems.AddRange(limits.Where(limit => !covered.Contains(limit.Id)).Select(limit => $"scope limit '{limit.Id}' of table {name} is expected by no hand-authored golden case."));
        if (table.CenterPostFactor is { } factor && !factorCovered)
        {
            fileProblems.Add($"the centre-post factor of table {name} (note {factor.Note}) is exercised by no hand-authored golden case naming its row: ask a centre post under a continuous beam.");
        }

        if (problems.Count > 0)
        {
            return;
        }

        List<string> expected = [.. Boundaries(table, cases)];
        if (!expected.SequenceEqual(committed))
        {
            fileProblems.Add(
                $"the committed boundary cases ({committed.Count}) are not the generator's ({expected.Count}); replace every \"generated\" case with these, in this order:"
                + Environment.NewLine + string.Join("," + Environment.NewLine, expected));
        }
    }

    /// <summary>The boundary pairs for a deck golden file's table, one compact JSON case each, in order.</summary>
    public static ValueList<string> Boundaries(LoadedPack pack, string json)
    {
        ArgumentNullException.ThrowIfNull(pack);
        using JsonDocument document = JsonDocument.Parse(json, JsonFile.Options);
        JsonElement root = document.RootElement;
        string name = root.GetProperty("deck").GetString()!;
        DeckTable table = pack.Deck.Tables.First(candidate => candidate.Designation == name);
        return Boundaries(table, [.. root.GetProperty("cases").EnumerateArray()]).ToValueList();
    }

    /// <summary>
    /// The generator (rules-engine-model §8.1): for each row, from its first hand-authored answered case —
    /// a span exactly at the row's allowed span and 1/1024″ over it; each banded input exactly at the row's
    /// bound and one step past it (the next row, or out of scope naming the column); then, from the file's
    /// first answered case, each <c>above</c> or <c>aboveInput</c> limit at its bound and one step over.
    /// </summary>
    static IEnumerable<string> Boundaries(DeckTable table, IReadOnlyList<JsonElement> cases)
    {
        // Span (or ledger, footing) cases and cantilever cases, each hand-authored and answered.
        List<(string Row, JsonObject Inputs, JsonObject Expect)> answered = [], cantilevers = [];
        foreach (JsonElement element in cases)
        {
            if (element.ValueKind == JsonValueKind.Object
                && !element.TryGetProperty("generated", out _)
                && element.TryGetProperty("row", out JsonElement row) && row.ValueKind == JsonValueKind.String
                && element.TryGetProperty("inputs", out JsonElement inputs) && inputs.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("expect", out JsonElement expect) && expect.ValueKind == JsonValueKind.Object
                && (expect.TryGetProperty("passes", out _) || expect.TryGetProperty("short", out _) || expect.TryGetProperty("sized", out _)))
            {
                (inputs.TryGetProperty("cantilever", out _) ? cantilevers : answered)
                    .Add((row.GetString()!, (JsonObject)JsonNode.Parse(inputs.GetRawText())!, (JsonObject)JsonNode.Parse(expect.GetRawText())!));
            }
        }

        foreach (DeckRow row in table.Rows)
        {
            foreach (string pair in RowPairs(table, row, answered.FirstOrDefault(c => c.Row == row.Id).Inputs))
            {
                yield return pair;
            }

            // The cantilever exactly at what the row allows on the case's span, and 1/1024″ over it.
            if (table.OverhangLimit is not null && row.Overhang is not null
                && cantilevers.FirstOrDefault(c => c.Row == row.Id) is { Inputs: not null } asked
                && asked.Inputs["span"] is { } spanNode)
            {
                Length allowed = DeckEvaluator.AllowedOverhang(table, row, new Length(ReadLength(spanNode).Magnitude));
                yield return Case(row.Id, With(asked.Inputs, "cantilever", Text(allowed)), new JsonObject { ["passes"] = new JsonObject { ["allowed"] = Text(allowed) } });
                yield return Case(row.Id, With(asked.Inputs, "cantilever", Text(allowed + new Length(1))), new JsonObject { ["short"] = new JsonObject { ["allowed"] = Text(allowed), ["over"] = Text(new Length(1)) } });
            }
        }

        if (answered.Count == 0)
        {
            yield break;
        }

        (string first, JsonObject inputs0, JsonObject expect0) = answered[0];
        foreach (ScopeLimit limit in (table.Guide?.Limits ?? ValueList<ScopeLimit>.Empty).Concat(table.Limits))
        {
            ScopeCondition when = limit.When;
            JsonNode? at = when.Form switch
            {
                ScopeForm.Above => Value(when.Value!.Value),
                ScopeForm.AboveInput => inputs0[when.OtherInput!]?.DeepClone(),
                _ => null,
            };
            if (at is null)
            {
                continue;
            }

            CellValue bound = when.Form == ScopeForm.Above ? when.Value!.Value : ReadLength(at);
            if (table.Inputs.All(column => column.Name != when.Input))
            {
                yield return Case(first, With(inputs0, when.Input, at), (JsonObject)expect0.DeepClone());
            }

            yield return Case(first, With(inputs0, when.Input, Value(bound with { Magnitude = bound.Magnitude + 1 })), new JsonObject { ["outOfScope"] = new JsonObject { ["limit"] = limit.Id } });
        }
    }

    /// <summary>
    /// One row's pairs from its first hand-authored answered case (none without one): a span table's span at
    /// the row's allowed span and 1/1024″ over, then each banded input at the row's bound and one step past.
    /// </summary>
    static IEnumerable<string> RowPairs(DeckTable table, DeckRow row, JsonObject? hand)
    {
        if (hand is null)
        {
            yield break;
        }

        // A span or post-height row: the span (height) exactly at the row's and 1/1024″ over it.
        if (Measured(table) is { } measure && Allowed(table, row) is { } allowed)
        {
            yield return Case(row.Id, With(hand, measure, Text(allowed)), Answer(table, row, hand));
            yield return Case(row.Id, With(hand, measure, Text(allowed + new Length(1))), new JsonObject { ["short"] = new JsonObject { ["allowed"] = Text(allowed), ["over"] = Text(new Length(1)) } });
        }

        // A centre post asked under a continuous beam is read at its area × the factor, so its bound is asked ÷ the factor.
        ExactFraction? factor = Factor(table, hand);
        foreach (InputColumn column in table.Inputs.Where(column => column.Band is BandKind.UpperBound or BandKind.LowerBound))
        {
            long bound = row.Inputs[column.Name].Magnitude;
            List<DeckRow> group = [.. table.Rows.Where(other => table.Inputs.Where(c => c.Name != column.Name).All(c => other.Inputs[c.Name] == row.Inputs[c.Name]))];
            bool up = column.Band == BandKind.UpperBound;
            (JsonNode At, JsonNode Past)? values = up && column.Type == ColumnType.SquareFeet
                ? AreaPair(bound, factor)
                : (Value(new CellValue(column.Type, null, bound)), Value(new CellValue(column.Type, null, up ? bound + 1 : bound - 1)));
            if (values is not { } pair)
            {
                // The bound ÷ the factor is not a whole number of 1/1024″ × 1 ft: no pair is generated for it.
                continue;
            }

            yield return Case(row.Id, Set(table, row, With(hand, column.Name, pair.At)), Answer(table, row, hand));

            DeckRow? next = up
                ? group.Where(other => other.Inputs[column.Name].Magnitude > bound).MinBy(other => other.Inputs[column.Name].Magnitude)
                : group.Where(other => other.Inputs[column.Name].Magnitude < bound).MaxBy(other => other.Inputs[column.Name].Magnitude);
            JsonObject past = Set(table, row, With(hand, column.Name, pair.Past));
            if (next is null)
            {
                yield return Case(row.Id, past, new JsonObject { ["outOfScope"] = new JsonObject { ["column"] = column.Name } });
                continue;
            }

            // The next row answers: a span or post table is asked about exactly that row's allowed span or height.
            yield return Case(next.Id, Set(table, next, past), Answer(table, next, past));
        }
    }

    /// <summary>The input a span or post table's case measures against its row: "span", "height", or none.</summary>
    static string? Measured(DeckTable table) => table.Kind switch
    {
        DeckReader.MemberSpanKind => "span",
        DeckReader.PostKind => "height",
        _ => null,
    };

    /// <summary>A span row's span or a post row's height; null for another row or one printed NP.</summary>
    static Length? Allowed(DeckTable table, DeckRow row) => table.Kind == DeckReader.MemberSpanKind ? row.Span : row.Height;

    /// <summary>The case's inputs with the span (height) set to the row's own, so it passes; unchanged for another kind or an NP row.</summary>
    static JsonObject Set(DeckTable table, DeckRow row, JsonObject inputs)
        => Measured(table) is { } measure && Allowed(table, row) is { } allowed ? With(inputs, measure, Text(allowed)) : inputs;

    /// <summary>
    /// The factor's reciprocal a hand case's area bound is divided by: the table's centre-post factor when the case asks a
    /// centre post under a continuous beam (a centre post table, or a footing case at position "center"); otherwise null.
    /// </summary>
    static ExactFraction? Factor(DeckTable table, JsonObject hand)
    {
        bool continuous = hand["continuousBeam"] is JsonValue beam && beam.TryGetValue(out bool yes) && yes;
        bool center = table.Position == PostPosition.Center || (hand["position"] is JsonValue at && at.TryGetValue(out string? name) && name == "center");
        return table.CenterPostFactor is { } factor && continuous && center ? factor.Multiply : null;
    }

    /// <summary>
    /// An area exactly at an upper bound (in whole square feet, or 1 ft × a length when not whole) and one step past it
    /// (1 ft × that length + 1/1024″), each divided by the factor when one applies; null when the bound ÷ the factor is
    /// not a whole number of 1/1024″ feet.
    /// </summary>
    static (JsonNode At, JsonNode Past)? AreaPair(long bound, ExactFraction? factor)
    {
        Int128 n = factor?.Numerator ?? 1, d = factor?.Denominator ?? 1;
        Int128 units = bound * d * Length.UnitsPerFoot;
        if (units % n != 0)
        {
            return null;
        }

        Length side = new((long)(units / n));
        JsonNode at = (bound * d) % n == 0 ? JsonValue.Create((long)(bound * d / n)) : new JsonArray(Text(Length.Feet(1)), Text(side));
        return (at, new JsonArray(Text(Length.Feet(1)), Text(side + new Length(1))));
    }

    /// <summary>
    /// The row's answer to a case with these inputs: its allowed span or height, "NP" for a post row that prints it, or
    /// its footing's sizes, or its words, spacing and napkin's count.
    /// </summary>
    static JsonObject Answer(DeckTable table, DeckRow row, JsonObject inputs)
    {
        if (table.Kind == DeckReader.PostKind && row.NotPermitted)
        {
            return new JsonObject { ["outOfScope"] = new JsonObject { ["notPermitted"] = true } };
        }

        if (Allowed(table, row) is { } allowed && Measured(table) is not null)
        {
            return new JsonObject { ["passes"] = new JsonObject { ["allowed"] = Text(allowed) } };
        }

        if (row.Footing is { } footing)
        {
            return new JsonObject { ["sized"] = new JsonObject { ["round"] = Text(footing.Round), ["square"] = Text(footing.Square), ["thickness"] = Text(footing.Thickness) } };
        }

        Length ledger = new(ReadLength(inputs["ledgerLength"]!).Magnitude);
        int count = (int)((ledger.Units + row.Spacing.Units - 1) / row.Spacing.Units) + 1;
        return new JsonObject { ["sized"] = new JsonObject { ["text"] = row.Text, ["spacing"] = Text(row.Spacing), ["count"] = count } };
    }

    static JsonNode Value(CellValue value) => value.Type switch
    {
        ColumnType.Length => Text(new Length(value.Magnitude)),
        _ => JsonValue.Create(value.Magnitude),
    };

    /// <summary>A length the runner has already read strictly from this file.</summary>
    static CellValue ReadLength(JsonNode node)
    {
        Length.TryParse(node.GetValue<string>(), out Length length, out _);
        return CellValue.Of(length);
    }

    /// <summary>A length as pack files write it: "11ft 1in", "22ft 0-1/1024in", "8-1/4in", "1/1024in".</summary>
    internal static string Text(Length length)
    {
        long feet = length.Units / Length.UnitsPerFoot;
        long rest = length.Units % Length.UnitsPerFoot;
        long inches = rest / Length.UnitsPerInch;
        long fraction = rest % Length.UnitsPerInch;
        long denominator = Length.UnitsPerInch;
        while (fraction != 0 && fraction % 2 == 0)
        {
            fraction /= 2;
            denominator /= 2;
        }

        string inch = fraction == 0 ? $"{inches}" : feet == 0 && inches == 0 ? $"{fraction}/{denominator}" : $"{inches}-{fraction}/{denominator}";
        return feet == 0 ? $"{inch}in" : $"{feet}ft {inch}in";
    }

    static JsonObject With(JsonObject inputs, string name, JsonNode? value)
    {
        JsonObject copy = (JsonObject)inputs.DeepClone();
        copy[name] = value;
        return copy;
    }

    static string Case(string row, JsonObject inputs, JsonObject expect)
        => new JsonObject { ["row"] = row, ["generated"] = "boundary", ["inputs"] = inputs, ["expect"] = expect }.ToJsonString(Canonical);

    static string Canonicalize(JsonElement element) => JsonNode.Parse(element.GetRawText())!.ToJsonString(Canonical);

    /// <summary>
    /// A case's lookup, read strictly: the inputs this table's kind takes, and a guide's scope inputs. <paramref name="factored"/>
    /// says whether it asks a centre post under a continuous beam of a table that declares a centre-post factor.
    /// </summary>
    static Func<DeckResult>? ReadRequest(JsonObj inputs, LoadedPack pack, DeckTable table, ProblemList problems, out bool factored)
    {
        factored = false;
        int before = problems.Count;
        string? Text(string name) => inputs.Has(name) ? inputs.String(name) : Unused(name);
        int? Whole(string name) => inputs.Has(name) ? inputs.Int(name) : UnusedInt(name);
        Length? Len(string name, bool required = false)
        {
            if (!inputs.Has(name) && !required)
            {
                inputs.MarkUsed(name);
                return null;
            }

            return inputs.Get(name) is { } e ? JsonObj.ReadLength(e, inputs.Child(name), inputs.Where, problems) : null;
        }

        string? Unused(string name)
        {
            inputs.MarkUsed(name);
            return null;
        }

        int? UnusedInt(string name)
        {
            inputs.MarkUsed(name);
            return null;
        }

        string? supports = Text("supports"), species = Text("species");
        int? snow = Whole("groundSnowLoad");
        Length? deckLength = Len("deckLength"), deckWidth = Len("deckWidth");
        DeckScopeInputs scope = new(supports, species, snow, deckLength, deckWidth);
        switch (table.Kind)
        {
            case DeckReader.MemberSpanKind:
            {
                string? member = inputs.String("member");
                Length? span = Len("span", required: true), spacing = Len("spacing"), joistSpan = Len("joistSpan");
                int? live = Whole("roofLiveLoad");

                // A cantilever asks the joists' overhang check instead, the span being the actual joist span.
                Length? cantilever = Len("cantilever");
                if (cantilever is not null && table.Use != SpanUse.DeckJoist)
                {
                    problems.Add(inputs.Where, $"{inputs.Child("cantilever")}: only a deck-joist table's case asks about a cantilever.");
                }

                if (problems.Count > before || member is null || span is null)
                {
                    return null;
                }

                SpanRequest request = new(member, span.Value, supports, species, spacing, joistSpan, snow, live, deckLength, deckWidth);
                return cantilever is { } past
                    ? () => DeckEvaluator.CheckCantilever(pack, request, past)
                    : () => DeckEvaluator.CheckSpan(pack, table.Use!.Value, request);
            }

            case DeckReader.LedgerKind:
            {
                string? member = inputs.String("member");
                Length? joistSpan = Len("joistSpan", required: true), ledger = Len("ledgerLength", required: true);
                return problems.Count > before || member is null || joistSpan is null || ledger is null
                    ? null
                    : () => DeckEvaluator.SizeLedger(pack, member, joistSpan.Value, ledger.Value, scope);
            }

            case DeckReader.PostKind:
            {
                string? post = inputs.String("post");
                Length? height = Len("height", required: true);
                ExactFraction? area = ReadArea(inputs, problems);
                bool continuous = ReadBool(inputs, "continuousBeam", problems);
                PostPosition position = table.Position!.Value;
                factored = continuous && position == PostPosition.Center && table.CenterPostFactor is not null;
                return problems.Count > before || post is null || height is null || area is null
                    ? null
                    : () => DeckEvaluator.CheckPost(pack, new PostRequest(post, height.Value, species, new PostArea(area.Value, position, continuous)), scope);
            }

            default:
            {
                ExactFraction? area = ReadArea(inputs, problems);
                int? soil = Whole("soilBearing");
                PostPosition? position = inputs.Get("position") is { } p ? JsonObj.ReadEnum(p, inputs.Child("position"), inputs.Where, problems, Positions) : null;
                bool continuous = ReadBool(inputs, "continuousBeam", problems);
                factored = continuous && position == PostPosition.Center && table.CenterPostFactor is not null;
                return problems.Count > before || area is null || position is null
                    ? null
                    : () => DeckEvaluator.SizeFooting(pack, new PostArea(area.Value, position.Value, continuous), soil, scope);
            }
        }
    }

    static readonly IReadOnlyDictionary<string, PostPosition> Positions = new Dictionary<string, PostPosition>(StringComparer.Ordinal)
    {
        ["corner"] = PostPosition.Corner,
        ["center"] = PostPosition.Center,
    };

    /// <summary>An optional true/false input, false when absent.</summary>
    static bool ReadBool(JsonObj inputs, string name, ProblemList problems)
    {
        if (!inputs.Has(name))
        {
            inputs.MarkUsed(name);
            return false;
        }

        JsonElement e = inputs.Get(name)!.Value;
        if (e.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            problems.Add(inputs.Where, $"{inputs.Child(name)}: true or false.");
            return false;
        }

        return e.ValueKind == JsonValueKind.True;
    }

    /// <summary>A tributary area: whole square feet, or two lengths multiplied, in square 1/1024″.</summary>
    static ExactFraction? ReadArea(JsonObj inputs, ProblemList problems)
    {
        if (inputs.Get("tributaryArea") is not { } e)
        {
            return null;
        }

        string path = inputs.Child("tributaryArea");
        if (e.ValueKind == JsonValueKind.Number)
        {
            return JsonObj.ReadInt(e, path, inputs.Where, problems) is { } feet
                ? new ExactFraction((Int128)feet * Length.UnitsPerFoot * Length.UnitsPerFoot, 1)
                : null;
        }

        List<JsonElement> sides = e.ValueKind == JsonValueKind.Array ? [.. e.EnumerateArray()] : [];
        if (sides.Count != 2)
        {
            problems.Add(inputs.Where, $"{path}: whole square feet, or two lengths whose product is the area.");
            return null;
        }

        Length? a = JsonObj.ReadLength(sides[0], $"{path}[0]", inputs.Where, problems), b = JsonObj.ReadLength(sides[1], $"{path}[1]", inputs.Where, problems);
        return a is null || b is null ? null : new ExactFraction((Int128)a.Value.Units * b.Value.Units, 1);
    }

    static Expectation? ReadExpectation(JsonObj expect, string? row, ProblemList problems)
    {
        List<string> present = [.. Kinds.Where(expect.Has)];
        if (present.Count != 1)
        {
            problems.Add(expect.Where, $"expect: exactly one of {string.Join(", ", Kinds)}.");
            return null;
        }

        JsonObj? e = expect.Obj(present[0]);
        if (e is null)
        {
            return null;
        }

        int before = problems.Count;
        Length? Len(string name) => e.Get(name) is { } element ? JsonObj.ReadLength(element, e.Child(name), e.Where, problems) : null;
        if (present[0] is "passes" or "short" or "sized" && row is null)
        {
            problems.Add(e.Where, $"a {present[0]} case names the row it expects (\"row\").");
        }

        Expectation? expectation = null;
        switch (present[0])
        {
            case "passes":
            {
                Length? allowed = Len("allowed");
                expectation = allowed is null ? null : new(r => r is DeckResult.Passes p
                    ? p.Row.Id != row ? $"passes, but from row '{p.Row.Id}', expected '{row}'"
                    : p.Allowed != allowed ? $"expected allowed {CellValue.Of(allowed.Value)}; got {CellValue.Of(p.Allowed)}" : null
                    : $"expected passes from row '{row}'; got {r}", null, allowed);
                break;
            }

            case "short":
            {
                Length? allowed = Len("allowed"), over = Len("over");
                expectation = allowed is null || over is null ? null : new(r => r is DeckResult.Short s
                    ? s.Row.Id != row ? $"short, but from row '{s.Row.Id}', expected '{row}'"
                    : s.Allowed != allowed || s.Over != over ? $"expected allowed {CellValue.Of(allowed.Value)}, over by {CellValue.Of(over.Value)}; got {CellValue.Of(s.Allowed)}, over by {CellValue.Of(s.Over)}" : null
                    : $"expected short from row '{row}'; got {r}", null, allowed);
                break;
            }

            case "sized" when e.Has("round") || e.Has("square") || e.Has("thickness"):
            {
                // A footing row's three outputs, all three expected.
                Length? round = Len("round"), square = Len("square"), thickness = Len("thickness");
                if (round is null || square is null || thickness is null)
                {
                    foreach (string name in new[] { "round", "square", "thickness" }.Where(name => !e.Has(name)))
                    {
                        problems.Add(e.Where, $"{e.Child(name)}: a footing case expects all three of round, square and thickness.");
                    }

                    break;
                }

                FootingSize size = new(round.Value, square.Value, thickness.Value);
                expectation = new(r => r is DeckResult.Sized s
                    ? s.Row.Id != row ? $"sized, but from row '{s.Row.Id}', expected '{row}'"
                    : s.Row.Footing != size ? $"expected {Said(size)}; got {(s.Row.Footing is { } got ? Said(got) : "no footing")}" : null
                    : $"expected sized from row '{row}'; got {r}", null);
                break;
            }

            case "sized":
            {
                string? text = e.String("text");
                Length? spacing = e.Has("spacing") ? Len("spacing") : null;
                e.MarkUsed("spacing");
                int? count = e.Has("count") ? e.Int("count", min: 1) : null;
                e.MarkUsed("count");
                expectation = text is null ? null : new(r => r is DeckResult.Sized s
                    ? s.Row.Id != row ? $"sized, but from row '{s.Row.Id}', expected '{row}'"
                    : s.Row.Text != text || (spacing is not null && s.Row.Spacing != spacing) || (count is not null && s.Count != count)
                        ? $"expected {text}{(spacing is { } g ? $", {CellValue.Of(g)}" : string.Empty)}{(count is { } n ? $", {n}" : string.Empty)}; got {s.Row.Text}, {CellValue.Of(s.Row.Spacing)}, {s.Count}"
                        : null
                    : $"expected sized from row '{row}'; got {r}", null);
                break;
            }

            case "outOfScope":
            {
                string? limit = e.Has("limit") ? e.String("limit") : null;
                string? column = e.Has("column") ? e.String("column") : null;
                bool printedNp = e.Get("notPermitted", required: false) is { ValueKind: JsonValueKind.True };
                e.MarkUsed("limit");
                e.MarkUsed("column");
                if (e.Has("notPermitted") && !printedNp)
                {
                    problems.Add(e.Where, "expect.outOfScope.notPermitted: true, when the row's cell prints NP.");
                    break;
                }

                if ((limit is null ? 0 : 1) + (column is null ? 0 : 1) + (printedNp ? 1 : 0) != 1)
                {
                    problems.Add(e.Where, "expect.outOfScope: exactly one of limit (a scope limit's id), column (the input no row covers) or notPermitted: true (the row prints NP).");
                    break;
                }

                if (printedNp)
                {
                    if (row is null)
                    {
                        problems.Add(e.Where, "an outOfScope notPermitted case names the row it expects (\"row\").");
                        break;
                    }

                    expectation = new(r => r is DeckResult.OutOfScope { Row: { NotPermitted: true } np }
                        ? np.Id != row ? $"NP, but from row '{np.Id}', expected '{row}'" : null
                        : $"expected out of scope, row '{row}' printing NP; got {r}", null);
                    break;
                }

                expectation = new(r => r is DeckResult.OutOfScope o
                    ? o.Limit?.Id != limit || o.Column != column ? $"out of scope for {o.Limit?.Id ?? o.Column ?? $"row {o.Row?.Id}"}, expected {limit ?? column}: {o.Explanation}" : null
                    : $"expected out of scope ({limit ?? column}); got {r}", limit);
                break;
            }

            case "inputMissing":
            {
                string? input = e.String("input");
                expectation = input is null ? null : new(r => r is DeckResult.InputMissing m
                    ? m.Input == input ? null : $"expected {input} missing; got {m.Input} missing"
                    : $"expected {input} missing; got {r}", null);
                break;
            }

            default:
                expectation = new(r => r is DeckResult.NoData ? null : $"expected no data; got {r}", null);
                break;
        }

        e.Done();
        return problems.Count > before ? null : expectation;
    }

    /// <summary>"16in round, 15in square, 6in thick", as a failure says a footing.</summary>
    static string Said(FootingSize size) => $"{Text(size.Round)} round, {Text(size.Square)} square, {Text(size.Thickness)} thick";
}
