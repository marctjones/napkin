using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The plan's dimension layout for a box on its side (docs/design/assembly-model.md &#xA7;7.2). Split
/// from PlanShapeTests when PlanShape moved to Napkin.Modules.Editing (#166): the layout is the
/// viewer's.
/// </summary>
public class PlanDimensionLayoutTests
{
    // 48" x 24" x 3/4", anchored at (10, 20) in the plan.
    static Box Blank(BoxFace faceUp, int quarterTurns = 0, params Cut[] cuts) => new(
        EntityId.New(),
        LayerId.Default,
        Point3.Inches(10, 20, 0),
        Length.Inches(48),
        Length.Inches(24),
        new Length(768),
        faceUp,
        Angle.Right * quarterTurns)
    {
        Cuts = [.. cuts],
    };

    [Fact]
    public void A_width_that_stands_vertical_is_not_drawn_as_a_plan_dimension()
    {
        Box standing = Blank(BoxFace.East) with { Id = EditingBuilder.Id(0) };
        Sketch sketch = Sketch.Empty.WithEntity(standing);

        Dimension width = new(EntityId.New(), LayerId.Default, new ParamMeasurand(new BoxWidthRef(standing.Id)), null, new DimensionPlacement(Length.Inches(1), DimensionSide.South));
        Dimension height = new(EntityId.New(), LayerId.Default, new ParamMeasurand(new BoxHeightRef(standing.Id)), null, new DimensionPlacement(Length.Inches(1), DimensionSide.West));

        Assert.False(DimensionLayout.TryMeasure(sketch, width, out _));
        Assert.True(DimensionLayout.TryMeasure(sketch, height, out DimensionMeasurement? measured));
        Assert.Equal(Length.Inches(24), measured.Value);
        Assert.Equal(standing.Footprint().Corner(BoxCorner.SouthWest), measured.From);
    }
}
