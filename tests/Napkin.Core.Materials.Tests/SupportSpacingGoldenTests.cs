using System.Text;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The support-spacing table against PS 2-18, and the loader's handling of a spacings file.
/// </summary>
/// <remarks>
/// <para>
/// Source read in the task that wrote these rows: Voluntary Product Standard PS 2-18,
/// <em>Performance Standard for Wood Structural Panels</em> (effective 30 March 2019; published by
/// APA as Form No. S350H), retrieved 2026-09-21 from
/// <c>https://tolko.com/wp-content/uploads/2019/11/APA-PS-2-18-Performance-Standard-for-Wood-Structural-Panels.pdf</c>.
/// </para>
/// <para>
/// §4.1.3, "Span rating": "An index number, based on customary inch units, that identifies the
/// recommended maximum center-to-center support spacing for the specified end use under normal use
/// conditions. … spans are typically specified singly for wall (Wall 24) and single floor
/// (Floor 24 o.c.) … a span rating of 32/16 designates a roof span of 32 inches and a subfloor
/// span of 16 inches."
/// </para>
/// <para>
/// The span ratings the standard's own tables are written against: Table 2 (pp. 12-13) lists
/// Roof - 16, 20, 24, 32, 40, 48, 54, 60 and Subfloor - 16, 20, 24, 32, 48; Table 3 (p. 15) lists
/// Single Floor - 16, 20, 24, 32, 48; Table 5 (p. 20) lists Wall - 16 and Wall - 24.
/// </para>
/// <para>
/// <strong>Deliberately not the IRC.</strong> DESIGN.md §5.6 asks for "standard stud spacing
/// (16″/24″ OC)", and the obvious source is IRC Table R602.3(5). napkin does not transcribe
/// adopted code tables: the copyright stance on that is the project owner's and it blocks the code
/// packs (CLAUDE.md). PS 2-18 states the same two spacings as span ratings, so the library reads
/// them from there and says so.
/// </para>
/// </remarks>
public sealed class SupportSpacingGoldenTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>Every span rating carried, against the table it was read from.</summary>
    public static TheoryData<string, string, Length> Rows => new()
    {
        { "Wall - 16", "Wall", Length.Inches(16) },
        { "Wall - 24", "Wall", Length.Inches(24) },

        { "Roof - 16", "Roof", Length.Inches(16) },
        { "Roof - 20", "Roof", Length.Inches(20) },
        { "Roof - 24", "Roof", Length.Inches(24) },
        { "Roof - 32", "Roof", Length.Inches(32) },
        { "Roof - 40", "Roof", Length.Inches(40) },
        { "Roof - 48", "Roof", Length.Inches(48) },
        { "Roof - 54", "Roof", Length.Inches(54) },
        { "Roof - 60", "Roof", Length.Inches(60) },

        { "Subfloor - 16", "Subfloor", Length.Inches(16) },
        { "Subfloor - 20", "Subfloor", Length.Inches(20) },
        { "Subfloor - 24", "Subfloor", Length.Inches(24) },
        { "Subfloor - 32", "Subfloor", Length.Inches(32) },
        { "Subfloor - 48", "Subfloor", Length.Inches(48) },

        { "Single Floor - 16", "Single Floor", Length.Inches(16) },
        { "Single Floor - 20", "Single Floor", Length.Inches(20) },
        { "Single Floor - 24", "Single Floor", Length.Inches(24) },
        { "Single Floor - 32", "Single Floor", Length.Inches(32) },
        { "Single Floor - 48", "Single Floor", Length.Inches(48) },
    };

    [Theory]
    [MemberData(nameof(Rows))]
    [Trait("Feature", "MAT-001")]
    public void EachSpanRatingIsTheSpacingTheStandardNames(string name, string endUse, Length spacing)
    {
        SupportSpacing row = Assert.Single(Library.SupportSpacings, each => each.Name == name);

        Assert.Equal(endUse, row.EndUse);
        Assert.Equal(spacing, row.Spacing);
    }

    /// <summary>
    /// The two spacings DESIGN.md §5.6 names — 16″ and 24″ on centre — as studs, read from
    /// PS 2-18's Wall span ratings rather than from the IRC.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void TheStudSpacingsAreSixteenAndTwentyFourOnCentre()
    {
        Assert.Equal(
            [Length.Inches(16), Length.Inches(24)],
            Library.SpacingsFor("Wall").Select(spacing => spacing.Spacing));

        Assert.Equal(2, Library.SpacingsFor("wall").Length);
        Assert.Empty(Library.SpacingsFor("Ceiling"));
    }

    [Fact]
    [Trait("Feature", "MAT-001")]
    public void ASpacingSaysItselfInInchesOnCentre()
    {
        SupportSpacing studs = Assert.Single(Library.SupportSpacings, each => each.Name == "Wall - 24");

        Assert.Equal("24\" o.c.", studs.SpacingText);
        Assert.Equal("PS 2-18", studs.Source.Designation);
    }

    /// <summary>
    /// A spacing is not stock — it has no cross-section and nothing buys one — so it is not in the
    /// picker and not in <see cref="MaterialsLibrary.Items"/>.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-001")]
    public void ASpacingIsNotAStockItem()
    {
        Assert.DoesNotContain(Library.Items, item => item.Name.StartsWith("Wall", StringComparison.Ordinal));
        Assert.False(Library.TryFind("Wall - 24", out _));
        Assert.Single(Library.SpacingTables);
    }

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void EverySpacingCarriesItsCitationAndItsDerivation()
    {
        Assert.NotEmpty(Library.SupportSpacings);

        foreach (SupportSpacing spacing in Library.SupportSpacings)
        {
            Assert.False(string.IsNullOrWhiteSpace(spacing.Derivation), $"{spacing.Name} has no derivation.");
            Assert.False(string.IsNullOrWhiteSpace(spacing.EndUse), $"{spacing.Name} has no end use.");
            Assert.Contains("§4.1.3", spacing.Source.Where, StringComparison.Ordinal);
            Assert.Contains("NOT read from the IRC", spacing.Source.Where, StringComparison.Ordinal);
            Assert.True(spacing.Spacing.Format(StockItem.SizeFormat).IsExact);
        }
    }

    // ---------------------------------------------------------------------------------------
    // The loader, on a spacings file
    // ---------------------------------------------------------------------------------------

    private const string Citation = """
        {
          "designation": "PS 2-18",
          "standard": "Voluntary Product Standard PS 2-18, Performance Standard for Wood Structural Panels",
          "publisher": "U.S. Department of Commerce / NIST",
          "where": "§4.1.3, Span rating, page 10",
          "url": "https://example.invalid/ps2.pdf",
          "retrieved": "2026-09-21"
        }
        """;

    private static string Table(string spacings) => $$"""
        {
          "tableVersion": 1,
          "kind": "spacings",
          "id": "test-spacings",
          "title": "A spacings table for a test",
          "citation": {{Citation}},
          "spacings": [{{spacings}}]
        }
        """;

    private static MaterialsLoadResult Read(string json)
        => MaterialsReader.Read("test-spacings.json", new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static void Refuses(string json, MaterialsProblemKind kind, string mustMention)
    {
        MaterialsRefused refused = Assert.IsType<MaterialsRefused>(Read(json));
        MaterialsProblem problem = Assert.Single(refused.Problems, each => each.Kind == kind);

        Assert.Equal("test-spacings.json", problem.File);
        Assert.Contains(mustMention, problem.ToString(), StringComparison.Ordinal);
    }

    private const string ValidSpacing = """
        { "name": "Wall - 24", "endUse": "Wall", "spacing": "24", "derivation": "Table 5." }
        """;

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AWellFormedSpacingsTableLoads()
    {
        MaterialsLoaded loaded = Assert.IsType<MaterialsLoaded>(Read(Table(ValidSpacing)));

        Assert.Empty(loaded.Library.Tables);
        SpacingTable table = Assert.Single(loaded.Library.SpacingTables);
        Assert.Equal("test-spacings", table.Id);

        SupportSpacing spacing = Assert.Single(loaded.Library.SupportSpacings);
        Assert.Equal(Length.Inches(24), spacing.Spacing);
        Assert.Equal("wall-24", spacing.Key);
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableOfAnUnknownKindIsRefusedListingTheOnesThisBuildReads()
        => Refuses(
            Table(ValidSpacing).Replace("\"spacings\",", "\"spans\",", StringComparison.Ordinal),
            MaterialsProblemKind.UnknownValue,
            "spans");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableWithNoKindIsRefused()
        => Refuses(
            // No trailing newline in the text being removed: a raw string literal carries whatever
            // line ending the checkout has, and this file is CRLF on a Windows runner. Leaving the
            // blank line behind is fine — JSON does not care, and the field is what is gone.
            Table(ValidSpacing).Replace("\"kind\": \"spacings\",", string.Empty, StringComparison.Ordinal),
            MaterialsProblemKind.MissingField,
            "kind");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void SpacingsThatAreNotAnArrayAreRefused()
        => Refuses(
            Table(ValidSpacing).Replace($"\"spacings\": [{ValidSpacing}]", "\"spacings\": {}", StringComparison.Ordinal),
            MaterialsProblemKind.Malformed,
            "spacings");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ASpacingsTableWithNoSpacingsFieldIsRefused()
        => Refuses(
            $$"""
            { "tableVersion": 1, "kind": "spacings", "id": "t", "title": "t", "citation": {{Citation}} }
            """,
            MaterialsProblemKind.MissingField,
            "spacings");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnEmptySpacingsListIsRefused()
        => Refuses(Table(string.Empty), MaterialsProblemKind.Empty, "spacings");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ASpacingThatIsNotAnObjectIsRefused()
        => Refuses(Table("16"), MaterialsProblemKind.Malformed, "a spacing");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ASpacingWithAFieldTheFormatDoesNotDefineIsRefused()
        => Refuses(
            Table(ValidSpacing.Replace("\"endUse\"", "\"enduse\"", StringComparison.Ordinal)),
            MaterialsProblemKind.UnknownField,
            "enduse");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ASpacingOffTheGridIsRefused()
        => Refuses(
            Table(ValidSpacing.Replace("\"24\"", "\"24-1/3\"", StringComparison.Ordinal)),
            MaterialsProblemKind.NotALength,
            "grid");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void TwoSpacingsMeaningTheSameThingAreRefused()
        => Refuses(
            Table($"{ValidSpacing}, {ValidSpacing.Replace("\"Wall - 24\"", "\"wall-24\"", StringComparison.Ordinal)}"),
            MaterialsProblemKind.DuplicateEntry,
            "wall-24");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ASpacingsTableWithNoCitationIsRefused()
        => Refuses(
            Table(ValidSpacing).Replace("\"citation\":", "\"source\":", StringComparison.Ordinal),
            MaterialsProblemKind.MissingField,
            "citation");
}
