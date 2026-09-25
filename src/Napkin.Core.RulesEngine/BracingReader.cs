using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Reads a base layer's wall-bracing provisions (<c>layers/&lt;id&gt;/bracing/&lt;file&gt;.json</c>,
/// docs/rules-engine.md "Wall bracing"). Strict like every pack file: unknown fields, an uncited
/// factor or limit, an unclassified footnote, a non-positive step or unit, and gaps or overlaps in
/// any band are load problems, all collected.
/// </summary>
internal static class BracingReader
{
    public static BracingProvisions? Read(
        IPackSource source, string file, IReadOnlyDictionary<string, SourceDocument> sources, ProblemList problems)
    {
        JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            int before = problems.Count;
            string? section = root.String("section");
            if (section is not null)
            {
                root.Where = new Where(file, section);
            }

            string? kind = root.String("kind");
            if (kind is not null && kind != Vocabulary.WallBracingKind)
            {
                problems.Add(root.Where, $"kind: a file under bracing/ is '{Vocabulary.WallBracingKind}', not '{kind}'.");
            }

            string? title = root.String("title");
            SourceDocument? doc = TableReader.ResolveSource(root.String("source"), sources, "source", root.Where, problems);
            string? location = root.String("location");
            Length? step = PositiveLength(root, "step");
            Length? unit = PositiveLength(root, "unitLength");
            List<InputColumn> inputs = ReadInputs(root, problems);
            List<Footnote> footnotes = ReadFootnotes(root, problems);

            if (doc is null || section is null)
            {
                root.MarkUsed("required");
                root.MarkUsed("factors");
                root.MarkUsed("limits");
                root.MarkUsed("methods");
                root.Done();
                return null;
            }

            SourceRef Cite(string where) => TableTyper.SourceOf(doc, where);
            List<BracingRequiredRow> required = ReadRequired(root, inputs, Cite, problems);
            List<BracingFactor> factors = ReadConditioned(root, "factors", inputs, problems, (o, id, sec, when, at) => ReadFactor(o, id, sec, when, Cite(at)));
            List<BracingLimit> limits = ReadConditioned(root, "limits", inputs, problems, (o, id, sec, when, at) =>
                o.String("text") is { } text ? new BracingLimit(id, sec, when, text, Cite(at)) : null);
            List<BracingMethod> methods = ReadMethods(root, Cite, problems);
            root.Done();

            if (problems.Count == before)
            {
                BandValidator.Validate(root.Where, inputs, [.. required.Select(r => new BandRow(r.Id, r.Inputs))], problems);
            }

            if (problems.Count > before || title is null || location is null || step is null || unit is null)
            {
                return null;
            }

            return new BracingProvisions(
                section,
                title,
                CitationLayer.ModelCode,
                Cite(location),
                step.Value,
                unit.Value,
                inputs.ToValueList(),
                required.OrderBy(r => r.Id, StringComparer.Ordinal).ToValueList(),
                factors.ToValueList(),
                limits.ToValueList(),
                methods.ToValueList(),
                footnotes.ToValueList());
        }
    }

    private static Length? PositiveLength(JsonObj o, string name)
    {
        JsonElement? e = o.Get(name);
        Length? length = e is null ? null : JsonObj.ReadLength(e.Value, o.Child(name), o.Where, o.Problems);
        if (length is { } l && l <= Length.Zero)
        {
            o.Problems.Add(o.Where, $"{o.Child(name)}: must be longer than zero.");
            return null;
        }

        return length;
    }

    private static List<InputColumn> ReadInputs(JsonObj o, ProblemList problems)
    {
        List<InputColumn> columns = [];
        IReadOnlyList<JsonElement>? items = o.Array("inputs");
        for (int i = 0; items is not null && i < items.Count; i++)
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
                problems.Add(o.Where, $"{path}.name: '{column.Name}' is declared twice.");
                continue;
            }

            columns.Add(column);
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
            c.MarkUsed("values");
            c.MarkUsed("domain");
            return null;
        }

        if (!Vocabulary.BracingInputs.TryGetValue(name, out ColumnType expected))
        {
            problems.Add(c.Where, $"{path}.name: '{name}' is not an input napkin can supply to a bracing check; they are: {Known()}.");
            c.MarkUsed("values");
            c.MarkUsed("domain");
            return null;
        }

        if (type != expected)
        {
            problems.Add(c.Where, $"{path}.type: '{name}' is '{Vocabulary.TypeName(expected)}', not '{Vocabulary.TypeName(type.Value)}'.");
        }

        List<string> values = [];
        ColumnDomain? domain = null;
        if (expected == ColumnType.Enum)
        {
            if (band != BandKind.Exact)
            {
                problems.Add(c.Where, $"{path}.band: a category column uses 'exact' bands.");
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
            if (band != BandKind.UpperBound)
            {
                problems.Add(c.Where, $"{path}.band: a bracing column that is not a category uses 'upper-bound' bands.");
            }

            domain = ReadDomain(c, "domain", expected, path, problems);
        }

        return problems.Count > before ? null : new InputColumn(name, expected, band.Value, values.ToValueList(), domain);
    }

    private static ColumnDomain? ReadDomain(JsonObj c, string name, ColumnType type, string path, ProblemList problems)
    {
        JsonObj? d = c.Obj(name);
        if (d is null)
        {
            return null;
        }

        CellValue? min = TableReader.ReadCell(d.Get("min"), type, $"{path}.{name}.min", c.Where, problems);
        CellValue? max = TableReader.ReadCell(d.Get("max"), type, $"{path}.{name}.max", c.Where, problems);
        d.Done();
        if (min is null || max is null)
        {
            return null;
        }

        if (min.Value.Magnitude > max.Value.Magnitude)
        {
            problems.Add(c.Where, $"{path}.{name}: min {min} is above max {max}.");
            return null;
        }

        return new ColumnDomain(min.Value, max.Value);
    }

    private static List<BracingRequiredRow> ReadRequired(
        JsonObj root, List<InputColumn> inputs, Func<string, SourceRef> cite, ProblemList problems)
    {
        List<BracingRequiredRow> rows = [];
        IReadOnlyList<JsonElement>? items = root.Array("required", minItems: 1);
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = root.Child($"required[{i}]");
            JsonObj? o = JsonObj.Create(items[i], path, root.Where, problems);
            if (o is null)
            {
                continue;
            }

            int before = problems.Count;
            string? id = o.String("id");
            if (id is not null)
            {
                o.Where = root.Where with { Row = id };
            }

            string? location = o.String("location");
            JsonElement? lengthElement = o.Get("length");
            Length? length = lengthElement is null ? null : JsonObj.ReadLength(lengthElement.Value, o.Child("length"), o.Where, problems);
            ImmutableSortedDictionary<string, CellValue>.Builder cells = ImmutableSortedDictionary.CreateBuilder<string, CellValue>(StringComparer.Ordinal);
            foreach (InputColumn column in inputs)
            {
                CellValue? cell = TableReader.ReadCell(o.Get(column.Name), column.Type, o.Child(column.Name), o.Where, problems);
                if (cell is null)
                {
                    continue;
                }

                if (column.Type == ColumnType.Enum && !column.Values.Contains(cell.Value.Symbol!, StringComparer.Ordinal))
                {
                    problems.Add(o.Where, $"{o.Child(column.Name)}: '{cell.Value.Symbol}' is not one of the column's declared values.");
                }
                else if (column.Domain is { } domain && (cell.Value.Magnitude < domain.Min.Magnitude || cell.Value.Magnitude > domain.Max.Magnitude))
                {
                    problems.Add(o.Where, $"{o.Child(column.Name)}: {cell.Value} is outside the column's declared domain {domain.Min} to {domain.Max}.");
                }
                else
                {
                    cells[column.Name] = cell.Value;
                }
            }

            o.Done();
            if (length is { } l && l < Length.Zero)
            {
                problems.Add(o.Where, $"{o.Child("length")}: a required length is not negative.");
            }

            if (id is not null && !ids.Add(id))
            {
                problems.Add(o.Where, $"{path}: row id '{id}' appears twice.");
            }

            if (problems.Count == before && id is not null && location is not null && length is not null)
            {
                rows.Add(new BracingRequiredRow(id, cells.ToImmutable(), length.Value, cite(location)));
            }
        }

        return rows;
    }

    private delegate T? ConditionedReader<T>(JsonObj o, string id, string section, BracingCondition when, string location);

    /// <summary>Reads factors or limits: each has an id, its own section and location (a citation), a condition, and its body.</summary>
    private static List<T> ReadConditioned<T>(
        JsonObj root, string name, List<InputColumn> inputs, ProblemList problems, ConditionedReader<T> body)
        where T : class
    {
        List<T> found = [];
        IReadOnlyList<JsonElement>? items = root.Array(name);
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = root.Child($"{name}[{i}]");
            JsonObj? o = JsonObj.Create(items[i], path, root.Where, problems);
            if (o is null)
            {
                continue;
            }

            int before = problems.Count;
            string? id = o.String("id");
            if (id is not null)
            {
                o.Where = root.Where with { Row = id };
            }

            string? section = Cited(o, "section");
            string? location = Cited(o, "location");
            BracingCondition? when = ReadCondition(o, inputs, problems);
            T? item = id is null || section is null || location is null || when is null ? null : body(o, id, section, when, location);
            if (item is null)
            {
                o.MarkUsed("multiply");
                o.MarkUsed("add");
                o.MarkUsed("text");
            }

            o.Done();
            if (id is not null && !ids.Add(id))
            {
                problems.Add(o.Where, $"{path}: id '{id}' appears twice.");
            }

            if (problems.Count == before && item is not null)
            {
                found.Add(item);
            }
        }

        return found;
    }

    /// <summary>A citation field: required and not blank, so every factor and limit names where it was read.</summary>
    private static string? Cited(JsonObj o, string name)
    {
        if (!o.Has(name))
        {
            o.MarkUsed(name);
            o.Problems.Add(o.Where, $"{o.Child(name)}: missing; every factor and limit cites its {name} in the source.");
            return null;
        }

        string? text = o.String(name);
        if (text is not null && string.IsNullOrWhiteSpace(text))
        {
            o.Problems.Add(o.Where, $"{o.Child(name)}: blank; every factor and limit cites its {name} in the source.");
            return null;
        }

        return text;
    }

    private static BracingCondition? ReadCondition(JsonObj o, List<InputColumn> inputs, ProblemList problems)
    {
        JsonObj? w = o.Obj("when");
        if (w is null)
        {
            return null;
        }

        string path = o.Child("when");
        string? input = w.String("input");
        bool hasAbove = w.Has("above");
        bool hasEquals = w.Has("equals");
        w.MarkUsed("above");
        w.MarkUsed("equals");
        BracingCondition? result = null;
        if (input is null)
        {
            // reported by String
        }
        else if (!Vocabulary.BracingInputs.TryGetValue(input, out ColumnType type))
        {
            problems.Add(w.Where, $"{path}.input: '{input}' is not an input napkin can supply to a bracing check; they are: {Known()}.");
        }
        else if (hasAbove == hasEquals)
        {
            problems.Add(w.Where, $"{path}: a condition has exactly one of 'above' (a number or length) or 'equals' (a category).");
        }
        else if (hasAbove && type == ColumnType.Enum)
        {
            problems.Add(w.Where, $"{path}.above: '{input}' is a category; use 'equals'.");
        }
        else if (hasEquals && type != ColumnType.Enum)
        {
            problems.Add(w.Where, $"{path}.equals: '{input}' is not a category; use 'above'.");
        }
        else if (hasAbove)
        {
            CellValue? above = TableReader.ReadCell(w.Get("above"), type, $"{path}.above", w.Where, problems);
            result = above is null ? null : new BracingCondition(input, above, null);
        }
        else
        {
            string? value = w.String("equals");
            InputColumn? column = inputs.FirstOrDefault(c => c.Name == input);
            if (value is not null && column is not null && !column.Values.Contains(value, StringComparer.Ordinal))
            {
                problems.Add(w.Where, $"{path}.equals: '{value}' is not one of the column's values.");
            }
            else if (value is not null)
            {
                result = new BracingCondition(input, null, value);
            }
        }

        w.Done();
        return result;
    }

    private static BracingFactor? ReadFactor(JsonObj o, string id, string section, BracingCondition when, SourceRef source)
    {
        bool hasMultiply = o.Has("multiply");
        bool hasAdd = o.Has("add");
        o.MarkUsed("multiply");
        o.MarkUsed("add");
        if (hasMultiply == hasAdd)
        {
            o.Problems.Add(o.Where, $"{o.Child("multiply")}: a factor has exactly one of 'multiply' (an exact fraction, \"3/2\") or 'add' (a length).");
            return null;
        }

        if (hasAdd)
        {
            JsonElement? e = o.Get("add");
            Length? add = e is null ? null : JsonObj.ReadLength(e.Value, o.Child("add"), o.Where, o.Problems);
            if (add is { } a && a < Length.Zero)
            {
                o.Problems.Add(o.Where, $"{o.Child("add")}: an added length is not negative.");
                return null;
            }

            return add is null ? null : new BracingFactor(id, section, when, null, add, source);
        }

        string? text = o.String("multiply");
        ExactFraction? multiply = text is null ? null : ParseFraction(text);
        if (text is not null && multiply is null)
        {
            o.Problems.Add(o.Where, $"{o.Child("multiply")}: '{text}' is not a positive exact fraction written \"n\" or \"n/d\" with whole numbers.");
            return null;
        }

        return multiply is null ? null : new BracingFactor(id, section, when, multiply, null, source);
    }

    /// <summary>"3/2" or "2": whole, positive numbers only; never a decimal (design §1.5).</summary>
    internal static ExactFraction? ParseFraction(string text)
    {
        string[] parts = text.Split('/');
        if (parts.Length is < 1 or > 2 || parts.Any(p => p.Length == 0 || p.Length > 9 || !p.All(char.IsAsciiDigit)))
        {
            return null;
        }

        long n = long.Parse(parts[0], CultureInfo.InvariantCulture);
        long d = parts.Length == 2 ? long.Parse(parts[1], CultureInfo.InvariantCulture) : 1;
        return n <= 0 || d <= 0 ? null : new ExactFraction(n, d);
    }

    private static List<BracingMethod> ReadMethods(JsonObj root, Func<string, SourceRef> cite, ProblemList problems)
    {
        List<BracingMethod> methods = [];
        IReadOnlyList<JsonElement>? items = root.Array("methods", minItems: 1);
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = root.Child($"methods[{i}]");
            JsonObj? o = JsonObj.Create(items[i], path, root.Where, problems);
            if (o is null)
            {
                continue;
            }

            int before = problems.Count;
            string? id = o.String("id");
            if (id is not null)
            {
                o.Where = root.Where with { Row = id };
                if (!Vocabulary.MethodIdPattern().IsMatch(id))
                {
                    problems.Add(o.Where, $"{o.Child("id")}: '{id}' is not a method id (lower case letters, digits and '-').");
                }
            }

            string? name = o.String("name");
            string? section = Cited(o, "section");
            string? location = Cited(o, "location");
            Length? cap = null;
            JsonElement? capElement = o.Get("cap");
            if (capElement is { ValueKind: not JsonValueKind.Null } c)
            {
                cap = JsonObj.ReadLength(c, o.Child("cap"), o.Where, problems);
                if (cap is { } capped && capped <= Length.Zero)
                {
                    problems.Add(o.Where, $"{o.Child("cap")}: a cap is longer than zero, or null for none.");
                }
            }

            (ColumnDomain? domain, List<MinimumPanelRow> rows) = ReadMinimumPanel(o, cite, problems);
            o.Done();
            if (id is not null && !ids.Add(id))
            {
                problems.Add(o.Where, $"{path}: method id '{id}' appears twice.");
            }

            if (problems.Count == before && id is not null && name is not null && section is not null && location is not null && domain is not null)
            {
                methods.Add(new BracingMethod(id, name, section, domain, rows.ToValueList(), cap, cite(location)));
            }
        }

        return methods;
    }

    private static (ColumnDomain? Domain, List<MinimumPanelRow> Rows) ReadMinimumPanel(JsonObj method, Func<string, SourceRef> cite, ProblemList problems)
    {
        List<MinimumPanelRow> rows = [];
        JsonObj? m = method.Obj("minimumPanel");
        if (m is null)
        {
            return (null, rows);
        }

        string path = method.Child("minimumPanel");
        int before = problems.Count;
        ColumnDomain? domain = ReadDomain(m, "wallHeightDomain", ColumnType.Length, path, problems);
        IReadOnlyList<JsonElement>? items = m.Array("rows", minItems: 1);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string rowPath = $"{path}.rows[{i}]";
            JsonObj? r = JsonObj.Create(items[i], rowPath, method.Where, problems);
            if (r is null)
            {
                continue;
            }

            string? id = r.String("id");
            string? location = r.String("location");
            Length? height = Read(r, "wallHeight");
            Length? length = Read(r, "length");
            r.Done();
            if (length is { } l && l <= Length.Zero)
            {
                problems.Add(method.Where, $"{rowPath}.length: a minimum panel length is longer than zero.");
            }
            else if (id is not null && location is not null && height is not null && length is not null)
            {
                rows.Add(new MinimumPanelRow(id, height.Value, length.Value, cite(location)));
            }
        }

        m.Done();
        if (problems.Count > before || domain is null)
        {
            return (null, rows);
        }

        foreach (MinimumPanelRow row in rows.Where(r => r.WallHeight.Units < domain.Min.Magnitude || r.WallHeight.Units > domain.Max.Magnitude))
        {
            problems.Add(method.Where, $"{path}: row '{row.Id}' wall height {CellValue.Of(row.WallHeight)} is outside the declared domain {domain.Min} to {domain.Max}.");
        }

        foreach (IGrouping<Length, MinimumPanelRow> same in rows.GroupBy(r => r.WallHeight).Where(g => g.Count() > 1))
        {
            problems.Add(method.Where, $"{path}: overlap: rows {string.Join(", ", same.Select(r => $"'{r.Id}'"))} have the same wall height {CellValue.Of(same.Key)}.");
        }

        foreach (IGrouping<string, MinimumPanelRow> same in rows.GroupBy(r => r.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            problems.Add(method.Where, $"{path}: row id '{same.Key}' appears twice.");
        }

        if (rows.Count > 0 && rows.Max(r => r.WallHeight.Units) != domain.Max.Magnitude)
        {
            problems.Add(method.Where, $"{path}: gap: the wall-height bands stop at {CellValue.Of(rows.Max(r => r.WallHeight))} but the domain declares max {domain.Max}.");
        }

        return problems.Count > before ? (null, rows) : (domain, [.. rows.OrderBy(r => r.WallHeight)]);

        Length? Read(JsonObj r, string name)
        {
            JsonElement? e = r.Get(name);
            return e is null ? null : JsonObj.ReadLength(e.Value, r.Child(name), r.Where, problems);
        }
    }

    private static List<Footnote> ReadFootnotes(JsonObj o, ProblemList problems)
    {
        List<Footnote> footnotes = [];
        IReadOnlyList<JsonElement>? items = o.Array("footnotes");
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = o.Child($"footnotes[{i}]");
            Footnote? footnote = TableReader.ReadFootnote(items[i], path, o.Where, [], problems);
            if (footnote is null)
            {
                continue;
            }

            if (footnote.EncodedAs is not (FootnoteEncoding.NotEncoded or FootnoteEncoding.AsRows))
            {
                problems.Add(o.Where, $"{path}.encodedAs: a bracing footnote is 'not-encoded' or 'as-rows'; limits and factors are declared in 'limits' and 'factors'.");
                continue;
            }

            if (footnote.AppliesTo != FootnoteScope.Table)
            {
                problems.Add(o.Where, $"{path}.appliesTo: a bracing footnote applies to the whole section ('table').");
                continue;
            }

            if (footnotes.Any(x => x.Id == footnote.Id))
            {
                problems.Add(o.Where, $"{path}.id: footnote '{footnote.Id}' is listed twice.");
                continue;
            }

            footnotes.Add(footnote);
        }

        return footnotes;
    }

    private static string Known() => string.Join(", ", Vocabulary.BracingInputs.Keys.Order(StringComparer.Ordinal));
}
