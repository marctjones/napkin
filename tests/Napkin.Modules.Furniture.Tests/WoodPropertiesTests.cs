namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The cited species table (docs/design/furniture-checks.md slice A): the Wood Handbook's Table 5–3b
/// 12 % rows as printed, found only by exact name, never by a guess at free text.
/// </summary>
public class WoodPropertiesTests
{
    static readonly WoodProperties Table = WoodProperties.Shipped;

    [Theory]
    [Trait("Feature", "MAT-006")]
    // Read from FPL-GTR-282 Table 5–3b on 2026-09-26: the 12% row's specific gravity and E (10^6 lbf/in²).
    [InlineData("Oak, northern red", "0.63", 1_820_000, "5–10")]
    [InlineData("Maple, sugar (hard maple)", "0.63", 1_830_000, "5–10")]
    [InlineData("Walnut, black", "0.55", 1_680_000, "5–11")]
    [InlineData("Douglas-fir, Coast", "0.48", 1_950_000, "5–12")]
    [InlineData("Pine, eastern white", "0.35", 1_240_000, "5–12")]
    [InlineData("Spruce, white", "0.36", 1_430_000, "5–13")]
    public void A_row_is_what_the_table_prints(string name, string specificGravity, long psi, string page)
    {
        WoodSpecies row = Table.Find(name)!;
        Assert.Equal((decimal.Parse(specificGravity, System.Globalization.CultureInfo.InvariantCulture), psi, page), (row.SpecificGravity, row.ModulusOfElasticityPsi, row.Page));
    }

    [Fact]
    [Trait("Feature", "MAT-006")]
    public void Every_row_is_cited_and_in_range()
    {
        Assert.Equal(26, Table.Species.Length);
        Assert.Equal(Table.Species.Length, Table.Species.Select(row => row.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(Table.Species, row =>
        {
            Assert.Contains(row.Group, new[] { "hardwood", "softwood" });
            Assert.InRange(row.SpecificGravity, 0.30m, 0.70m);
            Assert.InRange(row.ModulusOfElasticityPsi, 1_000_000, 2_100_000);
            Assert.Contains(row.Page, new[] { "5–9", "5–10", "5–11", "5–12", "5–13" });
            Assert.NotEmpty(row.TableRow);
        });
        Assert.Contains("FPL-GTR-282", Table.Source, StringComparison.Ordinal);
        Assert.Contains("retrieved 2026-09-26", Table.Source, StringComparison.Ordinal);
        Assert.Contains("To correct for shear deflection, the modulus can be increased by 10%.", Table.Where, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("red oak")]
    [InlineData("SPF")]
    [InlineData("Oak")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Free_text_is_never_matched_to_a_row(string? typed) => Assert.Null(Table.Find(typed));

    [Fact]
    public void A_name_is_found_whatever_its_case_and_surrounding_spaces()
        => Assert.Equal("Oak, northern red", Table.Find("  OAK, NORTHERN RED ")!.Name);
}
