using Napkin.Core.Geometry;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The deck, roof and opening fill a box carries (format version 13, deck-and-porch §7) are part of its
/// value: two boxes differing only in one of them are different boxes, and a deck's hardware list
/// compares by its items, not by reference.
/// </summary>
public class DeckInputsTests
{
    static DeckInputs Deck() => new(
        JoistDirection.Out, Length.Inches(16), "2x8", new BeamSpec(2, "2x10"), "4x4", 3, Length.Zero, "5/4x6", Length.Inches(0, 1, 8), true,
        null, null, null, null, null)
    {
        Hardware = [new HardwareItem("Joist hanger", 10)],
    };

    static RoofInputs Roof() => new(Length.Inches(16), "2x8", "2x8", Length.Inches(12), true, null, new Roofing("Shingles", null, 0), new WallLowEnd(new EntityId(Guid.Empty)));

    static Box Plain() => new(new EntityId(new Guid("00000000-0000-4000-8000-000000000001")), LayerId.Default, Point3.Origin, Length.Inches(144), Length.Inches(120), Length.Inches(36), BoxFace.Top, Angle.Zero);

    [Fact]
    public void Two_decks_with_equal_fields_and_equal_hardware_items_are_equal()
    {
        DeckInputs a = Deck(), b = Deck();

        Assert.NotSame(a.Hardware, b.Hardware);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.False(a.Equals(null));
    }

    public static TheoryData<DeckInputs> Changed => new()
    {
        // A direction M11 does not build yet, cast, so the comparison is exercised both ways.
        Deck() with { JoistDirection = (JoistDirection)1 },
        Deck() with { JoistSpacing = Length.Inches(12) },
        Deck() with { Joist = "2x10" },
        Deck() with { Beam = new BeamSpec(3, "2x10") },
        Deck() with { Post = "6x6" },
        Deck() with { PostCount = 4 },
        Deck() with { Cantilever = Length.Inches(12) },
        Deck() with { Decking = "5/4x4" },
        Deck() with { DeckingGap = Length.Zero },
        Deck() with { Blocking = false },
        Deck() with { Supports = "zz-deck" },
        Deck() with { Species = "zz-fir" },
        Deck() with { FootingDepth = Length.Inches(42) },
        Deck() with { Guard = new GuardInputs(Length.Inches(36), Length.Inches(72), Length.Inches(3), Length.Inches(3), "4x4", "2x4", "2x6", "2x2") },
        Deck() with { Stair = new StairInputs(DeckEdge.South, Length.Zero, Length.Inches(36), Length.Inches(10), null, 3, "2x12", 2) },
        Deck() with { Hardware = [new HardwareItem("Joist hanger", 12)] },
    };

    [Theory]
    [MemberData(nameof(Changed))]
    public void A_deck_differing_in_any_one_input_is_a_different_deck(DeckInputs changed)
        => Assert.NotEqual(Deck(), changed);

    [Fact]
    public void A_boxs_deck_roof_and_opening_are_part_of_its_value()
    {
        Box plain = Plain();

        foreach (Box changed in new[] { plain with { Deck = Deck() }, plain with { Roof = Roof() }, plain with { Opening = OpeningFill.Screen } })
        {
            Assert.NotEqual(plain, changed);
            Assert.NotEqual(plain.GetHashCode(), changed.GetHashCode());
        }

        Assert.Equal(plain with { Deck = Deck() }, plain with { Deck = Deck() });
        Assert.Equal((plain with { Roof = Roof() }).GetHashCode(), (plain with { Roof = Roof() }).GetHashCode());
    }

    [Fact]
    public void A_roofs_low_end_is_a_wall_or_a_beam_on_posts()
    {
        Assert.NotEqual(Roof(), Roof() with { LowEnd = new BeamLowEnd(new BeamSpec(2, "2x10"), "4x4", 2) });
        Assert.Equal(new BeamLowEnd(new BeamSpec(2, "2x10"), "4x4", 2), new BeamLowEnd(new BeamSpec(2, "2x10"), "4x4", 2));
    }
}
