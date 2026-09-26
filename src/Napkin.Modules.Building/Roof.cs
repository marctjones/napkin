using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>
/// A shed roof: a box on the layer Roof, or called "Roof", or carrying roof inputs (docs/design/deck-and-porch.md
/// §5.3). Its plan width runs along the house, its plan height is the run, its depth the rise; its anchor's
/// z is the top of the low support. It sits over a deck, whose ledger edge is the roof's high edge.
/// </summary>
/// <param name="Box">The box the roof is.</param>
public sealed record Roof(Box Box)
{
    /// <summary>The roof's id.</summary>
    public EntityId Id => Box.Id;

    /// <summary>What the roof is called on screen.</summary>
    public string Name => Box.Name.Length == 0 ? "Roof" : Box.Name;

    /// <summary>Whether a box reads as a roof in this sketch.</summary>
    public static bool Is(Sketch sketch, Box box) => Wall.Reads(sketch, box, BuildingLayers.Roof, "Roof") || box.Roof is not null;

    /// <summary>Every roof in a sketch, in id order.</summary>
    public static ImmutableArray<Roof> All(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return [.. Wall.Boxes(sketch).Where(box => Is(sketch, box)).Select(box => new Roof(box))];
    }

    /// <summary>The deck the roof stands over: the one whose outline is the roof's in plan; null when none.</summary>
    public Deck? Over(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return Deck.Bounds(Box) is { } roof ? Deck.All(sketch).FirstOrDefault(deck => deck.Outline == roof) : null;
    }

    /// <summary>The pitch in words: "5 in 12" when the rise over 12 of run is whole, "≈ 4.96 in 12 (rise 50\" over 121\")" when not.</summary>
    public string Pitch
    {
        get
        {
            Int128 rise = Box.Depth.Units, run = Box.Height.Units;
            if (rise * 12 % run == 0)
            {
                return $"{rise * 12 / run} in 12";
            }

            Int128 hundredths = ((rise * 1200) + (run / 2)) / run;
            LengthFormat words = new FeetInchesFormat(16);
            return $"≈ {(long)(hundredths / 100)}.{(long)(hundredths % 100):00} in 12 (rise {Box.Depth.Format(words).Text} over {Box.Height.Format(words).Text})";
        }
    }
}

/// <summary>A shed roof's frame and coverings (§5.3–§5.4), every quantity exact where it can be and said with ≈ where it cannot.</summary>
/// <param name="Roof">The roof.</param>
/// <param name="Deck">The deck it stands over.</param>
/// <param name="Hypotenuse">√(run² + rise²) in 1/1024″: exact when the triangle is whole, else rounded up once to the grid.</param>
/// <param name="HypotenuseExact">Whether it was whole.</param>
/// <param name="RafterRun">The rafter's run, ledger face to tail: run − ledger thickness + overhang.</param>
/// <param name="RafterLength">The rafter's length along the slope, exact: rafter run × hypotenuse ÷ run.</param>
/// <param name="Rafters">Rafter positions along the house, as joists are laid out.</param>
/// <param name="Hap">Height above plate: the rafter's plumb depth less the birdsmouth's, exact.</param>
/// <param name="LedgerAbovePlate">The ledger's top edge above the low support's top, exact: HAP + (run − ledger thickness) × rise ÷ run.</param>
/// <param name="HorizontalSpan">What the rafter table reads: run − ledger thickness.</param>
/// <param name="SlopedArea">Width × rafter length: the sheathed slope, in square 1/1024″.</param>
/// <param name="Cuts">The cuts in plain words.</param>
/// <param name="Pieces">The rafters, the ledger, the blocking — and a beam and posts for an open porch.</param>
/// <param name="Coverings">The sheathing and roofing lines.</param>
/// <param name="Notes">What napkin needs said: the wall that carries the roof, the house it cannot see.</param>
public sealed record RoofFraming(
    Roof Roof,
    Deck Deck,
    Length Hypotenuse,
    bool HypotenuseExact,
    Length RafterRun,
    ExactFraction RafterLength,
    ImmutableArray<Length> Rafters,
    ExactFraction Hap,
    ExactFraction LedgerAbovePlate,
    Length HorizontalSpan,
    ExactFraction SlopedArea,
    string Cuts,
    ImmutableArray<FramingPiece> Pieces,
    ImmutableArray<string> Coverings,
    ImmutableArray<string> Notes)
{
    /// <summary>"10 rafters 2x8 × 11'-9 3/8" at 16"; ledger 12'-0" at ≈4'-7 3/4" above the plates".</summary>
    public string Line
    {
        get
        {
            RoofInputs inputs = Roof.Box.Roof!;
            return $"{Rafters.Length} rafters {inputs.Rafter} × {DeckFrame.Words(RafterLength)} at {inputs.RafterSpacing.Format(new InchesOnlyFormat(16)).Text}; "
                   + $"ledger {inputs.Ledger} × {Roof.Box.Width.Format(new FeetInchesFormat(16)).Text}, its top {DeckFrame.Words(LedgerAbovePlate)} above the low support's top";
        }
    }
}

