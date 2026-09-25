using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Firm up's proposals on three committed samples (<c>docs/design/sketch-mode.md</c> &#xA7;7.2): the
/// residue is worked out by hand from each scene's boxes, in the comments, before the assertion.
/// </summary>
public class FirmUpSampleTests
{
    private static Sketch Load(string fixture)
        => Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(SampleFixtureTests.SampleDirectory, $"{fixture}.scene.json"))).Sketch;

    private static EntityId[] PartsOf(Sketch sketch) => [.. sketch.Entities.Values.OfType<Box>().Select(box => box.Id)];

    private static string Named(Sketch sketch, FirmUpProposal proposal)
    {
        Flush flush = Assert.IsType<Flush>(proposal.Relationship);
        FeatureRef a = Assert.IsType<FeatureRef>(flush.A);
        FeatureRef b = Assert.IsType<FeatureRef>(flush.B);
        return $"{proposal.Kind} {sketch.Find<Box>(a.Box)!.Name}.{Assert.Single(a.Feature.Faces)} {sketch.Find<Box>(b.Box)!.Name}.{Assert.Single(b.Feature.Faces)}";
    }

    private static void AcceptedAndMovesNothing(Sketch sketch, ImmutableArray<FirmUpProposal> proposals)
    {
        Sketch firmed = sketch;
        foreach (FirmUpProposal proposal in proposals)
        {
            firmed = Assert.IsAssignableFrom<Succeeded>(DirectUpdater.Instance.Apply(firmed, new AddRelationship(proposal.Relationship))).Sketch;
        }

        Assert.True(RelationshipChecker.Check(firmed).AllHold);
        foreach (Box box in sketch.Entities.Values.OfType<Box>())
        {
            Assert.Equal(box, firmed.Find<Box>(box.Id));
        }
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void The_overlap_sample_gets_nothing()
    {
        // Two boards 24 x 4 x 1 at the origin, one inside the other, and a 1 x 1 x 3 peg standing through
        // them at (10, 1.5): every coplanar pair of faces faces the same way (the boards' faces coincide;
        // the peg's bottom shares z = 0 with theirs), so no two faces face each other: no contact, nothing.
        Sketch sketch = Load("overlap");

        Assert.Empty(FirmUpProposals.For(sketch, PartsOf(sketch)));
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void The_l_bracket_gets_its_seven_contacts_and_nine_alongsides()
    {
        // Extents (x, y, z), inches:
        // Foot .5..6.5, .5..4.5, .25..0.75   Upright .5..1, .5..4.5, .75..6     Boss 5.5..6.5, .5..1.5, .75..1.75
        // Skid 3..4, 2..3, 0..0.25           Tab 6.5..7.5, 2..3, .25..0.75     Nub 0..0.5, 2..3, 3..3.75
        // Rib .5..1, 4.5..5, 2..4            Lug .5..1, 0..0.5, 4..5
        // Contacts: Foot top z .75 on Upright's and Boss's bottoms; Foot bottom z .25 on Skid's top; Foot
        // east x 6.5 on Tab's west; Upright west x .5 on Nub's east; Upright north y 4.5 on Rib's south;
        // Upright south y .5 on Lug's north. Nub and Foot share x .5 but z 3..3.75 misses .25..0.75.
        // Alongside (X, Y faces at one coordinate, the other extents meeting):
        // Foot–Upright west x .5, south y .5, north y 4.5 (z .25..0.75 and .75..6 abut);
        // Foot–Boss east x 6.5, south y .5; Upright–Rib east x 1, west x .5 (y abuts at 4.5);
        // Upright–Lug east x 1, west x .5 (y abuts at .5). None for Skid, Tab or Nub.
        // No relationship is held in the file, so nothing is skipped.
        Sketch sketch = Load("l-bracket");

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(sketch, PartsOf(sketch));

        Assert.Equal(
            [
                "Against Foot.Top Upright.Bottom",
                "Alongside Foot.West Upright.West",
                "Alongside Foot.South Upright.South",
                "Alongside Foot.North Upright.North",
                "Against Foot.Top Boss, top.Bottom",
                "Alongside Foot.East Boss, top.East",
                "Alongside Foot.South Boss, top.South",
                "Against Foot.Bottom Skid, bottom.Top",
                "Against Foot.East Tab, east.West",
                "Against Upright.West Nub, west.East",
                "Against Upright.North Rib, north.South",
                "Alongside Upright.East Rib, north.East",
                "Alongside Upright.West Rib, north.West",
                "Against Upright.South Lug, south.North",
                "Alongside Upright.East Lug, south.East",
                "Alongside Upright.West Lug, south.West",
            ],
            proposals.Select(proposal => Named(sketch, proposal)).ToArray());
        AcceptedAndMovesNothing(sketch, proposals);
    }

    [Trait("Feature", "GEO-019")]
    [Fact]
    public void The_chain_of_five_gets_three_per_joint_and_holds_none_of_them_yet()
    {
        // Five slats end to end on y 0..3.5, z 0..3/4: x 0..10, 10..18.5, 18.5..30.5, 30.5..37.25, 37.25..46.5.
        // Each neighbouring pair: the east end against the next one's west end, and their south faces
        // (y 0) and north faces (y 3.5) flush — the x extents abut. The file holds corner Coincidents,
        // not Flushes, so none is structurally already held: twelve proposals, four joints of three.
        Sketch sketch = Load("chain-of-five");

        ImmutableArray<FirmUpProposal> proposals = FirmUpProposals.For(sketch, PartsOf(sketch));

        string[] expected = [.. Enumerable.Range(1, 4).SelectMany(n => new[]
        {
            $"Against Slat {n}.East Slat {n + 1}.West",
            $"Alongside Slat {n}.South Slat {n + 1}.South",
            $"Alongside Slat {n}.North Slat {n + 1}.North",
        })];
        Assert.Equal(expected, proposals.Select(proposal => Named(sketch, proposal)).ToArray());
        AcceptedAndMovesNothing(sketch, proposals);
    }
}
