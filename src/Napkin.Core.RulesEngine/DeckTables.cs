using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>What a <c>member-span</c> table is for (docs/design/deck-and-porch.md §3.1).</summary>
public enum SpanUse
{
    /// <summary>Deck joists: supports, species, member, spacing.</summary>
    DeckJoist,

    /// <summary>A deck beam: species, member (plies and nominal), the joist span it carries.</summary>
    DeckBeam,

    /// <summary>Rafters: species, member, spacing, and whatever load the text bands on.</summary>
    Rafter,
}

/// <summary>One row of a deck table: its id, its value in each input column, its outputs and where it is printed.</summary>
/// <param name="Id">The row's id.</param>
/// <param name="Inputs">Its value in each input column.</param>
/// <param name="Span">A member-span row's allowed span; otherwise zero.</param>
/// <param name="Text">A ledger row's fastener, or a footing row's footing, as printed; otherwise empty.</param>
/// <param name="Spacing">A ledger row's fastener spacing; otherwise zero.</param>
/// <param name="Source">Where the row is printed.</param>
/// <param name="Footnotes">The ids of the footnotes the row lists.</param>
public sealed record DeckRow(
    string Id,
    ImmutableSortedDictionary<string, CellValue> Inputs,
    Length Span,
    string Text,
    Length Spacing,
    SourceRef Source,
    ValueList<string> Footnotes)
{
    /// <summary>
    /// A deck-joist row's allowed overhang past the beam (DCA 6's L_O, deck-guide-pack §3.1), when its table
    /// prints one; null otherwise. The cantilever may be the lesser of this and the table's
    /// <see cref="DeckTable.OverhangLimit"/> of the actual span.
    /// </summary>
    public Length? Overhang { get; init; }
}

/// <summary>
/// The cap a joist table puts on the overhang as a fraction of the joist span (deck-guide-pack §3.1):
/// DCA 6's "the lesser of allowable overhang, L_O, or one fourth the joist span, L/4". Declared once by
/// the table, cited, and applied exactly — never divided into a rounded length.
/// </summary>
/// <param name="Fraction">The fraction of the span, "1/4".</param>
/// <param name="Location">Where the rule is printed.</param>
public sealed record OverhangLimit(ExactFraction Fraction, string Location);

/// <summary>A deck table: a joist, beam or rafter span table, the ledger table, or the footing table.</summary>
/// <param name="Kind">The file's kind.</param>
/// <param name="Use">A member-span table's use; null for the others.</param>
/// <param name="Designation">The table's designation, as a citation prints it.</param>
/// <param name="Title">Its title as printed.</param>
/// <param name="Inputs">Its input columns.</param>
/// <param name="Rows">Its rows.</param>
/// <param name="Footnotes">Its footnotes: every one shown with the result, none encoded.</param>
/// <param name="Source">Where the table is printed.</param>
public sealed record DeckTable(
    string Kind,
    SpanUse? Use,
    string Designation,
    string Title,
    ValueList<InputColumn> Inputs,
    ValueList<DeckRow> Rows,
    ValueList<Footnote> Footnotes,
    SourceRef Source)
{
    /// <summary>The guide the table comes from (deck-guide-pack §1.2), or null for the base layer's own.</summary>
    public DeckGuide? Guide { get; init; }

    /// <summary>The table's own scope limits, tried after its guide's, before every lookup (deck-guide-pack §2).</summary>
    public ValueList<ScopeLimit> Limits { get; init; } = ValueList<ScopeLimit>.Empty;

    /// <summary>The species groups its rows carry (deck-guide-pack §3.6); empty when its rows name species.</summary>
    public ValueList<SpeciesGroup> SpeciesGroups { get; init; } = ValueList<SpeciesGroup>.Empty;

    /// <summary>The pack file it was read from, for a load problem that names two files.</summary>
    public string File { get; init; } = string.Empty;

    /// <summary>
    /// A deck-joist table's cap on the overhang as a fraction of the span, declared exactly when its rows
    /// carry <see cref="DeckRow.Overhang"/>; null for a table that does not cover an overhang.
    /// </summary>
    public OverhangLimit? OverhangLimit { get; init; }

    /// <summary>Which layer a citation names: the base layer's model code, or a guide.</summary>
    public CitationLayer Layer => Guide is null ? CitationLayer.ModelCode : CitationLayer.Guide;
}

