using Napkin.Core.Geometry;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// A joint is a relationship between one face of each of two parts (joinery note §4): stored
/// data that neither propagates nor refuses an edit, is unsatisfied when the parts stop touching,
/// and goes with either part. Every expectation below is worked out by hand in a comment.
/// </summary>
public class JointTests
{
    // The scene, in whole inches, every box lying as drawn (faceUp top, so local axes are world axes):
    //   Leg   at (0,0,0), 2 wide (x) by 2 deep (y) by 16 tall (z):   x 0..2,  y 0..2,  z 0..16
    //   Apron at (2,0,10), 10 long (x) by 2 (y) by 4 tall (z):       x 2..12, y 0..2,  z 10..14
    // The leg's east face is the plane x = 2 and the apron's west face is the plane x = 2.
    private static (Sketch Sketch, EntityId Leg, EntityId Apron) Frame()
    {
        SketchBuilder builder = new();
        EntityId leg = builder.AddBox(Point3.Inches(0, 0, 0), 2, 2, 16, name: "Leg");
        EntityId apron = builder.AddBox(Point3.Inches(2, 0, 10), 10, 2, 4, name: "Apron");
        return (builder.Sketch, leg, apron);
    }

    private static readonly RelationshipId JointId = new(Guid.Parse("0192f1a0-0000-4000-8000-000000000101"));

    private static Joint Butt(EntityId leg, EntityId apron, Fastening? fastening = null, JointType type = JointType.Butt, Length? depth = null)
        => new(
            JointId,
            new FeatureRef(leg, BoxFeature.Face(BoxFace.East)),
            new FeatureRef(apron, BoxFeature.Face(BoxFace.West)),
            type,
            depth,
            fastening ?? new Fastening(FasteningKind.PocketScrews, null, BoxFace.South),
            true);

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void The_contact_of_an_apron_end_on_a_leg_face_is_the_rectangle_they_share()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint joint = Butt(leg, apron);

        JointContact contact = Assert.IsType<JointContact>(JointGeometry.Contact(sketch.WithRelationship(joint), joint));

        // Plane x = 2. y: leg 0..2 meets apron 0..2 -> 0..2. z: leg 0..16 meets apron 10..14 -> 10..14.
        Assert.Equal(Axis.X, contact.Normal);
        Assert.Equal(Point3.Inches(2, 0, 10), contact.Low);
        Assert.Equal(Point3.Inches(2, 2, 14), contact.High);
        Assert.Equal(Length.Inches(4), contact.JointLength);   // the 4 in side (z)
        Assert.Equal(Length.Inches(2), contact.JointWidth);    // the 2 in side (y)
        Assert.Equal(Point3.Inches(2, 1, 12), contact.Centre);
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_joint_between_touching_parts_is_satisfied_and_the_checker_never_lists_it()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint joint = Butt(leg, apron);
        sketch = sketch.WithRelationship(joint);

        Assert.True(sketch.Validate().IsValid);
        Assert.True(RelationshipChecker.IsSatisfied(sketch, joint));
        Assert.True(RelationshipChecker.Check(sketch).AllHold);
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void Moving_a_part_away_leaves_the_joint_unsatisfied_and_the_move_is_not_refused_and_undoing_it_restores_it()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint joint = Butt(leg, apron);
        sketch = sketch.WithRelationship(joint);

        // The apron one inch east is at x 3..13: its west face is the plane x = 3, not 2.
        Solved moved = Assert.IsType<Solved>(
            DirectUpdater.Instance.Apply(sketch, new SetPosition(apron, Point3.Inches(3, 0, 10))));

        Assert.Equal(joint, moved.Sketch.Find(joint.Id));                       // the joint itself is untouched
        Assert.False(RelationshipChecker.IsSatisfied(moved.Sketch, joint));      // and reports it is not satisfied
        Assert.True(RelationshipChecker.Check(moved.Sketch).AllHold);            // yet nothing is a violation
        Assert.True(moved.Sketch.Validate().IsValid);                            // and the sketch is valid

