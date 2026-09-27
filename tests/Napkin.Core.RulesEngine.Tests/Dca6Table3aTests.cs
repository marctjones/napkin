using Napkin.Core.Geometry;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// DCA 6-2015 Table 3A, dimension-lumber deck beam spans (p. 6; the document's other "Table 3A", joist hanger
/// capacity on p. 9, is not this one), under the shipped Connecticut pack (#41 slice B2, docs/design/deck-guide-pack.md
/// §3.2). Expected values were typed from the DCA 6 PDF (sha256 205d57b5…3009e, read 2026-09-27), never copied out
/// of the pack; the full row-by-row check is packs/golden/us-ct-2022/dca6-table-3a.golden.json.
/// </summary>
public class Dca6Table3aTests
{
    static LoadedPack Ct() => Fx.Loaded(PackLoader.Load(Fx.RealRoot, "us-ct-2022"));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static Length Ft(long feet, long inches = 0) => Length.FeetInches(feet, inches);

    /// <summary>A beam under DCA 6's scope: a deck carrying only itself, 30 psf of snow, 12'-0" out from the house and 16'-0" along it.</summary>
    static SpanRequest Beam(string member, Length span, Length joists, string species = "Southern Pine", string supports = "deck")
        => new(member, span, supports, species, null, joists, GroundSnowLoad: 30, DeckLength: Ft(12), DeckWidth: Ft(16));

    static DeckResult Check(SpanRequest request) => DeckEvaluator.CheckSpan(Ct(), SpanUse.DeckBeam, request);

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void Table_3A_prints_168_rows_in_two_species_groups_banded_on_the_joist_span_up_to_18_feet()
    {
        DeckTable table = Ct().Deck.Spans[SpanUse.DeckBeam];

        Assert.Equal(("3A", "p. 6", "dca6-2015"), (table.Designation, table.Source.Location, table.Guide!.Id));
        Assert.StartsWith("Dimension Lumber Deck Beam Spans (LB) Supporting a Single Span of Joists", table.Title, StringComparison.Ordinal);

        // Southern Pine: 8 sizes × 7 columns; the rest: 12 printed sizes, four of them two-name cells, so 16 × 7.
        Assert.Equal(168, table.Rows.Count);
        Assert.Equal(["Southern Pine", "Douglas Fir-Larch, Hem-Fir, Spruce-Pine-Fir, Redwood, Western Cedars, Ponderosa Pine, Red Pine"], table.SpeciesGroups.Select(group => group.Group));
        Assert.Equal(56, table.Rows.Count(row => row.Inputs["species"].Symbol == "Southern Pine"));
        InputColumn joists = table.Inputs.Single(column => column.Name == "joistSpan");
        Assert.Equal((BandKind.UpperBound, Ft(18)), (joists.Band, new Length(joists.Domain!.Max.Magnitude)));

        // Notes 1 and 4 on every row; 2 and 3 on the second group's; all shown, none encoded.
        Assert.Equal(["1", "2", "3", "4"], table.Footnotes.Select(note => note.Id));
        Assert.All(table.Footnotes, note => Assert.Equal(FootnoteEncoding.NotEncoded, note.EncodedAs));
        Assert.Equal([FootnoteScope.Table, FootnoteScope.Rows, FootnoteScope.Rows, FootnoteScope.Table], table.Footnotes.Select(note => note.AppliesTo));
        Assert.All(table.Rows.Where(row => row.Inputs["species"].Symbol != "Southern Pine"), row => Assert.Equal(["2", "3"], row.Footnotes));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void The_worked_example_passes_on_its_span_between_post_faces()
    {
        // (2) 2x10 Southern Pine carrying 9'-9" of joists: the ≤ 10' column, 7'-9" (p. 6). Three 4x4 posts under
        // a 12'-0" deck, L_B face to face of posts (Figure 3, p. 7): (144 − 3 × 3 1/2) ÷ 2 = 66 3/4": passes.
        DeckResult.Passes passes = Assert.IsType<DeckResult.Passes>(Check(Beam("(2) 2x10", In(66, 3, 4), Ft(9, 9))));
        Assert.Equal(("r.sp.2-2x10.10", Ft(7, 9), "Southern Pine"), (passes.Row.Id, passes.Allowed, passes.Group!.Group));

        // Hem-Fir reads as the second group: its (2) 2x10 at ≤ 10' allows 6'-3", so 66 3/4" passes, 75 1/4" is 1/4" over.
        DeckResult.Short hem = Assert.IsType<DeckResult.Short>(Check(Beam("(2) 2x10", In(75, 1, 4), Ft(9, 9), "Hem-Fir")));
        Assert.Equal(("r.dfl-rw.2-2x10.10", Ft(6, 3), In(0, 1, 4)), (hem.Row.Id, hem.Allowed, hem.Over));
    }

    [Fact]
    [Trait("Feature", "DECK-002")]
    public void A_two_name_cell_is_two_rows_with_the_same_span_and_Southern_Pine_prints_no_solid_beam()
    {
        // "3x8 or 2-2x8", Redwood, ≤ 10': 5'-1" for either (p. 6).
        DeckResult.Passes solid = Assert.IsType<DeckResult.Passes>(Check(Beam("(1) 3x8", Ft(5, 1), Ft(10), "Redwood")));
        DeckResult.Passes plies = Assert.IsType<DeckResult.Passes>(Check(Beam("(2) 2x8", Ft(5, 1), Ft(10), "Redwood")));
        Assert.Equal(("r.dfl-rw.3x8.10", "r.dfl-rw.2-2x8.10"), (solid.Row.Id, plies.Row.Id));
        Assert.Equal(solid.Row.Source.Location.Replace("its 3x8", "its 2-2x8", StringComparison.Ordinal), plies.Row.Source.Location);

        // The Southern Pine rows are built-up 2x members only.
        Assert.Equal("member", Assert.IsType<DeckResult.OutOfScope>(Check(Beam("(1) 4x8", Ft(5), Ft(10)))).Column);

        // The last column is 18'; 18'-0 1/1024" of joists is past it.
        Assert.IsType<DeckResult.Passes>(Check(Beam("(3) 2x12", Ft(8, 7), Ft(18))));
        Assert.Equal("joistSpan", Assert.IsType<DeckResult.OutOfScope>(Check(Beam("(3) 2x12", Ft(8), Ft(18) + new Length(1)))).Column);

        // The guide's scope comes first: a porch roof on the deck is beyond it.
        Assert.Equal("s.loads", Assert.IsType<DeckResult.OutOfScope>(Check(Beam("(2) 2x10", In(70), Ft(9, 9), supports: "porch-roof"))).Limit!.Id);
    }
}
