using System.Text.Json;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Reads an <c>as-operations</c> footnote's <c>operations</c> (design §4.4). Checks what can be
/// checked without the table's rows here; <see cref="FootnoteOperationValidator"/> checks the rest
/// after composition, against the composed columns and bands.
/// </summary>
internal static class FootnoteOperationReader
{
    public static List<FootnoteOperation> Read(JsonObj f, string path, ProblemList problems)
    {
        List<FootnoteOperation> operations = [];
        IReadOnlyList<JsonElement>? items = f.Array("operations", minItems: 1);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string opPath = $"{path}.operations[{i}]";
            JsonObj? o = JsonObj.Create(items[i], opPath, f.Where, problems);
            if (o is null)
            {
                continue;
            }

            string? op = o.String("op");
            FootnoteOperation? read = op switch
            {
                null => null,
                Vocabulary.SubstituteInputOp => ReadSubstitute(o, opPath, problems),
                Vocabulary.InterpolateOp => ReadInterpolate(o, opPath, problems),
                _ => Unknown(o, opPath, op, problems),
            };
            o.Done();
            if (read is null)
            {
                continue;
            }

            if (operations.Any(x => x.GetType() == read.GetType()))
            {
                problems.Add(f.Where, $"{opPath}: a footnote declares at most one '{op}' operation.");
                continue;
            }

            operations.Add(read);
        }

        return operations;
    }

    private static FootnoteOperation? Unknown(JsonObj o, string path, string op, ProblemList problems)
    {
        foreach (string name in o.Names)
        {
            o.MarkUsed(name);
        }

        problems.Add(o.Where, $"{path}.op: unknown footnote operation '{op}'; this napkin knows '{Vocabulary.SubstituteInputOp}' and '{Vocabulary.InterpolateOp}'.");
        return null;
    }

    private static FootnoteOperation.SubstituteInput? ReadSubstitute(JsonObj o, string path, ProblemList problems)
    {
        int before = problems.Count;
        (string? input, ColumnType type) = BandedInput(o, path, problems);
        CellValue? below = input is null ? Skip(o, "below") : TableReader.ReadCell(o.Get("below"), type, $"{path}.below", o.Where, problems);
        CellValue? use = input is null ? Skip(o, "use") : TableReader.ReadCell(o.Get("use"), type, $"{path}.use", o.Where, problems);
        string? whenInput = null;
        CellValue? atMost = null;
        JsonObj? when = o.Obj("when");
        if (when is not null)
        {
            whenInput = when.String("input");
            if (whenInput is not null && !Vocabulary.ConditionInputs.TryGetValue(whenInput, out ColumnType whenType))
            {
                string known = string.Join(", ", Vocabulary.ConditionInputs.Keys.Order(StringComparer.Ordinal));
                problems.Add(o.Where, $"{path}.when.input: '{whenInput}' is not an input a condition can test; these are: {known}.");
                Skip(when, "atMost");
            }
            else if (whenInput is not null)
            {
                atMost = TableReader.ReadCell(when.Get("atMost"), Vocabulary.ConditionInputs[whenInput], $"{path}.when.atMost", o.Where, problems);
            }

            when.Done();
        }

        if (problems.Count == before && use!.Value.Magnitude < below!.Value.Magnitude)
        {
            problems.Add(o.Where, $"{path}.use: {use} is below {below}; a substituted input is never less demanding than the inputs it replaces.");
        }

        return problems.Count > before || input is null || below is null || use is null || whenInput is null || atMost is null
            ? null
            : new FootnoteOperation.SubstituteInput(input, below.Value, use.Value, whenInput, atMost.Value);
    }

    private static FootnoteOperation.Interpolate? ReadInterpolate(JsonObj o, string path, ProblemList problems)
    {
        int before = problems.Count;
        (string? input, ColumnType type) = BandedInput(o, path, problems);
        List<CellValue> bounds = [];
        IReadOnlyList<JsonElement>? items = o.Array("between");
        if (items is not null && items.Count != 2)
        {
            problems.Add(o.Where, $"{path}.between: exactly two columns, the lower and the upper.");
        }
        else if (items is not null && input is not null)
        {
            for (int i = 0; i < 2; i++)
            {
                if (TableReader.ReadCell(items[i], type, $"{path}.between[{i}]", o.Where, problems) is { } cell)
                {
                    bounds.Add(cell);
                }
            }
        }

        if (bounds.Count == 2 && bounds[0].Magnitude >= bounds[1].Magnitude)
        {
            problems.Add(o.Where, $"{path}.between: {bounds[0]} is not below {bounds[1]}; list the lower column first.");
        }

        string? quantity = o.String("quantity");
        if (quantity is not null && quantity != Vocabulary.HeaderSpan)
        {
            problems.Add(o.Where, $"{path}.quantity: only '{Vocabulary.HeaderSpan}' (the permitted span) is interpolated; counts never are.");
        }

        return problems.Count > before || input is null || bounds.Count != 2 || quantity is null
            ? null
            : new FootnoteOperation.Interpolate(input, bounds[0], bounds[1], quantity);
    }

    /// <summary>The operation's <c>input</c>: a numeric header input that is not the span.</summary>
    private static (string? Input, ColumnType Type) BandedInput(JsonObj o, string path, ProblemList problems)
    {
        string? input = o.String("input");
        if (input is null)
        {
            return (null, default);
        }

        if (!Vocabulary.HeaderInputs.TryGetValue(input, out ColumnType type) || type == ColumnType.Enum || input == Vocabulary.HeaderSpan)
        {
            problems.Add(o.Where, $"{path}.input: '{input}' is not a banded site input; an operation acts on an upper-bound column such as groundSnowLoad.");
            return (null, default);
        }

        return (input, type);
    }

    private static CellValue? Skip(JsonObj o, string name)
    {
        o.MarkUsed(name);
        return null;
    }
}