/// <summary>Derives a shed roof's frame (§5.4): exact rationals, an integer square root rounded up where the triangle is not whole, and no floating point.</summary>
public static class RoofFrame
{
    static readonly LengthFormat Words = new FeetInchesFormat(16);

    /// <summary>The roof's frame, or why there is none.</summary>
    public static (RoofFraming? Framing, string? Problem) Of(Sketch sketch, Roof roof, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(roof);
        ArgumentNullException.ThrowIfNull(library);
        if (roof.Box.Roof is not { } inputs)
        {
            return (null, "No roof inputs yet: choose the rafters and ledger in the panel.");
        }

        if (roof.Over(sketch) is not { } deck)
        {
            return (null, "The roof does not stand over a deck: draw it over one, with Draw → Porch roof.");
        }

        if (!library.TryFindLumber(inputs.Rafter, out LumberStock rafter) || !library.TryFindLumber(inputs.Ledger, out LumberStock ledger))
        {
            return (null, $"{(library.TryFindLumber(inputs.Rafter, out _) ? inputs.Ledger : inputs.Rafter)} is not in the materials library, so the roof cannot be framed.");
        }

        List<string> notes = [];
        Length plate;
        List<FramingPiece> pieces = [];
        switch (inputs.LowEnd)
        {
            case WallLowEnd low when sketch.Find<Box>(low.Wall) is { } wallBox && Wall.Is(sketch, wallBox):
                Wall wall = new(wallBox);
                plate = wall.Thickness;
                if (wallBox.WallInputs?.Bearing != true)
                {
                    notes.Add($"{wall.Name} carries the roof: mark it bearing, and choose what it supports.");
                }

                break;

            case WallLowEnd:
                return (null, "The roof's low end names something that is not a wall.");

            default:
                BeamLowEnd beam = (BeamLowEnd)inputs.LowEnd;
                if (!library.TryFindLumber(beam.Beam.Lumber, out LumberStock beamStock) || !library.TryFindLumber(beam.Post, out LumberStock postStock))
                {
                    return (null, $"The low end's beam or post is not in the materials library, so the roof cannot be framed.");
                }

                plate = beamStock.Thickness * beam.Beam.Plies;
                Length postLength = roof.Box.Anchor.Z - (deck.Box.Anchor.Z + deck.Height) - beamStock.Width;
                if (postLength <= Length.Zero)
                {
                    return (null, "The roof's low end is too low for its beam: raise the roof or use a shallower beam.");
                }

                pieces.Add(new FramingPiece(FramingRole.Beam, beam.Beam.Plies, roof.Box.Width, beamStock));
                pieces.Add(new FramingPiece(FramingRole.Post, beam.PostCount, postLength, postStock));
                break;
        }

        Int128 run = roof.Box.Height.Units, rise = roof.Box.Depth.Units;
        Int128 square = (run * run) + (rise * rise);
        Int128 hyp = StairFraming.CeilingRoot(square);
        bool exact = hyp * hyp == square;
        Length t = ledger.Thickness;
        Length rafterRun = roof.Box.Height - t + inputs.Overhang;
        ExactFraction length = new(rafterRun.Units * hyp, run);
        ExactFraction hap = new((rafter.Width.Units * hyp) - (plate.Units * rise), run);
        ExactFraction above = new((hap.Numerator * run) + (hap.Denominator * (roof.Box.Height - t).Units * rise), hap.Denominator * run);

        // Rafters along the house as joists are laid out: faces at k·s while k·s + t fits, then an end rafter.
        List<Length> rafters = [];
        for (Length at = Length.Zero; at + rafter.Thickness <= roof.Box.Width; at += inputs.RafterSpacing)
        {
            rafters.Add(at);
        }

        if (rafters.Count == 0 || rafters[^1] != roof.Box.Width - rafter.Thickness)
        {
            rafters.Add(roof.Box.Width - rafter.Thickness);
        }

        // A rafter is cut from its length rounded up to the grid; the exact length is what the panel says.
        Length cut = new((long)((length.Numerator + length.Denominator - 1) / length.Denominator));
        pieces.InsertRange(0,
        [
            new FramingPiece(FramingRole.Rafter, rafters.Count, cut, rafter),
            new FramingPiece(FramingRole.Ledger, 1, roof.Box.Width, ledger),
        ]);
        if (inputs.Blocking)
        {
            foreach (IGrouping<Length, Length> bay in rafters.Zip(rafters.Skip(1), (near, far) => far - near - rafter.Thickness).GroupBy(clear => clear).OrderByDescending(group => group.Count()))
            {
                pieces.Add(new FramingPiece(FramingRole.Blocking, bay.Count(), bay.Key, rafter));
            }
        }

        string risePer12 = rise * 12 % run == 0 ? $"{rise * 12 / run}" : roof.Pitch;
        string cuts = $"Plumb cut at the top: set the square at {risePer12} and 12 and mark plumb. "
                      + $"Birdsmouth: from the top plumb cut measure {DeckFrame.Words(new ExactFraction((roof.Box.Height - t).Units * hyp, run))} along the top edge, mark a plumb line, "
                      + $"then a level seat {plate.Format(Words).Text} long back toward the top; the notch is {DeckFrame.Words(new ExactFraction(plate.Units * rise, run))} deep. "
                      + $"Tail: {DeckFrame.Words(new ExactFraction(inputs.Overhang.Units * hyp, run))} further along the top edge, cut plumb.";

        ExactFraction area = new(roof.Box.Width.Units * length.Numerator, length.Denominator);
        List<string> coverings = [];
        if (inputs.Sheathing is { } sheathing)
        {
            coverings.Add(library.TryFind(sheathing, out StockItem item) && item is PanelStock panel
                ? $"Sheathing {panel.Name}: {DeckFrame.SquareFeet(area)} → {(long)((area.Numerator + ((Int128)panel.SheetWidth.Units * panel.SheetLength.Units * area.Denominator) - 1) / ((Int128)panel.SheetWidth.Units * panel.SheetLength.Units * area.Denominator))} sheets (sheets by area — a layout may need more)."
                : $"Sheathing {sheathing}: not in the materials library, so no sheets are counted.");
        }

        ExactFraction roofing = new(area.Numerator * (100 + inputs.Roofing.Waste), area.Denominator * 100);
        Int128 perFoot = (Int128)Length.UnitsPerFoot * Length.UnitsPerFoot;
        coverings.Add(inputs.Roofing.Coverage is { } coverage
            ? $"Roofing {inputs.Roofing.Name}: {DeckFrame.SquareFeet(roofing)} with {inputs.Roofing.Waste} % waste → {(long)((roofing.Numerator + (coverage * perFoot * roofing.Denominator) - 1) / (coverage * perFoot * roofing.Denominator))} units of {coverage} sq ft."
            : $"Roofing {inputs.Roofing.Name}: {DeckFrame.SquareFeet(roofing)} with {inputs.Roofing.Waste} % waste; type the coverage from the bundle.");
        notes.Add("napkin does not know the house: check the ledger clears its eave and openings.");

        return (new RoofFraming(
            roof, deck, new Length((long)hyp), exact, rafterRun, length, [.. rafters], hap, above, roof.Box.Height - t, area, cuts, [.. pieces], [.. coverings], [.. notes]), null);
    }
}

