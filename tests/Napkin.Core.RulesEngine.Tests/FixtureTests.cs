namespace Napkin.Core.RulesEngine.Tests;

/// <summary>Guards on the synthetic data itself, and on the catalog of packs under a root.</summary>
public class FixtureTests
{
    [Fact]
    public void Every_synthetic_file_carries_the_banner()
    {
        string[] files = [.. Directory.GetFiles(Fx.Root, "*.json", SearchOption.AllDirectories), .. Directory.GetFiles(Fx.GoldenRoot, "*.json", SearchOption.AllDirectories)];
        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.Contains(Fx.Banner, File.ReadAllText(f), StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Feature", "RUL-001")]
    public void The_catalog_lists_every_pack_under_the_root_loaded_or_invalid()
    {
        ValueList<PackLoadResult> all = PackCatalog.Discover(Fx.Root);
        Assert.Equal(
            ["us-zz-base", "us-zz-empty", "us-zz-state", "us-zz-town"],
            all.Select(r => r is PackLoadResult.Loaded l ? l.Pack.Manifest.Id : "invalid"));

        InMemoryPackSource broken = Fx.Source().With("packs/us-zz-broken/pack.json", "{}");
        ValueList<PackLoadResult> withBroken = PackCatalog.Discover(broken);
        PackLoadResult.Invalid invalid = Assert.Single(withBroken.OfType<PackLoadResult.Invalid>());
        Assert.Equal("us-zz-broken", invalid.PackId);
        Assert.Equal(4, withBroken.OfType<PackLoadResult.Loaded>().Count());
    }

    [Fact]
    public void Value_lists_compare_by_content()
    {
        Assert.Equal(ValueList.Of(1, 2), new[] { 1, 2 }.ToValueList());
        Assert.NotEqual(ValueList.Of(1, 2), ValueList.Of(2, 1));
        Assert.Equal(ValueList.Of("a").GetHashCode(), ValueList.Of("a").GetHashCode());
        Assert.False(ValueList.Of(1).Equals((object?)null));
        Assert.Equal("[1, 2]", ValueList.Of(1, 2).ToString());
        Assert.Empty(ValueList<int>.Empty);
    }

    [Fact]
    public void A_problem_prints_file_table_and_row()
    {
        Assert.Equal("f.json [T] (r): m", new PackProblem("f.json", "T", "r", "m").ToString());
        Assert.Equal("f.json: m", new PackProblem("f.json", null, null, "m").ToString());
    }
}
