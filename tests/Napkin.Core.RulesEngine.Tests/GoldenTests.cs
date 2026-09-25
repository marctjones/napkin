namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// The golden harness (design §8.2): one theory row per case in every golden file, named
/// pack/table/row/index so a failure names the row. The files here are SYNTHETIC TEST DATA - NOT
/// CODE VALUES. Set NAPKIN_PACKS_ROOT to a packs root of your own (with a golden/ folder beside
/// layers/ and packs/) to run your own transcription's golden files through the same harness.
/// </summary>
public class GoldenTests
{
    private static readonly Dictionary<string, GoldenFileResult> Cache = new(StringComparer.Ordinal);

    public static TheoryData<string, string, int> Cases()
    {
        TheoryData<string, string, int> data = [];
        foreach ((string root, string file) in Files())
        {
            GoldenFileResult result = Run(root, file);
            for (int i = 0; i < result.Cases.Count; i++)
            {
                data.Add(root, file, i);
            }
        }

        return data;
    }

    public static TheoryData<string, string> FileData()
    {
        TheoryData<string, string> data = [];
        foreach ((string root, string file) in Files())
        {
            data.Add(root, file);
        }

        return data;
    }

    private static IEnumerable<(string Root, string File)> Files()
    {
        foreach (string file in Directory.GetFiles(Fx.GoldenRoot, "*.golden.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            yield return (Fx.Root, file);
        }

        foreach (string file in Directory.GetFiles(Path.Combine(Fx.BraceRoot, "golden"), "*.golden.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            yield return (Fx.BraceRoot, file);
        }

        string? own = Environment.GetEnvironmentVariable("NAPKIN_PACKS_ROOT");
        string? ownGolden = own is null ? null : Path.Combine(own, "golden");
        if (ownGolden is not null && Directory.Exists(ownGolden))
        {
            foreach (string file in Directory.GetFiles(ownGolden, "*.golden.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                yield return (own!, file);
            }
        }
    }

    private static GoldenFileResult Run(string root, string file)
    {
        lock (Cache)
        {
            if (!Cache.TryGetValue(file, out GoldenFileResult? result))
            {
                result = GoldenRunner.Run(root, file);
                Cache[file] = result;
            }

            return result;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Feature", "RUL-004")]
    public void Golden_case_passes(string root, string file, int index)
    {
        GoldenCaseResult result = Run(root, file).Cases[index];
        Assert.True(result.Passed, result.ToString());
    }

    [Theory]
    [MemberData(nameof(FileData))]
    [Trait("Feature", "RUL-004")]
    public void Golden_file_is_well_formed_and_covers_every_row(string root, string file)
    {
        GoldenFileResult result = Run(root, file);
        Assert.True(result.Problems.Count == 0, result.ToString());
    }

    [Fact]
    public void Synthetic_golden_files_cover_every_case_kind()
    {
        GoldenFileResult result = Run(Fx.Root, Path.Combine(Fx.GoldenRoot, "us-zz-state", "test-header-table.golden.json"));
        Assert.True(result.Passed, result.ToString());
        Assert.Equal(35, result.Cases.Count);
    }

    [Fact]
    public void A_wrong_expectation_fails_and_says_why()
    {
        string json = File.ReadAllText(Path.Combine(Fx.GoldenRoot, "us-zz-state", "test-header-table.golden.json"))
            .Replace("\"nominal\": \"2x13\"", "\"nominal\": \"2x14\"", StringComparison.Ordinal);
        GoldenFileResult result = GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), json, "edited.golden.json");
        Assert.False(result.Passed);
        GoldenCaseResult failed = Assert.Single(result.Cases, c => !c.Passed);
        Assert.Contains("us-zz-state/TEST-HEADER-TABLE/roof.s99.w44.m4/", failed.Name, StringComparison.Ordinal);
        Assert.Contains("expected (4) 2x14", failed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_right_value_from_the_wrong_row_fails()
    {
        string json = File.ReadAllText(Path.Combine(Fx.GoldenRoot, "us-zz-state", "test-header-table.golden.json"))
            .Replace(
                "\"groundSnowLoad\": 20, \"buildingWidth\": \"30ft 0in\", \"ultimateWindSpeed\": 150, \"headerSpan\": \"7ft 1in\"",
                "\"groundSnowLoad\": 50, \"buildingWidth\": \"30ft 0in\", \"ultimateWindSpeed\": 150, \"headerSpan\": \"6ft 1in\"",
                StringComparison.Ordinal);
        GoldenFileResult result = GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), json, "edited.golden.json");
        GoldenCaseResult failed = Assert.Single(result.Cases, c => !c.Passed);
        Assert.Contains("right values but cites row 'roof.s66.w44.m1', expected 'roof.s33.w44.m1'", failed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void An_uncovered_row_and_an_orphan_case_fail_the_file()
    {
        string json = File.ReadAllText(Path.Combine(Fx.GoldenRoot, "us-zz-state", "test-header-table.golden.json"))
            .Replace("\"row\": \"roof.s66.w22.m3\"", "\"row\": \"roof.renamed\"", StringComparison.Ordinal);
        GoldenFileResult result = GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), json, "edited.golden.json");
        Assert.Contains(result.Problems, p => p.Contains("row 'roof.s66.w22.m3' of table TEST-HEADER-TABLE has no hand-authored golden case", StringComparison.Ordinal));
        Assert.Contains(result.Problems, p => p.Contains("row 'roof.renamed' is not in the composed table", StringComparison.Ordinal));
    }

    [Fact]
    public void A_malformed_golden_file_or_an_invalid_pack_fails_the_file()
    {
        Assert.False(GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), "{ not json", "bad.golden.json").Passed);
        Assert.Null(GoldenRunner.PeekPack("{ not json"));
        Assert.Null(GoldenRunner.PeekPack("[]"));

        string json = File.ReadAllText(Path.Combine(Fx.GoldenRoot, "us-zz-state", "test-header-table.golden.json"));
        GoldenFileResult invalid = GoldenRunner.Run(PackLoader.Load(Fx.Source().Without(Fx.TablePath), "us-zz-state"), json, "x.golden.json");
        Assert.False(invalid.Passed);

        GoldenFileResult wrongPack = GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-base"), json, "x.golden.json");
        Assert.Contains(wrongPack.Problems, p => p.Contains("was run against 'us-zz-base'", StringComparison.Ordinal));

        string noExpect = json.Replace("\"expect\": { \"noData\"", "\"expectation\": { \"noData\"", StringComparison.Ordinal);
        Assert.Contains(GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), noExpect, "x.golden.json").Problems, p => p.Contains("expect", StringComparison.Ordinal));

        string twoExpect = json.Replace(
            "\"expect\": { \"noData\": { \"reason\": \"NoTableForWallKind\" } }",
            "\"expect\": { \"noData\": { \"reason\": \"NoTableForWallKind\" }, \"sized\": {} }",
            StringComparison.Ordinal);
        Assert.Contains(GoldenRunner.Run(PackLoader.Load(Fx.Root, "us-zz-state"), twoExpect, "x.golden.json").Problems, p => p.Contains("exactly one of", StringComparison.Ordinal));
    }
}
