using System.Collections.Immutable;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// Properties P1 to P8 and P10 of docs/design/geometry-model.md &#xA7;7.2, and P1 for the cuts
/// and P11 of docs/design/shaped-parts-model.md &#xA7;9.2. P9 (serialization)
/// waits for #6. Since docs/design/assembly-model.md &#xA7;10 step 4 they run over three axes:
/// the generator places boxes on all six faces and at any height, relates them along Z, and asks
/// for <see cref="SetOrientation"/>, <see cref="DragFace"/> on all six faces and drags in space.
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

    [Trait("Feature", "GEO-015")]
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

    [Trait("Feature", "GEO-015")]
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
                    Length actual = succeeded.Sketch.PlaceOf(distance.To)[distance.Axis]
                                    - succeeded.Sketch.PlaceOf(distance.From)[distance.Axis];
                    Assert.True(actual == request.Value, $"{because}: the distance is {actual}, not {request.Value}");
                    break;

                default:
                    Assert.Fail($"{because}: the driving relationship went missing");
                    break;
            }
        }
    }

    [Trait("Feature", "GEO-015")]
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

    [Trait("Feature", "GEO-015")]
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

    [Trait("Feature", "GEO-015")]
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

    [Trait("Feature", "GEO-015")]
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
            Vector3 applied = result.Changes.AppliedDelta!.Value;

            // Best effort per axis, over all three (assembly-model §9.2).
            foreach (Axis axis in new[] { Axis.X, Axis.Y, Axis.Z })
            {
                Assert.True(
                    applied.Component(axis) == request.Delta.Component(axis) || applied.Component(axis) == Length.Zero,
                    $"{because}: {axis} was partly applied, as {applied.Component(axis)}");
            }

            Assert.True(
                PositionOf(result.Sketch, request.Id) - PositionOf(sketch, request.Id) == applied,
                $"{because}: the entity did not move by the delta that was reported");

            // Never worse: an axis the rigid group leaves free is applied in full. The group
            // reaches along an axis only through a relationship coupling that axis, so an entity
            // with nothing on it moves wherever it is asked, bar a node's missing Z.
            if (!sketch.Relationships.Values.Any(relationship => relationship.References.Contains(request.Id)))
            {
                Vector3 expected = sketch.Find(request.Id) is Node ? request.Delta with { Dz = Length.Zero } : request.Delta;
                Assert.True(applied == expected, $"{because}: a free entity was held back, applying {applied}");
            }
        }
    }

    [Trait("Feature", "GEO-015")]
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
                    // Rejected(CutDoesNotFit) counts as resolved: the conflict is gone, and what
                    // is left is not one. Shaped parts §2.3 refuses a resize that no longer fits a
                    // cut rather than reporting a conflict, precisely because no relationship is
                    // involved and ConflictReport.Relationships would have nothing honest to name.
                    Assert.True(
                        attempt is Succeeded or Rejected { Reason: RejectionReason.CutDoesNotFit },
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

    [Trait("Feature", "GEO-015")]
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

    [Trait("Feature", "GEO-015")]
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

    /// <summary>
    /// P1 for the cuts (docs/design/shaped-parts-model.md §9.2): every box whose cuts were derived
    /// from its blank satisfies invariants 5 to 9, so <see cref="Sketch.Validate"/> passes it and
    /// <see cref="AddEntity"/> takes it.
    /// </summary>
    /// <remarks>
    /// P1 proper — every sketch the updater hands back is consistent — now covers the cuts as
    /// well, because <see cref="SketchGenerator.NextSketch"/> produces boxes with cuts on them and
    /// <see cref="Sketch.Validate"/> checks invariants 5 to 9. This case stays for the narrower
    /// claim it makes: the model and <see cref="AddEntity"/> agree about which boxes are valid, on
    /// blanks at every rotation and with no relationships in the way.
    /// </remarks>
    [Trait("Feature", "GEO-008")]
    [Theory]
    [MemberData(nameof(Seeds))]
    public void P1_EveryBoxWhoseCutsWereDerivedFromItsBlankSatisfiesInvariants5To9(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch sketch = generator.NextShapedSketch();
            string because = $"seed {seed}, iteration {iteration}";

            ValidationResult validation = sketch.Validate();
            Assert.True(validation.IsValid, $"{because}{Environment.NewLine}{validation}");

            foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
            {
                UpdateResult result = Updater.Apply(Sketch.Empty, new AddEntity(box));
                Assert.True(result is Succeeded, $"{because}: AddEntity of {box.Id} was {result.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// P11 (shaped parts §9.2): consecutive segments share their endpoint, the last returns to the
    /// first, and the polygon of invariant 9 has positive area — in world coordinates, so a
    /// right-angle rotation is shown not to have turned the boundary inside out.
    /// </summary>
    [Trait("Feature", "GEO-007")]
    [Theory]
    [MemberData(nameof(Seeds))]
    public void P11_TheOutlineOfAValidBoxIsClosedAndEnclosesPositiveArea(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            foreach (Box box in generator.NextShapedSketch().Entities.Values.OfType<Box>().OrderBy(box => box.Id))
            {
                string because = $"seed {seed}, iteration {iteration}, box {box.Id}";
                ImmutableArray<OutlineSegment> segments = box.Outline().Segments;

                Assert.True(segments.Length >= 3, $"{because}: {segments.Length} segments");

                for (int i = 0; i < segments.Length; i++)
                {
                    OutlineSegment next = segments[(i + 1) % segments.Length];
                    Assert.True(segments[i].To == next.From, $"{because}: segment {i} does not meet the next");
                }

                Assert.True(
                    Area.TwiceSignedPolygon(box.Outline().Vertices) > Int128.Zero,
                    $"{because}: the outline encloses nothing");
            }
        }
    }

    /// <summary>
    /// P12 (shaped parts §9.2): cuts are invisible to propagation. For every sketch and every
    /// request that is not a <see cref="SetCut"/> or a <see cref="RemoveCut"/>, applying it to the
    /// sketch and to the same sketch with every cut stripped gives results whose <em>blanks</em>
    /// are identical — unless the cut-bearing one is <see cref="RejectionReason.CutDoesNotFit"/>,
    /// which is the one thing §2.3 lets the cuts decide.
    /// </summary>
    /// <remarks>
    /// <see cref="DragFace"/> on a side face is excluded, and the exclusion is the design's own
    /// doing rather than a weakening of the property: §2.3 makes it <em>clamp</em> instead of
    /// refusing, because it is best effort, so a shaped blank legitimately ends up a different size
    /// from a plain one and the result is <see cref="Solved"/> rather than the
    /// <c>Rejected(CutDoesNotFit)</c> that §9.2's wording allows for. A drag of the bottom or the
    /// top stays in: a cut never reaches either (assembly-model §2.4, §4.2), so nothing clamps it.
    /// Every other request kind is exact and takes the refusal.
    /// </remarks>
    [Trait("Feature", "GEO-015")]
    [Theory]
    [MemberData(nameof(Seeds))]
    public void P12_CutsAreInvisibleToPropagation(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch shaped = generator.WithCuts(generator.NextSketch());
            Sketch plain = SketchGenerator.WithoutCuts(shaped);
            Request request = generator.NextRequest(plain);
            string because = Because(seed, iteration, request);

            if (request is DragFace { Face: not (BoxFace.Bottom or BoxFace.Top) })
            {
                continue;
            }

            UpdateResult withCuts = Updater.Apply(shaped, request);
            UpdateResult without = Updater.Apply(plain, request);

            if (withCuts is Rejected { Reason: RejectionReason.CutDoesNotFit })
            {
                continue;
            }

            Assert.True(
                withCuts.GetType() == without.GetType(),
                $"{because}: {withCuts.GetType().Name} with cuts, {without.GetType().Name} without");

            if (withCuts is not Succeeded shapedResult || without is not Succeeded plainResult)
            {
                continue;
            }

            AssertTheBlanksAreIdentical(plainResult.Sketch, shapedResult.Sketch, because);
        }
    }

    /// <summary>
    /// The same boxes, at the same anchors, sizes and rotations — everything but the cuts.
    /// </summary>
    private static void AssertTheBlanksAreIdentical(Sketch plain, Sketch shaped, string because)
    {
        Assert.Equal(
            plain.Entities.Keys.Order().ToArray(),
            shaped.Entities.Keys.Order().ToArray());

        foreach (Box box in plain.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            Box other = Assert.IsType<Box>(shaped.Find(box.Id));
            Assert.True(
                box.Anchor == other.Anchor
                && box.Width == other.Width
                && box.Height == other.Height
                && box.Depth == other.Depth
                && box.Orientation == other.Orientation,
                $"{because}: box {box.Id} is {other.Anchor} {other.Width}x{other.Height}x{other.Depth} at "
                + $"{other.Orientation} with cuts and {box.Anchor} {box.Width}x{box.Height}x{box.Depth} at "
                + $"{box.Orientation} without");
        }
    }

    /// <summary>
    /// P13 (shaped parts §9.2): a successful resize never silently leaves a cut that does not fit.
    /// Every box on the result satisfies invariants 7 to 9 — which is what
    /// <see cref="Sketch.Validate"/> checks, so a consistent result is the whole claim.
    /// </summary>
    [Trait("Feature", "GEO-015")]
    [Theory]
    [MemberData(nameof(Seeds))]
    public void P13_ASuccessfulResizeLeavesEveryCutFitting(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations; iteration++)
        {
            Sketch shaped = generator.WithCuts(generator.NextSketch());
            SketchAssert.IsConsistent(shaped, $"seed {seed}, iteration {iteration}: the generator's own sketch");

            if (generator.NextResize(shaped) is not { } request)
            {
                continue;
            }

            if (Updater.Apply(shaped, request) is Succeeded succeeded)
            {
                SketchAssert.IsConsistent(succeeded.Sketch, Because(seed, iteration, request));
            }
        }
    }

    /// <summary>
    /// P13 would pass if the fit check were dead code and no generated resize ever reached it, so
    /// this counts the outcomes the property relies on: resizes that succeed, resizes refused
    /// because a cut no longer fits, and drags the cut clamp cut short.
    /// </summary>
    [Fact]
    public void TheGeneratorReachesTheOutcomesTheCutPropertiesRelyOn()
    {
        int resized = 0;
        int refusedForACut = 0;
        int clampedDrags = 0;
        int shapedSketches = 0;

        foreach (int seed in Enumerable.Range(1, 8))
        {
            SketchGenerator generator = new(seed);

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                Sketch shaped = generator.WithCuts(generator.NextSketch());
                shapedSketches += shaped.Entities.Values.OfType<Box>().Any(box => !box.Cuts.IsEmpty) ? 1 : 0;

                if (generator.NextResize(shaped) is not { } request)
                {
                    continue;
                }

                UpdateResult result = Updater.Apply(shaped, request);
                switch (result)
                {
                    case Succeeded ok when !ok.Changes.Resized.IsEmpty:
                        resized++;
                        break;

                    case Rejected { Reason: RejectionReason.CutDoesNotFit }:
                        refusedForACut++;
                        break;
                }

                // The clamp's own signature: the edge ended up leaving the blank bigger than the
                // drag asked for, which is the one thing only a clamp does.
                if (request is DragFace face
                    && result is Succeeded dragged
                    && shaped.Find<Box>(face.Box) is { } before
                    && dragged.Sketch.Find<Box>(face.Box) is { } after)
                {
                    Axis axis = face.Face switch
                    {
                        BoxFace.East or BoxFace.West => Axis.X,
                        BoxFace.South or BoxFace.North => Axis.Y,
                        _ => Axis.Z,
                    };
                    clampedDrags += before.Size(axis) + face.Delta < after.Size(axis) ? 1 : 0;
                }
            }
        }

        Assert.True(shapedSketches > 300, $"{shapedSketches} sketches carried a cut");
        Assert.True(resized > 100, $"{resized} resizes succeeded");
        Assert.True(refusedForACut > 0, $"{refusedForACut} resizes were refused for a cut");
        Assert.True(clampedDrags > 0, $"{clampedDrags} edge drags were clamped by a cut");
    }

    /// <summary>
    /// The shaped generator reaches every cut kind and both bows, or the two properties above
    /// would pass by never producing a cut at all.
    /// </summary>
    [Fact]
    public void TheShapedGeneratorReachesEveryCutKind()
    {
        int cornerCuts = 0;
        int roundedCorners = 0;
        int outwardCurves = 0;
        int inwardCurves = 0;
        int plainBlanks = 0;

        foreach (int seed in Enumerable.Range(1, 8))
        {
            SketchGenerator generator = new(seed);

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                foreach (Box box in generator.NextShapedSketch().Entities.Values.OfType<Box>())
                {
                    plainBlanks += box.Cuts.IsEmpty ? 1 : 0;
                    cornerCuts += box.Cuts.OfType<CornerCut>().Count();
                    roundedCorners += box.Cuts.OfType<RoundedCorner>().Count();
                    outwardCurves += box.Cuts.OfType<CurvedEdge>().Count(curve => curve.Bow == Bow.Outward);
                    inwardCurves += box.Cuts.OfType<CurvedEdge>().Count(curve => curve.Bow == Bow.Inward);
                }
            }
        }

        Assert.True(cornerCuts > 100, $"{cornerCuts} corner cuts");
        Assert.True(roundedCorners > 100, $"{roundedCorners} rounded corners");
        Assert.True(outwardCurves > 50, $"{outwardCurves} outward curves");
        Assert.True(inwardCurves > 50, $"{inwardCurves} inward curves");
        Assert.True(plainBlanks > 0, "no blank was ever left plain");
    }

    /// <summary>
    /// P17 (assembly-model §9.2): the solid is closed and exact. For every generated box with cuts,
    /// placed in space, and then turned to each of the 24 orientations in turn: every face's
    /// boundary closes; the caps are the local outline lifted to 0 and <see cref="Box.Depth"/> and
    /// wind outward; every side is one outline segment swept square to the caps, its four corners
    /// on the two caps; and every point of every face is <c>Anchor + Orientation.Apply(local)</c>
    /// for a point of the outline lifted to 0 or the depth.
    /// </summary>
    [Theory]
    [MemberData(nameof(Seeds))]
    public void P17_TheSolidIsClosedAndExact(int seed)
    {
        SketchGenerator generator = new(seed);

        for (int iteration = 0; iteration < Iterations / 4; iteration++)
        {
            foreach (Box generated in generator.NextShapedSketchInSpace().Entities.Values.OfType<Box>().OrderBy(box => box.Id))
            {
                foreach (BoxFace faceUp in Enum.GetValues<BoxFace>())
                {
                    for (int q = 0; q < 4; q++)
                    {
                        Box box = generated with { FaceUp = faceUp, Rotation = Angle.Right * q };
                        AssertTheSolidIsClosedAndExact(box, $"seed {seed}, iteration {iteration}, box {box.Id}, {box.Orientation}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// P17 would pass on plain boxes alone, so the generator it runs on is shown to carry every cut
    /// kind, at heights off the plan datum.
    /// </summary>
    [Fact]
    public void TheGeneratorInSpaceReachesEveryCutKindOffTheDatum()
    {
        List<Box> boxes = [];
        foreach (int seed in Enumerable.Range(1, 8))
        {
            SketchGenerator generator = new(seed);
            for (int iteration = 0; iteration < Iterations / 4; iteration++)
            {
                boxes.AddRange(generator.NextShapedSketchInSpace().Entities.Values.OfType<Box>());
            }
        }

        Assert.True(boxes.Count(box => box.Cuts.OfType<CornerCut>().Any()) > 20);
        Assert.True(boxes.Count(box => box.Cuts.OfType<RoundedCorner>().Any()) > 20);
        Assert.True(boxes.Count(box => box.Cuts.OfType<CurvedEdge>().Any()) > 10);
        Assert.True(boxes.Count(box => box.Anchor.Z != Length.Zero && !box.Cuts.IsEmpty) > 20);
        Assert.True(boxes.Count(box => box.Depth != Box.DefaultDepth) > 20);
    }

    private static void AssertTheSolidIsClosedAndExact(Box box, string because)
    {
        ImmutableArray<OutlineSegment> outline = box.Outline().Segments;
        ImmutableArray<SolidFace> faces = box.Solid().Faces;

        Point3 Lift(Point2 local, Length z) => box.Anchor + box.Orientation.Apply(new Vector3(local.X, local.Y, z));

        static IEnumerable<Point2> PointsOfOutline(OutlineSegment segment) => segment switch
        {
            ArcByCenter arc => [arc.From, arc.To, arc.Center],
            ArcThrough arc => [arc.From, arc.Through, arc.To],
            _ => [segment.From, segment.To],
        };

        HashSet<Point3> bottomPoints = [.. outline.SelectMany(PointsOfOutline).Select(p => Lift(p, Length.Zero))];
        HashSet<Point3> topPoints = [.. outline.SelectMany(PointsOfOutline).Select(p => Lift(p, box.Depth))];

        Assert.True(faces.Length == outline.Length + 2, $"{because}: {faces.Length} faces for {outline.Length} outline segments");
        Assert.True(faces[0].Of == BoxFace.Bottom && faces[1].Of == BoxFace.Top, $"{because}: the caps come first");
        Assert.True(faces[0].Boundary.Length == outline.Length && faces[1].Boundary.Length == outline.Length, $"{because}: a cap is not the outline");

        // Closed: each segment ends where the next begins, and the last where the first begins.
        foreach (SolidFace face in faces)
        {
            for (int i = 0; i < face.Boundary.Length; i++)
            {
                SolidSegment next = face.Boundary[(i + 1) % face.Boundary.Length];
                Assert.True(face.Boundary[i].To == next.From, $"{because}: a {face.Of?.ToString() ?? "cut"} face's segment {i} does not meet the next");
            }
        }

        // Exact: every point is a point of the outline, lifted to its cap's level and placed.
        Assert.True(faces[0].Boundary.SelectMany(SolidTests.PointsOf).All(bottomPoints.Contains), $"{because}: the bottom cap leaves the outline");
        Assert.True(faces[1].Boundary.SelectMany(SolidTests.PointsOf).All(topPoints.Contains), $"{because}: the top cap leaves the outline");

        // The caps wind outward: their right-hand normals are the box's own bottom and top normals.
        foreach (SolidFace cap in faces[..2])
        {
            (Axis axis, bool positive) = box.Orientation.Normal(cap.Of!.Value);
            (Int128 nx, Int128 ny, Int128 nz) = SolidTests.NewellNormal(cap);
            Int128 along = axis switch { Axis.X => nx, Axis.Y => ny, _ => nz };
            Int128 across = (axis == Axis.X ? 0 : Int128.Abs(nx)) + (axis == Axis.Y ? 0 : Int128.Abs(ny)) + (axis == Axis.Z ? 0 : Int128.Abs(nz));
            Assert.True(positive ? along > 0 : along < 0, $"{because}: the {cap.Of} cap winds inward");
            Assert.True(across == 0, $"{because}: the {cap.Of} cap is not in a plane square to {axis}");
        }

        // Every side: two corners on the bottom cap, two on the top, joined by rulings square to the
        // caps — along the box's own local Z, which is what "a cut is square through the cap" means.
        Vector3 up = box.Orientation.Apply(new Vector3(Length.Zero, Length.Zero, box.Depth));
        HashSet<BoxFace> sidesSeen = [];
        foreach (SolidFace side in faces[2..])
        {
            Assert.True(side.Boundary.Length == 4, $"{because}: a side of {side.Boundary.Length} segments");
            Assert.True(side.Of is null || side.Of is BoxFace.South or BoxFace.East or BoxFace.North or BoxFace.West, $"{because}: a side is Of {side.Of}");
            Assert.True(side.Of is null || sidesSeen.Add(side.Of.Value), $"{because}: two sides are Of {side.Of}");

            Assert.True(bottomPoints.Contains(side.Boundary[0].From) && bottomPoints.Contains(side.Boundary[0].To), $"{because}: a side's lower corners are not on the bottom cap");
            Assert.True(topPoints.Contains(side.Boundary[2].From) && topPoints.Contains(side.Boundary[2].To), $"{because}: a side's upper corners are not on the top cap");
            Assert.True(side.Boundary.SelectMany(SolidTests.PointsOf).All(p => bottomPoints.Contains(p) || topPoints.Contains(p)), $"{because}: a side leaves the outline");

            Assert.True(side.Boundary[1] is StraightSegment3 rising && rising.To - rising.From == up, $"{because}: a side's rising ruling is not square to the caps");
            Assert.True(side.Boundary[3] is StraightSegment3 falling && falling.From - falling.To == up, $"{because}: a side's falling ruling is not square to the caps");
            Assert.True(side.Boundary[0].GetType() == side.Boundary[2].GetType(), $"{because}: a side's two caps' edges are different kinds");
        }
    }

    [Fact]
    public void TheGeneratorReachesEveryOutcomeThesePropertiesRelyOn()
    {
        // Without this, a generator that only ever produced easy sketches would make P1, P6 and
        // P7 pass by never reaching the cases they are about.
        int succeeded = 0;
        int overConstrained = 0;
        int dragsBlocked = 0;
        int dragsApplied = 0;
        int oddSpans = 0;
        int rows = 0;
        int rowsResized = 0;
        int longestRow = 0;
        int tippedBoxes = 0;
        int heldAlongZ = 0;
        int liftedByADrag = 0;
        int zHeldBackByADrag = 0;
        int turned = 0;
        HashSet<RejectionReason> reasons = [];

        foreach (int seed in Enumerable.Range(1, 8))
        {
            SketchGenerator generator = new(seed);

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                Sketch sketch = generator.NextSketch();
                oddSpans += CentredOddSpans(sketch);

                // Three axes (assembly-model §9.2): boxes on every face, relationships along Z.
                tippedBoxes += sketch.Entities.Values.OfType<Box>().Count(box => box.FaceUp != BoxFace.Top);
                heldAlongZ += sketch.RelationshipsInOrder.Count(relationship => SpeaksAboutZ(sketch, relationship));

                // Issue #49 is about a row of three or more parts. Without this the properties
                // could pass while never propagating along one.
                int row = SketchGenerator.LongestRow(sketch);
                longestRow = Math.Max(longestRow, row);
                if (row >= 3)
                {
                    rows++;
                    if (generator.NextSetParameter(sketch) is { } dimension
                        && Updater.Apply(sketch, dimension) is Succeeded)
                    {
                        rowsResized++;
                    }
                }

                Request request = generator.NextRequest(sketch);
                switch (Updater.Apply(sketch, request))
                {
                    case Succeeded done:
                        succeeded++;
                        turned += request is SetOrientation && !done.Changes.Modified.IsEmpty ? 1 : 0;
                        break;

                    case OverConstrained:
                        overConstrained++;
                        break;

                    case Rejected refused:
                        reasons.Add(refused.Reason);
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

                if (drag.Delta.Dz != Length.Zero)
                {
                    liftedByADrag += result.Changes.AppliedDelta!.Value.Dz != Length.Zero ? 1 : 0;
                    zHeldBackByADrag += result.Changes.AppliedDelta!.Value.Dz == Length.Zero
                                        && sketch.Find(drag.Id) is Box ? 1 : 0;
                }
            }
        }

        string counts = $"succeeded {succeeded}, over-constrained {overConstrained}, "
                        + $"rejection reasons [{string.Join(", ", reasons.Order())}], "
                        + $"drags applied {dragsApplied}, drags blocked {dragsBlocked}, "
                        + $"odd centred spans {oddSpans}, "
                        + $"rows of three or more {rows} (longest {longestRow}), resized {rowsResized}, "
                        + $"tipped boxes {tippedBoxes}, relationships along Z {heldAlongZ}, "
                        + $"drags lifted {liftedByADrag}, box drags held on Z {zHeldBackByADrag}, turns {turned}";

        Assert.True(succeeded > 100, counts);
        Assert.True(overConstrained > 0, counts);
        Assert.True(dragsApplied > 0, counts);
        Assert.True(dragsBlocked > 0, counts);
        Assert.True(reasons.Count >= 3, counts);

        // Rows of three or more parts, and rows that a dimension actually resized: the shape
        // issue #49 is about, and the request that used to fail on it.
        Assert.True(rows > 0, counts);
        Assert.True(longestRow >= 4, counts);
        Assert.True(rowsResized > 0, counts);

        // The half-unit Centered case (Fable review of #35, finding 2) is only reachable when a
        // span is an odd number of units, which a 1/16" grid can never produce.
        Assert.True(oddSpans > 0, counts);

        // Three axes: without these the properties could pass while every sketch lay flat in the
        // plan. Tipped boxes, relationships held along Z, drags that lift and drags a relationship
        // holds down, and turns that went through.
        Assert.True(tippedBoxes > 100, counts);
        Assert.True(heldAlongZ > 50, counts);
        Assert.True(liftedByADrag > 0, counts);
        Assert.True(zHeldBackByADrag > 0, counts);
        Assert.True(turned > 0, counts);
        Assert.Contains(RejectionReason.OrientationWithRelationships, reasons);
    }

    /// <summary>How many Centered relationships in this sketch span an odd number of units.</summary>
    private static int CentredOddSpans(Sketch sketch)
        => sketch.RelationshipsInOrder
            .OfType<Centered>()
            .Count(centred => (sketch.PlaceOf(centred.A)[centred.Axis]
                               + sketch.PlaceOf(centred.B)[centred.Axis]).Units % 2 != 0);

    /// <summary>Whether a relationship holds something along world Z.</summary>
    private static bool SpeaksAboutZ(Sketch sketch, Relationship relationship) => relationship switch
    {
        Flush flush => sketch.PlaceOf(flush.A).Axes is [Axis.Z],
        Coincident coincident => Place.Common(sketch.PlaceOf(coincident.A), sketch.PlaceOf(coincident.B)).Contains(Axis.Z),
        AxisDistance distance => distance.Axis == Axis.Z,
        Centered centred => centred.Axis == Axis.Z,
        _ => false,
    };

    private static string Because(int seed, int iteration, Request request)
        => $"seed {seed}, iteration {iteration}, request {request}";

    private static Point3 PositionOf(Sketch sketch, EntityId id) => sketch.Find(id) switch
    {
        Box box => box.Anchor,
        Node node => new Point3(node.Position.X, node.Position.Y, Length.Zero),
        _ => Point3.Origin,
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
        DragFace dragFace => [dragFace.Box],
        SetOrientation turn => [turn.Box],
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
