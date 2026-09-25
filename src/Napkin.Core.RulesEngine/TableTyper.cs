using System.Collections.Immutable;
using System.Text.Json;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Types a composed table's rows against its (possibly amended) input declaration, then runs the
/// band validation of design §4.3. Runs after composition, so an overlay's rows are checked by
/// exactly the rules the base layer's are.
/// </summary>
internal static class TableTyper
{
    public static HeaderSizingTable? Type(RawTable raw, ProblemList problems)
    {
        int before = problems.Count;
        TableMeta meta = raw.Meta;
        List<HeaderRow> rows = [];
        foreach (RawRow row in raw.Rows)
        {
            HeaderRow? typed = TypeRow(raw.Designation, meta, row, problems);
            if (typed is not null)
            {
                rows.Add(typed);
            }
        }

        Where tableWhere = new(meta.File, raw.Designation);
        foreach (Footnote footnote in meta.Footnotes.Where(f => f.AppliesTo == FootnoteScope.Rows))
        {
            if (problems.Count == before && !rows.Any(r => r.Footnotes.Contains(footnote.Id)))
            {
                problems.Add(tableWhere, $"footnote '{footnote.Id}' applies to rows, but no row lists it.");
            }
        }

        if (problems.Count == before)
        {
            BandValidator.Validate(tableWhere, meta.Inputs, rows, problems);
        }

        if (problems.Count > before)
        {
            return null;
        }

        SourceRef source = SourceOf(meta.Document, meta.Location);
        return new HeaderSizingTable(
            raw.Designation,
            meta.Title,
            meta.WallKind,
            meta.Layer,
            source,
            meta.Inputs,
            meta.Footnotes,
            rows.OrderBy(r => r.Id, StringComparer.Ordinal).ToValueList());
    }

    public static SourceRef SourceOf(SourceDocument doc, string location)
        => new(doc.Id, doc.Title, location, doc.Url, doc.Printing, doc.RetrievedOn, doc.Sha256);

    private static HeaderRow? TypeRow(string designation, TableMeta meta, RawRow row, ProblemList problems)
    {
        Where where = new(row.File, designation, row.Id);
        JsonObj? o = JsonObj.Create(row.Element, row.Path, where, problems);
        if (o is null)
        {
            return null;
        }

        int before = problems.Count;
        o.MarkUsed("id");
        string? location = o.String("location");

        ImmutableSortedDictionary<string, CellValue>.Builder inputs = ImmutableSortedDictionary.CreateBuilder<string, CellValue>(StringComparer.Ordinal);
        foreach (InputColumn column in meta.Inputs)
        {
            string path = o.Child(column.Name);
            CellValue? cell = TableReader.ReadCell(o.Get(column.Name), column.Type, path, where, problems);
            if (cell is null)
            {
                continue;
            }

            if (column.Type == ColumnType.Enum && !column.Values.Contains(cell.Value.Symbol!, StringComparer.Ordinal))
            {
                problems.Add(where, $"{path}: '{cell.Value.Symbol}' is not one of the column's declared values.");
                continue;
            }

            if (column.Domain is { } domain
                && (cell.Value.Magnitude < domain.Min.Magnitude || cell.Value.Magnitude > domain.Max.Magnitude))
            {
                problems.Add(where, $"{path}: {cell.Value} is outside the column's declared domain {domain.Min} to {domain.Max}.");
                continue;
            }

            inputs[column.Name] = cell.Value;
        }

        MemberSpec? header = null;
        JsonObj? h = o.Obj("header");
        if (h is not null)
        {
            int? plies = h.Int("plies", min: 1);
            string? nominal = h.String("nominal");
            h.Done();
            header = plies is null || nominal is null ? null : new MemberSpec(plies.Value, nominal);
        }

        int? jack = o.Int("jackStuds");
        int? king = o.Int("kingStuds");

        List<string> footnotes = [];
        IReadOnlyList<JsonElement>? refs = o.Array("footnotes", required: false);
        for (int i = 0; refs is not null && i < refs.Count; i++)
        {
            string? id = JsonObj.ReadString(refs[i], o.Child($"footnotes[{i}]"), where, problems);
            if (id is null)
            {
                continue;
            }

            Footnote? footnote = meta.Footnotes.FirstOrDefault(f => f.Id == id);
            if (footnote is null)
            {
                problems.Add(where, $"{o.Child($"footnotes[{i}]")}: footnote '{id}' is not declared by the table.");
            }
            else if (footnote.AppliesTo == FootnoteScope.Table)
            {
                problems.Add(where, $"{o.Child($"footnotes[{i}]")}: footnote '{id}' applies to the whole table; rows list only row footnotes.");
            }
            else
            {
                footnotes.Add(id);
            }
        }

        o.Done();
        if (problems.Count > before || location is null || header is null || jack is null || king is null)
        {
            return null;
        }

        return new HeaderRow(
            row.Id,
            inputs.ToImmutable(),
            header,
            jack.Value,
            king.Value,
            footnotes.ToValueList(),
            row.Layer,
            SourceOf(row.Document, location));
    }
}

