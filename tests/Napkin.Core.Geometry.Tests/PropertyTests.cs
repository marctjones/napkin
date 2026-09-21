using System.Collections.Immutable;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Properties P1 to P8 and P10 of docs/design/geometry-model.md &#xA7;7.2. P9 (serialization)
/// waits for #6.
/// </summary>
/// <remarks>
/// Seeded loops rather than a property-testing package: <c>Napkin.Core.Geometry</c> takes no
/// third-party dependency and neither does its test project, so nothing new has to pass the #2
/// license gate. Every assertion prints the seed and the iteration, so any counterexample is
/// reproducible by running the one theory case.
/// </remarks>
public class PropertyTests
{
    private const int Iterations = 60;

    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    /// <summary>The seeds every property runs against. A failure names the one to rerun.</summary>
    public static IEnumerable<object[]> Seeds => Enumerable.Range(1, 8).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P1_ApplyNeverProducesAnInconsistentSketch(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            Request request = generator.NextRequest(sketch);
            string because = Because(seed, iteration, request);

            // The generator's own output has to hold together, or the property proves nothing.
            SketchAssert.IsConsistent(sketch, because);

            Sketch snapshot = generator.Shuffle(sketch);
            UpdateResult result = Updater.Apply(sketch, request);

            switch (result)
            {
                case Succeeded succeeded:
                    ValidationResult validation = succeeded.Sketch.Validate();
                    Assert.True(validation.IsValid, $"{because}{Environment.NewLine}{validation}");

                    CheckReport check = RelationshipChecker.Check(succeeded.Sketch);
                    Assert.True(check.AllHold, $"{because}{Environment.NewLine}{check}");
                    break;

                case OverConstrained or Rejected:
                    break;

                default:
                    Assert.Fail($"{because}: unexpected result {result.GetType().Name}");
                    break;
            }

            // Whatever happened, the sketch that went in is the sketch that is still here.
            Assert.True(snapshot == sketch, $"{because}: Apply mutated the sketch it was given");

            // The direct updater does no degree-of-freedom analysis, so a success is always Solved.
            Assert.IsNotType<UnderConstrained>(result);
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P2_ASetParameterThatSucceedsHoldsExactly(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            if (generator.NextSetParameter(sketch) is not { } request)
            {
                continue;
            }

            string because = Because(seed, iteration, request);
            if (Updater.Apply(sketch, request) is not Succeeded succeeded)
            {
                continue;
            }

            switch (succeeded.Sketch.Find(request.Driving))
            {
                case ParamValue paramValue:
                    Assert.True(
                        succeeded.Sketch.ValueOf(paramValue.Param) == request.Value,
                        $"{because}: the size is {succeeded.Sketch.ValueOf(paramValue.Param)}, not {request.Value}");
                    break;

                case AxisDistance distance:
                    Length actual = succeeded.Sketch.PointOf(distance.To).Component(distance.Axis)
                                    - succeeded.Sketch.PointOf(distance.From).Component(distance.Axis);
                    Assert.True(actual == request.Value, $"{because}: the distance is {actual}, not {request.Value}");
                    break;

                default:
                    Assert.Fail($"{because}: the driving relationship went missing");
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P3_ApplyIsPureAndDeterministicWhateverOrderTheDictionariesWereBuiltIn(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            Request request = generator.NextRequest(sketch);
            string because = Because(seed, iteration, request);

            UpdateResult first = Updater.Apply(sketch, request);
            UpdateResult second = Updater.Apply(sketch, request);
            AssertSameResult(first, second, $"{because}: two calls disagreed");

            // Iterating relationships by id and never by dictionary order is what makes this true.
            UpdateResult shuffled = Updater.Apply(generator.Shuffle(sketch), request);
            AssertSameResult(first, shuffled, $"{because}: the insertion order changed the answer");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P4_EntitiesNotConnectedToTheRequestAreUntouched(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            Request request = generator.NextRequest(sketch);
            if (TargetsOf(sketch, request) is not { } targets)
            {
                continue;
            }

            string because = Because(seed, iteration, request);
            if (Updater.Apply(sketch, request) is not Succeeded succeeded)
            {
                continue;
            }

            HashSet<EntityId> connected = ConnectedComponent(sketch, targets);
            foreach (KeyValuePair<EntityId, Entity> entry in sketch.Entities)
            {
                if (connected.Contains(entry.Key))
                {
                    continue;
                }

                Assert.True(
                    ReferenceEquals(entry.Value, succeeded.Sketch.Entities[entry.Key]),
                    $"{because}: {entry.Key} is not connected to the request but changed");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P5_ApplyingTheSameSetParameterTwiceChangesNothingTheSecondTime(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            if (generator.NextSetParameter(sketch) is not { } request
                || Updater.Apply(sketch, request) is not Succeeded once)
            {
                continue;
            }

            string because = Because(seed, iteration, request);
            Succeeded twice = Assert.IsType<Solved>(Updater.Apply(once.Sketch, request));

            Assert.True(twice.Changes.IsEmpty, $"{because}: the second application changed something");
            Assert.True(once.Sketch == twice.Sketch, $"{because}: the second application moved the geometry");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P6_ADragAppliesEachComponentInFullOrNotAtAll(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            Drag request = generator.NextDrag(sketch);
            string because = Because(seed, iteration, request);

            // Dragging is never OverConstrained by design: a drag is a question, not a demand.
            Solved result = Assert.IsType<Solved>(Updater.Apply(sketch, request));
            Assert.True(result.Changes.AppliedDelta.HasValue, $"{because}: a drag must report what it applied");
            Vector2 applied = result.Changes.AppliedDelta!.Value;

            Assert.True(
                applied.Dx == request.Delta.Dx || applied.Dx == Length.Zero,
                $"{because}: X was partly applied, as {applied.Dx}");
            Assert.True(
                applied.Dy == request.Delta.Dy || applied.Dy == Length.Zero,
                $"{because}: Y was partly applied, as {applied.Dy}");

            Assert.True(
                PositionOf(result.Sketch, request.Id) - PositionOf(sketch, request.Id) == applied,
                $"{because}: the entity did not move by the delta that was reported");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P7_EveryConflictReportNamesSomethingTheUserCanRemove(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextSketch();
            Request request = generator.NextRequest(sketch);
            if (Updater.Apply(sketch, request) is not OverConstrained conflict)
            {
                continue;
            }

            string because = Because(seed, iteration, request);
            RelationshipId? own = OwnRelationshipOf(request);

            Assert.NotEmpty(conflict.Conflict.Relationships);
            Assert.NotEmpty(conflict.Conflict.Summary);
            Assert.Equal(2, conflict.Conflict.Derivations.Count);

            foreach (RelationshipId id in conflict.Conflict.Relationships)
            {
                Assert.True(
                    sketch.Find(id) is not null || id == own,
                    $"{because}: the report names relationship {id}, which is not in the sketch");
            }

            foreach (EntityId id in conflict.Conflict.Entities)
            {
                Assert.True(sketch.Find(id) is not null, $"{because}: the report names a missing entity {id}");
            }

            // Removing what the report names has to make progress, and repeating it has to end in
            // a request that goes through. See docs/design/geometry-model.md §10 for why this is
            // stated as a loop rather than as one removal.
            Sketch reduced = sketch;
            bool resolved = false;
            for (int round = 0; round < 8 && !resolved; round++)
            {
                UpdateResult attempt = Updater.Apply(reduced, request);
                if (attempt is not OverConstrained again)
                {
                    Assert.True(
                        attempt is Succeeded,
                        $"{because}: removing what the report named left {attempt.GetType().Name}");
                    resolved = true;
                    break;
                }

                int before = reduced.Relationships.Count;
                foreach (RelationshipId id in again.Conflict.Relationships)
                {
                    // Never remove the relationship the request is about: without it there is
                    // nothing left to ask for.
                    if (id != own && !IsRequestTarget(request, id))
                    {
                        reduced = reduced.WithoutRelationship(id);
                    }
                }

                Assert.True(
                    reduced.Relationships.Count < before,
                    $"{because}: round {round} named nothing that could be removed");
            }

            Assert.True(resolved, $"{because}: removing what the reports named never resolved the conflict");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P8_LengthAlgebraHolds(int seed)
    {
        Random random = new(seed);

        for (int iteration = 0; iteration < 500; iteration++)
        {
            Length a = new(random.NextInt64(-1L << 40, 1L << 40));
            Length b = new(random.NextInt64(-1L << 40, 1L << 40));
            long n = random.NextInt64(1, 1000);
            long d = random.NextInt64(1, 1000);
            string because = $"seed {seed}, iteration {iteration}, a = {a.Units}, b = {b.Units}, n = {n}, d = {d}";

            Assert.True(a + b - b == a, because);
            Assert.True((a * n).TryDivideExact(n, out Length back) && back == a, because);

            // Scale is within half a unit of the real quotient: 2*|scaled*d - a*n| <= d.
            Length scaled = a.Scale(n, d, Rounding.HalfToEven);
            Int128 error = (Int128)scaled.Units * d - (Int128)a.Units * n;
            Assert.True(2 * (error < 0 ? -error : error) <= d, $"{because}: Scale was out by {error}/{d} units");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void P10_EachSketchOnTheUndoStackIsStillExactlyWhatItWas(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < 20; iteration++)
        {
            Sketch sketch = generator.NextSketch();

            // The stack an undo would pop, beside an independently built copy of each value.
            List<(Sketch Sketch, Sketch Copy)> stack = [(sketch, generator.Shuffle(sketch))];

            for (int step = 0; step < 6; step++)
            {
                Request request = generator.NextRequest(stack[^1].Sketch);
                if (Updater.Apply(stack[^1].Sketch, request) is not Succeeded succeeded)
                {
                    continue;
                }

                stack.Add((succeeded.Sketch, generator.Shuffle(succeeded.Sketch)));
            }

            for (int i = 0; i < stack.Count; i++)
            {
                Assert.True(
                    stack[i].Sketch == stack[i].Copy,
                    $"seed {seed}, iteration {iteration}, undo step {i}: an earlier sketch changed under it");
            }

            // Structural sharing: an entity an update did not change is the same object
            // afterwards, so undo maps selection and canvas visuals straight across.
            for (int i = 1; i < stack.Count; i++)
            {
                foreach (KeyValuePair<EntityId, Entity> entry in stack[i - 1].Sketch.Entities)
                {
                    if (!stack[i].Sketch.Entities.TryGetValue(entry.Key, out Entity? after)
                        || !entry.Value.Equals(after))
                    {
                        continue;
                    }

                    Assert.True(
                        ReferenceEquals(entry.Value, after),
                        $"seed {seed}, iteration {iteration}, step {i}: {entry.Key} was copied although it did not change");
                }
            }
        }
    }

    [Fact]
    public void TheGeneratorReachesEveryOutcomeThesePropertiesRelyOn()
    {
        // Without this, a generator that only ever produced easy sketches would make P1, P6 and
        // P7 pass by never reaching the cases they are about.
        int succeeded = 0;
        int overConstrained = 0;
        int rejected = 0;
        int dragsBlocked = 0;
        int dragsApplied = 0;

        foreach (int seed in Enumerable.Range(1, 8))
        {
            SketchGenerator generator = new(seed);

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                Sketch sketch = generator.NextSketch();

                switch (Updater.Apply(sketch, generator.NextRequest(sketch)))
                {
                    case Succeeded:
                        succeeded++;
                        break;

                    case OverConstrained:
                        overConstrained++;
                        break;

                    case Rejected:
                        rejected++;
                        break;
                }

                Drag drag = generator.NextDrag(sketch);
                Solved result = Assert.IsType<Solved>(Updater.Apply(sketch, drag));
                if (result.Changes.AppliedDelta == drag.Delta)
                {
                    dragsApplied++;
                }
                else
                {
                    dragsBlocked++;
                }
            }
        }

        string counts = $"succeeded {succeeded}, over-constrained {overConstrained}, rejected {rejected}, "
                        + $"drags applied {dragsApplied}, drags blocked {dragsBlocked}";

        Assert.True(succeeded > 100, counts);
        Assert.True(overConstrained > 0, counts);
        Assert.True(rejected > 0, counts);
        Assert.True(dragsApplied > 0, counts);
        Assert.True(dragsBlocked > 0, counts);
    }

    private static string Because(int seed, int iteration, Request request)
        => $"seed {seed}, iteration {iteration}, request {request.GetType().Name}";

    private static Point2 PositionOf(Sketch sketch, EntityId id) => sketch.Find(id) switch
    {
        Box box => box.Anchor,
        Node node => node.Position,
        _ => Point2.Origin,
    };

    private static RelationshipId? OwnRelationshipOf(Request request)
        => request is AddRelationship add ? add.Relationship.Id : null;

    private static bool IsRequestTarget(Request request, RelationshipId id)
        => request is SetParameter setParameter && setParameter.Driving == id;

    /// <summary>The entities a request is about, or null when locality is not meaningful for it.</summary>
    private static IEnumerable<EntityId>? TargetsOf(Sketch sketch, Request request) => request switch
    {
        SetPosition position => [position.Id],
        Drag drag => [drag.Id],
        DragEdge dragEdge => [dragEdge.Box],
        SetParameter parameter => sketch.Find(parameter.Driving)?.References,
        _ => null,
    };

    private static HashSet<EntityId> ConnectedComponent(Sketch sketch, IEnumerable<EntityId> seed)
    {
        HashSet<EntityId> component = [.. seed];

        for (bool grew = true; grew;)
        {
            grew = false;
            foreach (Relationship relationship in sketch.RelationshipsInOrder)
            {
                List<EntityId> touched = [.. relationship.References];

                // A segment carries no coordinates of its own; its nodes move for it.
                foreach (EntityId id in touched.ToList())
                {
                    if (sketch.Find(id) is Segment segment)
                    {
                        touched.Add(segment.Start);
                        touched.Add(segment.End);
                    }
                }

                if (!touched.Any(component.Contains))
                {
                    continue;
                }

                foreach (EntityId id in touched)
                {
                    grew |= component.Add(id);
                }
            }
        }

        return component;
    }

    private static void AssertSameResult(UpdateResult first, UpdateResult second, string because)
    {
        Assert.Equal(first.GetType(), second.GetType());

        switch (first)
        {
            case Succeeded succeeded:
            {
                Succeeded other = (Succeeded)second;
                Assert.True(succeeded.Sketch == other.Sketch, $"{because}: different sketches");
                AssertSameChanges(succeeded.Changes, other.Changes, because);
                break;
            }

            case OverConstrained conflict:
            {
                OverConstrained other = (OverConstrained)second;
                Assert.Equal(conflict.Conflict.Kind, other.Conflict.Kind);
                Assert.Equal(conflict.Conflict.Relationships, other.Conflict.Relationships);
                Assert.Equal(conflict.Conflict.Entities, other.Conflict.Entities);
                Assert.Equal(conflict.Conflict.Summary, other.Conflict.Summary);
                break;
            }

            default:
                Assert.Equal(first, second);
                break;
        }
    }

    private static void AssertSameChanges(ChangeSet first, ChangeSet second, string because)
    {
        AssertSameSet(first.Added, second.Added, $"{because}: Added");
        AssertSameSet(first.Removed, second.Removed, $"{because}: Removed");
        AssertSameSet(first.Moved, second.Moved, $"{because}: Moved");
        AssertSameSet(first.Resized, second.Resized, $"{because}: Resized");
        AssertSameSet(first.RelationshipsAdded, second.RelationshipsAdded, $"{because}: RelationshipsAdded");
        AssertSameSet(first.RelationshipsRemoved, second.RelationshipsRemoved, $"{because}: RelationshipsRemoved");
        Assert.Equal(first.AppliedDelta, second.AppliedDelta);
    }

    private static void AssertSameSet<T>(ImmutableHashSet<T> first, ImmutableHashSet<T> second, string because)
        => Assert.True(first.SetEquals(second), because);
}
