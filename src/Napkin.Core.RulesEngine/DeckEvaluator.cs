using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// A deck check's result (docs/design/deck-and-porch.md §3): exactly one of passes, short, sized, out
/// of scope, input missing or no data, each cited where a table answered.
/// </summary>
public abstract record DeckResult
{
    private DeckResult()
    {
    }

    /// <summary>The span is within the row's allowed span.</summary>
    public sealed record Passes(DeckTable Table, DeckRow Row, Length Allowed, Length Actual) : DeckResult;

    /// <summary>The span is past the row's allowed span, by <see cref="Over"/>.</summary>
    public sealed record Short(DeckTable Table, DeckRow Row, Length Allowed, Length Actual) : DeckResult
    {
        /// <summary>How far over.</summary>
        public Length Over => Actual - Allowed;
    }

    /// <summary>A ledger or footing row answered: its words as printed, the ledger's spacing and napkin's fastener count.</summary>
    public sealed record Sized(DeckTable Table, DeckRow Row, int? Count) : DeckResult;

    /// <summary>The request is outside what the table covers, and the table says so.</summary>
    public sealed record OutOfScope(DeckTable Table, string Explanation) : DeckResult;

    /// <summary>An input the table bands on is not entered.</summary>
    public sealed record InputMissing(string Input, string Explanation) : DeckResult;

    /// <summary>No adopted code, or a pack without the table.</summary>
    public sealed record NoData(string Explanation) : DeckResult;
}

/// <summary>What a span check asks (§3.1): the typed member and the actual span, and every input a table may band on.</summary>
/// <param name="Member">The member as the table names it: "2x8", or "(2) 2x10" for a beam.</param>
/// <param name="ActualSpan">The span from the drawing.</param>
/// <param name="Supports">What the deck supports, or null when not entered.</param>
/// <param name="Species">The species, or null when not entered.</param>
/// <param name="Spacing">The spacing on centre, for joists and rafters.</param>
/// <param name="JoistSpan">The joist span a beam carries.</param>
/// <param name="GroundSnowLoad">The site's ground snow load, psf, or null.</param>
/// <param name="RoofLiveLoad">The site's roof live load, psf, or null.</param>
public sealed record SpanRequest(
    string Member, Length ActualSpan, string? Supports, string? Species, Length? Spacing, Length? JoistSpan, int? GroundSnowLoad = null, int? RoofLiveLoad = null);

/// <summary>
/// The deck checks' lookups (§3.1–§3.3): a banded lookup in the adopted pack's deck tables, strictly
/// as the table declares its columns — exact, upper-bound, lower-bound — and never a guess.
/// </summary>
public static class DeckEvaluator
{
    /// <summary>What each span use is called in a sentence.</summary>
    public static string Words(SpanUse use) => use switch
    {
        SpanUse.DeckJoist => "deck joist span",
        SpanUse.DeckBeam => "deck beam span",
        _ => "rafter span",
    };

    /// <summary>A joist, beam or rafter checked against its span table.</summary>
    public static DeckResult CheckSpan(LoadedPack? pack, SpanUse use, SpanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (NoTable(pack, pack?.Deck.Spans.GetValueOrDefault(use), Words(use)) is { } none)
        {
            return none;
        }

        DeckTable table = pack!.Deck.Spans[use];
        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["supports"] = request.Supports is { } s ? (s, null) : null,
            ["species"] = request.Species is { } sp ? (sp, null) : null,
            ["member"] = (request.Member, null),
            ["spacing"] = request.Spacing is { } g ? (null, ExactFraction.Whole(g.Units)) : null,
            ["joistSpan"] = request.JoistSpan is { } j ? (null, ExactFraction.Whole(j.Units)) : null,
            ["groundSnowLoad"] = request.GroundSnowLoad is { } snow ? (null, ExactFraction.Whole(snow)) : null,
            ["roofLiveLoad"] = request.RoofLiveLoad is { } live ? (null, ExactFraction.Whole(live)) : null,
        };

