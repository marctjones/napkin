namespace Napkin.Core.Geometry;

/// <summary>
/// Anything the sketch holds by id. All entities are immutable records (design &#xA7;2.3).
/// </summary>
/// <param name="Id">The entity's identity, stable for its whole life.</param>
/// <param name="Layer">The layer the entity is drawn on.</param>
public abstract record Entity(EntityId Id, LayerId Layer)
{
    /// <summary>This entity moved to another layer.</summary>
    public abstract Entity OnLayer(LayerId layer);
}

/// <summary>
/// A free point. Used for construction geometry and as the endpoints of segments.
/// </summary>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the node is drawn on.</param>
/// <param name="Position">Where the node is.</param>
public sealed record Node(EntityId Id, LayerId Layer, Point2 Position) : Entity(Id, Layer)
{
    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };
}

/// <summary>
/// A straight segment between two nodes. Construction lines; wall centerlines later.
/// </summary>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the segment is drawn on.</param>
/// <param name="Start">The node the segment starts at.</param>
/// <param name="End">The node the segment ends at.</param>
public sealed record Segment(EntityId Id, LayerId Layer, EntityId Start, EntityId End) : Entity(Id, Layer)
{
    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };
}

/// <summary>
/// A rectangle defined by its parameters, not its corners: furniture parts, walls, openings.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Parametric on purpose.</strong> A box stores what the user typed — its width and
/// height — and derives its corners. The cut list (#8) reads <see cref="Width"/> and
/// <see cref="Height"/>, so a 10&#x2033; part is 10&#x2033; even when it is rotated 37&#xB0; and
/// its rounded corners are 10.0004&#x2033; apart. Corners of right-angle-rotated boxes are exact
/// sums and never round (design &#xA7;2.3).
/// </para>
/// <para>
/// A wall in plan view is a box whose <see cref="Width"/> is its length and <see cref="Height"/>
/// its thickness; an opening is a box related to its wall. The building module (#18) adds what a
/// wall supports on its own types that reference the box by id; the geometry kernel knows nothing
/// about headers.
/// </para>
/// </remarks>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the box is drawn on.</param>
/// <param name="Anchor">The local origin corner, <see cref="BoxCorner.SouthWest"/> in the local frame.</param>
/// <param name="Width">Along local X; must be greater than zero.</param>
/// <param name="Height">Along local Y; must be greater than zero.</param>
/// <param name="Rotation">About <paramref name="Anchor"/>; right-angle multiples only in the first beta.</param>
public sealed record Box(
    EntityId Id,
    LayerId Layer,
    Point2 Anchor,
    Length Width,
    Length Height,
    Angle Rotation) : Entity(Id, Layer)
{
    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };

    /// <summary>
    /// Where a corner is. Exact when <see cref="Rotation"/> is a right-angle multiple; otherwise
    /// it rounds through <see cref="Length.FromInches"/>, which only the solver path reaches.
    /// </summary>
    public Point2 Corner(BoxCorner which) => Anchor + LocalOffset(which).Rotate(Rotation);

    /// <summary>
    /// The centre. Rounds by at most half a unit when a side is an odd number of units.
    /// </summary>
    public Point2 Center => Anchor + new Vector2(
        Width.Divide(2, Rounding.HalfToEven),
        Height.Divide(2, Rounding.HalfToEven)).Rotate(Rotation);

    /// <summary>The size along <paramref name="axis"/> of the box's <em>local</em> frame.</summary>
    public Length Size(Axis axis) => axis == Axis.X ? Width : Height;

    /// <summary>The displacement from the anchor to a corner, before rotation.</summary>
    public Vector2 LocalOffset(BoxCorner which) => which switch
    {
        BoxCorner.SouthWest => Vector2.Zero,
        BoxCorner.SouthEast => new Vector2(Width, Length.Zero),
        BoxCorner.NorthEast => new Vector2(Width, Height),
        BoxCorner.NorthWest => new Vector2(Length.Zero, Height),
        _ => throw new ArgumentOutOfRangeException(nameof(which), which, "Unknown corner."),
    };

    /// <summary>The two corners an edge runs between, in the local frame.</summary>
    public static (BoxCorner From, BoxCorner To) Ends(BoxEdge edge) => edge switch
    {
        BoxEdge.South => (BoxCorner.SouthWest, BoxCorner.SouthEast),
        BoxEdge.East => (BoxCorner.SouthEast, BoxCorner.NorthEast),
        BoxEdge.North => (BoxCorner.NorthWest, BoxCorner.NorthEast),
        BoxEdge.West => (BoxCorner.SouthWest, BoxCorner.NorthWest),
        _ => throw new ArgumentOutOfRangeException(nameof(edge), edge, "Unknown edge."),
    };
}

/// <summary>
/// Where a dimension's witness lines and text sit. Canvas-only data; it never affects geometry.
/// </summary>
/// <param name="Offset">How far the dimension line sits from what it measures.</param>
/// <param name="Side">Which side of what it measures the dimension line sits on.</param>
public readonly record struct DimensionPlacement(Length Offset, DimensionSide Side);

/// <summary>Which side of what it measures a dimension line sits on.</summary>
public enum DimensionSide
{
    /// <summary>Above, in the plan view.</summary>
    North,

    /// <summary>Below, in the plan view.</summary>
    South,

    /// <summary>To the right, in the plan view.</summary>
    East,

    /// <summary>To the left, in the plan view.</summary>
    West,
}

/// <summary>
/// An annotation that measures something and draws itself on the canvas.
/// </summary>
/// <remarks>
/// A dimension never stores a length of its own; its value is always computed from the geometry it
/// measures. A <em>driving</em> dimension has <see cref="Drives"/> set to the id of a
/// <see cref="ParamValue"/> or <see cref="AxisDistance"/> relationship over the same measurand,
/// and that relationship owns the number. A <em>reference</em> dimension has
/// <see cref="Drives"/> <see langword="null"/>: the canvas offers to make it driving, which is
/// <see cref="AddRelationship"/> plus setting <see cref="Drives"/> (design &#xA7;3.3).
/// </remarks>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the dimension is drawn on.</param>
/// <param name="Measures">What the dimension measures.</param>
/// <param name="Drives">The relationship that owns the number, or <see langword="null"/> for a reference dimension.</param>
/// <param name="Placement">Where the dimension line sits. Canvas-only data.</param>
public sealed record Dimension(
    EntityId Id,
    LayerId Layer,
    Measurand Measures,
    RelationshipId? Drives,
    DimensionPlacement Placement) : Entity(Id, Layer)
{
    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };
}
