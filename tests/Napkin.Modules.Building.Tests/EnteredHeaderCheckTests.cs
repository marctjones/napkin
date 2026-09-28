using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// A header row entered by hand, routed by the code check (docs/design/manual-code-values.md §5,
/// §6, §13 slice A, #246): consulted only where napkin has no table and only while every input it
/// was entered for is exactly the live one; stale, naming what moved, otherwise; superseded by
/// napkin's own table; kept and ignored on a not-bearing wall; labelled in every string form.
/// </summary>
/// <remarks>
/// <c>Fixtures/entered-header.scene.json</c> is SYNTHETIC TEST DATA - NOT CODE VALUES: its row's
/// citation says so, its site values are 99s and a Z, and it follows the shipped CT pack only because
/// that pack has no header table — no number in it is, or claims to be, from any code. Every
/// expected string below is worked out by hand from the fixture, never read back from napkin.
/// </remarks>
public class EnteredHeaderCheckTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;
    static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "entered-header.scene.json");
    static readonly string RealPacks = Path.Combine(AppContext.BaseDirectory, "RealPacks");
    static readonly string RootOne = Path.Combine(AppContext.BaseDirectory, "CodePacks", "one");
    static readonly string BraceRoot = Path.Combine(AppContext.BaseDirectory, "CodePacks", "brace");

    static readonly CodePacks Ct = CodePacks.Discover([RealPacks]);
    static readonly CodePacks CtAndBrace = CodePacks.Discover([RealPacks, BraceRoot]);
    static readonly CodePacks FrameAndBrace = CodePacks.Discover([RootOne, BraceRoot]);

    static readonly EntityId WallId = new(new Guid("62000000-0000-4000-8000-000000000001"));
    static readonly EntityId WindowId = new(new Guid("62000000-0000-4000-8000-000000000002"));

    static Sketch Fixture() => Assert.IsType<Loaded>(SceneReader.ReadFile(FixturePath)).Sketch;

    static Box Window(Sketch sketch) => sketch.Find<Box>(WindowId)!;

    static Box WallBox(Sketch sketch) => sketch.Find<Box>(WallId)!;

    static EnteredHeader Row(Sketch sketch) => Window(sketch).EnteredHeader!;

    static Sketch WithWall(Sketch sketch, Func<WallInputs, WallInputs> change)
        => sketch.WithEntity(WallBox(sketch) with { WallInputs = change(WallBox(sketch).WallInputs!) });

    static Sketch WithWindow(Sketch sketch, Func<Box, Box> change) => sketch.WithEntity(change(Window(sketch)));

    static OpeningCheck Check(Sketch sketch, CodePacks? packs = null) => Assert.Single(CodeCheck.Of(sketch, packs ?? Ct));

    [Fact]
    public void The_fixture_loads_at_format_17_and_writes_back_identical()
    {
        string text = File.ReadAllText(FixturePath);
        Assert.Equal(text, SceneWriter.WriteToText(Fixture()));
        Assert.StartsWith(Row(Fixture()).Citation.Code, "SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099", StringComparison.Ordinal);
        Assert.IsType<Refused>(SceneReader.Read(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text.Replace("\"formatVersion\": 17", "\"formatVersion\": 16", StringComparison.Ordinal)))));
    }

    [Fact]
    public void Where_napkin_has_no_table_and_nothing_moved_the_check_is_the_entered_row_labelled_in_every_string_form()
    {
        OpeningCheck check = Check(Fixture());
        HeaderResult.Entered entered = Assert.IsType<HeaderResult.Entered>(check.Result);
        Assert.Equal(new MemberSpec(2, "2x10"), entered.Header);
        Assert.Equal(1, entered.JackStuds);
        Assert.Equal(2, entered.KingStuds);
        Assert.Null(check.Stale);
        Assert.Null(check.Superseded);

        const string Provenance = "ENTERED BY HAND — A. Person, 2026-09-27, from SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099, Table T-99, p. 99, row 9. "
                                  + "Typed from a copy of the code; not napkin's data, not reviewed by napkin.";
        CheckWords words = CodeCheck.Words(check, Library);
        Assert.Equal("ENTERED BY HAND — Header (2) 2x10, 1 jack stud and 2 king studs each side.", words.Headline);
        Assert.Equal(Provenance, words.Citation);
        Assert.Equal(
            string.Join(
                "\n",
                "Entered for the adopted code: pack us-ct-2022",
                "Entered for which side the wall is on: exterior",
                "Entered for what the wall supports: \"synthetic-roof\"",
                "Entered for the header span: 3'-0\"",
                "Entered for the ground snow load: 99 psf",
                "Entered for the wind speed: 999 mph",
                "Entered for the seismic design category: Z",
                "Entered for the frost depth: not entered",
                "Entered for the building width: 99'-0\"",
                "Entered for the roof live load: not entered"),
            words.Details);
        Assert.Equal(string.Empty, words.Interpolation);
        Assert.Equal(
            "(2) 2x10, 1 jack and 2 king each side (ENTERED BY HAND — A. Person, SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099 Table T-99 p. 99, row 9; not napkin's data)",
            CodeCheck.Short(check));
        Assert.Equal("ENTERED BY HAND — (2) 2x10, 1 jack, 2 king — " + Provenance, entered.ToString());
        foreach (string form in new[] { entered.ToString(), entered.Entry.ToString(), CodeCheck.Short(check), words.Headline, words.Citation })
        {
            Assert.StartsWith(EnteredRow.Tag, form.Replace("(2) 2x10, 1 jack and 2 king each side (", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void An_entered_rows_notes_and_a_lumber_the_library_does_not_have_are_said()
    {
        Sketch sketch = WithWindow(Fixture(), window => window with
        {
            EnteredHeader = window.EnteredHeader! with { Lumber = "2x99", Citation = window.EnteredHeader.Citation with { Notes = "footnote applied by hand" } },
        });
        CheckWords words = CodeCheck.Words(Check(sketch), Library);
        Assert.Equal("ENTERED BY HAND — Header (2) 2x99, 1 jack stud and 2 king studs each side. 2x99 is not in the materials library, so the header is not on the shopping list.", words.Headline);
        Assert.EndsWith("\nNotes: footnote applied by hand", words.Details, StringComparison.Ordinal);
    }

    public static TheoryData<string, string, string> Moves() => new()
    {
        // The input that moves, what it was when entered, what it is now — in the panel's words.
        { "headerSpan", "3'-0\"", "3'-6\"" },
        { "side", "exterior", "interior" },
        { "supports", "\"synthetic-roof\"", "\"synthetic-floor\"" },
        { "pack", "pack us-ct-2022", "pack us-zz-brace-a" },
        { "groundSnowLoad", "99 psf", "98 psf" },
        { "ultimateWindSpeed", "999 mph", "998 mph" },
        { "seismicDesignCategory", "Z", "Y" },
        { "frostDepth", "not entered", "3'-6\"" },
        { "buildingWidth", "99'-0\"", "98'-0\"" },
        { "roofLiveLoad", "not entered", "99 psf" },
    };

    [Theory]
    [MemberData(nameof(Moves))]
    public void Moving_any_one_of_the_ten_recorded_inputs_makes_the_row_stale_naming_exactly_that_input(string input, string was, string now)
    {
        Sketch sketch = Fixture();
        CodePacks packs = Ct;
        switch (input)
        {
            case "headerSpan":
                sketch = WithWindow(sketch, window => window with { Width = Length.Inches(42) });
                break;
            case "side":
                sketch = WithWall(sketch, wall => wall with { Side = WallSide.Interior });
                break;
            case "supports":
                sketch = WithWall(sketch, wall => wall with { Supports = "synthetic-floor" });
                break;
            case "pack":
                sketch = sketch with { Code = new CodeChoice("us-zz-brace-a", 1, CodeMode.Following, null) };
                packs = CtAndBrace;
                break;
            case "groundSnowLoad":
                sketch = sketch with { Site = sketch.Site with { GroundSnowLoadPsf = 98 } };
                break;
            case "ultimateWindSpeed":
                sketch = sketch with { Site = sketch.Site with { UltimateWindSpeedMph = 998 } };
                break;
            case "seismicDesignCategory":
                sketch = sketch with { Site = sketch.Site with { SeismicDesignCategory = "Y" } };
                break;
            case "frostDepth":
                sketch = sketch with { Site = sketch.Site with { FrostDepth = Length.Inches(42) } };
                break;
            case "buildingWidth":
                sketch = sketch with { Site = sketch.Site with { BuildingWidth = Length.Feet(98) } };
                break;
            case "roofLiveLoad":
                sketch = sketch with { Site = sketch.Site with { RoofLiveLoadPsf = 99 } };
                break;
        }

        OpeningCheck check = Check(sketch, packs);
        Assert.Equal(NoDataReason.NoTableForWallKind, Assert.IsType<HeaderResult.NoData>(check.Result).Reason);
        StaleRow stale = Assert.IsType<StaleRow>(check.Stale);
        Assert.Equal(Row(Fixture()), stale.Row);
        Assert.Equal([new MovedInput(input, was, now)], stale.Moved);
        Assert.Null(check.Superseded);
    }

    [Fact]
    public void Inputs_are_compared_as_values_so_a_span_a_1024th_wider_is_a_move_though_it_reads_the_same()
    {
        Sketch wider = WithWindow(Fixture(), window => window with { Width = window.Width + new Length(1) });
        StaleRow stale = Assert.IsType<StaleRow>(Check(wider).Stale);
        Assert.Equal("headerSpan", Assert.Single(stale.Moved).Input);
    }

    [Fact]
    public void No_code_an_unsaid_side_or_an_unsaid_bearing_makes_the_row_stale_naming_it()
    {
        OpeningCheck noCode = Check(Fixture() with { Code = null });
        Assert.Equal(NoDataReason.NoPackSelected, Assert.IsType<HeaderResult.NoData>(noCode.Result).Reason);
        Assert.Equal([new MovedInput("pack", "pack us-ct-2022", "no code")], noCode.Stale!.Moved);

        OpeningCheck noSide = Check(WithWall(Fixture(), wall => wall with { Side = null }));
        Assert.IsType<HeaderResult.InputMissing>(noSide.Result);
        Assert.Equal([new MovedInput("side", "exterior", "not said")], noSide.Stale!.Moved);

        OpeningCheck noBearing = Check(WithWall(Fixture(), wall => wall with { Bearing = null }));
        Assert.IsType<HeaderResult.InputMissing>(noBearing.Result);
        Assert.Equal([new MovedInput("bearing", "bearing", "not said")], noBearing.Stale!.Moved);

        OpeningCheck notChosen = Check(WithWall(Fixture(), wall => wall with { Supports = null }));
        Assert.Equal([new MovedInput("supports", "\"synthetic-roof\"", "not chosen")], notChosen.Stale!.Moved);
    }

    [Fact]
    public void A_stale_row_never_shows_its_number_and_says_what_moved()
    {
        CheckWords words = CodeCheck.Words(Check(WithWindow(Fixture(), window => window with { Width = Length.Inches(42) })), Library);
        Assert.EndsWith(
            " The row ENTERED BY HAND by A. Person on 2026-09-27 no longer applies: the header span moved from 3'-0\" to 3'-6\". Enter the row again, or remove it.",
            words.Headline,
            StringComparison.Ordinal);
        Assert.DoesNotContain("2x10", words.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("2x10", words.Citation + words.Details, StringComparison.Ordinal);
    }

    /// <summary>The fixture moved under us-zz-frame, whose ZZ-HEADER table has zz-roof rows (see CodeCheckTests' summary).</summary>
    static Sketch UnderATable(Length width, string? supports = "zz-roof")
    {
        Sketch sketch = WithWall(Fixture(), wall => wall with { Supports = supports });
        sketch = WithWindow(sketch, window => window with
        {
            Width = width,
            EnteredHeader = window.EnteredHeader! with { For = window.EnteredHeader.For with { Pack = "us-zz-frame", Supports = "zz-roof", Span = width, GroundSnowLoadPsf = 30 } },
        });
        return sketch with
        {
            Code = new CodeChoice("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25)),
            Site = sketch.Site with { GroundSnowLoadPsf = 30 },
        };
    }

    [Fact]
    public void Where_napkin_has_a_table_its_own_answer_stands_and_the_row_is_superseded_never_an_override()
    {
        // 36" under ZZ-HEADER, snow ≤ 30, zz-roof: ≤ 4'-1" is (1) 2x8 j1 k1, row r.s30.a — every
        // input the row records is exactly live, and still napkin's own table wins (§5.2, §5.3).
        Sketch sized = UnderATable(Length.Inches(36));
        OpeningCheck check = Check(sized, FrameAndBrace);
        HeaderResult.Sized result = Assert.IsType<HeaderResult.Sized>(check.Result);
        Assert.Equal(new MemberSpec(1, "2x8"), result.Header);
        Assert.Equal("r.s30.a", result.Citation.RowId);
        Assert.Equal(Row(sized), check.Superseded);
        Assert.Null(check.Stale);
        Assert.EndsWith(
            " napkin now answers this from its own table. The row ENTERED BY HAND by A. Person on 2026-09-27 said (2) 2x10, 1 jack and 2 king each side; remove it.",
            CodeCheck.Words(check, Library).Headline,
            StringComparison.Ordinal);

        // 98" is past 8'-1": beyond the table. A person's row never beats napkin's out-of-scope.
        OpeningCheck beyond = Check(UnderATable(Length.Inches(98)), FrameAndBrace);
        Assert.IsType<HeaderResult.OutOfScope>(beyond.Result);
        Assert.NotNull(beyond.Superseded);

        // A table exists but what the wall supports is not chosen: napkin asks, the row is not used.
        OpeningCheck unchosen = Check(UnderATable(Length.Inches(36), supports: null), FrameAndBrace);
        Assert.Equal(["supports"], Assert.IsType<HeaderResult.InputMissing>(unchosen.Result).Inputs);
        Assert.NotNull(unchosen.Superseded);
        Assert.Null(unchosen.Stale);
    }

    [Fact]
    public void A_not_bearing_wall_keeps_the_row_and_ignores_it()
    {
        Sketch sketch = WithWall(Fixture(), wall => wall with { Bearing = false });
        OpeningCheck check = Check(sketch);
        Assert.Null(check.Result);
        Assert.Null(check.Stale);
        Assert.Null(check.Superseded);
        Assert.Equal(
            "Wall 1 is marked not bearing, so napkin does not size this header from the code. No header chosen: choose one under Header in the wall's panel; "
            + "until then the header buys nothing. The header row entered by hand for this opening is kept but not used: napkin does not check a not-bearing wall's headers.",
            check.NotChecked);
        Assert.NotNull(Window(sketch).EnteredHeader);
    }

    [Fact]
    public void The_message_bar_says_each_change_to_an_entered_row()
    {
        Sketch live = Fixture();
        Sketch none = WithWindow(live, window => window with { EnteredHeader = null });
        const string Said = "(2) 2x10, 1 jack and 2 king each side (ENTERED BY HAND — A. Person, SYNTHETIC TEST DATA - NOT CODE VALUES, Test Code 2099 Table T-99 p. 99, row 9; not napkin's data)";

        Assert.Equal([$"Header for Window 1 is now entered by hand: {Said}."], Changes(none, live));
        Assert.Equal(
            ["Header for Window 1: the row ENTERED BY HAND no longer applies (the header span 3'-0\" → 3'-6\"); no data to check it against."],
            Changes(live, WithWindow(live, window => window with { Width = Length.Inches(42) })));
        Assert.Equal(["Header for Window 1: the row ENTERED BY HAND was removed; no data to check it against."], Changes(live, none));
        Assert.Equal(
            [$"Header for Window 1's entered row changed: (2) 2x10 → {Said.Replace("(2)", "(3)", StringComparison.Ordinal)}."],
            Changes(live, WithWindow(live, window => window with { EnteredHeader = window.EnteredHeader! with { Plies = 3 } })));

        // A live row that has not changed is not a change.
        Assert.Empty(Changes(live, live));
        Assert.Equal(1, CodeCheck.Report(CodeCheck.Of(live, Ct), CodeCheck.Of(live, Ct)).Unchanged);

        static IEnumerable<string> Changes(Sketch before, Sketch after) => CodeCheck.Changes(CodeCheck.Of(before, Ct), CodeCheck.Of(after, Ct));
    }

    /// <summary>
    /// Four windows in a 24 ft wall under us-zz-brace-a (no header table), three with rows entered
    /// under it: A 36" (1) 2x8 j1 k1, B 98" (2) 2x12 j2 k2, C 60" (2) 2x8 j1 k1; D 48" has none.
    /// </summary>
    static Sketch FourWindows()
    {
        LayerId wallLayer = LayerId.New();
        LayerId openingLayer = LayerId.New();
        Box wall = new Box(EntityId.New(), wallLayer, Point3.Origin, Length.Inches(288), Length.Inches(3, 1, 2), Length.Inches(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            WallInputs = new WallInputs("zz-roof", null) { Side = WallSide.Exterior, Bearing = true },
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(wallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(openingLayer, BuildingLayers.Opening))
            .WithEntity(wall) with
        {
            Code = new CodeChoice("us-zz-brace-a", 1, CodeMode.Following, null),
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 },
        };

        foreach ((string name, long offset, long width, EnteredHeader? row) in new (string, long, long, EnteredHeader?)[]
        {
            ("Window A", 6, 36, Row(1, "2x8", 1, 1, 36)),
            ("Window B", 48, 98, Row(2, "2x12", 2, 2, 98)),
            ("Window C", 152, 60, Row(2, "2x8", 1, 1, 60)),
            ("Window D", 218, 48, null),
        })
        {
            sketch = sketch.WithEntity(new Box(EntityId.New(), openingLayer, new Point3(Length.Inches(offset), Length.Zero, Length.Inches(36)), Length.Inches(width), Length.Inches(3, 1, 2), Length.Inches(42), BoxFace.Top, Angle.Zero)
            {
                Name = name,
                EnteredHeader = row,
            });
        }

        return sketch;

        static EnteredHeader Row(int plies, string lumber, int jacks, int kings, long span) => new(
            plies,
            lumber,
            jacks,
            kings,
            new EnteredCitation("SYNTHETIC TEST DATA - NOT CODE VALUES", "Table T-99", "p. 99", null),
            "A. Person",
            new DateOnly(2026, 9, 27),
            new EnteredHeaderInputs("us-zz-brace-a", WallSide.Exterior, "zz-roof", Length.Inches(span), 30, null, null, null, null, null));
    }

    [Fact]
    public void When_napkin_gains_a_table_the_changes_are_said_the_most_serious_first_and_the_switch_summary_counts_them()
    {
        Sketch braced = FourWindows();
        IReadOnlyList<OpeningCheck> before = CodeCheck.Of(braced, FrameAndBrace);
        Assert.Equal(3, before.Count(check => check.Result is HeaderResult.Entered));

        // ZZ-HEADER, snow ≤ 30, zz-roof: A 36" ≤ 4'-1" (1) 2x8 j1 k1 — the same as its row; B 98" is past
        // 8'-1"; C 60" ≤ 6'-1" (2) 2x10 j1 k2 — not its row's (2) 2x8; D 48" ≤ 4'-1" (1) 2x8.
        Sketch framed = braced with { Code = new CodeChoice("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25)) };
        IReadOnlyList<OpeningCheck> after = CodeCheck.Of(framed, FrameAndBrace);
        List<string> said = [.. CodeCheck.Changes(before, after)];

        Assert.Equal(4, said.Count);
        Assert.StartsWith("Header for Window B is now beyond Table ZZ-HEADER under ZZ FRAME's own table: get it engineered. The row ENTERED BY HAND ((2) 2x12) is superseded; remove it.", said[0], StringComparison.Ordinal);
        Assert.StartsWith("Header for Window A is now sized by napkin: (1) 2x8, 1 jack and 1 king each side (Table ZZ-HEADER row r.s30.a)", said[1], StringComparison.Ordinal);
        Assert.EndsWith("— the same as the row ENTERED BY HAND. Remove the entered row.", said[1], StringComparison.Ordinal);
        Assert.StartsWith("Header for Window C is now sized by napkin: (2) 2x10, 1 jack and 2 king each side (Table ZZ-HEADER row r.s30.b)", said[2], StringComparison.Ordinal);
        Assert.EndsWith("— instead of the row ENTERED BY HAND ((2) 2x8). Remove the entered row.", said[2], StringComparison.Ordinal);
        Assert.StartsWith("Header for Window D is now sized: (1) 2x8", said[3], StringComparison.Ordinal);

        BracingRecomputeReport noBracing = new(ValueList<BracingChange>.Empty, 0);
        AdoptedCodeRef frame = FrameAndBrace.Resolve(framed.Code).Pack!.Code;
        Assert.EndsWith(": every result recomputed; 4 changed, 1 newly flagged, none can no longer be computed.", CodeCheck.SwitchSummary(frame, CodeCheck.Report(before, after), noBracing));

        // Another code with no table: every row was entered under the first, so each stops applying.
        Sketch other = braced with { Code = new CodeChoice("us-zz-brace-b", 1, CodeMode.Following, null) };
        IReadOnlyList<OpeningCheck> stale = CodeCheck.Of(other, FrameAndBrace);
        // D, with no row, still has no answer: only the pack its No data names differs (not counted).
        List<string> switched = [.. CodeCheck.Changes(before, stale)];
        Assert.Equal(3, switched.Count(sentence => sentence.Contains("no longer applies (the adopted code pack us-zz-brace-a → pack us-zz-brace-b)", StringComparison.Ordinal)));
        Assert.StartsWith("Header for Window D still cannot be checked", Assert.Single(switched, sentence => !sentence.Contains(EnteredRow.Tag, StringComparison.Ordinal)), StringComparison.Ordinal);
        AdoptedCodeRef braceB = FrameAndBrace.Resolve(other.Code).Pack!.Code;
        Assert.EndsWith(": every result recomputed; 3 changed, none newly flagged, 3 can no longer be computed.", CodeCheck.SwitchSummary(braceB, CodeCheck.Report(before, stale), noBracing));
    }

    [Fact]
    public void An_entered_row_frames_the_opening_as_a_sized_one_does()
    {
        OpeningCheck check = Check(Fixture());
        FramingOptions options = CodeCheck.Framing([check], Library);
        Assert.Equal(1, options.JacksPerSide!(check.Opening));
        Assert.Equal(2, options.KingsPerSide!(check.Opening));
        HeaderMember header = Assert.IsType<HeaderMember>(options.Header!(check.Opening));
        Assert.Equal(2, header.Plies);
        Assert.Equal("2x10", header.Stock.Name);
        Assert.False(options.Chosen!(check.Opening));

        // What the shopping list's Framing note says for it (CutListWindow.CodeCheckNote) is Short: tagged.
        Assert.Contains(EnteredRow.Tag, CodeCheck.Short(check), StringComparison.Ordinal);

        // A stale row frames nothing: it is no data.
        OpeningCheck stale = Check(WithWindow(Fixture(), window => window with { Width = Length.Inches(42) }));
        FramingOptions none = CodeCheck.Framing([stale], Library);
        Assert.Null(none.JacksPerSide!(stale.Opening));
        Assert.Null(none.Header!(stale.Opening));
    }

    [Fact]
    public void The_stale_and_superseded_sentences_refuse_nothing()
    {
        Assert.Throws<ArgumentNullException>(() => CodeCheck.StaleText(null!));
        Assert.Throws<ArgumentNullException>(() => CodeCheck.SupersededText(null!));
        Assert.Equal("the header span moved from 3'-0\" to 3'-6\"", new MovedInput("headerSpan", "3'-0\"", "3'-6\"").ToString());
    }
}