        return Lookup(table, inputs) switch
        {
            (DeckRow row, _) when request.ActualSpan <= row.Span => new DeckResult.Passes(table, row, row.Span, request.ActualSpan),
            (DeckRow row, _) => new DeckResult.Short(table, row, row.Span, request.ActualSpan),
            (_, DeckResult other) => other,
        };
    }

    /// <summary>The ledger's fastening (§3.2), and napkin's count for a ledger of this length: ⌈length ÷ spacing⌉ + 1.</summary>
    public static DeckResult SizeLedger(LoadedPack? pack, string member, Length joistSpan, Length ledgerLength)
    {
        if (NoTable(pack, pack?.Deck.Ledger, "deck ledger table") is { } none)
        {
            return none;
        }

        DeckTable table = pack!.Deck.Ledger!;
        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["member"] = (member, null),
            ["joistSpan"] = (null, ExactFraction.Whole(joistSpan.Units)),
        };

        return Lookup(table, inputs) switch
        {
            (DeckRow row, _) => new DeckResult.Sized(table, row, (int)((ledgerLength.Units + row.Spacing.Units - 1) / row.Spacing.Units) + 1),
            (_, DeckResult other) => other,
        };
    }

    /// <summary>The footing (§3.3) for a post's tributary area, in square 1/1024″, and the site's soil bearing value.</summary>
    public static DeckResult SizeFooting(LoadedPack? pack, ExactFraction tributaryArea, int? soilBearing)
    {
        if (NoTable(pack, pack?.Deck.Footing, "deck footing table") is { } none)
        {
            return none;
        }

        DeckTable table = pack!.Deck.Footing!;
        Int128 perFoot = (Int128)Length.UnitsPerFoot * Length.UnitsPerFoot;
        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["tributaryArea"] = (null, new ExactFraction(tributaryArea.Numerator, tributaryArea.Denominator * perFoot)),
            ["soilBearing"] = soilBearing is { } psf ? (null, ExactFraction.Whole(psf)) : null,
        };

        return Lookup(table, inputs) switch
        {
            (DeckRow row, _) => new DeckResult.Sized(table, row, null),
            (_, DeckResult other) => other,
        };
    }

    /// <summary>No code chosen, or a pack without the table: the honest no-data sentence.</summary>
    static DeckResult.NoData? NoTable(LoadedPack? pack, DeckTable? table, string what)
    {
        if (pack is null)
        {
            return new DeckResult.NoData("No adopted code is chosen, so napkin cannot check this. Choose one in Project → Adopted code and site.");
        }

        return table is null
            ? new DeckResult.NoData(
                $"The loaded pack {pack.Manifest.Adoption.ShortName} has no {what}, so napkin cannot check this. Nothing is guessed: add it from your copy of the code (docs/rules-engine.md).")
            : null;
    }

    /// <summary>
    /// The one row the request selects, or why none: an input not entered, a category or exact value the
    /// table has no row for, or a banded value past the table's last band (upper-bound) or below its first
    /// (lower-bound).
    /// </summary>
    internal static (DeckRow? Row, DeckResult? Result) Lookup(DeckTable table, IReadOnlyDictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs)
    {
        IEnumerable<DeckRow> rows = table.Rows;
        foreach (InputColumn column in table.Inputs)
        {
            if (inputs.GetValueOrDefault(column.Name) is not { } value)
            {
                return (null, new DeckResult.InputMissing(column.Name, $"Enter the {Spoken(column.Name)}: table {table.Designation} bands on it."));
            }

            List<DeckRow> left = [.. rows];
            string Shown() => value.Symbol ?? new CellValue(column.Type, null, (long)(value.Magnitude!.Value.Numerator / value.Magnitude.Value.Denominator)).ToString();
            switch (column.Band)
            {
                case BandKind.Exact:
                    rows = left.Where(row => column.Type == ColumnType.Enum
                        ? row.Inputs[column.Name].Symbol == value.Symbol
                        : ExactFraction.Whole(row.Inputs[column.Name].Magnitude).CompareTo(value.Magnitude!.Value) == 0).ToList();
                    if (!rows.Any())
                    {
                        return (null, new DeckResult.OutOfScope(table, $"Table {table.Designation} has no row for {Spoken(column.Name)} {Shown()}: get it engineered."));
                    }

                    break;

                case BandKind.UpperBound:
                {
                    List<DeckRow> covering = [.. left.Where(row => ExactFraction.Whole(row.Inputs[column.Name].Magnitude) >= value.Magnitude!.Value)];
                    if (covering.Count == 0)
                    {
                        return (null, new DeckResult.OutOfScope(table, $"The {Spoken(column.Name)} {Shown()} is past the last band of table {table.Designation} ({column.Domain!.Max}): get it engineered."));
                    }

                    long bound = covering.Min(row => row.Inputs[column.Name].Magnitude);
                    rows = covering.Where(row => row.Inputs[column.Name].Magnitude == bound).ToList();
                    break;
                }

                default:
                {
                    List<DeckRow> covering = [.. left.Where(row => ExactFraction.Whole(row.Inputs[column.Name].Magnitude) <= value.Magnitude!.Value)];
                    if (covering.Count == 0)
                    {
                        return (null, new DeckResult.OutOfScope(table, $"The {Spoken(column.Name)} {Shown()} is below the lowest band of table {table.Designation} ({column.Domain!.Min}): get it engineered."));
                    }

                    long bound = covering.Max(row => row.Inputs[column.Name].Magnitude);
                    rows = covering.Where(row => row.Inputs[column.Name].Magnitude == bound).ToList();
                    break;
                }
            }
        }

        return (rows.Single(), null);
    }

    /// <summary>An input's name in a sentence.</summary>
    public static string Spoken(string input) => input switch
    {
        "supports" => "what the deck supports",
        "joistSpan" => "joist span",
        "groundSnowLoad" => "ground snow load",
        "roofLiveLoad" => "roof live load",
        "tributaryArea" => "tributary area",
        "soilBearing" => "soil bearing value",
        _ => input,
    };
}
