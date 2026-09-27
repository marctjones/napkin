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

    /// <summary>
    /// For a post or footing lookup, the tributary area it was asked with and, when the table's centre-post factor
    /// applied, the factor and the area looked up (deck-guide-pack §3.4); null for the other lookups.
    /// </summary>
    public AreaAsked? Area { get; init; }

    /// <summary>The span is within the row's allowed span.</summary>
    /// <param name="Code">The adopted code it was computed under.</param>
    /// <param name="Table">The table.</param>
    /// <param name="Row">The row that answered.</param>
    /// <param name="Allowed">The row's allowed span.</param>
    /// <param name="Actual">The span asked about.</param>
    /// <param name="Group">The species group the typed species was read as, when the table groups species; otherwise null.</param>
    public sealed record Passes(AdoptedCodeRef Code, DeckTable Table, DeckRow Row, Length Allowed, Length Actual, SpeciesGroup? Group) : DeckResult;

    /// <summary>The span is past the row's allowed span, by <see cref="Over"/>.</summary>
    /// <param name="Code">The adopted code it was computed under.</param>
    /// <param name="Table">The table.</param>
    /// <param name="Row">The row that answered.</param>
    /// <param name="Allowed">The row's allowed span.</param>
    /// <param name="Actual">The span asked about.</param>
    /// <param name="Group">The species group the typed species was read as, or null.</param>
    public sealed record Short(AdoptedCodeRef Code, DeckTable Table, DeckRow Row, Length Allowed, Length Actual, SpeciesGroup? Group) : DeckResult
    {
        /// <summary>How far over.</summary>
        public Length Over => Actual - Allowed;
    }

    /// <summary>A ledger or footing row answered: its words as printed, the ledger's spacing and napkin's fastener count.</summary>
    public sealed record Sized(AdoptedCodeRef Code, DeckTable Table, DeckRow Row, int? Count) : DeckResult;

    /// <summary>
    /// The request is outside what the table covers, and the table (or its guide) says so: a scope
    /// <paramref name="Limit"/> that held, or the <paramref name="Column"/> whose value no row covers.
    /// </summary>
    public sealed record OutOfScope(AdoptedCodeRef Code, DeckTable Table, string Explanation, ScopeLimit? Limit, string? Column) : DeckResult
    {
        /// <summary>The row that answered with no value — a post table's cell printed NP (deck-guide-pack §3.4) — or null.</summary>
        public DeckRow? Row { get; init; }
    }

    /// <summary>An input the table bands on, or a scope limit tests, is not entered.</summary>
    public sealed record InputMissing(string Input, string Explanation) : DeckResult;

    /// <summary>No adopted code, or a pack without the table.</summary>
    public sealed record NoData(string Explanation) : DeckResult;
}