/// <summary>The rafter check (§3.1, `member-span` use rafter): the typed rafter against the adopted pack's rafter table.</summary>
public static class RoofCheck
{
    /// <summary>
    /// The rafters' line: 2x8 at 16" o.c., the deck's species, the site's ground snow load (and roof live
    /// load when the table asks), over the horizontal span from the ledger face to the plate's outer face.
    /// </summary>
    public static DeckCheckLine Rafters(Sketch sketch, RoofFraming framing, LoadedPack? pack)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(framing);
        RoofInputs inputs = framing.Roof.Box.Roof!;
        SpanRequest request = new(
            inputs.Rafter,
            framing.HorizontalSpan,
            framing.Deck.Box.Deck?.Supports,
            framing.Deck.Box.Deck?.Species,
            inputs.RafterSpacing,
            null,
            sketch.Site.GroundSnowLoadPsf,
            sketch.Site.RoofLiveLoadPsf);
        DeckResult result = DeckEvaluator.CheckSpan(pack, SpanUse.Rafter, request);
        string what = $"Rafters {inputs.Rafter} at {inputs.RafterSpacing.Format(new InchesOnlyFormat(16)).Text} o.c., horizontal span {framing.HorizontalSpan.Format(new FeetInchesFormat(16)).Text}";
        return result switch
        {
            DeckResult.Passes passes => new DeckCheckLine(DeckCheckKind.Joists, result, $"{what}: allowed up to {passes.Allowed.Format(new FeetInchesFormat(16)).Text} ({DeckCheck.Cited(passes.Table, passes.Row)}).", true),
            DeckResult.Short over => new DeckCheckLine(DeckCheckKind.Joists, result, $"{what}: allowed up to {over.Allowed.Format(new FeetInchesFormat(16)).Text}, over by {over.Over.Format(new FeetInchesFormat(16)).Text} ({DeckCheck.Cited(over.Table, over.Row)}). Use a deeper rafter or closer spacing.", false),
            DeckResult.OutOfScope scope => new DeckCheckLine(DeckCheckKind.Joists, result, $"{what}: {scope.Explanation}", false),
            DeckResult.InputMissing missing => new DeckCheckLine(DeckCheckKind.Joists, result, $"{what}: {missing.Explanation}", false),
            _ => new DeckCheckLine(DeckCheckKind.Joists, result, $"{what}: {((DeckResult.NoData)result).Explanation}", false),
        };
    }
}
