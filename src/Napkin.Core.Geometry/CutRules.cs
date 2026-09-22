using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// Invariants 5 to 9 of <c>docs/design/shaped-parts-model.md</c> §1.6: what makes a blank's cuts
/// a shape that can actually be cut from it.
/// </summary>
/// <remarks>
/// All of them are cheap — a box has at most eight cuts — and all of them are checked by
/// <see cref="Sketch.Validate"/> and by <see cref="DirectUpdater"/> at each of its three doors: an
/// <see cref="AddEntity"/> carrying a box with cuts, a <see cref="SetCut"/>, and after every write
/// that resized a blank (§2.3). Invariant 5's other half, that the cuts are in site order, holds by
/// construction: <see cref="Box.Cuts"/>'s initialiser sorts.
/// </remarks>
internal static class CutRules
{
    private static readonly BoxEdge[] AllEdges = [BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West];

    /// <summary>Everything wrong with a box's cuts, in invariant order.</summary>
    internal static ImmutableList<ValidationError> Errors(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        // A blank that is not a blank at all is already reported as NonPositiveSize, and every
        // message below would be nonsense against it.
        if (box.Cuts.IsEmpty || box.Width <= Length.Zero || box.Height <= Length.Zero)
        {
            return [];
        }

        ImmutableList<ValidationError>.Builder errors = ImmutableList.CreateBuilder<ValidationError>();
        Dictionary<CutSite, Cut> bySite = [];
        foreach (Cut cut in box.Cuts)
        {
            if (!bySite.TryAdd(cut.Site, cut))
            {
                // Invariant 5. The list is sorted, so a repeat is a site used twice.
                errors.Add(new ValidationError(
                    ValidationErrorKind.DuplicateCutSite,
                    $"Box {box.Id} has more than one cut at the {cut.Site}."));
            }
        }

        CurvesClaimTheirCorners(box, bySite, errors);
        ValuesArePositiveAndFit(box, bySite, errors);
        ClaimsFitTheirEdge(box, bySite, errors);
        ClaimsFitAcrossTheBlank(box, bySite, errors);
        SomethingIsLeft(box, errors);

        return errors.ToImmutable();
    }

    /// <summary>The first thing wrong with a box's cuts, or <see langword="null"/>.</summary>
    internal static ValidationError? FirstError(Box box) => Errors(box).FirstOrDefault();