/// <summary>A guard's provisions (§3.5); any item may be null — "not covered by this pack".</summary>
public sealed record GuardProvisions(Length? TriggerHeight, Length? MinimumHeight, Length? MaximumOpening, SourceRef Source);

/// <summary>A stair's provisions (§3.5); any item may be null.</summary>
public sealed record StairProvisions(
    Length? MaximumRiser, Length? MinimumTread, Length? MaximumRiserDifference, int? HandrailWhenRisersAtLeast, Length? MinimumWidth, SourceRef Source);

/// <summary>A pack's guard and stair provisions file (<c>kind: deck-guard-stair</c>).</summary>
public sealed record GuardStairProvisions(string Section, GuardProvisions? Guard, StairProvisions? Stair, ValueList<Footnote> Footnotes)
{
    /// <summary>The guide the provisions come from, or null for the base layer's own.</summary>
    public DeckGuide? Guide { get; init; }

    /// <summary>The pack file they were read from.</summary>
    public string File { get; init; } = string.Empty;
}

/// <summary>
/// A pack's frost line depth (<c>frost.json</c>, §3.4): offered to the person as a cited suggestion,
/// never applied. Its footnotes ride with the frost check, not-encoded.
/// </summary>
public sealed record FrostProvision(Length FrostLineDepth, SourceRef Source, ValueList<Footnote> Footnotes);

/// <summary>Everything a pack says about decks (§3): the tables by kind and use, and the provisions.</summary>
public sealed record DeckProvisions(
    ImmutableDictionary<SpanUse, DeckTable> Spans,
    DeckTable? Ledger,
    DeckTable? Footing,
    GuardStairProvisions? GuardStair)
{
    /// <summary>No deck tables at all.</summary>
    public static readonly DeckProvisions None = new(ImmutableDictionary<SpanUse, DeckTable>.Empty, null, null, null);

    /// <summary>Every deck table, in kind order: the span tables by use, the ledger, the footing.</summary>
    public IEnumerable<DeckTable> Tables
        => Spans.OrderBy(pair => pair.Key).Select(pair => pair.Value).Concat(new[] { Ledger, Footing }.OfType<DeckTable>());
}

/// <summary>
/// Reads a base layer's <c>deck/</c> files and a pack's <c>frost.json</c> (§3, #198): strict like
/// every pack file — an unknown kind, use, input, band or field, a dangling source, a table declared
/// twice, a gap or overlap in any band, and an encoded footnote are load problems, all collected.
/// </summary>
internal static class DeckReader
{
    public const string MemberSpanKind = "member-span";
    public const string LedgerKind = "deck-ledger";
    public const string FootingKind = "deck-footing";
    public const string GuardStairKind = "deck-guard-stair";
    public const string FrostKind = "frost";

    static readonly IReadOnlyDictionary<string, SpanUse> Uses = new Dictionary<string, SpanUse>(StringComparer.Ordinal)
    {
        ["deck-joist"] = SpanUse.DeckJoist,
        ["deck-beam"] = SpanUse.DeckBeam,
        ["rafter"] = SpanUse.Rafter,
    };

    /// <summary>The inputs each kind (and span use) may declare, with their type and band.</summary>
    static IReadOnlyDictionary<string, (ColumnType Type, BandKind Band)> Allowed(string kind, SpanUse? use) => (kind, use) switch
    {
        (MemberSpanKind, SpanUse.DeckJoist) => new Dictionary<string, (ColumnType, BandKind)>
        {
            ["supports"] = (ColumnType.Enum, BandKind.Exact),
            ["species"] = (ColumnType.Enum, BandKind.Exact),
            ["member"] = (ColumnType.Enum, BandKind.Exact),
            ["spacing"] = (ColumnType.Length, BandKind.Exact),
        },
        (MemberSpanKind, SpanUse.DeckBeam) => new Dictionary<string, (ColumnType, BandKind)>
        {
            ["supports"] = (ColumnType.Enum, BandKind.Exact),
            ["species"] = (ColumnType.Enum, BandKind.Exact),
            ["member"] = (ColumnType.Enum, BandKind.Exact),
            ["joistSpan"] = (ColumnType.Length, BandKind.UpperBound),
        },
        (MemberSpanKind, _) => new Dictionary<string, (ColumnType, BandKind)>
        {
            ["species"] = (ColumnType.Enum, BandKind.Exact),
            ["member"] = (ColumnType.Enum, BandKind.Exact),
            ["spacing"] = (ColumnType.Length, BandKind.Exact),
            ["groundSnowLoad"] = (ColumnType.Psf, BandKind.UpperBound),
            ["roofLiveLoad"] = (ColumnType.Psf, BandKind.UpperBound),
        },
        (LedgerKind, _) => new Dictionary<string, (ColumnType, BandKind)>
        {
            ["member"] = (ColumnType.Enum, BandKind.Exact),
            ["joistSpan"] = (ColumnType.Length, BandKind.UpperBound),
        },
        _ => new Dictionary<string, (ColumnType, BandKind)>
        {
            ["tributaryArea"] = (ColumnType.SquareFeet, BandKind.UpperBound),
            ["soilBearing"] = (ColumnType.Psf, BandKind.LowerBound),
        },
    };

