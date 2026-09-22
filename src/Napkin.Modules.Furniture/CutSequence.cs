using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Furniture;

/// <summary>
/// Two lists of cuts, compared: for equality, so that a group key can hold one, and for order, so
/// that two rows that differ only in their cuts come out the same way round on every machine
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;4.3).
/// </summary>
/// <remarks>
/// <see cref="ImmutableArray{T}"/> compares by the identity of the array it wraps, so anything that
/// holds one and wants value semantics has to say so. A <c>record struct</c> group key with a cut
/// list in it would silently never group two shaped parts, which is the trap &#xA7;4.2 names.
/// </remarks>
internal static class CutSequence
{
    /// <summary>Whether two lists hold the same cuts, in the same order.</summary>
    internal static bool AreEqual(ImmutableArray<Cut> a, ImmutableArray<Cut> b) => a.SequenceEqual(b);

    /// <summary>A hash that agrees with <see cref="AreEqual"/>.</summary>
    internal static int HashOf(ImmutableArray<Cut> cuts)
    {
        HashCode hash = default;
        foreach (Cut cut in cuts)
        {
            hash.Add(cut);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// The final tie-break on the cut list's order: a plain rectangle before the same rectangle
    /// with cuts, and two cut blanks by site, then kind, then the values themselves.
    /// </summary>
    internal static IComparer<ImmutableArray<Cut>> Order { get; } = new Comparer();

    private sealed class Comparer : IComparer<ImmutableArray<Cut>>
    {
        public int Compare(ImmutableArray<Cut> x, ImmutableArray<Cut> y)
        {
            int shared = Math.Min(x.Length, y.Length);
            for (int i = 0; i < shared; i++)
            {
                int cut = CompareCuts(x[i], y[i]);
                if (cut != 0)
                {
                    return cut;
                }
            }

            // Everything they share is the same, so the shorter list — which is the plainer
            // blank — comes first.
            return x.Length.CompareTo(y.Length);
        }

        /// <remarks>
        /// Same site and same kind leaves the values, and the two kinds the switch names are the
        /// two that are not the curved edge — the hierarchy has three and no more.
        /// </remarks>
        private static int CompareCuts(Cut a, Cut b)
        {
            int site = a.Site.CompareTo(b.Site);
            if (site != 0)
            {
                return site;
            }

            int kind = Kind(a).CompareTo(Kind(b));
            if (kind != 0)
            {
                return kind;
            }

            return (a, b) switch
            {
                (CornerCut first, CornerCut second) => Then(
                    first.AlongX.Units.CompareTo(second.AlongX.Units),
                    first.AlongY.Units.CompareTo(second.AlongY.Units)),
                (RoundedCorner first, RoundedCorner second) =>
                    first.Radius.Units.CompareTo(second.Radius.Units),
                _ => Then(
                    ((int)((CurvedEdge)a).Bow).CompareTo((int)((CurvedEdge)b).Bow),
                    ((CurvedEdge)a).Depth.Units.CompareTo(((CurvedEdge)b).Depth.Units)),
            };
        }

        private static int Then(int first, int second) => first != 0 ? first : second;

        /// <summary>A fixed order for the three kinds, so that the comparison is total.</summary>
        private static int Kind(Cut cut) => cut switch
        {
            CornerCut => 0,
            RoundedCorner => 1,
            _ => 2,
        };
    }
}
