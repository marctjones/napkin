using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// DCA 6-2015 Appendix B under the shipped Connecticut pack (#42 slice B3, docs/design/deck-guide-pack.md §3.4): Table B1
/// (post heights, corner posts, p. B3), Table B2 (centre posts, p. B4) and Table B3 (footing sizes, p. B5), with Table B2
/// note 4's and Table B3 note 2's ×1.25 at a centre post under a continuous beam applied as the tables' declared factor.
/// Expected values were typed from the DCA 6 PDF (sha256 205d57b5…3009e, read 2026-09-27), never copied out of the pack;
/// the full row-by-row checks are packs/golden/us-ct-2022/dca6-table-b1/b2/b3.golden.json.
/// </summary>
public class Dca6AppendixBTests
{
    static LoadedPack Ct() => Fx.Loaded(PackLoader.Load(Fx.RealRoot, "us-ct-2022"));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static Length Ft(long feet, long inches = 0) => Length.FeetInches(feet, inches);

    /// <summary>DCA 6's scope for every lookup: a deck carrying only itself, 30 psf of snow, 12'-0" out from the house and 16'-0" along it.</summary>
    static readonly DeckScopeInputs Scope = new("deck", "Southern Pine", 30, Ft(12), Ft(16));

    /// <summary>An area of whole square feet, in square 1/1024″.</summary>
    static ExactFraction SquareFeet(long feet) => new((Int128)feet * Length.UnitsPerFoot * Length.UnitsPerFoot, 1);

    /// <summary>The worked example's middle post, Eq. B-1 (p. B1): B_L 72" × half of J_L 118 1/2" = 4266 sq in.</summary>
    static readonly ExactFraction Middle = new((Int128)In(72).Units * In(59, 1, 4).Units, 1);