    /// <summary>
    /// The smallest size along one axis of the blank at which its cuts still fit it — what a
    /// <see cref="DragEdge"/> clamps to, so that a blank cannot be dragged shorter than its cuts
    /// the way it cannot be dragged through an anchored neighbour
    /// (<c>docs/design/shaped-parts-model.md</c> §2.3).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Found by bisection <em>against the rules above</em> rather than by a second copy of them
    /// that could drift: "the cuts fit" is monotone in the size, because growing a blank loosens
    /// every invariant 7 limit, widens every invariant 8 budget and only adds area to invariant 9.
    /// A blank has at most eight cuts and the search is some thirty steps, so this is cheap.
    /// </para>
    /// <para>
    /// <see cref="Length.Zero"/> for a plain rectangle, which clamps nothing: a drag that would
    /// take a cutless box to nothing is <see cref="RejectionReason.NonPositiveSize"/> as it always
    /// was.
    /// </para>
    /// </remarks>
    internal static Length SmallestFitting(Box box, Axis axis)
    {
        ArgumentNullException.ThrowIfNull(box);

        if (box.Cuts.IsEmpty)
        {
            return Length.Zero;
        }

        long high = box.Size(axis).Units;
        if (high <= 1 || !Fits(box, axis, box.Size(axis)))
        {
            // The blank does not fit its own cuts even now, so nothing smaller will and there is
            // no floor to offer. Whoever is writing the size refuses it instead of clamping to it.
            return box.Size(axis);
        }

        long low = 1;
        while (low < high)
        {
            long middle = low + ((high - low) / 2);
            if (Fits(box, axis, new Length(middle)))
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return new Length(low);
    }

    private static bool Fits(Box box, Axis axis, Length size)
        => Errors(axis == Axis.X ? box with { Width = size } : box with { Height = size }).IsEmpty;

    /// <summary>
    /// Invariant 6: one cut of any kind per corner, and a curved edge counts at both of its
    /// corners — so nothing else may touch either end of it, and two adjacent edges cannot both be
    /// curved. One cut per corner is invariant 5's doing, since a corner is one site.
    /// </summary>
    private static void CurvesClaimTheirCorners(
        Box box,
        Dictionary<CutSite, Cut> bySite,
        ImmutableList<ValidationError>.Builder errors)
    {
        foreach (CurvedEdge curve in bySite.Values.OfType<CurvedEdge>().OrderBy(curve => curve.Site.Order))
        {
            (BoxCorner from, BoxCorner to) = Box.Ends(curve.Edge);
            foreach (BoxCorner corner in new[] { from, to })
            {
                if (bySite.ContainsKey(CutSite.Corner(corner)))
                {
                    errors.Add(new ValidationError(
                        ValidationErrorKind.CutSiteTaken,
                        $"Box {box.Id} has a cut at the {CutSite.Corner(corner)}, which the curved "
                        + $"{CutSite.Edge(curve.Edge)} already claims."));
                }

                BoxEdge other = OtherEdgeAt(corner, curve.Edge);
                if (curve.Edge < other && bySite.GetValueOrDefault(CutSite.Edge(other)) is CurvedEdge)
                {
                    errors.Add(new ValidationError(
                        ValidationErrorKind.CutSiteTaken,
                        $"Box {box.Id} curves both the {CutSite.Edge(curve.Edge)} and the "
                        + $"{CutSite.Edge(other)}, which meet at the {CutSite.Corner(corner)}."));
                }
            }
        }
    }

    /// <summary>
    /// Invariant 7: every stored value is positive, and every setback fits its edge.
    /// </summary>
    private static void ValuesArePositiveAndFit(
        Box box,
        Dictionary<CutSite, Cut> bySite,
        ImmutableList<ValidationError>.Builder errors)
    {
        foreach (Cut cut in bySite.Values.OrderBy(cut => cut.Site.Order))
        {
            switch (cut)
            {
                case CornerCut corner:
                    Fits(box, errors, cut.Site, corner.AlongX, box.Width, strictly: false, "along X");
                    Fits(box, errors, cut.Site, corner.AlongY, box.Height, strictly: false, "along Y");
                    break;

                case RoundedCorner rounded:
                    Fits(box, errors, cut.Site, rounded.Radius, box.Width, strictly: false, "across the width");
                    Fits(box, errors, cut.Site, rounded.Radius, box.Height, strictly: false, "across the height");
                    break;

                case CurvedEdge curve:
                    // An outward curve may reach the far edge line; an inward one may not, or
                    // there is nothing left in the middle of the blank.
                    Fits(
                        box,
                        errors,
                        cut.Site,
                        curve.Depth,
                        Across(box, curve.Edge),
                        strictly: curve.Bow == Bow.Inward,
                        "into the blank");
                    break;
            }
        }
    }

    private static void Fits(
        Box box,
        ImmutableList<ValidationError>.Builder errors,
        CutSite site,
        Length value,
        Length limit,
        bool strictly,
        string what)
    {
        if (value <= Length.Zero)
        {
            errors.Add(new ValidationError(
                ValidationErrorKind.CutDoesNotFit,
                $"Box {box.Id}'s cut at the {site} reaches {value} {what}; every value must be positive."));
            return;
        }

        if (value > limit || (strictly && value == limit))
        {
            errors.Add(new ValidationError(
                ValidationErrorKind.CutDoesNotFit,
                $"Box {box.Id}'s cut at the {site} reaches {value} {what}, which does not fit "
                + $"{(strictly ? "inside " : string.Empty)}the blank's {limit}."));
        }
    }

    /// <summary>
    /// Invariant 8, one edge at a time: each edge has a budget equal to its length, a cut at
    /// either end claims part of it, and the two claims sum to at most the edge. Equality is a
    /// mitre that runs the whole end, or two cuts meeting in a point.
    /// </summary>
    private static void ClaimsFitTheirEdge(
        Box box,
        Dictionary<CutSite, Cut> bySite,
        ImmutableList<ValidationError>.Builder errors)
    {
        foreach (BoxEdge edge in AllEdges)
        {
            (BoxCorner from, BoxCorner to) = Box.Ends(edge);
            Length budget = OutlineBuilder.RunsAlongX(edge) ? box.Width : box.Height;
            Length claimed = ClaimOn(box, bySite, edge, from) + ClaimOn(box, bySite, edge, to);

            if (claimed > budget)
            {
                errors.Add(new ValidationError(
                    ValidationErrorKind.CutDoesNotFit,
                    $"Box {box.Id}'s cuts claim {claimed} of the {CutSite.Edge(edge)}, which is {budget} long."));
            }
        }
    }

    /// <summary>What the cut at one end of an edge takes out of that edge's length.</summary>
    private static Length ClaimOn(Box box, Dictionary<CutSite, Cut> bySite, BoxEdge edge, BoxCorner corner)
    {
        switch (bySite.GetValueOrDefault(CutSite.Corner(corner)))
        {
            case CornerCut cut:
                return OutlineBuilder.RunsAlongX(edge) ? cut.AlongX : cut.AlongY;

            case RoundedCorner rounded:
                return rounded.Radius;
        }

        // An outward curve on the corner's other edge starts Depth along this one; an inward one
        // starts at the corner and claims nothing.
        return bySite.GetValueOrDefault(CutSite.Edge(OtherEdgeAt(corner, edge))) is CurvedEdge { Bow: Bow.Outward } curve
            ? curve.Depth
            : Length.Zero;
    }

    /// <summary>
    /// Invariant 8's other half: the same budget across the blank, so that a curve's sag can never
    /// reach a feature on the opposite edge. Strictly less when either side is an inward curve —
    /// two scallops that meet at the centre leave two lobes and no part.
    /// </summary>
    private static void ClaimsFitAcrossTheBlank(
        Box box,
        Dictionary<CutSite, Cut> bySite,
        ImmutableList<ValidationError>.Builder errors)
    {
        foreach (CurvedEdge curve in bySite.Values.OfType<CurvedEdge>().OrderBy(curve => curve.Site.Order))
        {
            BoxEdge opposite = Opposite(curve.Edge);
            Length across = Across(box, curve.Edge);

            (BoxCorner from, BoxCorner to) = Box.Ends(opposite);
            foreach (BoxCorner corner in new[] { from, to })
            {
                if (Reach(bySite.GetValueOrDefault(CutSite.Corner(corner)), curve.Edge) is { } reach)
                {
                    CheckAcross(box, errors, curve, CutSite.Corner(corner), reach, across, strictly: curve.Bow == Bow.Inward);
                }
            }

            // A pair of opposite curves is one check, from the side that sorts first.
            if (curve.Edge < opposite
                && bySite.GetValueOrDefault(CutSite.Edge(opposite)) is CurvedEdge facing)
            {
                CheckAcross(
                    box,
                    errors,
                    curve,
                    facing.Site,
                    facing.Depth,
                    across,
                    strictly: curve.Bow == Bow.Inward || facing.Bow == Bow.Inward);
            }
        }
    }

    /// <summary>How far a cut at a corner of the opposite edge reaches towards a curve.</summary>
    private static Length? Reach(Cut? cut, BoxEdge curved) => cut switch
    {
        CornerCut corner => OutlineBuilder.RunsAlongX(curved) ? corner.AlongY : corner.AlongX,
        RoundedCorner rounded => rounded.Radius,
        _ => null,
    };

    private static void CheckAcross(
        Box box,
        ImmutableList<ValidationError>.Builder errors,
        CurvedEdge curve,
        CutSite facing,
        Length reach,
        Length size,
        bool strictly)
    {
        Length claimed = curve.Depth + reach;
        if (claimed > size || (strictly && claimed == size))
        {
            errors.Add(new ValidationError(
                ValidationErrorKind.CutDoesNotFit,
                $"Box {box.Id}'s curved {curve.Site} and its cut at the {facing} claim {claimed} of "
                + $"the {size} between them."));
        }
    }

    /// <summary>
    /// Invariant 9: what is left has positive area — the catch-all for the degenerate cases the
    /// edge budgets do not catch, such as two full-diagonal corner cuts at opposite corners.
    /// Computed exactly in <see cref="Int128"/>, in the local frame, so no rotation can round it.
    /// </summary>
    private static void SomethingIsLeft(Box box, ImmutableList<ValidationError>.Builder errors)
    {
        Outline outline = OutlineBuilder.Build(box, world: false);
        if (Area.TwiceSignedPolygon(outline.Vertices) <= Int128.Zero)
        {
            errors.Add(new ValidationError(
                ValidationErrorKind.NonPositiveArea,
                $"Box {box.Id}'s cuts at the {string.Join(", ", box.Cuts.Select(cut => cut.Site))} "
                + "leave nothing of the blank."));
        }
    }

    /// <summary>The blank's size perpendicular to an edge: how far a curve on it can reach.</summary>
    private static Length Across(Box box, BoxEdge edge) => OutlineBuilder.RunsAlongX(edge) ? box.Height : box.Width;

    private static BoxEdge Opposite(BoxEdge edge) => edge switch
    {
        BoxEdge.South => BoxEdge.North,
        BoxEdge.North => BoxEdge.South,
        BoxEdge.East => BoxEdge.West,
        _ => BoxEdge.East,
    };

    /// <summary>The other of the two edges that meet at a corner.</summary>
    private static BoxEdge OtherEdgeAt(BoxCorner corner, BoxEdge edge)
    {
        BoxEdge incoming = OutlineBuilder.IncomingEdge(corner);
        return incoming == edge ? OutlineBuilder.OutgoingEdge(corner) : incoming;
    }
}
