namespace Napkin.Core.Geometry;

/// <summary>
/// Which plane an end of a <see cref="Strut"/> is cut to. The world is axis-aligned except for
/// struts, so these are the only planes an end can meet (<c>docs/design/assembly-model.md</c> §3a.2).
/// </summary>
public enum EndCut
{
    /// <summary>Cut perpendicular to the strut's centreline: it butts into nothing flat.</summary>
    Square,

    /// <summary>Cut to the plane through the end normal to world X.</summary>
    X,

    /// <summary>Cut to the plane through the end normal to world Y.</summary>
    Y,

    /// <summary>Cut to the plane through the end normal to world Z: a foot on the floor, a top under a seat.</summary>
    Z,
}

/// <summary>One of a strut's two stored ends.</summary>
public enum StrutEnd
{
    /// <summary>The stored <see cref="Strut.From"/>.</summary>
    From,

    /// <summary>The stored <see cref="Strut.To"/>.</summary>
    To,
}

/// <summary>
/// One of a strut's four long faces, named by the blank's own compass: local ∓Y are the two edges of
/// the wide (drawn) face, local ∓Z the two wide faces themselves (<c>docs/design/angled-parts.md</c> §2.2).
/// </summary>
public enum StrutFace
{
    /// <summary>The face at local −Y.</summary>
    South,

    /// <summary>The face at local +Y.</summary>
    North,

    /// <summary>The face at local −Z.</summary>
    Bottom,

    /// <summary>The face at local +Z.</summary>
    Top,
}

