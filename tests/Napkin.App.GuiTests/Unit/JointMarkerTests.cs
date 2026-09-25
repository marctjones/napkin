using System.Collections.Immutable;

using Avalonia;

using Napkin.App.Editing;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// Where a joint's marker goes, which ones are drawn, which one a click finds, and how a joint is selected
/// (joinery note &#xA7;5.2). Positions are worked out by hand from the DIY coffee table's sizes in the comments.
/// </summary>
public class JointMarkerTests
{
    private static Sketch Table() => SampleExpectations.Sample("diy-coffee-table-drawers").Load().Sketch;

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void A_marker_sits_at_the_middle_of_the_contact_and_points_at_the_inserted_part()
    {
        Sketch sketch = Table();
        Joint first = sketch.RelationshipsInOrder.OfType<Joint>().First();   // J1: the back apron's west end on the north-west leg
        JointMarker marker = JointMarkers.Of(sketch).Single(candidate => candidate.Id == first.Id);

        // The leg's east face is at x = 3; the apron is 3/4 thick against the outside (y 19 3/4 to 20 1/2) and runs
        // from 10 3/4 to 16 1/4 up. Middle: x 3, y 20 1/8, z 13 1/2, in 1/1024ths.
        Assert.Equal(new Point3(new Length(3 * 1024), new Length(20608), new Length(13824)), marker.Centre);
        Assert.Equal(('B', true), (marker.Letter, marker.Satisfied));

        // The tick points along the apron: its own middle is 18 to the east of the leg's face.
        Assert.Equal(new Length(21 * 1024), marker.Toward.X);
        Assert.Equal(34, JointMarkers.Of(sketch).Length);
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void A_joint_whose_parts_moved_apart_is_hollow_and_sits_on_the_inserted_parts_face()
    {
        Sketch sketch = Table();
        Box web = sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Web");
        Sketch apart = sketch.WithEntity(web with { Anchor = web.Anchor with { Y = web.Anchor.Y + new Length(1024) } });
        Joint toApron = apart.RelationshipsInOrder.OfType<Joint>().Single(joint => joint.Inserted.Box == web.Id && apart.Find(joint.Receiving.Box)!.Name == "Apron, back");

        JointMarker marker = JointMarkers.Of(apart).Single(candidate => candidate.Id == toApron.Id);

        Assert.False(marker.Satisfied);
        Assert.Equal(1, JointMarkers.Of(apart).Count(candidate => candidate.Id == toApron.Id));
        Assert.Equal(2, JointMarkers.Of(apart).Count(candidate => !candidate.Satisfied));
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void Markers_within_two_radii_are_dropped_and_the_selected_one_always_stays()
    {
        JointMarker At(byte id, long x) => new(new RelationshipId(new Guid(id, 0, 0, new byte[8])), new Point3(new Length(x), Length.Zero, Length.Zero), new Point3(new Length(x), Length.Zero, Length.Zero), 'B', true);
        JointMarker[] markers = [At(1, 0), At(2, 10), At(3, 100)];
        Point Project(Point3 point) => new(point.X.Units, 0);

        // 0 and 10 are 10 px apart, under two radii (16): the first wins and the second is dropped; 100 is clear.
        Assert.Equal([1, 3], JointMarkers.Layout(markers, Project, null).Select(placed => (int)placed.Marker.Id.Value.ToByteArray()[0]));

        // Selecting the second makes it the one that stays.
        Assert.Equal([2, 3], JointMarkers.Layout(markers, Project, markers[1].Id).Select(placed => (int)placed.Marker.Id.Value.ToByteArray()[0]));
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void A_click_finds_the_nearest_marker_within_its_radius_and_nothing_further()
    {
        PlacedJointMarker Placed(byte id, double x) => new(
            new JointMarker(new RelationshipId(new Guid(id, 0, 0, new byte[8])), default, default, 'B', true),
            new Point(x, 50),
            new Point(x, 50));
        ImmutableArray<PlacedJointMarker> placed = [Placed(1, 100), Placed(2, 130)];

        Assert.Equal(new RelationshipId(new Guid(1, 0, 0, new byte[8])), JointMarkers.HitTest(placed, new Point(105, 52))!.Value.Marker.Id);
        Assert.Equal(new RelationshipId(new Guid(2, 0, 0, new byte[8])), JointMarkers.HitTest(placed, new Point(120, 50))!.Value.Marker.Id);
        Assert.Null(JointMarkers.HitTest(placed, new Point(115, 50 + JointMarkers.Radius + 4)));
        Assert.Null(JointMarkers.HitTest(placed, new Point(400, 50)));
    }

    [Fact]
    [Trait("Feature", "GEO-018")]
    public void Selecting_a_joint_drops_the_parts_and_selecting_a_part_drops_the_joint()
    {
        DesignEditor editor = new();
        editor.Open(SampleExpectations.Sample("diy-coffee-table-drawers").Load());
        Joint joint = editor.Sketch.RelationshipsInOrder.OfType<Joint>().First();
        EntityId leg = editor.Sketch.Entities.Values.OfType<Box>().First(box => box.Name == "Leg, south-west").Id;
        int changes = 0;
        editor.SelectionChanged += (_, _) => changes++;

        editor.Select(leg);
        editor.SelectJoint(joint.Id);
        Assert.Equal((joint.Id, 0), (editor.SelectedJoint!.Value, editor.Selection.Count));

        editor.Select(leg);
        Assert.Equal((null, 1), (editor.SelectedJoint, editor.Selection.Count));

        editor.SelectJoint(joint.Id);
        editor.ClearSelection();
        Assert.Equal((null, 0), (editor.SelectedJoint, editor.Selection.Count));

        // A joint that does not exist, or an entity that is not a joint, selects nothing; a removed joint is not selected.
        editor.SelectJoint(new RelationshipId(Guid.NewGuid()));
        Assert.Null(editor.SelectedJoint);
        editor.SelectJoint(joint.Id);
        editor.Apply(new RemoveRelationship(joint.Id), "remove");
        Assert.Null(editor.SelectedJoint);
        Assert.True(changes >= 4);
    }
}
