namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// <see cref="Point3"/> and <see cref="Vector3"/>, the exact value types of
/// docs/design/assembly-model.md &#xA7;1.4.
/// </summary>
public class Point3Tests
{
    private static readonly Axis[] Axes = [Axis.X, Axis.Y, Axis.Z];

    [Fact]
    public void AlongPutsTheDistanceOnOneAxisOnly()
    {
        Length d = new(-7);

        Assert.Equal(new Vector3(d, Length.Zero, Length.Zero), Vector3.Along(Axis.X, d));
        Assert.Equal(new Vector3(Length.Zero, d, Length.Zero), Vector3.Along(Axis.Y, d));
        Assert.Equal(new Vector3(Length.Zero, Length.Zero, d), Vector3.Along(Axis.Z, d));

        foreach (Axis axis in Axes)
        {
            foreach (Axis other in Axes)
            {
                Assert.Equal(axis == other ? d : Length.Zero, Vector3.Along(axis, d).Component(other));
            }
        }
    }

    [Fact]
    public void ComponentAndWithComponentAddressEachAxis()
    {
        Vector3 v = new(new Length(1), new Length(2), new Length(3));

        Assert.Equal(new Length(1), v.Component(Axis.X));
        Assert.Equal(new Length(2), v.Component(Axis.Y));
        Assert.Equal(new Length(3), v.Component(Axis.Z));

        Assert.Equal(new Vector3(new Length(9), new Length(2), new Length(3)), v.WithComponent(Axis.X, new Length(9)));
        Assert.Equal(new Vector3(new Length(1), new Length(9), new Length(3)), v.WithComponent(Axis.Y, new Length(9)));
        Assert.Equal(new Vector3(new Length(1), new Length(2), new Length(9)), v.WithComponent(Axis.Z, new Length(9)));

        Point3 p = new(new Length(4), new Length(5), new Length(6));

        Assert.Equal(new Length(4), p.Component(Axis.X));
        Assert.Equal(new Length(5), p.Component(Axis.Y));
        Assert.Equal(new Length(6), p.Component(Axis.Z));

        Assert.Equal(new Point3(new Length(9), new Length(5), new Length(6)), p.WithComponent(Axis.X, new Length(9)));
        Assert.Equal(new Point3(new Length(4), new Length(9), new Length(6)), p.WithComponent(Axis.Y, new Length(9)));
        Assert.Equal(new Point3(new Length(4), new Length(5), new Length(9)), p.WithComponent(Axis.Z, new Length(9)));
    }

    [Fact]
    public void AnUndefinedAxisIsRefused()
    {
        Axis bogus = (Axis)3;
        Vector3 v = Vector3.Zero;
        Point3 p = Point3.Origin;

        Assert.Throws<ArgumentOutOfRangeException>(() => Vector3.Along(bogus, Length.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.Component(bogus));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.WithComponent(bogus, Length.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.Component(bogus));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.WithComponent(bogus, Length.Zero));
    }

    [Fact]
    public void ArithmeticIsExact()
    {
        // Values beyond 2^53 units: any detour through double would lose the odd unit.
        long big = (1L << 53) + 1;
        Vector3 a = new(new Length(big), new Length(-2), new Length(3));
        Vector3 b = new(new Length(1), new Length(big), new Length(-5));

        Assert.Equal(new Vector3(new Length(big + 1), new Length(big - 2), new Length(-2)), a + b);
        Assert.Equal(new Vector3(new Length(big - 1), new Length(-2 - big), new Length(8)), a - b);
        Assert.Equal(new Vector3(new Length(-big), new Length(2), new Length(-3)), -a);
        Assert.Equal(new Vector3(new Length(-3), new Length(-3 * big), new Length(15)), b * -3);
        Assert.Equal(Vector3.Zero, a - a);
        Assert.Equal(new Vector2(new Length(big), new Length(-2)), a.XY);
    }

    [Fact]
    public void PointsAndDisplacementsCombineExactly()
    {
        Point3 from = Point3.Inches(-3, -9, 0);
        Point3 to = Point3.Inches(3, 0, 27);
        Vector3 d = to - from;

        Assert.Equal(new Vector3(Length.Inches(6), Length.Inches(9), Length.Inches(27)), d);
        Assert.Equal(to, from + d);
        Assert.Equal(from, to - d);
        Assert.Equal(new Point3(Length.Inches(-3), Length.Inches(-9), Length.Zero), from);
        Assert.Equal(Point2.Inches(3, 0), to.XY);
        Assert.Equal(Point3.Origin, Point3.Origin + Vector3.Zero);
    }

    [Fact]
    public void ToStringNamesAllThreeComponents()
    {
        Vector3 v = new(new Length(1), new Length(2), new Length(3));
        Point3 p = new(new Length(1), new Length(2), new Length(3));

        Assert.Equal($"({new Length(1)}, {new Length(2)}, {new Length(3)})", v.ToString());
        Assert.Equal(v.ToString(), p.ToString());
    }
}