    /// <summary>
    /// Reads a layer's <c>deck/</c> files: a base layer's (<paramref name="guide"/> null) or a guide's, whose
    /// tables then carry the guide and meet its species list (deck-guide-pack §1.2, §3.6).
    /// </summary>
    public static DeckProvisions Read(IPackSource source, string directory, IReadOnlyDictionary<string, SourceDocument> sources, ProblemList problems, DeckGuide? guide = null)
    {
        Dictionary<SpanUse, DeckTable> spans = [];
        DeckTable? ledger = null, footing = null;
        GuardStairProvisions? guardStair = null;
        foreach (string name in source.ListFiles(directory).Where(name => name.EndsWith(".json", StringComparison.Ordinal)))
        {
            string file = $"{directory}/{name}";
            JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
            using (document)
            {
                if (root is null)
                {
                    continue;
                }

                string? kind = root.String("kind");
                switch (kind)
                {
                    case MemberSpanKind or LedgerKind or FootingKind:
                        if (ReadTable(root, kind, sources, guide, problems) is not { } read)
                        {
                            break;
                        }

                        DeckTable table = read with { Guide = guide, File = file };
                        bool twice = kind switch
                        {
                            MemberSpanKind => !spans.TryAdd(table.Use!.Value, table),
                            LedgerKind => ledger is not null,
                            _ => footing is not null,
                        };
                        if (twice)
                        {
                            problems.Add(root.Where, $"a second {kind}{(table.Use is { } u ? $" table for '{UseName(u)}'" : " table")}: a layer has one of each.");
                        }
                        else if (kind == LedgerKind)
                        {
                            ledger = table;
                        }
                        else if (kind == FootingKind)
                        {
                            footing = table;
                        }

                        break;

                    case GuardStairKind:
                        if (guardStair is not null)
                        {
                            problems.Add(root.Where, "a second deck-guard-stair file: a layer has one.");
                            root.Done();
                            break;
                        }

                        guardStair = ReadGuardStair(root, sources, problems) is { } provisions ? provisions with { Guide = guide, File = file } : null;
                        break;

                    case null:
                        root.Done();
                        break;

                    default:
                        problems.Add(root.Where, $"kind: '{kind}' is not a deck file kind; they are: {MemberSpanKind}, {LedgerKind}, {FootingKind}, {GuardStairKind}.");
                        foreach (string field in root.Names.ToList())
                        {
                            root.MarkUsed(field);
                        }

                        break;
                }
            }
        }

        return new DeckProvisions(spans.ToImmutableDictionary(), ledger, footing, guardStair);
    }

    /// <summary>
    /// The base layer's deck provisions with a guide's added (deck-guide-pack §1.2). Precedence is none:
    /// a kind (and for a span table, a use) both declare is a load problem naming both files.
    /// </summary>
    public static DeckProvisions Merge(DeckProvisions into, DeckProvisions guide, Where where, ProblemList problems)
    {
        string Clash(string what, string first, string second)
            => $"{first} and {second} both declare {what}: a pack's layers may not both carry it, and neither wins; remove the guide's (or the pack's guides entry) when the adopted code's table arrives (deck-guide-pack §1.2).";

        Dictionary<SpanUse, DeckTable> spans = new(into.Spans);
        foreach ((SpanUse use, DeckTable table) in guide.Spans.OrderBy(pair => pair.Key))
        {
            if (!spans.TryAdd(use, table))
            {
                problems.Add(where, Clash($"a {MemberSpanKind} table for '{UseName(use)}'", spans[use].File, table.File));
            }
        }

        DeckTable? One(DeckTable? first, DeckTable? second, string kind)
        {
            if (first is not null && second is not null)
            {
                problems.Add(where, Clash($"a {kind} table", first.File, second.File));
            }

            return first ?? second;
        }

        if (into.GuardStair is { } a && guide.GuardStair is { } b)
        {
            problems.Add(where, Clash($"a {GuardStairKind} file", a.File, b.File));
        }

        return new DeckProvisions(
            spans.ToImmutableDictionary(),
            One(into.Ledger, guide.Ledger, LedgerKind),
            One(into.Footing, guide.Footing, FootingKind),
            into.GuardStair ?? guide.GuardStair);
    }

