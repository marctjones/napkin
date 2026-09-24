using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The real cut list against <see cref="IndependentCutOracle"/>, which shares no code with it (#100).
/// </summary>
public sealed class IndependentOracleTests
{
    public static IEnumerable<object[]> Samples => SaveReopenTests.Samples;

    public static IEnumerable<object[]> Generated => SaveReopenTests.Generated;

    private static void AssertAgree(string sceneJson, Sketch sketch)
    {
        // Real rows without cuts, summed by size: the same shape the oracle produces.
        SortedDictionary<(long, long, long), long> real = [];
        foreach (CutListRow row in CutList.Of(sketch, MaterialsLibrary.Shipped).Where(r => r.CutText.IsDefaultOrEmpty))
        {
            (long, long, long) key = (row.Length.Units, row.Width.Units, row.Thickness.Units);
            real[key] = real.GetValueOrDefault(key) + row.Quantity;
        }

        Assert.Equal(IndependentCutOracle.Pieces(sceneJson), real);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    [Trait("Feature", "CUT-004")]
    public void A_samples_plain_parts_agree_with_the_oracle(string fixture)
    {
        string json = File.ReadAllText(ExpectedFixture.ScenePath(fixture));
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture))).Sketch;

        AssertAgree(json, sketch);
    }

    [Theory]
    [MemberData(nameof(Generated))]
    [Trait("Feature", "CUT-004")]
    public void A_generated_designs_plain_parts_agree_with_the_oracle(int seed)
    {
        Sketch sketch = SaveReopenTests.GenerateDesign(seed);

        AssertAgree(SceneWriter.WriteToText(sketch), sketch);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_oracle_sees_the_pieces_the_benchs_expectations_state()
    {
        // Guards the oracle itself: it must produce something, or every comparison is empty == empty.
        var pieces = IndependentCutOracle.Pieces(File.ReadAllText(ExpectedFixture.ScenePath("bench")));

        Assert.NotEmpty(pieces);
        Assert.True(pieces.Values.Sum() >= 5);
    }
}
