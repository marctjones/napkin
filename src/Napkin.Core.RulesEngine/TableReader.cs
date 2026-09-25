using System.Text.Json;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>Reads table files and table objects inside overlays (design §1.4).</summary>
internal static class TableReader
{
    public static RawTable? ReadTableFile(
        IPackSource source, string file, IReadOnlyDictionary<string, SourceDocument> sources, ProblemList problems)
    {
        JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            string? designation = root.String("table");
            if (designation is null)
            {
                return null;
            }

            root.Where = new Where(file, designation);
            JsonObj table = root;
            string? sourceId = table.String("source");
            SourceDocument? doc = ResolveSource(sourceId, sources, "source", table.Where, problems);
            TableMeta? meta = ReadMeta(table, doc, CitationLayer.ModelCode, file, problems);
            List<RawRow>? rows = ReadRows(table, file, CitationLayer.ModelCode, doc, problems);
            table.Done();
            return meta is null || rows is null ? null : new RawTable(designation, meta, rows);
        }
    }

    public static SourceDocument? ResolveSource(
        string? sourceId, IReadOnlyDictionary<string, SourceDocument> sources, string path, Where where, ProblemList problems)
    {
        if (sourceId is null)
        {
            return null;
        }

        if (sources.TryGetValue(sourceId, out SourceDocument? doc))
        {
            return doc;
        }

        problems.Add(where, $"{path}: source '{sourceId}' is not listed in the manifest's sources.");
        return null;
    }

    /// <summary>Reads a table's metadata: kind, title, wall kind, location, inputs, outputs, footnotes.</summary>
    public static TableMeta? ReadMeta(JsonObj o, SourceDocument? doc, CitationLayer layer, string file, ProblemList problems)
    {
        int before = problems.Count;
        string? kind = o.String("kind");
        if (kind is not null && kind != Vocabulary.HeaderSizingKind)
        {
            problems.Add(o.Where, $"kind: unknown table kind '{kind}'; this napkin knows '{Vocabulary.HeaderSizingKind}'.");
        }

        string? title = o.String("title");
        WallKind? wallKind = o.Enum("wallKind", Vocabulary.WallKinds);
        string? location = o.String("location");
        List<InputColumn> inputs = ReadInputs(o, problems);
        ReadOutputs(o, problems);
        List<Footnote> footnotes = ReadFootnotes(o, inputs, problems);

        if (problems.Count > before || title is null || wallKind is null || location is null || doc is null)
        {
            return null;
        }

        return new TableMeta(title, wallKind.Value, inputs.ToValueList(), footnotes.ToValueList(), layer, doc, location, file);
    }

    public static List<RawRow>? ReadRows(JsonObj o, string file, CitationLayer layer, SourceDocument? doc, ProblemList problems)
    {
        IReadOnlyList<JsonElement>? items = o.Array("rows");
        if (items is null)
        {
            return null;
        }

        List<RawRow> rows = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        bool ok = true;
        for (int i = 0; i < items.Count; i++)
        {
            RawRow? row = ReadRawRow(items[i], o.Child($"rows[{i}]"), file, o.Where, layer, doc, problems);
            if (row is null)
            {
                ok = false;
                continue;
            }

            if (!ids.Add(row.Id))
            {
                problems.Add(o.Where with { Row = row.Id }, $"rows[{i}]: row id '{row.Id}' appears twice.");
                ok = false;
                continue;
            }

            rows.Add(row);
        }

        return ok && doc is not null ? rows : null;
    }

    public static RawRow? ReadRawRow(
        JsonElement element, string path, string file, Where where, CitationLayer layer, SourceDocument? doc, ProblemList problems)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            problems.Add(where, $"{path}: must be a JSON object.");
            return null;
        }

        string? id = element.TryGetProperty("id", out JsonElement idElement)
            ? JsonObj.ReadString(idElement, path + ".id", where, problems)
            : null;
        if (id is null)
        {
            if (!element.TryGetProperty("id", out _))
            {
                problems.Add(where, $"{path}.id: missing required field.");
            }

            return null;
        }

        return doc is null ? null : new RawRow(id, element.Clone(), file, path, layer, doc);
    }

    private static List<InputColumn> ReadInputs(JsonObj o, ProblemList problems)
    {
        List<InputColumn> columns = [];
        IReadOnlyList<JsonElement>? items = o.Array("inputs", minItems: 1);
        if (items is null)
        {
            return columns;
        }

        for (int i = 0; i < items.Count; i++)
        {
            string path = o.Child($"inputs[{i}]");
            JsonObj? c = JsonObj.Create(items[i], path, o.Where, problems);
            if (c is null)
            {
                continue;
            }

            InputColumn? column = ReadInput(c, path, problems);
            c.Done();
            if (column is null)
            {
                continue;
            }

            if (columns.Any(x => x.Name == column.Name))
            {
                problems.Add(o.Where, $"{path}.name: column '{column.Name}' is declared twice.");
                continue;
            }

            columns.Add(column);
        }

        int spans = columns.Count(c => c.Name == Vocabulary.HeaderSpan);
        if (spans != 1 && items.Count == columns.Count)
        {
            problems.Add(o.Where, $"{o.Child("inputs")}: a header-sizing table needs exactly one '{Vocabulary.HeaderSpan}' capacity column.");
        }

        return columns;
    }

    private static InputColumn? ReadInput(JsonObj c, string path, ProblemList problems)
    {
        int before = problems.Count;
        string? name = c.String("name");
        ColumnType? type = c.Enum("type", Vocabulary.ColumnTypes);
        BandKind? band = c.Enum("band", Vocabulary.BandKinds);
        if (name is null || type is null || band is null)
        {
            return null;
        }

        if (!Vocabulary.HeaderInputs.TryGetValue(name, out ColumnType expected))
        {
            string known = string.Join(", ", Vocabulary.HeaderInputs.Keys.Order(StringComparer.Ordinal));
            problems.Add(c.Where, $"{path}.name: '{name}' is not an input napkin can supply; header-sizing columns are: {known}.");
            return null;
        }

        if (type != expected)
        {
            problems.Add(c.Where, $"{path}.type: '{name}' is '{Vocabulary.TypeName(expected)}', not '{Vocabulary.TypeName(type.Value)}'.");
            return null;
        }

        List<string> values = [];
        ColumnDomain? domain = null;
        if (type == ColumnType.Enum)
        {
            if (band != BandKind.Exact)
            {
                problems.Add(c.Where, $"{path}.band: a category column must use 'exact' bands.");
            }

            if (c.Has("domain"))
            {
                c.MarkUsed("domain");
                problems.Add(c.Where, $"{path}.domain: a category column has 'values', not a domain.");
            }

            IReadOnlyList<JsonElement>? items = c.Array("values", minItems: 1);
            for (int i = 0; items is not null && i < items.Count; i++)
            {
                string? value = JsonObj.ReadString(items[i], $"{path}.values[{i}]", c.Where, problems);
                if (value is not null && values.Contains(value, StringComparer.Ordinal))
                {
                    problems.Add(c.Where, $"{path}.values[{i}]: '{value}' is listed twice.");
                }
                else if (value is not null)
                {
                    values.Add(value);
                }
            }
        }
        else
        {
            if (band == BandKind.Exact)
            {
                problems.Add(c.Where, $"{path}.band: 'exact' bands are for category columns; '{name}' is upper-bound or capacity.");
            }
            else if (band == BandKind.Capacity && name != Vocabulary.HeaderSpan)
            {
                problems.Add(c.Where, $"{path}.band: only '{Vocabulary.HeaderSpan}' is a capacity column.");
            }
            else if (name == Vocabulary.HeaderSpan && band != BandKind.Capacity)
            {
                problems.Add(c.Where, $"{path}.band: '{Vocabulary.HeaderSpan}' must be a 'capacity' column.");
            }

            if (c.Has("values"))
            {
                c.MarkUsed("values");
                problems.Add(c.Where, $"{path}.values: only category columns have values.");
            }

            JsonObj? d = c.Obj("domain");
            if (d is not null)
            {
                CellValue? min = ReadCell(d.Get("min"), type.Value, $"{path}.domain.min", c.Where, problems);
                CellValue? max = ReadCell(d.Get("max"), type.Value, $"{path}.domain.max", c.Where, problems);
                d.Done();
                if (min is not null && max is not null)
                {
                    if (min.Value.Magnitude > max.Value.Magnitude)
                    {
                        problems.Add(c.Where, $"{path}.domain: min {min} is above max {max}.");
                    }
                    else
                    {
                        domain = new ColumnDomain(min.Value, max.Value);
                    }
                }
            }
        }

        return problems.Count > before ? null : new InputColumn(name, type.Value, band.Value, values.ToValueList(), domain);
    }

    private static void ReadOutputs(JsonObj o, ProblemList problems)
    {
        // The header-sizing kind has exactly these outputs; declaring them keeps the file
        // self-describing for a reviewer, and a mismatch is a table of some other kind.
        (string Name, string Type)[] expected = [("header", "member"), ("jackStuds", "count"), ("kingStuds", "count")];
        IReadOnlyList<JsonElement>? items = o.Array("outputs");
        if (items is null)
        {
            return;
        }

        List<(string, string)> found = [];
        for (int i = 0; i < items.Count; i++)
        {
            JsonObj? c = JsonObj.Create(items[i], o.Child($"outputs[{i}]"), o.Where, problems);
            if (c is null)
            {
                return;
            }

            string? name = c.String("name");
            string? type = c.String("type");
            c.Done();
            if (name is null || type is null)
            {
                return;
            }

            found.Add((name, type));
        }

        if (found.Count != expected.Length || !expected.All(found.Contains))
        {
            problems.Add(
                o.Where,
                $"{o.Child("outputs")}: a header-sizing table's outputs are exactly header (member), jackStuds (count), kingStuds (count).");
        }
    }

    private static List<Footnote> ReadFootnotes(JsonObj o, List<InputColumn> inputs, ProblemList problems)
    {
        List<Footnote> footnotes = [];
        IReadOnlyList<JsonElement>? items = o.Array("footnotes");
        if (items is null)
        {
            return footnotes;
        }

        for (int i = 0; i < items.Count; i++)
        {
            string path = o.Child($"footnotes[{i}]");
            JsonObj? f = JsonObj.Create(items[i], path, o.Where, problems);
            if (f is null)
            {
                continue;
            }

            int before = problems.Count;
            string? id = f.String("id");
            string? text = f.String("text");
            FootnoteEncoding? encoding = null;
            if (!f.Has("encodedAs"))
            {
                f.MarkUsed("encodedAs");
                problems.Add(
                    o.Where with { Row = id is null ? null : $"footnote {id}" },
                    $"{path}: footnote is not classified. Set encodedAs to 'not-encoded', 'as-rows' or 'as-limit'; a table with an unclassified footnote is invalid (design §1.4).");
            }
            else
            {
                encoding = f.Enum("encodedAs", Vocabulary.Encodings);
            }

            FootnoteScope? scope = f.Enum("appliesTo", Vocabulary.Scopes);
            FootnoteLimit? limit = null;
            if (encoding == FootnoteEncoding.AsLimit)
            {
                JsonObj? l = f.Obj("limit");
                if (l is not null)
                {
                    limit = ReadLimit(l, $"{path}.limit", inputs, problems);
                    l.Done();
                }
            }
            else if (f.Has("limit"))
            {
                f.MarkUsed("limit");
                problems.Add(o.Where, $"{path}.limit: only an 'as-limit' footnote has a limit.");
            }

            f.Done();
            if (problems.Count > before || id is null || text is null || encoding is null || scope is null)
            {
                continue;
            }

            if (footnotes.Any(x => x.Id == id))
            {
                problems.Add(o.Where, $"{path}.id: footnote '{id}' is listed twice.");
                continue;
            }

            footnotes.Add(new Footnote(id, text, encoding.Value, scope.Value, limit));
        }

        return footnotes;
    }

    private static FootnoteLimit? ReadLimit(JsonObj l, string path, List<InputColumn> inputs, ProblemList problems)
    {
        string? input = l.String("input");
        if (input is null)
        {
            return null;
        }

        if (!Vocabulary.HeaderInputs.TryGetValue(input, out ColumnType type))
        {
            problems.Add(l.Where, $"{path}.input: '{input}' is not an input napkin can supply.");
            return null;
        }

        bool hasAbove = l.Has("above");
        bool hasEquals = l.Has("equals");
        if (hasAbove == hasEquals)
        {
            l.MarkUsed("above");
            l.MarkUsed("equals");
            problems.Add(l.Where, $"{path}: a limit has exactly one of 'above' (a number or length) or 'equals' (a category).");
            return null;
        }

        if (hasAbove)
        {
            if (type == ColumnType.Enum)
            {
                l.MarkUsed("above");
                problems.Add(l.Where, $"{path}.above: '{input}' is a category; use 'equals'.");
                return null;
            }

            CellValue? above = ReadCell(l.Get("above"), type, $"{path}.above", l.Where, problems);
            return above is null ? null : new FootnoteLimit(input, above, null);
        }

        if (type != ColumnType.Enum)
        {
            l.MarkUsed("equals");
            problems.Add(l.Where, $"{path}.equals: '{input}' is not a category; use 'above'.");
            return null;
        }

        string? value = l.String("equals");
        InputColumn? column = inputs.FirstOrDefault(c => c.Name == input);
        if (value is not null && column is not null && !column.Values.Contains(value, StringComparer.Ordinal))
        {
            problems.Add(l.Where, $"{path}.equals: '{value}' is not one of the column's values.");
            return null;
        }

        return value is null ? null : new FootnoteLimit(input, null, value);
    }

    /// <summary>Reads one cell by its column type: a category string, a whole number, or an exact length.</summary>
    public static CellValue? ReadCell(JsonElement? element, ColumnType type, string path, Where where, ProblemList problems)
    {
        if (element is null)
        {
            return null;
        }

        switch (type)
        {
            case ColumnType.Enum:
                string? symbol = JsonObj.ReadString(element.Value, path, where, problems);
                return symbol is null ? null : CellValue.Category(symbol);
            case ColumnType.Length:
                Length? length = JsonObj.ReadLength(element.Value, path, where, problems);
                return length is null ? null : CellValue.Of(length.Value);
            default:
                int? whole = JsonObj.ReadInt(element.Value, path, where, problems);
                return whole is null ? null : CellValue.Whole(type, whole.Value);
        }
    }
}
