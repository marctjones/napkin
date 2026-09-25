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

    private static Box Plain(int id, double x, double y, double z, double width, double height, double depth) => new(
        new EntityId(new Guid(id, 0, 0, new byte[8])),
        LayerId.Default,
        new Point3(new Length((long)(x * 1024)), new Length((long)(y * 1024)), new Length((long)(z * 1024))),
        new Length((long)(width * 1024)),
        new Length((long)(height * 1024)),
        new Length((long)(depth * 1024)),
        BoxFace.Top,
        Angle.Zero);

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void Boxes_that_are_not_parts_are_read_by_their_smallest_size()
    {
        // A beam 12 x 4 x 3 with a 4 x 2 x 1/4 panel standing on its north face by its edge: the panel is thin
        // along z, its contact face (the south) is across y, so it is an edge, thinner than the beam, and
        // has margin on both sides of it on the beam's face: a groove. Its pocket faces, thin along z, are bottom and top.
        Sketch groove = Sketch.Empty.WithEntity(Plain(1, 0, 0, 0, 12, 4, 3)).WithEntity(Plain(2, 4, 4, 1, 4, 2, 0.25));
        JointProposal? proposal = JointProposals.For(groove, new EntityId(new Guid(1, 0, 0, new byte[8])), new EntityId(new Guid(2, 0, 0, new byte[8])));

        Assert.Equal(JointType.Groove, proposal!.Type);
        Assert.Equal(new EntityId(new Guid(2, 0, 0, new byte[8])), proposal.Roles.Inserted.Box);
        Assert.All(proposal.PocketFaces, face => Assert.True(face is BoxFace.Bottom or BoxFace.Top));

        // The same panel as thick as the beam is only a butt; and a panel lying across the beam's face by its broad face is one too.
        Sketch equal = Sketch.Empty.WithEntity(Plain(1, 0, 0, 0, 12, 4, 3)).WithEntity(Plain(2, 4, 4, 1, 4, 2, 3));
        Assert.Equal(JointType.Butt, JointProposals.For(equal, new EntityId(new Guid(1, 0, 0, new byte[8])), new EntityId(new Guid(2, 0, 0, new byte[8])))!.Type);
        Sketch flat = Sketch.Empty.WithEntity(Plain(1, 0, 0, 0, 12, 4, 3)).WithEntity(Plain(2, 4, 4, 1, 4, 0.25, 2));
        Assert.Equal(JointType.Butt, JointProposals.For(flat, new EntityId(new Guid(1, 0, 0, new byte[8])), new EntityId(new Guid(2, 0, 0, new byte[8])))!.Type);
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void A_top_on_a_square_leg_is_a_butt_not_a_tabletop()
    {
        Sketch sketch = Table();
        EntityId top = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Top").Id;
        EntityId leg = sketch.Entities.Values.OfType<Box>().First(box => box.Name == "Leg, south-west").Id;

        // The leg's top (1 1/2 x 1 1/2) meets the top's underside: a bottom face, but not long and thin like an apron's edge.
        Assert.Equal(JointType.Butt, JointProposals.For(sketch, top, leg)!.Type);
    }

    [Theory]
    [InlineData(PartDimension.Thickness, PartDimension.Length, BoxFace.South, Axis.X)]
    [InlineData(PartDimension.Length, PartDimension.Thickness, BoxFace.West, Axis.Y)]
    [InlineData(PartDimension.Length, PartDimension.Width, BoxFace.West, Axis.Z)]
    [Trait("Feature", "GEO-018")]
    public void Pocket_holes_are_offered_from_the_faces_across_the_parts_thickness(PartDimension x, PartDimension y, BoxFace contact, Axis thin)
    {
        // A part whose thickness lies along x, y or z (by its plan axes), joined at a face on another axis:
        // the faces offered are its two broad ones, the pair across its thickness.
        Box box = Plain(1, 0, 0, 0, 6, 4, 2) with { Part = new Part(null, null, 1, new PlanAxes(x, y)) };
        ImmutableArray<BoxFace> faces = JointProposals.PocketFacesFor(Sketch.Empty.WithEntity(box), box, contact);

        Assert.NotEmpty(faces);
        Assert.All(faces, face => Assert.Equal(thin, JointGeometry.LocalAxisOf(face)));
    }
}
