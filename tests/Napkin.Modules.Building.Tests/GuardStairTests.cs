using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The guard and stair of deck-and-porch §9.3 (§11.2 tests 9–10), worked by hand: §9's deck with a 36″
/// guard (posts at most 72″ apart, 3 1/2″ gaps and clearance, 4x4 / 2x4 / 2x6 / 2x2) and a 36″ stair on
/// the east edge, 42″ from the corner, with 10″ treads, 3 2x12 stringers and 2 tread boards; the
/// provisions are the SYNTHETIC pack us-zz-deck's (NOT CODE VALUES: riser 8 1/4″, tread 9″, …).
/// </summary>
public class GuardStairTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();
    static readonly CodePacks Synthetic = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "deck")]);
    static readonly LoadedPack ZzDeck = Assert.Single(Synthetic.Loaded);

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static readonly GuardInputs Guard = new(In(36), In(72), In(3, 1, 2), In(3, 1, 2), "4x4", "2x4", "2x6", "2x2");

    static StairInputs Stair(int? risers = null, long run = 10, DeckEdge edge = DeckEdge.East) => new(edge, In(42), In(36), In(run), risers, 3, "2x12", 2);

    static (Sketch Sketch, DeckFraming Framing) Deck(GuardInputs? guard, StairInputs? stair, long height = 36)
    {
        Box house = new(EntityId.New(), WallLayer, new Point3(Length.Zero, Length.Zero, In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Phase = Phase.Existing };
        Box deck = new(EntityId.New(), DeckLayer, new Point3(In(48), In(-120), Length.Zero), In(144), In(120), In(height), BoxFace.Top, Angle.Zero)
        {
            Name = "Deck 1",
            Deck = new DeckInputs(JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", In(0, 1, 8), true, "zz-deck", "zz-fir", In(42), guard, stair),
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck)).WithEntity(house).WithEntity(deck);
        return (sketch, DeckFrame.Of(sketch, new Building.Deck(deck), MaterialsLibrary.Shipped).Framing!);
    }

    static FramingPiece Piece(GuardLayout layout, FramingRole role, Length? length = null)
        => Assert.Single(layout.Pieces, piece => piece.Role == role && (length is null || piece.Length == length));

    [Fact]
    [Trait("Feature", "DECK-004")]
    public void The_guard_of_section_9_3_is_every_post_rail_cap_and_baluster()
    {
        (Sketch sketch, DeckFraming framing) = Deck(Guard, Stair());
        GuardLayout layout = GuardFraming.Of(sketch, framing, MaterialsLibrary.Shipped)!;

        // Runs: south 144 (2 bays), east 42 and 42 either side of the stair (1 bay each), west 120 (2 bays).
        Assert.Equal(
            [(DeckEdge.South, In(144), 2), (DeckEdge.East, In(42), 1), (DeckEdge.East, In(42), 1), (DeckEdge.West, In(120), 2)],
            layout.Runs.Select(run => (run.Edge, run.Length, run.Bays)));

        // 3 + 2 + 2 + 3 posts, the south-west and south-east corners shared: 8, each 36 + 7 1/4 + 1 = 44 1/4″.
        Assert.Equal((8, In(44, 1, 4)), (Piece(layout, FramingRole.GuardPost).Quantity, Piece(layout, FramingRole.GuardPost).Length));

        // Clears: south (144 − 10 1/2) ÷ 2 = 66 3/4; east 42 − 7 = 35; west (120 − 10 1/2) ÷ 2 = 54 3/4.
        // Balusters per bay 13, 7, 11; gaps (66 3/4 − 19 1/2) ÷ 14 = 3 3/8, (35 − 10 1/2) ÷ 8 = 3 1/16, (54 3/4 − 16 1/2) ÷ 12 = 3 3/16.
        Assert.Equal([13, 7, 7, 11], layout.Runs.Select(run => run.Balusters));
        Assert.Equal(["3 3/8\"", "3 1/16\"", "3 1/16\"", "3 3/16\""], layout.Runs.Select(run => GuardFraming.Words(run.Gap)));

        // Rails: 4 × 66 3/4, 4 × 35, 4 × 54 3/4; caps 144, 2 × 42, 120.
        Assert.Equal(4, Piece(layout, FramingRole.GuardRail, In(66, 3, 4)).Quantity);
        Assert.Equal(4, Piece(layout, FramingRole.GuardRail, In(35)).Quantity);
        Assert.Equal(4, Piece(layout, FramingRole.GuardRail, In(54, 3, 4)).Quantity);
        Assert.Equal(2, Piece(layout, FramingRole.GuardCap, In(42)).Quantity);
        Assert.Equal(1, Piece(layout, FramingRole.GuardCap, In(144)).Quantity);

        // 2 · 13 + 2 · 7 + 2 · 11 = 62 balusters, each 36 − 1 1/2 − 7 − 3 1/2 = 24″.
        Assert.Equal((62, In(24)), (Piece(layout, FramingRole.Baluster).Quantity, Piece(layout, FramingRole.Baluster).Length));
    }

    [Fact]
    [Trait("Feature", "DECK-004")]
    public void The_stair_of_section_9_3_is_five_risers_of_36_fifths_and_a_63_7_8_board()
    {
        (_, DeckFraming framing) = Deck(Guard, Stair());
        StairLayout stair = StairFraming.Of(framing, ZzDeck, MaterialsLibrary.Shipped).Layout!;

        // ⌈36 ÷ 8 1/4⌉ = 5 risers of 36/5″; 4 treads, 40″ of run; √(36² + 40²) = √2896 = 53.81… → 53 7/8″; + 10″ = 63 7/8″.
        Assert.Equal((5, new ExactFraction(36 * 1024, 5), 4, In(40)), (stair.Risers, stair.RiseEach, stair.Treads, stair.TotalRun));
        Assert.Equal((In(53, 7, 8), In(63, 7, 8)), (stair.Diagonal, stair.Board));
        Assert.Equal(
            "Lay out 5 risers of 7 3/16\" (≈, exactly 3'-0\" ÷ 5) and 4 treads of 10\" on a 2x12 with the square at 7 3/16 and 10; the diagonal of the whole rise and run is 4'-5 7/8\", so a 6'-0\" board is enough (the diagonal plus one tread, napkin's allowance for the end cuts).",
            stair.Layout);
        Assert.Equal((3, In(63, 7, 8)), (stair.Pieces[0].Quantity, stair.Pieces[0].Length));
        Assert.Equal((8, In(36)), (stair.Pieces[1].Quantity, stair.Pieces[1].Length));
    }

    [Fact]
    [Trait("Feature", "DECK-004")]
    public void A_three_four_five_stair_is_exact()
    {
        // 4 risers of 9″ (typed) and 3 treads of 16″: 36 and 48, a diagonal of exactly 60″.
        (_, DeckFraming framing) = Deck(null, Stair(risers: 4, run: 16));
        StairLayout stair = StairFraming.Of(framing, ZzDeck, MaterialsLibrary.Shipped).Layout!;

        Assert.Equal((In(60), In(76)), (stair.Diagonal, stair.Board));
        Assert.StartsWith("Lay out 4 risers of 9\" and 3 treads of 1'-4\"", stair.Layout, StringComparison.Ordinal);
        Assert.Equal(ExactFraction.Whole(In(9).Units), stair.RiseEach);
    }

    [Fact]
    public void A_stair_on_the_house_side_with_no_riser_rule_or_unknown_lumber_says_why()
    {
        Assert.Equal("The stair cannot be on the house side: choose another edge.", StairFraming.Of(Deck(null, Stair(edge: DeckEdge.North)).Framing, ZzDeck, MaterialsLibrary.Shipped).Problem);
        Assert.StartsWith("Type the riser count", StairFraming.Of(Deck(null, Stair()).Framing, null, MaterialsLibrary.Shipped).Problem, StringComparison.Ordinal);
        Assert.StartsWith("2x13 is not in the materials library", StairFraming.Of(Deck(null, Stair() with { Stringer = "2x13" }).Framing, ZzDeck, MaterialsLibrary.Shipped).Problem, StringComparison.Ordinal);
        (StairLayout? none, string? why) = StairFraming.Of(Deck(null, null).Framing, ZzDeck, MaterialsLibrary.Shipped);
        Assert.Equal((null, null), (none, why));

        // A 20-foot stair has no stocked 2x12 long enough, and says so.
        Assert.Contains("and no stocked 2x12 is that long", StairFraming.Of(Deck(null, Stair(risers: 2, run: 240)).Framing, ZzDeck, MaterialsLibrary.Shipped).Layout!.Layout, StringComparison.Ordinal);
    }

    [Fact]
    public void An_integer_square_root_rounds_up_only_when_it_is_not_exact()
    {
        Assert.Equal(60, StairFraming.CeilingRoot(3600));
        Assert.Equal(61, StairFraming.CeilingRoot(3601));
        Assert.Equal(0, StairFraming.CeilingRoot(0));
        Assert.Equal(1, StairFraming.CeilingRoot(1));
    }

    [Fact]
    [Trait("Feature", "DECK-004")]
    public void The_guard_and_stair_checks_read_the_packs_provisions_and_cite_them()
    {
        (Sketch sketch, _) = Deck(Guard, Stair());
        DeckChecks checks = DeckCheck.For(sketch, Building.Deck.All(sketch)[0], ZzDeck, MaterialsLibrary.Shipped);
        string[] guard = [.. checks.Lines.Where(line => line.Kind == DeckCheckKind.Guard).Select(line => line.Text)];
        string[] stair = [.. checks.Lines.Where(line => line.Kind == DeckCheckKind.Stair).Select(line => line.Text)];

        Assert.Equal(
            [
                "Guard required: the deck is 3'-0\" above grade, over 2'-4\", with 3 open edges (ZZ-GUARD.1, synthetic p. 7 guard).",
                "Guard height 3'-0\": at least 2'-10\" (ZZ-GUARD.1, synthetic p. 7 guard).",
                "Guard openings: baluster gaps 3 3/8\", 3 1/16\", 3 3/16\" and 3 1/2\" under the rail, none over 5\" (ZZ-GUARD.1, synthetic p. 7 guard).",
            ],
            guard);
        Assert.StartsWith("Stair: Lay out 5 risers", stair[0], StringComparison.Ordinal);
        Assert.Equal(
            [
                "Risers ≈7 3/16\": at most 8 1/4\" (ZZ-GUARD.1, synthetic p. 7 stair).",
                "Treads 10\": at least 9\" (ZZ-GUARD.1, synthetic p. 7 stair).",
                "Handrail required: 5 risers, at least 3 (ZZ-GUARD.1, synthetic p. 7 stair); add it as hardware.",
                "Stair width 3'-0\": at least 2'-8\" (ZZ-GUARD.1, synthetic p. 7 stair).",
            ],
            stair[1..]);
        Assert.All(checks.Lines.Where(line => line.Kind is DeckCheckKind.Guard or DeckCheckKind.Stair), line => Assert.True(line.Passing));
    }

    [Fact]
    public void Short_guards_steep_risers_and_narrow_stairs_fail_and_a_low_deck_needs_no_guard()
    {
        GuardInputs low = Guard with { Height = In(30), BalusterGap = In(6), BottomClearance = In(6) };
        (Sketch sketch, _) = Deck(low, Stair(risers: 4, run: 8) with { Width = In(30) });
        DeckChecks checks = DeckCheck.For(sketch, Building.Deck.All(sketch)[0], ZzDeck, MaterialsLibrary.Shipped);

        Assert.Contains(checks.Lines, line => line.Text == "Guard height 2'-6\": 4\" short of the 2'-10\" required (ZZ-GUARD.1, synthetic p. 7 guard).");
        Assert.Contains(checks.Lines, line => line.Text.StartsWith("Guard openings: the widest is 6\", over the 5\" allowed", StringComparison.Ordinal));
        Assert.Contains(checks.Lines, line => line.Text.StartsWith("Risers 9\": over the 8 1/4\" allowed", StringComparison.Ordinal));
        Assert.Contains(checks.Lines, line => line.Text.StartsWith("Treads 8\": 1\" short of the 9\" required", StringComparison.Ordinal));
        Assert.Contains(checks.Lines, line => line.Text.StartsWith("Stair width 2'-6\": 2\" short of the 2'-8\" required", StringComparison.Ordinal));

        // 24″ high: no guard required; nothing typed is asked for.
        (Sketch lowDeck, _) = Deck(null, null, height: 28);
        Assert.Contains(DeckCheck.For(lowDeck, Building.Deck.All(lowDeck)[0], ZzDeck, MaterialsLibrary.Shipped).Lines, line => line.Text.StartsWith("No guard required: the deck is 2'-4\" above grade, not over 2'-4\"", StringComparison.Ordinal));

        // 36″ high with no guard typed: required, and the line says to add one.
        (Sketch bare, _) = Deck(null, null);
        Assert.Contains(DeckCheck.For(bare, Building.Deck.All(bare)[0], ZzDeck, MaterialsLibrary.Shipped).Lines, line => line.Text.EndsWith(": add a guard in the panel.", StringComparison.Ordinal) && !line.Passing);

        // Two risers: fewer than 3, no handrail.
        (Sketch two, _) = Deck(null, Stair(risers: 2, run: 10));
        Assert.Contains(DeckCheck.For(two, Building.Deck.All(two)[0], ZzDeck, MaterialsLibrary.Shipped).Lines, line => line.Text.StartsWith("No handrail required: 2 risers, fewer than 3", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_provisions_the_guard_and_stair_say_nothing_is_guessed()
    {
        (Sketch sketch, _) = Deck(Guard, Stair(risers: 5));
        DeckChecks none = DeckCheck.For(sketch, Building.Deck.All(sketch)[0], null, MaterialsLibrary.Shipped);
        Assert.Contains(none.Lines, line => line.Text == "Guard: no adopted code is chosen, so napkin cannot say whether one is required. Nothing is guessed.");
        Assert.Contains(none.Lines, line => line.Text == "Stair: no adopted code is chosen, so napkin cannot check the risers, treads or handrail. Nothing is guessed.");

        LoadedPack ct = Assert.Single(CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "RealPacks")]).Loaded);
        DeckChecks under = DeckCheck.For(sketch, Building.Deck.All(sketch)[0], ct, MaterialsLibrary.Shipped);
        Assert.Contains(under.Lines, line => line.Text.StartsWith("Guard: the loaded pack CT 2022 has no guard provisions", StringComparison.Ordinal));
        Assert.Contains(under.Lines, line => line.Text.StartsWith("Stair: the loaded pack CT 2022 has no stair provisions", StringComparison.Ordinal));
    }

    [Fact]
    public void Items_the_pack_does_not_cover_are_said_and_a_stair_problem_is_one_line()
    {
        LoadedPack uncovered = ZzDeck with
        {
            Deck = ZzDeck.Deck with
            {
                GuardStair = ZzDeck.Deck.GuardStair! with
                {
                    Guard = ZzDeck.Deck.GuardStair.Guard! with { TriggerHeight = null, MinimumHeight = null, MaximumOpening = null },
                    Stair = ZzDeck.Deck.GuardStair.Stair! with { MaximumRiser = null, MinimumTread = null, HandrailWhenRisersAtLeast = null, MinimumWidth = null },
                },
            },
        };
        (Sketch sketch, _) = Deck(Guard, Stair(risers: 5));
        DeckChecks checks = DeckCheck.For(sketch, Building.Deck.All(sketch)[0], uncovered, MaterialsLibrary.Shipped);

        Assert.Contains(checks.Lines, line => line.Text == "When a guard is required is not covered by this pack (ZZ-GUARD.1, synthetic p. 7 guard).");
        Assert.Single(checks.Lines, line => line.Kind == DeckCheckKind.Stair);

        (Sketch north, _) = Deck(null, Stair(edge: DeckEdge.North));
        Assert.Contains(DeckCheck.For(north, Building.Deck.All(north)[0], ZzDeck, MaterialsLibrary.Shipped).Lines, line => line.Text == "Stair: The stair cannot be on the house side: choose another edge.");
    }

    [Fact]
    public void A_stair_at_a_corner_leaves_one_run_and_a_deck_with_no_open_edge_has_no_guard()
    {
        (Sketch sketch, DeckFraming framing) = Deck(Guard, Stair() with { At = Length.Zero, Width = In(120) });
        GuardLayout layout = GuardFraming.Of(sketch, framing, MaterialsLibrary.Shipped)!;
        Assert.DoesNotContain(layout.Runs, run => run.Edge == DeckEdge.East);

        (Sketch starting, DeckFraming framed) = Deck(Guard, Stair() with { At = Length.Zero });
        Assert.Equal(In(36), Assert.Single(GuardFraming.Of(starting, framed, MaterialsLibrary.Shipped)!.Runs, run => run.Edge == DeckEdge.East).From);

        Box enclose(DeckEdge edge) => edge switch
        {
            DeckEdge.South => new(EntityId.New(), WallLayer, new Point3(In(48), In(-120), In(36)), In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero),
            DeckEdge.East => new(EntityId.New(), WallLayer, new Point3(In(192), In(-120), In(36)), In(120), In(3, 1, 2), In(96), BoxFace.Top, Angle.Right),
            _ => new(EntityId.New(), WallLayer, new Point3(In(51, 1, 2), In(-120), In(36)), In(120), In(3, 1, 2), In(96), BoxFace.Top, Angle.Right),
        };
        Sketch porch = new[] { DeckEdge.South, DeckEdge.East, DeckEdge.West }.Aggregate(sketch, (with, edge) => with.WithEntity(enclose(edge)));
        if (Building.Deck.All(porch)[0].OpenEdges(porch).IsEmpty)
        {
            Assert.Null(GuardFraming.Of(porch, framing, MaterialsLibrary.Shipped));
        }

        Assert.Null(GuardFraming.Of(sketch, Deck(null, null).Framing, MaterialsLibrary.Shipped));
        Assert.Null(GuardFraming.Of(sketch, Deck(Guard with { Baluster = "2x3x" }, null).Framing, MaterialsLibrary.Shipped));
    }
}
