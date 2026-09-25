using Napkin.Core.Geometry;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// Every check runs on the building as it will be (docs/design/renovation-sketches.md §6.1): a
/// demolished opening is not in its wall's line and has no header check; a demolished wall has no
/// line; an existing wall is checked as any wall is.
/// </summary>
public class PhaseCheckTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();

    static (Sketch Sketch, Box Wall, Box Window) WallWithWindow(Phase wall, Phase window)
    {
        Box wallBox = new Box(EntityId.New(), WallLayer, Point3.Origin, Length.Inches(144), Length.Inches(3, 1, 2), Length.Inches(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            Phase = wall,
        };
        Box windowBox = new Box(EntityId.New(), OpeningLayer, new Point3(Length.Inches(54), Length.Zero, Length.Inches(36)), Length.Inches(36), Length.Inches(3, 1, 2), Length.Inches(48), BoxFace.Top, Angle.Zero)
        {
            Name = "Window 1",
            Phase = window,
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wallBox)
            .WithEntity(windowBox);
        return (sketch, wallBox, windowBox);
    }

    [Fact]
    public void A_new_window_in_an_existing_wall_is_checked_and_splits_the_line()
    {
        (Sketch sketch, Box wall, Box window) = WallWithWindow(Phase.Existing, Phase.New);

        Assert.Equal(window.Id, Assert.Single(CodeCheck.Of(sketch, CodePacks.None)).Opening.Id);
        Assert.Equal(2, WallLine.Of(sketch, new Wall(wall)).Segments.Length);
        Assert.Single(WallLine.All(sketch));
    }

    [Fact]
    public void A_demolished_window_is_in_no_check_and_not_in_its_walls_line()
    {
        (Sketch sketch, Box wall, _) = WallWithWindow(Phase.Existing, Phase.Demolish);

        Assert.Empty(CodeCheck.Of(sketch, CodePacks.None));

        // The whole 144 in wall is one solid segment again.
        WallSegment segment = Assert.Single(WallLine.Of(sketch, new Wall(wall)).Segments);
        Assert.Equal(Length.Inches(144), segment.Length);
    }

    [Fact]
    public void A_demolished_wall_has_no_line_and_its_openings_no_check()
    {
        (Sketch sketch, _, _) = WallWithWindow(Phase.Demolish, Phase.New);

        Assert.Empty(WallLine.All(sketch));
        Assert.Empty(CodeCheck.Of(sketch, CodePacks.None));
        Assert.Empty(BracingCheck.Of(sketch, CodePacks.None));
    }
}
