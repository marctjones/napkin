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

        // Beam span L_B face to face of posts (DCA 6 Figure 3, p. 7; #41): three 3 1/2″ posts take 10 1/2″ of the
        // 144″, and the two clear spans share the rest: (144 − 3 × 3 1/2) ÷ 2 = 133 1/2 ÷ 2 = 66 3/4″ exactly.
        Assert.Equal(ExactFraction.Whole(In(66, 3, 4).Units), frame.BeamSpan);
        Assert.Equal("5'-6 3/4\"", frame.BeamSpanText);

        // Joist span, ledger face to the beam's near face (DCA 6 Table 2, p. 3): D − t (ledger) − the (2) 2x10 beam's
        // own 3" thickness (2 plies × 1 1/2") − cantilever (0): 120 − 1 1/2 − 3 = 115 1/2".
        Assert.Equal(In(115, 1, 2), frame.JoistSpan);

        // Tributary area, DCA 6 Appendix B (pp. B1–B2), Eq. B-1 for the middle post: no beam overhang, so B_L runs from
        // its centreline (at 3 1/2 ÷ 2 + (144 − 3 1/2) ÷ 2 = 1 3/4 + 70 1/4 = 72″) to the deck's outside edge, 72″ each
        // way; no cantilever, so J_L runs from the ledger face (1 1/2″ out) to the rim's outside face (120″ out),
        // 118 1/2″, and J_O = 0. A = (118 1/2 ÷ 2 + 0) × 72 = 59 1/4 × 72 = 4266 sq in = 29.625 sq ft, shown 29.6.
        DeckTributary tributary = frame.Tributary;
        Assert.Equal(TributaryPost.Centre, tributary.Post);
        Assert.Equal(ExactFraction.Whole(In(72).Units), tributary.BeamSpan);
        Assert.Equal((ExactFraction.Whole(In(118, 1, 2).Units), ExactFraction.Whole(0)), (tributary.JoistLength, tributary.JoistOverhang));
        Assert.Equal(new ExactFraction((Int128)In(59, 1, 4).Units * In(72).Units, 1), frame.TributaryArea);
        Assert.Equal("29.6 sq ft", frame.TributaryAreaText);
        Assert.Equal(
            "29.6 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: 6'-0\" of beam, post centreline to the deck's outside edge, × half the joists' 9'-10 1/2\", ledger face to the rim's outside face)",
            tributary.Words);
        Assert.Same(frame.MiddlePost, frame.Tributary);
        Assert.Equal(PostPosition.Center, tributary.Position);

        // An end post, a corner post, Eq. B-2 (#42): the same end span, B_L 72″ from the middle post's centreline to the
        // deck's edge, halved, with B_O = 0: A = 59 1/4 × 36 = 2133 sq in = 14.8 sq ft.
        DeckTributary end = frame.EndPost;
        Assert.Equal((TributaryPost.Corner, PostPosition.Corner, false), (end.Post, end.Position, end.EdgeToEdge));
        Assert.Equal(new ExactFraction((Int128)In(59, 1, 4).Units * In(36).Units, 1), end.Area);
        Assert.Equal(
            "14.8 sq ft (DCA 6 Appendix B Eq. B-2, pp. B1–B2: half the beam's 6'-0\", the next post's centreline to the deck's outside edge, × half the joists' 9'-10 1/2\", ledger face to the rim's outside face)",
            end.Words);

        // Each post from grade to the beam's underside, as DCA 6 measures post height (p. 10): 36 − 1 − 7 1/4 − 9 1/4 = 18 1/2″.
        Assert.Equal(In(18, 1, 2), frame.PostLength);
    }

    [Fact]
    [Trait("Feature", "DECK-005")]
    public void The_beam_span_between_post_faces_and_the_tributary_area_are_exact_for_two_and_four_posts()
    {
        // Two posts: L_B = 144 − 2 × 3 1/2 = 137″. The most loaded post is an end post, Eq. B-2: with no overhang the one
        // span runs outside edge to outside edge, B_L = 144″, and B_O = 0, so A = 59 1/4 × (144 ÷ 2 + 0) = 59 1/4 × 72
        // = 4266 sq in = 29.6 sq ft — each end post carries half the deck.
        DeckFraming two = Frame(Drawing(House(), DeckBox(Inputs(2))));
        Assert.Equal(ExactFraction.Whole(In(137).Units), two.BeamSpan);
        Assert.Equal("11'-5\"", two.BeamSpanText);
        Assert.Equal((TributaryPost.Corner, ExactFraction.Whole(In(144).Units)), (two.Tributary.Post, two.Tributary.BeamSpan));
        Assert.Equal((true, (DeckTributary?)null), (two.EndPost.EdgeToEdge, two.MiddlePost));
        Assert.Equal(new ExactFraction((Int128)In(59, 1, 4).Units * In(72).Units, 1), two.TributaryArea);
        Assert.Equal(
            "29.6 sq ft (DCA 6 Appendix B Eq. B-2, pp. B1–B2: half the beam's 12'-0\", the deck's outside edge to outside edge, × half the joists' 9'-10 1/2\", ledger face to the rim's outside face)",
            two.Tributary.Words);

        // Four posts: L_B = (144 − 4 × 3 1/2) ÷ 3 = 130 ÷ 3 = 43 1/3″ — not on the grid, kept exact (133120/3 in 1/1024″);
        // shown to the nearest 1/16″, 43 5/16″. The posts' centres are (144 − 3 1/2) ÷ 3 = 46 5/6″ apart, so the middle post
        // beside an end has spans of 1 3/4 + 46 5/6 = 48 7/12″ (to the deck's edge) and 46 5/6″ (to the next centre); the
        // greater is B_L (p. B2). A = 59 1/4 × 48 7/12 = 2878.5625 sq in = 19.99 sq ft, shown 20.0.
        DeckFraming four = Frame(Drawing(House(), DeckBox(Inputs(4))));
        Assert.Equal(new ExactFraction(130 * 1024, 3), four.BeamSpan);
        Assert.Equal("≈3'-7 5/16\"", four.BeamSpanText);
        Assert.Equal((TributaryPost.Centre, new ExactFraction(((48 * 12) + 7) * 1024, 12)), (four.Tributary.Post, four.Tributary.BeamSpan));
        Assert.Equal(new ExactFraction((Int128)In(59, 1, 4).Units * ((48 * 12) + 7) * 1024, 12), four.TributaryArea);
        Assert.Equal("20.0 sq ft", four.TributaryAreaText);
        Assert.StartsWith("20.0 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: ≈4'-0 9/16\" of beam, post centreline", four.Tributary.Words, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "DECK-005")]
    public void A_cantilever_shortens_the_joist_span_and_measures_the_joists_to_the_beams_centre()
    {
        // 12″ cantilever: joist span, ledger face to the beam's near face, D − t − beam thickness − cantilever =
        // 120 − 1 1/2 − 3 − 12 = 103 1/2″. The (2) 2x10 beam is 3″ thick, its outer face 12″ in from the rim's
        // outer face, so its centre is 120 − 12 − 1 1/2 = 106 1/2″ out: J_L = 106 1/2 − 1 1/2 = 105″ (ledger face to the
        // beam's centre, p. B1) and J_O = 120 − 106 1/2 = 13 1/2″. A = (105 ÷ 2 + 13 1/2) × 72 = 66 × 72 = 4752 sq in = 33.0 sq ft.
        DeckFraming frame = Frame(Drawing(House(), DeckBox(Inputs(cantilever: In(12)))));

        Assert.Equal(In(103, 1, 2), frame.JoistSpan);
        Assert.Equal((ExactFraction.Whole(In(105).Units), ExactFraction.Whole(In(13, 1, 2).Units)), (frame.Tributary.JoistLength, frame.Tributary.JoistOverhang));
        Assert.Equal(new ExactFraction((Int128)In(66).Units * In(72).Units, 1), frame.TributaryArea);
        Assert.Equal(
            "33.0 sq ft (DCA 6 Appendix B Eq. B-1, pp. B1–B2: 6'-0\" of beam, post centreline to the deck's outside edge, × (half the joists' 8'-9\", ledger face to the beam's centre, + 1'-1 1/2\", the beam's centre to the deck's outside edge))",
            frame.Tributary.Words);

        // A (3) 2x10 beam is 4 1/2″ thick: its centre is 120 − 12 − 2 1/4 = 105 3/4″ out, J_L = 104 1/4″, J_O = 14 1/4″;
        // A = (52 1/8 + 14 1/4) × 72 = 66 3/8 × 72 = 4779 sq in = 33.19 sq ft, shown 33.2. Its joist span scales with the
        // extra ply: 120 − 1 1/2 − 4 1/2 (3 × 1 1/2) − 12 = 102″ — 1 1/2″ less than the (2)-ply beam's 103 1/2″.
        DeckFraming three = Frame(Drawing(House(), DeckBox(Inputs(cantilever: In(12)) with { Beam = new BeamSpec(3, "2x10") })));
        Assert.Equal(In(102), three.JoistSpan);
        Assert.Equal((ExactFraction.Whole(In(104, 1, 4).Units), ExactFraction.Whole(In(14, 1, 4).Units)), (three.Tributary.JoistLength, three.Tributary.JoistOverhang));
        Assert.Equal(new ExactFraction((Int128)In(66, 3, 8).Units * In(72).Units, 1), three.TributaryArea);
        Assert.Equal("33.2 sq ft", three.TributaryAreaText);
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
    [Trait("Feature", "DECK-005")]
    public void A_deck_against_a_wall_running_north_south_frames_the_same_and_opens_its_other_three_edges()
    {
        // The house turned a quarter: its long faces run north–south. The deck (120 across, 144 along
        // the wall) sits against its east face, so the ledger is the deck's west edge.
        Box house = House() with { Rotation = Angle.Right, Anchor = new Point3(Length.Zero, Length.Zero, In(36)) };
        Length east = new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(house.Corner).Max(point => point.X);
        Length south = new[] { BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest }.Select(house.Corner).Min(point => point.Y);
        Box deckBox = DeckBox() with { Anchor = new Point3(east, south + In(48), Length.Zero), Width = In(120), Height = In(144) };
        Sketch sketch = Drawing(house, deckBox);

        DeckFraming frame = Frame(sketch);
        Assert.Equal(DeckEdge.West, frame.Ledger);
        Assert.Equal((In(144), In(120), 10), (frame.Width, frame.Depth, frame.Joists.Length));
        Assert.Equal([DeckEdge.North, DeckEdge.South, DeckEdge.East], Deck.All(sketch)[0].OpenEdges(sketch));
    }

    [Fact]
    public void A_deck_on_the_houses_north_side_opens_its_north_edge()
    {
        // The deck north of the house: its south edge on the house's north face (y = 5 1/2).
        Box deckBox = DeckBox() with { Anchor = new Point3(In(48), In(5, 1, 2), Length.Zero) };
        Sketch sketch = Drawing(House(), deckBox);

        Assert.Equal(DeckEdge.South, Frame(sketch).Ledger);
        Assert.Equal([DeckEdge.North, DeckEdge.East, DeckEdge.West], Deck.All(sketch)[0].OpenEdges(sketch));
    }

    [Fact]
    public void A_deck_not_against_a_wall_has_no_open_edges_and_a_tipped_house_wall_holds_no_ledger()
    {
        Sketch alone = Drawing(DeckBox());
        Assert.Empty(Deck.All(alone)[0].OpenEdges(alone));

        Sketch tipped = Drawing(House() with { FaceUp = BoxFace.South }, DeckBox());
        Assert.Equal(DeckProblem.NotAgainstAWall, DeckFrame.Of(tipped, Deck.All(tipped)[0], MaterialsLibrary.Shipped).Refusal!.Problem);

        // A deck turned by an angle that is not a quarter is not square to the plan.
        Box skew = DeckBox() with { Rotation = new Angle(45 * 3600) };
        Assert.Null(new Deck(skew).Outline);
    }

    [Theory]
    [InlineData(FramingRole.Ledger, "ledgers")]
    [InlineData(FramingRole.Joist, "joists")]
    [InlineData(FramingRole.RimJoist, "rim joists")]
    [InlineData(FramingRole.Blocking, "blocks")]
    [InlineData(FramingRole.Beam, "beam plies")]
    [InlineData(FramingRole.Post, "posts")]
    [InlineData(FramingRole.DeckingBoard, "decking boards")]
    [InlineData(FramingRole.GuardPost, "guard posts")]
    [InlineData(FramingRole.GuardRail, "guard rails")]
    [InlineData(FramingRole.GuardCap, "guard caps")]
    [InlineData(FramingRole.Baluster, "balusters")]
    [InlineData(FramingRole.Stringer, "stringers")]
    [InlineData(FramingRole.Tread, "tread boards")]
    public void The_deck_pieces_are_named_one_and_many(FramingRole role, string many)
    {
        Assert.Equal(many, FramingList.Label(role, 2));
        Assert.Equal(many == "beam plies" ? "beam ply" : many[..^1], FramingList.Label(role, 1));
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
