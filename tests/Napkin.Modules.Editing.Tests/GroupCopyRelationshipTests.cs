using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Which relationships travel with a copy of several parts, and how each kind is re-pointed at the
/// copies — and, for a mirror, turned: a face that faced east now faces west and a distance measured
/// east is measured west.
/// </summary>
public class GroupCopyRelationshipTests
{
    // Three 4" x 2" x 1" parts in a row along X: A at 0, B at 7, C at 14.
    static readonly Box A = Part(0, 0);
    static readonly Box B = Part(1, 7);
    static readonly Box C = Part(2, 14);

    static Box Part(int index, long x) =>
        Box.AsDrawn(EditingBuilder.Id(index), LayerId.Default, Point2.Inches(x, 0), Length.Inches(4), Length.Inches(2), Length.Inches(1), Angle.Zero);

    static FeatureRef Face(Box box, BoxFace face) => new(box.Id, BoxFeature.Face(face));

    static readonly Relationship Gap = new AxisDistance(RelationshipId.New(), Face(A, BoxFace.East), Face(B, BoxFace.West), Axis.X, Length.Inches(3));
    static readonly Relationship Middle = new Centered(RelationshipId.New(), new CenterRef(B.Id), Face(A, BoxFace.West), Face(C, BoxFace.East), Axis.X);
    // A coincident needs two shared axes: the bottom south edges, both at Y = 0 and Z = 0.
    static readonly BoxFeature BottomSouth = BoxFeature.Edge(BoxFace.South, BoxFace.Bottom);
    static readonly Relationship Level = new Coincident(RelationshipId.New(), new FeatureRef(A.Id, BottomSouth), new FeatureRef(C.Id, BottomSouth));
    static readonly Relationship SameDepth = new EqualParam(RelationshipId.New(), new BoxDepthRef(A.Id), new BoxDepthRef(C.Id));
    static readonly Relationship SameHeight = new EqualParam(RelationshipId.New(), new BoxHeightRef(A.Id), new BoxHeightRef(B.Id));
    static readonly Relationship SameWidth = new EqualParam(RelationshipId.New(), new BoxWidthRef(A.Id), new BoxWidthRef(C.Id));
    static readonly Relationship Pin = new Anchored(RelationshipId.New(), A.Id);

    static Sketch Scene => Sketch.Empty
        .WithEntity(A).WithEntity(B).WithEntity(C)
        .WithRelationship(Gap).WithRelationship(Middle).WithRelationship(Level)
        .WithRelationship(SameDepth).WithRelationship(SameHeight).WithRelationship(SameWidth).WithRelationship(Pin);

    static ImmutableArray<Relationship> Added(ImmutableList<Request> requests) =>
        [.. requests.OfType<AddRelationship>().Select(add => add.Relationship)];

    [Fact]
    public void Only_relationships_among_two_or_more_copied_parts_are_among_them()
    {
        Assert.Equal(
            new[] { Gap.Id, SameHeight.Id }.Order(),
            GroupCopy.Among(Scene, [A.Id, B.Id]).Select(relationship => relationship.Id).Order());

        // A pin names one part: it is never "among" a group, even a group of that one part.
        Assert.Empty(GroupCopy.Among(Scene, [A.Id]));
    }

