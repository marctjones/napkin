using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Building;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Draw → Deck (deck-and-porch §8): a dragged deck snaps its edge onto an existing wall's face, which
/// makes that edge the ledger; it starts 3'-0" high with napkin's starting inputs, and never with a
/// Supports, species or footing depth.
/// </summary>
public class DeckToolTests
{
    static readonly LayerId WallLayer = LayerId.New();

    static Length In(long inches) => Length.Inches(inches);

    /// <summary>A 12 ft wall, x 0 to 144, y 0 to 3 1/2 (its south face on y = 0).</summary>
    static Sketch House(Phase phase = Phase.Existing)
    {
        Box wall = new(EntityId.New(), WallLayer, Point3.Origin, In(144), Length.Inches(3, 1, 2), In(96), BoxFace.Top, Angle.Zero) { Phase = phase };
        return Sketch.Empty.WithLayer(new Layer(WallLayer, BuildingLayers.Wall)).WithEntity(wall);
    }

    [Fact]
    public void A_drag_that_stops_short_of_the_house_snaps_its_edge_onto_the_face()
    {
        // Dragged from (24, −96) to (120, −1): the north edge is 1″ short of the wall's face at y = 0.
        (Point2 anchor, Length length, Length width) = DeckTool.SnapToHouse(House(), Point2.Inches(24, -96), In(96), In(95), In(3));

        Assert.Equal((Point2.Inches(24, -96), In(96), In(96)), (anchor, length, width));
    }

    [Fact]
    public void A_new_wall_or_a_face_out_of_reach_does_not_snap()
    {
        Assert.Equal(In(95), DeckTool.SnapToHouse(House(Phase.New), Point2.Inches(24, -96), In(96), In(95), In(3)).Width);
        Assert.Equal(In(90), DeckTool.SnapToHouse(House(), Point2.Inches(24, -96), In(96), In(90), In(3)).Width);
    }

    [Fact]
    public void The_new_deck_is_a_box_on_the_deck_layer_three_feet_high_with_the_starting_inputs()
    {
        EntityId id = EntityId.New();
        LayerId deckLayer = LayerId.New();
        AddEntity add = Assert.IsType<AddEntity>(DeckTool.Request(deckLayer, null, id, "Deck 1", Point2.Inches(24, -96), In(96), In(96)));
        Box box = Assert.IsType<Box>(add.Entity);

        Assert.Equal((In(36), "Deck 1", deckLayer), (box.Depth, box.Name, box.Layer));
        Assert.Equal(DeckTool.StartingInputs, box.Deck);
        Assert.Null(DeckRules.Refusal(DeckTool.StartingInputs));
        Assert.Equal((null, null, null), (box.Deck!.Supports, box.Deck.Species, box.Deck.FootingDepth));

        Request withLayer = DeckTool.Request(deckLayer, new AddLayer(new Layer(deckLayer, BuildingLayers.Deck)), id, "Deck 1", Point2.Inches(24, -96), In(96), In(96));
        Assert.IsType<Batch>(withLayer);
    }

    [Fact]
    public void The_frame_line_says_the_frame_in_one_sentence()
    {
        LayerId deckLayer = LayerId.New();
        Box deck = (Box)((AddEntity)DeckTool.Request(deckLayer, null, EntityId.New(), "Deck 1", Point2.Inches(0, -120), In(144), In(120))).Entity;
        Sketch sketch = House().WithLayer(new Layer(deckLayer, BuildingLayers.Deck)).WithEntity(deck);

        DeckFraming framing = DeckFrame.Of(sketch, new Deck(deck), MaterialsLibrary.Shipped).Framing!;

        Assert.Equal(
            "ledger, 10 joists 2x8 at 16\", rim, (2) 2x10 beam on 3 posts spanning 5'-6 3/4\", 22 boards (the last 1 7/8\" wide)",
            DeckTool.FrameLine(framing));
    }
}
