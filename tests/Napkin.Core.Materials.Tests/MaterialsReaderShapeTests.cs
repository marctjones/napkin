using System.Text;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The rest of the loader's refusal surface: every wrong <em>shape</em> a data file can have, and
/// the category-specific checks for panels, hardwood and fasteners.
/// </summary>
/// <remarks>
/// Kept apart from <see cref="MaterialsReaderTests"/>, which covers the refusals a person editing
/// a shipped table is most likely to hit. These are the ones that say the reader never trusts the
/// document's shape: an array where an object belongs, a number where text belongs, a field of the
/// right name and the wrong kind.
/// </remarks>
public sealed class MaterialsReaderShapeTests
{
    private const string Citation = """
        {
          "designation": "PS 20-20",
          "standard": "Voluntary Product Standard PS 20-20, American Softwood Lumber Standard",
          "publisher": "U.S. Department of Commerce / NIST",
          "where": "Table 3, page 16, Dry columns",
          "url": "https://www.alsc.org/greenbook%20collection/ps20.pdf",
          "retrieved": "2026-09-21"
        }
        """;

    private static string Table(string category, string entries) => $$"""
        {
          "tableVersion": 1,
          "kind": "stock",
          "id": "test-table",
          "title": "A table for a test",
          "category": "{{category}}",
          "citation": {{Citation}},
          "entries": [{{entries}}]
        }
        """;

    private const string ValidEntry = """
        { "name": "2x4", "sizeClass": "Dimension", "nominalThickness": "2", "nominalWidth": "4",
          "thickness": "1-1/2", "width": "3-1/2", "derivation": "Table 3, Dimension." }
        """;

