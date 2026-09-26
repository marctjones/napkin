using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The deck's code checks (deck-and-porch §3, §11.2 tests 6–8) on §9's deck under the SYNTHETIC pack
/// us-zz-deck (NOT CODE VALUES), and under the shipped Connecticut pack, which has no deck tables and
/// offers only its cited frost depth.
/// </summary>
public class DeckCheckTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();
    static readonly CodePacks Synthetic = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]);
    static readonly CodePacks Shipped = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "RealPacks")]);
    static readonly CodeChoice ZzDeck = new("us-zz-deck", 1, CodeMode.Locked, new DateOnly(2026, 9, 26));
    static readonly CodeChoice Connecticut = new("us-ct-2022", 1, CodeMode.Locked, new DateOnly(2026, 9, 26));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static DeckInputs Inputs() => new(
        JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", In(0, 1, 8), true, "zz-deck", "zz-fir", In(42), null, null);

    /// <summary>§9.1: the existing house wall and Deck 1, 144 × 120 × 36, north edge on the house.</summary>
    static Sketch Drawing(DeckInputs? inputs = null, long depth = 120, CodeChoice? code = null, SiteValues? site = null, params Box[] more)
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "House",
            Phase = Phase.Existing,
        };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-depth), Length.Zero), In(144), In(depth), In(36), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = inputs ?? Inputs(),
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck)).WithEntity(house).WithEntity(deck);
        sketch = more.Aggregate(sketch, (with, box) => with.WithEntity(box));
        return sketch with
        {
            Code = code ?? ZzDeck,
            Site = site ?? SiteValues.NotEntered with { SoilBearingPsf = 2000, FrostDepth = In(42), Source = new SiteSource("Town building department", null) },
        };
    }

    static DeckChecks Only(Sketch sketch, CodePacks? packs = null) => Assert.Single(DeckCheck.Of(sketch, packs ?? Synthetic));

    static DeckCheckLine Line(DeckChecks checks, DeckCheckKind kind) => Assert.Single(checks.Lines, line => line.Kind == kind);

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void The_worked_example_passes_every_check_with_its_citation()
    {
        DeckChecks checks = Only(Drawing());

        Assert.Equal(
            "Joists 2x8 at 16\" o.c., zz-fir, span 9'-9\": allowed up to 11'-1\" (ZZ-DECK-JOIST row r.fir.2x8.16, synthetic p. 2 row 2x8 16). Note a: SYNTHETIC footnote a: shown with results, not encoded.",
            Line(checks, DeckCheckKind.Joists).Text);
        Assert.StartsWith("Beam (2) 2x10 on 3 posts, span 5'-6 3/4\" carrying 9'-9\" of joists: allowed up to 6'-10\" (ZZ-DECK-BEAM", Line(checks, DeckCheckKind.Beam).Text, StringComparison.Ordinal);
        Assert.StartsWith(
            "Ledger to the house: zz-bolts, staggered, 1'-5\" on centre (ZZ-DECK-LEDGER row r.2x8.12, synthetic p. 5); 10 fasteners for a 12'-0\" ledger (⌈12'-0\" ÷ 1'-5\"⌉ + 1, napkin's count).",
            Line(checks, DeckCheckKind.Ledger).Text,
            StringComparison.Ordinal);
        Assert.StartsWith("Footings: zz 15 in square for a middle post's 27.1 sq ft on 2000 psf (ZZ-DECK-FOOTING", Line(checks, DeckCheckKind.Footing).Text, StringComparison.Ordinal);
        Assert.StartsWith("Frost: footings 3'-6\" below grade; frost line 3'-6\" (site value, Town building department). Note x: SYNTHETIC", Line(checks, DeckCheckKind.Frost).Text, StringComparison.Ordinal);
        Assert.All(checks.Lines.Where(line => line.Kind <= DeckCheckKind.Frost), line => Assert.True(line.Passing));

        // §9.2's deck is 36″ up with three open edges and no guard yet: the pack's trigger asks for one.
        Assert.Equal(
            "Guard required: the deck is 3'-0\" above grade, over 2'-4\", with 3 open edges (ZZ-GUARD.1, synthetic p. 7 guard): add a guard in the panel.",
            Line(checks, DeckCheckKind.Guard).Text);
        Assert.Null(checks.SupportsNote);
        Assert.Null(checks.Frost);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void Longer_joists_are_over_by_the_difference_and_the_advice_is_general()
    {
        // A 12'-6" deck: joists 150 − 3 = 147″ (12'-3"), allowed 11'-1": over by 1'-2".
        DeckCheckLine joists = Line(Only(Drawing(depth: 150)), DeckCheckKind.Joists);

        Assert.False(joists.Passing);
        Assert.Contains("span 12'-3\": allowed up to 11'-1\", over by 1'-2\"", joists.Text, StringComparison.Ordinal);
        Assert.Contains("Use a deeper joist, closer spacing or another beam.", joists.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_deck_carrying_a_roof_is_out_of_scope_of_a_table_without_that_row_and_a_missing_species_is_asked_for()
    {
        Assert.Contains("has no row for what the deck supports zz-deck-and-roof: get it engineered", Line(Only(Drawing(Inputs() with { Supports = "zz-deck-and-roof" })), DeckCheckKind.Joists).Text, StringComparison.Ordinal);

        DeckCheckLine species = Line(Only(Drawing(Inputs() with { Species = null })), DeckCheckKind.Joists);
        Assert.Equal("Joists 2x8 at 16\" o.c., span 9'-9\": Enter the species: table ZZ-DECK-JOIST bands on it.", species.Text);
        Assert.IsType<DeckResult.InputMissing>(species.Result);
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void Two_posts_put_the_footing_under_an_end_post_carrying_half_a_span()
    {
        // Two posts: span 137″; an end post carries 68 1/2 × 58 1/2 = 4007.25 sq in = 27.8 sq ft.
        DeckCheckLine footing = Line(Only(Drawing(Inputs() with { PostCount = 2 })), DeckCheckKind.Footing);

        Assert.StartsWith("Footings: zz 15 in square for an end post's 27.8 sq ft", footing.Text, StringComparison.Ordinal);

        // No soil bearing value: asked for.
        DeckCheckLine missing = Line(Only(Drawing(site: SiteValues.NotEntered with { FrostDepth = In(42) })), DeckCheckKind.Footing);
        Assert.Equal("Footings (a middle post, 27.1 sq ft): Enter the soil bearing value: table ZZ-DECK-FOOTING bands on it.", missing.Text);
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void The_frost_line_is_two_typed_values_compared_and_either_missing_is_asked_for()
    {
        Assert.StartsWith("Frost: footings 3'-0\" below grade, 6\" short of the frost line 3'-6\"", Line(Only(Drawing(Inputs() with { FootingDepth = In(36) })), DeckCheckKind.Frost).Text, StringComparison.Ordinal);
        Assert.Equal("Frost: enter how deep the footings go below grade in the deck panel.", Line(Only(Drawing(Inputs() with { FootingDepth = null })), DeckCheckKind.Frost).Text);
        Assert.Equal("Frost: enter the site's frost depth (Project → Adopted code and site).", Line(Only(Drawing(site: SiteValues.NotEntered with { SoilBearingPsf = 2000 })), DeckCheckKind.Frost).Text);
        Assert.Contains("(site value).", Line(Only(Drawing(site: SiteValues.NotEntered with { SoilBearingPsf = 2000, FrostDepth = In(42) })), DeckCheckKind.Frost).Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "DECK-001")]
    public void Under_the_shipped_Connecticut_pack_every_table_is_no_data_and_its_frost_depth_is_offered_with_its_citation()
    {
        DeckChecks checks = Only(Drawing(code: Connecticut, site: SiteValues.NotEntered with { SoilBearingPsf = 2000 }), Shipped);

        Assert.Equal(
            "Joists 2x8 at 16\" o.c., zz-fir, span 9'-9\": The loaded pack CT 2022 has no deck joist span, so napkin cannot check this. Nothing is guessed: add it from your copy of the code (docs/rules-engine.md).",
            Line(checks, DeckCheckKind.Joists).Text);
        Assert.All(checks.Lines.Where(line => line.Kind < DeckCheckKind.Frost), line => Assert.IsType<DeckResult.NoData>(line.Result));

        // Connecticut's Table R301.2, p. 131: 42" — offered, never applied.
        FrostSuggestion offer = checks.Frost!;
        Assert.Equal(In(42), offer.Depth);
        Assert.Equal("CT 2022 says 3'-6\" (TABLE R301.2 CLIMATIC AND GEOGRAPHIC DESIGN CRITERIA (Amd), p. 131 (document footer 'Page - 131')) — use it?", offer.Text);

        // With the site value at 42" the offer is not made again, and the frost line carries CT's deck exceptions.
        DeckChecks accepted = Only(Drawing(code: Connecticut), Shipped);
        Assert.Null(accepted.Frost);
        Assert.Contains("Note R403.1.4.1 exc. 3: Decks and ramps not supported by a dwelling need not be provided with footings that extend below the frost line.", Line(accepted, DeckCheckKind.Frost).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_code_every_table_is_no_data()
    {
        DeckChecks checks = Only(Drawing() with { Code = null });

        Assert.StartsWith("No adopted code is chosen", Assert.IsType<DeckResult.NoData>(Line(checks, DeckCheckKind.Ledger).Result).Explanation, StringComparison.Ordinal);
        Assert.Null(checks.Frost);
    }

    [Fact]
    public void A_bearing_wall_on_the_deck_with_nothing_said_about_supports_asks()
    {
        Box front = new(EntityId.New(), WallLayer, new Point3(In(48), In(-120), In(36)), In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Front",
            WallInputs = new WallInputs(null, null) { Bearing = true },
        };

        Assert.Equal("Front (bearing) stands on this deck: choose what the deck supports.", Only(Drawing(Inputs() with { Supports = null }, more: front)).SupportsNote);
        Assert.Null(Only(Drawing(more: front)).SupportsNote);
        Assert.Null(Only(Drawing(Inputs() with { Supports = null }, more: front with { WallInputs = null })).SupportsNote);
    }

    [Fact]
    public void A_deck_that_cannot_be_framed_has_no_lines_and_says_why()
    {
        Sketch drawing = Drawing();
        Sketch alone = drawing.WithoutEntity(drawing.Entities.Values.OfType<Box>().Single(box => box.Name == "House").Id);
        DeckChecks checks = Only(alone);

        Assert.Empty(checks.Lines);
        Assert.Equal(DeckProblem.NotAgainstAWall, checks.Refusal!.Problem);
    }
}
