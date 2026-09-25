using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Sketches for the editing tests, built here rather than drawn.
/// </summary>
/// <remarks>
/// The application is not allowed to build a sketch — that is CVS-005, and
/// <see cref="NoSketchConstructionTests"/> holds it to it. A <em>test</em> may, because a test
/// needs a starting position and driving the whole GUI to reach one is what the workflows are
/// for. Nothing here is reachable from the app.
/// </remarks>
static class EditingBuilder
{
    /// <summary>A design with one box per description, named "Part 1", "Part 2" and so on.</summary>
    public static Design Design(params (Point2 Anchor, Length Width, Length Height)[] boxes)
    {
        Sketch sketch = Sketch.Empty;
        ImmutableDictionary<EntityId, string>.Builder labels =
            ImmutableDictionary.CreateBuilder<EntityId, string>();

        for (int i = 0; i < boxes.Length; i++)
        {
            EntityId id = Id(i);
            (Point2 anchor, Length width, Length height) = boxes[i];
            sketch = sketch.WithEntity(Napkin.Core.Geometry.Box.AsDrawn(id, LayerId.Default, anchor, width, height, Napkin.Core.Geometry.Box.DefaultDepth, Angle.Zero));
            labels.Add(id, $"Part {i + 1}");
        }

        return new Design("Test", sketch, labels.ToImmutable());
    }

    /// <summary>A box at whole inches.</summary>
    public static (Point2 Anchor, Length Width, Length Height) At(long x, long y, long width, long height) =>
        (Point2.Inches(x, y), Length.Inches(width), Length.Inches(height));

    /// <summary>
    /// The id of the nth box, fixed so a failing assertion names the same part every run.
    /// </summary>
    public static EntityId Id(int index) =>
        new(new Guid($"00000000-0000-4000-8000-{index + 1:000000000000}"));

    /// <summary>The nth box of a design.</summary>
    public static Box Box(this Design design, int index) =>
        design.Sketch.Find<Box>(Id(index))
        ?? throw new InvalidOperationException($"No part {index} in this design.");
}
