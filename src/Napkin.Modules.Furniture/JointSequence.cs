using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>What one entry of a part's joinery is (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;6.3).</summary>
public enum JointFactKind
{
    /// <summary>A rabbet cut at an end of the part.</summary>
    Rabbet,

    /// <summary>A groove (along the length) or a dado (across the width).</summary>
    Groove,

    /// <summary>A half-lap on the part.</summary>
    HalfLap,

    /// <summary>Pocket holes drilled in one end of the part.</summary>
    PocketHoles,

    /// <summary>Tabletop clips along the part's top edge.</summary>
    TabletopClips,

    /// <summary>The part is longer or wider than drawn by the depth of a groove or rabbet it is inserted into.</summary>
    Allowance,
}

/// <summary>
/// One entry of a part's joinery in its <em>structural</em> form: the numbers and the faces, in the
/// part's own frame, and no words. Two parts have the same joinery when their entries are the same
/// values, up to a half-turn (<see cref="JointSequence"/>).
/// </summary>
/// <remarks>
/// A field a kind does not use is <see langword="null"/> or zero, so that a half-turn, which renames
/// faces, never touches what was not there.
/// </remarks>
/// <param name="Kind">What this is.</param>
/// <param name="Face">The face it is on: a groove's, a rabbet's, a lap's; the face pocket holes are drilled from; the face clips go on; the contact face an allowance is at.</param>
/// <param name="End">A rabbet's end; the face a groove's offset is measured from; the end a lap is nearest; the end pocket holes are drilled in.</param>
/// <param name="Dimension">An allowance's dimension: the one it lengthens.</param>
/// <param name="Type">An allowance's joint type (groove or rabbet).</param>
/// <param name="Direction">Whether a groove runs along the length or across the width.</param>
/// <param name="Width">A groove's or rabbet's width.</param>
/// <param name="Depth">A groove's, rabbet's or lap's depth; an allowance's amount.</param>
/// <param name="Long">A lap's length along the part.</param>
/// <param name="Offset">A groove's offset from its edge; a lap's from its end.</param>
/// <param name="Count">Pocket holes or clips.</param>
public readonly record struct JointFact(
    JointFactKind Kind,
    BoxFace? Face = null,
    BoxFace? End = null,
    PartDimension? Dimension = null,
    JointType? Type = null,
    GrooveDirection? Direction = null,
    Length Width = default,
    Length Depth = default,
    Length Long = default,
    Length Offset = default,
    int Count = 0);

/// <summary>
/// A part's joinery compared, and ordered (<c>docs/design/joinery-and-fasteners.md</c> &#xA7;6.3).
/// </summary>
/// <remarks>
/// <para>
/// Two joinery sets are equal when one is the other under a <strong>half-turn of the part about
/// any of its own three axes</strong> — the ways a person can pick up an identical blank and use it
/// the other way round. A half-turn renames faces: about X, south and north swap and bottom and
/// top; about Y, east and west and bottom and top; about Z, south and north and east and west. The
/// four (with the identity) are a group, so the <em>canonical form</em> — the least of the four
/// images, each sorted — is one value for everything that is equal, and equality and the hash read
/// it. Compared as a sorted multiset, never as sentences, because two aprons whose pocket holes are
/// listed in different orders are the same part.
/// </para>
/// <para>
/// Like <see cref="CutSequence"/>, this wraps an <see cref="ImmutableArray{T}"/> that would
/// otherwise compare by the identity of its array.
/// </para>
/// </remarks>
internal static class JointSequence
{
    private enum HalfTurn
    {
        None,
        AboutX,
        AboutY,
        AboutZ,
    }

    /// <summary>The joinery sorted into a fixed order, as the row holds it.</summary>
    internal static ImmutableArray<JointFact> Sorted(IEnumerable<JointFact> facts)
        => [.. facts.Order(FactOrder)];