    public static string UseName(SpanUse use) => Uses.First(pair => pair.Value == use).Key;

    static DeckTable? ReadTable(JsonObj root, string kind, IReadOnlyDictionary<string, SourceDocument> sources, DeckGuide? guide, ProblemList problems)
    {
        int before = problems.Count;
        SpanUse? use = null;
        if (kind == MemberSpanKind)
        {
            use = root.Enum("use", Uses);
        }

        string? designation = root.String("table");
        if (designation is not null)
        {
            root.Where = new Where(root.Where.File, designation);
        }

        string? title = root.String("title");
        SourceDocument? doc = TableReader.ResolveSource(root.String("source"), sources, "source", root.Where, problems);
        string? location = root.String("location");
        List<InputColumn> inputs = kind == MemberSpanKind && use is null ? [] : ReadInputs(root, Allowed(kind, use), problems);
        List<Footnote> footnotes = ReadFootnotes(root, problems);

        // The table's own limits (deck-guide-pack §2), whose ids may not repeat its guide's.
        HashSet<string> ids = new(guide is null ? [] : guide.Limits.Select(limit => limit.Id).Concat(guide.Notes.Select(note => note.Id)), StringComparer.Ordinal);
        List<ScopeLimit> limits = ScopeReader.ReadLimits(root, "limits", required: false, ids, problems);
        List<SpeciesGroup> groups = ReadSpeciesGroups(root, problems);
        CheckSpeciesGroups(root.Where, inputs.FirstOrDefault(column => column.Name == "species"), groups, guide, problems);
        bool declaresCap = root.Has("overhangLimit");
        OverhangLimit? overhangLimit = ReadOverhangLimit(root, kind, use, problems);
        List<DeckRow> rows = [];
        IReadOnlyList<JsonElement>? items = root.Array("rows", minItems: 1);
        for (int i = 0; items is not null && doc is not null && i < items.Count; i++)
        {
            if (ReadRow(items[i], root.Child($"rows[{i}]"), root.Where, kind, use, inputs, footnotes, doc, problems) is { } row)
            {
                rows.Add(row);
            }
        }

        CheckOverhangs(root.Where, declaresCap, rows, problems);
        root.Done();
        if (problems.Count > before || designation is null || title is null || doc is null || location is null)
        {
            return null;
        }

        foreach (IGrouping<string, DeckRow> same in rows.GroupBy(row => row.Id).Where(group => group.Count() > 1))
        {
            problems.Add(root.Where, $"row id '{same.Key}' is used {same.Count()} times.");
        }

        BandValidator.Validate(root.Where, inputs, [.. rows.Select(row => new BandRow(row.Id, row.Inputs))], problems);
        return problems.Count > before
            ? null
            : new DeckTable(kind, use, designation, title, inputs.ToValueList(), rows.ToValueList(), footnotes.ToValueList(), TableTyper.SourceOf(doc, location))
            {
                Limits = limits.ToValueList(),
                SpeciesGroups = groups.ToValueList(),
                OverhangLimit = overhangLimit,
            };
    }

