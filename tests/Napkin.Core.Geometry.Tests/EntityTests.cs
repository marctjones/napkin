namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Corner derivation for all four rotations and centre rounding: docs/design/geometry-model.md
/// &#xA7;8 step 3.
/// </summary>
public class EntityTests
{
    private static Box At(int quarterTurns) => new(
        EntityId.New(),
        LayerId.Default,
        Point2.Inches(10, 20),
        Length.Inches(30),
        Length.Inches(8),
        Angle.Zero.Rotate90(quarterTurns));

    [Trait("Feature", "GEO-007")]
    [Theory]
    [InlineData(0, 10, 20, 40, 20, 40, 28, 10, 28)]
    [InlineData(1, 10, 20, 10, 50, 2, 50, 2, 20)]
    [InlineData(2, 10, 20, -20, 20, -20, 12, 10, 12)]
    [InlineData(3, 10, 20, 10, -10, 18, -10, 18, 20)]
    public void CornersOfARightAngleRotatedBoxAreExact(
        int quarterTurns,
        int swX, int swY, int seX, int seY, int neX, int neY, int nwX, int nwY)
    {
        Box box = At(quarterTurns);

        Assert.Equal(Point2.Inches(swX, swY), box.Corner(BoxCorner.SouthWest));
        Assert.Equal(Point2.Inches(seX, seY), box.Corner(BoxCorner.SouthEast));
        Assert.Equal(Point2.Inches(neX, neY), box.Corner(BoxCorner.NorthEast));
        Assert.Equal(Point2.Inches(nwX, nwY), box.Corner(BoxCorner.NorthWest));

        // The anchor is the south-west corner of the local frame, whatever the rotation.
        Assert.Equal(box.Anchor, box.Corner(BoxCorner.SouthWest));
    }

    [Trait("Feature", "GEO-007")]
    [Theory]
    [InlineData(0, 25, 24)]
    [InlineData(1, 6, 35)]
    [InlineData(2, -5, 16)]
    [InlineData(3, 14, 5)]
    public void CentreIsDerivedFromTheParameters(int quarterTurns, int centreX, int centreY)
    {
        Assert.Equal(Point2.Inches(centreX, centreY), At(quarterTurns).Center);
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void CentreRoundsByAtMostHalfAUnitOnAnOddSide()
    {
        Box odd = new(EntityId.New(), LayerId.Default, Point2.Origin, new Length(3), new Length(5), Angle.Zero);

        // 3/2 rounds half to even to 2; 5/2 rounds half to even to 2.
        Assert.Equal(new Point2(new Length(2), new Length(2)), odd.Center);
    }

    [Fact]
    public void SizeIsWhatTheUserTypedEvenWhenTheCornersRound()
    {
        // A 10" part is 10" even when it is rotated 37° and its rounded corners are not (§2.3).
        Box tilted = new(
            EntityId.New(),
            LayerId.Default,
            Point2.Origin,
            Length.Inches(10),
            Length.Inches(10),
            Angle.Degrees(37));

        Assert.Equal(Length.Inches(10), tilted.Width);
        Assert.Equal(Length.Inches(10), tilted.Height);

        // The derived corner is within a unit of the real position, but need not be exact.
        Vector2 alongTheBottom = tilted.Corner(BoxCorner.SouthEast) - tilted.Corner(BoxCorner.SouthWest);
        Assert.InRange(
            Length.Abs(alongTheBottom.Magnitude() - Length.Inches(10)).Units,
            0L,
            2L);
    }

    [Fact]
    public void EdgesRunBetweenTheCornersTheDesignNames()
    {
        Assert.Equal((BoxCorner.SouthWest, BoxCorner.SouthEast), Box.Ends(BoxEdge.South));
        Assert.Equal((BoxCorner.SouthEast, BoxCorner.NorthEast), Box.Ends(BoxEdge.East));
        Assert.Equal((BoxCorner.NorthWest, BoxCorner.NorthEast), Box.Ends(BoxEdge.North));
        Assert.Equal((BoxCorner.SouthWest, BoxCorner.NorthWest), Box.Ends(BoxEdge.West));
    }

    [Fact]
    public void RotatingAVectorByARightAngleMultipleIsExact()
    {
        Vector2 v = new(Length.Inches(3), Length.Inches(4));

        Assert.Equal(v, v.Rotate(Angle.Zero));
        Assert.Equal(new Vector2(Length.Inches(-4), Length.Inches(3)), v.Rotate(Angle.Right));
        Assert.Equal(new Vector2(Length.Inches(-3), Length.Inches(-4)), v.Rotate(Angle.Straight));
        Assert.Equal(new Vector2(Length.Inches(4), Length.Inches(-3)), v.Rotate(Angle.Right * 3));

        // Four quarter turns is the identity, with no drift.
        Assert.Equal(v, v.Rotate(Angle.Right).Rotate(Angle.Right).Rotate(Angle.Right).Rotate(Angle.Right));
        Assert.Equal(Length.Inches(5), v.Magnitude());
    }

    [Fact]
    public void EntitiesMoveBetweenLayersWithoutChangingAnythingElse()
    {
        LayerId other = LayerId.New();
        Box box = At(0);
        Node node = new(EntityId.New(), LayerId.Default, Point2.Origin);

        Assert.Equal(box with { Layer = other }, box.OnLayer(other));
        Assert.Equal(node with { Layer = other }, node.OnLayer(other));
    }

    [Fact]
    public void IdsAreDistinctAndOrderable()
    {
        EntityId a = EntityId.New();
        EntityId b = EntityId.New();

        Assert.NotEqual(a, b);
        Assert.Equal(a, new EntityId(a.Value));
        Assert.Equal(0, a.CompareTo(new EntityId(a.Value)));
        Assert.NotEqual(0, a.CompareTo(b));
    }
}
