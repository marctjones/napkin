namespace Napkin.Core.Geometry;

/// <summary>
/// A point in the plan view. X increases to the right, Y increases <em>upward</em> — the CAD, DXF
/// and PDF convention; the canvas applies the screen flip in its view transform and nowhere else
/// (docs/design/geometry-model.md &#xA7;2.1).
/// </summary>
/// <param name="X">The coordinate along X.</param>
/// <param name="Y">The coordinate along Y.</param>
public readonly record struct Point2(Length X, Length Y)
{
    /// <summary>The origin.</summary>
    public static readonly Point2 Origin = new(Length.Zero, Length.Zero);

    /// <summary>A point from inch values, for readable call sites in tests and fixtures.</summary>
    public static Point2 Inches(long x, long y) => new(Length.Inches(x), Length.Inches(y));

    /// <summary>The coordinate along <paramref name="axis"/>.</summary>
    public Length Component(Axis axis) => axis == Axis.X ? X : Y;

    /// <summary>This point with the coordinate along <paramref name="axis"/> replaced.</summary>
    public Point2 WithComponent(Axis axis, Length value)
        => axis == Axis.X ? this with { X = value } : this with { Y = value };

    /// <summary>The displacement from <paramref name="b"/> to <paramref name="a"/>.</summary>
    public static Vector2 operator -(Point2 a, Point2 b) => new(a.X - b.X, a.Y - b.Y);

    /// <summary>This point displaced.</summary>
    public static Point2 operator +(Point2 point, Vector2 displacement)
        => new(point.X + displacement.Dx, point.Y + displacement.Dy);

    /// <summary>This point displaced backwards.</summary>
    public static Point2 operator -(Point2 point, Vector2 displacement)
        => new(point.X - displacement.Dx, point.Y - displacement.Dy);

    /// <inheritdoc/>
    public override string ToString() => $"({X}, {Y})";
}
