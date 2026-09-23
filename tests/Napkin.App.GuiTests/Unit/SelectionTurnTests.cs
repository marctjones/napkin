using System.Collections.Immutable;
using Napkin.App.Designs;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The turn command turns a part in place: the low corner of its extent stays where it was (#75).
/// </summary>
public class SelectionTurnTests
{
    static readonly EntityId Leg = EditingBuilder.Id(0);

    public static TheoryData<BoxFace, int, Axis, int> EveryTurnOfEveryOrientation()
    {
        TheoryData<BoxFace, int, Axis, int> data = [];
        foreach (BoxFace faceUp in Enum.GetValues<BoxFace>())
        {
            foreach (int quarters in (int[])[0, 1, 2, 3])
            {
                foreach (Axis axis in Enum.GetValues<Axis>())
                {
                    data.Add(faceUp, quarters, axis, 1);
                    data.Add(faceUp, quarters, axis, -1);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTurnOfEveryOrientation))]
    public void A_turn_keeps_the_low_corner_of_the_extent_where_it_was(BoxFace faceUp, int quarters, Axis axis, int turns)
    {
        // Three different sizes, so every orientation has a different extent.
        Box box = new(Leg, LayerId.Default, Point3.Inches(10, 20, 30), Length.Inches(3), Length.Inches(5), Length.Inches(7), faceUp, Angle.Right * quarters);
        DesignEditor editor = EditorWith(box);

        UpdateResult? result = SelectionTurn.Turn(editor, axis, turns);

        Assert.IsAssignableFrom<Succeeded>(result);
        Box turned = editor.Sketch.Find<Box>(Leg)!;
        Assert.Equal(box.Orientation.TurnedAbout(axis, turns), turned.Orientation);
        Assert.Equal(SpaceSnapResolver.Extent(box).Low, SpaceSnapResolver.Extent(turned).Low);
        Assert.Equal((box.Width, box.Height, box.Depth), (turned.Width, turned.Height, turned.Depth));
    }

    [Fact]
    public void A_leg_on_the_floor_turned_forward_stays_on_the_floor()
    {
        // Shift+X on a standing leg puts its south face up; about its anchor that is 16 1/4" below
        // the floor (assembly-model §7.1: South spans [-H, 0] in Z). In place, it lies on the floor.
        Box leg = Box.AsDrawn(Leg, LayerId.Default, Point2.Inches(0, 0), Length.Inches(2, 1, 2), Length.Inches(2, 1, 2), Length.Inches(16, 1, 4), Angle.Zero);
        DesignEditor editor = EditorWith(leg);

        SelectionTurn.Turn(editor, Axis.X, -1);

        Box lying = editor.Sketch.Find<Box>(Leg)!;
        Assert.Equal(BoxFace.South, lying.FaceUp);
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(lying);
        Assert.Equal(Length.Zero, low.Z);
        Assert.Equal(Length.Inches(2, 1, 2), high.Z);
        Assert.Equal(Point3.Origin, low);
    }

    [Fact]
    public void A_turn_in_place_is_one_undo_step()
    {
        Box box = new(Leg, LayerId.Default, Point3.Inches(4, 5, 6), Length.Inches(3), Length.Inches(5), Length.Inches(7), BoxFace.Top, Angle.Zero);
        DesignEditor editor = EditorWith(box);

        SelectionTurn.Turn(editor, Axis.Y, 1);
        Assert.NotEqual(box, editor.Sketch.Find<Box>(Leg));

        Assert.True(editor.Undo());
        Assert.Equal(box, editor.Sketch.Find<Box>(Leg));
    }

    [Fact]
    public void A_pinned_part_turns_about_the_corner_it_is_pinned_by_and_says_so()
    {
        Box box = new(Leg, LayerId.Default, Point3.Inches(4, 5, 6), Length.Inches(3), Length.Inches(5), Length.Inches(7), BoxFace.Top, Angle.Zero);
        DesignEditor editor = EditorWith(box);
        editor.Apply(new AddRelationship(new Anchored(RelationshipId.New(), Leg)), "pin it");

        UpdateResult? result = SelectionTurn.Turn(editor, Axis.X, -1);

        Assert.IsAssignableFrom<Succeeded>(result);
        Box turned = editor.Sketch.Find<Box>(Leg)!;
        Assert.Equal(BoxFace.South, turned.FaceUp);
        Assert.Equal(box.Anchor, turned.Anchor);
        Assert.Contains("pinned", editor.LastMessage!.Text, StringComparison.Ordinal);
    }

    static DesignEditor EditorWith(Box box)
    {
        DesignEditor editor = new();
        editor.Open(new Design("Test", Sketch.Empty.WithEntity(box), ImmutableDictionary<EntityId, string>.Empty.Add(box.Id, "Leg")));
        editor.Select(box.Id);
        return editor;
    }
}
