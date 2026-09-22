namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The chains of issue #49, built in every order the sketch could have been drawn in.
/// </summary>
/// <remarks>
/// <para>
/// A chain's answer must not depend on the order the relationships were added in (which is the
/// order <see cref="SketchBuilder"/> hands out <see cref="RelationshipId"/>s, and therefore the
/// order the propagator iterates them in), nor on which way round each relationship was written.
/// So every scenario here is a function of a permutation and a set of argument-order flips, and
/// the tests run all of them and demand one answer.
/// </para>
/// </remarks>
internal static class ChainScenarios
{
    /// <summary>A chain scenario: the sketch, the boxes west to east, and the ids that matter.</summary>
    internal sealed record Chain(
        Sketch Sketch,
        IReadOnlyList<EntityId> Boxes,
        IReadOnlyList<RelationshipId> Flushes,
        RelationshipId Width,
        IReadOnlyList<RelationshipId> Anchors,
        IReadOnlyList<RelationshipId> EqualParams);

    /// <summary>Every permutation of <paramref name="count"/> indices, in a stable order.</summary>
    internal static IEnumerable<int[]> Permutations(int count)
    {
        int[] items = [.. Enumerable.Range(0, count)];
        return Permute(items, 0);

        static IEnumerable<int[]> Permute(int[] items, int from)
        {
            if (from == items.Length - 1)
            {
                yield return [.. items];
                yield break;
            }

            for (int i = from; i < items.Length; i++)
            {
                (items[from], items[i]) = (items[i], items[from]);
                foreach (int[] permutation in Permute(items, from + 1))
                {
                    yield return permutation;
                }

                (items[from], items[i]) = (items[i], items[from]);
            }
        }
    }

    /// <summary>The flips to try for a chain with <paramref name="count"/> flippable relationships.</summary>
    internal static IEnumerable<bool[]> Flips(int count)
    {
        for (int mask = 0; mask < 1 << count; mask++)
        {
            int captured = mask;
            yield return [.. Enumerable.Range(0, count).Select(bit => (captured & (1 << bit)) != 0)];
        }
    }

    /// <summary>
    /// The bookcase of #49: parts in a row, each flush with the next, the last one anchored, and a
    /// driving dimension on the first one's width.
    /// </summary>
    /// <param name="order">The order the relationships are added in: the flushes west to east, then the anchor, then the width.</param>
    /// <param name="flips">One flag per flush: true writes it as (east box's west edge, west box's east edge).</param>
    /// <param name="parts">How many parts are in the row.</param>
    /// <param name="anchorLast">Whether the last part is anchored.</param>
    /// <param name="anchorFirst">Whether the first part is anchored too, which makes the chain unsatisfiable.</param>
    /// <param name="widthOf">Which part carries the driving width dimension.</param>
    internal static Chain Bookcase(
        int[] order,
        bool[] flips,
        int parts = 3,
        bool anchorLast = true,
        bool anchorFirst = false,
        int widthOf = 0)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(flips);

        SketchBuilder builder = new();
        List<EntityId> boxes = [];
        long x = 0;
        for (int i = 0; i < parts; i++)
        {
            boxes.Add(builder.AddBox(x, 0, Widths[i], 4));
            x += Widths[i];
        }

        RelationshipId[] flushes = new RelationshipId[parts - 1];
        List<RelationshipId> anchors = [];
        RelationshipId width = default;

        List<Action> adds = [];
        for (int i = 0; i < parts - 1; i++)
        {
            int index = i;
            adds.Add(() => flushes[index] = flips[index]
                ? builder.Flush(boxes[index + 1], BoxEdge.West, boxes[index], BoxEdge.East)
                : builder.Flush(boxes[index], BoxEdge.East, boxes[index + 1], BoxEdge.West));
        }

        adds.Add(() =>
        {
            if (anchorLast)
            {
                anchors.Add(builder.Anchor(boxes[^1]));
            }
        });

        adds.Add(() => width = builder.WidthIs(boxes[widthOf], Length.Inches(Widths[widthOf])));

        if (anchorFirst)
        {
            adds.Add(() => anchors.Add(builder.Anchor(boxes[0])));
        }

        foreach (int index in order)
        {
            adds[index]();
        }

        return new Chain(builder.Sketch, boxes, flushes, width, anchors, []);
    }

    /// <summary>
    /// The equal-width chain of #49's comment: three boxes flush in a row, all widths tied
    /// together, and a driving dimension on the first one's width. Nothing is anchored.
    /// </summary>
    /// <param name="order">The order the relationships are added in: two flushes, two EqualParams, the width.</param>
    /// <param name="flips">
    /// One flag per flush and per <see cref="EqualParam"/>: true writes the relationship the other
    /// way round.
    /// </param>
    internal static Chain EqualChain(int[] order, bool[] flips)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(flips);

        SketchBuilder builder = new();
        EntityId a = builder.AddBox(0, 0, 10, 4);
        EntityId b = builder.AddBox(10, 0, 10, 4);
        EntityId c = builder.AddBox(20, 0, 10, 4);

        RelationshipId[] flushes = new RelationshipId[2];
        RelationshipId[] equals = new RelationshipId[2];
        RelationshipId width = default;

        List<Action> adds =
        [
            () => flushes[0] = flips[0] ? builder.Flush(b, BoxEdge.West, a, BoxEdge.East) : builder.Flush(a, BoxEdge.East, b, BoxEdge.West),
            () => flushes[1] = flips[1] ? builder.Flush(c, BoxEdge.West, b, BoxEdge.East) : builder.Flush(b, BoxEdge.East, c, BoxEdge.West),
            () => equals[0] = flips[2] ? builder.EqualWidths(b, a) : builder.EqualWidths(a, b),
            () => equals[1] = flips[3] ? builder.EqualWidths(c, b) : builder.EqualWidths(b, c),
            () => width = builder.WidthIs(a, Length.Inches(10)),
        ];

        foreach (int index in order)
        {
            adds[index]();
        }

        return new Chain(builder.Sketch, [a, b, c], flushes, width, [], equals);
    }

    /// <summary>The widths of the parts in a row, west to east, in inches.</summary>
    private static readonly long[] Widths = [30, 10, 10, 15, 7];
}
