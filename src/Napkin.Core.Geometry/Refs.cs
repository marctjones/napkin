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
/// Something with a place: a point, a line or a plane, each fixing some world axes
/// (<c>docs/design/assembly-model.md</c> &#xA7;2.2). Relationships never point at coordinates; they
/// point at <em>what</em> on <em>which</em> entity (geometry-model &#xA7;3.1).
/// </summary>
/// <remarks>
/// One base for points, lines and planes, because what a relationship may take is a validation rule
/// on the axes each place fixes (<see cref="PlaceRules"/>, &#xA7;2.3) rather than a static type: a
/// feature can be a face, an edge or a vertex, and a node — a two-axis thing — legally coincides
/// with an upright edge. <see cref="Sketch.PlaceOf"/> says what a reference fixes.
/// </remarks>
public abstract record PlaceRef
{
    private protected PlaceRef()
    {
    }

    /// <summary>The entity this place belongs to.</summary>
    public abstract EntityId Owner { get; }
}

/// <summary>A node's position. Fixes X and Y: a node lies at the plan datum and says nothing about Z.</summary>
/// <param name="Node">The node.</param>
public sealed record NodeRef(EntityId Node) : PlaceRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Node;
}

/// <summary>
/// A whole segment, as a line in the plan. Fixes X when it is vertical in the plan and Y when it is
/// horizontal, and nothing when it is diagonal.
/// </summary>
/// <param name="Segment">The segment.</param>
public sealed record SegmentRef(EntityId Segment) : PlaceRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Segment;
}

/// <summary>The centre of a box. Fixes X, Y and Z.</summary>
/// <param name="Box">The box.</param>
public sealed record CenterRef(EntityId Box) : PlaceRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Box;
}

/// <summary>
/// One feature of a box — a face, an edge or a vertex, named in the box's local frame — which fixes
/// one, two or three world axes once the box's orientation is applied
/// (<c>docs/design/assembly-model.md</c> &#xA7;2.1, &#xA7;2.2).
/// </summary>
/// <remarks>
/// The feature is the <em>blank's</em>: a face a cut has taken part of still fixes the blank's face
/// plane, and an upright edge at a clipped corner is the blank's virtual edge (&#xA7;2.5). A local
/// upright — <c>BoxFeature.LocalUpright(SouthWest)</c> — is what a plan corner was, and a side
/// face is what a plan edge was, on a box lying as drawn.
/// </remarks>
/// <param name="Box">The box.</param>
/// <param name="Feature">Which feature, in the box's local frame. <c>default(BoxFeature)</c> is not a feature, and <see cref="Sketch.Validate"/> refuses it (invariant 12).</param>
public sealed record FeatureRef(EntityId Box, BoxFeature Feature) : PlaceRef
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

/// <summary>A size: a box's width, height or depth, or a segment's length.</summary>
/// <param name="Param">The size measured.</param>
public sealed record ParamMeasurand(ParamRef Param) : Measurand;

/// <summary>The signed distance between two places along one axis, which both must fix.</summary>
/// <param name="From">The place measured from.</param>
/// <param name="To">The place measured to.</param>
/// <param name="Axis">The axis measured along: X or Y, since a dimension lies in the plan (assembly-model invariant 13).</param>
public sealed record AxisMeasurand(PlaceRef From, PlaceRef To, Axis Axis) : Measurand;

/// <summary>
/// One of a strut's two stored ends: a point fixing X, Y and Z (<c>docs/design/assembly-model.md</c>
/// &#xA7;3a.5). It takes part in <see cref="Coincident"/>, <see cref="AxisDistance"/> and
/// <see cref="Centered"/>, and in nothing that needs a face.
/// </summary>
/// <param name="Strut">The strut.</param>
/// <param name="End">Which end.</param>
public sealed record StrutEndRef(EntityId Strut, StrutEnd End) : PlaceRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Strut;
}

/// <summary>
/// One of a strut's four long faces (<c>docs/design/angled-parts.md</c> &#xA7;3.2). In the file and
/// the kernel since format 11; a place a relationship may hold only once slice B (#190) gives a
/// one-way lean's side faces their plane. Until then every relationship naming one is refused:
/// a strut's body is not a place (assembly-model &#xA7;3a.5).
/// </summary>
/// <param name="Strut">The strut.</param>
/// <param name="Face">Which long face.</param>
public sealed record StrutFaceRef(EntityId Strut, StrutFace Face) : PlaceRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Strut;
}

/// <summary>
/// The end face of a strut, as a joint's inserted face (<c>docs/design/angled-parts.md</c> &#xA7;5).
/// In the file and the kernel since format 11; joinery on it is slice E (#193), and until then
/// every relationship naming one is refused.
/// </summary>
/// <param name="Strut">The strut.</param>
/// <param name="End">Which end.</param>
public sealed record StrutEndFaceRef(EntityId Strut, StrutEnd End) : PlaceRef
{
    /// <inheritdoc/>
    public override EntityId Owner => Strut;
}
