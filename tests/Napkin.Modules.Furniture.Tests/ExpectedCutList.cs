using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The cut-list part of a <c>samples/*.expected.json</c> file: rows worked out by hand from the
/// dimensions the design states, and committed before the code that produces them existed.
/// </summary>
/// <remarks>
/// Only the fields a test compares are bound. <c>derivation</c> and <c>cutListOrder</c> are for
/// the reviewer — they are the arithmetic, written out so that a person can re-check a row without
/// running anything (<c>samples/README.md</c>, "The rule"). Nothing here may be regenerated from
/// napkin's own output.
/// </remarks>
internal sealed record ExpectedFixture(
    string Fixture,
    int FormatVersion,
    IReadOnlyList<ExpectedCutRow> CutList,
    IReadOnlyList<string> CutListCsv)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>Where the fixtures sit beside the test binary.</summary>
    internal static string Directory => Path.Combine(AppContext.BaseDirectory, "samples");

    internal static string ScenePath(string fixture) => Path.Combine(Directory, $"{fixture}.scene.json");

    internal static ExpectedFixture Read(string fixture)
    {
        using FileStream stream = File.OpenRead(Path.Combine(Directory, $"{fixture}.expected.json"));
        ExpectedFixture expected = JsonSerializer.Deserialize<ExpectedFixture>(stream, Options)
                                   ?? throw new InvalidOperationException($"{fixture}.expected.json is empty.");

        Assert.Equal(fixture, expected.Fixture);
        return expected;
    }

    /// <summary>The CSV lines as one document, the way <c>ToCsv</c> writes it.</summary>
    internal string Csv => string.Concat(CutListCsv.Select(line => line + "\n"));
}

internal sealed record ExpectedCutRow(
    string Label,
    int Quantity,
    long LengthUnits,
    long WidthUnits,
    long ThicknessUnits,
    string LengthText,
    string WidthText,
    string ThicknessText,
    string Material,
    bool Unresolved,
    IReadOnlyList<string> Members,
    string Derivation);
