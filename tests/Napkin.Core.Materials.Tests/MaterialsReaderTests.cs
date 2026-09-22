using System.Text;

using Napkin.Core.Materials;

namespace Napkin.Core.Materials.Tests;

/// <summary>
/// The loader's strictness: what it refuses, and whether the refusal names the file and the entry
/// a person has to go and fix (<c>MAT-005</c>).
/// </summary>
public sealed class MaterialsReaderTests
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

    private static string Table(string entries, string? citation = null, string extra = "") => $$"""
        {
          "tableVersion": 1,
          "id": "test-table",
          "title": "A table for a test",
          "category": "DimensionalLumber",
          {{extra}}
          "citation": {{citation ?? Citation}},
          "entries": [{{entries}}]
        }
        """;

    private const string TwoByFour = """
        {
          "name": "2x4", "sizeClass": "Dimension",
          "nominalThickness": "2", "nominalWidth": "4",
          "thickness": "1-1/2", "width": "3-1/2",
          "derivation": "Table 3, Dimension."
        }
        """;

    private static MaterialsLoadResult Read(string json)
        => MaterialsReader.Read("test-table.json", new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static MaterialsRefused Refusal(string json)
    {
        MaterialsLoadResult result = Read(json);
        MaterialsRefused refused = Assert.IsType<MaterialsRefused>(result);
        Assert.NotEmpty(refused.Problems);
        Assert.False(result.IsLoaded);
        return refused;
    }

    private static void Refuses(string json, MaterialsProblemKind kind, params string[] mustMention)
    {
        MaterialsRefused refused = Refusal(json);
        MaterialsProblem problem = Assert.Single(refused.Problems, each => each.Kind == kind);

        Assert.Equal("test-table.json", problem.File);
        foreach (string text in mustMention)
        {
            Assert.Contains(text, problem.ToString(), StringComparison.Ordinal);
        }

        Assert.Contains("test-table.json", refused.Summary, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AWellFormedTableLoadsThroughTheSameDoorTheAppUses()
    {
        MaterialsLoaded loaded = Assert.IsType<MaterialsLoaded>(Read(Table(TwoByFour)));

        StockTable table = Assert.Single(loaded.Library.Tables);
        Assert.Equal("test-table", table.Id);
        Assert.Equal("test-table.json", table.File);

        StockItem item = Assert.Single(loaded.Library.Items);
        Assert.Equal("2x4", item.Name);
        Assert.Equal("2x4", item.Key);
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ADuplicateEntryIsRefusedNamingBothSpellings()
        => Refuses(
            Table($"{TwoByFour}, {TwoByFour.Replace("\"2x4\"", "\"2 X 4\"", StringComparison.Ordinal)}"),
            MaterialsProblemKind.DuplicateEntry,
            "2 X 4",
            "2x4");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnUnknownFieldIsRefusedNamingTheField()
        => Refuses(
            Table(TwoByFour.Replace("\"derivation\"", "\"derivaton\"", StringComparison.Ordinal)),
            MaterialsProblemKind.UnknownField,
            "derivaton");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ARepeatedFieldIsRefusedEvenThoughJsonAllowsIt()
        => Refuses(
            Table(TwoByFour.Replace("\"width\": \"3-1/2\"", "\"width\": \"3-1/2\", \"width\": \"5-1/2\"", StringComparison.Ordinal)),
            MaterialsProblemKind.DuplicateField,
            "width");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AMissingFieldIsRefusedNamingIt()
        => Refuses(
            Table("""{ "name": "2x4", "sizeClass": "Dimension", "nominalThickness": "2", "nominalWidth": "4", "thickness": "1-1/2", "derivation": "x" }"""),
            MaterialsProblemKind.MissingField,
            "width");

    /// <summary>
    /// A third of an inch cannot be stored on the 1/1024 inch grid. A printed standard never
    /// states one, so a value that has to be rounded is a transcription mistake and fails the
    /// load rather than being quietly approximated.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ADimensionThatDoesNotLandOnTheGridIsRefused()
        => Refuses(
            Table(TwoByFour.Replace("\"3-1/2\"", "\"3-1/3\"", StringComparison.Ordinal)),
            MaterialsProblemKind.NotALength,
            "3-1/3",
            "grid");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ADimensionWrittenAsANumberIsRefused()
        => Refuses(
            Table(TwoByFour.Replace("\"3-1/2\"", "3.5", StringComparison.Ordinal)),
            MaterialsProblemKind.Malformed,
            "width");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ANonPositiveDimensionIsRefused()
        => Refuses(
            Table(TwoByFour.Replace("\"3-1/2\"", "\"-3-1/2\"", StringComparison.Ordinal)),
            MaterialsProblemKind.InvalidValue,
            "width");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ATableWithNoCitationIsRefused()
        => Refuses(
            Table(TwoByFour).Replace("\"citation\":", "\"notacitation\":", StringComparison.Ordinal),
            MaterialsProblemKind.MissingField,
            "citation");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ACitationWithAnEmptyFieldIsRefused()
        => Refuses(
            Table(TwoByFour, Citation.Replace("\"Table 3, page 16, Dry columns\"", "\"   \"", StringComparison.Ordinal)),
            MaterialsProblemKind.EmptyValue,
            "where");

    [Fact]
    [Trait("Feature", "MAT-004")]
    public void ACitationWithoutARetrievalDateInTheRightShapeIsRefused()
        => Refuses(
            Table(TwoByFour, Citation.Replace("\"2026-09-21\"", "\"September 2026\"", StringComparison.Ordinal)),
            MaterialsProblemKind.NotADate,
            "September 2026");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnotherTableVersionIsRefusedRatherThanConverted()
        => Refuses(
            Table(TwoByFour).Replace("\"tableVersion\": 1", "\"tableVersion\": 2", StringComparison.Ordinal),
            MaterialsProblemKind.UnsupportedTableVersion,
            "tableVersion");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnUnknownCategoryIsRefusedListingTheOnesThisBuildKnows()
        => Refuses(
            Table(TwoByFour).Replace("\"DimensionalLumber\"", "\"Plasterboard\"", StringComparison.Ordinal),
            MaterialsProblemKind.UnknownValue,
            "Plasterboard",
            "SheetGood");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AnUnknownSizeClassIsRefused()
        => Refuses(
            Table(TwoByFour.Replace("\"Dimension\"", "\"Studwood\"", StringComparison.Ordinal)),
            MaterialsProblemKind.UnknownValue,
            "Studwood");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ATableWithNoEntriesIsRefused()
        => Refuses(Table(string.Empty), MaterialsProblemKind.Empty, "entries");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void BytesThatAreNotJsonAreRefused()
        => Refusal("this is not json").Problems.Single(problem => problem.Kind == MaterialsProblemKind.Malformed);

    /// <summary>
    /// A length list comes from a grading agency's rulebook, not from the size standard, so an
    /// entry that lists lengths without one is refused rather than inheriting the wrong citation.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-004")]
    public void StandardLengthsWithoutTheirOwnCitationAreRefused()
        => Refuses(
            Table(TwoByFour.Replace(
                "\"derivation\": \"Table 3, Dimension.\"",
                "\"derivation\": \"Table 3, Dimension.\", \"standardLengths\": [\"8'\"], \"standardLengthDerivation\": \"x\"",
                StringComparison.Ordinal)),
            MaterialsProblemKind.MissingField,
            "standardLengthCitation");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void StandardLengthsOutOfOrderAreRefused()
        => Refuses(
            Table(
                TwoByFour.Replace(
                    "\"derivation\": \"Table 3, Dimension.\"",
                    "\"derivation\": \"Table 3, Dimension.\", \"standardLengths\": [\"10'\", \"8'\"], \"standardLengthDerivation\": \"x\"",
                    StringComparison.Ordinal),
                extra: $"\"standardLengthCitation\": {Citation},"),
            MaterialsProblemKind.InvalidValue,
            "shortest first");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void StandardLengthsWithoutADerivationAreRefused()
        => Refuses(
            Table(
                TwoByFour.Replace(
                    "\"derivation\": \"Table 3, Dimension.\"",
                    "\"derivation\": \"Table 3, Dimension.\", \"standardLengths\": [\"8'\"]",
                    StringComparison.Ordinal),
                extra: $"\"standardLengthCitation\": {Citation},"),
            MaterialsProblemKind.MissingField,
            "standardLengthDerivation");

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void AFileThatIsNotThereIsRefusedRatherThanThrowing()
    {
        MaterialsLoadResult result = MaterialsReader.ReadFile(
            Path.Combine(Path.GetTempPath(), $"napkin-no-such-file-{Guid.NewGuid():N}.json"));

        MaterialsRefused refused = Assert.IsType<MaterialsRefused>(result);
        Assert.Equal(MaterialsProblemKind.Unreadable, refused.Problems[0].Kind);
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ADirectoryOfTablesLoadsAsOneLibraryAndCatchesDuplicatesAcrossFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"napkin-materials-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "a.json"), Table(TwoByFour));
            File.WriteAllText(
                Path.Combine(directory, "b.json"),
                Table(TwoByFour).Replace("\"test-table\"", "\"other-table\"", StringComparison.Ordinal));

            MaterialsRefused refused = Assert.IsType<MaterialsRefused>(MaterialsReader.ReadDirectory(directory));
            MaterialsProblem problem = Assert.Single(refused.Problems);

            Assert.Equal(MaterialsProblemKind.DuplicateEntry, problem.Kind);
            Assert.Equal("b.json", problem.File);
            Assert.Contains("a.json", problem.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void TwoFilesClaimingTheSameTableIdAreRefused()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"napkin-materials-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "a.json"), Table(TwoByFour));
            File.WriteAllText(
                Path.Combine(directory, "b.json"),
                Table(TwoByFour.Replace("\"2x4\"", "\"2x6\"", StringComparison.Ordinal)));

            MaterialsRefused refused = Assert.IsType<MaterialsRefused>(MaterialsReader.ReadDirectory(directory));
            MaterialsProblem problem = Assert.Single(refused.Problems);

            Assert.Equal(MaterialsProblemKind.DuplicateTable, problem.Kind);
            Assert.Contains("a.json", problem.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    [Trait("Feature", "MAT-005")]
    public void ADirectoryThatIsNotThereIsRefusedRatherThanThrowing()
    {
        MaterialsLoadResult result = MaterialsReader.ReadDirectory(
            Path.Combine(Path.GetTempPath(), $"napkin-no-such-directory-{Guid.NewGuid():N}"));

        MaterialsRefused refused = Assert.IsType<MaterialsRefused>(result);
        Assert.Equal(MaterialsProblemKind.Unreadable, refused.Problems[0].Kind);
    }

    /// <summary>
    /// The refusal a person reads: one line saying nothing loaded, then every problem, each
    /// naming its file and its place in it.
    /// </summary>
    [Fact]
    [Trait("Feature", "MAT-005")]
    public void TheRefusalReadsAsSomethingAPersonCanAct0n()
    {
        MaterialsRefused refused = Refusal(
            Table(TwoByFour.Replace("\"3-1/2\"", "\"3-1/3\"", StringComparison.Ordinal)));

        Assert.StartsWith("The materials library could not be loaded", refused.Summary, StringComparison.Ordinal);
        Assert.Equal(refused.Summary, refused.ToString());
        Assert.Contains("/entries/0/width", refused.Summary, StringComparison.Ordinal);
    }
}
