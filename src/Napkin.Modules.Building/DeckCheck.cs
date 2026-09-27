using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>What a deck check is about.</summary>
public enum DeckCheckKind
{
    /// <summary>The joists' span.</summary>
    Joists,

    /// <summary>The beam's span between posts.</summary>
    Beam,

    /// <summary>The ledger's fastening to the house.</summary>
    Ledger,

    /// <summary>The footing under the most loaded post.</summary>
    Footing,

    /// <summary>The footings' depth against the frost line.</summary>
    Frost,

    /// <summary>The guard: whether one is required, its height and its openings (§3.5, §4.1).</summary>
    Guard,

    /// <summary>The stair: its risers, treads, handrail and width (§3.5, §4.2).</summary>
    Stair,
}

/// <summary>One line of a deck's code check: what it is about, the engine's result (none for frost, which is napkin's comparison), and the sentence.</summary>
/// <param name="Kind">What the line checks.</param>
/// <param name="Result">The engine's result, or null for the frost comparison.</param>
/// <param name="Text">The sentence the panel shows.</param>
/// <param name="Passing">Whether the line passes or is sized; false for short, out of scope, missing or no data.</param>
public sealed record DeckCheckLine(DeckCheckKind Kind, DeckResult? Result, string Text, bool Passing);

/// <summary>A cited frost depth the adopted code offers, for the person to accept into the site value (§3.4).</summary>
/// <param name="Depth">The frost line depth.</param>
/// <param name="Text">"CT 2022 says 3'-6" (TABLE R301.2 …) — use it?"</param>
public sealed record FrostSuggestion(Length Depth, string Text);

/// <summary>A deck's checks: its frame (or why none), its lines, what napkin needs said, and a frost suggestion.</summary>
/// <param name="Deck">The deck.</param>
/// <param name="Framing">Its frame, or null when it has none.</param>
/// <param name="Refusal">Why it has no frame, or null.</param>
/// <param name="Lines">The check lines, in §3's order.</param>
/// <param name="SupportsNote">A bearing wall stands on the deck and nothing says what the deck supports; otherwise null.</param>
/// <param name="Frost">A frost depth the adopted code offers, when it differs from the site's; otherwise null.</param>
/// <param name="Guides">
/// For each guide the pack declares, the paragraph shown once at the top of the block: what the guide is
/// and is not, its caveats and its scope (deck-guide-pack §1.2, Decision 5); empty without a guide.
/// </param>
public sealed record DeckChecks(
    Deck Deck, DeckFraming? Framing, DeckRefusal? Refusal, ImmutableArray<DeckCheckLine> Lines, string? SupportsNote, FrostSuggestion? Frost, ImmutableArray<string> Guides);

/// <summary>
/// Every deck's code checks (docs/design/deck-and-porch.md §3): a lookup in the adopted pack for each,
/// each result exactly one honest state with its citation, nothing stored and nothing guessed. The
/// frost line is napkin's comparison of two typed values; the pack only offers its depth.
/// </summary>
public static class DeckCheck
{
    static readonly LengthFormat Words = new FeetInchesFormat(16);

