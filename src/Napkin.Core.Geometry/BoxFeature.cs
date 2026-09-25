using System.Collections.Immutable;
using System.Numerics;

namespace Napkin.Core.Geometry;

/// <summary>
/// A face of a box, in its local frame before orientation (docs/design/assembly-model.md &#xA7;1.5).
/// The four sides carry the names of the plan edges they are seen as from above; Bottom is local
/// z = 0 and Top is local z = depth.
/// </summary>
/// <remarks>
/// The declaration order is the one spelling of a <see cref="BoxFeature"/>, in memory and in the
/// file; do not reorder it.
/// </remarks>
public enum BoxFace
{
    /// <summary>The local y = 0 face, seen from above as <see cref="BoxEdge.South"/>.</summary>
    South,

    /// <summary>The local x = width face, seen from above as <see cref="BoxEdge.East"/>.</summary>
    East,

    /// <summary>The local y = height face, seen from above as <see cref="BoxEdge.North"/>.</summary>
    North,

    /// <summary>The local x = 0 face, seen from above as <see cref="BoxEdge.West"/>.</summary>
    West,

    /// <summary>The local z = 0 face.</summary>
    Bottom,

    /// <summary>The local z = depth face.</summary>
    Top,
}

/// <summary>Bottom or top: which end of a local upright edge, or which level a horizontal edge is at.</summary>
public enum BoxLevel
{
    /// <summary>Local z = 0, the <see cref="BoxFace.Bottom"/> face.</summary>
    Bottom,

    /// <summary>Local z = depth, the <see cref="BoxFace.Top"/> face.</summary>
    Top,
}

