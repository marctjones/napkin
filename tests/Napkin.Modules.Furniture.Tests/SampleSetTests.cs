using System.Numerics;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The purpose-built samples (#101): where every box really is in the world, how big the whole
/// design is, and how much wood the cut list adds up to, each against numbers worked out by hand.
/// </summary>
/// <remarks>
/// The cut-list rows and CSV of these samples are held by <see cref="SampleCutListTests"/> with the
/// other fixtures. This adds what only these samples exist to prove: that a box turned, tipped or
/// spun occupies the space the design says (<c>lying-beam</c>), and that the rows' total volume is
/// the volume of the boxes (every sample). Expectations are hand-derived and never regenerated from
/// napkin's output (<c>samples/README.md</c>).
/// </remarks>
public sealed class SampleSetTests
{
    public static IEnumerable<object[]> Samples => [["bookcase"], ["bench"], ["lying-beam"], ["chain-of-five"], ["fraction-stress"], ["scale-extremes"], ["framing-16-oc"], ["l-bracket"], ["overlap"], ["picture-frame"], ["stocked-bench"]];

    private static Sketch Read(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture));
        if (result is Refused refused)
        {
            Assert.Fail($"{fixture}.scene.json was refused: {refused.Summary}");
        }

        return Assert.IsType<Loaded>(result).Sketch;
    }

    private static JsonElement Expectations(string fixture)
    {
        using FileStream stream = File.OpenRead(Path.Combine(ExpectedFixture.Directory, $"{fixture}.expected.json"));
        return JsonDocument.Parse(stream).RootElement.Clone();
    }

    private static (long X, long Y, long Z) Corner(JsonElement point)
        => (point.GetProperty("x").GetInt64(), point.GetProperty("y").GetInt64(), point.GetProperty("z").GetInt64());

    private static ((long X, long Y, long Z) Min, (long X, long Y, long Z) Max) WorldBounds(Box box)
    {
        List<Point3> corners =
        [
            .. Enum.GetValues<BoxCorner>().SelectMany(corner => new[]
            {
                box.Vertex(corner, BoxLevel.Bottom),
                box.Vertex(corner, BoxLevel.Top),
            }),
        ];

        return (
            (corners.Min(c => c.X.Units), corners.Min(c => c.Y.Units), corners.Min(c => c.Z.Units)),
            (corners.Max(c => c.X.Units), corners.Max(c => c.Y.Units), corners.Max(c => c.Z.Units)));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_box_occupies_the_space_the_expectations_work_out(string fixture)
    {
        Sketch sketch = Read(fixture);
        JsonElement expected = Expectations(fixture);
        JsonElement boxes = expected.GetProperty("boxes");

        Assert.Equal(expected.GetProperty("counts").GetProperty("boxes").GetInt32(), sketch.Entities.Values.OfType<Box>().Count());
        Assert.Equal(boxes.GetArrayLength(), sketch.Entities.Values.OfType<Box>().Count());

        foreach (JsonElement want in boxes.EnumerateArray())
        {
            string name = want.GetProperty("name").GetString()!;
            Box box = Assert.Single(sketch.Entities.Values.OfType<Box>(), b => b.Name == name);
            Assert.Equal(Guid.Parse(want.GetProperty("id").GetString()!), box.Id.Value);

            (long X, long Y, long Z) min = Corner(want.GetProperty("minUnits"));
            (long X, long Y, long Z) max = Corner(want.GetProperty("maxUnits"));
            ((long X, long Y, long Z) gotMin, (long X, long Y, long Z) gotMax) = WorldBounds(box);

            Assert.True(min == gotMin && max == gotMax, $"{fixture}: {name} occupies {gotMin}..{gotMax}, the design works out {min}..{max}.");
            Assert.False(string.IsNullOrWhiteSpace(want.GetProperty("derivation").GetString()), $"{name} has no derivation.");
        }
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_whole_design_is_the_size_the_expectations_work_out(string fixture)
    {
        Sketch sketch = Read(fixture);
        JsonElement overall = Expectations(fixture).GetProperty("overall");

        List<(long X, long Y, long Z)> all = [.. sketch.Entities.Values.OfType<Box>().SelectMany(b =>
        {
            ((long X, long Y, long Z) lo, (long X, long Y, long Z) hi) = WorldBounds(b);
            return new[] { lo, hi };
        })];

        Assert.Equal(overall.GetProperty("widthUnits").GetInt64(), all.Max(p => p.X) - all.Min(p => p.X));
        Assert.Equal(overall.GetProperty("depthUnits").GetInt64(), all.Max(p => p.Y) - all.Min(p => p.Y));
        Assert.Equal(overall.GetProperty("heightUnits").GetInt64(), all.Max(p => p.Z) - all.Min(p => p.Z));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void The_cut_list_adds_up_to_the_volume_the_expectations_work_out(string fixture)
    {
        Sketch sketch = Read(fixture);
        JsonElement expected = Expectations(fixture);
        BigInteger want = BigInteger.Parse(expected.GetProperty("totalVolumeCubicUnits").GetRawText());

        BigInteger fromRows = CutList.Of(sketch, MaterialsLibrary.Shipped).Aggregate(
            BigInteger.Zero,
            (sum, row) => sum + (row.Quantity * (BigInteger)row.Length.Units * row.Width.Units * row.Thickness.Units));

        Assert.Equal(want, fromRows);
        Assert.Equal(expected.GetProperty("counts").GetProperty("pieces").GetInt32(), CutList.Of(sketch, MaterialsLibrary.Shipped).Sum(row => row.Quantity));
    }

    [Fact]
    public void The_benchs_one_rail_box_is_two_pieces_on_the_bench_and_one_member_in_the_row()
    {
        CutListRow rail = Assert.Single(CutList.Of(Read("bench"), MaterialsLibrary.Shipped), row => row.Label == "Rail");

        Assert.Equal(2, rail.Quantity);
        Assert.Single(rail.Members);
    }

    [Fact]
    public void The_four_beams_are_one_row_however_they_are_turned()
    {
        CutListRow beam = Assert.Single(CutList.Of(Read("lying-beam"), MaterialsLibrary.Shipped));

        Assert.Equal("Beam", beam.Label);
        Assert.Equal(4, beam.Quantity);
        Assert.Equal(4, beam.Members.Length);
    }
}
