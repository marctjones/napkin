namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// References and relationships in three dimensions, docs/design/assembly-model.md &#xA7;10 step 3:
/// golden case 8 (what every feature fixes, under every orientation), case 11 (the refusals), the
/// checker over three axes, &#xA7;2.5's virtual features, and invariants 12 and 13.
/// </summary>
/// <remarks>
/// Case 8's expectations are written independently of <see cref="Orientation"/>: &#xA7;1.3's tip
/// table typed out as coordinate formulas and the spin written by hand, as
/// <see cref="BoxInSpaceTests"/> does for case 5.
/// </remarks>
public class ReferenceTests
{
    private static readonly BoxFace[] Faces = Enum.GetValues<BoxFace>();

    // Case 5's box: 48" x 24" x 3/4", anchored at (1536, 2048, 4096).
    private const long W = 49152;
    private const long H = 24576;
    private const long D = 768;
    private static readonly Point3 CaseAnchor = new(new Length(1536), new Length(2048), new Length(4096));

    public static TheoryData<BoxFace, int> AllOrientations
    {
        get
        {
            TheoryData<BoxFace, int> data = [];
            foreach (BoxFace face in Faces)
            {
                for (int q = 0; q < 4; q++)
                {
                    data.Add(face, q);
                }
            }

            return data;
        }
    }

    /// <summary>All 26 features of a box: 6 faces, 12 edges, 8 vertices.</summary>
    private static IEnumerable<BoxFeature> AllFeatures()
    {
        foreach (BoxFace face in Faces)
        {
            yield return BoxFeature.Face(face);
        }

        for (int i = 0; i < Faces.Length; i++)
        {
            for (int j = i + 1; j < Faces.Length; j++)
            {
                if (!AreOpposite(Faces[i], Faces[j]))
                {
                    yield return BoxFeature.Edge(Faces[i], Faces[j]);
                }
            }
        }

        foreach (BoxFace northSouth in new[] { BoxFace.South, BoxFace.North })
        {
            foreach (BoxFace eastWest in new[] { BoxFace.East, BoxFace.West })
            {
                foreach (BoxFace cap in new[] { BoxFace.Bottom, BoxFace.Top })
                {
                    yield return BoxFeature.Vertex(northSouth, eastWest, cap);
                }
            }
        }
    }

    private static bool AreOpposite(BoxFace a, BoxFace b)
        => (a, b) is (BoxFace.South, BoxFace.North) or (BoxFace.North, BoxFace.South)
            or (BoxFace.East, BoxFace.West) or (BoxFace.West, BoxFace.East)
            or (BoxFace.Bottom, BoxFace.Top) or (BoxFace.Top, BoxFace.Bottom);

    /// <summary>§1.3's Tip(FaceUp), typed out.</summary>
    private static (long X, long Y, long Z) Tip(BoxFace faceUp, long x, long y, long z) => faceUp switch
    {
        BoxFace.Top => (x, y, z),
        BoxFace.Bottom => (x, -y, -z),
        BoxFace.North => (x, -z, y),
        BoxFace.South => (x, z, -y),
        BoxFace.East => (-z, y, x),
        BoxFace.West => (z, y, -x),
        _ => throw new ArgumentOutOfRangeException(nameof(faceUp)),
    };

    /// <summary>A right-hand quarter-turn spin about Z, by hand.</summary>
    private static (long X, long Y) Spin(int quarterTurns, long x, long y) => quarterTurns switch
    {
        0 => (x, y),
        1 => (-y, x),
        2 => (-x, -y),
        _ => (y, -x),
    };

    private static Box CaseBox(BoxFace faceUp, int quarterTurns) => new(
        SketchBuilder.EntityIdAt(1), LayerId.Default, CaseAnchor, new Length(W), new Length(H), new Length(D), faceUp, Angle.Right * quarterTurns);