    private static MaterialsLoadResult Read(string json)
        => MaterialsReader.Read("test-table.json", new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static void Refuses(string json, MaterialsProblemKind kind, string mustMention)
    {
        MaterialsRefused refused = Assert.IsType<MaterialsRefused>(Read(json));
        MaterialsProblem problem = Assert.Single(refused.Problems, each => each.Kind == kind);

        Assert.Equal("test-table.json", problem.File);
        Assert.Contains(mustMention, problem.ToString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------
    // The document's shape
    // ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableThatIsNotAnObjectIsRefused()
        => Refuses("[]", MaterialsProblemKind.Malformed, "the table");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void EntriesThatAreNotAnArrayAreRefused()
        => Refuses(
            $$"""
            { "tableVersion": 1, "kind": "stock", "id": "t", "title": "t", "category": "DimensionalLumber",
              "citation": {{Citation}}, "entries": {} }
            """,
            MaterialsProblemKind.Malformed,
            "entries");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableWithNoEntriesFieldAtAllIsRefused()
        => Refuses(
            $$"""
            { "tableVersion": 1, "kind": "stock", "id": "t", "title": "t", "category": "DimensionalLumber", "citation": {{Citation}} }
            """,
            MaterialsProblemKind.MissingField,
            "entries");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnEntryThatIsNotAnObjectIsRefused()
        => Refuses(Table("DimensionalLumber", "3"), MaterialsProblemKind.Malformed, "an entry");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableVersionThatIsNotAWholeNumberIsRefused()
        => Refuses(
            Table("DimensionalLumber", ValidEntry).Replace("\"tableVersion\": 1", "\"tableVersion\": \"one\"", StringComparison.Ordinal),
            MaterialsProblemKind.Malformed,
            "tableVersion");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableWithNoVersionIsRefused()
        => Refuses(
            $$"""
            { "kind": "stock", "id": "t", "title": "t", "category": "DimensionalLumber", "citation": {{Citation}}, "entries": [] }
            """,
            MaterialsProblemKind.MissingField,
            "tableVersion");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ACitationThatIsNotAnObjectIsRefused()
        => Refuses(
            Table("DimensionalLumber", ValidEntry).Replace(Citation, "\"PS 20-20\"", StringComparison.Ordinal),
            MaterialsProblemKind.Malformed,
            "the citation");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ACitationWithAFieldTheFormatDoesNotDefineIsRefused()
        => Refuses(
            Table("DimensionalLumber", ValidEntry).Replace(
                "\"designation\": \"PS 20-20\",",
                "\"designation\": \"PS 20-20\", \"isbn\": \"x\",",
                StringComparison.Ordinal),
            MaterialsProblemKind.UnknownField,
            "isbn");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ACitationWithNoDesignationIsRefused()
        => Refuses(
            Table("DimensionalLumber", ValidEntry).Replace("\"designation\": \"PS 20-20\",", string.Empty, StringComparison.Ordinal),
            MaterialsProblemKind.MissingField,
            "designation");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATitleThatIsNotTextIsRefused()
        => Refuses(
            Table("DimensionalLumber", ValidEntry).Replace("\"title\": \"A table for a test\"", "\"title\": 7", StringComparison.Ordinal),
            MaterialsProblemKind.Malformed,
            "title");

    /// <summary>
    /// A name that survives <see cref="TakeText"/> — it is non-empty text — but that normalises
    /// away to nothing is refused: there would be no key to look it up by.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ANameThatNormalisesToNothingIsRefused()
        => Refuses(
            Table("DimensionalLumber", """
                { "name": "\"\"", "sizeClass": "Dimension", "nominalThickness": "2", "nominalWidth": "4",
                  "thickness": "1-1/2", "width": "3-1/2", "derivation": "x" }
                """),
            MaterialsProblemKind.EmptyValue,
            "not a name");

    // ---------------------------------------------------------------------------------------
    // Category-specific
    // ---------------------------------------------------------------------------------------

    [Fact]
    [Trait("Feature", "MAT-002")]
    public void APanelWithNoPerformanceCategoryIsRefused()
        => Refuses(
            Table("SheetGood", """
                { "name": "x plywood", "thickness": "3/4", "sheetWidth": "4'", "sheetLength": "8'", "derivation": "x" }
                """),
            MaterialsProblemKind.MissingField,
            "performanceCategory");

    /// <summary>Surfacing takes thickness off, so a surfaced thickness that does not is a mistake.</summary>
    [Fact]
    [Trait("Feature", "MAT-003")]
    public void HardwoodSurfacedThickerThanRoughIsRefused()
        => Refuses(
            Table("HardwoodBoard", """
                { "name": "9/4", "roughThickness": "1", "surfacedTwoSides": "1-1/4", "derivation": "x" }
                """),
            MaterialsProblemKind.InvalidValue,
            "less than the rough");

    [Fact]
    [Trait("Feature", "MAT-003")]
    public void AWireDiameterWrittenAsAJsonNumberIsRefused()
        => Refuses(
            Table("Fastener", """
                { "name": "1d", "pennySize": "1d", "length": "1", "shankDiameterInches": 0.072, "derivation": "x" }
                """),
            MaterialsProblemKind.Malformed,
            "shankDiameterInches");

    [Fact]
    [Trait("Feature", "MAT-003")]
    public void AWireDiameterThatIsNotANumberIsRefused()
        => Refuses(
            Table("Fastener", """
                { "name": "1d", "pennySize": "1d", "length": "1", "shankDiameterInches": "thin", "derivation": "x" }
                """),
            MaterialsProblemKind.Malformed,
            "thin");

    [Fact]
    [Trait("Feature", "MAT-003")]
    public void ANegativeWireDiameterIsRefused()
        => Refuses(
            Table("Fastener", """
                { "name": "1d", "pennySize": "1d", "length": "1", "shankDiameterInches": "-.072", "derivation": "x" }
                """),
            MaterialsProblemKind.InvalidValue,
            "shankDiameterInches");

    [Fact]
    [Trait("Feature", "MAT-003")]
    public void AFastenerWithNoDiameterAtAllIsRefused()
        => Refuses(
            Table("Fastener", """
                { "name": "1d", "family": "nail", "pennySize": "1d", "length": "1", "derivation": "x" }
                """),
            MaterialsProblemKind.MissingField,
            "shankDiameterInches");

    [Fact]
    [Trait("Feature", "MAT-003")]
    public void AFastenerFamilyThatIsNeitherNailNorBradIsRefused()
        => Refuses(
            Table("Fastener", """
                { "name": "1d", "family": "screw", "pennySize": "1d", "length": "1", "shankDiameterInches": ".05", "derivation": "x" }
                """),
            MaterialsProblemKind.UnknownValue,
            "family");

    [Fact]
    [Trait("Feature", "MAT-003")]
    public void AFastenerWithNoFamilyIsRefused()
        => Refuses(
            Table("Fastener", """
                { "name": "1d", "pennySize": "1d", "length": "1", "shankDiameterInches": ".05", "derivation": "x" }
                """),
            MaterialsProblemKind.MissingField,
            "family");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ASizeClassThatIsNotTextIsRefused()
        => Refuses(
            Table("DimensionalLumber", """
                { "name": "2x4", "sizeClass": 2, "nominalThickness": "2", "nominalWidth": "4",
                  "thickness": "1-1/2", "width": "3-1/2", "derivation": "x" }
                """),
            MaterialsProblemKind.MissingField,
            "sizeClass");

    // ---------------------------------------------------------------------------------------
    // Standard lengths
    // ---------------------------------------------------------------------------------------

    private static string WithLengths(string lengths) => $$"""
        {
          "tableVersion": 1, "kind": "stock", "id": "t", "title": "t", "category": "HardwoodBoard",
          "citation": {{Citation}},
          "standardLengthCitation": {{Citation}},
          "entries": [
            { "name": "4/4", "roughThickness": "1", "surfacedTwoSides": "13/16", "derivation": "x",
              "standardLengths": {{lengths}}, "standardLengthDerivation": "x" }
          ]
        }
        """;

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void StandardLengthsThatAreNotAnArrayAreRefused()
        => Refuses(WithLengths("\"8'\""), MaterialsProblemKind.Malformed, "standardLengths");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AStandardLengthThatIsNotTextIsRefused()
        => Refuses(WithLengths("[96]"), MaterialsProblemKind.NotALength, "standardLengths/0");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnEmptyStandardLengthListIsRefused()
        => Refuses(WithLengths("[]"), MaterialsProblemKind.Empty, "standardLengths");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ARepeatedStandardLengthIsRefused()
        => Refuses(WithLengths("[\"8'\", \"8'\"]"), MaterialsProblemKind.InvalidValue, "shortest first");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void AnEntryMayOverrideTheTablesCitationWithItsOwn()
    {
        string json = Table("DimensionalLumber", $$"""
            { "name": "2x4", "sizeClass": "Dimension", "nominalThickness": "2", "nominalWidth": "4",
              "thickness": "1-1/2", "width": "3-1/2", "derivation": "x",
              "citation": {{Citation.Replace("\"PS 20-20\"", "\"PS 20-15\"", StringComparison.Ordinal)}} }
            """);

        MaterialsLoaded loaded = Assert.IsType<MaterialsLoaded>(Read(json));
        StockItem item = Assert.Single(loaded.Library.Items);

        Assert.Equal("PS 20-15", item.Source.Designation);
        Assert.Equal("PS 20-20", Assert.Single(loaded.Library.Tables).Source.Designation);
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableOnDiskLoadsThroughReadFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"napkin-materials-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, Table("DimensionalLumber", """
            { "name": "2x4", "sizeClass": "Dimension", "nominalThickness": "2", "nominalWidth": "4",
              "thickness": "1-1/2", "width": "3-1/2", "derivation": "x" }
            """));
        try
        {
            MaterialsLoaded loaded = Assert.IsType<MaterialsLoaded>(MaterialsReader.ReadFile(path));
            LumberStock lumber = Assert.IsType<LumberStock>(Assert.Single(loaded.Library.Items));

            Assert.Equal(Length.Inches(3, 1, 2), lumber.Width);
            Assert.Equal(Path.GetFileName(path), Assert.Single(loaded.Library.Tables).File);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnEmptyDirectoryHoldsNoLibrary()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"napkin-materials-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            MaterialsRefused refused = Assert.IsType<MaterialsRefused>(MaterialsReader.ReadDirectory(directory));
            Assert.Equal(MaterialsProblemKind.Empty, Assert.Single(refused.Problems).Kind);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// The characters a word processor, a catalogue or a phone keyboard puts in a name that a
    /// person means as plain ASCII: an inch mark, a curly apostrophe, an en dash.
    /// </summary>
    [Theory]
    [InlineData("2x4\"", "2x4")]
    [InlineData("2x4″", "2x4")]
    [InlineData("2x4’", "2x4")]
    [InlineData("1–1/8 plywood", "1-1/8plywood")]
    [InlineData("1—1/8 plywood", "1-1/8plywood")]
    [InlineData("1‐1/8 plywood", "1-1/8plywood")]
    [InlineData("bypass", "bypass")]
    [InlineData("by", "by")]
    [InlineData("2by4by6", "2x4x6")]
    [Trait("Feature", "MAT-001")]
    public void NormalisationTidiesTypographyWithoutChangingMeaning(string typed, string expected)
        => Assert.Equal(expected, NominalName.Normalize(typed));
}