        // Undo is the sketch value before; moving the apron back restores it just the same.
        Solved back = Assert.IsType<Solved>(
            DirectUpdater.Instance.Apply(moved.Sketch, new SetPosition(apron, Point3.Inches(2, 0, 10))));
        Assert.True(RelationshipChecker.IsSatisfied(back.Sketch, joint));
        Assert.Equal(sketch, back.Sketch);
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void Sliding_a_part_along_the_contact_keeps_the_joint_while_they_still_overlap_and_loses_it_when_they_do_not()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint joint = Butt(leg, apron);
        sketch = sketch.WithRelationship(joint);

        // Apron z 10..14 moved up 4 is z 14..18: it still meets the leg's 0..16 over 14..16 (2 in).
        Solved slid = Assert.IsType<Solved>(
            DirectUpdater.Instance.Apply(sketch, new SetPosition(apron, Point3.Inches(2, 0, 14))));
        JointContact contact = Assert.IsType<JointContact>(JointGeometry.Contact(slid.Sketch, joint));
        Assert.Equal(Length.Inches(2), contact.Extent(Axis.Z));

        // Moved up 6 it is z 16..20, which only touches the leg's top edge: no overlap, no contact.
        Solved off = Assert.IsType<Solved>(
            DirectUpdater.Instance.Apply(sketch, new SetPosition(apron, Point3.Inches(2, 0, 16))));
        Assert.Null(JointGeometry.Contact(off.Sketch, joint));
        Assert.False(RelationshipChecker.IsSatisfied(off.Sketch, joint));
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void Resizing_a_part_with_a_joint_is_solved_and_leaves_the_joint_as_it_was()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint joint = Butt(leg, apron);
        ParamValue length = new(new RelationshipId(Guid.Parse("0192f1a0-0000-4000-8000-000000000102")), new BoxWidthRef(apron), Length.Inches(10));
        sketch = sketch.WithRelationship(joint).WithRelationship(length);

