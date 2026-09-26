using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The deck frame of <c>docs/design/deck-and-porch.md</c> §9.2, worked by hand: a 12 × 10 ft deck, 3 ft
/// above grade, against an existing house wall, 2x8 joists at 16″, a (2) 2x10 beam on three 4x4 posts,
/// 5/4x6 decking with a 1/8″ gap. Lumber sizes are the shipped library's (2x8 1 1/2 × 7 1/4, 2x10
/// 9 1/4, 4x4 3 1/2, 5/4x6 1 × 5 1/2).
/// </summary>
public class DeckFramingTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId DeckLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static DeckInputs Inputs(int posts = 3, Length? cantilever = null) => new(
        JoistDirection.Out, In(16), "2x8", new BeamSpec(2, "2x10"), "4x4", posts, cantilever ?? Length.Zero, "5/4x6", In(0, 1, 8), true,
        "zz-deck", "zz-fir", In(42), null, null);

    /// <summary>The house wall: existing, 20 ft of 2x6, 8 ft tall, its floor 36″ up, its south face on y = 0.</summary>
    static Box House(Phase phase = Phase.Existing, long y = 0) => new(EntityId.New(), WallLayer, new Point3(Length.Zero, new Length(y), In(36)), In(240), In(5, 1, 2), In(96), BoxFace.Top, Angle.Zero)
    {
        Name = "House",
        Phase = phase,
    };

    /// <summary>Deck 1: 144 × 120, 36″ high, anchored at (48, −120), so its north edge is on y = 0.</summary>
    static Box DeckBox(DeckInputs? inputs = null, long northOffsetUnits = 0) => new(EntityId.New(), DeckLayer, new Point3(In(48), In(-120) + new Length(northOffsetUnits), Length.Zero), In(144), In(120), In(36), BoxFace.Top, Angle.Zero)
    {
        Name = "Deck 1",
        Deck = inputs ?? Inputs(),
    };

    static Sketch Drawing(params Box[] boxes)
    {
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithLayer(new Layer(DeckLayer, BuildingLayers.Deck));
        return boxes.Aggregate(sketch, (with, box) => with.WithEntity(box));
    }

    static DeckFraming Frame(Sketch sketch)
    {
        (DeckFraming? framing, DeckRefusal? refusal) = DeckFrame.Of(sketch, Assert.Single(Deck.All(sketch)), MaterialsLibrary.Shipped);
        Assert.Null(refusal);
        return framing!;
    }

    static FramingPiece Piece(DeckFraming framing, FramingRole role, Length? length = null)
        => Assert.Single(framing.Pieces, piece => piece.Role == role && (length is null || piece.Length == length));

    [Fact]
    [Trait("Feature", "DECK-005")]
    public void The_worked_example_frame_is_every_piece_of_section_9_2()
    {
        DeckFraming frame = Frame(Drawing(House(), DeckBox()));

        Assert.Equal(DeckEdge.North, frame.Ledger);
        Assert.Equal((In(144), In(120)), (frame.Width, frame.Depth));

        // Joists at 0, 16, … 128 (nine: 128 + 1 1/2 ≤ 144) and the end joist at 142 1/2: ten × 117″.
        Assert.Equal([.. Enumerable.Range(0, 9).Select(k => In(16 * k)), In(142, 1, 2)], frame.Joists);
        Assert.Equal((10, In(117), "2x8"), (Piece(frame, FramingRole.Joist).Quantity, Piece(frame, FramingRole.Joist).Length, Piece(frame, FramingRole.Joist).Stock!.Name));
        Assert.Equal((1, In(144)), (Piece(frame, FramingRole.Ledger).Quantity, Piece(frame, FramingRole.Ledger).Length));
        Assert.Equal((1, In(144)), (Piece(frame, FramingRole.RimJoist).Quantity, Piece(frame, FramingRole.RimJoist).Length));

        // Blocking: eight bays of 14 1/2″ and the last, 142 1/2 − 129 1/2 = 13″.
        Assert.Equal(8, Piece(frame, FramingRole.Blocking, In(14, 1, 2)).Quantity);
        Assert.Equal(1, Piece(frame, FramingRole.Blocking, In(13)).Quantity);

        // Beam: two plies of 144″ 2x10. Posts: 36 − 1 − 7 1/4 − 9 1/4 = 18 1/2″, three.
        Assert.Equal((2, In(144), "2x10"), (Piece(frame, FramingRole.Beam).Quantity, Piece(frame, FramingRole.Beam).Length, Piece(frame, FramingRole.Beam).Stock!.Name));
        Assert.Equal((3, In(18, 1, 2)), (Piece(frame, FramingRole.Post).Quantity, Piece(frame, FramingRole.Post).Length));

        // Decking: 5 5/8 n ≥ 120 1/8 → 22 boards of 144″; the 22nd shows 120 − 21 × 5 5/8 = 1 7/8″.
        Assert.Equal((22, In(144)), (Piece(frame, FramingRole.DeckingBoard).Quantity, Piece(frame, FramingRole.DeckingBoard).Length));
        Assert.Equal((22, In(1, 7, 8)), (frame.DeckingBoards, frame.LastBoardWidth));

        // Beam span (144 − 3 × 3 1/2) ÷ 2 = 66 3/4″ exactly; tributary area 66 3/4 × 58 1/2 = 3904.875 sq in = 27.1 sq ft.
        Assert.Equal(ExactFraction.Whole(In(66, 3, 4).Units), frame.BeamSpan);
        Assert.Equal("5'-6 3/4\"", frame.BeamSpanText);
        Assert.Equal(new ExactFraction((Int128)In(66, 3, 4).Units * In(58, 1, 2).Units, 1), frame.TributaryArea);
        Assert.Equal("27.1 sq ft", frame.TributaryAreaText);
        Assert.Equal(In(117), frame.JoistSpan);
    }

    [Theory]
    [Trait("Feature", "DECK-005")]
    // Two posts: 144 − 7 = 137″ between them.
    [InlineData(2, 137 * 1024, 1)]
    // Four posts: (144 − 14) ÷ 3 = 43 1/3″ — not on the grid, kept exact.
    [InlineData(4, 130 * 1024, 3)]
    public void The_beam_span_between_posts_is_exact_for_any_count(int posts, long numerator, long denominator)
    {
        DeckFraming frame = Frame(Drawing(House(), DeckBox(Inputs(posts))));

        Assert.Equal(new ExactFraction(numerator, denominator), frame.BeamSpan);
        Assert.Equal(denominator == 1 ? "11'-5\"" : "≈3'-7 5/16\"", frame.BeamSpanText);
    }

    [Fact]
    [Trait("Feature", "DECK-005")]
    public void A_cantilever_shortens_the_joist_span_and_moves_load_onto_the_beam()
    {
        // 12″ cantilever: joist span 117 − 12 = 105″; tributary depth 105 ÷ 2 + 12 = 64 1/2″; area 66 3/4 × 64 1/2.
        DeckFraming frame = Frame(Drawing(House(), DeckBox(Inputs(cantilever: In(12)))));

        Assert.Equal(In(105), frame.JoistSpan);
        Assert.Equal(new ExactFraction((Int128)In(66, 3, 4).Units * In(64, 1, 2).Units, 1), frame.TributaryArea);
        Assert.Equal("29.9 sq ft", frame.TributaryAreaText);
    }

    [Fact]
    [Trait("Feature", "DECK-005")]
    public void A_deck_a_1024th_off_the_wall_face_is_not_against_a_wall_and_one_in_a_corner_is_refused()
    {
        Sketch off = Drawing(House(), DeckBox(northOffsetUnits: -1));
        Assert.Equal(DeckProblem.NotAgainstAWall, DeckFrame.Of(off, Deck.All(off)[0], MaterialsLibrary.Shipped).Refusal!.Problem);

        // A second existing wall on the deck's west edge (x = 48, running north–south).
        Box side = new(EntityId.New(), WallLayer, new Point3(In(48), In(-120), In(36)), In(120), In(5, 1, 2), In(96), BoxFace.Top, Angle.Right) { Phase = Phase.Existing };
        Sketch corner = Drawing(House(), side, DeckBox());
        Assert.Equal(DeckProblem.AgainstTwoWalls, DeckFrame.Of(corner, Deck.All(corner)[0], MaterialsLibrary.Shipped).Refusal!.Problem);

        // A new wall is not the house: nothing to hang a ledger on.
        Sketch newHouse = Drawing(House(Phase.New), DeckBox());
        Assert.Equal(DeckProblem.NotAgainstAWall, DeckFrame.Of(newHouse, Deck.All(newHouse)[0], MaterialsLibrary.Shipped).Refusal!.Problem);
    }

    [Fact]
    public void A_deck_with_no_inputs_unknown_lumber_or_too_low_says_why()
    {
        Box bare = DeckBox() with { Deck = null };
        Sketch noInputs = Drawing(House(), bare);
        Assert.Equal(DeckProblem.NoInputs, DeckFrame.Of(noInputs, Deck.All(noInputs)[0], MaterialsLibrary.Shipped).Refusal!.Problem);

        Sketch unknown = Drawing(House(), DeckBox(Inputs() with { Joist = "2x7" }));
        DeckRefusal refusal = DeckFrame.Of(unknown, Deck.All(unknown)[0], MaterialsLibrary.Shipped).Refusal!;
        Assert.Equal(DeckProblem.UnknownLumber, refusal.Problem);
        Assert.StartsWith("2x7 is not in the materials library", refusal.Text, StringComparison.Ordinal);

        // 17″ high: 17 − 1 − 7 1/4 − 9 1/4 = −1/2″. No post fits.
        Sketch low = Drawing(House(), DeckBox() with { Depth = In(17) });
        Assert.Equal(DeckProblem.TooLow, DeckFrame.Of(low, Deck.All(low)[0], MaterialsLibrary.Shipped).Refusal!.Problem);
    }

    [Fact]
    public void Every_refusal_reads_as_a_sentence()
    {
        foreach (DeckProblem problem in Enum.GetValues<DeckProblem>())
        {
            Assert.EndsWith(".", new DeckRefusal(problem, "2x7").Text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_turned_deck_is_not_level_and_frames_nothing()
    {
        Box tipped = DeckBox() with { FaceUp = BoxFace.South };
        Sketch sketch = Drawing(House(), tipped);

        Assert.Equal(DeckProblem.NotLevel, DeckFrame.Of(sketch, Deck.All(sketch)[0], MaterialsLibrary.Shipped).Refusal!.Problem);
        Assert.Empty(new Deck(tipped).OpenEdges(sketch));
    }

    [Fact]
    public void The_three_edges_away_from_the_house_are_open_until_a_wall_stands_on_one()
    {
        Sketch open = Drawing(House(), DeckBox());
        Deck deck = Deck.All(open)[0];
        Assert.Equal([DeckEdge.South, DeckEdge.East, DeckEdge.West], deck.OpenEdges(open));

        // The porch's front wall on the south edge, standing on the deck's surface.
        Box front = new(EntityId.New(), WallLayer, new Point3(In(48), In(-120), In(36)), In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Name = "Front" };
        Assert.Equal([DeckEdge.East, DeckEdge.West], deck.OpenEdges(Drawing(House(), DeckBox(), front)));

        // At the ground, it is not on the deck, and a demolished one closes nothing.
        Assert.Equal(3, deck.OpenEdges(Drawing(House(), DeckBox(), front with { Anchor = new Point3(In(48), In(-120), Length.Zero) })).Length);
        Assert.Equal(3, deck.OpenEdges(Drawing(House(), DeckBox(), front with { Phase = Phase.Demolish })).Length);
    }

    [Fact]
    [Trait("Feature", "DECK-005")]
    public void A_wall_standing_on_the_deck_frames_exactly_as_the_same_wall_on_the_floor()
    {
        // §5.1's test: nothing in the wall's frame assumes its bottom is at world 0.
        static WallFraming Framed(Length z)
        {
            LayerId opening = LayerId.New();
            Box wall = new(EntityId.New(), WallLayer, new Point3(In(48), In(-120), z), In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Name = "Front" };
            Box window = new(EntityId.New(), opening, new Point3(In(57), In(-120), z + In(24)), In(36), In(3, 1, 2), In(60), BoxFace.Top, Angle.Zero);
            Sketch sketch = Drawing(wall, window).WithLayer(new Layer(opening, BuildingLayers.Opening));
            return FramingList.Frame(sketch, new Wall(wall), MaterialsLibrary.Shipped);
        }

        WallFraming floor = Framed(Length.Zero), deck = Framed(In(36));
        Assert.Equal(floor.Pieces, deck.Pieces);
        Assert.NotEmpty(floor.Pieces);
        Assert.Contains(deck.Pieces, piece => piece.Role == FramingRole.Header);
    }

    [Fact]
    public void The_deck_pieces_are_cut_rows_the_shopping_list_buys()
    {
        DeckFraming frame = Frame(Drawing(House(), DeckBox()));

        var rows = DeckFrame.CutRows(frame);
        Assert.Contains(rows, row => row.Label == "Deck 1 joist" && row.Quantity == 10 && row.Length == In(117));

        // 2x10: two 144″ plies, each on a 12' board (144″ exactly fills it: one cut fewer).
        ShoppingListRow beam = Assert.Single(ShoppingList.Of(rows), row => row.Material == "2x10");
        Assert.Equal(2, beam.Count);
    }

    [Fact]
    public void A_deck_is_read_by_its_layer_its_name_or_its_inputs()
    {
        Box named = DeckBox() with { Layer = LayerId.Default, Name = "Deck", Deck = null };
        Box byInputs = DeckBox() with { Layer = LayerId.Default, Name = "Platform" };
        Box neither = DeckBox() with { Layer = LayerId.Default, Name = "Platform", Deck = null };
        Sketch sketch = Drawing(named, byInputs, neither);

        Assert.Equal(2, Deck.All(sketch).Length);
        Assert.Equal("Deck", new Deck(named).Name);
        Assert.Equal("Deck", new Deck(neither with { Name = string.Empty }).Name);
        Assert.Equal(In(36), new Deck(named).Height);
    }
}