    // ---- Case 8 ------------------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Case8_EveryFeatureFixesTheAxesAndCoordinatesTheTableImplies(BoxFace faceUp, int quarterTurns)
    {
        Box box = CaseBox(faceUp, quarterTurns);
        Sketch sketch = Sketch.Empty.WithEntity(box);
        int count = 0;

        foreach (BoxFeature feature in AllFeatures())
        {
            count++;
            Place place = sketch.PlaceOf(new FeatureRef(box.Id, feature));

            // A face fixes one axis, an edge two, a vertex three.
            Assert.Equal(3 - feature.Dimension, place.Count);

            foreach (BoxFace face in feature.Faces)
            {
                // The face's local axis, and the size standing at its far end.
                (long ux, long uy, long uz, long size, bool far) = face switch
                {
                    BoxFace.West => (1L, 0L, 0L, W, false),
                    BoxFace.East => (1L, 0L, 0L, W, true),
                    BoxFace.South => (0L, 1L, 0L, H, false),
                    BoxFace.North => (0L, 1L, 0L, H, true),
                    BoxFace.Bottom => (0L, 0L, 1L, D, false),
                    _ => (0L, 0L, 1L, D, true),
                };

                // Where that local axis lands in the world, and which way, by the typed-out table.
                (long tx, long ty, long tz) = Tip(faceUp, ux, uy, uz);
                (long sx, long sy) = Spin(quarterTurns, tx, ty);
                (Axis axis, long sign, long anchor) = (sx, sy, tz) switch
                {
                    (not 0, _, _) => (Axis.X, sx, CaseAnchor.X.Units),
                    (_, not 0, _) => (Axis.Y, sy, CaseAnchor.Y.Units),
                    _ => (Axis.Z, tz, CaseAnchor.Z.Units),
                };

                long expected = anchor + (far ? sign * size : 0);
                Assert.True(place.Fixes(axis), $"{feature} under {faceUp}, {quarterTurns} turns: {face} should fix {axis}; {place}");
                Assert.Equal(expected, place[axis].Units);
                Assert.Equal(0, place[axis].Units % 256);
            }
        }

        Assert.Equal(26, count);
    }

    [Theory]
    [MemberData(nameof(AllOrientations))]
    public void Case8_AVertexIsBoxVertexAndACentreIsBoxCentre(BoxFace faceUp, int quarterTurns)
    {
        Box box = CaseBox(faceUp, quarterTurns);
        Sketch sketch = Sketch.Empty.WithEntity(box);

        foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
        {
            foreach (BoxLevel level in Enum.GetValues<BoxLevel>())
            {
                Point3 vertex = box.Vertex(corner, level);
                Assert.Equal(
                    new Place(vertex.X, vertex.Y, vertex.Z),
                    sketch.PlaceOf(new FeatureRef(box.Id, BoxFeature.Vertex(corner, level))));
            }
        }

        Point3 centre = box.Center;
        Assert.Equal(new Place(centre.X, centre.Y, centre.Z), sketch.PlaceOf(new CenterRef(box.Id)));
    }

    [Fact]
    public void Case8_ABoxAsDrawnFixesWhatItsCornersAndEdgesAlwaysDid()
    {
        // FaceUp Top: a local upright is the plan corner, on X and Y, and a side face the plan
        // edge's line, on its one axis — for every quarter turn.
        for (int q = 0; q < 4; q++)
        {
            Box box = Box.AsDrawn(
                SketchBuilder.EntityIdAt(1), LayerId.Default, Point2.Inches(10, 20), Length.Inches(30), Length.Inches(8), Length.Inches(2), Angle.Right * q);
            Sketch sketch = Sketch.Empty.WithEntity(box);

            foreach (BoxCorner corner in Enum.GetValues<BoxCorner>())
            {
                Point2 plan = box.Corner(corner);
                Assert.Equal(new Place(plan.X, plan.Y, null), sketch.PlaceOf(TestRefs.Corner(box.Id, corner)));
            }

            foreach (BoxEdge edge in Enum.GetValues<BoxEdge>())
            {
                (BoxCorner from, BoxCorner to) = Box.Ends(edge);
                Point2 a = box.Corner(from);
                Point2 b = box.Corner(to);
                Axis normal = a.X == b.X ? Axis.X : Axis.Y;
                Assert.Equal(Place.On(normal, a.Component(normal)), sketch.PlaceOf(TestRefs.Edge(box.Id, edge)));
            }
        }
    }

    [Fact]
    public void NodesSegmentsAndCentresFixWhatTheyAlwaysDid()
    {
        SketchBuilder builder = new();
        EntityId a = builder.AddNode(1, 2);
        EntityId b = builder.AddNode(1, 9);
        EntityId c = builder.AddNode(6, 9);
        EntityId d = builder.AddNode(7, 3);
        EntityId vertical = builder.AddSegment(a, b);
        EntityId horizontal = builder.AddSegment(b, c);
        EntityId diagonal = builder.AddSegment(c, d);
        EntityId nothing = builder.AddSegment(a, a);
        Sketch sketch = builder.Sketch;

        Assert.Equal(new Place(Length.Inches(1), Length.Inches(2), null), sketch.PlaceOf(new NodeRef(a)));
        Assert.Equal(Place.On(Axis.X, Length.Inches(1)), sketch.PlaceOf(new SegmentRef(vertical)));
        Assert.Equal(Place.On(Axis.Y, Length.Inches(9)), sketch.PlaceOf(new SegmentRef(horizontal)));
        Assert.Equal(0, sketch.PlaceOf(new SegmentRef(diagonal)).Count);
        Assert.Equal(0, sketch.PlaceOf(new SegmentRef(nothing)).Count);
    }

