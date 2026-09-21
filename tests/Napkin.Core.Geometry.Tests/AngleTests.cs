namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The golden cases for <see cref="Angle"/> from docs/design/geometry-model.md &#xA7;7.1.
/// </summary>
public class AngleTests
{
    [Fact]
    public void DegreesMinutesSecondsAddExactly()
    {
        Assert.Equal(Angle.Degrees(22, 30), Angle.Degrees(22) + Angle.Degrees(0, 30));
        Assert.Equal(Angle.Degrees(45, 30, 15), Angle.Degrees(45) + Angle.Degrees(0, 30) + Angle.Degrees(0, 0, 15));
        Assert.Equal(324000, Angle.Right.Arcseconds);
        Assert.Equal(163815, Angle.Degrees(45, 30, 15).Arcseconds);
    }

    [Theory]
    // Every common miter angle is an exact integer of arcseconds (design §1.6).
    [InlineData(45.0, 162000)]
    [InlineData(30.0, 108000)]
    [InlineData(22.5, 81000)]
    [InlineData(15.0, 54000)]
    [InlineData(7.5, 27000)]
    [InlineData(11.25, 40500)]
    public void CommonMiterAnglesAreExact(double degrees, long expectedArcseconds)
    {
        Angle angle = Angle.FromDegrees(degrees, Rounding.HalfToEven);

        Assert.Equal(expectedArcseconds, angle.Arcseconds);
        Assert.Equal(degrees, angle.ToDegrees());
    }

    [Fact]
    public void RightAngleMultiplesAreRecognisedExactly()
    {
        Assert.True(Angle.Zero.IsRightAngleMultiple);
        Assert.True(Angle.Right.IsRightAngleMultiple);
        Assert.True((Angle.Right * 2).IsRightAngleMultiple);
        Assert.True((Angle.Right * 3).IsRightAngleMultiple);
        Assert.Equal(3, (Angle.Right * 3).QuarterTurns);
        Assert.Equal(2, Angle.Straight.QuarterTurns);

        Assert.False(Angle.Degrees(45).IsRightAngleMultiple);
        Assert.False(Angle.Degrees(0, 0, 1).IsRightAngleMultiple);
    }

    [Fact]
    public void AnglesNormaliseIntoOneTurn()
    {
        Assert.Equal(Angle.Zero, Angle.Right * 4);
        Assert.Equal(Angle.Degrees(270), Angle.Degrees(-90));
        Assert.Equal(Angle.Degrees(10), Angle.Degrees(370));
        Assert.Equal(Angle.Degrees(350), -Angle.Degrees(10));
        Assert.Equal(Angle.Degrees(350), Angle.Degrees(10) - Angle.Degrees(20));
        Assert.Equal(Angle.Zero, new Angle(-Angle.FullTurn));
        Assert.InRange(new Angle(-1).Arcseconds, 0L, Angle.FullTurn - 1);
    }

    [Fact]
    public void Rotate90IsExact()
    {
        Assert.Equal(Angle.Right, Angle.Zero.Rotate90(1));
        Assert.Equal(Angle.Degrees(270), Angle.Zero.Rotate90(-1));
        Assert.Equal(Angle.Zero, Angle.Right.Rotate90(3));
        Assert.Equal(Angle.Degrees(135), Angle.Degrees(45).Rotate90(1));
    }

    [Fact]
    public void HalfAnArcsecondIsFarBelowThePositionTolerance()
    {
        // Half an arcsecond over a ten-foot part moves an endpoint by about 0.3 units (design §1.6).
        double tenFeetInUnits = 10 * Length.UnitsPerFoot;
        double halfArcsecondInRadians = new Angle(1).ToRadians() / 2;

        Assert.True(tenFeetInUnits * halfArcsecondInRadians < 0.5);
    }

    [Fact]
    public void CheckedOverflowThrows()
    {
        Assert.Throws<OverflowException>(() => Angle.Degrees(long.MaxValue));
        Assert.Throws<OverflowException>(() => new Angle(long.MaxValue) * long.MaxValue);
    }
}
