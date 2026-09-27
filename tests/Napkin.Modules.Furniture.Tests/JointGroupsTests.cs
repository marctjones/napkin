using Napkin.Core.Geometry;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Jointed groups and drawers (docs/design/furniture-checks.md §4.2, #218), on the DIY coffee table:
/// the carcass is one group and each drawer box another, because slides are hardware, not joints.
/// </summary>
public class JointGroupsTests
{
    static Sketch Diy() => Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", "diy-coffee-table-drawers.scene.json"))).Sketch;

    static Box Named(Sketch sketch, string name) => sketch.Entities.Values.OfType<Box>().Single(box => box.Name == name);

    static Sketch Mark(Sketch sketch, string name, long inches)
    {
        Box box = Named(sketch, name);
        return sketch.WithEntity(box with { Part = box.Part! with { Drawer = new DrawerMark(Length.Inches(inches)) } });
    }

    [Fact]
    [Trait("Feature", "FURN-002")]
    public void The_carcass_and_each_drawer_box_are_separate_groups()
    {
        Sketch sketch = Diy();
        string[][] groups = [.. JointGroups.Of(sketch).Select(group => group.Select(box => box.Name).Order(StringComparer.Ordinal).ToArray())];

        Assert.Equal(3, groups.Length);
        Assert.Contains(groups, group => group.SequenceEqual(new[] { "Drawer bottom, A", "Drawer box back, A", "Drawer box front, A", "Drawer front, A", "Drawer side, left, A", "Drawer side, right, A" }));
        Assert.Contains(groups, group => group.Contains("Top") && group.Contains("Leg, south-west") && group.Contains("Slide cleat, east"));
        Assert.Empty(JointGroups.Drawers(sketch));
    }

    [Fact]
    [Trait("Feature", "FURN-002")]
    public void A_drawer_is_the_marked_parts_group_and_a_second_mark_in_it_is_said_not_read()
    {
        Sketch sketch = Mark(Diy(), "Drawer front, A", 14);
        Drawer drawer = Assert.Single(JointGroups.Drawers(sketch));
        Assert.Equal(("Drawer front, A", 6, Length.Inches(14), (string?)null), (drawer.Marked.Name, drawer.Members.Length, drawer.Extension, drawer.Note));

        Sketch twice = Mark(sketch, "Drawer side, left, A", 10);
        Drawer first = Assert.Single(JointGroups.Drawers(twice));
        Assert.Equal(Named(twice, "Drawer side, left, A").Id.CompareTo(Named(twice, "Drawer front, A").Id) < 0 ? "Drawer side, left, A" : "Drawer front, A", first.Marked.Name);
        Assert.Contains("is marked a drawer too", first.Note, StringComparison.Ordinal);

        // Both drawers marked: two drawers, one per group.
        Assert.Equal(2, JointGroups.Drawers(Mark(sketch, "Drawer front, B", 14)).Length);
    }

    [Fact]
    public void A_sketch_with_no_joints_is_one_group_per_part()
    {
        Sketch sketch = Diy();
        Sketch bare = sketch.RelationshipsInOrder.OfType<Joint>().Aggregate(sketch, (with, joint) => with.WithoutRelationship(joint.Id));
        Assert.Equal(bare.Entities.Values.OfType<Box>().Count(), JointGroups.Of(bare).Length);
    }
}