    [Fact]
    public void OffTheQuarterTurnsAnUprightStillFixesXAndYAndASideFaceFixesNothing()
    {
        // A solver-written box at 45°: the checker still judges an upright at its rounded point and
        // a flush on a side face geometrically, as geometry-model §10.2 does.
        Box box = Box.AsDrawn(SketchBuilder.EntityIdAt(1), LayerId.Default, Point2.Origin, Length.Inches(20), Length.Inches(4), Length.Inches(1), Angle.Degrees(45));
        Sketch sketch = Sketch.Empty.WithEntity(box);

        Point3 vertex = box.Vertex(BoxCorner.NorthEast, BoxLevel.Top);
        Assert.Equal(new Place(vertex.X, vertex.Y, null), sketch.PlaceOf(TestRefs.Corner(box.Id, BoxCorner.NorthEast)));
        Assert.Equal(new Place(vertex.X, vertex.Y, vertex.Z), sketch.PlaceOf(new FeatureRef(box.Id, BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top))));
        Assert.Equal(0, sketch.PlaceOf(TestRefs.Edge(box.Id, BoxEdge.North)).Count);
        Assert.Equal(Place.On(Axis.Z, Length.Inches(1)), sketch.PlaceOf(new FeatureRef(box.Id, BoxFeature.Face(BoxFace.Top))));