    [Fact]
    public void A_duplicate_of_all_three_re_points_every_kind_at_the_copies_under_new_ids()
    {
        (ImmutableList<Request> requests, ImmutableDictionary<EntityId, EntityId> copies) =
            GroupCopy.Duplicate(Scene, [A, B, C], Vector3.Along(Axis.Y, Length.Inches(10)));
        EntityId a = copies[A.Id], b = copies[B.Id], c = copies[C.Id];

        ImmutableArray<Relationship> added = Added(requests);

        Assert.Equal(6, added.Length);
        Assert.DoesNotContain(added, relationship => Scene.Relationships.ContainsKey(relationship.Id));
        Assert.Contains(added, relationship => relationship is AxisDistance
        {
            From: FeatureRef { Box: var from, Feature: var fromFace },
            To: FeatureRef { Box: var to, Feature: var toFace },
            Axis: Axis.X,
        } distance && from == a && to == b && fromFace == BoxFeature.Face(BoxFace.East) && toFace == BoxFeature.Face(BoxFace.West) && distance.Distance == Length.Inches(3));
        Assert.Contains(added, relationship => relationship is Centered { Middle: CenterRef { Box: var m }, Axis: Axis.X } centred
            && m == b && centred.A == new FeatureRef(a, BoxFeature.Face(BoxFace.West)) && centred.B == new FeatureRef(c, BoxFeature.Face(BoxFace.East)));
        Assert.Contains(added, relationship => relationship is Coincident coincident
            && coincident.A == new FeatureRef(a, BottomSouth) && coincident.B == new FeatureRef(c, BottomSouth));
        Assert.Contains(added, relationship => relationship is EqualParam equal
            && equal.A == new BoxDepthRef(a) && equal.B == new BoxDepthRef(c));
        Assert.Contains(added, relationship => relationship is EqualParam equal
            && equal.A == new BoxHeightRef(a) && equal.B == new BoxHeightRef(b));
        Assert.Contains(added, relationship => relationship is EqualParam equal
            && equal.A == new BoxWidthRef(a) && equal.B == new BoxWidthRef(c));
        Assert.DoesNotContain(added, relationship => relationship is Anchored);
    }

    [Fact]
    public void The_duplicated_group_and_its_relationships_are_accepted_by_the_updater()
    {
        (ImmutableList<Request> requests, _) = GroupCopy.Duplicate(Scene, [A, B, C], Vector3.Along(Axis.Y, Length.Inches(10)));

        Assert.IsAssignableFrom<Succeeded>(DirectUpdater.Instance.Apply(Scene, Batch.Of([.. requests])));
    }

    [Fact]
    public void A_mirror_across_x_turns_east_faces_west_and_a_distance_east_into_one_west()
    {
        // The plane through the middle of the row: A spans 0..4, C spans 14..18, so X = 9".
        (ImmutableList<Request> requests, ImmutableDictionary<EntityId, EntityId> copies) =
            GroupCopy.Mirror(Scene, [A, B, C], Axis.X, Length.Inches(9), out Box? refused)!.Value;
        EntityId a = copies[A.Id], b = copies[B.Id];

        Assert.Null(refused);
        AxisDistance gap = Assert.Single(Added(requests).OfType<AxisDistance>());
        Assert.Equal(new FeatureRef(a, BoxFeature.Face(BoxFace.West)), gap.From);
        Assert.Equal(new FeatureRef(b, BoxFeature.Face(BoxFace.East)), gap.To);
        Assert.Equal(Length.Inches(-3), gap.Distance);

        // Faces that do not face along X are not turned; nor is a size.
        Coincident level = Assert.Single(Added(requests).OfType<Coincident>());
        Assert.Equal(BottomSouth, ((FeatureRef)level.A).Feature);
        Assert.Equal(3, Added(requests).OfType<EqualParam>().Count());
    }

    [Fact]
    public void A_mirror_across_y_leaves_a_distance_along_x_as_it_was()
    {
        (ImmutableList<Request> requests, _) = GroupCopy.Mirror(Scene, [A, B], Axis.Y, Length.Inches(-5), out _)!.Value;

        AxisDistance gap = Assert.Single(Added(requests).OfType<AxisDistance>());
        Assert.Equal(Length.Inches(3), gap.Distance);
        Assert.Equal(BoxFeature.Face(BoxFace.East), ((FeatureRef)gap.From).Feature);
    }

    [Fact]
    public void A_mirrored_edge_and_vertex_turn_only_their_faces_along_the_mirror_axis()
    {
        BoxFeature edge = BoxFeature.Edge(BoxFace.East, BoxFace.Top);
        BoxFeature vertex = BoxFeature.Vertex(BoxFace.East, BoxFace.North, BoxFace.Top);

        Assert.Equal(BoxFeature.Edge(BoxFace.West, BoxFace.Top), GroupCopy.Mirrored(A, edge, Axis.X));
        Assert.Equal(BoxFeature.Vertex(BoxFace.East, BoxFace.South, BoxFace.Top), GroupCopy.Mirrored(A, vertex, Axis.Y));
        Assert.Throws<ArgumentNullException>(() => GroupCopy.Mirrored(null!, edge, Axis.X));
    }
}
