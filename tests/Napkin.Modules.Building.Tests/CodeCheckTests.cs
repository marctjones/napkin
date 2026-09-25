using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The code check on an opening, against the SYNTHETIC packs in <c>CodePacks/</c> (their numbers are
/// made up; see its README). Every expected row is worked out by hand from that fixture's rows in the
/// comments, never read back from napkin's output. The ZZ-HEADER rows used here, for supports
/// "zz-roof":
/// <code>
/// snow ≤ 30: span ≤ 4'-1" (1) 2x8 j1 k1 [r.s30.a]; ≤ 6'-1" (2) 2x10 j1 k2 [r.s30.b]; ≤ 8'-1" (2) 2x12 j2 k2 [r.s30.c]
/// snow ≤ 60: span ≤ 3'-1" (1) 2x8 j1 k1 [r.s60.a]; ≤ 5'-1" (2) 2x10 j2 k1 [r.s60.b]
/// </code>
/// and for "zz-roof-floor", snow ≤ 30: ≤ 3'-1" (2) 2x10 j1 k1 [rf.s30.a]. Revision 2 (root <c>two/</c>)
/// changes r.s30.b to (2) 2x12. <c>us-zz-other</c>'s one row: zz-roof, snow ≤ 60, ≤ 5'-1" (3) 2x10 [o.a].
/// </summary>
public class CodeCheckTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();

    static readonly string RootOne = Path.Combine(AppContext.BaseDirectory, "CodePacks", "one");
    static readonly string RootTwo = Path.Combine(AppContext.BaseDirectory, "CodePacks", "two");
    static readonly string RealPacks = Path.Combine(AppContext.BaseDirectory, "RealPacks");

    static readonly CodePacks One = CodePacks.Discover([RootOne]);
    static readonly CodePacks Both = CodePacks.Discover([RootOne, RootTwo]);

    static readonly CodeChoice Frame = new("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    /// <summary>
    /// A 12 ft 2x4 wall (144 × 3 1/2 × 96 in) carrying <paramref name="supports"/>, and a window
    /// <paramref name="width"/> wide at <paramref name="offset"/>, sill 36 in, 42 in tall.
    /// </summary>
    static (Sketch Sketch, EntityId Window) Design(Length width, string? supports = "zz-roof", int? snow = 30, CodeChoice? code = null, long offset = 12)
    {
        Box wall = new Box(EntityId.New(), WallLayer, Point3.Origin, In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            WallInputs = new WallInputs(supports, null).OrNull(),
        };
        Box window = new Box(EntityId.New(), OpeningLayer, new Point3(In(offset), Length.Zero, In(36)), width, In(3, 1, 2), In(42), BoxFace.Top, Angle.Zero)
        {
            Name = "Window 1",
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wall)
            .WithEntity(window) with
        {
            Code = code ?? Frame,
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = snow },
        };
        return (sketch, window.Id);
    }

    static HeaderResult Check(Sketch sketch, CodePacks? packs = null) => Assert.Single(CodeCheck.Of(sketch, packs ?? One)).Result;

    static void AssertSized(HeaderResult result, int plies, string nominal, int jacks, int kings, string row)
    {
        HeaderResult.Sized sized = Assert.IsType<HeaderResult.Sized>(result);
        Assert.Equal(new MemberSpec(plies, nominal), sized.Header);
        Assert.Equal(jacks, sized.JackStuds);
        Assert.Equal(kings, sized.KingStuds);
        Assert.Equal(row, sized.Citation.RowId);
        Assert.Equal("ZZ-HEADER", sized.Citation.Table);
    }

    [Fact]
    public void The_synthetic_packs_load()
    {
        Assert.Empty(Both.Invalid);
        Assert.Equal(["us-zz-frame", "us-zz-other", "us-zz-frame"], Both.Loaded.Select(pack => pack.Manifest.Id));
        Assert.Equal(["zz-roof", "zz-roof-floor"], CodeCheck.SupportsChoices(One.Loaded[0]));
        Assert.Empty(CodeCheck.SupportsChoices(null));
    }

    // Span = the opening's rough width. Snow 30 → the ≤ 30 band. Each edge is inclusive (a capacity
    // band holds up to and including its span); one 1/1024 in past it is the next row.
    [Theory]
    [Trait("Feature", "BLD-003")]
    [InlineData(36, 0, 1, "2x8", 1, 1, "r.s30.a")]      // 36 ≤ 49 (4'-1")
    [InlineData(49, 0, 1, "2x8", 1, 1, "r.s30.a")]      // exactly 49: the edge is in
    [InlineData(49, 1, 2, "2x10", 1, 2, "r.s30.b")]     // 49 1/1024 > 49, ≤ 73 (6'-1")
    [InlineData(60, 0, 2, "2x10", 1, 2, "r.s30.b")]
    [InlineData(73, 0, 2, "2x10", 1, 2, "r.s30.b")]     // exactly 73
    [InlineData(73, 1, 2, "2x12", 2, 2, "r.s30.c")]     // past 73, ≤ 97 (8'-1")
    [InlineData(97, 0, 2, "2x12", 2, 2, "r.s30.c")]     // exactly 97: the table's last span
    public void The_header_follows_the_span_across_the_band_edges(long inches, long overBy1024, int plies, string nominal, int jacks, int kings, string row)
    {
        (Sketch sketch, _) = Design(In(inches) + new Length(overBy1024));
        AssertSized(Check(sketch), plies, nominal, jacks, kings, row);
    }

    [Fact]
    [Trait("Feature", "BLD-003")]
    public void The_snow_band_and_what_the_wall_supports_pick_the_rows()
    {
        // Snow 31 is past the ≤ 30 band, in ≤ 60: 36 ≤ 37 (3'-1") → r.s60.a; 49 ≤ 61 (5'-1") → r.s60.b.
        AssertSized(Check(Design(In(36), snow: 31).Sketch), 1, "2x8", 1, 1, "r.s60.a");
        AssertSized(Check(Design(In(49), snow: 60).Sketch), 2, "2x10", 2, 1, "r.s60.b");

        // A roof and a floor, snow 30: 36 ≤ 37 → rf.s30.a.
        AssertSized(Check(Design(In(36), supports: "zz-roof-floor").Sketch), 2, "2x10", 1, 1, "rf.s30.a");
    }

    [Fact]
    [Trait("Feature", "RUL-002")]
    public void A_sized_result_carries_the_citation_the_engine_gives_with_its_trace()
    {
        HeaderResult.Sized sized = Assert.IsType<HeaderResult.Sized>(Check(Design(In(60)).Sketch));

        Assert.Equal("us-zz-frame", sized.Citation.Code.PackId);
        Assert.Equal(1, sized.Citation.Code.Revision);
        Assert.Equal("ZZ FRAME", sized.Citation.Code.ShortName);
        Assert.Equal("IRC 2099", sized.Citation.Code.BaseCode);
        Assert.Equal(
            ["supports zz-roof → = zz-roof", "groundSnowLoad 30 psf → ≤ 30 psf", "headerSpan 5'-0\" → ≤ 6'-1\""],
            sized.Citation.Trace.Select(match => match.ToString()));

        CheckWords words = CodeCheck.Words(sized, Library);
        Assert.Equal("Header (2) 2x10, 1 jack stud and 2 king studs each side.", words.Headline);
        Assert.Equal(sized.Citation.ToString(), words.Citation);
        Assert.Contains("Table ZZ-HEADER", words.Citation, StringComparison.Ordinal);
        Assert.Contains("row r.s30.b", words.Citation, StringComparison.Ordinal);
        Assert.Contains("How it was found: groundSnowLoad 30 psf → ≤ 30 psf", words.Details, StringComparison.Ordinal);
        Assert.Contains("Footnote a: SYNTHETIC footnote a", words.Details, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "BLD-004")]
    public void Past_the_last_span_is_out_of_scope_citing_the_limit_and_shows_no_size()
    {
        // 97 in is r.s30.c's span, the longest for zz-roof at snow ≤ 30; 97 1/1024 is past it.
        HeaderResult.OutOfScope beyond = Assert.IsType<HeaderResult.OutOfScope>(Check(Design(In(97) + new Length(1)).Sketch));
        Assert.Equal(OutOfScopeReason.SpanExceedsTable, beyond.Reason);
        Assert.Equal("r.s30.c", beyond.Limit.RowId);

        CheckWords words = CodeCheck.Words(beyond, Library);
        Assert.StartsWith("This opening is beyond what Table ZZ-HEADER covers: ", words.Headline, StringComparison.Ordinal);
        Assert.Contains("(8'-1\", row r.s30.c)", words.Headline, StringComparison.Ordinal);
        Assert.EndsWith("napkin stops here: get this header engineered.", words.Headline, StringComparison.Ordinal);
        Assert.DoesNotContain("2x", words.Headline, StringComparison.Ordinal);
        Assert.StartsWith("Limit: ", words.Citation, StringComparison.Ordinal);

        // At snow ≤ 60 the last span is r.s60.b's 61 in.
        HeaderResult.OutOfScope heavier = Assert.IsType<HeaderResult.OutOfScope>(Check(Design(In(61) + new Length(1), snow: 45).Sketch));
        Assert.Equal("r.s60.b", heavier.Limit.RowId);

        // Snow 61 is heavier than the table's heaviest band, 60.
        Assert.Equal(OutOfScopeReason.InputAboveTableBands, Assert.IsType<HeaderResult.OutOfScope>(Check(Design(In(36), snow: 61).Sketch)).Reason);

        // No size is bought for it: the header stays unsized in the frame.
        (Sketch sketch, _) = Design(In(97) + new Length(1));
        WallFraming framing = Assert.Single(FramingList.Of(sketch, Library, CodeCheck.Framing(CodeCheck.Of(sketch, One), Library)));
        Assert.Null(Assert.Single(framing.Pieces, piece => piece.Role == FramingRole.Header).Stock);
    }

    [Fact]
    [Trait("Feature", "BLD-001")]
    public void A_snow_load_not_entered_is_input_missing_never_a_default()
    {
        HeaderResult.InputMissing missing = Assert.IsType<HeaderResult.InputMissing>(Check(Design(In(36), snow: null).Sketch));
        Assert.Equal(["groundSnowLoad"], missing.Inputs);
        Assert.Equal("ZZ-HEADER", missing.Table);

        CheckWords words = CodeCheck.Words(missing, Library);
        Assert.Equal(
            "Not checked: the ground snow load is not entered, and napkin never assumes a value. Enter the site values under Edit → Adopted code and site.",
            words.Headline);
    }

    [Fact]
    [Trait("Feature", "BLD-001")]
    public void What_the_wall_supports_not_chosen_is_input_missing_naming_the_wall()
    {
        HeaderResult.InputMissing missing = Assert.IsType<HeaderResult.InputMissing>(Check(Design(In(36), supports: null).Sketch));
        Assert.Equal(["supports"], missing.Inputs);
        Assert.Contains("what Wall 1 supports", missing.Explanation, StringComparison.Ordinal);
        Assert.Contains("under Supports in the wall's panel", CodeCheck.Words(missing, Library).Headline, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-007")]
    public void With_no_code_chosen_or_the_shipped_empty_pack_there_is_no_data_and_no_guess()
    {
        (Sketch sketch, _) = Design(In(36));
        HeaderResult.NoData none = Assert.IsType<HeaderResult.NoData>(Check(sketch with { Code = null }));
        Assert.Equal(NoDataReason.NoPackSelected, none.Reason);
        Assert.Equal("No code selected: choose one under Edit → Adopted code and site.", CodeCheck.Words(none, Library).Headline);

        // The shipped Connecticut pack: its IRC base tables are not loaded (docs/rules-engine.md).
        CodePacks shipped = CodePacks.Discover([RealPacks]);
        LoadedPack ct = Assert.Single(shipped.Loaded);
        Assert.Equal("base tables not loaded", ct.StatusLabel);
        Assert.Empty(CodeCheck.SupportsChoices(ct));
        HeaderResult.NoData empty = Assert.IsType<HeaderResult.NoData>(
            Check(sketch with { Code = new CodeChoice("us-ct-2022", 1, CodeMode.Following, null) }, shipped));
        Assert.Equal(NoDataReason.NoTableForWallKind, empty.Reason);
        CheckWords words = CodeCheck.Words(empty, Library);
        Assert.StartsWith(empty.Explanation, words.Headline, StringComparison.Ordinal);
        Assert.EndsWith("Where to add tables: docs/rules-engine.md", words.Headline, StringComparison.Ordinal);

        // A pack the project names that is not installed.
        HeaderResult.NoData missing = Assert.IsType<HeaderResult.NoData>(Check(sketch with { Code = Frame with { PackId = "us-zz-gone" } }));
        Assert.Contains("us-zz-gone", missing.Explanation, StringComparison.Ordinal);
        Assert.Contains("not installed", missing.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "RUL-008")]
    public void Locked_keeps_its_revision_and_following_takes_the_newer_one()
    {
        // A 60 in window at snow 30 is r.s30.b: (2) 2x10 in revision 1, (2) 2x12 in revision 2.
        (Sketch sketch, _) = Design(In(60));

        HeaderResult.Sized locked = Assert.IsType<HeaderResult.Sized>(Check(sketch, Both));
        Assert.Equal(1, locked.Citation.Code.Revision);
        Assert.Equal(new MemberSpec(2, "2x10"), locked.Header);

        HeaderResult.Sized following = Assert.IsType<HeaderResult.Sized>(Check(sketch with { Code = new CodeChoice("us-zz-frame", 1, CodeMode.Following, null) }, Both));
        Assert.Equal(2, following.Citation.Code.Revision);
        Assert.Equal(new MemberSpec(2, "2x12"), following.Header);

        // Locked to revision 1 with only revision 2 installed: nothing computed under a revision the
        // project did not choose.
        HeaderResult.NoData stale = Assert.IsType<HeaderResult.NoData>(Check(sketch, CodePacks.Discover([RootTwo])));
        Assert.Contains("locked to code pack us-zz-frame revision 1", stale.Explanation, StringComparison.Ordinal);
        Assert.Contains("revision 2", stale.Explanation, StringComparison.Ordinal);

        Assert.Equal(new CodeSelection("us-zz-frame", 1, CodeLockMode.Locked, new DateOnly(2026, 9, 25)), CodeCheck.Selection(Frame));
        Assert.Null(CodeCheck.Selection(null));
    }

    [Fact]
    [Trait("Feature", "RUL-008")]
    public void A_recompute_names_every_changed_header_most_serious_first()
    {
        (Sketch small, EntityId window) = Design(In(36));
        ImmutableArray<OpeningCheck> before = CodeCheck.Of(small, One);

        // Widened to 60: (1) 2x8 → (2) 2x10, r.s30.b.
        Sketch wider = small.WithEntity(small.Find<Box>(window)! with { Width = In(60) });
        Assert.Equal(
            ["Header for Window 1 changed: (1) 2x8 → (2) 2x10, 1 jack and 2 king each side (Table ZZ-HEADER row r.s30.b)."],
            CodeCheck.Changes(before, CodeCheck.Of(wider, One)));

        // Switching packs: us-zz-other's o.a, (3) 2x10, needs snow ≤ 60 and span ≤ 61: 36 is in.
        Sketch other = small with { Code = new CodeChoice("us-zz-other", 1, CodeMode.Following, null) };
        Assert.Equal(
            ["Header for Window 1 changed: (1) 2x8 → (3) 2x10, 1 jack and 1 king each side (Table ZZ-OTHER-HEADER row o.a)."],
            CodeCheck.Changes(before, CodeCheck.Of(other, One)));

        // Past the table, and the snow cleared: each said, nothing kept.
        Sketch beyond = small.WithEntity(small.Find<Box>(window)! with { Width = In(98) });
        Assert.Equal(["Header for Window 1 is now beyond Table ZZ-HEADER: get it engineered."], CodeCheck.Changes(before, CodeCheck.Of(beyond, One)));
        Sketch cleared = small with { Site = SiteValues.NotEntered };
        Assert.Equal(
            ["Header for Window 1 is no longer sized: not checked: the ground snow load not entered."],
            CodeCheck.Changes(before, CodeCheck.Of(cleared, One)));
        Assert.Equal(
            ["Header for Window 1 is now sized: (1) 2x8, 1 jack and 1 king each side (Table ZZ-HEADER row r.s30.a)."],
            CodeCheck.Changes(CodeCheck.Of(cleared, One), before));

        // Out of scope, then the snow cleared: it can no longer be checked; then no code at all: still not.
        Assert.Equal(
            ["Header for Window 1 can no longer be checked: not checked: the ground snow load not entered."],
            CodeCheck.Changes(CodeCheck.Of(beyond, One), CodeCheck.Of(beyond with { Site = SiteValues.NotEntered }, One)));
        Assert.Equal(
            ["Header for Window 1 still cannot be checked: no data to check it against."],
            CodeCheck.Changes(CodeCheck.Of(cleared, One), CodeCheck.Of(cleared with { Code = null }, One)));

        // Following revision 2: r.s30.a is the same (1) 2x8 there, now cited from revision 2.
        Assert.Equal(
            ["Header for Window 1 is unchanged, (1) 2x8, now cited from ZZ FRAME rev 2 Table ZZ-HEADER row r.s30.a."],
            CodeCheck.Changes(CodeCheck.Of(small, Both), CodeCheck.Of(small with { Code = Frame with { Mode = CodeMode.Following, LockedOn = null } }, Both)));

        // A resize inside one band (36 → 40, both ≤ 49) changes only the trace: not announced.
        Sketch inside = small.WithEntity(small.Find<Box>(window)! with { Width = In(40) });
        Assert.Empty(CodeCheck.Changes(before, CodeCheck.Of(inside, One)));

        // Nothing changed, and an opening that only exists on one side, are not changes.
        Assert.Empty(CodeCheck.Changes(before, before));
        Assert.Empty(CodeCheck.Changes([], before));
    }

    [Fact]
    [Trait("Feature", "BLD-003")]
    public void A_sized_header_frames_with_its_member_its_jacks_and_its_kings()
    {
        // 60 in at 12 in: r.s30.b, (2) 2x10, 1 jack and 2 kings each side. t = 1 1/2.
        (Sketch sketch, _) = Design(In(60));
        FramingOptions options = CodeCheck.Framing(CodeCheck.Of(sketch, One), Library);
        WallFraming framing = Assert.Single(FramingList.Of(sketch, Library, options));
        Assert.Empty(framing.Problems);
        Assert.True(Library.TryFindLumber("2x10", out LumberStock twoByTen));
        LumberStock stud = framing.Stock!;

        // Header: w + 2jt = 60 + 3 = 63 long, two plies of the library's 2x10.
        Assert.Equal(new FramingPiece(FramingRole.Header, 2, In(63), twoByTen), Assert.Single(framing.Pieces, p => p.Role == FramingRole.Header));

        // Kings: 2 each side, 4, full height 96 − 4 1/2 = 91 1/2. Jacks: 1 each side, 36 + 42 − 1 1/2 = 76 1/2.
        Assert.Equal(new FramingPiece(FramingRole.KingStud, 4, In(91, 1, 2), stud), Assert.Single(framing.Pieces, p => p.Role == FramingRole.KingStud));
        Assert.Equal(new FramingPiece(FramingRole.JackStud, 2, In(76, 1, 2), stud), Assert.Single(framing.Pieces, p => p.Role == FramingRole.JackStud));

        // Zone [12 − 4 1/2, 72 + 4 1/2) = [7 1/2, 76 1/2) takes the layout studs at 16, 32, 48, 64:
        // 9 layout + the end stud at 142 1/2 = 10, less 4 = 6.
        Assert.Equal(6, framing.Count(FramingRole.Stud));

        // Room over the opening: 96 − 3 − 78 = 15; the header's depth is the 2x10's dressed width.
        // Cripples above at the layout positions within [10 1/2, 73 1/2]: 16, 32, 48, 64.
        Assert.Equal(new FramingPiece(FramingRole.CrippleAbove, 4, In(15) - twoByTen.Width, stud), Assert.Single(framing.Pieces, p => p.Role == FramingRole.CrippleAbove));
        Assert.Contains("2 header pieces (2x10)", framing.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("not yet sized", framing.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain(FramingOptions.PlaceholderJacks, framing.Notes);

        // Bought: the header is a row of 2x10 on the framing section's shopping list.
        Assert.Contains(FramingList.CutRows([framing]), row => row.Material == "2x10" && row.Quantity == 2 && row.Length == In(63));
    }

    [Fact]
    [Trait("Feature", "BLD-003")]
    public void An_unsized_header_keeps_the_placeholder_and_says_so()
    {
        (Sketch sketch, _) = Design(In(36), snow: null);
        WallFraming framing = Assert.Single(FramingList.Of(sketch, Library, CodeCheck.Framing(CodeCheck.Of(sketch, One), Library)));

        Assert.Contains("1 header (not yet sized)", framing.Summary, StringComparison.Ordinal);
        Assert.Contains(FramingOptions.PlaceholderJacks, framing.Notes);
        Assert.Equal(2, framing.Count(FramingRole.KingStud));
        Assert.Equal(2, framing.Count(FramingRole.JackStud));
    }

    [Fact]
    public void A_member_the_library_does_not_carry_is_said_and_not_bought()
    {
        HeaderResult.Sized odd = new(new MemberSpec(2, "2x7"), 1, 1, Assert.IsType<HeaderResult.Sized>(Check(Design(In(36)).Sketch)).Citation);
        Assert.Contains("2x7 is not in the materials library", CodeCheck.Words(odd, Library).Headline, StringComparison.Ordinal);
    }

    [Fact]
    public void Site_values_become_the_engines_inputs_unchanged()
    {
        SiteValues typed = new(30, 115, "B", In(42), In(288), 20, new SiteSource("Town office", new DateOnly(2026, 9, 24)));
        SiteInputs site = CodeCheck.Site(typed);
        Assert.Equal((30, 115, "B"), (site.GroundSnowLoadPsf!.Value, site.UltimateWindSpeedMph!.Value, site.SeismicDesignCategory));
        Assert.Equal(In(42), site.FrostDepth);
        Assert.Equal(In(288), site.BuildingWidth);
        Assert.Equal(20, site.RoofLiveLoadPsf);
        Assert.Equal(new InputProvenance("Town office", new DateOnly(2026, 9, 24)), site.Provenance);

        SiteInputs none = CodeCheck.Site(SiteValues.NotEntered);
        Assert.Null(none.GroundSnowLoadPsf);
        Assert.Null(none.BuildingWidth);
    }

    [Fact]
    public void A_stored_stud_spacing_frames_that_wall_and_drops_the_default_note()
    {
        (Sketch sketch, EntityId window) = Design(In(36));
        Box wall = Wall.All(sketch)[0].Box;
        Sketch spaced = sketch.WithEntity(wall with { WallInputs = new WallInputs("zz-roof", In(24)) });
        WallFraming framing = Assert.Single(FramingList.Of(spaced, Library));
        Assert.Equal(In(24), framing.Spacing);
        Assert.DoesNotContain(framing.Notes, note => note.Contains(FramingOptions.DefaultSpacingNote, StringComparison.Ordinal));
        Assert.NotEqual(default, window);
    }

    [Fact]
    public void A_pack_that_did_not_load_is_named_with_its_problems_and_no_packs_is_nothing()
    {
        CodePacks bad = new([new PackLoadResult.Invalid("us-zz-bad", new ValueList<PackProblem>([new PackProblem("pack.json", null, null, "SYNTHETIC problem")]))]);
        CodeResolution resolved = bad.Resolve(Frame with { PackId = "us-zz-bad" });
        Assert.Null(resolved.Pack);
        Assert.Contains("us-zz-bad is installed but does not load", resolved.Problem, StringComparison.Ordinal);
        Assert.Contains("SYNTHETIC problem", resolved.Problem, StringComparison.Ordinal);

        Assert.Empty(CodePacks.None.Loaded);
        Assert.Empty(CodePacks.None.Invalid);
        Assert.Empty(CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "no-such-folder")]).Loaded);
        Assert.Equal(new CodeSelection("us-zz-frame", 1, CodeLockMode.Following, null), CodeCheck.Selection(new CodeChoice("us-zz-frame", 1, CodeMode.Following, null)));
    }

    [Fact]
    public void Supports_not_chosen_with_no_table_is_the_packs_no_data()
    {
        CodePacks shipped = CodePacks.Discover([RealPacks]);
        (Sketch sketch, _) = Design(In(36), supports: null, code: new CodeChoice("us-ct-2022", 1, CodeMode.Following, null));
        Assert.Equal(NoDataReason.NoTableForWallKind, Assert.IsType<HeaderResult.NoData>(Check(sketch, shipped)).Reason);
    }

    [Fact]
    public void The_short_forms_and_the_words_cover_every_result()
    {
        HeaderResult.Sized sized = Assert.IsType<HeaderResult.Sized>(Check(Design(In(36)).Sketch));
        HeaderResult.OutOfScope beyond = Assert.IsType<HeaderResult.OutOfScope>(Check(Design(In(98)).Sketch));
        Assert.Equal("(1) 2x8, 1 jack and 1 king each side (Table ZZ-HEADER row r.s30.a)", CodeCheck.Short(sized));
        Assert.Equal("beyond Table ZZ-HEADER: get it engineered", CodeCheck.Short(beyond));

        // Two inputs missing at once: both named, and both places to enter them said.
        HeaderResult.InputMissing both = new(new ValueList<string>(["groundSnowLoad", "supports"]), "ZZ-HEADER", sized.Citation.Code, "SYNTHETIC");
        Assert.Equal(
            "Not checked: the ground snow load, what the wall supports are not entered, and napkin never assumes a value. "
            + "Choose what the wall supports under Supports in the wall's panel. Enter the site values under Edit → Adopted code and site.",
            CodeCheck.Words(both, Library).Headline);
        Assert.Equal("not checked: the ground snow load, what the wall supports not entered", CodeCheck.Short(both));

        // A snow load past the heaviest band: "This case is outside …" is the sentence dropped.
        HeaderResult.OutOfScope heavy = Assert.IsType<HeaderResult.OutOfScope>(Check(Design(In(36), snow: 61).Sketch));
        Assert.Equal(
            "This opening is beyond what Table ZZ-HEADER covers: groundSnowLoad 61 psf is above the largest band table ZZ-HEADER covers (60 psf, row r.s60.b). napkin stops here: get this header engineered.",
            CodeCheck.Words(heavy, Library).Headline);

        // An explanation without the engineer sentence is kept whole.
        HeaderResult.OutOfScope plain = heavy with { Explanation = "SYNTHETIC limit." };
        Assert.Contains("covers: SYNTHETIC limit. napkin stops here", CodeCheck.Words(plain, Library).Headline, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("supports", "what the wall supports")]
    [InlineData("groundSnowLoad", "the ground snow load")]
    [InlineData("ultimateWindSpeed", "the wind speed")]
    [InlineData("seismicDesignCategory", "the seismic design category")]
    [InlineData("frostDepth", "the frost depth")]
    [InlineData("buildingWidth", "the building width")]
    [InlineData("headerSpan", "headerSpan")]
    public void Every_table_input_has_plain_words(string name, string words) => Assert.Equal(words, CodeCheck.Input(name));

    [Fact]
    public void Every_kind_of_change_is_said_the_most_serious_first()
    {
        (Sketch small, EntityId window) = Design(In(36));
        Sketch beyond = small.WithEntity(small.Find<Box>(window)! with { Width = In(98) });
        Sketch missing = small with { Site = SiteValues.NotEntered };

        // Out of scope → sized; no answer → out of scope; out of scope at another limit (snow 45:
        // r.s60.b's 61 in instead of r.s30.c's 97 in).
        Assert.Equal(["Header for Window 1 is now sized: (1) 2x8, 1 jack and 1 king each side (Table ZZ-HEADER row r.s30.a)."], CodeCheck.Changes(CodeCheck.Of(beyond, One), CodeCheck.Of(small, One)));
        Assert.Equal(["Header for Window 1 is now beyond Table ZZ-HEADER: get it engineered."], CodeCheck.Changes(CodeCheck.Of(missing, One), CodeCheck.Of(beyond, One)));
        Assert.Equal(
            ["Header for Window 1 is now beyond Table ZZ-HEADER: get it engineered."],
            CodeCheck.Changes(CodeCheck.Of(beyond, One), CodeCheck.Of(beyond with { Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 45 } }, One)));

        // Two windows: one loses its size, one grows; the lost size is said first.
        Box second = new Box(EntityId.New(), OpeningLayer, new Point3(In(100), Length.Zero, In(36)), In(20), In(3, 1, 2), In(42), BoxFace.Top, Angle.Zero) { Name = "Window 2" };
        Sketch two = small.WithEntity(second);
        Sketch after = two.WithEntity(second with { Width = In(30) }).WithEntity(small.Find<Box>(window)! with { Width = In(98) });
        Sketch grown = two.WithEntity(second with { Width = In(60) });
        Assert.Equal(
            [
                "Header for Window 1 is now beyond Table ZZ-HEADER: get it engineered.",
                "Header for Window 2 changed: (1) 2x8 → (2) 2x10, 1 jack and 2 king each side (Table ZZ-HEADER row r.s30.b).",
            ],
            CodeCheck.Changes(CodeCheck.Of(two, One), CodeCheck.Of(after.WithEntity(second with { Width = In(60) }), One)));
        Assert.Single(CodeCheck.Changes(CodeCheck.Of(two, One), CodeCheck.Of(grown, One)));
    }

    [Fact]
    public void One_sized_header_piece_and_a_king_count_below_one_are_said()
    {
        (Sketch sketch, _) = Design(In(36));
        WallFraming framing = Assert.Single(FramingList.Of(sketch, Library, CodeCheck.Framing(CodeCheck.Of(sketch, One), Library)));
        Assert.Contains("1 header piece (2x8)", framing.Summary, StringComparison.Ordinal);

        Box second = new Box(EntityId.New(), OpeningLayer, new Point3(In(100), Length.Zero, In(36)), In(20), In(3, 1, 2), In(42), BoxFace.Top, Angle.Zero) { Name = "Window 2" };
        Assert.Contains("2 headers (not yet sized)", Assert.Single(FramingList.Of(sketch.WithEntity(second), Library)).Summary, StringComparison.Ordinal);

        WallFraming refused = Assert.Single(FramingList.Of(sketch, Library, new FramingOptions { KingsPerSide = _ => 0 }));
        Assert.Contains("Window 1: an opening needs at least one king stud each side", refused.Problems);
    }

    [Fact]
    [Trait("Feature", "RUL-002")]
    public void An_interpolated_header_says_so_on_its_own_line_and_shows_the_working()
    {
        // CodePacks/three (SYNTHETIC): (1) 2x8 is 6'-0" at 30 psf and 4'-0" at 50 psf; at 40 psf, 5'-0".
        LoadedPack pack = Assert.Single(CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "three")]).Loaded);
        SiteInputs site = CodeCheck.Site(SiteValues.NotEntered with { GroundSnowLoadPsf = 40 });
        HeaderResult result = RulesEngine.SizeHeader(pack, new HeaderRequest("zz-roof", WallKind.ExteriorBearing, In(60), site));
        CheckWords words = CodeCheck.Words(result, Library);
        Assert.Equal("Header (1) 2x8, 1 jack stud and 1 king stud each side.", words.Headline);
        Assert.Equal("Interpolated between the 30 psf row (i.s30.a) and the 50 psf row (i.s50.a) (ZZ INTERP footnote e, p. 9)", words.Interpolation);
        Assert.Contains("Interpolation: groundSnowLoad 40 psf is between the 30 psf and 50 psf columns", words.Details, StringComparison.Ordinal);

        HeaderResult plain = RulesEngine.SizeHeader(pack, new HeaderRequest("zz-roof", WallKind.ExteriorBearing, In(60), CodeCheck.Site(SiteValues.NotEntered with { GroundSnowLoadPsf = 30 })));
        Assert.Equal(string.Empty, CodeCheck.Words(plain, Library).Interpolation);

        HeaderResult missing = RulesEngine.SizeHeader(pack, new HeaderRequest("zz-roof", WallKind.ExteriorBearing, In(60), CodeCheck.Site(SiteValues.NotEntered with { GroundSnowLoadPsf = 25 })));
        Assert.StartsWith("Not checked: the roof live load is not entered", CodeCheck.Words(missing, Library).Headline, StringComparison.Ordinal);
    }
}
