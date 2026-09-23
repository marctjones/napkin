using System.Collections.Immutable;
using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Duplicating and mirroring several parts at once, with the relationships among them (#87).
/// </summary>
public class GroupCopyTests
{
    // A 48" x 24" top up at 16 1/4", a leg under its south-west corner, and an apron against the leg.
    static readonly Box Top = new(EditingBuilder.Id(0), LayerId.Default, Point3.Inches(0, 0, 0) with { Z = Length.Inches(16, 1, 4) }, Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), BoxFace.Top, Angle.Zero);
    static readonly Box Leg = new(EditingBuilder.Id(1), LayerId.Default, Point3.Inches(1, 1, 0) with { X = Length.Inches(1, 1, 2), Y = Length.Inches(1, 1, 2) }, Length.Inches(2, 1, 2), Length.Inches(2, 1, 2), Length.Inches(16, 1, 4), BoxFace.Top, Angle.Zero);
    static readonly Box Apron = new(EditingBuilder.Id(2), LayerId.Default, new Point3(Length.Inches(4), Length.Inches(1, 1, 2), Length.Inches(12, 3, 4)), Length.Inches(40), Length.Inches(0, 3, 4), Length.Inches(3, 1, 2), BoxFace.Top, Angle.Zero);

    static readonly Flush ApronOnLeg = new(RelationshipId.New(), new FeatureRef(Leg.Id, BoxFeature.Face(BoxFace.East)), new FeatureRef(Apron.Id, BoxFeature.Face(BoxFace.West)));
    static readonly AxisDistance LegInset = new(RelationshipId.New(), new FeatureRef(Top.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest)), new FeatureRef(Leg.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest)), Axis.X, Length.Inches(1, 1, 2));
    static readonly ParamValue ApronLength = new(RelationshipId.New(), new BoxWidthRef(Apron.Id), Length.Inches(40));
    static readonly Anchored TopPinned = new(RelationshipId.New(), Top.Id);

    static DesignEditor Table()
    {
        Sketch sketch = Sketch.Empty.WithEntity(Top).WithEntity(Leg).WithEntity(Apron);
        foreach (Relationship relationship in (Relationship[])[ApronOnLeg, LegInset, ApronLength, TopPinned])
        {
            sketch = sketch.WithRelationship(relationship);
        }

        DesignEditor editor = new();
        editor.Open(new Design("Table", sketch, ImmutableDictionary<EntityId, string>.Empty.Add(Top.Id, "Top").Add(Leg.Id, "Leg").Add(Apron.Id, "Apron")));
        return editor;
    }

    [Fact]
    public void Duplicating_two_parts_brings_the_relationship_between_them_and_nothing_else()
    {
        DesignEditor editor = Table();
        editor.SelectAll([Leg.Id, Apron.Id]);

        SelectionCommands.Duplicate(editor, gridStepInches: 1);

        Sketch after = editor.Sketch;
        Box[] copies = [.. editor.Selection.Select(after.Find<Box>).OfType<Box>()];
        Assert.Equal(2, copies.Length);
        Box legCopy = Assert.Single(copies, copy => copy.Width == Leg.Width);
        Box apronCopy = Assert.Single(copies, copy => copy.Width == Apron.Width);

        // The flush between them, renamed onto the copies; not the inset to the top, not the
        // apron's own typed length.
        Flush copied = Assert.Single(after.RelationshipsInOrder.OfType<Flush>(), flush => flush.References.Contains(legCopy.Id));
        Assert.Equal(new FeatureRef(legCopy.Id, BoxFeature.Face(BoxFace.East)), copied.A);
        Assert.Equal(new FeatureRef(apronCopy.Id, BoxFeature.Face(BoxFace.West)), copied.B);
        Assert.Equal(Table().Sketch.Relationships.Count + 1, after.Relationships.Count);

        // Moved together, level with the originals, and clear of them.
        Assert.Equal(legCopy.Anchor - Leg.Anchor, apronCopy.Anchor - Apron.Anchor);
        Assert.Equal(Length.Zero, (legCopy.Anchor - Leg.Anchor).Dz);

        Assert.True(editor.Undo());
        Assert.Equal(3, editor.Sketch.Entities.Count);
    }

    [Fact]
    public void A_leg_mirrored_east_to_west_lands_where_the_opposite_leg_goes()
    {
        DesignEditor editor = Table();
        editor.Select(Leg.Id);

        EntityId copy = SelectionCommands.Mirror(editor, Axis.X)!.Value;

        // The drawing spans 0 to 48 east-west; the leg's 1 1/2 to 4 reflects to 44 to 46 1/2.
        (Point3 low, Point3 high) = SpaceSnapResolver.Extent(editor.Sketch.Find<Box>(copy)!);
        Assert.Equal(new Point3(Length.Inches(44), Length.Inches(1, 1, 2), Length.Zero), low);
        Assert.Equal(Length.Inches(46, 1, 2), high.X);
        Assert.Equal(copy, editor.OnlySelected);
        Assert.Contains("Mirrored Leg east–west", editor.LastMessage!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Mirroring_a_leg_and_its_apron_mirrors_the_flush_between_them_and_it_still_holds()
    {
        DesignEditor editor = Table();
        editor.SelectAll([Leg.Id, Apron.Id]);

        SelectionCommands.Mirror(editor, Axis.X);

        Sketch after = editor.Sketch;
        Box[] copies = [.. editor.Selection.Select(after.Find<Box>).OfType<Box>()];
        Box legCopy = Assert.Single(copies, box => box.Width == Leg.Width);
        Box apronCopy = Assert.Single(copies, box => box.Width == Apron.Width);

        // Across the middle, the apron is west of the leg: the flush is the leg's west face and
        // the apron's east face now, and it holds where the copies are.
        Flush mirrored = Assert.Single(after.RelationshipsInOrder.OfType<Flush>(), flush => flush.References.Contains(legCopy.Id));
        Assert.Equal(new FeatureRef(legCopy.Id, BoxFeature.Face(BoxFace.West)), mirrored.A);
        Assert.Equal(new FeatureRef(apronCopy.Id, BoxFeature.Face(BoxFace.East)), mirrored.B);
        Assert.Equal(SpaceSnapResolver.Extent(legCopy).Low.X, SpaceSnapResolver.Extent(apronCopy).High.X);
    }

    [Fact]
    public void A_part_with_cuts_is_not_mirrored_the_wrong_way_round()
    {
        DesignEditor editor = Table();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(new SetCut(Leg.Id, new CornerCut(BoxCorner.NorthEast, Length.Inches(1), Length.Inches(1))), "cut the leg"));
        editor.SelectAll([Leg.Id, Apron.Id]);
        int before = editor.Sketch.Entities.Count;

        Assert.Null(SelectionCommands.Mirror(editor, Axis.X));
        Assert.Equal(before, editor.Sketch.Entities.Count);
        Assert.Contains("Leg has cuts", editor.LastMessage!.Text, StringComparison.Ordinal);
    }
}
