using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// What Firm up proposes (<c>docs/design/sketch-mode.md</c> &#xA7;3.2, &#xA7;7.2). Every sketch is
/// written out here in inches and every expected proposal is worked out by hand in a comment from
/// the boxes' extents; napkin's output is never the source.
/// </summary>
public class FirmUpProposalsTests
{
    private static int next;

    private static Length In(double inches) => new((long)(inches * 1024));

    // A part lying as drawn (faceUp top), 3/4" deep on the datum, so its local axes are the world's.
    private static Box Plank(string name, double x, double y, double width, double height, bool part = true, double z = 0, double depth = 0.75)
    {
        EntityId id = new(new Guid(++next, 0, 0, new byte[8]));
        return new Box(id, LayerId.Default, new Point3(In(x), In(y), In(z)), In(width), In(height), In(depth), BoxFace.Top, Angle.Zero)
        {
            Name = name,
            Part = part ? new Part(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)) { Rough = true } : null,
        };
    }

    private static Sketch Sketched(params Box[] boxes)
        => boxes.Aggregate(Sketch.Empty, (sketch, box) => sketch.WithEntity(box));

    private static EntityId[] Ids(params Box[] boxes) => [.. boxes.Select(box => box.Id)];

    private static (EntityId A, BoxFace FaceA, EntityId B, BoxFace FaceB) Of(FirmUpProposal proposal)
    {
        Flush flush = Assert.IsType<Flush>(proposal.Relationship);
        FeatureRef a = Assert.IsType<FeatureRef>(flush.A);
        FeatureRef b = Assert.IsType<FeatureRef>(flush.B);
        return (a.Box, Assert.Single(a.Feature.Faces), b.Box, Assert.Single(b.Feature.Faces));
    }

    // ---- The quick bench, drawn in the plan as its side elevation (§7.2) ----
    // Top 48 x 2 at (0, 16): x 0..48, y 16..18. Leg 1 4 x 16 at (2, 0): x 2..6, y 0..16.
    // Leg 2 4 x 16 at (42, 0): x 42..46, y 0..16. Stretcher 36 x 3 at (6, 4): x 6..42, y 4..7. All z 0..3/4.
    private static (Sketch Sketch, Box Top, Box Leg1, Box Leg2, Box Stretcher) QuickBench()
    {
        Box top = Plank("Top", 0, 16, 48, 2);
        Box leg1 = Plank("Leg 1", 2, 0, 4, 16);
        Box leg2 = Plank("Leg 2", 42, 0, 4, 16);
        Box stretcher = Plank("Stretcher", 6, 4, 36, 3);
        return (Sketched(top, leg1, leg2, stretcher), top, leg1, leg2, stretcher);
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void The_quick_bench_gets_exactly_its_four_contacts_in_order()
    {
        (Sketch sketch, Box top, Box leg1, Box leg2, Box stretcher) = QuickBench();

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(sketch, Ids(top, leg1, leg2, stretcher));

        // 1. Top–Leg 1: Top's south face y = 16 meets Leg 1's north face y = 16 over x 2..6.
        // 2. Top–Leg 2: the same at y = 16 over x 42..46.
        // 3. Leg 1–Stretcher: Leg 1's east face x = 6 meets Stretcher's west face x = 6 over y 4..7.
        // 4. Leg 2–Stretcher: Leg 2's west face x = 42 meets Stretcher's east face x = 42.
        // Nothing alongside: no X or Y face of a touching pair shares a coordinate facing the same way
        // (Top x 0/48 vs legs 2/6, 42/46; legs vs stretcher y 0/16 vs 4/7). Leg 1 and Leg 2 share
        // y = 0 and y = 16 but do not touch. No Z proposal though all four share z = 0 and z = 3/4.
        Assert.Equal(
            [
                (top.Id, BoxFace.South, leg1.Id, BoxFace.North),
                (top.Id, BoxFace.South, leg2.Id, BoxFace.North),
                (leg1.Id, BoxFace.East, stretcher.Id, BoxFace.West),
                (leg2.Id, BoxFace.West, stretcher.Id, BoxFace.East),
            ],
            proposals.Select(Of).ToArray());
        Assert.All(proposals, proposal => Assert.Equal(FirmUpKind.Against, proposal.Kind));
        Assert.Equal(
            [
                "Top's south face against Leg 1's north face",
                "Top's south face against Leg 2's north face",
                "Leg 1's east face against Stretcher's west face",
                "Leg 2's west face against Stretcher's east face",
            ],
            proposals.Select(proposal => proposal.Sentence).ToArray());
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void The_order_is_by_id_whatever_order_the_parts_are_given_in()
    {
        (Sketch sketch, Box top, Box leg1, Box leg2, Box stretcher) = QuickBench();

        ImmutableArray<FirmUpProposal> shuffled = FirmUpProposals.For(sketch, Ids(stretcher, leg2, top, leg1, top));
        ImmutableArray<FirmUpProposal> sorted = FirmUpProposals.For(sketch, Ids(top, leg1, leg2, stretcher));

        Assert.Equal(sorted.Select(Of), shuffled.Select(Of));
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Two_planks_edge_to_edge_get_one_against_and_two_alongside()
    {
        // First 48 x 6 at (0, 0): x 0..48, y 0..6. Second at (0, 6): x 0..48, y 6..12.
        // Against: First's north y = 6 meets Second's south y = 6 over x 0..48.
        // Alongside: west faces both at x = 0 (y 0..6 and 6..12 abut; z overlaps); east faces both at x = 48.
        // Order: against first; then alongside by face of the first part, East before West.
        Box first = Plank("First", 0, 0, 48, 6);
        Box second = Plank("Second", 0, 6, 48, 6);

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(Sketched(first, second), Ids(first, second));

        Assert.Equal(
            [
                (first.Id, BoxFace.North, second.Id, BoxFace.South),
                (first.Id, BoxFace.East, second.Id, BoxFace.East),
                (first.Id, BoxFace.West, second.Id, BoxFace.West),
            ],
            proposals.Select(Of).ToArray());
        Assert.Equal([FirmUpKind.Against, FirmUpKind.Alongside, FirmUpKind.Alongside], proposals.Select(p => p.Kind).ToArray());
        Assert.Equal("First's east face flush with Second's east face", proposals[1].Sentence);
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void A_plank_shifted_along_gets_the_against_and_no_alongside()
    {
        // Second at (12, 6): x 12..60. Still touches First over x 12..48 at y = 6. West x 12 != 0, east x 60 != 48.
        Box first = Plank("First", 0, 0, 48, 6);
        Box second = Plank("Second", 12, 6, 48, 6);

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(Sketched(first, second), Ids(first, second));

        Assert.Equal([(first.Id, BoxFace.North, second.Id, BoxFace.South)], proposals.Select(Of).ToArray());
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Parts_sharing_a_line_but_not_touching_get_nothing()
    {
        // Both on y = 0..6, x 0..10 and 20..30: south faces share y = 0 but the parts are 10" apart.
        Box first = Plank("First", 0, 0, 10, 6);
        Box second = Plank("Second", 20, 0, 10, 6);

        Assert.Empty(FirmUpProposals.For(Sketched(first, second), Ids(first, second)));
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Parts_touching_only_along_an_edge_get_nothing()
    {
        // First x 0..10, y 0..6; Second x 10..20, y 6..12: they meet at the one line x = 10, y = 6 —
        // no area, so no contact, so nothing, not even the alongside faces that would otherwise qualify.
        Box first = Plank("First", 0, 0, 10, 6);
        Box second = Plank("Second", 10, 6, 10, 6);

        Assert.Empty(FirmUpProposals.For(Sketched(first, second), Ids(first, second)));
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Overlapping_parts_get_nothing()
    {
        // Interpenetrating: First x 0..10, Second x 5..15, both y 0..6. No face of one faces a face of the
        // other at one coordinate (First's east x = 10 is inside Second; Second's west x = 5 inside First).
        // Their south faces share y = 0 facing the same way, but there is no contact, so no alongside.
        Box first = Plank("First", 0, 0, 10, 6);
        Box second = Plank("Second", 5, 0, 10, 6);

        Assert.Empty(FirmUpProposals.For(Sketched(first, second), Ids(first, second)));
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void A_wall_is_never_firmed_and_a_part_is_never_paired_with_itself()
    {
        // The wall (no Part) touches First exactly as Second would; only nothing is proposed, since the
        // only part in the set is First, and First with itself is not a pair.
        Box first = Plank("First", 0, 0, 48, 6);
        Box wall = Plank("Wall", 0, 6, 48, 6, part: false);

        Assert.Empty(FirmUpProposals.For(Sketched(first, wall), Ids(first, wall, first)));
        Assert.Empty(FirmUpProposals.For(Sketched(first, wall), Ids(first)));
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Only_the_given_parts_are_considered()
    {
        (Sketch sketch, Box top, Box leg1, _, _) = QuickBench();

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(sketch, Ids(top, leg1));

        Assert.Equal([(top.Id, BoxFace.South, leg1.Id, BoxFace.North)], proposals.Select(Of).ToArray());
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void A_proposal_already_held_is_skipped_in_either_order()
    {
        (Sketch sketch, Box top, Box leg1, Box leg2, Box stretcher) = QuickBench();
        // Held the same way round as proposed (Top-Leg 1), and the other way round (Stretcher before Leg 2).
        sketch = sketch
            .WithRelationship(new Flush(RelationshipId.New(), new FeatureRef(top.Id, BoxFeature.Face(BoxFace.South)), new FeatureRef(leg1.Id, BoxFeature.Face(BoxFace.North))))
            .WithRelationship(new Flush(RelationshipId.New(), new FeatureRef(stretcher.Id, BoxFeature.Face(BoxFace.East)), new FeatureRef(leg2.Id, BoxFeature.Face(BoxFace.West))));

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(sketch, Ids(top, leg1, leg2, stretcher));

        Assert.Equal(
            [
                (top.Id, BoxFace.South, leg2.Id, BoxFace.North),
                (leg1.Id, BoxFace.East, stretcher.Id, BoxFace.West),
            ],
            proposals.Select(Of).ToArray());
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void A_part_set_on_another_gets_an_against_in_z_and_alongside_in_x_and_y_only()
    {
        // A box 10 x 6 x 1 on the floor (z 0..1) and a 10 x 6 x 1 block set on it (z 1..2), exactly over it.
        // Against: Base's top z = 1 meets Block's bottom z = 1 over x 0..10, y 0..6.
        // Alongside, X then Y, by the base's face South, East, North, West: south y = 0, east x = 10,
        // north y = 6, west x = 0 — the z extents 0..1 and 1..2 abut. Nothing in Z alongside.
        Box floor = Plank("Base", 0, 0, 10, 6, depth: 1);
        Box block = Plank("Block", 0, 0, 10, 6, z: 1, depth: 1);

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(Sketched(floor, block), Ids(floor, block));

        Assert.Equal(
            [
                (floor.Id, BoxFace.Top, block.Id, BoxFace.Bottom),
                (floor.Id, BoxFace.East, block.Id, BoxFace.East),
                (floor.Id, BoxFace.West, block.Id, BoxFace.West),
                (floor.Id, BoxFace.South, block.Id, BoxFace.South),
                (floor.Id, BoxFace.North, block.Id, BoxFace.North),
            ],
            proposals.Select(Of).ToArray());
        Assert.Equal("Base's top face against Block's bottom face", proposals[0].Sentence);
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Alongside_faces_are_ordered_by_the_first_parts_face_when_the_second_is_turned()
    {
        // Base 6 x 6 x 1 on the floor, unturned. Block 6 x 6 x 1 turned a quarter turn anticlockwise
        // about its anchor (6, 0, 1): local +x runs world +y, local +y runs world -x, so its extent is
        // x 0..6, y 0..6, z 1..2, exactly over the base. Its faces now face: South east, East north,
        // North west, West south.
        // Against: Base top against Block bottom. Alongside, X first: Base east with Block south, Base
        // west with Block north; then Y by the base's face South before North: Base south with Block
        // west, Base north with Block east — not Block's own face order, which would put east first.
        Box floor = Plank("Base", 0, 0, 6, 6, depth: 1);
        EntityId id = new(new Guid(++next, 0, 0, new byte[8]));
        Box block = new(id, LayerId.Default, new Point3(In(6), In(0), In(1)), In(6), In(6), In(1), BoxFace.Top, Angle.Zero.Rotate90(1))
        {
            Name = "Block",
            Part = new Part(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
        Assert.Equal((new Point3(In(0), In(0), In(1)), new Point3(In(6), In(6), In(2))), JointGeometry.Extent(block));

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(Sketched(floor, block), Ids(floor, block));

        Assert.Equal(
            [
                (floor.Id, BoxFace.Top, block.Id, BoxFace.Bottom),
                (floor.Id, BoxFace.East, block.Id, BoxFace.South),
                (floor.Id, BoxFace.West, block.Id, BoxFace.North),
                (floor.Id, BoxFace.South, block.Id, BoxFace.West),
                (floor.Id, BoxFace.North, block.Id, BoxFace.East),
            ],
            proposals.Select(Of).ToArray());
        Assert.Equal("Base's south face flush with Block's south face", proposals[3].Sentence);
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void A_part_with_no_name_is_called_a_part()
    {
        Box first = Plank("", 0, 0, 48, 6);
        Box second = Plank("Second", 12, 6, 48, 6);

        FirmUpProposal proposal = Assert.Single(FirmUpProposals.For(Sketched(first, second), Ids(first, second)));

        Assert.Equal("a part's north face against Second's south face", proposal.Sentence);
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void Every_proposal_is_accepted_by_the_updater_and_moves_nothing()
    {
        (Sketch sketch, Box top, Box leg1, Box leg2, Box stretcher) = QuickBench();
        Box first = Plank("First", 100, 0, 48, 6);
        Box second = Plank("Second", 100, 6, 48, 6);
        sketch = sketch.WithEntity(first).WithEntity(second);
        EntityId[] all = Ids(top, leg1, leg2, stretcher, first, second);

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(sketch, all);
        Assert.Equal(7, proposals.Length);

        Sketch firmed = sketch;
        foreach (FirmUpProposal proposal in proposals)
        {
            Succeeded result = Assert.IsAssignableFrom<Succeeded>(DirectUpdater.Instance.Apply(firmed, new AddRelationship(proposal.Relationship)));
            firmed = result.Sketch;
        }

        SketchAssert.IsConsistent(firmed);
        foreach (EntityId id in all)
        {
            Assert.Equal(sketch.Find<Box>(id), firmed.Find<Box>(id));
        }

        Assert.Empty(FirmUpProposals.For(firmed, all));
    }
}
