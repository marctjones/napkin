using Napkin.App.Editing;
using Napkin.Core.Geometry;
using Xunit;
using Napkin.Modules.Editing;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The two sizes a selected part shows on the canvas (<see cref="SelectionDimensions"/>). Split from
/// the dimension-entry tests when the editing model moved to Napkin.Modules.Editing (#166):
/// <see cref="SelectionDimensions"/> measures through the viewer's dimension layout, so it stays in the app.
/// </summary>
public class SelectionDimensionsTests
{
    [Fact]
    [Trait("Feature", "CVS-007")]
    public void A_selected_part_shows_its_two_sizes_measured_from_the_geometry()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));
        EntityId box = EditingBuilder.Id(0);
        Length offset = Length.Inches(2);

        Assert.True(SelectionDimensions.TryFor(
            design.Sketch, box, SizeAxis.Width, offset, out var width, out bool annotatedWidth));
        Assert.True(SelectionDimensions.TryFor(
            design.Sketch, box, SizeAxis.Height, offset, out var height, out _));

        Assert.False(annotatedWidth);
        Assert.Equal(Length.Inches(24).Units, width.Value.Units);
        Assert.Equal(Length.Inches(12).Units, height.Value.Units);

        // Move the part and the same call reads the same sizes: the value is computed from the
        // geometry every time, never cached (docs/design/geometry-model.md §3.3).
        Sketch moved = design.Sketch.WithEntity(
            design.Box(0) with { Anchor = Point3.Inches(50, 50, 0), Width = Length.Inches(31) });

        Assert.True(SelectionDimensions.TryFor(
            moved, box, SizeAxis.Width, offset, out var after, out _));
        Assert.Equal(Length.Inches(31).Units, after.Value.Units);
        Assert.NotEqual(width.LabelAnchor, after.LabelAnchor);
    }

    [Fact]
    public void A_dimension_the_drawing_already_holds_is_the_one_shown()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));
        EntityId box = EditingBuilder.Id(0);
        EntityId annotation = EntityId.New();

        Sketch sketch = design.Sketch.WithEntity(new Dimension(
            annotation,
            LayerId.Default,
            new ParamMeasurand(new BoxWidthRef(box)),
            Drives: null,
            new DimensionPlacement(Length.Inches(6), DimensionSide.North)));

        Assert.True(SelectionDimensions.TryFor(
            sketch, box, SizeAxis.Width, Length.Inches(2), out var measurement, out bool annotated));

        Assert.True(annotated);
        Assert.Equal(annotation, measurement.Dimension.Id);
    }

    [Fact]
    public void Nothing_is_shown_for_something_that_is_not_a_part()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12));

        Assert.False(SelectionDimensions.TryFor(
            design.Sketch, EntityId.New(), SizeAxis.Width, Length.Inches(2), out _, out _));
    }
}
