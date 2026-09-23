using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// Anything the sketch holds by id. All entities are immutable records (design &#xA7;2.3).
/// </summary>
/// <param name="Id">The entity's identity, stable for its whole life.</param>
/// <param name="Layer">The layer the entity is drawn on.</param>
public abstract record Entity(EntityId Id, LayerId Layer)
{
    /// <summary>
    /// What this is called — "Leg, south-west". Empty means unnamed, which is legal.
    /// </summary>
    /// <remarks>
    /// A name is not an id and is not unique: the cut list groups by dimensions, never by name
    /// (<c>docs/design/parts-and-cut-list.md</c> §2.1). It is on every entity rather than only on
    /// a box because a named dimension reads better in a conflict message too.
    /// </remarks>
    public string Name { get; init; } = string.Empty;

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
    /// <summary>
    /// What this box is a piece of, or <see langword="null"/> when it is not a part — a wall, an
    /// opening.
    /// </summary>
    /// <remarks>
    /// A part is fields on the box rather than a side table, so undo, redo, save and load carry it
    /// for free. A box whose part is <see langword="null"/> behaves exactly as a box did before
    /// parts existed: nothing in the geometry kernel reads this.
    /// </remarks>
    public Part? Part { get; init; }

    /// <summary>
    /// What has been cut off the blank, in site order. Empty for a plain rectangle
    /// (<c>docs/design/shaped-parts-model.md</c> §1.1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The initialiser sorts, so §1.6's invariant 5 — cuts in <see cref="CutSite"/> order — holds
    /// by construction and <see cref="Sketch.Validate"/> only has to check that no site is used
    /// twice. Sorting is lossless, so the model normalises rather than rejects; a <em>file</em>
    /// whose cuts are out of order is refused by the loader instead (§5), because a file has one
    /// spelling.
    /// </para>
    /// <para>
    /// A box with an empty list is today's box in every respect: same corners, same relationships,
    /// same cut-list row, same drawing.
    /// </para>
    /// </remarks>
    public ImmutableList<Cut> Cuts
    {
        get => _cuts;
        init => _cuts = InSiteOrder(value);
    }

    /// <summary>
    /// The shape that is left: derived, never stored (§1.5). In world coordinates,
    /// counter-clockwise in the box's local frame, starting along the south edge.
    /// </summary>
    public Outline Outline() => OutlineBuilder.Build(this, world: true);

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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="axis"/> is not X or Y.</exception>
    public Length Size(Axis axis) => axis switch
    {
        Axis.X => Width,
        Axis.Y => Height,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "A plan box has a size along local X and Y only."),
    };

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

    /// <summary>
    /// Equality by value, including the cuts compared as a sequence.
    /// </summary>
    /// <remarks>
    /// <see cref="ImmutableList{T}"/> compares by the identity of the list it wraps, so the
    /// synthesised equality would call two boxes with the same cuts different — and #6's
    /// <c>Load(Save(s)) == s</c> and the cut list's by-value grouping both depend on it not doing
    /// that.
    /// </remarks>
    public bool Equals(Box? other)
        => base.Equals(other)
           && Anchor == other!.Anchor
           && Width == other.Width
           && Height == other.Height
           && Rotation == other.Rotation
           && Part == other.Part
           && _cuts.SequenceEqual(other._cuts);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(base.GetHashCode());
        hash.Add(Anchor);
        hash.Add(Width);
        hash.Add(Height);
        hash.Add(Rotation);
        hash.Add(Part);
        foreach (Cut cut in _cuts)
        {
            hash.Add(cut);
        }

        return hash.ToHashCode();
    }

    private readonly ImmutableList<Cut> _cuts = [];

    private static ImmutableList<Cut> InSiteOrder(ImmutableList<Cut> cuts)
    {
        ArgumentNullException.ThrowIfNull(cuts);

        // OrderBy is stable, so two cuts at one site keep the order they were given in and
        // Validate can report them.
        return cuts.Count < 2 ? cuts : [.. cuts.OrderBy(cut => cut.Site.Order)];
    }
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
