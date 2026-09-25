using System.Text.Json;
using System.Text.Json.Serialization;
using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;

namespace Napkin.App.GuiTests;

/// <summary>
/// The hand-derived answers for one sample: <c>samples/&lt;fixture&gt;.expected.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>These are the viewer's expectations too, now.</strong> GUI-VIEW-04's acceptance sentence
/// says the labels read "the exact feet-inch-fraction strings the fixture's expectations file
/// gives", and until the sample files shipped with the app there was nowhere for it to read them
/// from, so the workflow stated them inline. It reads them from here instead: one set of numbers,
/// worked out by hand once, asserted by both the reader's tests and the viewer's.
/// </para>
/// <para>
/// Nothing in an expectations file may be regenerated from napkin's output (samples/README.md).
/// A failure here is a question about which side is wrong, not a prompt to re-record.
/// </para>
/// <para>
/// The <c>derivation</c>, <c>notes</c> and <c>statedNotInScene</c> fields are for a reviewer rather
/// than for a test, so they are not bound. This is a second, independent binding of the same files
/// that <c>tests/Napkin.Core.Project.Tests/Expectations.cs</c> binds; that one is internal to its
/// assembly, and a test project reaching into another test project would be worse than eighty lines
/// of records.
/// </para>
/// </remarks>
public sealed record SampleExpectations(
    string Fixture,
    int FormatVersion,
    long UnitsPerInch,
    ExpectedCounts Counts,
    ExpectedOverall Overall,
    IReadOnlyList<ExpectedBox> Boxes,
    IReadOnlyList<ExpectedLabel> DimensionLabels,
    IReadOnlyList<ExpectedCutRow> CutList)
{
    static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
    };

    /// <summary>The fixture names the app ships and this suite asserts against.</summary>
    public static IReadOnlyList<string> Fixtures { get; } = ["coffee-table", "wall-with-window"];

    /// <summary>Reads one fixture's expectations from the committed <c>samples/</c> directory.</summary>
    public static SampleExpectations For(string fixture)
    {
        string path = Path.Combine(RepositoryLayout.SamplesDirectory, $"{fixture}.expected.json");
        using FileStream stream = File.OpenRead(path);
        SampleExpectations expectations =
            JsonSerializer.Deserialize<SampleExpectations>(stream, Options)
            ?? throw new InvalidOperationException($"{path} is empty.");

        if (expectations.Fixture != fixture)
        {
            throw new InvalidOperationException(
                $"{path} says it is the expectations for \"{expectations.Fixture}\", not \"{fixture}\".");
        }

        return expectations;
    }

    /// <summary>The scene file the application ships for this fixture.</summary>
    public static string SceneFile(string fixture) =>
        Path.Combine(SampleFiles.SampleDirectory, $"{fixture}.scene.json");

    /// <summary>The sample source the Samples menu offers for this fixture.</summary>
    public static FileDesignSource Sample(string fixture) =>
        SampleFiles.All.SingleOrDefault(sample =>
            string.Equals(
                sample.FileName,
                $"{fixture}.scene.json",
                StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(
            $"The application did not ship {fixture}.scene.json. Looked in "
            + $"{SampleFiles.SampleDirectory}.");

    /// <summary>One expected box, by the name the expectations file gives it.</summary>
    public ExpectedBox Box(string name) =>
        Boxes.SingleOrDefault(box => box.Name == name)
        ?? throw new InvalidOperationException($"{Fixture} has no expected part called \"{name}\".");

    /// <summary>One expected dimension, by the name the expectations file gives it.</summary>
    public ExpectedLabel Label(string name) =>
        DimensionLabels.SingleOrDefault(label => label.Name == name)
        ?? throw new InvalidOperationException(
            $"{Fixture} has no expected dimension called \"{name}\".");
}

/// <summary>How many of each thing the fixture holds.</summary>
public sealed record ExpectedCounts(int Layers, int Entities, int Boxes, int Dimensions, int Relationships);

/// <summary>What the whole drawing measures.</summary>
public sealed record ExpectedOverall(long WidthUnits, long DepthUnits, string WidthText, string DepthText);

/// <summary>One part, where the arithmetic says it is, in integer units.</summary>
public sealed record ExpectedBox(
    string Id,
    string Name,
    long AnchorXUnits,
    long AnchorYUnits,
    long WidthUnits,
    long HeightUnits,
    long RotationArcseconds)
{
    /// <summary>The entity id the scene file gives this part.</summary>
    public EntityId EntityId => new(Guid.Parse(Id));
}

/// <summary>
/// One row of the hand-derived cut list: what the table must say, word for word and number for
/// number.
/// </summary>
public sealed record ExpectedCutRow(
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
    string Derivation)
{
    /// <summary>The line this row reads as on screen, tab separated, as the table renders it.</summary>
    public string OnScreen => string.Join(
        "\t",
        Label,
        Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        LengthText,
        WidthText,
        ThicknessText,
        Material);
}

/// <summary>One dimension: what it measures and what it must read.</summary>
public sealed record ExpectedLabel(
    string Id,
    string Name,
    bool Driving,
    long ValueUnits,
    string Text)
{
    /// <summary>The entity id the scene file gives this dimension.</summary>
    public EntityId EntityId => new(Guid.Parse(Id));
}