        // The legality rule has no opinion off the quarter turns.
        Assert.Null(PlaceRules.Refusal(sketch, new Flush(RelationshipId.New(), TestRefs.Edge(box.Id, BoxEdge.North), TestRefs.Edge(box.Id, BoxEdge.South))));
    }

    // ---- Case 11 -----------------------------------------------------------------------------

    private static Box Lying(int index, string name, Point3 anchor, long width, long height, long depth, BoxFace faceUp = BoxFace.Top)
        => new Box(SketchBuilder.EntityIdAt(index), LayerId.Default, anchor, Length.Inches(width), Length.Inches(height), Length.Inches(depth), faceUp, Angle.Zero)
        {
            Name = name,
        };

    [Fact]
    public void Case11_AFlushBetweenFacesOnDifferentAxesIsRefusedNamingBothAndWhatEachFixes()
    {
        Box top = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Box leg = Lying(2, "Leg", Point3.Origin, 2, 2, 16);
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg);

        Flush flush = new(RelationshipId.New(), new FeatureRef(top.Id, BoxFeature.Face(BoxFace.North)), new FeatureRef(leg.Id, BoxFeature.Face(BoxFace.Top)));
        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(sketch, new AddRelationship(flush)));

        Assert.Equal(RejectionReason.PlacesNotComparable, rejected.Reason);
        ValidationError detail = Assert.IsType<ValidationError>(rejected.Detail);
        Assert.Equal(ValidationErrorKind.PlacesNotComparable, detail.Kind);
        Assert.Contains("Top's north face fixes Y", detail.Message, StringComparison.Ordinal);
        Assert.Contains("Leg's top face fixes Z", detail.Message, StringComparison.Ordinal);

        // And a file holding it is refused by the same rule.
        ValidationResult validation = sketch.WithRelationship(flush).Validate();
        Assert.Equal(ValidationErrorKind.PlacesNotComparable, Assert.Single(validation.Errors).Kind);
    }

    [Fact]
    public void Case11_ACoincidentBetweenAFaceAndAVertexIsRefused()
    {
        Box top = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Box leg = Lying(2, "Leg", Point3.Origin, 2, 2, 16);
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg);

        Coincident coincident = new(
            RelationshipId.New(),
            new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom)),
            new FeatureRef(leg.Id, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top)));
        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(sketch, new AddRelationship(coincident)));

        Assert.Equal(RejectionReason.PlacesNotComparable, rejected.Reason);
        Assert.Contains("Top's bottom face fixes Z", rejected.Detail!.Message, StringComparison.Ordinal);
        Assert.Contains("Leg's top south-west corner fixes X, Y and Z", rejected.Detail.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Case11_ANodeOnALocalUprightOfABoxAsDrawnIsHeldOnXAndY()
    {
        Box drawn = Lying(1, "Leg", Point3.Inches(10, 20, 0), 2, 2, 16);
        Node node = new(SketchBuilder.EntityIdAt(2), LayerId.Default, Point2.Inches(3, 4));
        Sketch sketch = Sketch.Empty.WithEntity(drawn).WithEntity(node);

        FeatureRef upright = new(drawn.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest));
        Solved solved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(
            sketch, new AddRelationship(new Coincident(RelationshipId.New(), upright, new NodeRef(node.Id)))));

        // The second place follows the first: the node lands on the upright's X and Y.
        Assert.Equal(Point2.Inches(10, 20), solved.Sketch.Find<Node>(node.Id)!.Position);
        SketchAssert.IsConsistent(solved.Sketch);
    }

    [Fact]
    public void Case11_ANodeOnTheSameLocalUprightOfABoxOnItsEastFaceIsRefused()
    {
        // East up, the blank's south-west corner edge lies along plan Y at ground level: it fixes Y
        // and Z, and shares only Y with a node.
        Box standing = Lying(1, "Leg", Point3.Inches(10, 20, 0), 16, 2, 2, BoxFace.East);
        Node node = new(SketchBuilder.EntityIdAt(2), LayerId.Default, Point2.Inches(3, 4)) { Name = "N" };
        Sketch sketch = Sketch.Empty.WithEntity(standing).WithEntity(node);

        FeatureRef upright = new(standing.Id, BoxFeature.LocalUpright(BoxCorner.SouthWest));
        Assert.Equal(new Place(null, Length.Inches(20), Length.Zero), sketch.PlaceOf(upright));

        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(
            sketch, new AddRelationship(new Coincident(RelationshipId.New(), new NodeRef(node.Id), upright))));
        Assert.Equal(RejectionReason.PlacesNotComparable, rejected.Reason);
        Assert.Contains("Leg's south-west edge fixes Y and Z", rejected.Detail!.Message, StringComparison.Ordinal);
        Assert.Contains("node N fixes X and Y", rejected.Detail.Message, StringComparison.Ordinal);

        // Through the footprint, the plan's south-west corner of the same box is a real upright.
        FeatureRef planUpright = new(standing.Id, standing.Footprint().UprightAt(BoxCorner.SouthWest));
        Assert.Equal([Axis.X, Axis.Y], sketch.PlaceOf(planUpright).Axes);
        Assert.Null(PlaceRules.Refusal(sketch, new Coincident(RelationshipId.New(), new NodeRef(node.Id), planUpright)));
    }

    [Fact]
    public void Case11_ANodeOnACentreIsHeldOnXAndYAsItAlwaysWas()
    {
        Box drawn = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Node node = new(SketchBuilder.EntityIdAt(2), LayerId.Default, Point2.Inches(3, 4));
        Sketch sketch = Sketch.Empty.WithEntity(drawn).WithEntity(node);

        Solved solved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(
            sketch, new AddRelationship(new Coincident(RelationshipId.New(), new CenterRef(drawn.Id), new NodeRef(node.Id)))));

        Assert.Equal(Point2.Inches(20, 10), solved.Sketch.Find<Node>(node.Id)!.Position);
        SketchAssert.IsConsistent(solved.Sketch);
    }

    [Fact]
    public void Case11_TwoVerticesAreComparableOnAllThreeAxesAndWaitForTheZPropagator()
    {
        Box top = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Box leg = Lying(2, "Leg", Point3.Origin, 2, 2, 16);
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg);

        FeatureRef under = new(top.Id, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Bottom));
        FeatureRef on = new(leg.Id, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top));
        Coincident coincident = new(RelationshipId.New(), under, on);

        // Legal: the two vertices share X, Y and Z — and here they already coincide on all three.
        Assert.Null(PlaceRules.Refusal(sketch, coincident));
        Assert.Equal([Axis.X, Axis.Y, Axis.Z], Place.Common(sketch.PlaceOf(under), sketch.PlaceOf(on)));
        Sketch holding = sketch.WithRelationship(coincident);
        SketchAssert.IsConsistent(holding);

        // The checker judges Z too: lift the top one unit and it no longer holds.
        Sketch lifted = holding.WithEntity(top with { Anchor = top.Anchor with { Z = top.Anchor.Z + new Length(1) } });
        Violation violation = Assert.Single(RelationshipChecker.Check(lifted).Violations);
        Assert.Equal(new Length(1), violation.Residual);
        Assert.True(violation.Exact);

        // Holding it through an edit is §10 step 4's: this build says so rather than holding X and Y only.
        Assert.Equal(
            new Rejected(RejectionReason.UnsupportedRelationship),
            DirectUpdater.Instance.Apply(sketch, new AddRelationship(coincident)));
    }

    [Fact]
    public void TwoParallelEdgesAreComparableAndTwoCrossingEdgesAreNot()
    {
        Box a = Lying(1, "A", Point3.Origin, 10, 4, 1);
        Box b = Lying(2, "B", Point3.Inches(10, 0, 0), 10, 4, 1);
        Sketch sketch = Sketch.Empty.WithEntity(a).WithEntity(b);

        // Two edges along X at the top south side of each: parallel, sharing Y and Z.
        FeatureRef alongA = new(a.Id, BoxFeature.Edge(BoxFace.South, BoxFace.Top));
        FeatureRef alongB = new(b.Id, BoxFeature.Edge(BoxFace.South, BoxFace.Top));
        Assert.Null(PlaceRules.Refusal(sketch, new Coincident(RelationshipId.New(), alongA, alongB)));

        // An edge along X and an edge along Y share only Z.
        FeatureRef acrossB = new(b.Id, BoxFeature.Edge(BoxFace.West, BoxFace.Top));
        Assert.Equal(
            ValidationErrorKind.PlacesNotComparable,
            PlaceRules.Refusal(sketch, new Coincident(RelationshipId.New(), alongA, acrossB))!.Kind);
    }

    [Fact]
    public void AFlushBetweenTwoPlanFacesIsStillHeldAndOneBetweenTwoCapsWaitsForZ()
    {
        SketchBuilder builder = new();
        EntityId left = builder.AddBox(0, 0, 10, 4);
        EntityId right = builder.AddBox(12, 0, 10, 4);
        Sketch sketch = builder.Sketch;

        Solved solved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(
            sketch, new AddRelationship(new Flush(RelationshipId.New(), TestRefs.Edge(left, BoxEdge.East), TestRefs.Edge(right, BoxEdge.West)))));
        Assert.Equal(Length.Inches(10), solved.Sketch.Find<Box>(right)!.Anchor.X);

        Flush caps = new(
            RelationshipId.New(),
            new FeatureRef(left, BoxFeature.Face(BoxFace.Top)),
            new FeatureRef(right, BoxFeature.Face(BoxFace.Bottom)));
        Assert.Null(PlaceRules.Refusal(sketch, caps));
        Assert.Equal(new Rejected(RejectionReason.UnsupportedRelationship), DirectUpdater.Instance.Apply(sketch, new AddRelationship(caps)));
    }

    [Fact]
    public void AxisDistanceAndCenteredNeedEveryPlaceToFixTheirAxis()
    {
        Box top = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Box leg = Lying(2, "Leg", Point3.Origin, 2, 2, 16);
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg);
        FeatureRef bottom = new(top.Id, BoxFeature.Face(BoxFace.Bottom));
        FeatureRef legTop = new(leg.Id, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top));
        FeatureRef west = new(leg.Id, BoxFeature.Face(BoxFace.West));

        // Along Z between a face and a vertex that both fix Z: legal, and it holds at zero.
        AxisDistance alongZ = new(RelationshipId.New(), bottom, legTop, Axis.Z, Length.Zero);
        Assert.Null(PlaceRules.Refusal(sketch, alongZ));
        SketchAssert.IsConsistent(sketch.WithRelationship(alongZ));

        // Along X from a face that fixes only Z: refused.
        AxisDistance alongX = new(RelationshipId.New(), bottom, legTop, Axis.X, Length.Zero);
        Assert.Contains(
            "needs both places to fix X",
            Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(sketch, new AddRelationship(alongX))).Detail!.Message,
            StringComparison.Ordinal);

        // Centred along Z between the leg's two caps: the top's bottom face is not; the leg's centre is.
        FeatureRef legBottom = new(leg.Id, BoxFeature.Face(BoxFace.Bottom));
        FeatureRef legCap = new(leg.Id, BoxFeature.Face(BoxFace.Top));
        Centered centred = new(RelationshipId.New(), new CenterRef(leg.Id), legBottom, legCap, Axis.Z);
        Assert.Null(PlaceRules.Refusal(sketch, centred));
        SketchAssert.IsConsistent(sketch.WithRelationship(centred));
        Assert.Equal(
            ValidationErrorKind.PlacesNotComparable,
            PlaceRules.Refusal(sketch, centred with { Middle = west })!.Kind);
    }

    // ---- The checker over three axes ----------------------------------------------------------

    [Fact]
    public void TheCheckerJudgesAFlushOfTwoCapsOnZWithZeroTolerance()
    {
        Box top = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Box leg = Lying(2, "Leg", Point3.Origin, 2, 2, 16);
        Flush flush = new(
            RelationshipId.New(),
            new FeatureRef(top.Id, BoxFeature.Face(BoxFace.Bottom)),
            new FeatureRef(leg.Id, BoxFeature.Face(BoxFace.Top)));
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg).WithRelationship(flush);

        Assert.True(RelationshipChecker.Check(sketch).AllHold);
        Assert.True(RelationshipChecker.IsExactClass(sketch, flush));

        // A leg one unit short: out by exactly one unit, exact class, whatever the tolerance.
        Sketch shortLeg = sketch.WithEntity(leg with { Depth = leg.Depth - new Length(1) });
        Violation violation = Assert.Single(RelationshipChecker.Check(shortLeg, new Tolerances(Length.Inches(1), Angle.Degrees(1))).Violations);
        Assert.Equal(new Length(1), violation.Residual);
        Assert.True(violation.Exact);
    }

    [Fact]
    public void TheCheckerJudgesATippedBoxsFacesThroughItsOrientation()
    {
        // A board standing on its north face: its drawn top faces south, so a flush between that
        // top and a wall's north face is a flush on world Y.
        Box board = Lying(1, "Board", Point3.Inches(0, 10, 0), 30, 4, 1, BoxFace.North);
        Box wall = Lying(2, "Wall", Point3.Inches(0, 0, 0), 30, 9, 96);
        FeatureRef boardTop = new(board.Id, BoxFeature.Face(BoxFace.Top));
        Sketch sketch = Sketch.Empty.WithEntity(board).WithEntity(wall);

        // North up: local +Z -> world -Y, so the drawn top is at y = 10 - 1.
        Assert.Equal(Place.On(Axis.Y, Length.Inches(9)), sketch.PlaceOf(boardTop));

        Flush flush = new(RelationshipId.New(), new FeatureRef(wall.Id, BoxFeature.Face(BoxFace.North)), boardTop);
        Assert.Null(PlaceRules.Refusal(sketch, flush));
        SketchAssert.IsConsistent(sketch.WithRelationship(flush));
    }

    [Fact]
    public void TheCheckerCannotEvaluateAPairingWithNothingInCommon()
    {
        // Stored anyway — by a file the loader would refuse — a flush with nothing to compare is
        // never "satisfied".
        Box a = Lying(1, "A", Point3.Origin, 10, 4, 1);
        Node node = new(SketchBuilder.EntityIdAt(2), LayerId.Default, Point2.Origin);
        Sketch sketch = Sketch.Empty.WithEntity(a).WithEntity(node)
            .WithRelationship(new AxisDistance(RelationshipId.New(), new FeatureRef(a.Id, BoxFeature.Face(BoxFace.Top)), new NodeRef(node.Id), Axis.Z, Length.Zero))
            .WithRelationship(new Coincident(RelationshipId.New(), new FeatureRef(a.Id, BoxFeature.Face(BoxFace.Top)), new NodeRef(node.Id)))
            .WithRelationship(new Centered(RelationshipId.New(), new NodeRef(node.Id), new NodeRef(node.Id), new NodeRef(node.Id), Axis.Z))
            .WithRelationship(new Flush(RelationshipId.New(), new FeatureRef(a.Id, BoxFeature.Face(BoxFace.Top)), new FeatureRef(a.Id, BoxFeature.Edge(BoxFace.Top, BoxFace.North))));

        Assert.Equal(4, RelationshipChecker.Check(sketch).Violations.Count);
        Assert.Equal(4, sketch.Validate().Errors.Count(error => error.Kind == ValidationErrorKind.PlacesNotComparable));
    }

    [Fact]
    public void APlaceTooFarOutToAddUpIsLeftToTheCheckerRatherThanThrownFromValidate()
    {
        // The loader validates, then checks under a guard that reports an overflow as a file out
        // of range. The legality rule reads places too, so it must not throw first.
        Box far = new(SketchBuilder.EntityIdAt(1), LayerId.Default, new Point3(new Length(long.MaxValue - 10), Length.Zero, Length.Zero), Length.Inches(1), Length.Inches(1), Length.Inches(1), BoxFace.Top, Angle.Zero);
        Box near = Lying(2, "Near", Point3.Origin, 1, 1, 1);
        Sketch sketch = Sketch.Empty.WithEntity(far).WithEntity(near)
            .WithRelationship(new Flush(RelationshipId.New(), TestRefs.Edge(far.Id, BoxEdge.East), TestRefs.Edge(near.Id, BoxEdge.West)))
            .WithEntity(DimensionOf(3, new AxisMeasurand(TestRefs.Corner(far.Id, BoxCorner.NorthEast), TestRefs.Corner(near.Id, BoxCorner.SouthWest), Axis.X)));

        Assert.True(sketch.Validate().IsValid);
        Assert.Throws<OverflowException>(() => RelationshipChecker.Check(sketch));
    }

    // ---- §2.5: features are the blank's ------------------------------------------------------

    [Fact]
    public void AFeatureAtAClippedCornerIsTheBlanksVirtualFeature()
    {
        // A 2" x 2" corner cut off the south-west: the upright there, the vertex on it, and the
        // west face all stay where the blank has them.
        SketchBuilder builder = new();
        EntityId clipped = builder.AddBlank(10, 20, 30, 12, new CornerCut(BoxCorner.SouthWest, Length.Inches(2), Length.Inches(2)));
        EntityId plain = builder.AddBlank(10, 20, 30, 12);
        Sketch sketch = builder.Sketch;

        foreach (BoxFeature feature in AllFeatures())
        {
            Assert.Equal(
                sketch.PlaceOf(new FeatureRef(plain, feature)),
                sketch.PlaceOf(new FeatureRef(clipped, feature)));
        }

        Assert.Equal(new Place(Length.Inches(10), Length.Inches(20), null), sketch.PlaceOf(TestRefs.Corner(clipped, BoxCorner.SouthWest)));
        Assert.Equal(
            new Place(Length.Inches(10), Length.Inches(20), new Length(768)),
            sketch.PlaceOf(new FeatureRef(clipped, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top))));

        // A flush on the clipped west face holds on the blank's face plane.
        EntityId neighbour = builder.AddBox(-5, 20, 10, 12);
        Solved solved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(
            builder.Sketch,
            new AddRelationship(new Flush(RelationshipId.New(), TestRefs.Edge(clipped, BoxEdge.West), TestRefs.Edge(neighbour, BoxEdge.East)))));
        Assert.Equal(Length.Zero, solved.Sketch.Find<Box>(neighbour)!.Anchor.X);
        SketchAssert.IsConsistent(solved.Sketch);
    }

    // ---- Invariant 12 ------------------------------------------------------------------------

    [Fact]
    public void Invariant12_AFeatureNamingNoFacesIsRefusedEverywhere()
    {
        SketchBuilder builder = new();
        EntityId box = builder.AddBox(0, 0, 10, 4);
        EntityId node = builder.AddNode(0, 0);
        FeatureRef nothing = new(box, default);
        Coincident coincident = new(RelationshipId.New(), nothing, new NodeRef(node));

        Assert.Throws<InvalidOperationException>(() => builder.Sketch.PlaceOf(nothing));
        Assert.Equal(
            new Rejected(RejectionReason.DanglingReference),
            DirectUpdater.Instance.Apply(builder.Sketch, new AddRelationship(coincident)));

        ValidationError error = Assert.Single(builder.Sketch.WithRelationship(coincident).Validate().Errors);
        Assert.Equal(ValidationErrorKind.NotAFeature, error.Kind);
    }

    // ---- Invariant 13 ------------------------------------------------------------------------

    private static Dimension DimensionOf(int index, Measurand measures) => new(
        SketchBuilder.EntityIdAt(index), LayerId.Default, measures, null, new DimensionPlacement(Length.Inches(2), DimensionSide.North));

    public static TheoryData<BoxFace, string, bool> SizesInThePlan => new()
    {
        { BoxFace.Top, "width", true },
        { BoxFace.Top, "height", true },
        { BoxFace.Top, "depth", false },
        { BoxFace.Bottom, "depth", false },
        { BoxFace.East, "width", false },
        { BoxFace.East, "depth", true },
        { BoxFace.West, "width", false },
        { BoxFace.North, "height", false },
        { BoxFace.North, "depth", true },
        { BoxFace.South, "height", false },
    };

    [Theory]
    [MemberData(nameof(SizesInThePlan))]
    public void Invariant13_ASizeDimensionMustLieInThePlanOnceTheBoxIsTurned(BoxFace faceUp, string size, bool inThePlan)
    {
        Box box = Lying(1, "Leg", Point3.Origin, 16, 2, 3, faceUp);
        ParamRef param = size switch
        {
            "width" => new BoxWidthRef(box.Id),
            "height" => new BoxHeightRef(box.Id),
            _ => new BoxDepthRef(box.Id),
        };

        Sketch sketch = Sketch.Empty.WithEntity(box);
        Dimension dimension = DimensionOf(2, new ParamMeasurand(param));
        UpdateResult result = DirectUpdater.Instance.Apply(sketch, new AddEntity(dimension));

        if (inThePlan)
        {
            Assert.IsType<Solved>(result);
            Assert.True(sketch.WithEntity(dimension).Validate().IsValid);
        }
        else
        {
            Rejected rejected = Assert.IsType<Rejected>(result);
            Assert.Equal(RejectionReason.UnsupportedRequest, rejected.Reason);
            Assert.Equal(ValidationErrorKind.MeasurandLeavesThePlan, rejected.Detail!.Kind);
            Assert.Contains($"the {size} of Leg", rejected.Detail.Message, StringComparison.Ordinal);
            Assert.Equal(
                ValidationErrorKind.MeasurandLeavesThePlan,
                Assert.Single(sketch.WithEntity(dimension).Validate().Errors).Kind);
        }
    }

    [Fact]
    public void Invariant13_AnAxisDimensionMeasuresAlongXOrYBetweenPlacesThatFixIt()
    {
        Box top = Lying(1, "Top", Point3.Inches(0, 0, 16), 40, 20, 1);
        Box leg = Lying(2, "Leg", Point3.Origin, 2, 2, 16);
        Sketch sketch = Sketch.Empty.WithEntity(top).WithEntity(leg);
        FeatureRef topCorner = TestRefs.Corner(top.Id, BoxCorner.SouthWest);
        FeatureRef legCorner = TestRefs.Corner(leg.Id, BoxCorner.SouthEast);
        FeatureRef underside = new(top.Id, BoxFeature.Face(BoxFace.Bottom));

        // Between two uprights along X: in the plan.
        Assert.IsType<Solved>(DirectUpdater.Instance.Apply(
            sketch, new AddEntity(DimensionOf(3, new AxisMeasurand(topCorner, legCorner, Axis.X)))));

        // Along Z, between the underside and a vertex that both fix Z: out of the plan.
        Rejected alongZ = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(
            sketch,
            new AddEntity(DimensionOf(3, new AxisMeasurand(underside, new FeatureRef(leg.Id, BoxFeature.Vertex(BoxCorner.SouthWest, BoxLevel.Top)), Axis.Z)))));
        Assert.Equal(RejectionReason.UnsupportedRequest, alongZ.Reason);
        Assert.Equal(ValidationErrorKind.MeasurandLeavesThePlan, alongZ.Detail!.Kind);

        // Along X from a place that does not fix X: nothing to measure.
        Rejected unfixed = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(
            sketch, new AddEntity(DimensionOf(3, new AxisMeasurand(underside, legCorner, Axis.X)))));
        Assert.Contains("Top's bottom face fixes Z", unfixed.Detail!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invariant13_IsJudgedOnTheSketchAsItWouldBeAfterATurn()
    {
        // A reorientation request (§10 step 4) asks the rule of the sketch it would write: the same
        // dimension that is fine on a box lying as drawn leaves the plan once the box stands East.
        Box drawn = Lying(1, "Leg", Point3.Origin, 16, 2, 3);
        Dimension width = DimensionOf(2, new ParamMeasurand(new BoxWidthRef(drawn.Id)));
        Sketch sketch = Sketch.Empty.WithEntity(drawn).WithEntity(width);

        Assert.Null(PlaceRules.MeasurandRefusal(sketch, width));
        Assert.NotNull(PlaceRules.MeasurandRefusal(sketch.WithEntity(drawn with { FaceUp = BoxFace.East }), width));
        Assert.Null(PlaceRules.MeasurandRefusal(sketch.WithEntity(drawn with { FaceUp = BoxFace.Bottom }), width));
        Assert.Null(PlaceRules.MeasurandRefusal(sketch.WithEntity(drawn with { Rotation = Angle.Right }), width));
    }

    // ---- Place and words ---------------------------------------------------------------------

    [Fact]
    public void APlaceSaysWhatItFixesInWords()
    {
        Assert.Equal("nothing", default(Place).AxesInWords());
        Assert.Equal("Z", Place.On(Axis.Z, Length.Zero).AxesInWords());
        Assert.Equal("X and Y", new Place(Length.Zero, Length.Zero, null).AxesInWords());
        Assert.Equal("X, Y and Z", new Place(Length.Zero, Length.Zero, Length.Zero).AxesInWords());
        Assert.Equal("Place(nothing)", default(Place).ToString());
        Assert.Equal($"Place(Y = {Length.Inches(1)}, Z = {Length.Inches(2)})", new Place(null, Length.Inches(1), Length.Inches(2)).ToString());

        Assert.Throws<InvalidOperationException>(() => Place.On(Axis.X, Length.Zero)[Axis.Y]);
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Place).Coordinate((Axis)3));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(Place).With((Axis)3, Length.Zero));
    }

    [Fact]
    public void AFeatureIsNamedInTheBlanksOwnWords()
    {
        Assert.Equal("north face", PlaceRules.InWords(BoxFeature.Face(BoxFace.North)));
        Assert.Equal("top face", PlaceRules.InWords(BoxFeature.Face(BoxFace.Top)));
        Assert.Equal("south-west edge", PlaceRules.InWords(BoxFeature.LocalUpright(BoxCorner.SouthWest)));
        Assert.Equal("bottom east edge", PlaceRules.InWords(BoxFeature.Edge(BoxFace.East, BoxFace.Bottom)));
        Assert.Equal("top north-east corner", PlaceRules.InWords(BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top)));
        Assert.Equal("feature naming no faces", PlaceRules.InWords(default));

        SketchBuilder builder = new();
        EntityId node = builder.AddNode(0, 0);
        EntityId segment = builder.AddSegment(node, node);
        EntityId box = builder.AddBox(0, 0, 1, 1);
        Assert.Equal($"node {node}", PlaceRules.Describe(builder.Sketch, new NodeRef(node)));
        Assert.Equal($"segment {segment}", PlaceRules.Describe(builder.Sketch, new SegmentRef(segment)));
        Assert.Equal($"{box}'s centre", PlaceRules.Describe(builder.Sketch, new CenterRef(box)));
    }
}
