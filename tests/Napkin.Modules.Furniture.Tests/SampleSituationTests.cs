using System.Text;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The behaviour the #101 situations exist to pin down: what napkin does with an overlap, with a
/// chain, with two dimensions that cannot both hold, and with sizes off the 1/16" grid.
/// </summary>
public sealed class SampleSituationTests
{
    private static Sketch Read(string fixture)
        => Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture))).Sketch;

    private static EntityId Slat(Sketch sketch, int n)
        => sketch.Entities.Values.OfType<Box>().Single(b => b.Name == $"Slat {n}").Id;

    [Fact]
    public void Overlap_is_not_detected_the_file_is_valid_and_the_cut_list_counts_every_piece()
    {
        // assembly-model.md section 6: no interference detection, by design. Two boards occupy
        // the same space and napkin says nothing; a later "parts overlap" report is decision 15.
        Sketch sketch = Read("overlap");

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.True(RelationshipChecker.Check(sketch).AllHold);

        Box first = sketch.Entities.Values.OfType<Box>().Single(b => b.Name == "Board, first");
        Box second = sketch.Entities.Values.OfType<Box>().Single(b => b.Name == "Board, second");
        Assert.Equal(first.Anchor, second.Anchor);

        CutListRow boards = Assert.Single(CutList.Of(sketch, MaterialsLibrary.Shipped), row => row.Label == "Board");
        Assert.Equal(2, boards.Quantity);
    }

    [Fact]
    public void A_chain_of_five_holds_when_the_first_slat_is_widened()
    {
        Sketch sketch = Read("chain-of-five");
        Box first = (Box)sketch.Entities[Slat(sketch, 1)];
        RelationshipId driver = new(Guid.Parse("5f0000ff-0000-4000-8000-000000000001"));
        sketch = sketch.WithRelationship(new ParamValue(driver, new BoxWidthRef(first.Id), first.Width));

        UpdateResult result = DirectUpdater.Instance.Apply(sketch, new SetParameter(driver, Length.Inches(12)));

        Succeeded updated = Assert.IsAssignableFrom<Succeeded>(result);
        // Slat 1 grew by 2"; each later slat moved east by exactly that, staying flush.
        Assert.Equal(Length.Inches(12), ((Box)updated.Sketch.Entities[first.Id]).Width);
        for (int n = 2; n <= 5; n++)
        {
            Box before = (Box)sketch.Entities[Slat(sketch, n)];
            Box after = (Box)updated.Sketch.Entities[before.Id];
            Assert.Equal(before.Anchor.X.Units + 2048, after.Anchor.X.Units);
            Assert.Equal(before.Width, after.Width);
        }

        Assert.True(RelationshipChecker.Check(updated.Sketch).AllHold);
    }

    [Fact]
    public void A_conflict_cannot_be_a_file_because_a_file_must_satisfy_its_own_relationships()
    {
        // Two dimensions that cannot both hold: slat 1 is stored 10" wide but the file also says
        // its width is 8". A scene file that says so is refused, naming the violated relationship
        // (docs/file-format.md rule 4), so this situation is a test, not a sample.
        string text = File.ReadAllText(ExpectedFixture.ScenePath("chain-of-five"));
        Sketch sketch = Read("chain-of-five");
        string slat1 = Slat(sketch, 1).Value.ToString("D");
        string extra = "    {\n      \"id\": \"5f0000ff-0000-4000-8000-000000000002\",\n      \"kind\": \"paramValue\",\n"
            + $"      \"param\": {{ \"kind\": \"boxWidth\", \"box\": \"{slat1}\" }},\n      \"value\": 8192\n    }},\n";
        int at = text.IndexOf("\"relationships\": [\n", StringComparison.Ordinal) + "\"relationships\": [\n".Length;
        string bad = text.Insert(at, extra);

        Refused refused = Assert.IsType<Refused>(SceneReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(bad))));

        LoadProblem problem = Assert.Single(refused.Problems);
        Assert.Equal(LoadProblemKind.RelationshipViolated, problem.Kind);
        Assert.Contains("it is out by", problem.Message, StringComparison.Ordinal);
        Assert.Contains("does not satisfy its own", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Widening_a_slat_held_at_both_ends_is_refused_as_a_contradiction_that_says_so()
    {
        // The same conflict as a request instead of a file: the first slat is anchored, and so is
        // the last, so the row cannot grow.
        Sketch sketch = Read("chain-of-five");
        Box first = (Box)sketch.Entities[Slat(sketch, 1)];
        RelationshipId driver = new(Guid.Parse("5f0000ff-0000-4000-8000-000000000001"));
        sketch = sketch
            .WithRelationship(new ParamValue(driver, new BoxWidthRef(first.Id), first.Width))
            .WithRelationship(new Anchored(new RelationshipId(Guid.Parse("5f0000ff-0000-4000-8000-000000000003")), Slat(sketch, 5)));

        OverConstrained result = Assert.IsType<OverConstrained>(
            DirectUpdater.Instance.Apply(sketch, new SetParameter(driver, Length.Inches(12))));

        Assert.Equal(ConflictKind.Contradictory, result.Conflict.Kind);
        Assert.False(string.IsNullOrWhiteSpace(result.Conflict.Summary));
        Assert.Contains(driver, result.Conflict.Relationships);
    }

    [Fact]
    public void Sizes_off_the_sixteenth_grid_read_as_approximate_and_the_third_is_341_units()
    {
        Sketch sketch = Read("fraction-stress");
        IReadOnlyList<CutListRow> rows = CutList.Of(sketch, MaterialsLibrary.Shipped);
        CutListRow third = rows.Single(r => r.Label == "Third");

        Assert.Equal(341, third.Length.Units);
        Assert.Equal("≈5/16\"", CutListCsv.Text(third.Length));
        Assert.Equal("1/16\"", CutListCsv.Text(third.Width));
    }
}
