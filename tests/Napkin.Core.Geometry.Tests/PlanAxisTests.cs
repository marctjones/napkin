namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The plan-only types refuse <see cref="Axis.Z"/> out loud. Before Z existed their members were
/// two-way tests — <c>axis == Axis.X ? … : …</c> — so a Z would have been read as Y with no
/// complaint, which is exactly the silent corruption docs/design/assembly-model.md &#xA7;10 step 2
/// would otherwise have started producing.
/// </summary>
public class PlanAxisTests
{
    [Fact]
    public void APlanPointHasNoZ()
    {
        Point2 p = new(new Length(4), new Length(5));

        Assert.Equal(new Length(4), p.Component(Axis.X));
        Assert.Equal(new Length(5), p.Component(Axis.Y));
        Assert.Equal(new Point2(new Length(9), new Length(5)), p.WithComponent(Axis.X, new Length(9)));
        Assert.Equal(new Point2(new Length(4), new Length(9)), p.WithComponent(Axis.Y, new Length(9)));

        ArgumentOutOfRangeException error = Assert.Throws<ArgumentOutOfRangeException>(() => p.Component(Axis.Z));
        Assert.Contains("only X and Y", error.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => p.WithComponent(Axis.Z, Length.Zero));
    }

    [Fact]
    public void APlanDisplacementHasNoZ()
    {
        Vector2 v = new(new Length(1), new Length(2));

        Assert.Equal(new Length(1), v.Component(Axis.X));
        Assert.Equal(new Length(2), v.Component(Axis.Y));
        Assert.Equal(new Vector2(new Length(7), Length.Zero), Vector2.Along(Axis.X, new Length(7)));
        Assert.Equal(new Vector2(Length.Zero, new Length(7)), Vector2.Along(Axis.Y, new Length(7)));
        Assert.Equal(new Vector2(new Length(1), new Length(9)), v.WithComponent(Axis.Y, new Length(9)));

        Assert.Throws<ArgumentOutOfRangeException>(() => v.Component(Axis.Z));
        Assert.Throws<ArgumentOutOfRangeException>(() => v.WithComponent(Axis.Z, Length.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Vector2.Along(Axis.Z, new Length(7)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Vector2.Along((Axis)3, new Length(7)));
    }
}