    /// <summary>The canonical form: the least, under a half-turn about any axis, of the sorted joinery.</summary>
    internal static ImmutableArray<JointFact> Canonical(ImmutableArray<JointFact> facts)
    {
        ImmutableArray<JointFact> least = Sorted(facts);
        foreach (HalfTurn turn in new[] { HalfTurn.AboutX, HalfTurn.AboutY, HalfTurn.AboutZ })
        {
            ImmutableArray<JointFact> image = Sorted(facts.Select(fact => Turned(fact, turn)));
            if (Order.Compare(image, least) < 0)
            {
                least = image;
            }
        }

        return least;
    }

    /// <summary>Whether two joinery sets are the same up to a half-turn.</summary>
    internal static bool AreEqual(ImmutableArray<JointFact> a, ImmutableArray<JointFact> b)
        => Canonical(a).SequenceEqual(Canonical(b));

    /// <summary>A hash that agrees with <see cref="AreEqual"/>.</summary>
    internal static int HashOf(ImmutableArray<JointFact> facts)
    {
        HashCode hash = default;
        foreach (JointFact fact in Canonical(facts))
        {
            hash.Add(fact);
        }

        return hash.ToHashCode();
    }

    /// <summary>The final tie-break on the cut list's order: less joinery first, then fact by fact.</summary>
    internal static IComparer<ImmutableArray<JointFact>> Order { get; } = new SequenceComparer();

    private static JointFact Turned(JointFact fact, HalfTurn turn)
        => fact with { Face = Turned(fact.Face, turn), End = Turned(fact.End, turn) };

    private static BoxFace? Turned(BoxFace? face, HalfTurn turn) => face is not { } value
        ? null
        : turn switch
        {
            HalfTurn.AboutX => value switch
            {
                BoxFace.South => BoxFace.North,
                BoxFace.North => BoxFace.South,
                BoxFace.Bottom => BoxFace.Top,
                BoxFace.Top => BoxFace.Bottom,
                _ => value,
            },
            HalfTurn.AboutY => value switch
            {
                BoxFace.East => BoxFace.West,
                BoxFace.West => BoxFace.East,
                BoxFace.Bottom => BoxFace.Top,
                BoxFace.Top => BoxFace.Bottom,
                _ => value,
            },
            _ => value switch
            {
                BoxFace.South => BoxFace.North,
                BoxFace.North => BoxFace.South,
                BoxFace.East => BoxFace.West,
                BoxFace.West => BoxFace.East,
                _ => value,
            },
        };

    /// <summary>A total order on facts: field by field, a missing value before a present one.</summary>
    internal static IComparer<JointFact> FactOrder { get; } = Comparer<JointFact>.Create(CompareFacts);

    private static int CompareFacts(JointFact a, JointFact b)
    {
        int result = ((int)a.Kind).CompareTo((int)b.Kind);
        result = result != 0 ? result : Compare(a.Face, b.Face);
        result = result != 0 ? result : Compare(a.End, b.End);
        result = result != 0 ? result : Compare(a.Dimension, b.Dimension);
        result = result != 0 ? result : Compare(a.Type, b.Type);
        result = result != 0 ? result : Compare(a.Direction, b.Direction);
        result = result != 0 ? result : a.Width.CompareTo(b.Width);
        result = result != 0 ? result : a.Depth.CompareTo(b.Depth);
        result = result != 0 ? result : a.Long.CompareTo(b.Long);
        result = result != 0 ? result : a.Offset.CompareTo(b.Offset);
        return result != 0 ? result : a.Count.CompareTo(b.Count);
    }

    private static int Compare<T>(T? a, T? b)
        where T : struct, Enum
        => (a is null ? -1 : (int)(object)a.Value).CompareTo(b is null ? -1 : (int)(object)b.Value);

    private sealed class SequenceComparer : IComparer<ImmutableArray<JointFact>>
    {
        public int Compare(ImmutableArray<JointFact> x, ImmutableArray<JointFact> y)
        {
            int shared = Math.Min(x.Length, y.Length);
            for (int i = 0; i < shared; i++)
            {
                int fact = CompareFacts(x[i], y[i]);
                if (fact != 0)
                {
                    return fact;
                }
            }

            return x.Length.CompareTo(y.Length);
        }
    }
}