/// <summary>
/// The facts about the deck itself that a guide's scope limits may test (deck-guide-pack §2), for a
/// lookup that does not carry them in its own request: what it supports, its species, the site's ground
/// snow load, and its length out from the house and width along it (Figure 5's definitions).
/// </summary>
public sealed record DeckScopeInputs(string? Supports, string? Species, int? GroundSnowLoad, Length? DeckLength, Length? DeckWidth)
{
    /// <summary>Nothing entered.</summary>
    public static readonly DeckScopeInputs NotEntered = new(null, null, null, null, null);
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
/// <param name="DeckLength">The deck's length out from the house, for a guide's scope; null when not known.</param>
/// <param name="DeckWidth">The deck's width along the house, for a guide's scope; null when not known.</param>
public sealed record SpanRequest(
    string Member,
    Length ActualSpan,
    string? Supports,
    string? Species,
    Length? Spacing,
    Length? JoistSpan,
    int? GroundSnowLoad = null,
    int? RoofLiveLoad = null,
    Length? DeckLength = null,
    Length? DeckWidth = null);

/// <summary>
/// A post's tributary area as a post-height or footing lookup asks it (deck-guide-pack §3.4): the area in square
/// 1/1024″, exact, where the post stands, and whether the beam over it is continuous (not spliced), which decides
/// whether a table's centre-post factor applies.
/// </summary>
/// <param name="Area">The tributary area, Eq. B-1 or Eq. B-2 of DCA 6 Appendix B for napkin's deck, in square 1/1024″.</param>
/// <param name="Position">A corner post or a centre post.</param>
/// <param name="ContinuousBeam">Whether the beam runs over the post unspliced.</param>
public sealed record PostArea(ExactFraction Area, PostPosition Position, bool ContinuousBeam);

/// <summary>The tributary area a post or footing lookup was asked with, and what it looked up.</summary>
/// <param name="Area">The area asked, in square 1/1024″.</param>
/// <param name="Factor">The table's centre-post factor when it applied (a centre post under a continuous beam); otherwise null.</param>
/// <param name="Looked">The area the table was read at: <paramref name="Area"/> × the factor, or the area itself.</param>
public sealed record AreaAsked(ExactFraction Area, CenterPostFactor? Factor, ExactFraction Looked);

/// <summary>What a post-height check asks (deck-guide-pack §3.4): the post as the table names it, its height, the species and its area.</summary>
/// <param name="Post">The post, "4x4" or "6x6".</param>
/// <param name="Height">The post's height, measured as the table's guide measures it (DCA 6 p. 10: grade to the beam's underside).</param>
/// <param name="Species">The species, or null when not entered.</param>
/// <param name="Area">Its tributary area and position; the position picks the table.</param>
public sealed record PostRequest(string Post, Length Height, string? Species, PostArea Area);

/// <summary>
/// The deck checks' lookups (§3.1–§3.3): a banded lookup in the adopted pack's deck tables, strictly
/// as the table declares its columns — exact, upper-bound, lower-bound — and never a guess. Before every
/// lookup the table's guide's scope limits and then its own are tried (deck-guide-pack §2), and a typed
/// species is read as the group the table prints (§3.6).
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
        (DeckTable? table, DeckRow? row, SpeciesGroup? group, DeckResult? other) = SpanRow(pack, use, request, null);
        if (other is not null)
        {
            return other;
        }