/// <summary>An exact integer vector, wider than a <see cref="Vector3"/>: a strut's frame is products of coordinates.</summary>
/// <param name="X">The component along X.</param>
/// <param name="Y">The component along Y.</param>
/// <param name="Z">The component along Z.</param>
public readonly record struct IntegerVector3(Int128 X, Int128 Y, Int128 Z)
{
    /// <summary>A displacement's components, in grid units.</summary>
    public static IntegerVector3 Of(Vector3 vector) => new(vector.Dx.Units, vector.Dy.Units, vector.Dz.Units);

    /// <summary>The unit vector along an axis.</summary>
    public static IntegerVector3 Unit(Axis axis) => axis switch
    {
        Axis.X => new(1, 0, 0),
        Axis.Y => new(0, 1, 0),
        Axis.Z => new(0, 0, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The component along <paramref name="axis"/>.</summary>
    public Int128 Component(Axis axis) => axis switch
    {
        Axis.X => X,
        Axis.Y => Y,
        Axis.Z => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Not an axis."),
    };

    /// <summary>The exact dot product; throws on overflow rather than wrapping.</summary>
    public Int128 Dot(IntegerVector3 other) => checked((X * other.X) + (Y * other.Y) + (Z * other.Z));

    /// <summary>The exact cross product; throws on overflow rather than wrapping.</summary>
    public IntegerVector3 Cross(IntegerVector3 other) => checked(new IntegerVector3(
        (Y * other.Z) - (Z * other.Y),
        (Z * other.X) - (X * other.Z),
        (X * other.Y) - (Y * other.X)));

    /// <summary>The exact squared norm.</summary>
    public Int128 SquaredLength => Dot(this);

    /// <summary>Exact negation.</summary>
    public static IntegerVector3 operator -(IntegerVector3 value) => new(-value.X, -value.Y, -value.Z);
}

/// <summary>
/// A strut's local axes as exact integer vectors (<c>docs/design/angled-parts.md</c> §1.2, changing
/// assembly-model §3a.3 step 1): local X is <see cref="D"/>, local Z is <c>d × r̂</c> for the
/// reference axis, local Y is <c>z × d</c>. Right-handed; nothing is normalised.
/// </summary>
/// <param name="D">The direction, oriented so that it points along the reference axis (see <see cref="Reversed"/>).</param>
/// <param name="Y">Local Y: in the drawn (wide) face, perpendicular to <see cref="D"/>. <c>|y| = |z|·|d|</c>.</param>
/// <param name="Z">Local Z: normal to the drawn face.</param>
/// <param name="Reversed">
/// Whether the derivation took <see cref="Strut.To"/> as its first (west) end. Nothing stored
/// changes; this is what makes the derived blank the same for a leg drawn foot-first or top-first.
/// </param>
public sealed record StrutFrame(IntegerVector3 D, IntegerVector3 Y, IntegerVector3 Z, bool Reversed)
{
    /// <summary>The frame of a strut whose direction is not axis-aligned.</summary>
    /// <exception cref="ArgumentException">The direction is axis-aligned or zero (invariant 14): there is no frame.</exception>
    public static StrutFrame Of(Vector3 direction, Axis reference)
    {
        IntegerVector3 d = IntegerVector3.Of(direction);
        if (!Strut.LeansIn(direction))
        {
            throw new ArgumentException($"A strut running {direction} is axis-aligned and has no frame.", nameof(direction));
        }

        // d · r̂ > 0 (angled-parts §1.2). When the strut runs square to its reference axis that says
        // nothing, and the lower end comes first, then the south, then the west — the order §3a.3
        // gives the cut axes — so that the orientation is still one of a pair's two.
        Int128 along = d.Component(reference);
        bool reversed = along != 0 ? along < 0
            : d.Z != 0 ? d.Z < 0
            : d.Y < 0; // no Z run and no Y run would be axis-aligned, so d.Y is not zero here
        if (reversed)
        {
            d = -d;
        }

        IntegerVector3 z = d.Cross(IntegerVector3.Unit(reference));
        IntegerVector3 y = z.Cross(d);
        return new StrutFrame(d, y, z, reversed);
    }
}

/// <summary>
/// A member between two points in space, not axis-aligned: a splayed leg, a raked back, a brace.
/// Its cross-section is a rectangle from stock; its length and end cuts are derived from where its
/// ends are (<c>docs/design/assembly-model.md</c> §3a, as amended by <c>docs/design/angled-parts.md</c> §1).
/// </summary>
/// <param name="Id">The entity's identity.</param>
/// <param name="Layer">The layer the strut is drawn on.</param>
/// <param name="From">Exact: where the centreline meets the surface the From end is cut to.</param>
/// <param name="To">Exact: the same at the other end.</param>
/// <param name="FromCut">The plane the From end is cut to.</param>
/// <param name="ToCut">The plane the To end is cut to.</param>
/// <param name="Reference">The world axis the wide (drawn) face stays parallel to (angled-parts §1.2).</param>
/// <param name="Height">The cross-section size along local Y, in the drawn face; greater than zero.</param>
/// <param name="Depth">The cross-section size along local Z, out of the drawn face; greater than zero.</param>
public sealed record Strut(
    EntityId Id,
    LayerId Layer,
    Point3 From,
    Point3 To,
    EndCut FromCut,
    EndCut ToCut,
    Axis Reference,
    Length Height,
    Length Depth) : Entity(Id, Layer)
{
    /// <summary>
    /// What this strut is a piece of, or <see langword="null"/>. Its <see cref="PlanAxes.X"/> names
    /// the derived dimension — <c>length</c> or <c>width</c>, never <c>thickness</c> (invariant 16).
    /// </summary>
    public Part? Part { get; init; }

    /// <summary>The exact direction, <c>To − From</c>; never normalised in the kernel.</summary>
    public Vector3 Direction => To - From;

    /// <summary>The end at <paramref name="end"/>.</summary>
    public Point3 End(StrutEnd end) => end == StrutEnd.From ? From : To;

    /// <summary>The local axes as exact integer vectors. Only for a strut that is not axis-aligned (invariant 14).</summary>
    public StrutFrame Frame() => StrutFrame.Of(Direction, Reference);

    /// <summary>The board to cut, derived once from the exact ends (angled-parts §2). Only for a strut that passes invariants 14–16.</summary>
    public StrutBlank Blank() => StrutBlank.Derive(this);

    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };

    /// <summary>
    /// The first end cut whose plane contains the strut's direction — an <see cref="EndCut.X"/> end on
    /// a strut that runs square to X — or <see langword="null"/>. Such a plane never crosses the
    /// centreline, so there is no board: the end cannot be cut to it.
    /// </summary>
    public static EndCut? CutAlongItself(Strut strut)
    {
        ArgumentNullException.ThrowIfNull(strut);
        foreach (EndCut cut in new[] { strut.FromCut, strut.ToCut })
        {
            Length along = cut switch
            {
                EndCut.X => strut.Direction.Dx,
                EndCut.Y => strut.Direction.Dy,
                EndCut.Z => strut.Direction.Dz,
                _ => new Length(1),
            };
            if (along == Length.Zero)
            {
                return cut;
            }
        }

        return null;
    }

    /// <summary>Whether a direction differs in at least two coordinates — leans, rather than being a box laid along an axis (invariant 14).</summary>
    public static bool LeansIn(Vector3 direction)
        => (direction.Dx.Units != 0 ? 1 : 0) + (direction.Dy.Units != 0 ? 1 : 0) + (direction.Dz.Units != 0 ? 1 : 0) >= 2;
}
