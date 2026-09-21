using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// An axis-aligned box in model space, in exact <see cref="Length"/>s: what a zoom-to-fit frames.
/// </summary>
/// <remarks>
/// Kept exact rather than in inches because it is accumulated from model coordinates, and only the
/// transform is allowed to leave the integer grid. The inch accessors exist for the one consumer
/// that needs them, <see cref="ViewTransform.FitTo"/>.
/// </remarks>
public readonly record struct WorldBounds
{
    WorldBounds(Length minX, Length minY, Length maxX, Length maxY, bool isEmpty)
    {
        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
        IsEmpty = isEmpty;
    }

    /// <summary>Bounds containing nothing. Growing them by a point gives that point.</summary>
    public static readonly WorldBounds Empty =
        new(Length.Zero, Length.Zero, Length.Zero, Length.Zero, isEmpty: true);

    /// <summary>The left edge.</summary>
    public Length MinX { get; }

    /// <summary>The bottom edge — the model's Y is up.</summary>
    public Length MinY { get; }

    /// <summary>The right edge.</summary>
    public Length MaxX { get; }

    /// <summary>The top edge.</summary>
    public Length MaxY { get; }

    /// <summary>Whether nothing has been added yet.</summary>
    public bool IsEmpty { get; }

    /// <summary>The width, zero for empty bounds or a vertical line.</summary>
    public Length Width => IsEmpty ? Length.Zero : MaxX - MinX;

    /// <summary>The height, zero for empty bounds or a horizontal line.</summary>
    public Length Height => IsEmpty ? Length.Zero : MaxY - MinY;

    /// <summary>The width in inches, for the fit calculation.</summary>
    public double WidthInches => Width.ToInches();

    /// <summary>The height in inches, for the fit calculation.</summary>
    public double HeightInches => Height.ToInches();

    /// <summary>The centre's X in inches, for the fit calculation.</summary>
    public double CenterXInches => IsEmpty ? 0 : (MinX.ToInches() + MaxX.ToInches()) / 2.0;

    /// <summary>The centre's Y in inches, for the fit calculation.</summary>
    public double CenterYInches => IsEmpty ? 0 : (MinY.ToInches() + MaxY.ToInches()) / 2.0;

    /// <summary>These bounds grown to contain a point.</summary>
    public WorldBounds Including(Point2 point) => IsEmpty
        ? new WorldBounds(point.X, point.Y, point.X, point.Y, isEmpty: false)
        : new WorldBounds(
            Length.Min(MinX, point.X),
            Length.Min(MinY, point.Y),
            Length.Max(MaxX, point.X),
            Length.Max(MaxY, point.Y),
            isEmpty: false);

    /// <summary>These bounds grown to contain every one of some points.</summary>
    public WorldBounds Including(IEnumerable<Point2> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        WorldBounds bounds = this;
        foreach (Point2 point in points)
        {
            bounds = bounds.Including(point);
        }

        return bounds;
    }

    /// <summary>These bounds grown to contain other bounds.</summary>
    public WorldBounds Including(WorldBounds other) => other.IsEmpty
        ? this
        : Including(new Point2(other.MinX, other.MinY)).Including(new Point2(other.MaxX, other.MaxY));

    /// <summary>These bounds pushed out by a margin on every side.</summary>
    public WorldBounds Inflated(Length margin) => IsEmpty
        ? this
        : new WorldBounds(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin, isEmpty: false);

    /// <inheritdoc/>
    public override string ToString() =>
        IsEmpty ? "empty" : $"({MinX}, {MinY}) to ({MaxX}, {MaxY})";
}
