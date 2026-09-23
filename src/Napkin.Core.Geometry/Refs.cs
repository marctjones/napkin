namespace Napkin.Core.Geometry;

/// <summary>A corner of a box, named in the box's own local frame before rotation.</summary>
public enum BoxCorner
{
    /// <summary>The local origin corner, which is the box's <see cref="Box.Anchor"/>.</summary>
    SouthWest,

    /// <summary>The corner at local (width, 0).</summary>
    SouthEast,

    /// <summary>The corner at local (width, height).</summary>
    NorthEast,

    /// <summary>The corner at local (0, height).</summary>
    NorthWest,
}

/// <summary>An edge of a box, named in the box's own local frame before rotation.</summary>
public enum BoxEdge
{
    /// <summary>The local y = 0 edge, from <see cref="BoxCorner.SouthWest"/> to <see cref="BoxCorner.SouthEast"/>.</summary>
    South,

    /// <summary>The local x = width edge, from <see cref="BoxCorner.SouthEast"/> to <see cref="BoxCorner.NorthEast"/>.</summary>
    East,

    /// <summary>The local y = height edge, from <see cref="BoxCorner.NorthWest"/> to <see cref="BoxCorner.NorthEast"/>.</summary>
    North,

    /// <summary>The local x = 0 edge, from <see cref="BoxCorner.SouthWest"/> to <see cref="BoxCorner.NorthWest"/>.</summary>
    West,
}

/// <summary>
/// A reference to a point. Relationships never point at coordinates; they point at <em>what</em>
/// on <em>which</em> entity (design &#xA7;3.1).
/// </summary>
public abstract record PointRef
{
    private protected PointRef()
    {
    }

    /// <summary>The entity this point belongs to.</summary>
    public abstract EntityId Owner { get; }
}

/// <summary>A node's position.</summary>
/// <param name="Node">The node.</param>
public sealed record NodeRef(EntityId Node) : PointRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Node;
}

/// <summary>One corner of a box.</summary>
/// <param name="Box">The box.</param>
/// <param name="Corner">Which corner, in the box's local frame.</param>
public sealed record CornerRef(EntityId Box, BoxCorner Corner) : PointRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>The centre of a box.</summary>
/// <param name="Box">The box.</param>
public sealed record CenterRef(EntityId Box) : PointRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>A reference to an edge.</summary>
public abstract record EdgeRef
{
    private protected EdgeRef()
    {
    }

    /// <summary>The entity this edge belongs to.</summary>
    public abstract EntityId Owner { get; }
}

/// <summary>A whole segment, treated as an edge.</summary>
/// <param name="Segment">The segment.</param>
public sealed record SegmentRef(EntityId Segment) : EdgeRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Segment;
}

/// <summary>One edge of a box.</summary>
/// <param name="Box">The box.</param>
/// <param name="Edge">Which edge, in the box's local frame.</param>
public sealed record BoxEdgeRef(EntityId Box, BoxEdge Edge) : EdgeRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>A reference to a size: something a dimension can drive.</summary>
public abstract record ParamRef
{
    private protected ParamRef()
    {
    }

    /// <summary>The entity this size belongs to.</summary>
    public abstract EntityId Owner { get; }
}

/// <summary>A box's width, along its local X.</summary>
/// <param name="Box">The box.</param>
public sealed record BoxWidthRef(EntityId Box) : ParamRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>A box's height, along its local Y.</summary>
/// <param name="Box">The box.</param>
public sealed record BoxHeightRef(EntityId Box) : ParamRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>
/// A box's depth, along its local Z (<c>docs/design/assembly-model.md</c> §2.2). A typed depth is a
/// <see cref="ParamValue"/> on this, exactly as a typed width is one on <see cref="BoxWidthRef"/>.
/// </summary>
/// <param name="Box">The box.</param>
public sealed record BoxDepthRef(EntityId Box) : ParamRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>A segment's length.</summary>
/// <param name="Segment">The segment.</param>
public sealed record SegmentLengthRef(EntityId Segment) : ParamRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Segment;
}

/// <summary>
/// What a <see cref="Dimension"/> measures. A dimension never stores a length of its own; its
/// value is always computed from the geometry it measures (design &#xA7;3.3).
/// </summary>
public abstract record Measurand
{
    private protected Measurand()
    {
    }
}

/// <summary>A size: a box's width or height, or a segment's length.</summary>
/// <param name="Param">The size measured.</param>
public sealed record ParamMeasurand(ParamRef Param) : Measurand;

/// <summary>The signed distance between two points along one axis.</summary>
/// <param name="From">The point measured from.</param>
/// <param name="To">The point measured to.</param>
/// <param name="Axis">The axis measured along.</param>
public sealed record AxisMeasurand(PointRef From, PointRef To, Axis Axis) : Measurand;