    /// <summary>Its end post, Eq. B-2: half of B_L, 36", × 59 1/4" = 2133 sq in.</summary>
    static readonly ExactFraction End = new((Int128)In(36).Units * In(59, 1, 4).Units, 1);

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void Tables_B1_and_B2_print_250_rows_each_in_five_species_groups_with_their_NP_cells()
    {
        LoadedPack ct = Ct();
        DeckTable corner = ct.Deck.Posts[PostPosition.Corner], center = ct.Deck.Posts[PostPosition.Center];

        Assert.Equal(("B1", "p. B3", "B2", "p. B4"), (corner.Designation, corner.Source.Location, center.Designation, center.Source.Location));
        Assert.Equal(("Post Heights Based on Tributary Area for Corner Posts.", "Post Heights Based on Tributary Area for Center Posts."), (corner.Title, center.Title));

        // 25 areas (10 … 250 sq ft) × two posts (6x6, 4x4) × five species headings.
        Assert.All(new[] { corner, center }, table => Assert.Equal(250, table.Rows.Count));
        Assert.Equal(
            ["Southern Pine", "Douglas Fir-Larch", "Hem-Fir, Western Cedars", "Redwood", "Ponderosa Pine, Red Pine, SPF"],
            corner.SpeciesGroups.Select(group => group.Group));
        Assert.Equal(["Ponderosa Pine", "Red Pine", "Spruce-Pine-Fir"], corner.SpeciesGroups[4].Species);
        InputColumn area = corner.Inputs.Single(column => column.Name == "tributaryArea");
        Assert.Equal((BandKind.UpperBound, 250L), (area.Band, area.Domain!.Max.Magnitude));

        // p. B3 prints NP in 13 cells, p. B4 in 11; B2's 4x4 Redwood at 250 sq ft prints 1, not NP.
        Assert.Equal(13, corner.Rows.Count(row => row.NotPermitted));
        Assert.Equal(11, center.Rows.Count(row => row.NotPermitted));
        Assert.Equal(Ft(1), center.Rows.Single(row => row.Id == "r.rw.4x4.250").Height);
        Assert.True(corner.Rows.Single(row => row.Id == "r.pp-rp-spf.6x6.170").NotPermitted);
        Assert.Equal(Ft(2), corner.Rows.Single(row => row.Id == "r.pp-rp-spf.6x6.160").Height);

        // The design note's sample: centre post, 40 sq ft, Southern Pine 6x6: 14 ft (p. B4).
        Assert.Equal(Ft(14), center.Rows.Single(row => row.Id == "r.sp.6x6.40").Height);

        // Notes 1 and 3 on the title, note 2 on Douglas Fir-Larch, Hem-Fir and SPF; all shown, none encoded.
        Assert.All(new[] { corner, center }, table =>
        {
            Assert.Equal(["1", "2", "3"], table.Footnotes.Select(note => note.Id));
            Assert.Equal([FootnoteScope.Table, FootnoteScope.Rows, FootnoteScope.Table], table.Footnotes.Select(note => note.AppliesTo));
            Assert.Equal(150, table.Rows.Count(row => row.Footnotes.SequenceEqual(["2"])));
        });

        // B2 note 4 is its centre-post factor, not a footnote; B1 has none.
        Assert.Null(corner.CenterPostFactor);
        CenterPostFactor factor = center.CenterPostFactor!;
        Assert.Equal(("4", new ExactFraction(5, 4), "1.25"), (factor.Note, factor.Multiply, factor.Words));
        Assert.Equal("Tributary area shall be multiplied by 1.25 at center posts with beams not spliced (continuous).", factor.Text);
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void Table_B3_prints_100_rows_banded_on_area_and_soil_bearing_from_1500_psf()
    {
        DeckTable footing = Ct().Deck.Footing!;

        Assert.Equal(("B3", "p. B5", "dca6-2015"), (footing.Designation, footing.Source.Location, footing.Guide!.Id));
        Assert.Equal(100, footing.Rows.Count);
        InputColumn soil = footing.Inputs.Single(column => column.Name == "soilBearing");
        Assert.Equal((BandKind.LowerBound, 1500L, 3000L), (soil.Band, soil.Domain!.Min.Magnitude, soil.Domain.Max.Magnitude));

        // The design note's sample: 40 sq ft at 1,500 psf: 16" round, 15" square, 6" thick (p. B5).
        Assert.Equal(new FootingSize(In(16), In(15), In(6)), footing.Rows.Single(row => row.Id == "r.40.1500").Footing);
        Assert.Equal(new FootingSize(In(43), In(38), In(19)), footing.Rows.Single(row => row.Id == "r.250.1500").Footing);

        Assert.Equal("1", Assert.Single(footing.Footnotes).Id);
        Assert.Equal(("2", new ExactFraction(5, 4)), (footing.CenterPostFactor!.Note, footing.CenterPostFactor.Multiply));
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void The_worked_examples_posts_and_footing_answer_with_the_centre_post_factor_at_the_middle_post()
    {
        LoadedPack ct = Ct();

        // End post, a corner post: 2133 sq in = 14.8 sq ft, the ≤ 20 row; a Southern Pine 4x4 may stand 6 ft (p. B3).
        DeckResult.Passes end = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckPost(ct, new PostRequest("4x4", In(18, 1, 2), "Southern Pine", new PostArea(End, PostPosition.Corner, true)), Scope));
        Assert.Equal(("B1", "r.sp.4x4.20", Ft(6)), (end.Table.Designation, end.Row.Id, end.Allowed));
        Assert.Null(end.Area!.Factor);

        // Middle post under a continuous beam: 4266 × 5/4 = 5332 1/2 sq in = 37.0 sq ft, the ≤ 40 row, 13 ft (p. B4).
        DeckResult.Passes middle = Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckPost(ct, new PostRequest("4x4", In(18, 1, 2), "Southern Pine", new PostArea(Middle, PostPosition.Center, true)), Scope));
        Assert.Equal(("B2", "r.sp.4x4.40", Ft(13)), (middle.Table.Designation, middle.Row.Id, middle.Allowed));
        Assert.Equal(new ExactFraction((Int128)In(72).Units * In(59, 1, 4).Units * 5, 4), middle.Area!.Looked);

        // Its footing on 2000 psf: the ≤ 40 row, 14" round or 13" square, 6" thick (p. B5); on a spliced beam, 29.6 sq ft reads
        // the ≤ 30 row, 12" round or 11" square.
        DeckResult.Sized footing = Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(ct, new PostArea(Middle, PostPosition.Center, true), 2000, Scope));
        Assert.Equal(("r.40.2000", new FootingSize(In(14), In(13), In(6))), (footing.Row.Id, footing.Row.Footing));
        Assert.Equal("2", footing.Area!.Factor!.Note);
        Assert.Equal("r.30.2000", Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(ct, new PostArea(Middle, PostPosition.Center, false), 2000, Scope)).Row.Id);

        // A corner post is never multiplied: its footing reads its own area.
        Assert.Null(Assert.IsType<DeckResult.Sized>(DeckEvaluator.SizeFooting(ct, new PostArea(Middle, PostPosition.Corner, true), 2000, Scope)).Area!.Factor);
    }

    [Fact]
    [Trait("Feature", "DECK-003")]
    public void Soft_soil_an_NP_cell_a_porch_roof_and_an_untyped_soil_value_each_answer_honestly()
    {
        LoadedPack ct = Ct();
        PostArea corner = new(SquareFeet(170), PostPosition.Corner, true);

        // Below the 1500 psf column: out of scope (p. 11: a soils investigation decides).
        DeckResult.OutOfScope soft = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(ct, corner, 1499, Scope));
        Assert.Equal("soilBearing", soft.Column);
        Assert.Contains("below the lowest band of table B3 (1500 psf)", soft.Explanation, StringComparison.Ordinal);
        Assert.Equal("soilBearing", Assert.IsType<DeckResult.InputMissing>(DeckEvaluator.SizeFooting(ct, corner, null, Scope)).Input);

        // A Spruce-Pine-Fir 6x6 corner post over 160 sq ft: p. B3 prints NP; at 160 sq ft, 2 ft.
        DeckResult.OutOfScope np = Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckPost(ct, new PostRequest("6x6", Ft(1), "Spruce-Pine-Fir", corner), Scope with { Species = "Spruce-Pine-Fir" }));
        Assert.Equal("r.pp-rp-spf.6x6.170", np.Row!.Id);
        Assert.StartsWith("Table B1 prints NP, no height, for a 6x6 post of Ponderosa Pine, Red Pine, SPF carrying up to 170 sq ft (row r.pp-rp-spf.6x6.170, p. B3", np.Explanation, StringComparison.Ordinal);
        Assert.Equal(Ft(2), Assert.IsType<DeckResult.Passes>(DeckEvaluator.CheckPost(ct, new PostRequest("6x6", Ft(2), "Spruce-Pine-Fir", corner with { Area = SquareFeet(160) }), Scope)).Allowed);

        // The guide's scope comes first for posts and footings too.
        Assert.Equal("s.loads", Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.CheckPost(ct, new PostRequest("6x6", Ft(2), "Southern Pine", corner), Scope with { Supports = "porch-roof" })).Limit!.Id);
        Assert.Equal("s.snow", Assert.IsType<DeckResult.OutOfScope>(DeckEvaluator.SizeFooting(ct, corner, 2000, Scope with { GroundSnowLoad = 41 })).Limit!.Id);
    }

    [Fact]
    public void A_post_table_the_base_layer_and_a_guide_both_declare_is_refused_naming_both_files()
    {
        InMemoryPackSource source = InMemoryPackSource.FromDirectory(Fx.RealRoot);
        string b1 = source.Text("layers/dca6-2015/deck/table-b1.json").Replace("\"source\": \"awc-dca6-2015\"", "\"source\": \"ct-csbc-2022\"", StringComparison.Ordinal);
        source.With("layers/irc-2021/deck/table-b1.json", b1);

        string refused = string.Join("\n", Fx.Invalid(PackLoader.Load(source, "us-ct-2022")).Problems.Select(problem => problem.Message));
        Assert.Contains("layers/irc-2021/deck/table-b1.json and layers/dca6-2015/deck/table-b1.json both declare a deck-post table for 'corner' posts", refused, StringComparison.Ordinal);
    }
}
