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
public sealed record DeckChecks(Deck Deck, DeckFraming? Framing, DeckRefusal? Refusal, ImmutableArray<DeckCheckLine> Lines, string? SupportsNote, FrostSuggestion? Frost);

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
        if (framing is null)
        {
            return new DeckChecks(deck, null, refusal, [], null, frost);
        }

        DeckInputs inputs = deck.Box.Deck!;
        string member = inputs.Joist;
        string spacing = inputs.JoistSpacing.Format(new InchesOnlyFormat(16)).Text;
        List<DeckCheckLine> lines = [];

        DeckResult joists = DeckEvaluator.CheckSpan(pack, SpanUse.DeckJoist, new SpanRequest(member, framing.JoistSpan, inputs.Supports, inputs.Species, inputs.JoistSpacing, null));
        lines.Add(Span(DeckCheckKind.Joists, joists, $"Joists {member} at {spacing} o.c.{Species(inputs)}, span {Text(framing.JoistSpan)}", "Use a deeper joist, closer spacing or another beam."));

        // The beam's clear span is exact but may fall between grid points: the table is asked about
        // the span rounded up, never down, and the sentence says ≈ when it was.
        Length beamSpan = new((long)((framing.BeamSpan.Numerator + framing.BeamSpan.Denominator - 1) / framing.BeamSpan.Denominator));
        string beamMember = $"({inputs.Beam.Plies}) {inputs.Beam.Lumber}";
        DeckResult beam = DeckEvaluator.CheckSpan(pack, SpanUse.DeckBeam, new SpanRequest(beamMember, beamSpan, inputs.Supports, inputs.Species, null, framing.JoistSpan));
        lines.Add(Span(DeckCheckKind.Beam, beam, $"Beam {beamMember} on {inputs.PostCount} posts, span {framing.BeamSpanText} carrying {Text(framing.JoistSpan)} of joists", "Add a post, or use a deeper beam."));

        DeckResult ledger = DeckEvaluator.SizeLedger(pack, member, framing.JoistSpan, framing.Width);
        lines.Add(ledger is DeckResult.Sized sized
            ? new DeckCheckLine(
                DeckCheckKind.Ledger,
                ledger,
                $"Ledger to the house: {sized.Row.Text}, {Text(sized.Row.Spacing)} on centre ({Cited(sized.Table, sized.Row)}); {sized.Count} fasteners for a {Text(framing.Width)} ledger "
                + $"(⌈{Text(framing.Width)} ÷ {Text(sized.Row.Spacing)}⌉ + 1, napkin's count).{Notes(sized.Table, sized.Row)}",
                true)
            : Other(DeckCheckKind.Ledger, ledger, "Ledger"));

        // The most loaded post: a middle one when there are three or more, otherwise an end post, which
        // carries half a span.
        bool middle = inputs.PostCount >= 3;
        ExactFraction area = middle ? framing.TributaryArea : new ExactFraction(framing.TributaryArea.Numerator, framing.TributaryArea.Denominator * 2);
        string which = middle ? "a middle post" : "an end post";
        DeckResult footing = DeckEvaluator.SizeFooting(pack, area, sketch.Site.SoilBearingPsf);
        lines.Add(footing is DeckResult.Sized foot
            ? new DeckCheckLine(
                DeckCheckKind.Footing,
                footing,
                $"Footings: {foot.Row.Text} for {which}'s {DeckFrame.SquareFeet(area)} on {sketch.Site.SoilBearingPsf} psf ({Cited(foot.Table, foot.Row)}).{Notes(foot.Table, foot.Row)}",
                true)
            : Other(DeckCheckKind.Footing, footing, $"Footings ({which}, {DeckFrame.SquareFeet(area)})"));

        lines.Add(FrostLine(sketch, inputs, pack));
        return new DeckChecks(deck, framing, null, [.. lines], SupportsNote(sketch, deck, inputs), frost);
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

    /// <summary>§5.1: a bearing wall stands on the deck and nothing says what the deck supports.</summary>
    static string? SupportsNote(Sketch sketch, Deck deck, DeckInputs inputs)
    {
        if (inputs.Supports is not null || deck.Outline is not { } outline)
        {
            return null;
        }

        Wall? bearing = Wall.All(sketch).FirstOrDefault(wall =>
            wall.Box.WallInputs?.Bearing == true
            && wall.Box.Anchor.Z == deck.Box.Anchor.Z + deck.Height
            && Deck.Bounds(wall.Box) is { } w
            && outline.West <= w.West && w.East <= outline.East && outline.South <= w.South && w.North <= outline.North);
        return bearing is null ? null : $"{bearing.Name} (bearing) stands on this deck: choose what the deck supports.";
    }

    static DeckCheckLine Span(DeckCheckKind kind, DeckResult result, string what, string advice) => result switch
    {
        DeckResult.Passes passes => new DeckCheckLine(kind, result, $"{what}: allowed up to {Text(passes.Allowed)} ({Cited(passes.Table, passes.Row)}).{Notes(passes.Table, passes.Row)}", true),
        DeckResult.Short over => new DeckCheckLine(kind, result, $"{what}: allowed up to {Text(over.Allowed)}, over by {Text(over.Over)} ({Cited(over.Table, over.Row)}). {advice}{Notes(over.Table, over.Row)}", false),
        _ => Other(kind, result, what),
    };

    static DeckCheckLine Other(DeckCheckKind kind, DeckResult result, string what) => result switch
    {
        DeckResult.OutOfScope scope => new DeckCheckLine(kind, result, $"{what}: {scope.Explanation}", false),
        DeckResult.InputMissing missing => new DeckCheckLine(kind, result, $"{what}: {missing.Explanation}", false),
        DeckResult.NoData none => new DeckCheckLine(kind, result, $"{what}: {none.Explanation}", false),
        _ => new DeckCheckLine(kind, result, what, false),
    };

    /// <summary>"ZZ-DECK-JOIST row r.fir.2x8.16, synthetic p. 2 row 2x8 16".</summary>
    public static string Cited(DeckTable table, DeckRow row) => $"{table.Designation} row {row.Id}, {row.Source.Location}";

    /// <summary>The table's footnotes that apply to this row, shown with the result.</summary>
    static string Notes(DeckTable table, DeckRow row)
        => string.Concat(table.Footnotes.Where(note => note.AppliesTo == FootnoteScope.Table || row.Footnotes.Contains(note.Id)).Select(note => $" Note {note.Id}: {note.Text}"));

    static string Species(DeckInputs inputs) => inputs.Species is { } species ? $", {species}" : string.Empty;

    static string Text(Length length) => length.Format(Words).Text;
}