/// <summary>
/// Gaps and overlaps are rejected at load (design §4.3). A transcription check: it catches "the
/// same band typed twice" and "a band missing for one combination", not "the code meant something else".
/// </summary>
internal static class BandValidator
{
    public static void Validate(Where where, IReadOnlyList<InputColumn> inputs, IReadOnlyList<HeaderRow> rows, ProblemList problems)
    {
        if (rows.Count == 0)
        {
            problems.Add(where, "the table has no rows.");
            return;
        }

        List<InputColumn> exact = [.. inputs.Where(c => c.Band == BandKind.Exact)];
        List<InputColumn> bounds = [.. inputs.Where(c => c.Band == BandKind.UpperBound)];
        InputColumn capacity = inputs.Single(c => c.Band == BandKind.Capacity);

        foreach (InputColumn column in exact)
        {
            foreach (string value in column.Values.Where(v => !rows.Any(r => r.Inputs[column.Name].Symbol == v)))
            {
                problems.Add(where, $"column '{column.Name}' declares '{value}' but no row covers it.");
            }
        }

        foreach (IGrouping<string, HeaderRow> group in rows.GroupBy(r => Key(r, exact)).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            string label = group.Key.Length == 0 ? "the table" : $"rows with {group.Key}";

            foreach (InputColumn column in bounds)
            {
                long top = group.Max(r => r.Inputs[column.Name].Magnitude);
                if (top != column.Domain!.Max.Magnitude)
                {
                    problems.Add(
                        where,
                        $"gap: for {label}, the '{column.Name}' bands stop at {Show(column, top)} but the column's domain declares max {column.Domain.Max}.");
                }
            }

            // Every combination of upper-bound bands must have rows, or an input would fall
            // through a hole into a more demanding band — or out of scope — that the table does not say.
            List<List<long>> distinct = [.. bounds.Select(c => group.Select(r => r.Inputs[c.Name].Magnitude).Distinct().Order().ToList())];
            HashSet<string> present = [.. group.Select(r => Key(r, bounds))];
            foreach (List<long> combination in Cartesian(distinct))
            {
                string key = string.Join("; ", bounds.Select((c, i) => $"{c.Name}={Show(c, combination[i])}"));
                if (!present.Contains(key))
                {
                    problems.Add(where, $"gap: for {label}, no rows for {key.Replace("=", " ≤ ", StringComparison.Ordinal)}.");
                }
            }

            foreach (IGrouping<string, HeaderRow> cell in group.GroupBy(r => Key(r, bounds)))
            {
                foreach (IGrouping<long, HeaderRow> same in cell.GroupBy(r => r.Inputs[capacity.Name].Magnitude).Where(g => g.Count() > 1))
                {
                    problems.Add(
                        where,
                        $"overlap: rows {string.Join(", ", same.Select(r => $"'{r.Id}'").Order(StringComparer.Ordinal))} have the same inputs and the same {capacity.Name} {Show(capacity, same.Key)}.");
                }
            }
        }
    }

    private static string Key(HeaderRow row, IEnumerable<InputColumn> columns)
        => string.Join("; ", columns.Select(c => $"{c.Name}={row.Inputs[c.Name]}"));

    private static string Show(InputColumn column, long magnitude) => new CellValue(column.Type, null, magnitude).ToString();

    private static IEnumerable<List<long>> Cartesian(List<List<long>> sets)
    {
        IEnumerable<List<long>> result = [[]];
        foreach (List<long> set in sets)
        {
            result = result.SelectMany(prefix => set.Select(v => (List<long>)[.. prefix, v])).ToList();
        }

        return result;
    }
}
