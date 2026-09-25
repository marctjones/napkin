using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Building.Tests;

/// <summary>An opening placed by <see cref="OpeningPlacement"/> is bound to its wall (BLD-002).</summary>
public class OpeningPlacementTests
{
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static (Sketch Sketch, Wall Wall, EntityId Opening) Placed(Angle rotation, Length offset)
    {
        Box box = new Box(EntityId.New(), WallLayer, Point3.Inches(10, 20, 0), In(144), In(3, 1, 2), In(96), BoxFace.Top, rotation) with { Name = "Wall 1" };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(box);
        Wall wall = new(box);
        EntityId id = EntityId.New();

        Request request = OpeningPlacement.Request(wall, OpeningLayer, id, "Window 1", offset, In(36), In(36), In(42));
        Solved solved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, request));
        return (solved.Sketch, wall, id);
    }

    [Fact]
    [Trait("Feature", "BLD-002")]
    public void APlacedOpeningIsHeldInItsWall()
    {
        (Sketch sketch, Wall wall, EntityId id) = Placed(Angle.Zero, In(54));

        Opening opening = Opening.Find(sketch, id)!;
        Assert.Equal(In(54), opening.Offset);
        Assert.Equal(In(36), opening.Sill);
        Assert.Equal(In(3, 1, 2), opening.Box.Height);
        Assert.Contains(sketch.Relationships.Values, relationship => relationship is Flush);
        AxisDistance distance = Assert.Single(sketch.Relationships.Values.OfType<AxisDistance>());
        Assert.Equal(Axis.X, distance.Axis);
        Assert.Equal(In(54), distance.Distance);

        // Moving the wall moves the opening with it.
        Solved moved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, new Drag(wall.Id, new Vector3(In(12), In(6), Length.Zero))));
        Opening after = Opening.Find(moved.Sketch, id)!;
        Assert.Equal(In(54), after.Offset);
        Assert.Equal(new Point3(In(10 + 12 + 54), In(26), In(36)), after.Box.Anchor);

        // Changing the distance slides it along the wall.
        Solved slid = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, new SetParameter(distance.Id, In(30))));
        Assert.Equal(In(30), Opening.Find(slid.Sketch, id)!.Offset);
    }

    [Fact]
    [Trait("Feature", "BLD-002")]
    public void AThickerWallTakesItsOpeningWithIt()
    {
        (Sketch sketch, Wall wall, EntityId id) = Placed(Angle.Zero, In(54));
        Solved thicker = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(sketch, new DragFace(wall.Id, BoxFace.North, In(2))));

        Assert.Equal(In(5, 1, 2), thicker.Sketch.Find<Box>(id)!.Height);
        Assert.NotNull(Opening.Find(thicker.Sketch, id));
    }

    [Fact]
    public void AnOpeningInATurnedWallIsMeasuredAlongIt()
    {
        (Sketch sketch, _, EntityId id) = Placed(Angle.Right, In(54));

        Assert.Equal(In(54), Opening.Find(sketch, id)!.Offset);
        Assert.Equal(Axis.Y, Assert.Single(sketch.Relationships.Values.OfType<AxisDistance>()).Axis);
    }

    [Fact]
    public void AnOpeningInAWallTurnedHalfWayIsMeasuredBackwards()
    {
        (Sketch sketch, _, EntityId id) = Placed(Angle.Straight, In(54));

        Assert.Equal(In(54), Opening.Find(sketch, id)!.Offset);
        Assert.Equal(-In(54), Assert.Single(sketch.Relationships.Values.OfType<AxisDistance>()).Distance);
    }

    [Fact]
    public void AClickOnTheWallCentresTheOpeningThereWithinTheWall()
    {
        Wall wall = new(new Box(EntityId.New(), WallLayer, Point3.Inches(10, 20, 0), In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero));

        // A click 72 in along centres a 36 in opening on it: its near side at 72 − 18 = 54.
        Assert.Equal(In(54), OpeningPlacement.OffsetAt(wall, new Point2(In(82), In(21)), In(36)));

        // Near either end it is kept inside the wall.
        Assert.Equal(Length.Zero, OpeningPlacement.OffsetAt(wall, new Point2(In(12), In(21)), In(36)));
        Assert.Equal(In(108), OpeningPlacement.OffsetAt(wall, new Point2(In(152), In(21)), In(36)));

        // Off the wall, nothing.
        Assert.Null(OpeningPlacement.OffsetAt(wall, new Point2(In(82), In(30)), In(36)));
        Assert.Null(OpeningPlacement.OffsetAt(wall, new Point2(In(5), In(21)), In(36)));
    }

    [Fact]
    public void AWallAtAnOddAngleTakesNoClick()
    {
        Wall wall = new(new Box(EntityId.New(), WallLayer, Point3.Origin, In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Degrees(30)));
        Assert.Null(OpeningPlacement.OffsetAt(wall, new Point2(In(10), In(1)), In(36)));
    }

    [Fact]
    [Trait("Feature", "BLD-002")]
    public void APlacedOpeningSurvivesASaveAndItsFrameWithIt()
    {
        (Sketch sketch, _, EntityId id) = Placed(Angle.Zero, In(54));
        using MemoryStream stream = new(SceneWriter.WriteToBytes(sketch));
        Sketch read = Assert.IsType<Loaded>(SceneReader.Read(stream)).Sketch;

        Assert.Single(read.Relationships.Values.OfType<AxisDistance>());
        Assert.Equal(In(54), Opening.Find(read, id)!.Offset);
        Assert.Equal(
            FramingList.Of(sketch, MaterialsLibrary.Shipped)[0].Summary,
            FramingList.Of(read, MaterialsLibrary.Shipped)[0].Summary);
    }
}
