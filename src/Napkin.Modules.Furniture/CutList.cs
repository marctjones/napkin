using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>
/// The cut list: every piece a design says to cut, grouped, ordered and sized.
/// </summary>
/// <remarks>
/// <para>
/// <strong>A pure function of a sketch and a library.</strong> No state, no I/O and no UI, so a
/// test calls it directly and the table on screen and the CSV export are two renderings of one
/// list of rows (<c>docs/design/parts-and-cut-list.md</c> §3).
/// </para>
/// <para>
/// <strong>No saw kerf and no joinery allowance.</strong> A kerf is a real 1/8&#x2033; per cut and
/// napkin does not know the blade; a tenon, a dado and a mitre change a finished length and napkin
/// does not know about them either. The list says so on the table and in the CSV header rather
/// than being quietly optimistic (§1.3).
/// </para>
/// </remarks>
public static class CutList
{
    /// <summary>
    /// What a cut list is before, said on the table and in the exported file so that nobody cuts
    /// to these numbers believing they include anything they do not.
    /// </summary>
    public const string BeforeKerfAndJoinery = "Cut list: finished sizes before saw kerf and joinery allowance.";

    /// <summary>
    /// The cut list for a design.
    /// </summary>
    /// <param name="sketch">The design. Boxes whose <see cref="Box.Part"/> is null are not pieces anybody cuts.</param>
    /// <param name="library">
    /// The materials library a part's stock name is resolved through. A name it does not carry
    /// still produces a row, marked <see cref="CutListRow.Unresolved"/>.
    /// </param>
    public static ImmutableArray<CutListRow> Of(Sketch sketch, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(library);

        // Step 1 — collect, in ascending id order, so that the first member of a group is a
        // property of the design rather than of a dictionary's enumeration order.
        List<Piece> pieces = [];
        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            if (box.Part is not { } part)
            {
                continue;
            }

            // Steps 2 and 3 — name the three dimensions from planAxes, and resolve the stock.
            FinishedSize size = part.SizeOn(box);
            StockItem? stock = part.Stock is not null && library.TryFind(part.Stock, out StockItem item)
                ? item
                : null;

            // The cuts join the key by value, in the site order Box.Cuts holds them in, so that
            // the same cuts at the same sites group and nothing else does (§4.3).
            ImmutableArray<Cut> cuts = [.. box.Cuts];

            pieces.Add(new Piece(
                box.Id,
                box.Name,
                part.Quantity,
                size,
                Key: new GroupKey(
                    size.Length,
                    size.Width,
                    size.Thickness,
                    NominalName.Normalize(part.Stock),
                    part.Species ?? string.Empty,
                    cuts),
                Material: stock?.Name ?? part.Stock ?? string.Empty,
                Unresolved: part.Stock is not null && stock is null,
                Stock: stock,
                PlanAxes: part.PlanAxes));
        }

        // Step 4 — group. Exact integer equality on all three dimensions, with no tolerance: two
        // parts typed to the same number are equal on the grid, and a tolerance would quietly
        // merge a 10" part with a 10 1/64" one. The name is deliberately not in the key.
        List<CutListRow> rows = [];
        foreach (IGrouping<GroupKey, Piece> group in pieces.GroupBy(piece => piece.Key))
        {
            Piece[] members = [.. group];

            rows.Add(new CutListRow(
                Label(members),
                members.Sum(member => member.Quantity),
                group.Key.Length,
                group.Key.Width,
                group.Key.Thickness,
                members[0].Material,
                members[0].Unresolved,
                members[0].Stock,
                group.Key.Cuts,
                members[0].PlanAxes,
                [.. members.Select(member => member.Id)]));
        }

