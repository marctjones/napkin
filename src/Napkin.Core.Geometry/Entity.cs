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

    /// <summary>
    /// Whether this is already there, going in, or coming out (format version 10,
    /// <c>docs/design/renovation-sketches.md</c> §6.1). <see cref="Phase.New"/> unless set; a copy
    /// carries it.
    /// </summary>
    public Phase Phase { get; init; } = Phase.New;

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
/// A block defined by its parameters, not its corners: furniture parts, walls, openings. It has a
/// position and one of the 24 axis-aligned orientations in space
/// (<c>docs/design/assembly-model.md</c> §1.1).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Parametric on purpose.</strong> A box stores what the user typed — its width, height
/// and depth — and derives its corners. The cut list (#8) reads <see cref="Width"/>,
/// <see cref="Height"/> and <see cref="Depth"/>, so a 10&#x2033; part is 10&#x2033; even when it is
/// rotated 37&#xB0; and its rounded corners are 10.0004&#x2033; apart. Corners of boxes turned by
/// right angles are exact sums and never round (design &#xA7;2.3, assembly-model &#xA7;1.3).
/// </para>
/// <para>
/// <strong>The plain-English trap</strong> (assembly-model &#xA7;1.2): <see cref="Height"/> is the
/// size along <em>local Y</em> — the plan-view depth of the footprint — and <see cref="Depth"/> is
/// the size along local Z, which for a box lying as drawn is what a person would call its height or
/// thickness.
/// </para>
/// <para>
/// A box whose <see cref="Anchor"/> Z is zero, whose <see cref="FaceUp"/> is
/// <see cref="BoxFace.Top"/> and whose <see cref="Rotation"/> is a right-angle multiple is the
/// plan box napkin had before assembly-model in every respect: same corners in the plan, same
/// relationships, same cut-list row, same drawing.
/// </para>
/// <para>
/// A wall in plan view is a box whose <see cref="Width"/> is its length, <see cref="Height"/> its
/// thickness and <see cref="Depth"/> its height; an opening is a box related to its wall. The
/// building module (#18) adds what a wall supports on its own types that reference the box by id;
/// the geometry kernel knows nothing about headers.
/// </para>
/// </remarks>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the box is drawn on.</param>
/// <param name="Anchor">The local origin corner: south-west-bottom in the local frame.</param>
/// <param name="Width">Along local X; must be greater than zero.</param>
/// <param name="Height">Along local Y; must be greater than zero.</param>
/// <param name="Depth">Along local Z; must be greater than zero. What a part's third, out-of-plan dimension is.</param>
/// <param name="FaceUp">Which local face points to world +Z. <see cref="BoxFace.Top"/> is the box as drawn.</param>
/// <param name="Rotation">The spin about world Z, about <paramref name="Anchor"/>; right-angle multiples only in the direct updater.</param>
public sealed record Box(
    EntityId Id,
    LayerId Layer,
    Point3 Anchor,
    Length Width,
    Length Height,
    Length Depth,
    BoxFace FaceUp,
    Angle Rotation) : Entity(Id, Layer)
{
    /// <summary>
    /// The depth a box gets when nothing says otherwise: 3/4&#x2033;, 768 units — what the
    /// rectangle tool draws (<c>docs/design/assembly-model.md</c> &#xA7;11 decision 7). A design
    /// choice for a default, visible and editable in the properties panel; not a stock dimension.
    /// </summary>
    public static readonly Length DefaultDepth = new(768);

    /// <summary>A box as drawn — top up, at the plan datum Z = 0 — from its plan anchor and its three sizes.</summary>
    /// <param name="id">The entity's identity.</param>
    /// <param name="layer">The layer the box is drawn on.</param>
    /// <param name="anchor">The anchor in the plan; Z is zero.</param>
    /// <param name="width">Along local X.</param>
    /// <param name="height">Along local Y.</param>
    /// <param name="depth">Along local Z.</param>
    /// <param name="rotation">The spin about world Z.</param>
    public static Box AsDrawn(EntityId id, LayerId layer, Point2 anchor, Length width, Length height, Length depth, Angle rotation)
        => new(id, layer, new Point3(anchor.X, anchor.Y, Length.Zero), width, height, depth, BoxFace.Top, rotation);

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
    /// What the person entered for this box as a wall — what it supports and its stud spacing —
    /// or <see langword="null"/> when nothing has been (format version 6). Fields on the box, like
    /// <see cref="Part"/>, so undo, copy and delete carry it; nothing in the kernel reads it.
    /// </summary>
    public WallInputs? WallInputs { get; init; }

    /// <summary>
    /// The finishes and measurements the person entered for this box as a room, or
    /// <see langword="null"/> when none (format version 10, renovation-sketches §7). Nothing in the
    /// kernel reads it.
    /// </summary>
    public RoomInputs? Room { get; init; }

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
    /// The shape that is left: derived, never stored (shaped-parts §1.5). In the blank's own local
    /// XY frame — the cap of the solid — with the south-west corner at the origin,
    /// counter-clockwise, starting along the south edge.
    /// </summary>
    /// <remarks>
    /// Local, not world, since docs/design/assembly-model.md §7.2: the world form of a tipped box's
    /// outline is not a plan shape at all. Whoever places it — the plan canvas, by the box's
    /// orientation — does so from this and the box's anchor and orientation.
    /// </remarks>
    public Outline Outline() => OutlineBuilder.Build(this);

    /// <summary>
    /// The shape in space: the local <see cref="Outline"/> extruded along local Z from 0 to
    /// <see cref="Depth"/>, then oriented (<c>docs/design/assembly-model.md</c> &#xA7;4.1). Derived,
    /// never stored.
    /// </summary>
    /// <remarks>
    /// Every vertex is <see cref="World"/> of an outline point at local z = 0 or <see cref="Depth"/>,
    /// so it is exact for all 24 orientations. A box with no cuts gives six quads, one per
    /// <see cref="BoxFace"/>.
    /// </remarks>
    public Solid Solid() => SolidBuilder.Build(this);

    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };

    /// <summary>
    /// How the box is turned: which face is up, then the spin (<c>docs/design/assembly-model.md</c>
    /// &#xA7;1.3). Derived from the two stored fields, which are its one spelling.
    /// </summary>
    public Orientation Orientation => new(FaceUp, Rotation);

    /// <summary>
    /// Where a local point of the box lands in the world: <c>Anchor + Rz(Rotation) · Tip(FaceUp) · p</c>
    /// (&#xA7;1.3). Exact for all 24 orientations: every component is an anchor coordinate plus or
    /// minus a component of <paramref name="local"/>.
    /// </summary>
    public Point3 World(Vector3 local) => Anchor + Orientation.Apply(local);

    /// <summary>
    /// Where a vertex is: the corner of the blank as drawn, at the bottom (local z = 0) or the top
    /// (local z = <see cref="Depth"/>). Exact for all 24 orientations when <see cref="Rotation"/> is
    /// a right-angle multiple; otherwise X and Y round through <see cref="Length.FromInches"/>,
    /// which only the solver path reaches.
    /// </summary>
    public Point3 Vertex(BoxCorner corner, BoxLevel level)
    {
        Vector2 plan = LocalOffset(corner);
        Length z = level switch
        {
            BoxLevel.Bottom => Length.Zero,
            BoxLevel.Top => Depth,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Not a box level."),
        };

        return World(new Vector3(plan.Dx, plan.Dy, z));
    }

    /// <summary>
    /// Where a corner of the blank as drawn is, in the plan: the X and Y of its bottom vertex.
    /// </summary>
    /// <remarks>
    /// For a <see cref="BoxFace.Top"/> box this is the plan corner it always was — the local
    /// upright at that corner stands vertical and projects to this point — where a
    /// <see cref="FeatureRef"/> to <see cref="BoxFeature.LocalUpright"/> is, on X and Y. The plan
    /// canvas reads a box's corners through <see cref="Footprint"/> instead, because a tipped box's
    /// plan south-west is not its blank's.
    /// </remarks>
    public Point2 Corner(BoxCorner which) => Vertex(which, BoxLevel.Bottom).XY;

    /// <summary>
    /// The centre, in space. Rounds by at most half a unit per axis when a size is an odd number
    /// of units: each half-size is rounded first, then placed exactly.
    /// </summary>
    public Point3 Center => World(new Vector3(
        Width.Divide(2, Rounding.HalfToEven),
        Height.Divide(2, Rounding.HalfToEven),
        Depth.Divide(2, Rounding.HalfToEven)));

    /// <summary>
    /// What the plan view sees of this box: a rectangle spun by <see cref="Rotation"/> about the
    /// anchor (<c>docs/design/assembly-model.md</c> &#xA7;7.1). For a <see cref="BoxFace.Top"/>
    /// box it is the anchor's X and Y, no offset, <see cref="Width"/> by <see cref="Height"/>.
    /// </summary>
    public Footprint Footprint() => Geometry.Footprint.Of(this);

    /// <summary>The size along <paramref name="axis"/> of the box's <em>local</em> frame.</summary>
    public Length Size(Axis axis) => axis switch
    {
        Axis.X => Width,
        Axis.Y => Height,
        Axis.Z => Depth,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
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
           && Depth == other.Depth
           && FaceUp == other.FaceUp
           && Rotation == other.Rotation
           && Part == other.Part
           && WallInputs == other.WallInputs
           && Room == other.Room
           && _cuts.SequenceEqual(other._cuts);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(base.GetHashCode());
        hash.Add(Anchor);
        hash.Add(Width);
        hash.Add(Height);
        hash.Add(Depth);
        hash.Add(FaceUp);
        hash.Add(Rotation);
        hash.Add(Part);
        hash.Add(WallInputs);
        hash.Add(Room);
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