    /// <summary>
    /// A deck-joist table's <c>overhangLimit</c> (deck-guide-pack §3.1), <c>{ "fraction": "1/4", "of": "span",
    /// "location": … }</c>: the fraction a positive exact "n/d" of at most 1. No other table has one.
    /// </summary>
    static OverhangLimit? ReadOverhangLimit(JsonObj root, string kind, SpanUse? use, ProblemList problems)
    {
        if (!root.Has("overhangLimit"))
        {
            return null;
        }

        if (kind != MemberSpanKind || use != SpanUse.DeckJoist)
        {
            root.MarkUsed("overhangLimit");
            problems.Add(root.Where, "overhangLimit: only a deck-joist table's rows carry an overhang, so only it declares the overhang's cap.");
            return null;
        }

        if (root.Obj("overhangLimit") is not { } o)
        {
            return null;
        }

        int before = problems.Count;
        string? text = o.String("fraction");
        ExactFraction? fraction = text is null ? null : BracingReader.ParseFraction(text);
        if (text is not null && (fraction is not { } f || f.Numerator > f.Denominator))
        {
            problems.Add(o.Where, $"{o.Child("fraction")}: '{text}' is not a positive exact fraction of the span, written \"n/d\" with whole numbers and at most 1.");
        }

        string? of = o.String("of");
        if (of is not null && of != "span")
        {
            problems.Add(o.Where, $"{o.Child("of")}: '{of}': an overhang is capped as a fraction of the joist 'span', the one quantity napkin supplies.");
        }

        string? location = o.String("location");
        o.Done();
        return problems.Count > before || fraction is null || location is null ? null : new OverhangLimit(fraction.Value, location);
    }

    /// <summary>
    /// Overhangs are all or none: under an <c>overhangLimit</c> every row prints its overhang, and a row's
    /// overhang needs the table's cap, since the allowed overhang is the lesser of the two (§3.1).
    /// </summary>
    static void CheckOverhangs(Where where, bool declaresCap, List<DeckRow> rows, ProblemList problems)
    {
        List<string> without = [.. rows.Where(row => row.Overhang is null).Select(row => row.Id)];
        if (declaresCap && without.Count > 0)
        {
            problems.Add(where, $"rows {string.Join(", ", without)}: a table with an overhangLimit gives every row its overhang.");
        }
        else if (!declaresCap && without.Count < rows.Count)
        {
            problems.Add(where, "overhangLimit: the rows carry an overhang, so the table declares the cap on it as a fraction of the span (\"fraction\", \"of\": \"span\", \"location\").");
        }
    }

    /// <summary>A table's printed species groups (deck-guide-pack §3.6), each with its species and where it is printed.</summary>
    static List<SpeciesGroup> ReadSpeciesGroups(JsonObj root, ProblemList problems)
    {
        List<SpeciesGroup> groups = [];
        IReadOnlyList<JsonElement>? items = root.Array("speciesGroups", required: false);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = root.Child($"speciesGroups[{i}]");
            JsonObj? g = JsonObj.Create(items[i], path, root.Where, problems);
            if (g is null)
            {
                continue;
            }

            int before = problems.Count;
            string? name = g.String("group");
            List<string> species = [];
            IReadOnlyList<JsonElement>? listed = g.Array("species", minItems: 1);
            for (int j = 0; listed is not null && j < listed.Count; j++)
            {
                if (JsonObj.ReadString(listed[j], $"{path}.species[{j}]", g.Where, problems) is { } one)
                {
                    species.Add(one);
                }
            }

            string? location = g.String("location");
            g.Done();
            if (name is not null && groups.Any(group => group.Group == name))
            {
                problems.Add(g.Where, $"{path}.group: '{name}' is declared twice.");
            }
            else if (problems.Count == before && name is not null && location is not null)
            {
                groups.Add(new SpeciesGroup(name, species.ToValueList(), location));
            }
        }

