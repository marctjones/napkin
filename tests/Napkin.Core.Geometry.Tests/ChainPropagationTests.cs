namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Issue #49: a chain of three or more parts. Every case here is run in every order the
/// relationships could have been added in and with every relationship written both ways round,
/// because the answer has to be a property of the sketch rather than of how it was drawn.
/// </summary>
/// <remarks>
/// The rule these pin down is docs/design/geometry-model.md &#xA7;4.4: a side held by something
/// assigned — through however many relationships that already hold — does not move, and when a
/// chain is held by nothing, the part closest to what the request changed keeps its anchor.
/// </remarks>
public class ChainPropagationTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void TheBookcaseGrowsWestAndTheRestOfTheRowStaysWhereItIs()
    {
        // Three parts in a row, the last one fixed, resize the first: a bookcase. Before #49 this
        // was OverConstrained in all 96 orderings although a solution exists.
        int cases = 0;

        foreach (int[] order in ChainScenarios.Permutations(4))
        {
            foreach (bool[] flips in ChainScenarios.Flips(2))
            {
                ChainScenarios.Chain chain = ChainScenarios.Bookcase(order, flips);
                string because = Because(order, flips);

                Solved result = Assert.IsType<Solved>(
                    Updater.Apply(chain.Sketch, new SetParameter(chain.Width, Length.Inches(40))));

                // A grows west by the ten inches it gained; B and C do not move at all.
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[0], -10, 0, 40, 4);
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[1], 30, 0, 10, 4);
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[2], 40, 0, 10, 4);
                SketchAssert.IsConsistent(result.Sketch, because);
                Assert.True(result.Changes.Moved.SetEquals([chain.Boxes[0]]), because);
                cases++;
            }
        }

        Assert.Equal(96, cases);
    }

    [Trait("Feature", "GEO-011")]
    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    public void ALongerRowBehavesTheSameWayHoweverManyPartsAreInIt(int parts)
    {
        // Four and five parts: only the resized part moves, whatever order the row was drawn in.
        long[] anchors = [0, 30, 40, 50, 65];

        foreach (int[] order in ChainScenarios.Permutations(parts + 1))
        {
            foreach (bool[] flips in ChainScenarios.Flips(parts - 1))
            {
                ChainScenarios.Chain chain = ChainScenarios.Bookcase(order, flips, parts);
                string because = $"{parts} parts, {Because(order, flips)}";

                Solved result = Assert.IsType<Solved>(
                    Updater.Apply(chain.Sketch, new SetParameter(chain.Width, Length.Inches(40))));

                SketchAssert.BoxIs(result.Sketch, chain.Boxes[0], -10, 0, 40, 4);
                for (int i = 1; i < parts; i++)
                {
                    Box box = result.Sketch.Find<Box>(chain.Boxes[i])!;
                    Assert.True(box.Anchor.X == Length.Inches(anchors[i]), $"{because}: part {i} moved to {box.Anchor.X}");
                }

                SketchAssert.IsConsistent(result.Sketch, because);
            }
        }
    }

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void ADimensionInTheMiddleOfTheRowPushesWestAndLeavesTheAnchoredEndAlone()
    {
        // The dimension is on the middle part. It and everything west of it move; the anchored
        // east end does not.
        foreach (int[] order in ChainScenarios.Permutations(4))
        {
            foreach (bool[] flips in ChainScenarios.Flips(2))
            {
                ChainScenarios.Chain chain = ChainScenarios.Bookcase(order, flips, widthOf: 1);
                string because = Because(order, flips);

                Solved result = Assert.IsType<Solved>(
                    Updater.Apply(chain.Sketch, new SetParameter(chain.Width, Length.Inches(20))));

                SketchAssert.BoxIs(result.Sketch, chain.Boxes[0], -10, 0, 30, 4);
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[1], 20, 0, 20, 4);
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[2], 40, 0, 10, 4);
                SketchAssert.IsConsistent(result.Sketch, because);
            }
        }
    }

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void TheEqualWidthRowKeepsTheDimensionedPartsAnchorAndMovesTheRestEast()
    {
        // #49's comment: three equal-width boxes flush in a row, nothing anchored. Today's four
        // outcomes — two different "grew west" answers, this one, and OverConstrained on a
        // solvable sketch — collapse to one. This is the answer §4.4's anchor rule asks for: the
        // part whose dimension the user typed keeps its anchor, and the parts that were resized
        // only because an EqualParam passed the change on give way to it.
        foreach (int[] order in ChainScenarios.Permutations(5))
        {
            foreach (bool[] flips in ChainScenarios.Flips(4))
            {
                ChainScenarios.Chain chain = ChainScenarios.EqualChain(order, flips);
                string because = Because(order, flips);

                Solved result = Assert.IsType<Solved>(
                    Updater.Apply(chain.Sketch, new SetParameter(chain.Width, Length.Inches(20))));

                SketchAssert.BoxIs(result.Sketch, chain.Boxes[0], 0, 0, 20, 4);
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[1], 20, 0, 20, 4);
                SketchAssert.BoxIs(result.Sketch, chain.Boxes[2], 40, 0, 20, 4);
                SketchAssert.IsConsistent(result.Sketch, because);
            }
        }
    }

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void ARowAnchoredAtBothEndsIsAContradictionThatNamesBothAnchorsAndBothFlushes()
    {
        // Widening the first part cannot work when both ends are held: this one really is
        // over-constrained, and the report has to name the whole path, not just the near end.
        foreach (int[] order in ChainScenarios.Permutations(5))
        {
            foreach (bool[] flips in ChainScenarios.Flips(2))
            {
                ChainScenarios.Chain chain = ChainScenarios.Bookcase(order, flips, anchorFirst: true);
                string because = Because(order, flips);

                OverConstrained result = Assert.IsType<OverConstrained>(
                    Updater.Apply(chain.Sketch, new SetParameter(chain.Width, Length.Inches(40))));

                Assert.Equal(ConflictKind.Contradictory, result.Conflict.Kind);
                foreach (RelationshipId id in chain.Anchors.Concat(chain.Flushes).Append(chain.Width))
                {
                    Assert.True(result.Conflict.Relationships.Contains(id), $"{because}: {id} was not named");
                }

                foreach (EntityId id in chain.Boxes)
                {
                    Assert.True(result.Conflict.Entities.Contains(id), $"{because}: {id} was not named");
                }

                Assert.NotEmpty(result.Conflict.Summary);
                Assert.Equal(2, result.Conflict.Derivations.Count);
            }
        }
    }

    [Trait("Feature", "GEO-011")]
    [Fact]
    public void AnAnchorThatHoldsOnePartAlongOneAxisDoesNotBlockTheRowAlongTheOther()
    {
        // A and B are flush along X; B sits on C, which is anchored, along Y. Pinning is per
        // scalar, so C holds the row's Y and leaves its X free: widening A still works, and
        // nothing moves in Y.
        foreach (int[] order in ChainScenarios.Permutations(4))
        {
            SketchBuilder builder = new();
            EntityId a = builder.AddBox(0, 0, 30, 4);
            EntityId b = builder.AddBox(30, 0, 10, 4);
            EntityId c = builder.AddBox(30, 4, 10, 6);

            RelationshipId width = default;
            List<Action> adds =
            [
                () => builder.Flush(a, BoxEdge.East, b, BoxEdge.West),
                () => builder.Flush(b, BoxEdge.North, c, BoxEdge.South),
                () => builder.Anchor(c),
                () => width = builder.WidthIs(a, Length.Inches(30)),
            ];

            foreach (int index in order)
            {
                adds[index]();
            }

            string because = Because(order, []);
            Solved result = Assert.IsType<Solved>(
                Updater.Apply(builder.Sketch, new SetParameter(width, Length.Inches(40))));

            SketchAssert.BoxIs(result.Sketch, a, 0, 0, 40, 4);
            SketchAssert.BoxIs(result.Sketch, b, 40, 0, 10, 4);
            SketchAssert.BoxIs(result.Sketch, c, 30, 4, 10, 6);
            SketchAssert.IsConsistent(result.Sketch, because);
        }
    }

    [Trait("Feature", "GEO-010")]
    [Fact]
    public void TheRowIsIdempotentAndUndoesNothingOnTheSecondApplication()
    {
        // The chain rules must not drift: applying the same dimension again moves nothing.
        ChainScenarios.Chain chain = ChainScenarios.Bookcase([0, 1, 2, 3], [false, false]);
        SetParameter request = new(chain.Width, Length.Inches(40));

        Solved once = Assert.IsType<Solved>(Updater.Apply(chain.Sketch, request));
        Solved twice = Assert.IsType<Solved>(Updater.Apply(once.Sketch, request));

        Assert.True(twice.Changes.IsEmpty);
        Assert.True(once.Sketch == twice.Sketch);
    }

    private static string Because(int[] order, bool[] flips)
        => $"order [{string.Join(", ", order)}], flips [{string.Join(", ", flips)}]";
}
