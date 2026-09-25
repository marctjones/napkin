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
/// <strong>Finished sizes include joinery allowances and not saw kerf.</strong> A part inserted
/// into a groove or a rabbet is that much longer than it is drawn (<c>docs/design/joinery-and-fasteners.md</c>
/// &#xA7;6.1), so its row says the finished size; a kerf is a real 1/8&#x2033; per cut and napkin does not
/// know the blade. The list says so on the table and in the CSV header rather than being quietly
/// optimistic (§1.3).
/// </para>
/// </remarks>
public static class CutList
{
    /// <summary>
    /// What a cut list is, said on the table and in the exported file so that nobody cuts to these
    /// numbers believing they include anything they do not: the finished sizes, joinery allowances
    /// in, saw kerf not (#138).
    /// </summary>
    public const string BeforeKerfAndJoinery = "Cut list: finished sizes: joinery allowances included; before saw kerf (#138).";

    /// <summary>
    /// The line under the table when any row is rough (<c>docs/design/sketch-mode.md</c> &#xA7;5):
    /// "3 rows are rough — sizes as drawn, stock not chosen", or null when none is.
    /// </summary>
    /// <param name="rows">The cut list's rows.</param>
    public static string? RoughFooter(IEnumerable<CutListRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int rough = rows.Count(row => row.Rough);
        return rough switch
        {
            0 => null,
            1 => "1 row is rough — sizes as drawn, stock not chosen",
            _ => $"{rough.ToString(System.Globalization.CultureInfo.InvariantCulture)} rows are rough — sizes as drawn, stock not chosen",
        };
    }

    /// <summary>
    /// What the cut list window says in place of the table when a design has nothing to cut at all
    /// (#179) — distinct from the window's "nothing is a part yet" note, which is for a design
    /// that has boxes but none of them are parts.
    /// </summary>
    public const string NothingToCut = "This design has nothing in it to cut.";

    /// <summary>The cut list in a phrase after the design's name: "4 rows, 9 pieces to cut", or "nothing to cut".</summary>
    /// <param name="rows">How many rows.</param>
    /// <param name="pieces">How many pieces, over all rows.</param>
    public static string Headline(int rows, int pieces) => rows == 0
        ? "nothing to cut"
        : $"{rows} {(rows == 1 ? "row" : "rows")}, {pieces} {(pieces == 1 ? "piece" : "pieces")} to cut";

    /// <summary>
    /// The cut list for a design.
    /// </summary>
    /// <param name="sketch">The design. Boxes whose <see cref="Box.Part"/> is null are not pieces anybody cuts, nor are parts that are not <see cref="Phase.New"/>.</param>
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
            // Only what is New is cut (renovation-sketches §6.2): an existing part is already there,
            // a demolished one is counted under Demolition.
            if (box.Part is not { } part || box.Phase != Phase.New)
            {
                continue;
            }

            // Steps 2 and 3 — name the three dimensions from planAxes, and resolve the stock. The
            // drawn size is the visible, shoulder-to-shoulder one; finished adds what the part goes
            // into a groove or a rabbet by (§6.1), and the joinery joins the key.
            FinishedSize drawn = part.SizeOn(box);
            ImmutableArray<JointFact> joinery = JointDescription.FactsOf(sketch, box);
            FinishedSize size = new(
                drawn.Length + JointDescription.AllowanceOn(joinery, PartDimension.Length),
                drawn.Width + JointDescription.AllowanceOn(joinery, PartDimension.Width),
                drawn.Thickness + JointDescription.AllowanceOn(joinery, PartDimension.Thickness));
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
                    cuts,
                    joinery),
                Material: stock?.Name ?? part.Stock ?? string.Empty,
                Unresolved: part.Stock is not null && stock is null,
                Stock: stock,
                PlanAxes: part.PlanAxes,
                Drawn: drawn,
                Joinery: joinery,
                Unsatisfied: JointDescription.Unsatisfied(sketch, box),
                Rough: part.Rough));
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
                [.. members.Select(member => member.Id)])
            {
                Species = group.Key.Species,
                Drawn = members[0].Drawn,
                Joinery = members[0].Joinery,
                JointsUnsatisfied = members.Any(member => member.Unsatisfied),

                // Not in the key (docs/design/sketch-mode.md §5): a rough leg and a firm one of the
                // same sizes are one row of two, and the row is rough because one of them is.
                Rough = members.Any(member => member.Rough),
            });
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
                .ThenBy(row => row.Species, StringComparer.Ordinal)
                .ThenBy(row => row.Cuts, CutSequence.Order)
                .ThenBy(row => row.Joinery, JointSequence.Order),
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
        PlanAxes PlanAxes,
        FinishedSize Drawn,
        ImmutableArray<JointFact> Joinery,
        bool Unsatisfied,
        bool Rough);

    /// <summary>
    /// What makes two parts one row: the same three finished dimensions out of the same stock in
    /// the same species, with the same cuts at the same sites and the same joinery. The name is not
    /// in it — four boxes called "Leg, south-west" through "Leg, north-east" are one row of four.
    /// </summary>
    /// <remarks>
    /// Four legs chamfered on the same corner are one row of four; two legs chamfered on
    /// mirror-image corners are two rows, each saying which corner, because they are not the same
    /// piece (§4.3). A rectangle and the same rectangle with a cut are two rows for the same
    /// reason. Joinery is compared up to a half-turn of the part about any of its axes
    /// (<see cref="JointSequence"/>): two side aprons drilled from opposite faces at opposite ends
    /// are one row, and a left drawer side and a right one are two.
    /// </remarks>
    internal readonly record struct GroupKey(
        Length Length,
        Length Width,
        Length Thickness,
        string Stock,
        string Species,
        ImmutableArray<Cut> Cuts,
        ImmutableArray<JointFact> Joinery)
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
               & CutSequence.AreEqual(Cuts, other.Cuts)
               & JointSequence.AreEqual(Joinery, other.Joinery);

        /// <inheritdoc/>
        public override int GetHashCode()
            => HashCode.Combine(
                Length,
                Width,
                Thickness,
                StringComparer.Ordinal.GetHashCode(Stock),
                StringComparer.Ordinal.GetHashCode(Species),
                CutSequence.HashOf(Cuts),
                JointSequence.HashOf(Joinery));
    }
}
