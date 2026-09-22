namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// <see cref="Box.Outline"/> against golden cases 1 to 4 of
/// docs/design/shaped-parts-model.md §9.1: a 48&#x2033; &#xD7; 24&#x2033; blank, every cut kind at
/// every site it can be made at, every point exact.
/// </summary>
public class OutlineTests
{
    /// <summary>The blank every case below starts from, anchored at the origin.</summary>
    private static Box Blank(params Cut[] cuts) => Blank(Point2.Origin, Angle.Zero, cuts);

    private static Box Blank(Point2 anchor, Angle rotation, params Cut[] cuts)
        => new(SketchBuilder.EntityIdAt(1), LayerId.Default, anchor, Length.Inches(48), Length.Inches(24), rotation)
        {
            Cuts = [.. cuts],
        };

    private static StraightSegment Straight(long fromX, long fromY, long toX, long toY)
        => new(Point2.Inches(fromX, fromY), Point2.Inches(toX, toY));

    private static void Expect(Box box, params OutlineSegment[] segments)
        => Assert.Equal(segments, box.Outline().Segments);

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden1_ABlankWithNoCutsIsFourStraightSegmentsThroughItsCorners()
    {
        Expect(
            Blank(),
            Straight(0, 0, 48, 0),
            Straight(48, 0, 48, 24),
            Straight(48, 24, 0, 24),
            Straight(0, 24, 0, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden2_ACornerCutAtTheSouthWest()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(5))),
            Straight(3, 0, 48, 0),
            Straight(48, 0, 48, 24),
            Straight(48, 24, 0, 24),
            Straight(0, 24, 0, 5),
            Straight(0, 5, 3, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden2_ACornerCutAtTheSouthEast()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.SouthEast, Length.Inches(3), Length.Inches(5))),
            Straight(0, 0, 45, 0),
            Straight(45, 0, 48, 5),
            Straight(48, 5, 48, 24),
            Straight(48, 24, 0, 24),
            Straight(0, 24, 0, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden2_ACornerCutAtTheNorthEast()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.NorthEast, Length.Inches(3), Length.Inches(5))),
            Straight(0, 0, 48, 0),
            Straight(48, 0, 48, 19),
            Straight(48, 19, 45, 24),
            Straight(45, 24, 0, 24),
            Straight(0, 24, 0, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden2_ACornerCutAtTheNorthWest()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.NorthWest, Length.Inches(3), Length.Inches(5))),
            Straight(0, 0, 48, 0),
            Straight(48, 0, 48, 24),
            Straight(48, 24, 3, 24),
            Straight(3, 24, 0, 19),
            Straight(0, 19, 0, 0));
    }

    /// <summary>
    /// The sample of §8: a 1&#x2033; radius at all four corners of the coffee table's top. Every
    /// tangent point and every arc centre is a multiple of 1024 units; nothing rounds.
    /// </summary>
    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden2_ARoundedCornerAtEverySiteHasItsCentreInFromTheCornerOnBothAxes()
    {
        Box top = Blank(
            new RoundedCorner(BoxCorner.SouthWest, Length.Inches(1)),
            new RoundedCorner(BoxCorner.SouthEast, Length.Inches(1)),
            new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1)),
            new RoundedCorner(BoxCorner.NorthWest, Length.Inches(1)));

        Expect(
            top,
            Straight(1, 0, 47, 0),
            new ArcByCenter(Point2.Inches(47, 0), Point2.Inches(48, 1), Point2.Inches(47, 1)),
            Straight(48, 1, 48, 23),
            new ArcByCenter(Point2.Inches(48, 23), Point2.Inches(47, 24), Point2.Inches(47, 23)),
            Straight(47, 24, 1, 24),
            new ArcByCenter(Point2.Inches(1, 24), Point2.Inches(0, 23), Point2.Inches(1, 23)),
            Straight(0, 23, 0, 1),
            new ArcByCenter(Point2.Inches(0, 1), Point2.Inches(1, 0), Point2.Inches(1, 1)));

        // The eight tangent points §8 derives by hand, in units: the polygon invariant 9 measures.
        Point2[] tangents =
        [
            new(Length.Zero, new Length(1024)),
            new(Length.Zero, new Length(23552)),
            new(new Length(1024), Length.Zero),
            new(new Length(1024), new Length(24576)),
            new(new Length(48128), Length.Zero),
            new(new Length(48128), new Length(24576)),
            new(new Length(49152), new Length(1024)),
            new(new Length(49152), new Length(23552)),
        ];

        Assert.Equal(
            tangents,
            top.Outline().Vertices.OrderBy(point => point.X.Units).ThenBy(point => point.Y.Units));
    }

    [Trait("Feature", "GEO-007")]
    [Theory]
    [InlineData(BoxEdge.South)]
    [InlineData(BoxEdge.East)]
    [InlineData(BoxEdge.North)]
    [InlineData(BoxEdge.West)]
    public void Golden2_AnOutwardCurveStopsTheAdjacentEdgesShortAndPassesThroughTheMiddleOfItsOwn(BoxEdge edge)
    {
        Box box = Blank(new CurvedEdge(edge, Bow.Outward, Length.Inches(2)));

        switch (edge)
        {
            case BoxEdge.South:
                Expect(
                    box,
                    new ArcThrough(Point2.Inches(0, 2), Point2.Inches(24, 0), Point2.Inches(48, 2)),
                    Straight(48, 2, 48, 24),
                    Straight(48, 24, 0, 24),
                    Straight(0, 24, 0, 2));
                break;

            case BoxEdge.East:
                Expect(
                    box,
                    Straight(0, 0, 46, 0),
                    new ArcThrough(Point2.Inches(46, 0), Point2.Inches(48, 12), Point2.Inches(46, 24)),
                    Straight(46, 24, 0, 24),
                    Straight(0, 24, 0, 0));
                break;

            case BoxEdge.North:
                Expect(
                    box,
                    Straight(0, 0, 48, 0),
                    Straight(48, 0, 48, 22),
                    new ArcThrough(Point2.Inches(48, 22), Point2.Inches(24, 24), Point2.Inches(0, 22)),
                    Straight(0, 22, 0, 0));
                break;

            default:
                Expect(
                    box,
                    Straight(2, 0, 48, 0),
                    Straight(48, 0, 48, 24),
                    Straight(48, 24, 2, 24),
                    new ArcThrough(Point2.Inches(2, 24), Point2.Inches(0, 12), Point2.Inches(2, 0)));
                break;
        }
    }

    [Trait("Feature", "GEO-007")]
    [Theory]
    [InlineData(BoxEdge.South)]
    [InlineData(BoxEdge.East)]
    [InlineData(BoxEdge.North)]
    [InlineData(BoxEdge.West)]
    public void Golden2_AnInwardCurveKeepsItsCornersAndSagsToTheMiddleMovedInByTheDepth(BoxEdge edge)
    {
        Box box = Blank(new CurvedEdge(edge, Bow.Inward, Length.Inches(2)));

        switch (edge)
        {
            case BoxEdge.South:
                Expect(
                    box,
                    new ArcThrough(Point2.Inches(0, 0), Point2.Inches(24, 2), Point2.Inches(48, 0)),
                    Straight(48, 0, 48, 24),
                    Straight(48, 24, 0, 24),
                    Straight(0, 24, 0, 0));
                break;

            case BoxEdge.East:
                Expect(
                    box,
                    Straight(0, 0, 48, 0),
                    new ArcThrough(Point2.Inches(48, 0), Point2.Inches(46, 12), Point2.Inches(48, 24)),
                    Straight(48, 24, 0, 24),
                    Straight(0, 24, 0, 0));
                break;

            case BoxEdge.North:
                Expect(
                    box,
                    Straight(0, 0, 48, 0),
                    Straight(48, 0, 48, 24),
                    new ArcThrough(Point2.Inches(48, 24), Point2.Inches(24, 22), Point2.Inches(0, 24)),
                    Straight(0, 24, 0, 0));
                break;

            default:
                Expect(
                    box,
                    Straight(0, 0, 48, 0),
                    Straight(48, 0, 48, 24),
                    Straight(48, 24, 0, 24),
                    new ArcThrough(Point2.Inches(0, 24), Point2.Inches(2, 12), Point2.Inches(0, 0)));
                break;
        }
    }

    /// <summary>
    /// The one rounding in the outline: a curved edge's middle on an edge that is an odd number of
    /// units long, which rounds by half a unit exactly as <see cref="Box.Center"/> does.
    /// </summary>
    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden2_TheMiddleOfAnOddEdgeRoundsByHalfAUnitAndNothingElseDoes()
    {
        Box odd = new(
            SketchBuilder.EntityIdAt(1),
            LayerId.Default,
            Point2.Origin,
            new Length(49153),
            Length.Inches(24),
            Angle.Zero)
        {
            Cuts = [new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2))],
        };

        ArcThrough arc = Assert.IsType<ArcThrough>(odd.Outline().Segments[2]);

        Assert.Equal(new Length(49153).Divide(2, Rounding.HalfToEven), arc.Through.X);
        Assert.Equal(new Length(24576), arc.Through.Y);
        Assert.Equal(new Length(49153), arc.From.X);
        Assert.Equal(Length.Zero, arc.To.X);
    }

    [Trait("Feature", "GEO-007")]
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Golden3_ARightAngleRotationIsTheRotationZeroOutlineTurnedAboutTheAnchor(int quarterTurns)
    {
        Cut[] cuts =
        [
            new CornerCut(BoxCorner.SouthEast, Length.Inches(3), Length.Inches(5)),
            new RoundedCorner(BoxCorner.NorthEast, Length.Inches(2)),
            new CurvedEdge(BoxEdge.West, Bow.Inward, Length.Inches(4)),
        ];

        Point2 anchor = Point2.Inches(5, 7);
        Angle rotation = Angle.Zero.Rotate90(quarterTurns);

        Outline upright = Blank(anchor, Angle.Zero, cuts).Outline();
        Outline turned = Blank(anchor, rotation, cuts).Outline();

        Point2 Turn(Point2 point) => anchor + (point - anchor).Rotate(rotation);

        Assert.Equal(
            upright.Segments.Select(segment => (OutlineSegment)(segment switch
            {
                ArcByCenter arc => new ArcByCenter(Turn(arc.From), Turn(arc.To), Turn(arc.Center)),
                ArcThrough arc => new ArcThrough(Turn(arc.From), Turn(arc.Through), Turn(arc.To)),
                _ => new StraightSegment(Turn(segment.From), Turn(segment.To)),
            })),
            turned.Segments);
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden4_AMitreRunsTheWholeEndAndTheConsumedEdgeIsOmitted()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.SouthEast, Length.Inches(24), Length.Inches(24))),
            Straight(0, 0, 24, 0),
            Straight(24, 0, 48, 24),
            Straight(48, 24, 0, 24),
            Straight(0, 24, 0, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden4_ATaperKeepsWhatIsLeftOfBothEdges()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.SouthEast, Length.Inches(40), Length.Inches(4))),
            Straight(0, 0, 8, 0),
            Straight(8, 0, 48, 4),
            Straight(48, 4, 48, 24),
            Straight(48, 24, 0, 24),
            Straight(0, 24, 0, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden4_ADiagonalLeavesATriangleOfThreeSegments()
    {
        Expect(
            Blank(new CornerCut(BoxCorner.NorthEast, Length.Inches(48), Length.Inches(24))),
            Straight(0, 0, 48, 0),
            Straight(48, 0, 0, 24),
            Straight(0, 24, 0, 0));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Golden4_TwoCutsMeetingInAPointShareTheirPointAndOmitTheEdgeBetweenThem()
    {
        Expect(
            Blank(
                new CornerCut(BoxCorner.SouthEast, Length.Inches(6), Length.Inches(12)),
                new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(12))),
            Straight(0, 0, 42, 0),
            Straight(42, 0, 48, 12),
            Straight(48, 12, 42, 24),
            Straight(42, 24, 0, 24),
            Straight(0, 24, 0, 0));
    }
}