        return request.ActualSpan <= row!.Span
            ? new DeckResult.Passes(pack!.Code, table!, row, row.Span, request.ActualSpan, group)
            : new DeckResult.Short(pack!.Code, table!, row, row.Span, request.ActualSpan, group);
    }

    /// <summary>
    /// The joists' cantilever past the beam (deck-guide-pack §3.1), checked against the row the joists answer
    /// from: allowed up to the lesser of the row's overhang and the table's fraction of the actual span
    /// (<see cref="SpanRequest.ActualSpan"/>), compared exactly. The scope, the species group and the lookup
    /// are the span check's; a joist table that prints no overhang does not cover one, and says so.
    /// </summary>
    public static DeckResult CheckCantilever(LoadedPack? pack, SpanRequest joists, Length cantilever)
    {
        ArgumentNullException.ThrowIfNull(joists);
        (DeckTable? table, DeckRow? row, SpeciesGroup? group, DeckResult? other) = SpanRow(pack, SpanUse.DeckJoist, joists, cantilever);
        if (other is not null)
        {
            return other;
        }

        Length allowed = AllowedOverhang(table!, row!, joists.ActualSpan);
        return cantilever <= allowed
            ? new DeckResult.Passes(pack!.Code, table!, row!, allowed, cantilever, group)
            : new DeckResult.Short(pack!.Code, table!, row!, allowed, cantilever, group);
    }

    /// <summary>
    /// The overhang a row allows on this span: the lesser of the row's own and the table's fraction of the span,
    /// the latter rounded down to 1/1024″ — exact for the verdict, since a cantilever is whole 1/1024″ units.
    /// </summary>
    public static Length AllowedOverhang(DeckTable table, DeckRow row, Length span)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(row);
        if (table.OverhangLimit is not { } cap || row.Overhang is not { } own)
        {
            throw new ArgumentException($"table {table.Designation} does not cover an overhang.", nameof(table));
        }

        Length share = new((long)(span.Units * cap.Fraction.Numerator / cap.Fraction.Denominator));
        return share < own ? share : own;
    }

    /// <summary>
    /// The row a span table answers from, or why none: no table, a scope limit, an unplaced species, the
    /// lookup's own answer — and for a cantilever, a table that does not cover an overhang.
    /// </summary>
    static (DeckTable? Table, DeckRow? Row, SpeciesGroup? Group, DeckResult? Result) SpanRow(LoadedPack? pack, SpanUse use, SpanRequest request, Length? cantilever)
    {
        if (NoTable(pack, pack?.Deck.Spans.GetValueOrDefault(use), Words(use)) is { } none)
        {
            return (null, null, null, none);
        }

        DeckTable table = pack!.Deck.Spans[use];
        DeckScopeInputs scope = new(request.Supports, request.Species, request.GroundSnowLoad, request.DeckLength, request.DeckWidth);
        if (Scope(pack.Code, table, scope, request.Member) is { } stopped)
        {
            return (null, null, null, stopped);
        }

        if (cantilever is { } past && table.OverhangLimit is null)
        {
            return (null, null, null, new DeckResult.OutOfScope(
                pack.Code,
                table,
                $"Table {table.Designation} does not cover an overhang, so a cantilever of {CellValue.Of(past)} past the beam is not checked: get it engineered.",
                null,
                "cantilever"));
        }

        (SpeciesGroup? group, DeckResult? unplaced) = Group(pack.Code, table, request.Species);
        if (unplaced is not null)
        {
            return (null, null, null, unplaced);
        }

        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["supports"] = request.Supports is { } s ? (s, null) : null,
            ["species"] = request.Species is { } sp ? (group?.Group ?? sp, null) : null,
            ["member"] = (request.Member, null),
            ["spacing"] = request.Spacing is { } g ? (null, ExactFraction.Whole(g.Units)) : null,
            ["joistSpan"] = request.JoistSpan is { } j ? (null, ExactFraction.Whole(j.Units)) : null,
            ["groundSnowLoad"] = request.GroundSnowLoad is { } snow ? (null, ExactFraction.Whole(snow)) : null,
            ["roofLiveLoad"] = request.RoofLiveLoad is { } live ? (null, ExactFraction.Whole(live)) : null,
        };

        return Lookup(pack.Code, table, inputs) switch
        {
            (DeckRow row, _) => (table, row, group, null),
            (_, DeckResult other) => (null, null, null, other),
        };
    }

    /// <summary>
    /// The ledger's fastening (§3.2), and napkin's count for a ledger of this length: ⌈length ÷ spacing⌉ + 1.
    /// <paramref name="deck"/> carries what a guide's scope may test; null means nothing entered.
    /// </summary>
    public static DeckResult SizeLedger(LoadedPack? pack, string member, Length joistSpan, Length ledgerLength, DeckScopeInputs? deck = null)
    {
        if (NoTable(pack, pack?.Deck.Ledger, "deck ledger table") is { } none)
        {
            return none;
        }

        DeckTable table = pack!.Deck.Ledger!;
        if (Scope(pack.Code, table, deck ?? DeckScopeInputs.NotEntered, member) is { } stopped)
        {
            return stopped;
        }

        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["member"] = (member, null),
            ["joistSpan"] = (null, ExactFraction.Whole(joistSpan.Units)),
        };

        return Lookup(pack.Code, table, inputs) switch
        {
            (DeckRow row, _) => new DeckResult.Sized(pack.Code, table, row, (int)((ledgerLength.Units + row.Spacing.Units - 1) / row.Spacing.Units) + 1),
            (_, DeckResult other) => other,
        };
    }

    /// <summary>
    /// The footing (§3.3, deck-guide-pack §3.4) for a post's tributary area and the site's soil bearing value: the area
    /// multiplied by the table's centre-post factor for a centre post under a continuous beam, then the row. The result
    /// carries the area asked and looked up. <paramref name="deck"/> carries what a guide's scope may test; null means
    /// nothing entered.
    /// </summary>
    public static DeckResult SizeFooting(LoadedPack? pack, PostArea area, int? soilBearing, DeckScopeInputs? deck = null)
    {
        ArgumentNullException.ThrowIfNull(area);
        if (NoTable(pack, pack?.Deck.Footing, "deck footing table") is { } none)
        {
            return none;
        }

        DeckTable table = pack!.Deck.Footing!;
        if (Scope(pack.Code, table, deck ?? DeckScopeInputs.NotEntered, null) is { } stopped)
        {
            return stopped;
        }

        AreaAsked asked = Factored(table, area);
        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["tributaryArea"] = (null, SquareFeet(asked.Looked)),
            ["soilBearing"] = soilBearing is { } psf ? (null, ExactFraction.Whole(psf)) : null,
        };

        return Lookup(pack.Code, table, inputs) switch
        {
            (DeckRow row, _) => new DeckResult.Sized(pack.Code, table, row, null) { Area = asked },
            (_, DeckResult other) => other with { Area = asked },
        };
    }

    /// <summary>
    /// A post's height against the post table for its position (deck-guide-pack §3.4): the guide's scope, the species read
    /// as the table's group, the area multiplied by the table's centre-post factor when it applies, then the row — passes
    /// or short against the row's height, or Out of scope citing a row that prints NP. <paramref name="deck"/> carries
    /// what a guide's scope may test; null means nothing entered.
    /// </summary>
    public static DeckResult CheckPost(LoadedPack? pack, PostRequest request, DeckScopeInputs? deck = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        PostPosition position = request.Area.Position;
        string what = position == PostPosition.Corner ? "corner post table" : "centre post table";
        if (NoTable(pack, pack?.Deck.Posts.GetValueOrDefault(position), what) is { } none)
        {
            return none;
        }

        DeckTable table = pack!.Deck.Posts[position];
        if (Scope(pack.Code, table, deck ?? DeckScopeInputs.NotEntered, request.Post) is { } stopped)
        {
            return stopped;
        }

        (SpeciesGroup? group, DeckResult? unplaced) = Group(pack.Code, table, request.Species);
        if (unplaced is not null)
        {
            return unplaced;
        }

        AreaAsked asked = Factored(table, request.Area);
        Dictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs = new(StringComparer.Ordinal)
        {
            ["species"] = request.Species is { } sp ? (group?.Group ?? sp, null) : null,
            ["post"] = (request.Post, null),
            ["tributaryArea"] = (null, SquareFeet(asked.Looked)),
        };

        (DeckRow? found, DeckResult? other) = Lookup(pack.Code, table, inputs);
        if (other is not null)
        {
            return other with { Area = asked };
        }

        DeckRow row = found!;
        if (row.NotPermitted)
        {
            string area = new CellValue(ColumnType.SquareFeet, null, row.Inputs["tributaryArea"].Magnitude).ToString();
            return new DeckResult.OutOfScope(
                pack.Code,
                table,
                $"Table {table.Designation} prints NP, no height, for a {request.Post} post of {group?.Group ?? request.Species} carrying up to {area} (row {row.Id}, {row.Source.Location}): get it engineered.",
                null,
                null)
            {
                Row = row,
                Area = asked,
            };
        }

        Length allowed = row.Height!.Value;
        return request.Height <= allowed
            ? new DeckResult.Passes(pack.Code, table, row, allowed, request.Height, group) { Area = asked }
            : new DeckResult.Short(pack.Code, table, row, allowed, request.Height, group) { Area = asked };
    }

    /// <summary>
    /// The area a post or footing table is read at (deck-guide-pack §3.4): multiplied, exactly, by the table's centre-post
    /// factor when the post is a centre post under a continuous beam and the table declares one; otherwise the area.
    /// </summary>
    static AreaAsked Factored(DeckTable table, PostArea area)
        => table.CenterPostFactor is { } factor && area.Position == PostPosition.Center && area.ContinuousBeam
            ? new AreaAsked(area.Area, factor, new ExactFraction(area.Area.Numerator * factor.Multiply.Numerator, area.Area.Denominator * factor.Multiply.Denominator))
            : new AreaAsked(area.Area, null, area.Area);

    /// <summary>Square 1/1024″ as square feet, exact.</summary>
    static ExactFraction SquareFeet(ExactFraction squareUnits)
        => new(squareUnits.Numerator, squareUnits.Denominator * Length.UnitsPerFoot * Length.UnitsPerFoot);

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
    /// The table's guide's scope limits and then its own, in order (deck-guide-pack §2): the first whose
    /// input is not entered is Input missing, naming it; the first that holds is Out of scope, citing it;
    /// none holding is null, and the lookup goes on. <paramref name="member"/> is the lookup's own member,
    /// null for a footing.
    /// </summary>
    internal static DeckResult? Scope(AdoptedCodeRef code, DeckTable table, DeckScopeInputs deck, string? member)
    {
        CellValue? Value(string input) => input switch
        {
            "supports" => deck.Supports is { } s ? CellValue.Category(s) : null,
            "species" => deck.Species is { } sp ? CellValue.Category(sp) : null,
            "member" => member is { } m ? CellValue.Category(m) : null,
            "groundSnowLoad" => deck.GroundSnowLoad is { } snow ? CellValue.Whole(ColumnType.Psf, snow) : null,
            "deckLength" => deck.DeckLength is { } l ? CellValue.Of(l) : null,
            _ => deck.DeckWidth is { } w ? CellValue.Of(w) : null,
        };

        // A guide's limit is "beyond the scope of DCA 6-2015"; a table's own, "beyond table X". Either is
        // cited with the guide's name before its location when the table comes from a guide.
        string cite = table.Guide is { } guide ? $"{guide.ShortName} " : string.Empty;
        IEnumerable<(ScopeLimit Limit, string Beyond, string Named)> limits = (table.Guide?.Limits ?? ValueList<ScopeLimit>.Empty)
            .Select(limit => (limit, $"the scope of {table.Guide!.ShortName}", $"{table.Guide.ShortName} scope limit {limit.Id}"))
            .Concat(table.Limits.Select(limit => (limit, $"table {table.Designation}", $"table {table.Designation} limit {limit.Id}")));
        foreach ((ScopeLimit limit, string beyond, string named) in limits)
        {
            ScopeCondition when = limit.When;
            foreach (string input in new[] { when.Input, when.OtherInput }.OfType<string>())
            {
                if (Value(input) is null)
                {
                    return new DeckResult.InputMissing(input, $"{Enter(input)}: {named} ({cite}{limit.Location}) depends on it.");
                }
            }

            CellValue value = Value(when.Input)!.Value;
            bool holds = when.Form switch
            {
                ScopeForm.Above => value.Magnitude > when.Value!.Value.Magnitude,
                ScopeForm.AboveInput => value.Magnitude > Value(when.OtherInput!)!.Value.Magnitude,
                ScopeForm.EqualTo or ScopeForm.In => when.Values.Contains(value.Symbol!),
                _ => !when.Values.Contains(value.Symbol!),
            };
            if (holds)
            {
                return new DeckResult.OutOfScope(code, table, $"Beyond {beyond}: \"{limit.Text}\" ({cite}{limit.Location}). Get it engineered.", limit, null);
            }
        }

        return null;
    }

    /// <summary>
    /// The printed group a typed species is read as (deck-guide-pack §3.6): none when the table does not
    /// group species or none is entered; out of scope when no group places it.
    /// </summary>
    static (SpeciesGroup? Group, DeckResult? Unplaced) Group(AdoptedCodeRef code, DeckTable table, string? species)
    {
        if (table.SpeciesGroups.Count == 0 || species is null)
        {
            return (null, null);
        }

        return table.SpeciesGroups.FirstOrDefault(group => group.Species.Contains(species)) is { } found
            ? (found, null)
            : (null, new DeckResult.OutOfScope(
                code,
                table,
                $"Table {table.Designation} places no species {species} in its groups ({string.Join("; ", table.SpeciesGroups.Select(group => group.Group))}): get it engineered.",
                null,
                "species"));
    }

    /// <summary>
    /// The one row the request selects, or why none: an input not entered, a category or exact value the
    /// table has no row for, or a banded value past the table's last band (upper-bound) or below its first
    /// (lower-bound).
    /// </summary>
    internal static (DeckRow? Row, DeckResult? Result) Lookup(AdoptedCodeRef code, DeckTable table, IReadOnlyDictionary<string, (string? Symbol, ExactFraction? Magnitude)?> inputs)
    {
        IEnumerable<DeckRow> rows = table.Rows;
        foreach (InputColumn column in table.Inputs)
        {
            if (inputs.GetValueOrDefault(column.Name) is not { } value)
            {
                return (null, new DeckResult.InputMissing(column.Name, $"{Enter(column.Name)}: table {table.Designation} bands on it."));
            }

            List<DeckRow> left = [.. rows];
            string Shown() => value.Symbol ?? new CellValue(column.Type, null, (long)(value.Magnitude!.Value.Numerator / value.Magnitude.Value.Denominator)).ToString();
            DeckResult.OutOfScope Stop(string explanation) => new(code, table, explanation, null, column.Name);
            switch (column.Band)
            {
                case BandKind.Exact:
                    rows = left.Where(row => column.Type == ColumnType.Enum
                        ? row.Inputs[column.Name].Symbol == value.Symbol
                        : ExactFraction.Whole(row.Inputs[column.Name].Magnitude).CompareTo(value.Magnitude!.Value) == 0).ToList();
                    if (!rows.Any())
                    {
                        return (null, Stop($"Table {table.Designation} has no row for {Spoken(column.Name)} {Shown()}: get it engineered."));
                    }

                    break;

                case BandKind.UpperBound:
                {
                    List<DeckRow> covering = [.. left.Where(row => ExactFraction.Whole(row.Inputs[column.Name].Magnitude) >= value.Magnitude!.Value)];
                    if (covering.Count == 0)
                    {
                        return (null, Stop($"The {Spoken(column.Name)} {Shown()} is past the last band of table {table.Designation} ({column.Domain!.Max}): get it engineered."));
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
                        return (null, Stop($"The {Spoken(column.Name)} {Shown()} is below the lowest band of table {table.Designation} ({column.Domain!.Min}): get it engineered."));
                    }

                    long bound = covering.Max(row => row.Inputs[column.Name].Magnitude);
                    rows = covering.Where(row => row.Inputs[column.Name].Magnitude == bound).ToList();
                    break;
                }
            }
        }

        return (rows.Single(), null);
    }

    /// <summary>
    /// "Enter the species", "Enter what the deck supports"; the soil bearing value, never defaulted, says where it is typed
    /// and where it comes from (#42).
    /// </summary>
    static string Enter(string input) => input switch
    {
        "supports" => $"Enter {Spoken(input)}",
        "soilBearing" => "Enter the site's soil bearing value, from the building department or a soils report, in Project → Adopted code and site",
        _ => $"Enter the {Spoken(input)}",
    };

    /// <summary>An input's name in a sentence.</summary>
    public static string Spoken(string input) => input switch
    {
        "supports" => "what the deck supports",
        "joistSpan" => "joist span",
        "groundSnowLoad" => "ground snow load",
        "roofLiveLoad" => "roof live load",
        "tributaryArea" => "tributary area",
        "soilBearing" => "soil bearing value",
        "deckLength" => "deck's length out from the house",
        "deckWidth" => "deck's width along the house",
        _ => input,
    };
}
