namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The cut model and invariants 5 to 9 of docs/design/shaped-parts-model.md §1.6: golden cases 5
/// and 6 of §9.1. A box that fails any of them is an invalid sketch, and
/// <see cref="AddEntity"/> of one is rejected.
/// </summary>
public class CutTests
{
    private static readonly DirectUpdater Updater = DirectUpdater.Instance;

    /// <summary>A 48&#x2033; &#xD7; 24&#x2033; blank with the given cuts.</summary>
    private static Box Blank(params Cut[] cuts)
        => Box.AsDrawn(SketchBuilder.EntityIdAt(1), LayerId.Default, Point2.Origin, Length.Inches(48), Length.Inches(24), Box.DefaultDepth, Angle.Zero) with
        {
            Cuts = [.. cuts],
        };

    private static Sketch SketchOf(Box box) => Sketch.Empty.WithEntity(box);

    /// <summary>The kinds of problem a box's cuts have, in the order Validate reports them.</summary>
    private static ValidationErrorKind[] Problems(Box box)
        => [.. SketchOf(box).Validate().Errors.Select(error => error.Kind)];

    /// <summary>The box is invalid for this reason, it says so naming the box, and AddEntity refuses it.</summary>
    private static void IsRefused(Box box, ValidationErrorKind kind, RejectionReason reason, string site)
    {
        ValidationResult validation = SketchOf(box).Validate();

        Assert.False(validation.IsValid);
        // One bad cut can trip several checks; what matters is that this one is reported, and
        // that it names the box and the site it is at.
        Assert.Contains(
            validation.Errors,
            error => error.Kind == kind
                     && error.Message.Contains(box.Id.ToString(), StringComparison.Ordinal)
                     && error.Message.Contains(site, StringComparison.Ordinal));

        Rejected rejected = Assert.IsType<Rejected>(Updater.Apply(Sketch.Empty, new AddEntity(box)));
        Assert.Equal(reason, rejected.Reason);
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void ABlankWithCutsThatFitIsValidAndCanBeAdded()
    {
        Box box = Blank(
            new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(5)),
            new RoundedCorner(BoxCorner.SouthEast, Length.Inches(2)),
            new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(4)));

