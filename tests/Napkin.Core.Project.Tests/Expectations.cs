using System.Text.Json;
using System.Text.Json.Serialization;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// The shape of a <c>samples/*.expected.json</c> file: numbers worked out by hand from the
/// dimensions the design states, committed before the reader that produces them existed.
/// </summary>
/// <remarks>
/// The <c>derivation</c>, <c>notes</c> and <c>statedNotInScene</c> fields of those files are for
/// the reviewer, not for the test, so they are not bound here. Nothing in this file may be
/// regenerated from napkin's output; see <c>samples/README.md</c>.
/// </remarks>
internal sealed record Expectations(
    string Fixture,
    int FormatVersion,
    long UnitsPerInch,
    ExpectedCounts Counts,
    ExpectedOverall Overall,
    IReadOnlyList<ExpectedBox> Boxes,
    IReadOnlyList<ExpectedPart> PartsList,
    IReadOnlyDictionary<string, int> RelationshipKinds,
    IReadOnlyList<ExpectedLabel> DimensionLabels)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    internal static Expectations Read(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Expectations>(stream, Options)
               ?? throw new InvalidOperationException($"{path} is empty.");
    }
}

internal sealed record ExpectedCounts(int Layers, int Entities, int Boxes, int Dimensions, int Relationships);

internal sealed record ExpectedOverall(long WidthUnits, long DepthUnits, string WidthText, string DepthText);

internal sealed record ExpectedBox(
    string Id,
    string Name,
    long AnchorXUnits,
    long AnchorYUnits,
    long AnchorZUnits,
    long WidthUnits,
    long HeightUnits,
    long DepthUnits,
    string FaceUp,
    long RotationArcseconds);

internal sealed record ExpectedPart(
    string Name,
    int Quantity,
    long PlanWidthUnits,
    long PlanHeightUnits,
    string PlanWidthText,
    string PlanHeightText);

internal sealed record ExpectedLabel(
    string Id,
    string Name,
    bool Driving,
    long ValueUnits,
    string Text);
