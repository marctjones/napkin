using System.Globalization;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Small designs built in code, for tests about the cut list rather than about a drawing.
/// </summary>
/// <remarks>
/// Ids are handed out in declaration order, because the cut list reads boxes in ascending id order
/// and several of its rules — which member a label falls back to, which order a row's members come
/// out in — are stated in terms of that order. A random id would make those tests flap.
/// </remarks>
internal static class Design
{
    /// <summary>
    /// A sketch of parts, each a box of the given plan size at the origin, unrotated.
    /// </summary>
    /// <param name="parts">The parts, in the order their ids are handed out.</param>
    internal static Sketch WithParts(params (string Name, long WidthUnits, long HeightUnits, Part Part)[] parts)
    {
        Sketch sketch = Sketch.Empty;
        for (int i = 0; i < parts.Length; i++)
        {
            (string name, long width, long height, Part part) = parts[i];
            sketch = sketch.WithEntity(BoxAt(i, name, width, height, part, quarterTurns: 0));
        }

        return sketch;
    }

    /// <summary>
    /// A sketch of parts, each with its own cuts, in the order their ids are handed out.
    /// </summary>
    /// <param name="parts">The parts and what has been cut off each blank.</param>
    internal static Sketch WithCutParts(
        params (string Name, long WidthUnits, long HeightUnits, Part Part, Cut[] Cuts)[] parts)
    {
        Sketch sketch = Sketch.Empty;
        for (int i = 0; i < parts.Length; i++)
        {
            (string name, long width, long height, Part part, Cut[] cuts) = parts[i];
            sketch = sketch.WithEntity(BoxAt(i, name, width, height, part, quarterTurns: 0) with
            {
                Cuts = [.. cuts],
            });
        }

        return sketch;
    }

    /// <summary>One part, turned by some number of right angles about its anchor.</summary>
    /// <param name="name">What the part is called.</param>
    /// <param name="widthUnits">The box's stored width.</param>
    /// <param name="heightUnits">The box's stored height.</param>
    /// <param name="part">The part on it.</param>
    /// <param name="quarterTurns">How many right angles to turn it by.</param>
    internal static Sketch WithRotatedPart(
        string name, long widthUnits, long heightUnits, Part part, int quarterTurns)
        => Sketch.Empty.WithEntity(BoxAt(0, name, widthUnits, heightUnits, part, quarterTurns));

    private static Box BoxAt(int index, string name, long width, long height, Part part, int quarterTurns)
        => new(
            new EntityId(SequentialId(index)),
            LayerId.Default,
            new Point2(Length.Zero, Length.Zero),
            new Length(width),
            new Length(height),
            Angle.Zero.Rotate90(quarterTurns))
        {
            Name = name,
            Part = part,
        };

    /// <summary>Ids that sort in the order they were asked for.</summary>
    private static Guid SequentialId(int index)
        => Guid.ParseExact(
            $"10000000-0000-4000-8000-{index.ToString("d12", CultureInfo.InvariantCulture)}",
            "D");
}