        SketchAssert.IsConsistent(SketchOf(box));
        Assert.IsType<Solved>(Updater.Apply(Sketch.Empty, new AddEntity(box)));
    }

    // -----------------------------------------------------------------------------------------
    // Golden 6: the cuts are held in site order, compared by value, and a repeated site is caught.
    // -----------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Invariant5_CutsGivenOutOfSiteOrderAreHeldInSiteOrder()
    {
        Box box = Blank(
            new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(2)),
            new CornerCut(BoxCorner.SouthEast, Length.Inches(3), Length.Inches(5)),
            new CurvedEdge(BoxEdge.South, Bow.Outward, Length.Inches(2)),
            new RoundedCorner(BoxCorner.SouthWest, Length.Inches(1)));

        Assert.Equal(
            [
                CutSite.Corner(BoxCorner.SouthWest),
                CutSite.Corner(BoxCorner.SouthEast),
                CutSite.Edge(BoxEdge.South),
                CutSite.Edge(BoxEdge.North),
            ],
            box.Cuts.Select(cut => cut.Site));
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Invariant5_TwoBoxesWithTheSameCutsAreEqualByValueWhateverOrderTheyWereGivenIn()
    {
        Box one = Blank(
            new RoundedCorner(BoxCorner.NorthWest, Length.Inches(1)),
            new CornerCut(BoxCorner.SouthEast, Length.Inches(3), Length.Inches(5)));
        Box other = Blank(
            new CornerCut(BoxCorner.SouthEast, Length.Inches(3), Length.Inches(5)),
            new RoundedCorner(BoxCorner.NorthWest, Length.Inches(1)));

        Assert.Equal(one, other);
        Assert.Equal(one.GetHashCode(), other.GetHashCode());
        Assert.Single(new HashSet<Box> { one, other });

        // And a different value is a different box, which is what the cut list's grouping needs.
        Assert.NotEqual(one, Blank(new RoundedCorner(BoxCorner.NorthWest, Length.Inches(1))));
        Assert.NotEqual(one, Blank());
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant5_ASiteUsedTwiceIsReported()
        => IsRefused(
            Blank(
                new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(5)),
                new RoundedCorner(BoxCorner.SouthWest, Length.Inches(1))),
            ValidationErrorKind.DuplicateCutSite,
            RejectionReason.CutSiteTaken,
            "SouthWest corner");

    // -----------------------------------------------------------------------------------------
    // Golden 5: each invariant violated in turn.
    // -----------------------------------------------------------------------------------------

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant6_ACurvedEdgeClaimsBothOfItsCorners()
        => IsRefused(
            Blank(
                new RoundedCorner(BoxCorner.NorthEast, Length.Inches(1)),
                new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2))),
            ValidationErrorKind.CutSiteTaken,
            RejectionReason.CutSiteTaken,
            "NorthEast corner");

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant6_TwoAdjacentEdgesCannotBothBeCurved()
        => IsRefused(
            Blank(
                new CurvedEdge(BoxEdge.East, Bow.Inward, Length.Inches(2)),
                new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(2))),
            ValidationErrorKind.CutSiteTaken,
            RejectionReason.CutSiteTaken,
            "North edge");

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant6_TwoOppositeEdgesMayBothBeCurved()
        => SketchAssert.IsConsistent(SketchOf(Blank(
            new CurvedEdge(BoxEdge.South, Bow.Inward, Length.Inches(2)),
            new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(2)))));

    [Trait("Feature", "GEO-008")]
    [Theory]
    [InlineData(0, 5)]
    [InlineData(3, 0)]
    [InlineData(-3, 5)]
    public void Invariant7_ASetbackMustBePositive(long alongX, long alongY)
        => IsRefused(
            Blank(new CornerCut(BoxCorner.SouthWest, Length.Inches(alongX), Length.Inches(alongY))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "SouthWest corner");

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant7_ARadiusMustFitTheShorterSide()
        => IsRefused(
            Blank(new RoundedCorner(BoxCorner.SouthWest, Length.Inches(30))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "SouthWest corner");

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant7_AnOutwardCurveMayReachTheFarEdgeLineButNoFurther()
    {
        SketchAssert.IsConsistent(SketchOf(Blank(new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(24)))));

        IsRefused(
            Blank(new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(25))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "North edge");
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant7_AnInwardCurveMayNotReachTheFarEdgeLine()
    {
        SketchAssert.IsConsistent(SketchOf(Blank(new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(23)))));

        IsRefused(
            Blank(new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(24))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "North edge");
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant8_TwoCutsOnOneEdgeMayMeetInAPointButMayNotOverlap()
    {
        // Equality is allowed: the two cuts meet at the middle of the east edge.
        SketchAssert.IsConsistent(SketchOf(Blank(
            new CornerCut(BoxCorner.SouthEast, Length.Inches(6), Length.Inches(12)),
            new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(12)))));

        IsRefused(
            Blank(
                new CornerCut(BoxCorner.SouthEast, Length.Inches(6), Length.Inches(13)),
                new CornerCut(BoxCorner.NorthEast, Length.Inches(6), Length.Inches(12))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "East edge");
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant8_AnOutwardCurveClaimsItsDepthFromTheEdgesItStartsOn()
        => IsRefused(
            Blank(
                new CornerCut(BoxCorner.SouthEast, Length.Inches(6), Length.Inches(23)),
                new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(2))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "East edge");

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant8_ACurveAndTheOppositeEdgesCutsShareTheBlankBetweenThem()
    {
        // An outward curve may just touch what faces it: 12 + 12 is the blank's 24.
        SketchAssert.IsConsistent(SketchOf(Blank(
            new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(12)),
            new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(12)))));

        IsRefused(
            Blank(
                new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(13)),
                new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(12))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "SouthWest corner");
    }

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant8_AnInwardCurveMustLeaveMaterialWhateverFacesIt()
        => IsRefused(
            Blank(
                new CornerCut(BoxCorner.SouthWest, Length.Inches(3), Length.Inches(12)),
                new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(12))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "SouthWest corner");

    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant8_TwoScallopsThatMeetInTheMiddleLeaveTwoLobesAndNoPart()
    {
        // Outward against outward may meet; inward against anything may not.
        SketchAssert.IsConsistent(SketchOf(Blank(
            new CurvedEdge(BoxEdge.South, Bow.Outward, Length.Inches(12)),
            new CurvedEdge(BoxEdge.North, Bow.Outward, Length.Inches(12)))));

        IsRefused(
            Blank(
                new CurvedEdge(BoxEdge.South, Bow.Inward, Length.Inches(12)),
                new CurvedEdge(BoxEdge.North, Bow.Inward, Length.Inches(12))),
            ValidationErrorKind.CutDoesNotFit,
            RejectionReason.CutDoesNotFit,
            "North edge");
    }

    /// <summary>
    /// Invariant 9 is the catch-all: two full diagonals at opposite corners pass every edge
    /// budget and leave nothing at all.
    /// </summary>
    [Trait("Feature", "GEO-008")]
    [Fact]
    public void Invariant9_TwoFullDiagonalsLeaveNothingAndAreCaughtByTheAreaAndNotByTheBudgets()
    {
        Box box = Blank(
            new CornerCut(BoxCorner.SouthWest, Length.Inches(48), Length.Inches(24)),
            new CornerCut(BoxCorner.NorthEast, Length.Inches(48), Length.Inches(24)));

        Assert.Equal([ValidationErrorKind.NonPositiveArea], Problems(box));

        IsRefused(
            box,
            ValidationErrorKind.NonPositiveArea,
            RejectionReason.CutDoesNotFit,
            "SouthWest corner");
    }

    [Trait("Feature", "GEO-007")]
    [Fact]
    public void Invariant9_ASingleDiagonalLeavesHalfTheBlank()
    {
        Box triangle = Blank(new CornerCut(BoxCorner.NorthEast, Length.Inches(48), Length.Inches(24)));

        SketchAssert.IsConsistent(SketchOf(triangle));
        Assert.Equal(
            Area.Of(Length.Inches(48), Length.Inches(24)),
            Area.TwiceSignedPolygon(triangle.Outline().Vertices));
    }

    // -----------------------------------------------------------------------------------------
    // CutSite itself: the fixed ordering everything above depends on.
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void ACutSiteSortsCornersBeforeEdgesInTheFixedOrder()
    {
        CutSite[] sites =
        [
            CutSite.Corner(BoxCorner.SouthWest),
            CutSite.Corner(BoxCorner.SouthEast),
            CutSite.Corner(BoxCorner.NorthEast),
            CutSite.Corner(BoxCorner.NorthWest),
            CutSite.Edge(BoxEdge.South),
            CutSite.Edge(BoxEdge.East),
            CutSite.Edge(BoxEdge.North),
            CutSite.Edge(BoxEdge.West),
        ];

        Assert.Equal(Enumerable.Range(0, 8), sites.Select(site => site.Order));
        Assert.Equal(sites, sites.OrderByDescending(site => site.Order).Order().ToArray());

        Assert.True(sites[0] < sites[1]);
        Assert.True(sites[1] <= sites[1]);
        Assert.True(sites[7] > sites[0]);
        Assert.True(sites[7] >= sites[7]);
        Assert.Equal(1, sites[4].CompareTo(sites[3]));
    }

    [Fact]
    public void ACutSiteKnowsWhetherItIsACornerOrAnEdgeAndSaysWhich()
    {
        CutSite corner = CutSite.Corner(BoxCorner.NorthWest);
        CutSite edge = CutSite.Edge(BoxEdge.South);

        Assert.True(corner.IsCorner);
        Assert.Equal(BoxCorner.NorthWest, corner.AsCorner);
        Assert.Null(corner.AsEdge);
        Assert.Equal("NorthWest corner", corner.ToString());

        Assert.False(edge.IsCorner);
        Assert.Equal(BoxEdge.South, edge.AsEdge);
        Assert.Null(edge.AsCorner);
        Assert.Equal("South edge", edge.ToString());

        Assert.Equal(corner, CutSite.Corner(BoxCorner.NorthWest));
        Assert.NotEqual(corner, edge);
    }

    [Fact]
    public void ACutKnowsItsOwnSite()
    {
        Assert.Equal(
            CutSite.Corner(BoxCorner.SouthEast),
            new CornerCut(BoxCorner.SouthEast, Length.Inches(1), Length.Inches(1)).Site);
        Assert.Equal(
            CutSite.Corner(BoxCorner.NorthWest),
            new RoundedCorner(BoxCorner.NorthWest, Length.Inches(1)).Site);
        Assert.Equal(
            CutSite.Edge(BoxEdge.East),
            new CurvedEdge(BoxEdge.East, Bow.Outward, Length.Inches(1)).Site);
    }

    [Fact]
    public void ASiteThatNamesNoCornerOrEdgeIsARejectedArgument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CutSite.Corner((BoxCorner)9));
        Assert.Throws<ArgumentOutOfRangeException>(() => CutSite.Edge((BoxEdge)9));
    }
}
