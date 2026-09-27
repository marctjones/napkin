using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The deck's code checks (deck-and-porch §3, §11.2 tests 6–8) on §9's deck under the SYNTHETIC pack
/// us-zz-deck (NOT CODE VALUES), and under the shipped Connecticut pack, whose only deck table is DCA
/// 6-2015's Table 2 (#41; expected values typed from p. 4 of the PDF) and which offers its cited frost depth.
/// </summary>
public class DeckCheckTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();
    static readonly CodePacks Synthetic = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]);
    static readonly CodePacks Shipped = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "RealPacks")]);
    static readonly CodeChoice ZzDeck = new("us-zz-deck", 1, CodeMode.Locked, new DateOnly(2026, 9, 26));
    static readonly CodeChoice Connecticut = new("us-ct-2022", 3, CodeMode.Locked, new DateOnly(2026, 9, 27));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static DeckInputs Inputs() => new(
        JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", In(0, 1, 8), true, "zz-deck", "zz-fir", In(42), null, null);

    /// <summary>§9.1: the existing house wall and Deck 1, 144 × 120 × 36, north edge on the house.</summary>
    static Sketch Drawing(DeckInputs? inputs = null, long depth = 120, CodeChoice? code = null, SiteValues? site = null, long width = 144, params Box[] more)
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "House",
            Phase = Phase.Existing,
        };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-depth), Length.Zero), In(width), In(depth), In(36), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = inputs ?? Inputs(),
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck)).WithEntity(house).WithEntity(deck);
        sketch = more.Aggregate(sketch, (with, box) => with.WithEntity(box));
        return sketch with
        {
            Code = code ?? ZzDeck,
            Site = site ?? Entered,
        };
    }

    /// <summary>The site values the worked example has entered: the guide's scope asks for the snow load.</summary>
    static readonly SiteValues Entered = SiteValues.NotEntered with { SoilBearingPsf = 2000, FrostDepth = In(42), GroundSnowLoadPsf = 30, Source = new SiteSource("Town building department", null) };

    const string Unreviewed = " UNREVIEWED: values not yet checked against the source.";

    static DeckChecks Only(Sketch sketch, CodePacks? packs = null) => Assert.Single(DeckCheck.Of(sketch, packs ?? Synthetic));

    static DeckCheckLine Line(DeckChecks checks, DeckCheckKind kind) => Assert.Single(checks.Lines, line => line.Kind == kind);

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void The_worked_example_passes_every_check_with_its_citation()
    {
        DeckChecks checks = Only(Drawing());

        // The joists come from the synthetic guide: the clause says what it is not, and the species is read as its group.
        Assert.Equal(
            "Joists 2x8 at 16\" o.c., zz-fir, span 9'-9\": allowed up to 11'-1\" (ZZ GUIDE Table ZZ-GUIDE-JOIST row r.fir.2x8.16, synthetic guide p. 3 row zz-fir 2x8 16; "
            + "species group \"zz-fir, zz-hem\", synthetic guide p. 3, first row heading — a guide on the 2098 IRC, not ZZ DECK's adopted IRC 2099; the IRC governs where they differ (synthetic guide p. 1))."
            + Unreviewed + " Note a: SYNTHETIC footnote a: shown with results, not encoded.",
            Line(checks, DeckCheckKind.Joists).Text);
        Assert.StartsWith("Deck checks under ZZ DECK use ZZ GUIDE (SYNTHETIC DECK GUIDE - NOT A CODE, nobody (synthetic)), a guide based on the 2098 IRC", Assert.Single(checks.Guides), StringComparison.Ordinal);
        // The beam span L_B is measured face to face of posts (DCA 6 Figure 3, p. 7; #41): (144 − 3 × 3 1/2) ÷ 2 = 66 3/4".
        Assert.StartsWith("Beam (2) 2x10 on 3 posts, span 5'-6 3/4\" between post faces, carrying 9'-9\" of joists: allowed up to 6'-10\" (ZZ-DECK-BEAM", Line(checks, DeckCheckKind.Beam).Text, StringComparison.Ordinal);
        Assert.StartsWith(
            "Ledger to the house: zz-bolts, staggered, 1'-5\" on centre (ZZ-DECK-LEDGER row r.2x8.12, synthetic p. 5); 10 fasteners for a 12'-0\" ledger (⌈12'-0\" ÷ 1'-5\"⌉ + 1, napkin's count).",
            Line(checks, DeckCheckKind.Ledger).Text,
            StringComparison.Ordinal);
        // A middle post carries, by DCA 6 Appendix B Eq. B-1 (pp. B1–B2), B_L 72" (its centreline to the deck's outside edge)
        // × half of J_L 118 1/2" (ledger face to the rim's outside face) = 72 × 59 1/4 = 4266 sq in = 29.6 sq ft.
        Assert.StartsWith(
            "Footings: zz 15 in square for a middle post's 29.6 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: 6'-0\" of beam, post centreline to the deck's outside edge, "
            + "× half the joists' 9'-10 1/2\", ledger face to the rim's outside face) on 2000 psf (ZZ-DECK-FOOTING",
            Line(checks, DeckCheckKind.Footing).Text,
            StringComparison.Ordinal);
        Assert.StartsWith("Frost: footings 3'-6\" below grade; frost line 3'-6\" (site value, Town building department). Note x: SYNTHETIC", Line(checks, DeckCheckKind.Frost).Text, StringComparison.Ordinal);
        Assert.All(checks.Lines.Where(line => line.Kind <= DeckCheckKind.Frost), line => Assert.True(line.Passing));

        // §9.2's deck is 36″ up with three open edges and no guard yet: the pack's trigger asks for one.
        Assert.Equal(
            "Guard required: the deck is 3'-0\" above grade, over 2'-4\", with 3 open edges (ZZ-GUARD.1, synthetic p. 7 guard): add a guard in the panel." + Unreviewed,
            Line(checks, DeckCheckKind.Guard).Text);
        Assert.Null(checks.SupportsNote);
        Assert.Null(checks.Frost);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void Longer_joists_are_over_by_the_difference_and_the_advice_is_general()
    {
        // A 12'-6" deck, 13'-0" wide so the guide covers its shape: joists 150 − 3 = 147″ (12'-3"), allowed 11'-1": over by 1'-2".
        DeckCheckLine joists = Line(Only(Drawing(depth: 150, width: 156)), DeckCheckKind.Joists);

        Assert.False(joists.Passing);
        Assert.Contains("span 12'-3\": allowed up to 11'-1\", over by 1'-2\"", joists.Text, StringComparison.Ordinal);
        Assert.Contains("Use a deeper joist, closer spacing or another beam.", joists.Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_deck_carrying_a_roof_is_out_of_the_guides_scope_citing_its_limit_and_a_missing_species_is_asked_for()
    {
        // The porch case (deck-and-porch decision 4): the guide's table has no supports column, and its scope answers.
        DeckCheckLine porch = Line(Only(Drawing(Inputs() with { Supports = "zz-deck-and-roof" })), DeckCheckKind.Joists);
        Assert.Equal(
            "Joists 2x8 at 16\" o.c., zz-fir, span 9'-9\": Beyond the scope of ZZ GUIDE: \"SYNTHETIC: covers a deck carrying only its own loads.\" (ZZ GUIDE synthetic guide p. 2, item 8). Get it engineered." + Unreviewed,
            porch.Text);
        Assert.Equal("s.loads", Assert.IsType<DeckResult.OutOfScope>(porch.Result).Limit!.Id);

        DeckCheckLine species = Line(Only(Drawing(Inputs() with { Species = null })), DeckCheckKind.Joists);
        Assert.Equal("Joists 2x8 at 16\" o.c., span 9'-9\": Enter the species: table ZZ-GUIDE-JOIST bands on it.", species.Text);
        Assert.IsType<DeckResult.InputMissing>(species.Result);
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void The_guides_scope_asks_for_the_snow_load_and_refuses_a_deck_longer_than_it_is_wide()
    {
        DeckChecks snowless = Only(Drawing(site: Entered with { GroundSnowLoadPsf = null }));
        Assert.Equal(
            "Joists 2x8 at 16\" o.c., zz-fir, span 9'-9\": Enter the ground snow load: ZZ GUIDE scope limit s.snow (ZZ GUIDE synthetic guide p. 2, item 9) depends on it.",
            Line(snowless, DeckCheckKind.Joists).Text);

        // The base layer's beam has no guide, so no scope: it still answers.
        Assert.IsType<DeckResult.Passes>(Line(snowless, DeckCheckKind.Beam).Result);

        // 12'-6" out from the house on a 12'-0" ledger: beyond the guide's shape limit.
        DeckCheckLine deep = Line(Only(Drawing(depth: 150)), DeckCheckKind.Joists);
        Assert.Equal("s.shape", Assert.IsType<DeckResult.OutOfScope>(deep.Result).Limit!.Id);
        Assert.Contains("\"SYNTHETIC: a deck no longer out from the house than it is wide.\" (ZZ GUIDE synthetic guide p. 2, item 2)", deep.Text, StringComparison.Ordinal);

        // A species the guide covers reads as its printed group: zz-cedar's 2x8 at 16" allows 9'-7", 2" short of 9'-9".
        Assert.Contains("allowed up to 9'-7\", over by 2\" (ZZ GUIDE Table ZZ-GUIDE-JOIST row r.cedar.2x8.16, synthetic guide p. 3 row zz-cedar 2x8 16; species group \"zz-cedar\"",Line(Only(Drawing(Inputs() with { Species = "zz-cedar" })), DeckCheckKind.Joists).Text, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void Two_posts_put_the_footing_under_an_end_post_carrying_half_a_span()
    {
        // Two posts: an end post, DCA 6 Appendix B Eq. B-2 with no beam overhang (B_O = 0): half of B_L 144" (the deck's
        // outside edge to outside edge) × half of J_L 118 1/2" = 72 × 59 1/4 = 4266 sq in = 29.6 sq ft.
        DeckCheckLine footing = Line(Only(Drawing(Inputs() with { PostCount = 2 })), DeckCheckKind.Footing);

        Assert.StartsWith(
            "Footings: zz 15 in square for an end post's 29.6 sq ft (DCA 6 Appendix B Eq. B-2, pp. B1–B2: half the beam's 12'-0\", the deck's outside edge to outside edge, ",
            footing.Text,
            StringComparison.Ordinal);

        // No soil bearing value: asked for, the area and its measure said.
        DeckCheckLine missing = Line(Only(Drawing(site: SiteValues.NotEntered with { FrostDepth = In(42) })), DeckCheckKind.Footing);
        Assert.Equal(
            "Footings for a middle post's 29.6 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: 6'-0\" of beam, post centreline to the deck's outside edge, "
            + "× half the joists' 9'-10 1/2\", ledger face to the rim's outside face): Enter the soil bearing value: table ZZ-DECK-FOOTING bands on it.",
            missing.Text);
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
    public void Under_the_shipped_Connecticut_pack_the_joists_answer_from_DCA_6_the_rest_is_no_data_and_its_frost_depth_is_offered()
    {
        // The note's worked example: 2x8 at 16" o.c., Southern Pine, supporting only the deck, 9'-9" of span. Table 2 (p. 4): 11'-10".
        DeckInputs typed = Inputs() with { Supports = "deck", Species = "Southern Pine" };
        DeckChecks answered = Only(Drawing(typed, code: Connecticut), Shipped);
        DeckCheckLine joists = Line(answered, DeckCheckKind.Joists);
        Assert.True(joists.Passing);
        Assert.StartsWith(
            "Joists 2x8 at 16\" o.c., Southern Pine, span 9'-9\": allowed up to 11'-10\" (DCA 6-2015 Table 2 row r.sp.2x8.16, p. 4, ",
            joists.Text,
            StringComparison.Ordinal);
        Assert.Contains(
            "species group \"Southern Pine\", p. 4, Table 2, Species column, first row heading — a guide on the 2015 IRC, not CT 2022's adopted IRC 2021; the IRC governs where they differ (p. 1))." + Unreviewed,
            joists.Text,
            StringComparison.Ordinal);
        Assert.Contains(" Note 1: Assumes 40 psf live load, 10 psf dead load, No. 2 grade, and wet service conditions. Note 2: Assumes L/360 deflection.", joists.Text, StringComparison.Ordinal);
        Assert.StartsWith("Deck checks under CT 2022 use DCA 6-2015 (Prescriptive Residential Wood Deck Construction Guide, Based on the 2015 International Residential Code, American Wood Council)", Assert.Single(answered.Guides), StringComparison.Ordinal);
        Assert.All(answered.Lines.Where(line => line.Kind is DeckCheckKind.Ledger or DeckCheckKind.Footing), line => Assert.IsType<DeckResult.NoData>(line.Result));

        // The beam: (2) 2x10 Southern Pine carrying 9'-9" of joists is Table 3A's ≤ 10' column, 7'-9" (p. 6); its span
        // L_B between post faces (Figure 3, p. 7), (144 − 3 × 3 1/2) ÷ 2 = 66 3/4", passes.
        DeckCheckLine beam = Line(answered, DeckCheckKind.Beam);
        Assert.True(beam.Passing);
        Assert.StartsWith(
            "Beam (2) 2x10 on 3 posts, span 5'-6 3/4\" between post faces, carrying 9'-9\" of joists: allowed up to 7'-9\" "
            + "(DCA 6-2015 Table 3A row r.sp.2-2x10.10, p. 6, row Southern Pine 2-2x10, joist span column 10'; species group \"Southern Pine\", p. 6, Table 3A, Species column, first row heading "
            + "— a guide on the 2015 IRC, not CT 2022's adopted IRC 2021; the IRC governs where they differ (p. 1))." + Unreviewed,
            beam.Text);
        Assert.EndsWith(
            " Note 1: Assumes 40 psf live load, 10 psf dead load, L/360 simple span beam deflection limit, cantilever length/180 deflection limit, No. 2 grade, and wet service conditions."
            + " Note 4: Beam depth must be equal to or greater than joist depth if joist hangers are used (see Figure 6, Option 3).",
            beam.Text);

        // Nothing typed about what the deck supports: DCA 6's scope asks (item 8, p. 2), and the panel offers its word.
        DeckChecks untyped = Only(Drawing(typed with { Supports = null }, code: Connecticut), Shipped);
        Assert.Equal("supports", Assert.IsType<DeckResult.InputMissing>(Line(untyped, DeckCheckKind.Joists).Result).Input);
        Assert.Equal(["deck"], DeckCheck.SupportsOffered(Shipped.Loaded.Single()));

        DeckChecks checks = Only(Drawing(code: Connecticut, site: SiteValues.NotEntered with { SoilBearingPsf = 2000 }), Shipped);

        // Connecticut's Table R301.2, p. 131: 42" — offered, never applied.
        FrostSuggestion offer = checks.Frost!;
        Assert.Equal(In(42), offer.Depth);
        Assert.Equal("CT 2022 says 3'-6\" (TABLE R301.2 CLIMATIC AND GEOGRAPHIC DESIGN CRITERIA (Amd), p. 131 (document footer 'Page - 131')) — use it?", offer.Text);

        // With the site value at 42" the offer is not made again, and the frost line carries CT's deck exceptions.
        DeckChecks accepted = Only(Drawing(code: Connecticut), Shipped);
        Assert.Null(accepted.Frost);
        Assert.Contains("Note R403.1.4.1 exc. 3: Decks and ramps not supported by a dwelling need not be provided with footings that extend below the frost line.", Line(accepted, DeckCheckKind.Frost).Text, StringComparison.Ordinal);
    }

    /// <summary>The same drawing with Deck 1's box changed: the same deck, so a recompute compares its lines.</summary>
    static Sketch Changed(Sketch sketch, Func<Box, Box> change)
    {
        Box deck = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Deck 1");
        return sketch.WithEntity(change(deck));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void Switching_to_a_pack_without_the_deck_tables_says_which_lines_went_to_no_data_and_back()
    {
        CodePacks both = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck"), Path.Combine(AppContext.BaseDirectory, "RealPacks")]);
        Sketch zz = Drawing();
        Sketch ct = zz with { Code = Connecticut };
        ImmutableArray<DeckChecks> before = DeckCheck.Of(zz, both), after = DeckCheck.Of(ct, both);

        // Connecticut has DCA 6's joist and beam tables, whose scope does not know the synthetic "zz-deck" (newly
        // flagged), and no ledger or footing table (no longer computable).
        DeckRecomputeReport lost = DeckCheck.Report(before, after);
        Assert.Equal(["Ledger", "Footing"], lost.NoLongerComputable.Select(change => change.Key.Check));
        Assert.Equal(["Joists", "Beam"], lost.NewlyFlagged.Select(change => change.Key.Check));
        ImmutableArray<string> said = DeckCheck.Changes(before, after);
        Assert.StartsWith("Deck 1, newly flagged: Joists 2x8 at 16\" o.c., zz-fir, span 9'-9\": Beyond the scope of DCA 6-2015: \"Assumes 40 psf live load", said[0], StringComparison.Ordinal);
        Assert.StartsWith(
            "Deck 1, newly flagged: Beam (2) 2x10 on 3 posts, span 5'-6 3/4\" between post faces, carrying 9'-9\" of joists: Beyond the scope of DCA 6-2015: \"Assumes 40 psf live load",
            said[1],
            StringComparison.Ordinal);
        Assert.Equal(
            "Deck 1, can no longer be checked: Ledger: The loaded pack CT 2022 has no deck ledger table, so napkin cannot check this. "
            + "Nothing is guessed: add it from your copy of the code (docs/rules-engine.md).",
            said[2]);
        Assert.Equal(
            "Now checking against CT 2022 (IRC 2021, pack us-ct-2022 rev 3): every result recomputed; 4 changed, 2 newly flagged, 2 can no longer be computed.",
            CodeCheck.SwitchSummary(Assert.Single(both.Loaded, pack => pack.Manifest.Id == "us-ct-2022").Code, CodeCheck.Report([], []), BracingCheck.Report([], []), lost));

        // And back: every line answers again.
        Assert.All(DeckCheck.Changes(after, before), said => Assert.StartsWith("Deck 1, now answered: ", said, StringComparison.Ordinal));
        Assert.Equal(4, DeckCheck.Report(after, before).Changes.Count(change => change.Kind == DeckChangeKind.ToAnswer));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void An_edit_says_each_deck_line_whose_answer_changed_and_not_a_span_that_moved_within_its_row()
    {
        Sketch fir = Drawing();
        ImmutableArray<DeckChecks> was = DeckCheck.Of(fir, Synthetic);
        DeckChecks After(Func<Box, Box> change) => Assert.Single(DeckCheck.Of(Changed(fir, change), Synthetic));

        // zz-cedar's 2x8 at 16" allows 9'-7": the joists become short; the base layer's beam has no zz-cedar row.
        DeckChecks cedar = After(box => box with { Deck = Inputs() with { Species = "zz-cedar" } });
        ImmutableArray<string> flagged = DeckCheck.Changes(was, [cedar]);
        Assert.StartsWith("Deck 1, newly flagged: Joists 2x8 at 16\" o.c., zz-cedar, span 9'-9\": allowed up to 9'-7\", over by 2\"", flagged[0], StringComparison.Ordinal);
        Assert.StartsWith("Deck 1, newly flagged: Beam (2) 2x10 on 3 posts", flagged[1], StringComparison.Ordinal);
        Assert.All(DeckCheck.Changes([cedar], was), said => Assert.StartsWith("Deck 1, now answered: ", said, StringComparison.Ordinal));

        // At 12" the joists answer from another row.
        Assert.StartsWith("Deck 1, changed: Joists 2x8 at 12\" o.c., zz-fir, span 9'-9\": allowed up to 12'-7\"", Assert.Single(DeckCheck.Changes(was, [After(box => box with { Deck = Inputs() with { JoistSpacing = In(12) } })])), StringComparison.Ordinal);

        // 6" deeper: the joists span 10'-3" in the same row (not said); the beam now carries more than 10'-0" of joists.
        DeckChecks deeper = After(box => box with { Anchor = new Point3(In(48), In(-126), Length.Zero), Height = In(126) });
        Assert.Contains(DeckCheck.Report(was, [deeper]).Changes, change => change.Key.Check == "Joists" && change.Kind == DeckChangeKind.SpanMoved);
        Assert.DoesNotContain(DeckCheck.Changes(was, [deeper]), said => said.Contains("Joists", StringComparison.Ordinal));

        // One missing input for another is not said either.
        DeckChecks noSpecies = After(box => box with { Deck = Inputs() with { Species = null } });
        DeckChecks noSupports = After(box => box with { Deck = Inputs() with { Species = null, Supports = null } });
        Assert.Equal(DeckChangeKind.NoAnswerChanged, Assert.Single(DeckCheck.Report([noSpecies], [noSupports]).Changes).Kind);
        Assert.Empty(DeckCheck.Changes([noSpecies], [noSupports]));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_cantilever_is_checked_against_the_lesser_of_the_rows_overhang_and_a_quarter_of_the_joist_span()
    {
        DeckInputs typed = Inputs() with { Supports = "deck", Species = "Southern Pine" };

        // 1'-6" past the beam: the joists span 120 − 3 − 18 = 99" (8'-3"); Table 2's Southern Pine 2x8 at 16" allows
        // an overhang of 2'-0" (p. 4) and a quarter of 8'-3" is 2'-0 3/4" (p. 3), so 2'-0" governs.
        DeckCheckLine passes = Line(Only(Drawing(typed with { Cantilever = In(18) }, code: Connecticut), Shipped), DeckCheckKind.Cantilever);
        Assert.True(passes.Passing);
        Assert.StartsWith(
            "Cantilever 1'-6\" past the beam: allowed up to 2'-0\", the lesser of the row's 2'-0\" and 1/4 of the 8'-3\" span, 2'-0 3/4\" (DCA 6-2015 Table 2 row r.sp.2x8.16, p. 4, ",
            passes.Text,
            StringComparison.Ordinal);
        Assert.Contains("; the cap: JOIST SIZE, p. 3", passes.Text, StringComparison.Ordinal);
        Assert.Contains(Unreviewed, passes.Text, StringComparison.Ordinal);

        // 2'-3" past the beam: the joists span 90" (7'-6"); a quarter of it, 1'-10 1/2", governs; 4 1/2" over.
        DeckCheckLine over = Line(Only(Drawing(typed with { Cantilever = In(27) }, code: Connecticut), Shipped), DeckCheckKind.Cantilever);
        Assert.False(over.Passing);
        Assert.StartsWith(
            "Cantilever 2'-3\" past the beam: allowed up to 1'-10 1/2\", the lesser of the row's 2'-0\" and 1/4 of the 7'-6\" span, 1'-10 1/2\"; over by 4 1/2\" (DCA 6-2015 Table 2",
            over.Text,
            StringComparison.Ordinal);
        Assert.Contains("Shorten the cantilever.", over.Text, StringComparison.Ordinal);

        // No cantilever, no line; the synthetic guide's table prints no overhang, so a cantilever is out of its scope.
        Assert.DoesNotContain(Only(Drawing(typed, code: Connecticut), Shipped).Lines, line => line.Kind == DeckCheckKind.Cantilever);
        DeckCheckLine synthetic = Line(Only(Drawing(Inputs() with { Cantilever = In(18) })), DeckCheckKind.Cantilever);
        Assert.Equal(
            "Cantilever 1'-6\" past the beam: Table ZZ-GUIDE-JOIST does not cover an overhang, so a cantilever of 1'-6\" past the beam is not checked: get it engineered." + Unreviewed,
            synthetic.Text);
        Assert.Equal("Cantilever 1'-6\" past the beam: No adopted code is chosen, so napkin cannot check this. Choose one in Project → Adopted code and site.", Line(Only(Drawing(Inputs() with { Cantilever = In(18) }) with { Code = null }), DeckCheckKind.Cantilever).Text);
    }

    [Fact]
    public void The_panel_is_offered_what_the_packs_tables_name_for_supports_and_species_and_nothing_is_filled_in()
    {
        LoadedPack ct = Shipped.Loaded.Single(), zz = Synthetic.Loaded.Single();

        Assert.Equal(["deck"], DeckCheck.SupportsOffered(ct));
        Assert.Equal(["Southern Pine", "Douglas Fir-Larch", "Hem-Fir", "Spruce-Pine-Fir", "Redwood", "Western Cedars", "Ponderosa Pine", "Red Pine"], DeckCheck.SpeciesOffered(ct));
        Assert.Equal(
            "CT 2022's deck tables name what a deck supports as: deck (anything else it carries is beyond the scope of DCA 6-2015, Table 2 note 1, p. 4; MINIMUM REQUIREMENTS & LIMITATIONS item 8, p. 2); "
            + "and species as: Southern Pine, Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine. "
            + "Type one in each box; nothing is filled in for you.",
            DeckCheck.InputsOffered(ct));

        Assert.Equal(["zz-deck"], DeckCheck.SupportsOffered(zz));
        Assert.Equal(["zz-fir", "zz-hem", "zz-cedar"], DeckCheck.SpeciesOffered(zz));
        Assert.Empty(DeckCheck.SupportsOffered(null));
        Assert.Empty(DeckCheck.SpeciesOffered(null));
        Assert.Null(DeckCheck.InputsOffered(null));
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
        Assert.Single(checks.Guides);
    }
}