        return groups;
    }

    /// <summary>
    /// The groups name exactly the species column's values, place each species once, and under a guide
    /// place every species the guide covers and no other; a table under a guide with a species column
    /// must declare its groups (deck-guide-pack §3.6).
    /// </summary>
    static void CheckSpeciesGroups(Where where, InputColumn? column, List<SpeciesGroup> groups, DeckGuide? guide, ProblemList problems)
    {
        if (column is null)
        {
            if (groups.Count > 0)
            {
                problems.Add(where, "speciesGroups: the table has no 'species' column to group.");
            }

            return;
        }

        if (groups.Count == 0)
        {
            if (guide is not null)
            {
                problems.Add(where, $"speciesGroups: a table under guide '{guide.Id}' with a 'species' column declares the species groups it prints (deck-guide-pack §3.6).");
            }

            return;
        }

        foreach (string value in column.Values.Where(value => groups.All(group => group.Group != value)))
        {
            problems.Add(where, $"speciesGroups: the species column's value '{value}' is not one of the table's groups.");
        }

        foreach (SpeciesGroup group in groups.Where(group => !column.Values.Contains(group.Group)))
        {
            problems.Add(where, $"speciesGroups: group '{group.Group}' is not one of the species column's values.");
        }

        foreach (IGrouping<string, SpeciesGroup> twice in groups.SelectMany(group => group.Species.Select(species => (species, group))).GroupBy(pair => pair.species, pair => pair.group).Where(same => same.Count() > 1))
        {
            problems.Add(where, $"speciesGroups: '{twice.Key}' is placed in {string.Join(" and ", twice.Select(group => $"'{group.Group}'"))}; a species is in one group of a table.");
        }

        if (guide is null)
        {
            return;
        }

        foreach (string species in groups.SelectMany(group => group.Species).Distinct().Where(species => !guide.Species.Contains(species)))
        {
            problems.Add(where, $"speciesGroups: '{species}' is not one of guide '{guide.Id}''s species.");
        }

        foreach (string species in guide.Species.Where(species => groups.All(group => !group.Species.Contains(species))))
        {
            problems.Add(where, $"speciesGroups: guide '{guide.Id}' covers '{species}' but no group of this table places it; every species the guide covers is placed.");
        }
    }

    static List<InputColumn> ReadInputs(JsonObj o, IReadOnlyDictionary<string, (ColumnType Type, BandKind Band)> allowed, ProblemList problems)
    {
        List<InputColumn> columns = [];
        IReadOnlyList<JsonElement>? items = o.Array("inputs", minItems: 1);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = o.Child($"inputs[{i}]");
            JsonObj? c = JsonObj.Create(items[i], path, o.Where, problems);
            if (c is null)
            {
                continue;
            }

            int before = problems.Count;
            string? name = c.String("name");
            ColumnType? type = c.Enum("type", Vocabulary.ColumnTypes);
            BandKind? band = c.Enum("band", Vocabulary.BandKinds);
            List<string> values = [];
            ColumnDomain? domain = null;
            if (name is not null && !allowed.ContainsKey(name))
            {
                problems.Add(c.Where, $"{path}.name: '{name}' is not an input this table can be asked; they are: {string.Join(", ", allowed.Keys.Order(StringComparer.Ordinal))}.");
            }
            else if (name is not null && type is { } t && band is { } b)
            {
                (ColumnType expectedType, BandKind expectedBand) = allowed[name];
                if (t != expectedType)
                {
                    problems.Add(c.Where, $"{path}.type: '{name}' is '{Vocabulary.TypeName(expectedType)}', not '{Vocabulary.TypeName(t)}'.");
                }

                if (b != expectedBand)
                {
                    problems.Add(c.Where, $"{path}.band: '{name}' uses '{Vocabulary.BandName(expectedBand)}' bands, not '{Vocabulary.BandName(b)}'.");
                }

                if (expectedType == ColumnType.Enum)
                {
                    IReadOnlyList<JsonElement>? listed = c.Array("values", minItems: 1);
                    for (int j = 0; listed is not null && j < listed.Count; j++)
                    {
                        string? value = JsonObj.ReadString(listed[j], $"{path}.values[{j}]", c.Where, problems);
                        if (value is not null && values.Contains(value, StringComparer.Ordinal))
                        {
                            problems.Add(c.Where, $"{path}.values[{j}]: '{value}' is listed twice.");
                        }
                        else if (value is not null)
                        {
                            values.Add(value);
                        }
                    }
                }
                else if (expectedBand != BandKind.Exact && c.Obj("domain") is { } d)
                {
                    CellValue? min = TableReader.ReadCell(d.Get("min"), expectedType, $"{path}.domain.min", c.Where, problems);
                    CellValue? max = TableReader.ReadCell(d.Get("max"), expectedType, $"{path}.domain.max", c.Where, problems);
                    d.Done();
                    if (min is { } lo && max is { } hi)
                    {
                        if (lo.Magnitude > hi.Magnitude)
                        {
                            problems.Add(c.Where, $"{path}.domain: min {lo} is above max {hi}.");
                        }
                        else
                        {
                            domain = new ColumnDomain(lo, hi);
                        }
                    }
                }
            }

            c.Done();
            if (problems.Count > before || name is null || type is null || band is null)
            {
                continue;
            }

            if (columns.Any(column => column.Name == name))
            {
                problems.Add(o.Where, $"{path}.name: '{name}' is declared twice.");
                continue;
            }

            columns.Add(new InputColumn(name, type.Value, band.Value, values.ToValueList(), domain));
        }

        return columns;
    }

    static List<Footnote> ReadFootnotes(JsonObj o, ProblemList problems)
    {
        List<Footnote> footnotes = [];
        IReadOnlyList<JsonElement>? items = o.Array("footnotes", required: false);
        for (int i = 0; items is not null && i < items.Count; i++)
        {
            string path = o.Child($"footnotes[{i}]");
            Footnote? footnote = TableReader.ReadFootnote(items[i], path, o.Where, [], problems);
            if (footnote is null)
            {
                continue;
            }

            if (footnote.EncodedAs != FootnoteEncoding.NotEncoded)
            {
                problems.Add(o.Where, $"{path}.encodedAs: a deck file's footnotes are shown with the result, 'not-encoded'; this one is '{Vocabulary.Encodings.First(pair => pair.Value == footnote.EncodedAs).Key}'.");
            }
            else if (footnotes.Any(other => other.Id == footnote.Id))
            {
                problems.Add(o.Where, $"{path}.id: footnote '{footnote.Id}' is declared twice.");
            }
            else
            {
                footnotes.Add(footnote);
            }
        }

        return footnotes;
    }

    static DeckRow? ReadRow(
        JsonElement element, string path, Where where, string kind, SpanUse? use, List<InputColumn> inputs, List<Footnote> footnotes, SourceDocument doc, ProblemList problems)
    {
        JsonObj? r = JsonObj.Create(element, path, where, problems);
        if (r is null)
        {
            return null;
        }

        int before = problems.Count;
        string? id = r.String("id");
        r.Where = where with { Row = id };
        ImmutableSortedDictionary<string, CellValue>.Builder cells = ImmutableSortedDictionary.CreateBuilder<string, CellValue>(StringComparer.Ordinal);
        foreach (InputColumn column in inputs)
        {
            if (TableReader.ReadCell(r.Get(column.Name), column.Type, r.Child(column.Name), r.Where, problems) is not { } cell)
            {
                continue;
            }

            if (column.Type == ColumnType.Enum && !column.Values.Contains(cell.Symbol!))
            {
                problems.Add(r.Where, $"{r.Child(column.Name)}: '{cell.Symbol}' is not one of the column's values ({string.Join(", ", column.Values)}).");
            }
            else if (column.Domain is { } domain && (cell.Magnitude < domain.Min.Magnitude || cell.Magnitude > domain.Max.Magnitude))
            {
                problems.Add(r.Where, $"{r.Child(column.Name)}: {cell} is outside the column's domain {domain.Min} to {domain.Max}.");
            }
            else
            {
                cells.Add(column.Name, cell);
            }
        }

        Length span = Length.Zero, spacing = Length.Zero;
        Length? overhang = null;
        string text = string.Empty;
        switch (kind)
        {
            case MemberSpanKind:
                span = Positive(r, "span", problems) ?? Length.Zero;
                if (r.Has("overhang") && use != SpanUse.DeckJoist)
                {
                    r.MarkUsed("overhang");
                    problems.Add(r.Where, $"{r.Child("overhang")}: only a deck-joist row carries an overhang.");
                }
                else if (r.Has("overhang"))
                {
                    overhang = Positive(r, "overhang", problems);
                }

                break;
            case LedgerKind:
                text = r.String("fastener") ?? string.Empty;
                spacing = Positive(r, "spacing", problems) ?? Length.Zero;
                break;
            default:
                text = r.String("footing") ?? string.Empty;
                break;
        }

        string? location = r.String("location");
        List<string> listed = [];
        IReadOnlyList<JsonElement>? notes = r.Array("footnotes", required: false);
        for (int i = 0; notes is not null && i < notes.Count; i++)
        {
            string? note = JsonObj.ReadString(notes[i], r.Child($"footnotes[{i}]"), r.Where, problems);
            if (note is not null && footnotes.All(footnote => footnote.Id != note))
            {
                problems.Add(r.Where, $"{r.Child($"footnotes[{i}]")}: '{note}' is not one of the table's footnotes.");
            }
            else if (note is not null)
            {
                listed.Add(note);
            }
        }

        r.Done();
        return problems.Count > before || id is null || location is null
            ? null
            : new DeckRow(id, cells.ToImmutable(), span, text, spacing, TableTyper.SourceOf(doc, location), listed.ToValueList()) { Overhang = overhang };
    }

    static Length? Positive(JsonObj o, string name, ProblemList problems)
    {
        if (o.Get(name) is not { } element || JsonObj.ReadLength(element, o.Child(name), o.Where, problems) is not { } length)
        {
            return null;
        }

        if (length <= Length.Zero)
        {
            problems.Add(o.Where, $"{o.Child(name)}: must be longer than zero.");
            return null;
        }

        return length;
    }

    static Length? OptionalLength(JsonObj o, string name, ProblemList problems)
    {
        JsonElement? element = o.Get(name);
        if (element is not { } e || e.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        Length? length = JsonObj.ReadLength(e, o.Child(name), o.Where, problems);
        if (length is { } l && l <= Length.Zero)
        {
            problems.Add(o.Where, $"{o.Child(name)}: must be longer than zero, or null when this pack does not cover it.");
            return null;
        }

        return length;
    }

    static GuardStairProvisions? ReadGuardStair(JsonObj root, IReadOnlyDictionary<string, SourceDocument> sources, ProblemList problems)
    {
        int before = problems.Count;
        string? section = root.String("section");
        SourceDocument? doc = TableReader.ResolveSource(root.String("source"), sources, "source", root.Where, problems);
        root.String("location");
        GuardProvisions? guard = null;
        StairProvisions? stair = null;
        if (root.Get("guard") is { ValueKind: not JsonValueKind.Null } && root.Obj("guard") is { } g)
        {
            Length? trigger = OptionalLength(g, "triggerHeight", problems), height = OptionalLength(g, "minimumHeight", problems), opening = OptionalLength(g, "maximumOpening", problems);
            string? at = g.String("location");
            g.Done();
            guard = doc is not null && at is not null ? new GuardProvisions(trigger, height, opening, TableTyper.SourceOf(doc, at)) : null;
        }

        if (root.Get("stair") is { ValueKind: not JsonValueKind.Null } && root.Obj("stair") is { } s)
        {
            Length? riser = OptionalLength(s, "maximumRiser", problems), tread = OptionalLength(s, "minimumTread", problems);
            Length? difference = OptionalLength(s, "maximumRiserDifference", problems), width = OptionalLength(s, "minimumWidth", problems);
            int? handrail = null;
            if (s.Get("handrailWhenRisersAtLeast") is { ValueKind: not JsonValueKind.Null } h)
            {
                handrail = JsonObj.ReadInt(h, s.Child("handrailWhenRisersAtLeast"), s.Where, problems, min: 1);
            }

            string? at = s.String("location");
            s.Done();
            stair = doc is not null && at is not null ? new StairProvisions(riser, tread, difference, handrail, width, TableTyper.SourceOf(doc, at)) : null;
        }

        List<Footnote> footnotes = ReadFootnotes(root, problems);
        root.Done();
        return problems.Count > before || section is null || doc is null ? null : new GuardStairProvisions(section, guard, stair, footnotes.ToValueList());
    }

    /// <summary>A pack's <c>frost.json</c>.</summary>
    public static FrostProvision? ReadFrost(IPackSource source, string file, IReadOnlyDictionary<string, SourceDocument> sources, ProblemList problems)
    {
        JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            int before = problems.Count;
            string? kind = root.String("kind");
            if (kind is not null && kind != FrostKind)
            {
                problems.Add(root.Where, $"kind: frost.json is '{FrostKind}', not '{kind}'.");
            }

            root.String("notes", required: false);
            SourceDocument? doc = TableReader.ResolveSource(root.String("source"), sources, "source", root.Where, problems);
            string? location = root.String("location");
            Length? depth = root.Get("frostLineDepth") is { } e ? JsonObj.ReadLength(e, "frostLineDepth", root.Where, problems) : null;
            if (depth is { } d && d <= Length.Zero)
            {
                problems.Add(root.Where, "frostLineDepth: must be deeper than zero.");
            }

            List<Footnote> footnotes = ReadFootnotes(root, problems);
            root.Done();
            return problems.Count > before || doc is null || location is null || depth is null
                ? null
                : new FrostProvision(depth.Value, TableTyper.SourceOf(doc, location), footnotes.ToValueList());
        }
    }
}
