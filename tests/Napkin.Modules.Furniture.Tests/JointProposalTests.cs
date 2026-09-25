using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// What the join tool proposes (docs/design/joinery-and-fasteners.md &#xA7;5.1, &#xA7;6.2) for each of the
/// DIY coffee table's 34 joints, against what the builder recorded and the note's arithmetic says. The
/// expected types are worked out from the note's table in &#xA7;2.3, not from napkin's output.
/// </summary>
public sealed class JointProposalTests
{
    private static Sketch Table() => Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("diy-coffee-table-drawers"))).Sketch;

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void Every_joint_of_the_table_is_proposed_between_its_own_faces_with_the_type_its_contact_suggests()
    {
        Sketch sketch = Table();
        Joint[] joints = [.. sketch.RelationshipsInOrder.OfType<Joint>()];
        Assert.Equal(34, joints.Length);

        foreach (Joint joint in joints)
        {
            JointProposal proposal = JointProposals.For(sketch, joint.Receiving.Box, joint.Inserted.Box)!;
            string who = $"{sketch.Find(joint.Inserted.Box)!.Name} -> {sketch.Find(joint.Receiving.Box)!.Name}";

            // Note 2.3: a rabbet cannot be told from a butt by looking, so the tool suggests a butt for J7 and the
            // person chooses; everything else is what the builder chose.
            JointType want = joint.Type == JointType.Rabbet ? JointType.Butt : joint.Type;
            Assert.True(want == proposal.Type, $"{who}: wanted {want}, proposed {proposal.Type}");

            // The stored faces are the ones the contact is on, and the tool's direction rule names the same receiver.
            Assert.True(joint.Receiving.Box == proposal.Roles.Receiving.Box, $"{who}: receiver");
            Assert.True(
                joint.Type == JointType.Rabbet || joint.Receiving.Feature.Faces[0] == proposal.Roles.Receiving.Feature.Faces[0],
                $"{who}: receiving face");
        }
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void The_direction_rule_asks_only_where_it_cannot_decide()
    {
        Sketch sketch = Table();
        Joint cleat = Assert.Single(sketch.RelationshipsInOrder.OfType<Joint>(), joint => sketch.Find(joint.Inserted.Box)!.Name == "Slide cleat, west");
        Joint apronOnLeg = sketch.RelationshipsInOrder.OfType<Joint>().First();

        // A cleat lies face to face on the apron: neither part presents an end, so the tool asks (note 4.1).
        Assert.True(JointProposals.For(sketch, cleat.Receiving.Box, cleat.Inserted.Box)!.Roles.Ambiguous);

        // An apron's end on a leg's face is decided by the rule: no question.
        Assert.False(JointProposals.For(sketch, apronOnLeg.Receiving.Box, apronOnLeg.Inserted.Box)!.Roles.Ambiguous);
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void Pocket_holes_default_to_the_inside_face_and_a_tie_asks()
    {
        Sketch sketch = Table();
        Joint[] pocket = [.. sketch.RelationshipsInOrder.OfType<Joint>().Where(joint => joint.Fastening.Kind == FasteningKind.PocketScrews)];
        Assert.Equal(10, pocket.Length);

        // J1-J3 and their partners (the aprons and the rail, whose inside is the face nearer the middle of everything)
        // are the builder's own picks; the web (J9, J10) is at the centre, so both of its broad faces are offered,
        // the low one first (west), which is what the builder recorded.
        for (int i = 0; i < 8; i++)
        {
            JointProposal proposal = JointProposals.For(sketch, pocket[i].Receiving.Box, pocket[i].Inserted.Box)!;
            Assert.False(proposal.PocketFaceTied);
            Assert.Equal(pocket[i].Fastening.PocketFace, proposal.PocketFace);
        }

        foreach (Joint web in pocket.Skip(8))
        {
            JointProposal proposal = JointProposals.For(sketch, web.Receiving.Box, web.Inserted.Box)!;
            Assert.True(proposal.PocketFaceTied);
            Assert.Equal([BoxFace.West, BoxFace.East], proposal.PocketFaces);
            Assert.Equal(web.Fastening.PocketFace, proposal.PocketFace);
        }
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void Parts_that_do_not_touch_have_no_proposal_and_a_lap_is_suggested_for_overlapping_equal_boards()
    {
        Sketch sketch = Table();
        EntityId leg = sketch.Entities.Values.OfType<Box>().First(box => box.Name == "Leg, south-west").Id;
        EntityId top = sketch.Entities.Values.OfType<Box>().First(box => box.Name == "Drawer bottom, B").Id;
        Assert.Null(JointProposals.For(sketch, leg, top));
        Assert.Null(JointProposals.For(sketch, leg, leg));

        // Two 1 1/2-thick boards crossing, one lying over the other's end, lap: overlap with volume in one thickness.
        Box a = sketch.Find<Box>(leg)!;
        Box b = a with { Id = new EntityId(Guid.NewGuid()), Anchor = a.Anchor with { X = a.Anchor.X + new Length(768) } };
        Sketch crossed = sketch.WithEntity(b);

        Assert.Equal(JointType.HalfLap, JointProposals.For(crossed, a.Id, b.Id)!.Type);
    }
}