/// <summary>
/// Checks a composed table's <c>as-operations</c> footnotes against its columns and bands (design
/// §4.3, §4.4): an operation's input is one of the table's upper-bound columns; an interpolation's
/// two columns are adjacent bands present for every group of rows; each input has at most one
/// operation of each kind; and a member appears at most once per cell of an interpolated column.
/// </summary>
internal static class FootnoteOperationValidator
{
    public static void Validate(Where where, TableMeta meta, IReadOnlyList<HeaderRow> rows, ProblemList problems)
    {
        List<(Footnote Footnote, FootnoteOperation Op)> ops = [.. meta.Footnotes.SelectMany(f => f.Operations.Select(op => (f, op)))];
        foreach (IGrouping<(Type, string), (Footnote Footnote, FootnoteOperation Op)> twice in ops
                     .GroupBy(x => (x.Op.GetType(), InputOf(x.Op)))
                     .Where(g => g.Count() > 1))
        {
            problems.Add(
                where,
                $"footnotes {string.Join(" and ", twice.Select(x => $"'{x.Footnote.Id}'"))} both declare a '{Name(twice.First().Op)}' operation on '{twice.Key.Item2}'; one per input.");
        }

        List<InputColumn> exact = [.. meta.Inputs.Where(c => c.Band == BandKind.Exact)];
        foreach ((Footnote footnote, FootnoteOperation op) in ops)
        {
            string input = InputOf(op);
            InputColumn? column = meta.Inputs.FirstOrDefault(c => c.Name == input);
            if (column is null || column.Band != BandKind.UpperBound)
            {
                problems.Add(where, $"footnote '{footnote.Id}': its '{Name(op)}' operation acts on '{input}', which is not an upper-bound column of this table.");
                continue;
            }

            if (op is not FootnoteOperation.Interpolate interpolate)
            {
                continue;
            }

            foreach (IGrouping<string, HeaderRow> group in rows.GroupBy(r => string.Join("; ", exact.Select(c => $"{c.Name}={r.Inputs[c.Name]}"))))
            {
                List<long> bands = [.. group.Select(r => r.Inputs[input].Magnitude).Distinct().Order()];
                string label = group.Key.Length == 0 ? "the table" : $"rows with {group.Key}";
                int lower = bands.IndexOf(interpolate.Lower.Magnitude);
                int upper = bands.IndexOf(interpolate.Upper.Magnitude);
                if (lower < 0 || upper < 0)
                {
                    problems.Add(
                        where,
                        $"footnote '{footnote.Id}': interpolation between {interpolate.Lower} and {interpolate.Upper}, but {label} have no '{input}' band at {(lower < 0 ? interpolate.Lower : interpolate.Upper)}.");
                }
                else if (upper != lower + 1)
                {
                    problems.Add(
                        where,
                        $"footnote '{footnote.Id}': interpolation between {interpolate.Lower} and {interpolate.Upper}, but {label} have a band between them; interpolation is only between adjacent columns.");
                }
            }

            List<InputColumn> others = [.. meta.Inputs.Where(c => c.Band != BandKind.Capacity)];
            foreach (IGrouping<string, HeaderRow> cell in rows
                         .Where(r => r.Inputs[input].Magnitude == interpolate.Lower.Magnitude || r.Inputs[input].Magnitude == interpolate.Upper.Magnitude)
                         .GroupBy(r => string.Join("; ", others.Select(c => $"{c.Name}={r.Inputs[c.Name]}")) + $"; header={r.Header}")
                         .Where(g => g.Count() > 1)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                problems.Add(
                    where,
                    $"footnote '{footnote.Id}': rows {string.Join(", ", cell.Select(r => $"'{r.Id}'").Order(StringComparer.Ordinal))} give the same member in one cell, so interpolation cannot pair them.");
            }
        }
    }

    private static string InputOf(FootnoteOperation op) => op switch
    {
        FootnoteOperation.SubstituteInput s => s.Input,
        FootnoteOperation.Interpolate i => i.Input,
        _ => throw new InvalidOperationException(),
    };

    private static string Name(FootnoteOperation op) => op is FootnoteOperation.Interpolate ? Vocabulary.InterpolateOp : Vocabulary.SubstituteInputOp;
}
