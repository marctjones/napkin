namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The furniture marks (format version 14, docs/design/furniture-checks.md §4.2, §9.1): what the piece
/// is and whether it is anchored, set in one step; a part's drawer mark, part of its value, refused
/// when the drawer opens no distance.
/// </summary>
public class FurnitureMarksTests
{
    static readonly IGeometryUpdater Updater = DirectUpdater.Instance;

    static Part Plain() => new(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width));

    [Fact]
    public void The_marks_are_set_in_one_step_and_are_part_of_the_sketchs_value()
    {
        SketchBuilder builder = new();
        FurnitureMarks dresser = new(FurnitureKind.ClothingStorage, true);
        Solved set = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetFurnitureMarks(dresser)));

        Assert.Equal(dresser, set.Sketch.Furniture);
        Assert.Equal(FurnitureMarks.None, builder.Sketch.Furniture);
        Assert.NotEqual(builder.Sketch, set.Sketch);
        Assert.Equal(builder.Sketch, Assert.IsType<Solved>(Updater.Apply(set.Sketch, new SetFurnitureMarks(FurnitureMarks.None))).Sketch);
    }

    [Fact]
    public void A_drawer_mark_is_part_of_a_parts_value_and_one_that_opens_no_distance_is_refused()
    {
        Part drawer = Plain() with { Drawer = new DrawerMark(Length.Inches(12)) };
        Assert.NotEqual(Plain(), drawer);
        Assert.Equal(drawer, Plain() with { Drawer = new DrawerMark(Length.Inches(12)) });
        Assert.Equal(drawer.GetHashCode(), (Plain() with { Drawer = new DrawerMark(Length.Inches(12)) }).GetHashCode());

        SketchBuilder builder = new();
        EntityId front = builder.AddBox(0, 0, 16, 1);
        Solved marked = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetPart(front, drawer)));
        Assert.Equal(Length.Inches(12), marked.Sketch.Find<Box>(front)!.Part!.Drawer!.Extension);

        foreach (Length none in new[] { Length.Zero, new Length(-1) })
        {
            Assert.Equal(
                RejectionReason.NonPositiveSize,
                Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetPart(front, Plain() with { Drawer = new DrawerMark(none) }))).Reason);
        }
    }
}
