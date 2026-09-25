using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// Wall lines and the wall-bracing check (#39), against the SYNTHETIC packs in <c>CodePacks/brace</c>
/// (NOT CODE VALUES; see its README). Every expected length is worked by hand in the comments.
/// </summary>
/// <remarks>
/// The design: a 16 ft wall (192"), 8 ft tall, Window 1 at 30"-66" and Window 2 at 126"-162", so
/// its segments are 30" (wall start to Window 1), 60" (Window 1 to Window 2) and 30" (Window 2 to
/// wall end). Pack A at 90 mph: required 48" × 192 / 120 = 76.8" → 78" (6'-6") on the 2" step; a
/// zz-panel segment counts from 24" (walls ≤ 8'-0"), capped at 72". Pack B: 36" × 192 / 96 = 72"
/// (6'-0") on a 1" step; zz-panel counts from 36".
/// </remarks>
public class BracingCheckTests
{
    const string Panel = "zz-panel";
    const string Board = "zz-board";
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();
    static readonly string BraceRoot = Path.Combine(AppContext.BaseDirectory, "CodePacks", "brace");
    static readonly string RealPacks = Path.Combine(AppContext.BaseDirectory, "RealPacks");
    static readonly CodePacks Packs = CodePacks.Discover([BraceRoot]);
    static readonly CodeChoice A = new("us-zz-brace-a", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
    static readonly CodeChoice B = new("us-zz-brace-b", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    sealed record Plan(Sketch Sketch, EntityId Wall, EntityId One, EntityId Two)
    {
        public WallLine Line => WallLine.Of(Sketch, new Wall(Sketch.Find<Box>(Wall)!));

        public Plan Assign(int index, string? method)
        {
            Box wall = Sketch.Find<Box>(Wall)!;
            WallInputs inputs = (wall.WallInputs ?? new WallInputs(null, null)) with { Bracing = Line.Assign(index, method) };
            return this with { Sketch = Sketch.WithEntity(wall with { WallInputs = inputs.OrNull() }) };
        }

        public Plan Opening(EntityId id, long offset, Length width)
        {
            Box box = Sketch.Find<Box>(id)!;
            return this with { Sketch = Sketch.WithEntity(box with { Anchor = box.Anchor with { X = In(offset) }, Width = width }) };
        }

        public Plan Without(EntityId id) => this with { Sketch = Sketch.WithoutEntity(id) };

        public BracingResult Result(CodePacks? packs = null) => Assert.Single(BracingCheck.Of(Sketch, packs ?? Packs)).Result;

        public WallBracingCheck Check => Assert.Single(BracingCheck.Of(Sketch, Packs));
    }

    static Plan Design(int? wind = 90, Length? height = null, CodeChoice? code = null)
    {
        Box wall = new(EntityId.New(), WallLayer, Point3.Origin, In(192), In(3, 1, 2), height ?? In(96), BoxFace.Top, Angle.Zero) { Name = "Wall 1" };
        Box one = new(EntityId.New(), OpeningLayer, new Point3(In(30), Length.Zero, In(36)), In(36), In(3, 1, 2), In(42), BoxFace.Top, Angle.Zero) { Name = "Window 1" };
        Box two = new(EntityId.New(), OpeningLayer, new Point3(In(126), Length.Zero, In(36)), In(36), In(3, 1, 2), In(42), BoxFace.Top, Angle.Zero) { Name = "Window 2" };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wall)
            .WithEntity(one)
            .WithEntity(two) with
        {
            Code = code ?? A,
            Site = SiteValues.NotEntered with { UltimateWindSpeedMph = wind },
        };
        return new Plan(sketch, wall.Id, one.Id, two.Id);
    }

    static Plan AllPanels(Plan plan) => plan.Assign(0, Panel).Assign(1, Panel).Assign(2, Panel);

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void A_wall_line_is_its_solid_segments_between_openings_named_by_their_boundaries()
    {
        Plan plan = Design();
        WallLine line = plan.Line;
        Assert.Equal([In(30), In(60), In(30)], line.Segments.Select(s => s.Length));
        Assert.Equal(["wall start to Window 1", "Window 1 to Window 2", "Window 2 to wall end"], line.Segments.Select(s => s.Label));
        Assert.Equal([(null, (EntityId?)plan.One), (plan.One, plan.Two), (plan.Two, null)], line.Segments.Select(s => (s.From, s.To)));
        Assert.All(line.Segments, s => Assert.Equal(AssignmentOrigin.None, s.Origin));

        // An opening at the wall's start leaves no segment before it; overlapping openings bound none between them.
        Plan edge = plan.Opening(plan.One, 0, In(36)).Opening(plan.Two, 20, In(36));
        Assert.Equal([(plan.Two, (EntityId?)null)], edge.Line.Segments.Select(s => (s.From, s.To)));
        Assert.Equal(In(192 - 56), Assert.Single(edge.Line.Segments).Length);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void Nothing_assigned_is_not_braced_and_the_line_falls_short_by_all_of_it()
    {
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(Design().Result());
        Assert.Equal(Length.Zero, fails.Provided);
        Assert.Equal(In(78), fails.Shortfall);
        Assert.Equal("Braced length 0\" of 6'-6\" required: SHORT by 6'-6\" (ZZ-BRACE.1).", BracingCheck.Words(fails).Headline);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void Assigned_segments_pass_and_widening_a_window_1_1024_inch_past_the_minimum_panel_flags_the_wall()
    {
        // 30 + 60 + 30 = 120" (10'-0") ≥ 78": passes.
        Plan plan = AllPanels(Design());
        CheckWords passes = BracingCheck.Words(plan.Result());
        Assert.Equal("Braced length 10'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", passes.Headline);
        Assert.StartsWith("IRC 2099 Section ZZ-BRACE.1, as adopted by ZZ BRACE A row q.w99", passes.Citation, StringComparison.Ordinal);
        Assert.Contains("Segment Window 1 to Window 2, 5'-0\": 5'-0\"", passes.Details, StringComparison.Ordinal);

        // Window 1 widened to 72": the middle segment is 126 − 102 = 24", exactly the minimum: 30 + 24 + 30 = 84" ≥ 78".
        Plan at = plan.Opening(plan.One, 30, In(72));
        Assert.Equal(In(84), Assert.IsType<BracingResult.Passes>(at.Result()).Provided);

        // 1/1024" wider: 23-1023/1024" is under the 24" minimum and counts for nothing: 60" < 78", short 18".
        Plan over = plan.Opening(plan.One, 30, In(72) + new Length(1));
        BracingResult.Fails fails = Assert.IsType<BracingResult.Fails>(over.Result());
        Assert.Equal(In(60), fails.Provided);
        Assert.Equal(In(18), fails.Shortfall);
        Assert.Equal("Braced length 5'-0\" of 6'-6\" required: SHORT by 1'-6\" (ZZ-BRACE.1).", BracingCheck.Words(fails).Headline);
        Assert.Equal(
            ["Wall 1's braced line is now SHORT by 1'-6\", braced 5'-0\" of 6'-6\" required (ZZ-BRACE.1)."],
            BracingCheck.Changes([at.Check], [over.Check]));

        // The widened window kept every assignment: its boundaries did not change.
        Assert.All(over.Line.Segments, s => Assert.Equal(AssignmentOrigin.Assigned, s.Origin));
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void Deleting_a_window_merges_its_neighbours_keeping_a_shared_method_and_undo_restores_both()
    {
        Plan plan = AllPanels(Design());
        Plan merged = plan.Without(plan.One);
        WallSegment first = merged.Line.Segments[0];
        Assert.Equal((null, (EntityId?)plan.Two), (first.From, first.To));
        Assert.Equal(In(126), first.Length);
        Assert.Equal((Panel, AssignmentOrigin.Merged), (first.Method, first.Origin));
        Assert.Empty(BracingCheck.Unassigned([plan.Check], [merged.Check]));

        // Deleting Window 2 instead merges the last two segments towards the wall's end the same way.
        WallSegment last = plan.Without(plan.Two).Line.Segments[^1];
        Assert.Equal((plan.One, (EntityId?)null, Panel, AssignmentOrigin.Merged), (last.From, last.To, last.Method, last.Origin));

        // Undo is the old sketch: Window 1 back, both of its neighbours assigned exactly again.
        Assert.All(plan.Line.Segments, s => Assert.Equal(AssignmentOrigin.Assigned, s.Origin));

        // Choosing a method rewrites the list for the current segments only: the stale entries go.
        Plan chosen = merged.Assign(0, Board);
        Assert.Equal(
            [new BracingAssignment(null, plan.Two, Board), new BracingAssignment(plan.Two, null, Panel)],
            chosen.Sketch.Find<Box>(plan.Wall)!.WallInputs!.Bracing);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void Merging_segments_with_different_methods_unassigns_the_merged_one_and_says_so()
    {
        Plan plan = Design().Assign(0, Panel).Assign(1, Board).Assign(2, Panel);
        Plan merged = plan.Without(plan.One);
        WallSegment first = merged.Line.Segments[0];
        Assert.Null(first.Method);
        Assert.Equal(AssignmentOrigin.MergedConflict, first.Origin);
        Assert.Equal(
            ["Wall 1: the segment wall start to Window 2 merged braced segments with different methods, so it is not braced now; choose its method again."],
            BracingCheck.Unassigned([plan.Check], [merged.Check]));

        // Said once: a later edit that leaves the conflict as it was does not say it again.
        Assert.Empty(BracingCheck.Unassigned([merged.Check], [merged.Check]));

        // Choosing "not braced" there rewrites the list for the current segments: the stale choices go.
        Plan cleared = merged.Assign(0, null);
        Assert.Equal([new BracingAssignment(plan.Two, null, Panel)], cleared.Sketch.Find<Box>(plan.Wall)!.WallInputs!.Bracing);
        Assert.Equal(AssignmentOrigin.None, cleared.Line.Segments[0].Origin);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void A_window_moved_past_the_other_leaves_new_segments_unassigned_and_says_so()
    {
        Plan plan = AllPanels(Design());

        // Window 2 moved to 0"-24", before Window 1: segments Window 2 to Window 1 (6") and Window 1 to wall end.
        Plan moved = plan.Opening(plan.Two, 0, In(24));
        Assert.Equal([(plan.Two, (EntityId?)plan.One), (plan.One, null)], moved.Line.Segments.Select(s => (s.From, s.To)));
        Assert.All(moved.Line.Segments, s => Assert.Null(s.Method));
        Assert.Equal(2, BracingCheck.Unassigned([plan.Check], [moved.Check]).Length);
        Assert.Contains("is a new segment where a braced one was", BracingCheck.Unassigned([plan.Check], [moved.Check])[1], StringComparison.Ordinal);

        // A new window splitting the braced middle segment: its two parts are new segments, unassigned.
        Box extra = new(EntityId.New(), OpeningLayer, new Point3(In(90), Length.Zero, In(36)), In(12), In(3, 1, 2), In(42), BoxFace.Top, Angle.Zero) { Name = "Window 3" };
        Plan split = plan with { Sketch = plan.Sketch.WithEntity(extra) };
        Assert.Equal([Panel, null, null, Panel], split.Line.Segments.Select(s => s.Method));
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void Out_of_scope_input_missing_and_no_data_are_honest()
    {
        BracingResult.OutOfScope tall = Assert.IsType<BracingResult.OutOfScope>(AllPanels(Design(height: In(145))).Result());
        Assert.StartsWith("This wall line is beyond what Section ZZ-BRACE.1 covers: Section ZZ-BRACE.1 limit l.tall excludes wallHeight 12'-1\"", BracingCheck.Words(tall).Headline, StringComparison.Ordinal);

        Assert.Equal(
            "Not checked: the wind speed is not entered, and napkin never assumes a value. Enter the site values under Project → Adopted code and site.",
            BracingCheck.Words(AllPanels(Design(wind: null)).Result()).Headline);

        Plan none = Design() with { Sketch = Design().Sketch with { Code = null } };
        Assert.Equal("No code selected: choose one under Project → Adopted code and site.", BracingCheck.Words(none.Result()).Headline);

        CodePacks shipped = CodePacks.Discover([RealPacks]);
        CodeChoice ct = new("us-ct-2022", shipped.Loaded.Single().Manifest.Revision, CodeMode.Locked, new DateOnly(2026, 9, 25));
        Plan onCt = Design(code: ct);
        CheckWords words = BracingCheck.Words(onCt.Result(shipped));
        Assert.Equal(
            "The loaded pack CT 2022 has no wall-bracing provisions, so napkin cannot check this wall line's bracing. Nothing is guessed: add them to the pack directory from your copy of the code (docs/rules-engine.md). Where to add tables: docs/rules-engine.md",
            words.Headline);
        Assert.Empty(BracingCheck.Methods(shipped.Loaded.Single()));
        Assert.Empty(BracingCheck.Methods(null));
    }

    [Fact]
    [Trait("Feature", "RUL-008")]
    public void Switching_to_pack_B_recomputes_the_wall_and_names_the_newly_flagged_result()
    {
        // A: passes, 120" of 78". B: the 30" segments are under B's 36" minimum: 60" of 72", short 12".
        Plan plan = AllPanels(Design());
        Plan switched = plan with { Sketch = plan.Sketch with { Code = B } };
        BracingRecomputeReport report = BracingCheck.Report([plan.Check], [switched.Check]);
        Assert.Equal(BracingChangeKind.PassToFail, Assert.Single(report.NewlyFlagged).Kind);
        Assert.Equal(
            ["Wall 1's braced line is now SHORT by 1'-0\", braced 5'-0\" of 6'-0\" required (ZZ-BRACE-B.7)."],
            BracingCheck.Changes([plan.Check], [switched.Check]));

        // A zz-board segment (pack A's only) counts for nothing under B and says why.
        BracingResult.Fails board = Assert.IsType<BracingResult.Fails>(
            (switched with { Sketch = switched.Sketch }).Assign(1, Board).Result());
        Assert.Contains("method 'zz-board' is not one of ZZ BRACE B's methods", board.Working.Segments[1].Why, StringComparison.Ordinal);
        Assert.Equal([Panel], BracingCheck.Methods(Packs.Resolve(B).Pack).Select(m => m.Id));
    }

    [Fact]
    [Trait("Feature", "RUL-008")]
    public void Every_kind_of_bracing_change_is_said_in_plain_words()
    {
        Plan plan = AllPanels(Design());

        // Still passing with other lengths: Window 1 at 5'-0" leaves 36" between: 96" (8'-0").
        Plan wider = plan.Opening(plan.One, 30, In(60));
        Assert.Equal(["Wall 1's braced line now passes, braced 8'-0\" of 6'-6\" required (ZZ-BRACE.1)."], BracingCheck.Changes([plan.Check], [wider.Check]));

        // A 12'-1" wall is beyond the section; from there, no code at all can no longer be checked.
        Plan tall = plan with { Sketch = plan.Sketch.WithEntity(plan.Sketch.Find<Box>(plan.Wall)! with { Depth = In(145) }) };
        Assert.Equal(["Wall 1's bracing is now beyond Section ZZ-BRACE.1: get it engineered."], BracingCheck.Changes([plan.Check], [tall.Check]));
        Plan none = tall with { Sketch = tall.Sketch with { Code = null } };
        Assert.Equal(
            ["Wall 1's bracing can no longer be checked: no data to check it against."],
            BracingCheck.Changes([tall.Check], [none.Check]));
        Plan missing = none with { Sketch = none.Sketch with { Code = new CodeChoice("us-zz-gone", 1, CodeMode.Locked, new DateOnly(2026, 9, 25)) } };
        Assert.Equal(["Wall 1's bracing still cannot be checked: no data to check it against."], BracingCheck.Changes([none.Check], [missing.Check]));
        Plan unwinded = plan with { Sketch = plan.Sketch with { Site = SiteValues.NotEntered } };
        Assert.Equal(
            ["Wall 1's bracing can no longer be checked: not checked: the wind speed not entered."],
            BracingCheck.Changes([plan.Check], [unwinded.Check]));

        // The same lengths under another pack (a copy of A named ZZ BRACE C): said as unchanged, now under C.
        LoadedPack a = Packs.Resolve(A).Pack!;
        LoadedPack c = a with { Manifest = a.Manifest with { Id = "us-zz-brace-c", Adoption = a.Manifest.Adoption with { ShortName = "ZZ BRACE C" } } };
        CodePacks both = new([new PackLoadResult.Loaded(a), new PackLoadResult.Loaded(c)]);
        CodeChoice onC = new("us-zz-brace-c", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
        WallBracingCheck underA = Assert.Single(BracingCheck.Of(plan.Sketch, both));
        WallBracingCheck underC = Assert.Single(BracingCheck.Of(plan.Sketch with { Code = onC }, both));
        Assert.Equal(
            ["Wall 1's bracing is unchanged, passes, braced 10'-0\" of 6'-6\" required (ZZ-BRACE.1), now under ZZ BRACE C."],
            BracingCheck.Changes([underA], [underC]));

        Plan bare = Design();
        Assert.Equal(
            ["Wall 1's bracing is unchanged, SHORT by 6'-6\", braced 0\" of 6'-6\" required (ZZ-BRACE.1), now under ZZ BRACE C."],
            BracingCheck.Changes(BracingCheck.Of(bare.Sketch, both), BracingCheck.Of(bare.Sketch with { Code = onC }, both)));

        // A relabelled segment under the same code is not announced; a wall new since before is not a change.
        Assert.Empty(BracingCheck.Changes([plan.Check], [plan.Check]));
        Assert.Empty(BracingCheck.Unassigned([], [plan.Check]));
        Assert.Equal("beyond Section ZZ-BRACE.1: get it engineered", BracingCheck.Short(tall.Result()));
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void A_merge_through_a_segment_that_was_never_braced_is_not_braced()
    {
        // Only the first segment braced; deleting Window 1 merges it with the unbraced middle one.
        Plan plan = Design().Assign(0, Panel);
        WallSegment merged = plan.Without(plan.One).Line.Segments[0];
        Assert.Null(merged.Method);
        Assert.Equal(AssignmentOrigin.None, merged.Origin);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void The_words_name_every_missing_input_and_keep_an_explanation_without_the_engineer_sentence()
    {
        AdoptedCodeRef code = Packs.Resolve(A).Pack!.Code;
        BracingResult.InputMissing two = new(ValueList.Of("ultimateWindSpeed", "seismicDesignCategory"), "ZZ-BRACE.1", code, "x");
        Assert.StartsWith("Not checked: the wind speed, the seismic design category are not entered", BracingCheck.Words(two).Headline, StringComparison.Ordinal);
        Assert.Equal("not checked: the wind speed, the seismic design category not entered", BracingCheck.Short(two));

        BracingResult.OutOfScope plain = Assert.IsType<BracingResult.OutOfScope>(AllPanels(Design(height: In(145))).Result()) with { Explanation = "A limit." };
        Assert.Equal("This wall line is beyond what Section ZZ-BRACE.1 covers: A limit. napkin stops here: get the bracing engineered.", BracingCheck.Words(plain).Headline);
    }
}