    /// <summary>The checks for every deck in the sketch as it will be.</summary>
    public static ImmutableArray<DeckChecks> Of(Sketch sketch, CodePacks packs, MaterialsLibrary? library = null)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        Sketch after = sketch.After();
        LoadedPack? pack = packs.Resolve(after.Code).Pack;
        return [.. Deck.All(after).Select(deck => For(after, deck, pack, library ?? MaterialsLibrary.Shipped))];
    }

    /// <summary>One deck's checks under a pack (null when no code is chosen or it does not resolve).</summary>
    public static DeckChecks For(Sketch sketch, Deck deck, LoadedPack? pack, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(deck);
        (DeckFraming? framing, DeckRefusal? refusal) = DeckFrame.Of(sketch, deck, library);
        FrostSuggestion? frost = pack?.Frost is { } offered && sketch.Site.FrostDepth != offered.FrostLineDepth
            ? new FrostSuggestion(offered.FrostLineDepth, $"{pack.Manifest.Adoption.ShortName} says {Text(offered.FrostLineDepth)} ({offered.Source.Location}) — use it?")
            : null;
        ImmutableArray<string> guides = pack is null ? [] : [.. pack.Guides.Select(guide => guide.Paragraph(pack.Code))];
        if (framing is null)
        {
            return new DeckChecks(deck, null, refusal, [], null, frost, guides);
        }

        DeckInputs inputs = deck.Box.Deck!;
        string member = inputs.Joist;
        string spacing = inputs.JoistSpacing.Format(new InchesOnlyFormat(16)).Text;
        List<DeckCheckLine> lines = [];

        // What a guide's scope may test (deck-guide-pack §2): Figure 5's length out from the house and
        // width along it, the site's ground snow load, what the deck supports and its species.
        DeckScopeInputs scope = new(inputs.Supports, inputs.Species, sketch.Site.GroundSnowLoadPsf, framing.Depth, framing.Width);
        DeckResult joists = DeckEvaluator.CheckSpan(
            pack, SpanUse.DeckJoist, new SpanRequest(member, framing.JoistSpan, inputs.Supports, inputs.Species, inputs.JoistSpacing, null, scope.GroundSnowLoad, null, scope.DeckLength, scope.DeckWidth));
        lines.Add(Span(DeckCheckKind.Joists, joists, $"Joists {member} at {spacing} o.c.{Species(inputs)}, span {Text(framing.JoistSpan)}", "Use a deeper joist, closer spacing or another beam."));

        // The beam's clear span is exact but may fall between grid points: the table is asked about
        // the span rounded up, never down, and the sentence says ≈ when it was.
        Length beamSpan = new((long)((framing.BeamSpan.Numerator + framing.BeamSpan.Denominator - 1) / framing.BeamSpan.Denominator));
        string beamMember = $"({inputs.Beam.Plies}) {inputs.Beam.Lumber}";
        DeckResult beam = DeckEvaluator.CheckSpan(
            pack, SpanUse.DeckBeam, new SpanRequest(beamMember, beamSpan, inputs.Supports, inputs.Species, null, framing.JoistSpan, scope.GroundSnowLoad, null, scope.DeckLength, scope.DeckWidth));
        lines.Add(Span(DeckCheckKind.Beam, beam, $"Beam {beamMember} on {inputs.PostCount} posts, span {framing.BeamSpanText} carrying {Text(framing.JoistSpan)} of joists", "Add a post, or use a deeper beam."));

        DeckResult ledger = DeckEvaluator.SizeLedger(pack, member, framing.JoistSpan, framing.Width, scope);
        lines.Add(ledger is DeckResult.Sized sized
            ? new DeckCheckLine(
                DeckCheckKind.Ledger,
                ledger,
                $"Ledger to the house: {sized.Row.Text}, {Text(sized.Row.Spacing)} on centre ({Cited(sized.Code, sized.Table, sized.Row)}); {sized.Count} fasteners for a {Text(framing.Width)} ledger "
                + $"(⌈{Text(framing.Width)} ÷ {Text(sized.Row.Spacing)}⌉ + 1, napkin's count).{sized.Code.UnreviewedSentence}{Notes(sized.Table, sized.Row)}",
                true)
            : Other(DeckCheckKind.Ledger, ledger, "Ledger"));

        // The most loaded post: a middle one when there are three or more, otherwise an end post, which
        // carries half a span.
        bool middle = inputs.PostCount >= 3;
        ExactFraction area = middle ? framing.TributaryArea : new ExactFraction(framing.TributaryArea.Numerator, framing.TributaryArea.Denominator * 2);
        string which = middle ? "a middle post" : "an end post";
        DeckResult footing = DeckEvaluator.SizeFooting(pack, area, sketch.Site.SoilBearingPsf, scope);
        lines.Add(footing is DeckResult.Sized foot
            ? new DeckCheckLine(
                DeckCheckKind.Footing,
                footing,
                $"Footings: {foot.Row.Text} for {which}'s {DeckFrame.SquareFeet(area)} on {sketch.Site.SoilBearingPsf} psf ({Cited(foot.Code, foot.Table, foot.Row)}).{foot.Code.UnreviewedSentence}{Notes(foot.Table, foot.Row)}",
                true)
            : Other(DeckCheckKind.Footing, footing, $"Footings ({which}, {DeckFrame.SquareFeet(area)})"));

        lines.Add(FrostLine(sketch, inputs, pack));
        lines.AddRange(GuardLines(sketch, framing, pack, library));
        lines.AddRange(StairLines(framing, pack, library));
        return new DeckChecks(deck, framing, null, [.. lines], SupportsNote(sketch, deck, inputs), frost, guides);
    }

    /// <summary>
    /// The engine's diff over the deck lookups (joists, beam, ledger, footing) of the decks present both
    /// before and after (design §7.3, deck-guide-pack §6 slice A).
    /// </summary>
    public static DeckRecomputeReport Report(IReadOnlyList<DeckChecks> before, IReadOnlyList<DeckChecks> after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        List<KeyValuePair<DeckCheckKey, DeckResult>> was = [.. Results(before)], now = [.. Results(after)];
        HashSet<DeckCheckKey> both = [.. was.Select(pair => pair.Key).Intersect(now.Select(pair => pair.Key))];
        return Recompute.DiffDeck([.. was.Where(pair => both.Contains(pair.Key))], [.. now.Where(pair => both.Contains(pair.Key))]);

        static IEnumerable<KeyValuePair<DeckCheckKey, DeckResult>> Results(IEnumerable<DeckChecks> decks)
            => decks.SelectMany(checks => checks.Lines.Where(line => line.Result is not null)
                .Select(line => KeyValuePair.Create(new DeckCheckKey(checks.Deck.Id, line.Kind.ToString()), line.Result!)));
    }

    /// <summary>
    /// What a recompute changed on the decks, one sentence per lookup whose answer differs, newly flagged
    /// first: "Deck 1, can no longer be checked: Joists 2x8 …: The loaded pack CT 2022 has no deck joist span …".
    /// A span that moved within its row is not a change of the answer, and is not said.
    /// </summary>
    public static ImmutableArray<string> Changes(IReadOnlyList<DeckChecks> before, IReadOnlyList<DeckChecks> after)
        => [.. Report(before, after).Changes.Where(change => change.Kind is not (DeckChangeKind.SpanMoved or DeckChangeKind.NoAnswerChanged)).Select(change => Sentence(after, change))];

    static string Sentence(IReadOnlyList<DeckChecks> after, DeckChange change)
    {
        DeckChecks checks = after.First(deck => deck.Deck.Id == change.Key.Element);
        string said = change.Kind switch
        {
            DeckChangeKind.PassToShort or DeckChangeKind.ToShort or DeckChangeKind.ToOutOfScope => "newly flagged",
            DeckChangeKind.ToNoAnswer => "can no longer be checked",
            DeckChangeKind.ShortToPass or DeckChangeKind.ToAnswer => "now answered",
            _ => "changed",
        };
        return $"{checks.Deck.Name}, {said}: {checks.Lines.First(line => line.Kind.ToString() == change.Key.Check).Text}";
    }

    /// <summary>The footing depth against the site's frost depth: two typed values, compared exactly.</summary>
    static DeckCheckLine FrostLine(Sketch sketch, DeckInputs inputs, LoadedPack? pack)
    {
        string notes = pack?.Frost is { } provision ? string.Concat(provision.Footnotes.Select(note => $" Note {note.Id}: {note.Text}")) : string.Empty;
        if (inputs.FootingDepth is not { } footing)
        {
            return new DeckCheckLine(DeckCheckKind.Frost, null, "Frost: enter how deep the footings go below grade in the deck panel.", false);
        }

        if (sketch.Site.FrostDepth is not { } frost)
        {
            return new DeckCheckLine(DeckCheckKind.Frost, null, "Frost: enter the site's frost depth (Project → Adopted code and site).", false);
        }

        string source = sketch.Site.Source is { } from ? $" (site value, {from.Text})" : " (site value)";
        return footing >= frost
            ? new DeckCheckLine(DeckCheckKind.Frost, null, $"Frost: footings {Text(footing)} below grade; frost line {Text(frost)}{source}.{notes}", true)
            : new DeckCheckLine(DeckCheckKind.Frost, null, $"Frost: footings {Text(footing)} below grade, {Text(frost - footing)} short of the frost line {Text(frost)}{source}.{notes}", false);
    }

    /// <summary>The guard's lines (§3.5): required, its height, its openings — each against the pack's provisions, cited, or not covered.</summary>
    static IEnumerable<DeckCheckLine> GuardLines(Sketch sketch, DeckFraming framing, LoadedPack? pack, MaterialsLibrary library)
    {
        GuardStairProvisions? provisions = pack?.Deck.GuardStair;
        DeckInputs inputs = framing.Deck.Box.Deck!;
        int open = framing.Deck.OpenEdges(sketch).Length;
        if (provisions?.Guard is not { } guard)
        {
            if (inputs.Guard is not null || open > 0)
            {
                string why = pack is null ? "no adopted code is chosen" : $"the loaded pack {pack.Manifest.Adoption.ShortName} has no guard provisions";
                yield return new DeckCheckLine(DeckCheckKind.Guard, null, $"Guard: {why}, so napkin cannot say whether one is required. Nothing is guessed.", false);
            }

            yield break;
        }

        string cite = Provisions(pack!, provisions, guard.Source);
        string mark = pack!.Code.UnreviewedSentence;
        if (guard.TriggerHeight is { } trigger)
        {
            bool required = framing.Deck.Height > trigger && open > 0;
            string need = required
                ? $"Guard required: the deck is {Text(framing.Deck.Height)} above grade, over {Text(trigger)}, with {open} open {(open == 1 ? "edge" : "edges")} ({cite})"
                : $"No guard required: {(open == 0 ? "no edge is open" : $"the deck is {Text(framing.Deck.Height)} above grade, not over {Text(trigger)}")} ({cite})";
            yield return new DeckCheckLine(
                DeckCheckKind.Guard,
                null,
                (required && inputs.Guard is null ? $"{need}: add a guard in the panel." : need + ".") + mark,
                !required || inputs.Guard is not null);
        }
        else
        {
            yield return new DeckCheckLine(DeckCheckKind.Guard, null, $"When a guard is required is not covered by this pack ({cite}).", false);
        }

        if (inputs.Guard is not { } typed)
        {
            yield break;
        }

        if (guard.MinimumHeight is { } minimum)
        {
            yield return typed.Height >= minimum
                ? new DeckCheckLine(DeckCheckKind.Guard, null, $"Guard height {Text(typed.Height)}: at least {Text(minimum)} ({cite}).{mark}", true)
                : new DeckCheckLine(DeckCheckKind.Guard, null, $"Guard height {Text(typed.Height)}: {Text(minimum - typed.Height)} short of the {Text(minimum)} required ({cite}).{mark}", false);
        }

        if (guard.MaximumOpening is { } opening && GuardFraming.Of(sketch, framing, library) is { } layout)
        {
            ExactFraction widest = layout.Runs.Select(run => run.Gap).Max();
            if (ExactFraction.Whole(typed.BottomClearance.Units) > widest)
            {
                widest = ExactFraction.Whole(typed.BottomClearance.Units);
            }

            string gaps = string.Join(", ", layout.Runs.Select(run => GuardFraming.Words(run.Gap)).Distinct());
            yield return widest <= ExactFraction.Whole(opening.Units)
                ? new DeckCheckLine(DeckCheckKind.Guard, null, $"Guard openings: baluster gaps {gaps} and {Text(typed.BottomClearance)} under the rail, none over {Text(opening)} ({cite}).{mark}", true)
                : new DeckCheckLine(DeckCheckKind.Guard, null, $"Guard openings: the widest is {GuardFraming.Words(widest)}, over the {Text(opening)} allowed ({cite}).{mark}", false);
        }
    }

    /// <summary>The stair's lines (§3.5): riser, tread, handrail and width, each against the pack's provisions, cited.</summary>
    static IEnumerable<DeckCheckLine> StairLines(DeckFraming framing, LoadedPack? pack, MaterialsLibrary library)
    {
        DeckInputs inputs = framing.Deck.Box.Deck!;
        if (inputs.Stair is not { } stair)
        {
            yield break;
        }

        (StairLayout? layout, string? problem) = StairFraming.Of(framing, pack, library);
        if (layout is null)
        {
            yield return new DeckCheckLine(DeckCheckKind.Stair, null, $"Stair: {problem}", false);
            yield break;
        }

        yield return new DeckCheckLine(DeckCheckKind.Stair, null, $"Stair: {layout.Layout}", true);
        if (pack?.Deck.GuardStair?.Stair is not { } rules)
        {
            yield return new DeckCheckLine(
                DeckCheckKind.Stair,
                null,
                $"Stair: {(pack is null ? "no adopted code is chosen" : $"the loaded pack {pack.Manifest.Adoption.ShortName} has no stair provisions")}, so napkin cannot check the risers, treads or handrail. Nothing is guessed.",
                false);
            yield break;
        }

        string cite = Provisions(pack, pack.Deck.GuardStair!, rules.Source);
        string mark = pack.Code.UnreviewedSentence;
        if (rules.MaximumRiser is { } riser)
        {
            yield return layout.RiseEach <= ExactFraction.Whole(riser.Units)
                ? new DeckCheckLine(DeckCheckKind.Stair, null, $"Risers {DeckFrame.Words(layout.RiseEach)}: at most {Text(riser)} ({cite}).{mark}", true)
                : new DeckCheckLine(DeckCheckKind.Stair, null, $"Risers {DeckFrame.Words(layout.RiseEach)}: over the {Text(riser)} allowed ({cite}).{mark}", false);
        }

        if (rules.MinimumTread is { } tread)
        {
            yield return stair.Run >= tread
                ? new DeckCheckLine(DeckCheckKind.Stair, null, $"Treads {Text(stair.Run)}: at least {Text(tread)} ({cite}).{mark}", true)
                : new DeckCheckLine(DeckCheckKind.Stair, null, $"Treads {Text(stair.Run)}: {Text(tread - stair.Run)} short of the {Text(tread)} required ({cite}).{mark}", false);
        }

        if (rules.HandrailWhenRisersAtLeast is { } handrail)
        {
            yield return new DeckCheckLine(
                DeckCheckKind.Stair,
                null,
                layout.Risers >= handrail
                    ? $"Handrail required: {layout.Risers} risers, at least {handrail} ({cite}); add it as hardware.{mark}"
                    : $"No handrail required: {layout.Risers} risers, fewer than {handrail} ({cite}).{mark}",
                true);
        }

        if (rules.MinimumWidth is { } width)
        {
            yield return stair.Width >= width
                ? new DeckCheckLine(DeckCheckKind.Stair, null, $"Stair width {Text(stair.Width)}: at least {Text(width)} ({cite}).{mark}", true)
                : new DeckCheckLine(DeckCheckKind.Stair, null, $"Stair width {Text(stair.Width)}: {Text(width - stair.Width)} short of the {Text(width)} required ({cite}).{mark}", false);
        }
    }

    /// <summary>
    /// "ZZ-GUARD.1, synthetic p. 7 guard": the provisions' section and where the item is printed, and from a
    /// guide the clause that says what the guide is not (deck-guide-pack §1.2).
    /// </summary>
    static string Provisions(LoadedPack pack, GuardStairProvisions provisions, SourceRef source)
        => $"{provisions.Section}, {source.Location}" + (provisions.Guide is { } guide ? $" — {guide.Clause(pack.Code)}" : string.Empty);

    /// <summary>§5.1: a bearing wall stands on the deck and nothing says what the deck supports.</summary>
    static string? SupportsNote(Sketch sketch, Deck deck, DeckInputs inputs)
    {
        if (inputs.Supports is not null)
        {
            return null;
        }

        // Only asked of a deck that framed, so it stands level and has an outline.
        var outline = deck.Outline!.Value;

        Wall? bearing = Wall.All(sketch).FirstOrDefault(wall =>
            wall.Box.WallInputs?.Bearing == true
            && wall.Box.Anchor.Z == deck.Box.Anchor.Z + deck.Height
            && Deck.Bounds(wall.Box) is { } w
            && outline.West <= w.West && w.East <= outline.East && outline.South <= w.South && w.North <= outline.North);
        return bearing is null ? null : $"{bearing.Name} (bearing) stands on this deck: choose what the deck supports.";
    }

    static DeckCheckLine Span(DeckCheckKind kind, DeckResult result, string what, string advice) => result switch
    {
        DeckResult.Passes passes => new DeckCheckLine(
            kind, result, $"{what}: allowed up to {Text(passes.Allowed)} ({Cited(passes.Code, passes.Table, passes.Row, passes.Group)}).{passes.Code.UnreviewedSentence}{Notes(passes.Table, passes.Row)}", true),
        DeckResult.Short over => new DeckCheckLine(
            kind, result, $"{what}: allowed up to {Text(over.Allowed)}, over by {Text(over.Over)} ({Cited(over.Code, over.Table, over.Row, over.Group)}). {advice}{over.Code.UnreviewedSentence}{Notes(over.Table, over.Row)}", false),
        _ => Other(kind, result, what),
    };

    static DeckCheckLine Other(DeckCheckKind kind, DeckResult result, string what) => result switch
    {
        DeckResult.OutOfScope scope => new DeckCheckLine(kind, result, $"{what}: {scope.Explanation}{scope.Code.UnreviewedSentence}", false),
        DeckResult.InputMissing missing => new DeckCheckLine(kind, result, $"{what}: {missing.Explanation}", false),
        _ => new DeckCheckLine(kind, result, $"{what}: {((DeckResult.NoData)result).Explanation}", false),
    };

    /// <summary>
    /// "ZZ-DECK-BEAM row r.2-2x10.10, synthetic p. 3"; with the species group the typed species was read as,
    /// and for a guide's table the clause that says what the guide is not (deck-guide-pack §1.2, Decision 5):
    /// "… — a guide on the 2015 IRC, not CT 2022's adopted IRC 2021; the IRC governs where they differ (p. 1)".
    /// </summary>
    public static string Cited(AdoptedCodeRef code, DeckTable table, DeckRow row, SpeciesGroup? group = null)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(row);
        return $"{table.Designation} row {row.Id}, {row.Source.Location}"
               + (group is null ? string.Empty : $"; species group \"{group.Group}\", {group.Location}")
               + (table.Guide is { } guide ? $" — {guide.Clause(code)}" : string.Empty);
    }

    /// <summary>The table's footnotes that apply to this row, shown with the result.</summary>
    static string Notes(DeckTable table, DeckRow row)
        => string.Concat(table.Footnotes.Where(note => note.AppliesTo == FootnoteScope.Table || row.Footnotes.Contains(note.Id)).Select(note => $" Note {note.Id}: {note.Text}"));

    static string Species(DeckInputs inputs) => inputs.Species is { } species ? $", {species}" : string.Empty;

    static string Text(Length length) => length.Format(Words).Text;
}
