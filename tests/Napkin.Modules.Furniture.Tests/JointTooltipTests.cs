using Napkin.Core.Geometry;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>A joint in a sentence (joinery note &#xA7;5.3), on the DIY coffee table; every sentence written out by hand from note &#xA7;2.3.</summary>
public sealed class JointTooltipTests
{
    private static Sketch Table() => Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("diy-coffee-table-drawers"))).Sketch;

    private static string Of(Sketch sketch, string inserted, string receiving)
        => JointTooltip.Of(sketch, Assert.Single(
            sketch.RelationshipsInOrder.OfType<Joint>(),
            joint => sketch.Find(joint.Inserted.Box)!.Name == inserted && sketch.Find(joint.Receiving.Box)!.Name == receiving));

    [Fact]
    [Trait("Feature", "CUT-014")]
    public void Each_kind_of_joint_reads_as_the_note_shows()
    {
        Sketch sketch = Table();

        // J1: 5 1/2 in contact, one pocket screw per 2 in, ceil(2.75) = 3; the typed size; glued.
        Assert.Equal("Butt — Leg, north-west ← Apron, back. 3 pocket screws (1-1/4 in coarse), glue.", Of(sketch, "Apron, back", "Leg, north-west"));

        // J7: a rabbet 1/4 deep, as wide as the 1/2 in box front; 3 brads for 3 1/2 in; glued.
        Assert.Equal(
            "Rabbet — Drawer side, left, A ← Drawer box front, A. 1/4\" deep, 1/2\" wide. 3 brads (18 ga x 1), glue.",
            Of(sketch, "Drawer box front, A", "Drawer side, left, A"));

        // J9: a groove 1/4 deep for the 1/4 in bottom; nothing fastens it and it is not glued.
        Assert.Equal(
            "Groove — Drawer side, left, A ← Drawer bottom, A. 1/4\" deep, 1/4\" wide.",
            Of(sketch, "Drawer bottom, A", "Drawer side, left, A"));

        // J10: the count was typed as 4 over the recipe's 3; no glue.
        Assert.Equal("Butt — Drawer front, A ← Drawer box front, A. 4 wood screws (#8 x 1).", Of(sketch, "Drawer box front, A", "Drawer front, A"));

        // J11: 36 in apron, one clip per 12 in, 3, no thickness in the choice.
        Assert.Equal("Tabletop — Top ← Apron, back. 3 tabletop clips (figure-8, with screws).", Of(sketch, "Apron, back", "Top"));
    }

    [Fact]
    [Trait("Feature", "CUT-014")]
    public void A_missing_size_says_so_and_parts_that_have_moved_apart_say_that_and_lose_the_recipe_count()
    {
        Sketch sketch = Table() with { FastenerChoices = [] };
        Assert.Equal("Butt — Apron, back ← Web. 3 pocket screws (size not chosen), glue.", Of(sketch, "Web", "Apron, back"));

        Box web = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Web");
        Sketch apart = sketch.WithEntity(web with { Anchor = web.Anchor with { Y = web.Anchor.Y + new Length(1024) } });

        // A 1 in move north leaves the web's north end 1 in short of the apron: no contact, so no recipe count.
        Assert.Equal("Butt (parts no longer touch) — Apron, back ← Web. Pocket screws (size not chosen), glue.", Of(apart, "Web", "Apron, back"));
    }

    [Fact]
    [Trait("Feature", "CUT-014")]
    public void A_marker_carries_the_letter_of_its_type()
    {
        Assert.Equal(
            "BGRLT",
            string.Concat(new[] { JointType.Butt, JointType.Groove, JointType.Rabbet, JointType.HalfLap, JointType.Tabletop }.Select(JointTooltip.Letter)));
        Assert.Equal("Half-lap", JointTooltip.TypeName(JointType.HalfLap));
    }

    [Fact]
    [Trait("Feature", "CUT-014")]
    public void A_rabbet_that_moved_apart_keeps_its_depth_and_a_single_fastener_is_singular()
    {
        Sketch sketch = Table();
        Box front = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Drawer box front, A");
        Sketch apart = sketch.WithEntity(front with { Anchor = front.Anchor with { X = front.Anchor.X + new Length(4096) } });

        // No contact, so no width and no recipe count; the depth is the joint's own.
        Assert.Equal(
            "Rabbet (parts no longer touch) \u2014 Drawer side, left, A \u2190 Drawer box front, A. 1/4\" deep. Brads (18 ga x 1), glue.",
            Of(apart, "Drawer box front, A", "Drawer side, left, A").Replace("brads (", "Brads (", StringComparison.Ordinal));

        Joint one = apart.RelationshipsInOrder.OfType<Joint>().First() with { Fastening = new Fastening(FasteningKind.PocketScrews, 1, BoxFace.South) };
        Assert.Contains("1 pocket screw (1-1/4 in coarse)", JointTooltip.Of(sketch, one), StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "CUT-014")]
    public void A_part_without_a_name_is_called_a_part_unless_the_caller_names_it()
    {
        Sketch sketch = Table();
        Joint joint = sketch.RelationshipsInOrder.OfType<Joint>().First();
        Sketch unnamed = sketch.WithEntity(sketch.Find<Box>(joint.Receiving.Box)! with { Name = string.Empty });

        Assert.StartsWith("Butt \u2014 a part \u2190 Apron, back.", JointTooltip.Of(unnamed, joint), StringComparison.Ordinal);
        Assert.StartsWith("Butt \u2014 X \u2190 X.", JointTooltip.Of(unnamed, joint, _ => "X"), StringComparison.Ordinal);
    }
}