        // Step 5 — order: largest piece first, which is the order a person cuts in. The label and
        // then the material and species break a tie between rows of identical size, and the cuts
        // break the last one — a plain rectangle before the same rectangle with cuts — so that the
        // order is total and the same on every machine whatever order the boxes were enumerated in.
        return
        [
            .. rows
                .OrderByDescending(row => row.Length)
                .ThenByDescending(row => row.Width)
                .ThenByDescending(row => row.Thickness)
                .ThenBy(row => row.Label, StringComparer.Ordinal)
                .ThenBy(row => row.Material, StringComparer.Ordinal)
                .ThenBy(row => row.Cuts, CutSequence.Order),
        ];
    }

    /// <summary>
    /// A row's label: the longest common prefix of its members' names, cut back to the last word
    /// boundary and stripped of trailing punctuation and whitespace.
    /// </summary>
    /// <remarks>
    /// "Leg, south-west" … "Leg, north-east" give <c>Leg</c>; "Apron, long, south" and
    /// "Apron, long, north" give <c>Apron, long</c>. With no usable common prefix — including a
    /// prefix that stops in the middle of a word — the label is the first member's own name in id
    /// order, which is never worse than a truncated word.
    /// </remarks>
    internal static string Label(IReadOnlyList<Piece> members)
    {
        string first = members[0].Name;
        if (members.All(member => string.Equals(member.Name, first, StringComparison.Ordinal)))
        {
            return first;
        }

        string prefix = CommonPrefix(members);
        int lastBoundary = LastWordBoundary(prefix);
        string cut = lastBoundary < 0 ? string.Empty : prefix[..lastBoundary];

        string label = cut.TrimEnd(TrailingTrim);
        return label.Length > 0 ? label : first;
    }

    private static readonly char[] TrailingTrim =
        [' ', '\t', ',', ';', ':', '-', '–', '—', '.', '/', '(', '[', '{', '"', '\''];

    private static int LastWordBoundary(string prefix)
    {
        for (int i = prefix.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(prefix[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string CommonPrefix(IReadOnlyList<Piece> members)
    {
        string prefix = members[0].Name;
        foreach (Piece member in members)
        {
            int shared = 0;
            int limit = Math.Min(prefix.Length, member.Name.Length);
            while (shared < limit && prefix[shared] == member.Name[shared])
            {
                shared++;
            }

            prefix = prefix[..shared];
        }

        return prefix;
    }

    /// <summary>One box's contribution to the list, before grouping.</summary>
    internal readonly record struct Piece(
        EntityId Id,
        string Name,
        int Quantity,
        FinishedSize Size,
        GroupKey Key,
        string Material,
        bool Unresolved,
        StockItem? Stock,
        PlanAxes PlanAxes);

    /// <summary>
    /// What makes two parts one row: the same three finished dimensions out of the same stock in
    /// the same species, with the same cuts at the same sites. The name is not in it — four boxes
    /// called "Leg, south-west" through "Leg, north-east" are one row of four.
    /// </summary>
    /// <remarks>
    /// Four legs chamfered on the same corner are one row of four; two legs chamfered on
    /// mirror-image corners are two rows, each saying which corner, because they are not the same
    /// piece (§4.3). A rectangle and the same rectangle with a cut are two rows for the same
    /// reason.
    /// </remarks>
    internal readonly record struct GroupKey(
        Length Length,
        Length Width,
        Length Thickness,
        string Stock,
        string Species,
        ImmutableArray<Cut> Cuts)
    {
        /// <summary>
        /// Equality by value, with the cuts compared as a sequence.
        /// </summary>
        /// <remarks>
        /// Written out because the synthesised equality of a <c>record struct</c> compares an
        /// <see cref="ImmutableArray{T}"/> by the identity of the array it wraps, which would mean
        /// two shaped parts never grouped however identical they were — the trap
        /// <c>docs/design/shaped-parts-model.md</c> §4.2 names for this key in particular.
        /// </remarks>
        /// <param name="other">The key to compare with.</param>
        /// <devdoc>
        /// Every rung is compared, with <c>&amp;</c> rather than <c>&amp;&amp;</c>: the values are
        /// three integers, two short strings and a handful of cuts, so there is nothing here worth
        /// skipping, and a comparison with no short-circuit in it is one with no path through it
        /// that a test cannot reach. Two keys are only ever compared when their hashes agree.
        /// </devdoc>
        public bool Equals(GroupKey other)
            => Length == other.Length
               & Width == other.Width
               & Thickness == other.Thickness
               & string.Equals(Stock, other.Stock, StringComparison.Ordinal)
               & string.Equals(Species, other.Species, StringComparison.Ordinal)
               & CutSequence.AreEqual(Cuts, other.Cuts);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                Length,
                Width,
                Thickness,
                StringComparer.Ordinal.GetHashCode(Stock),
                StringComparer.Ordinal.GetHashCode(Species),
                CutSequence.HashOf(Cuts));
    }
}
