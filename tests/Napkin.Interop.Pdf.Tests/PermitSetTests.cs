using System.Globalization;
using System.Text.RegularExpressions;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

using static Napkin.Interop.Pdf.Tests.ReadBack;

namespace Napkin.Interop.Pdf.Tests;

/// <summary>
/// The permit set's framework (#225, docs/design/permit-set.md §3, §3.1, §4) against the SYNTHETIC
/// packs (made-up numbers; CodePacks/README.md): us-zz-frame's ZZ-HEADER sizes a 60″ opening in a
/// zz-roof wall at snow 30 as row r.s30.b, and has no wall-bracing provisions.
/// </summary>
public class PermitSetTests
{
    static readonly CodePacks One = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "one")]);
    static readonly CodeChoice Frame = new("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
    static readonly DateOnly Day = new(2026, 9, 27);
    static readonly LayerId WallLayer = LayerId.New(), OpeningLayer = LayerId.New();

    static Length In(long whole) => Length.Inches(whole);

    /// <summary>A 12 ft exterior wall, bearing unless said, carrying <paramref name="supports"/>, with a 60″ window.</summary>
    static Sketch Wall(string? supports = "zz-roof", bool bearing = true, int? snow = 30)
    {
        Box wall = new Box(EntityId.New(), WallLayer, Point3.Origin, In(144), Length.Inches(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            WallInputs = new WallInputs(supports, null) { Side = WallSide.Exterior, Bearing = bearing },
        };
        Box window = new Box(EntityId.New(), OpeningLayer, new Point3(In(12), Length.Zero, In(36)), In(60), Length.Inches(3, 1, 2), In(42), BoxFace.Top, Angle.Zero)
        {
            Name = "Window 1",
        };
        return Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wall)
            .WithEntity(window) with
        {
            Code = Frame,
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = snow },
        };
    }

    static PermitSet Set(Sketch sketch, SheetPaper? paper = null) => new(
        new TitleBlock("Window move", Day, SheetNotes.CodeLine(sketch, One), ScopeDisclaimer.Text + " This site plan is not a survey."),
        new FeetInchesFormat(16),
        paper ?? SheetPaper.Letter,
        PermitItems.Of(sketch, One, MaterialsLibrary.Shipped));

    static ReadBack Read(PermitSet set, params PermitSheet[] sheets)
    {
        using MemoryStream stream = new();
        PermitSetPdf.Document(set, sheets).Save(stream);
        return ReadBack.Open(stream.ToArray());
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void The_architect_and_engineer_scales_say_themselves_as_the_scale_rule_reads_them()
    {
        FeetInchesFormat format = new(16);
        Assert.Equal(
            ["1 1/2\" = 1'-0\"", "1\" = 1'-0\"", "3/4\" = 1'-0\"", "1/2\" = 1'-0\"", "3/8\" = 1'-0\"", "1/4\" = 1'-0\"", "3/16\" = 1'-0\"", "1/8\" = 1'-0\"", "3/32\" = 1'-0\""],
            SheetScale.Architect.Select(scale => scale.InWords(format)));
        Assert.Equal(["1\" = 10'", "1\" = 20'", "1\" = 30'", "1\" = 40'", "1\" = 50'", "1\" = 60'"], SheetScale.Engineer.Select(scale => scale.InWords(format)));
        Assert.Equal("1:48", SheetScale.Architect[5].Label);

        // 5 pt/in at most: 1:12 is 6, 1:16 is 4.5 — the largest that fits. Nothing on the list fits 0.1.
        Assert.Equal(new SheetScale(16, ScaleWords.Architect), SheetScale.Largest(SheetScale.Architect, 5));
        Assert.Null(SheetScale.Largest(SheetScale.Architect, 0.1));
        Assert.Equal(new SheetScale(240, ScaleWords.Engineer), SheetScale.Largest(SheetScale.Engineer, 0.3));
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void Each_result_is_sized_not_sized_or_not_checked_and_an_unsized_one_says_what_to_look_up()
    {
        IReadOnlyList<PermitItem> sized = PermitItems.Of(Wall(), One, MaterialsLibrary.Shipped);
        PermitItem header = sized[0];
        Assert.Equal(("Window 1: header", PermitStatus.Sized), (header.What, header.Status));
        Assert.Equal(string.Empty, header.Lookup);
        Assert.Contains(AdoptedCodeRef.UnreviewedText, header.Lines[1], StringComparison.Ordinal);

        // us-zz-frame has no bracing provisions: the wall's bracing is not sized, and says where to look.
        PermitItem bracing = sized[1];
        Assert.Equal(("Wall 1: bracing", PermitStatus.NotSized), (bracing.What, bracing.Status));
        Assert.Equal(
            "Look up: ZZ FRAME (IRC 2099), pack us-zz-frame rev 1, which has no wall-bracing provisions. Known: Wall 1, wall line 12'-0\", wall height 8'-0\"; "
            + "ground snow load 30 psf; ultimate wind speed: not entered; seismic design category: not entered.",
            bracing.Lookup);

        // What the wall supports not chosen: the header is not sized; the lookup names the table and the span.
        PermitItem missing = PermitItems.Of(Wall(supports: null), One, MaterialsLibrary.Shipped)[0];
        Assert.Equal(PermitStatus.NotSized, missing.Status);
        Assert.Equal(
            "Look up: ZZ FRAME (IRC 2099), pack us-zz-frame rev 1, Table ZZ-HEADER. Known: Wall 1, exterior, bearing; header span 5'-0\"; "
            + "what the wall supports: not chosen; ground snow load 30 psf; ultimate wind speed: not entered; seismic design category: not entered.",
            missing.Lookup);

        // A wall said not to bear: its opening's header is the person's own, not a code question.
        Assert.Equal(PermitStatus.NotChecked, PermitItems.Of(Wall(bearing: false), One, MaterialsLibrary.Shipped)[0].Status);

        // No code chosen: the lookup says so.
        Assert.StartsWith("Look up: " + CodeCheck.NoCodeSelectedText, PermitItems.Of(Wall() with { Code = null }, One, MaterialsLibrary.Shipped)[0].Lookup, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void A_set_with_anything_unsized_carries_the_banner_on_every_sheet_and_prints_the_worksheet()
    {
        PermitSet set = Set(Wall(supports: null));
        Assert.Equal("NOT A COMPLETE PERMIT SET — 2 items are not sized; see C1 and W1", PermitItems.Banner(set.Items));

        ReadBack read = Read(set, new PermitSheet("A1", "Plan", new SheetScale(48, ScaleWords.Architect), (ink, area) => ink.Line(area.Centre, new PagePoint(area.Centre.X + 72, area.Centre.Y), DrawingLines.Of(LineKind.Visible), null)));
        Assert.All(read.Text, text => Assert.Contains("NOT A COMPLETE PERMIT SET — 2 items are not sized; see C1 and W1", text, StringComparison.Ordinal));
        Assert.All(read.Text, text => Assert.Contains("This site plan is not a survey.", text, StringComparison.Ordinal));

        // The drawn sheet, then C1, then W1, each named in its title block.
        Assert.Contains("A1 Plan", read.Text[0], StringComparison.Ordinal);
        Assert.Contains("Scale 1:48", read.Text[0], StringComparison.Ordinal);
        Assert.Contains("1/4\" = 1'-0\"", read.Text[0], StringComparison.Ordinal);
        Assert.Contains(PermitSetPdf.CodePage, read.Text[1], StringComparison.Ordinal);
        Assert.Contains(PermitSetPdf.Worksheet, read.Text[2], StringComparison.Ordinal);

        // C1 lists every result with its status and the panel's words.
        string code = read.Letters[1];
        foreach (PermitItem item in set.Items)
        {
            Assert.Contains(Squash($"{item.What} — {PermitSetPdf.StatusWords(item.Status)}"), code, StringComparison.Ordinal);
            Assert.All(item.Lines, line => Assert.Contains(Squash(line), code, StringComparison.Ordinal));
        }

        // W1: each unsized item, what to look up, and blank rules for the answer (180 pt) and where it is printed (260 pt).
        string sheet = read.Letters[2];
        Assert.Contains(Squash(PermitSetPdf.WorksheetIntro), sheet, StringComparison.Ordinal);
        Assert.All(set.Items.Where(item => item.Status == PermitStatus.NotSized), item => Assert.Contains(Squash(item.Lookup), sheet, StringComparison.Ordinal));
        MatchCollection answers = Regex.Matches(read.Operators[2], @"0 G\n0\.8 w\n(\S+) (\S+) m\n(\S+) \2 l\nS");
        double[] lengths = [.. answers.Select(match => double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) - double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))];
        Assert.Equal(2, lengths.Count(length => Math.Abs(length - PermitSetPdf.AnswerBlank) < 1e-6));
        Assert.Equal(2, lengths.Count(length => Math.Abs(length - PermitSetPdf.CitationBlank) < 1e-6));
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void A_set_with_everything_sized_has_no_banner_and_no_worksheet_but_still_its_code_page()
    {
        // The window's header sized; this pack's missing bracing is left out by giving the design no wall line to brace.
        Sketch sketch = Wall();
        PermitSet set = Set(sketch) with { Items = [.. PermitItems.Of(sketch, One, MaterialsLibrary.Shipped).Where(item => item.Status == PermitStatus.Sized)] };
        Assert.Null(PermitItems.Banner(set.Items));
        ReadBack read = Read(set);
        string page = Assert.Single(read.Text);
        Assert.Contains(PermitSetPdf.CodePage, page, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT A COMPLETE", page, StringComparison.Ordinal);

        // With nothing asked, the code page says so.
        Assert.Contains(PermitSetPdf.NothingAsked, Assert.Single(Read(set with { Items = [] }).Text), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void Tabloid_is_17_by_11_inches_and_the_banner_band_sits_across_the_title_block()
    {
        PermitSet set = Set(Wall(supports: null), SheetPaper.Tabloid);
        ReadBack read = Read(set);
        Assert.Equal((1224.0, 792.0), (read.Document.Pages[0].Width, read.Document.Pages[0].Height));

        // The band: the title block's full width, 18 pt, at its top, in the visible line's 1.4 pt.
        TitleBlock title = set.Title with { Banner = PermitItems.Banner(set.Items) };
        SheetFrame frame = SheetFrame.For(title, SheetPaper.Tabloid);
        SheetFrame plain = SheetFrame.For(set.Title, SheetPaper.Tabloid);
        Assert.Equal(plain.TitleBlock.Height + SheetFrame.BannerBand, frame.TitleBlock.Height, 9);
        Assert.Contains($"0 G\n1.4 w\n36 {N(frame.TitleBlock.Top - 18)} 1152 18 re\nS", read.Operators[0], StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void Under_a_bracing_pack_a_short_wall_line_is_an_answer_and_a_missing_wind_speed_is_looked_up_in_its_section()
    {
        // us-zz-brace-a has wall-bracing provisions (ZZ-BRACE.1) and no header table.
        CodePacks brace = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "brace")]);
        Sketch sketch = Wall() with { Code = new CodeChoice("us-zz-brace-a", 1, CodeMode.Locked, new DateOnly(2026, 9, 25)) };

        Sketch windy = sketch with { Site = sketch.Site with { UltimateWindSpeedMph = 90, SeismicDesignCategory = "C" } };
        IReadOnlyList<PermitItem> answered = PermitItems.Of(windy, brace, MaterialsLibrary.Shipped);
        Assert.Equal(PermitStatus.Sized, Assert.Single(answered, item => item.What == "Wall 1: bracing").Status);
        PermitItem header = Assert.Single(answered, item => item.What == "Window 1: header");
        Assert.Equal(PermitStatus.NotSized, header.Status);
        Assert.Contains(", which has no header table for this wall. Known:", header.Lookup, StringComparison.Ordinal);
        Assert.EndsWith("ground snow load 30 psf; ultimate wind speed 90 mph; seismic design category C.", header.Lookup, StringComparison.Ordinal);

        PermitItem missing = Assert.Single(PermitItems.Of(sketch, brace, MaterialsLibrary.Shipped), item => item.What == "Wall 1: bracing");
        Assert.Equal(PermitStatus.NotSized, missing.Status);
        Assert.StartsWith("Look up: ZZ BRACE A (IRC 2099), pack us-zz-brace-a rev 1, Section ZZ-BRACE.1. Known: Wall 1, wall line 12'-0\"", missing.Lookup, StringComparison.Ordinal);

        // With no snow load entered, the lookup says so.
        Assert.Contains("ground snow load: not entered;", PermitItems.Of(Wall(supports: null, snow: null), One, MaterialsLibrary.Shipped)[0].Lookup, StringComparison.Ordinal);

        // With no code chosen, neither lookup names a table or a section.
        IReadOnlyList<PermitItem> none = PermitItems.Of(Wall() with { Code = null }, One, MaterialsLibrary.Shipped);
        Assert.All(none, item => Assert.StartsWith($"Look up: {CodeCheck.NoCodeSelectedText}. Known:", item.Lookup, StringComparison.Ordinal));
    }

    [Fact]
    public void A_header_whose_wall_does_not_say_its_side_or_is_interior_says_so_in_its_lookup()
    {
        Sketch sketch = Wall();
        Box wall = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Wall 1");
        PermitItem unsaid = PermitItems.Of(sketch.WithEntity(wall with { WallInputs = null }), One, MaterialsLibrary.Shipped)[0];
        Assert.Equal(PermitStatus.NotSized, unsaid.Status);
        Assert.Contains("Known: Wall 1, side not said, bearing;", unsaid.Lookup, StringComparison.Ordinal);

        // An interior bearing wall asks for the interior table, which us-zz-frame does not have.
        PermitItem interior = PermitItems.Of(sketch.WithEntity(wall with { WallInputs = wall.WallInputs! with { Side = WallSide.Interior } }), One, MaterialsLibrary.Shipped)[0];
        Assert.Equal(PermitStatus.NotSized, interior.Status);
        Assert.Contains("which has no header table for this wall. Known: Wall 1, interior, bearing;", interior.Lookup, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "PERMIT-001")]
    public void One_unsized_item_is_said_in_the_singular_and_a_not_checked_opening_is_listed_as_such()
    {
        PermitItem one = new("Window 1: header", PermitStatus.NotSized, ["Not checked: …"], "Look up: …");
        Assert.Equal("NOT A COMPLETE PERMIT SET — 1 item is not sized; see C1 and W1", PermitItems.Banner([one]));

        PermitSet set = Set(Wall(bearing: false));
        using MemoryStream stream = new();
        PermitSetPdf.Write(set, stream);
        ReadBack read = ReadBack.Open(stream.ToArray());
        Assert.Contains(Squash("Window 1: header — not checked"), read.Letters[0], StringComparison.Ordinal);
        Assert.Equal(PermitSetPdf.Document(set).PageCount, read.Text.Count);
    }

    [Fact]
    public void A_deck_line_napkin_cannot_answer_is_not_sized_and_one_it_answers_is()
    {
        // The synthetic deck, its footing depth not entered: the frost line is not answered.
        CodePacks packs = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]);
        LayerId deckLayer = LayerId.New();
        DeckInputs inputs = new(
            JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "6x6", 3, Length.Zero, "5/4x6", Length.Inches(0, 1, 8), true, "zz-deck", "zz-fir", null, null, null);
        Box deck = new(EntityId.New(), deckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = inputs,
        };
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), Length.Inches(5, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "House",
            Phase = Phase.Existing,
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(deckLayer, BuildingLayers.Deck)).WithEntity(house).WithEntity(deck) with
        {
            Code = new CodeChoice("us-zz-deck", 1, CodeMode.Locked, new DateOnly(2026, 9, 26)),
            Site = SiteValues.NotEntered with { SoilBearingPsf = 2000, FrostDepth = In(42), GroundSnowLoadPsf = 30 },
        };

        IReadOnlyList<PermitItem> items = PermitItems.Of(sketch, packs, MaterialsLibrary.Shipped);
        PermitItem frost = Assert.Single(items, item => item.What == "Deck 1: frost");
        Assert.Equal(PermitStatus.NotSized, frost.Status);
        Assert.StartsWith("Look up: ZZ DECK", frost.Lookup, StringComparison.Ordinal);
        Assert.Contains("Known: Deck 1 12'-0\" × 10'-0\", 3'-0\" above grade; joists 2x8 at 1'-4\"", frost.Lookup, StringComparison.Ordinal);
        Assert.Contains(items, item => item.What == "Deck 1: joists" && item.Status == PermitStatus.Sized);

        // Past the guide's snow scope (above 77 psf), the joists are out of scope: looked up in their table.
        PermitItem scope = Assert.Single(PermitItems.Of(sketch with { Site = sketch.Site with { GroundSnowLoadPsf = 80 } }, packs, MaterialsLibrary.Shipped), item => item.What == "Deck 1: joists");
        Assert.Equal(PermitStatus.NotSized, scope.Status);
        Assert.Contains("ZZ-GUIDE-JOIST", scope.Lookup, StringComparison.Ordinal);

        // With no species typed, the joists wait for it.
        PermitItem species = Assert.Single(PermitItems.Of(sketch.WithEntity(deck with { Deck = inputs with { Species = null } }), packs, MaterialsLibrary.Shipped), item => item.What == "Deck 1: joists");
        Assert.Equal(PermitStatus.NotSized, species.Status);
        Assert.Contains(", once ", species.Lookup, StringComparison.Ordinal);
        Assert.Contains("species not chosen", species.Lookup, StringComparison.Ordinal);

        // With no supports typed, the lookup says so too.
        Assert.Contains("supports not chosen", Assert.Single(PermitItems.Of(sketch.WithEntity(deck with { Deck = inputs with { Supports = null } }), packs, MaterialsLibrary.Shipped), item => item.What == "Deck 1: joists").Lookup, StringComparison.Ordinal);

        // A box on the deck layer with nothing typed for its frame: known only by its size.
        PermitItem bare = Assert.Single(PermitItems.Of(sketch.WithEntity(deck with { Deck = null }), packs, MaterialsLibrary.Shipped), item => item.What == "Deck 1: frame");
        Assert.EndsWith("Known: Deck 1 12'-0\" × 10'-0\", 3'-0\" above grade.", bare.Lookup, StringComparison.Ordinal);

        // Unframed: one unsized item, the refusal.
        Sketch unframed = sketch.WithEntity(deck with { Deck = inputs with { Joist = "zz-nothing" } });
        PermitItem refusal = Assert.Single(PermitItems.Of(unframed, packs, MaterialsLibrary.Shipped), item => item.What.StartsWith("Deck", StringComparison.Ordinal));
        Assert.Equal(("Deck 1: frame", PermitStatus.NotSized), (refusal.What, refusal.Status));
        Assert.All(Enum.GetValues<DeckCheckKind>(), kind => Assert.False(string.IsNullOrEmpty(PermitItems.Kind(kind))));
    }
}