        Solved resized = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, new SetParameter(length.Id, Length.Inches(8))));

        Assert.Equal(Length.Inches(8), resized.Sketch.Find<Box>(apron)!.Width);
        Assert.Equal(joint, resized.Sketch.Find(joint.Id));
        Assert.True(RelationshipChecker.IsSatisfied(resized.Sketch, joint));    // its west face has not moved
        Assert.DoesNotContain(joint.Id, resized.Changes.RelationshipsAdded);
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_joint_is_added_through_the_updater_and_removing_either_part_removes_it()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint joint = Butt(leg, apron);

        Solved added = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, new AddRelationship(joint)));
        Assert.Equal(joint, added.Sketch.Find(joint.Id));

        foreach (EntityId part in new[] { leg, apron })
        {
            Solved removed = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(added.Sketch, new RemoveEntity(part)));
            Assert.Null(removed.Sketch.Find(joint.Id));
            Assert.Contains(joint.Id, removed.Changes.RelationshipsRemoved);
        }
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_joint_that_could_never_touch_is_refused_naming_both_faces()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();

        // The leg's east face fixes x; the apron's top face fixes z.
        Joint joint = Butt(leg, apron) with { Inserted = new FeatureRef(apron, BoxFeature.Face(BoxFace.Top)) };
        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(sketch, new AddRelationship(joint)));

        Assert.Equal(RejectionReason.PlacesNotComparable, rejected.Reason);
        Assert.Contains("fixing exactly one axis, the same one", rejected.Detail!.Message, StringComparison.Ordinal);
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_joint_whose_own_fields_break_a_rule_is_refused_by_the_updater()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Joint noDepth = Butt(leg, apron, Fastening.None, JointType.Groove, depth: null);

        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(sketch, new AddRelationship(noDepth)));

        Assert.Equal(RejectionReason.InvalidJoint, rejected.Reason);
        Assert.Contains("depth", rejected.Detail!.Message, StringComparison.Ordinal);
    }

    [Trait("Feature", "GEO-016")]
    [Theory]
    [InlineData(JointType.Butt, FasteningKind.PocketScrews, true)]
    [InlineData(JointType.Butt, FasteningKind.Clips, false)]
    [InlineData(JointType.Groove, FasteningKind.Brads, true)]
    [InlineData(JointType.Groove, FasteningKind.PocketScrews, false)]
    [InlineData(JointType.Groove, FasteningKind.Screws, false)]
    [InlineData(JointType.Rabbet, FasteningKind.Screws, true)]
    [InlineData(JointType.Rabbet, FasteningKind.Dowels, false)]
    [InlineData(JointType.HalfLap, FasteningKind.Dowels, true)]
    [InlineData(JointType.HalfLap, FasteningKind.Brads, false)]
    [InlineData(JointType.Tabletop, FasteningKind.Clips, true)]
    [InlineData(JointType.Tabletop, FasteningKind.None, false)]
    public void The_allowed_pairs_are_exactly_the_table_of_section_3_3(JointType type, FasteningKind kind, bool allowed)
    {
        Assert.Equal(allowed, JointRules.AllowedFastenings(type).Contains(kind));
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_depth_that_is_not_less_than_the_receiving_thickness_is_unsatisfied_and_never_refused()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();

        // The leg's east face is cut across its width, 2 in: a 1 in rabbet fits, a 2 in one is a slot.
        Joint fits = Butt(leg, apron, Fastening.None, JointType.Rabbet, Length.Inches(1));
        Joint slot = Butt(leg, apron, Fastening.None, JointType.Rabbet, Length.Inches(2));

        Assert.True(JointGeometry.IsSatisfied(sketch.WithRelationship(fits), fits));
        Sketch withSlot = sketch.WithRelationship(slot);
        Assert.True(withSlot.Validate().IsValid);
        Assert.False(JointGeometry.IsSatisfied(withSlot, slot));
        Assert.True(RelationshipChecker.Check(withSlot).AllHold);
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_half_lap_between_parts_of_different_thickness_is_unsatisfied()
    {
        // Two flat boards whose top faces are both the plane z = 2: A is 2 thick (z 0..2) and B is
        // 1 thick (z 1..2). They overlap in plan over x 4..6, so the two top faces share a rectangle.
        SketchBuilder builder = new();
        EntityId a = builder.AddBox(Point3.Inches(0, 0, 0), 6, 2, 2);
        EntityId b = builder.AddBox(Point3.Inches(4, 0, 1), 6, 2, 1);
        Joint lap = new(
            JointId,
            new FeatureRef(a, BoxFeature.Face(BoxFace.Top)),
            new FeatureRef(b, BoxFeature.Face(BoxFace.Top)),
            JointType.HalfLap, null, Fastening.None, true);
        Sketch sketch = builder.Sketch.WithRelationship(lap);

        Assert.NotNull(JointGeometry.Contact(sketch, lap));   // the faces do touch,
        Assert.False(JointGeometry.IsSatisfied(sketch, lap)); // but 2 in against 1 in is not a half-lap
    }

    [Trait("Feature", "GEO-016")]
    [Fact]
    public void A_sketch_with_a_joint_is_equal_to_itself_by_value_and_to_a_copy_with_the_same_typed_lists()
    {
        (Sketch sketch, EntityId leg, EntityId apron) = Frame();
        Sketch withJoint = sketch.WithRelationship(Butt(leg, apron));
        Sketch a = withJoint with { Supplies = [new SupplyLine("Wood glue", string.Empty)] };
        Sketch b = withJoint with { Supplies = [new SupplyLine("Wood glue", string.Empty)] };
        Sketch c = withJoint with { Supplies = [new SupplyLine("Finish", string.Empty)] };

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, withJoint);
    }
}