/// <summary>
/// One feature of a box — a face, an edge (two adjacent faces) or a vertex (three mutually adjacent
/// faces) — named in the box's local frame (docs/design/assembly-model.md &#xA7;1.5).
/// </summary>
/// <remarks>
/// <para>
/// A feature <em>is</em> the set of faces that meet at it. It is held as that set, so two
/// spellings of one edge — <c>Edge(West, South)</c> and <c>Edge(South, West)</c> — are one value,
/// and <see cref="Faces"/> lists them in <see cref="BoxFace"/> order. Opposite faces
/// (<see cref="BoxFace.South"/>/<see cref="BoxFace.North"/>, <see cref="BoxFace.East"/>/<see cref="BoxFace.West"/>,
/// <see cref="BoxFace.Bottom"/>/<see cref="BoxFace.Top"/>) never share a feature.
/// </para>
/// <para>
/// <c>default(BoxFeature)</c> names no faces and is not a feature; every factory here returns a
/// real one.
/// </para>
/// </remarks>
public readonly record struct BoxFeature
{
    private const int FaceCount = 6;

    // One bit per BoxFace, bit i for (BoxFace)i. The only field, so value equality is set equality.
    private readonly int mask;

    private BoxFeature(int mask) => this.mask = mask;

    /// <summary>The faces that meet at this feature, in <see cref="BoxFace"/> order: one spelling.</summary>
    public ImmutableArray<BoxFace> Faces
    {
        get
        {
            ImmutableArray<BoxFace>.Builder faces = ImmutableArray.CreateBuilder<BoxFace>(BitOperations.PopCount((uint)mask));
            for (int bit = 0; bit < FaceCount; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    faces.Add((BoxFace)bit);
                }
            }

            return faces.MoveToImmutable();
        }
    }

    /// <summary>2 for a face, 1 for an edge, 0 for a vertex.</summary>
    /// <exception cref="InvalidOperationException">This is <c>default(BoxFeature)</c>, which names no faces.</exception>
    public int Dimension => mask == 0
        ? throw new InvalidOperationException("default(BoxFeature) names no faces and is not a feature.")
        : 3 - BitOperations.PopCount((uint)mask);

    /// <summary>One face.</summary>
    public static BoxFeature Face(BoxFace face) => new(Bit(face));

    /// <summary>The edge where two adjacent faces meet, in either order.</summary>
    /// <exception cref="ArgumentException">The faces are the same face or opposite faces.</exception>
    public static BoxFeature Edge(BoxFace a, BoxFace b)
    {
        int bitA = Bit(a);
        int bitB = Bit(b);
        if (a == b || Opposite(a) == b)
        {
            throw new ArgumentException($"{a} and {b} do not meet at an edge: an edge is two adjacent faces.");
        }

        return new BoxFeature(bitA | bitB);
    }

    /// <summary>The vertex where three mutually adjacent faces meet, in any order.</summary>
    /// <exception cref="ArgumentException">Two of the faces are the same face or opposite faces.</exception>
    public static BoxFeature Vertex(BoxFace a, BoxFace b, BoxFace c)
    {
        int bits = Bit(a) | Bit(b) | Bit(c);
        if (a == b || b == c || a == c || Opposite(a) == b || Opposite(b) == c || Opposite(a) == c)
        {
            throw new ArgumentException($"{a}, {b} and {c} do not meet at a vertex: a vertex is three mutually adjacent faces.");
        }

        return new BoxFeature(bits);
    }

    /// <summary>
    /// The edge along <em>local</em> Z at a corner of the blank as drawn: <c>LocalUpright(SouthWest)</c>
    /// is <c>Edge(South, West)</c>. It points up only when the box's face-up is
    /// <see cref="BoxFace.Top"/>; the edge a plan corner projects from is the footprint's business.
    /// </summary>
    public static BoxFeature LocalUpright(BoxCorner corner)
    {
        (BoxFace northSouth, BoxFace eastWest) = SidesAt(corner);
        return Edge(northSouth, eastWest);
    }

    /// <summary>The vertex at a corner of the blank as drawn, at the bottom or the top.</summary>
    public static BoxFeature Vertex(BoxCorner corner, BoxLevel level)
    {
        (BoxFace northSouth, BoxFace eastWest) = SidesAt(corner);
        BoxFace cap = level switch
        {
            BoxLevel.Bottom => BoxFace.Bottom,
            BoxLevel.Top => BoxFace.Top,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "Not a box level."),
        };

        return Vertex(northSouth, eastWest, cap);
    }

    /// <summary>The face on the other side of the box: South and North, East and West, Bottom and Top.</summary>
    internal static BoxFace Opposite(BoxFace face) => face switch
    {
        BoxFace.South => BoxFace.North,
        BoxFace.North => BoxFace.South,
        BoxFace.East => BoxFace.West,
        BoxFace.West => BoxFace.East,
        BoxFace.Bottom => BoxFace.Top,
        BoxFace.Top => BoxFace.Bottom,
        _ => throw new ArgumentOutOfRangeException(nameof(face), face, "Not a box face."),
    };

    /// <inheritdoc/>
    public override string ToString() => mask == 0
        ? "BoxFeature(none)"
        : $"{(BitOperations.PopCount((uint)mask) switch { 1 => "Face", 2 => "Edge", _ => "Vertex" })}({string.Join(", ", Faces)})";

    // The two side faces that meet at a plan corner, from BoxCorner's own definitions: SouthWest is
    // local (0, 0), SouthEast (width, 0), NorthEast (width, height), NorthWest (0, height).
    private static (BoxFace NorthSouth, BoxFace EastWest) SidesAt(BoxCorner corner) => corner switch
    {
        BoxCorner.SouthWest => (BoxFace.South, BoxFace.West),
        BoxCorner.SouthEast => (BoxFace.South, BoxFace.East),
        BoxCorner.NorthEast => (BoxFace.North, BoxFace.East),
        BoxCorner.NorthWest => (BoxFace.North, BoxFace.West),
        _ => throw new ArgumentOutOfRangeException(nameof(corner), corner, "Not a box corner."),
    };

    private static int Bit(BoxFace face)
        => face is >= BoxFace.South and <= BoxFace.Top
            ? 1 << (int)face
            : throw new ArgumentOutOfRangeException(nameof(face), face, "Not a box face.");
}
