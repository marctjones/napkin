using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// How close is close enough for the relationships that cannot hold exactly on a fixed grid.
/// </summary>
/// <remarks>
/// Exact-class relationships are checked with zero tolerance whatever is passed here. These
/// numbers matter only after a solver has rounded (design &#xA7;5.3); the first beta has no
/// tolerance anywhere.
/// </remarks>
/// <param name="Position">How far apart two points may be and still count as one.</param>
/// <param name="Angle">How far apart two directions may be and still count as one.</param>
public sealed record Tolerances(Length Position, Angle Angle)
{
    /// <summary>
    /// The tolerances of design &#xA7;5.3: 1/256&#x2033; and 2 arcseconds.
    /// </summary>
    /// <remarks>
    /// Each rounded coordinate is within half a unit; a rotated corner adds up to 0.71 units from
    /// width and height rounding and up to 0.3 units from angle rounding over 10 ft; the
    /// difference of two such corners is within about 3 units, and 4 is the next power of two.
    /// Derived from <see cref="Length.UnitsPerInch"/> so that changing the grid cannot leave this
    /// stale.
    /// </remarks>
    public static readonly Tolerances Default = new(new Length(Length.UnitsPerInch / 256), new Angle(2));
}

/// <summary>A relationship that does not hold, and by how much.</summary>
/// <param name="Relationship">Which relationship.</param>
/// <param name="Residual">
/// How far from holding it is, as a distance. For the angular kinds this is the positional
/// deviation the angular error produces at the far end of the longer edge, so that one number is
/// comparable across kinds; the angular kinds are judged against
/// <see cref="Tolerances.Angle"/> rather than against this.
/// </param>
/// <param name="Exact">Whether this relationship is in the exact class, which is checked with zero tolerance.</param>
public sealed record Violation(RelationshipId Relationship, Length Residual, bool Exact);

/// <summary>The result of checking every relationship in a sketch.</summary>
/// <param name="Violations">The relationships that do not hold.</param>
public sealed record CheckReport(ImmutableList<Violation> Violations)
{
    /// <summary>A report with nothing wrong.</summary>
    public static readonly CheckReport AllGood = new(ImmutableList<Violation>.Empty);

    /// <summary>Whether every relationship holds.</summary>
    public bool AllHold => Violations.IsEmpty;

    /// <summary>The violations, one per line, for an assertion or a log.</summary>
    public override string ToString()
        => Violations.IsEmpty
            ? "all hold"
            : string.Join(
                Environment.NewLine,
                Violations.Select(v => $"{v.Relationship} is out by {v.Residual}{(v.Exact ? " (exact class)" : string.Empty)}"));
}

/// <summary>
/// Evaluates every relationship against a sketch's current geometry. This is invariant 3 of
/// design &#xA7;2.5, and the one definition of "holds" that the direct updater, the solver and
/// the tests all share.
/// </summary>
public static class RelationshipChecker
{
    /// <summary>Checks every relationship with the default tolerances.</summary>
    public static CheckReport Check(Sketch sketch) => Check(sketch, Tolerances.Default);

    /// <summary>
    /// Checks every relationship. The sketch must already pass <see cref="Sketch.Validate"/>:
    /// a dangling reference is a load failure, not something to measure.
    /// </summary>
    public static CheckReport Check(Sketch sketch, Tolerances tolerances)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(tolerances);

        ImmutableList<Violation>.Builder violations = ImmutableList.CreateBuilder<Violation>();

        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            // A joint is data, not a constraint (joinery note §4.3): parts that have drifted apart
            // leave it unsatisfied — drawn hollow, flagged in the cut list — and never make a
            // file unloadable or an edit refused. IsSatisfied is how that is asked.
            if (relationship is Joint)
            {
                continue;
            }

            Residual residual = Evaluate(sketch, relationship);
            if (Holds(residual, tolerances))
            {
                continue;
            }

            violations.Add(new Violation(relationship.Id, residual.PositionalResidual, residual.Exact));
        }

        return violations.Count == 0 ? CheckReport.AllGood : new CheckReport(violations.ToImmutable());
    }

    /// <summary>
    /// Whether a relationship is in the exact class for this sketch — a copy, a sum or an exact
    /// rotation of integers, checked with zero tolerance. A relationship's class is a property of
    /// its kind <em>and the rotations involved</em> (design &#xA7;5.3).
    /// </summary>
    public static bool IsExactClass(Sketch sketch, Relationship relationship)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(relationship);

        return Evaluate(sketch, relationship).Exact;
    }

    /// <summary>
    /// Whether a joint's parts still touch and its numbers still make sense
    /// (<see cref="JointGeometry.IsSatisfied"/>). <see cref="Check(Sketch)"/> never lists a joint,
    /// so this is the one place to ask.
    /// </summary>
    public static bool IsSatisfied(Sketch sketch, Joint joint)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(joint);

        return Holds(Evaluate(sketch, joint), Tolerances.Default);
    }

    private static bool Holds(Residual residual, Tolerances tolerances)
    {
        if (!residual.Evaluable)
        {
            return false;
        }

        if (residual.IsAngular)
        {
            return residual.AngularResidual.Arcseconds
                   <= (residual.Exact ? 0 : tolerances.Angle.Arcseconds);
        }

        return residual.PositionalResidual <= (residual.Exact ? Length.Zero : tolerances.Position);
    }

    private static Residual Evaluate(Sketch sketch, Relationship relationship) => relationship switch
    {
        // Anchored constrains updates, not state: there is no geometry it can contradict.
        Anchored => Residual.FromDistance(Length.Zero, exact: true),

        Coincident coincident => CoincidentResidual(sketch, coincident),
        Horizontal horizontal => AxisAlignmentResidual(sketch, horizontal.Edge, Axis.Y),
        Vertical vertical => AxisAlignmentResidual(sketch, vertical.Edge, Axis.X),
        Flush flush => FlushResidual(sketch, flush),
        AxisDistance axisDistance => AxisDistanceResidual(sketch, axisDistance),
        ParamValue paramValue => Residual.FromDistance(
            Length.Abs(sketch.ValueOf(paramValue.Param) - paramValue.Value),
            ExactParam(sketch, paramValue.Param)),
        EqualParam equalParam => Residual.FromDistance(
            Length.Abs(sketch.ValueOf(equalParam.A) - sketch.ValueOf(equalParam.B)),
            ExactParam(sketch, equalParam.A) && ExactParam(sketch, equalParam.B)),
        Centered centered => CenteredResidual(sketch, centered),

        Parallel parallel => AngularResidual(sketch, parallel.A, parallel.B, Angle.Zero, undirected: true),
        Perpendicular perpendicular => AngularResidual(sketch, perpendicular.A, perpendicular.B, Angle.Right, undirected: true),
        AngleBetween angleBetween => AngularResidual(sketch, angleBetween.A, angleBetween.B, angleBetween.Angle, undirected: false),
        Distance distance => DistanceResidual(sketch, distance),
        PointOnEdge pointOnEdge => PointOnEdgeResidual(sketch, pointOnEdge),
        Symmetric symmetric => SymmetricResidual(sketch, symmetric),

        // Zero while the two faces touch; NotEvaluable once they do not (joinery note §4.3).
        Joint joint => JointGeometry.IsSatisfied(sketch, joint)
            ? Residual.FromDistance(Length.Zero, exact: true)
            : Residual.NotEvaluable,

        // Tangent and Radius are about arcs, and there are no arcs in #5 (design §3.2, §10).
        Tangent or Radius => Residual.NotEvaluable,

        _ => Residual.NotEvaluable,
    };

    /// <summary>
    /// Equal on every axis both places fix (<c>docs/design/assembly-model.md</c> &#xA7;2.1): the
    /// residual is the largest gap on any of them. Places with no axis in common have nothing to
    /// compare, and <see cref="PlaceRules"/> refuses them before they are stored.
    /// </summary>
    private static Residual CoincidentResidual(Sketch sketch, Coincident coincident)
    {
        Place a = sketch.PlaceOf(coincident.A);
        Place b = sketch.PlaceOf(coincident.B);
        ImmutableArray<Axis> common = Place.Common(a, b);
        if (common.IsEmpty)
        {
            return Residual.NotEvaluable;
        }

        Length gap = Length.Zero;
        foreach (Axis axis in common)
        {
            gap = Length.Max(gap, Length.Abs(a[axis] - b[axis]));
        }

        return Residual.FromDistance(gap, ExactPlace(sketch, coincident.A) && ExactPlace(sketch, coincident.B));
    }

    private static Residual AxisAlignmentResidual(Sketch sketch, PlaceRef edge, Axis mustNotVary)
    {
        if (sketch.PlanLineOf(edge) is not (Point2 from, Point2 to))
        {
            return Residual.NotEvaluable;
        }

        return Residual.FromDistance(
            Length.Abs(to.Component(mustNotVary) - from.Component(mustNotVary)),
            ExactPlace(sketch, edge));
    }

    /// <summary>
    /// The same plane: two places that each fix one axis, the same one, compared on it — two faces,
    /// or a face and an axis-aligned segment, in any of the 24 orientations. Anything else is judged
    /// geometrically in the plan, in the tolerance class, as it was before assembly-model: how far
    /// the second line's ends sit off the first's.
    /// </summary>
    private static Residual FlushResidual(Sketch sketch, Flush flush)
    {
        Place a = sketch.PlaceOf(flush.A);
        Place b = sketch.PlaceOf(flush.B);
        if (a.Count == 1 && b.Count == 1 && a.Axes[0] == b.Axes[0])
        {
            Axis axis = a.Axes[0];
            return Residual.FromDistance(
                Length.Abs(a[axis] - b[axis]),
                ExactPlace(sketch, flush.A) && ExactPlace(sketch, flush.B));
        }

        if (sketch.PlanLineOf(flush.A) is not (Point2 a0, Point2 a1) || sketch.PlanLineOf(flush.B) is not (Point2 b0, Point2 b1))
        {
            return Residual.NotEvaluable;
        }

        // Not both axis-aligned the same way: how far B's ends sit off A's line.
        return Residual.FromDistance(
            Length.Max(PerpendicularDistance(a0, a1, b0), PerpendicularDistance(a0, a1, b1)),
            exact: false);
    }

    private static Residual AxisDistanceResidual(Sketch sketch, AxisDistance relationship)
    {
        Place from = sketch.PlaceOf(relationship.From);
        Place to = sketch.PlaceOf(relationship.To);
        if (!from.Fixes(relationship.Axis) || !to.Fixes(relationship.Axis))
        {
            return Residual.NotEvaluable;
        }

        Length actual = to[relationship.Axis] - from[relationship.Axis];

        return Residual.FromDistance(
            Length.Abs(actual - relationship.Distance),
            ExactPlace(sketch, relationship.From) && ExactPlace(sketch, relationship.To));
    }

    private static Residual CenteredResidual(Sketch sketch, Centered centered)
    {
        Length? a = sketch.PlaceOf(centered.A).Coordinate(centered.Axis);
        Length? b = sketch.PlaceOf(centered.B).Coordinate(centered.Axis);
        Length? middle = sketch.PlaceOf(centered.Middle).Coordinate(centered.Axis);
        if (a is null || b is null || middle is null)
        {
            return Residual.NotEvaluable;
        }

        return Residual.FromDistance(
            MiddleResidual(middle.Value, a.Value, b.Value),
            ExactPlace(sketch, centered.Middle) && ExactPlace(sketch, centered.A) && ExactPlace(sketch, centered.B));
    }

    /// <summary>
    /// The midpoint of a span, rounded half to even. What the propagator puts a middle point at.
    /// </summary>
    public static Length Midpoint(Length a, Length b) => (a + b).Divide(2, Rounding.HalfToEven);

    /// <summary>
    /// How far a middle point is from the nearest position that centres it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When the span is an even number of units there is one such position and the relationship is
    /// exact. When it is odd the true midpoint falls between two units, and <em>either</em>
    /// neighbour centres it to within half a unit — which is the only tolerance the direct updater
    /// ever needs (design &#xA7;3.2).
    /// </para>
    /// <para>
    /// Measuring against the nearest of the two, rather than against the half-to-even one the
    /// propagator happens to pick, is what makes the relationship survive being translated: a
    /// group moved by an odd number of units lands on the other side of the tie, and a rule that
    /// insisted on half-to-even would call that a violation.
    /// </para>
    /// </remarks>
    internal static Length MiddleResidual(Length middle, Length a, Length b)
    {
        Int128 sum = (Int128)a.Units + b.Units;
        Int128 low = sum >= 0 ? sum / 2 : (sum - 1) / 2;
        Int128 high = low * 2 == sum ? low : low + 1;
        Int128 toLow = middle.Units - low;
        Int128 toHigh = middle.Units - high;

        if (toLow < 0)
        {
            toLow = -toLow;
        }

        if (toHigh < 0)
        {
            toHigh = -toHigh;
        }

        return new Length(checked((long)(toLow < toHigh ? toLow : toHigh)));
    }

    /// <summary>Whether a middle point centres a span, to within the half unit of design &#xA7;3.2.</summary>
    internal static bool IsCentred(Length middle, Length a, Length b)
        => MiddleResidual(middle, a, b) == Length.Zero;

    // The solver-reserved kinds below are plan-view measurements, as they were before
    // assembly-model: between plan points and against plan lines. A place that is neither — a face
    // lying flat, a horizontal edge — gives them nothing to measure (docs/design/assembly-model.md
    // §2.3 leaves them otherwise untouched, and the direct updater refuses them).
    private static Residual DistanceResidual(Sketch sketch, Distance distance)
    {
        if (sketch.PlanPointOf(distance.A) is not { } a || sketch.PlanPointOf(distance.B) is not { } b)
        {
            return Residual.NotEvaluable;
        }

        return Residual.FromDistance(Length.Abs((b - a).Magnitude() - distance.Value), exact: false);
    }

    private static Residual PointOnEdgeResidual(Sketch sketch, PointOnEdge relationship)
    {
        if (sketch.PlanLineOf(relationship.Edge) is not (Point2 from, Point2 to)
            || sketch.PlanPointOf(relationship.Point) is not { } point)
        {
            return Residual.NotEvaluable;
        }

        return Residual.FromDistance(PerpendicularDistance(from, to, point), exact: false);
    }

    private static Residual SymmetricResidual(Sketch sketch, Symmetric symmetric)
    {
        if (sketch.PlanLineOf(symmetric.Mirror) is not (Point2 from, Point2 to)
            || sketch.PlanPointOf(symmetric.A) is not { } a
            || sketch.PlanPointOf(symmetric.B) is not { } b)
        {
            return Residual.NotEvaluable;
        }

        Point2 reflected = Reflect(from, to, a);
        return Residual.FromDistance((b - reflected).Magnitude(), exact: false);
    }

    private static Residual AngularResidual(Sketch sketch, PlaceRef a, PlaceRef b, Angle target, bool undirected)
    {
        if (sketch.PlanLineOf(a) is not (Point2 a0, Point2 a1) || sketch.PlanLineOf(b) is not (Point2 b0, Point2 b1))
        {
            return Residual.NotEvaluable;
        }

        Length span = Length.Max((a1 - a0).Magnitude(), (b1 - b0).Magnitude());

        // Both directions are stored Angles the repair pass can copy, so the relationship is
        // exact class (design §5.3); with any segment involved the direction comes from two node
        // positions and it is tolerance class.
        Angle? exactA = ExactDirection(sketch, a);
        Angle? exactB = ExactDirection(sketch, b);

        long errorArcseconds;
        bool exact;
        if (exactA is { } directionA && exactB is { } directionB)
        {
            errorArcseconds = Fold(directionB.Arcseconds - directionA.Arcseconds - target.Arcseconds, undirected);
            exact = true;
        }
        else
        {
            double degrees = DirectionDegrees(b0, b1) - DirectionDegrees(a0, a1) - target.ToDegrees();
            errorArcseconds = Fold(
                (long)Math.Round(degrees * Angle.ArcsecondsPerDegree, MidpointRounding.ToEven),
                undirected);
            exact = false;
        }

        Angle error = new(errorArcseconds);
        Length deviation = Length.FromInches(
            span.ToInches() * Math.Abs(Math.Sin(error.ToRadians())),
            Rounding.HalfToEven);

        return Residual.FromAngle(error, deviation, exact);
    }

    /// <summary>
    /// The magnitude of an angular error: into [0, 90&#xB0;] for the undirected kinds (parallel
    /// and perpendicular do not care which way an edge runs) and into [0, 180&#xB0;] otherwise.
    /// </summary>
    private static long Fold(long arcseconds, bool undirected)
    {
        long period = undirected ? 2 * Angle.RightAngleArcseconds : Angle.FullTurn;
        long folded = arcseconds % period;
        if (folded < 0)
        {
            folded += period;
        }

        return folded > period / 2 ? period - folded : folded;
    }

    private static Angle? ExactDirection(Sketch sketch, PlaceRef edge)
    {
        if (edge is not FeatureRef { Feature.Faces: [var face] } feature
            || sketch.Find<Box>(feature.Box) is not { } box
            || !box.Rotation.IsRightAngleMultiple)
        {
            return null;
        }

        // A side face is seen from above as a line across the footprint: one whose tipped normal
        // is plan Y runs along plan X, and one whose normal is plan X runs along Y. For a box lying
        // as drawn, South and North run along its local X and East and West along its local Y.
        Axis normal = new Orientation(box.FaceUp, Angle.Zero).Normal(face).Axis;
        if (normal == Axis.Z)
        {
            return null;
        }

        Angle local = normal == Axis.Y ? Angle.Zero : Angle.Right;
        return box.Rotation + local;
    }

    private static double DirectionDegrees(Point2 from, Point2 to)
        => Math.Atan2((to.Y - from.Y).ToInches(), (to.X - from.X).ToInches()) * (180.0 / Math.PI);

    /// <summary>The axis whose coordinate an axis-aligned edge holds constant, or null if it is not axis-aligned.</summary>
    private static Axis? NormalAxis(Point2 from, Point2 to)
    {
        bool sameX = from.X == to.X;
        bool sameY = from.Y == to.Y;

        if (sameX == sameY)
        {
            // Either a diagonal, or a degenerate edge with no direction at all.
            return null;
        }

        return sameX ? Axis.X : Axis.Y;
    }

    private static Length PerpendicularDistance(Point2 lineFrom, Point2 lineTo, Point2 point)
    {
        double dx = (lineTo.X - lineFrom.X).ToInches();
        double dy = (lineTo.Y - lineFrom.Y).ToInches();
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
        {
            return (point - lineFrom).Magnitude();
        }

        double px = (point.X - lineFrom.X).ToInches();
        double py = (point.Y - lineFrom.Y).ToInches();
        double cross = dx * py - dy * px;

        return Length.FromInches(Math.Abs(cross) / Math.Sqrt(lengthSquared), Rounding.HalfToEven);
    }

    private static Point2 Reflect(Point2 lineFrom, Point2 lineTo, Point2 point)
    {
        double dx = (lineTo.X - lineFrom.X).ToInches();
        double dy = (lineTo.Y - lineFrom.Y).ToInches();
        double lengthSquared = dx * dx + dy * dy;

        if (lengthSquared == 0)
        {
            return lineFrom - (point - lineFrom);
        }

        double px = (point.X - lineFrom.X).ToInches();
        double py = (point.Y - lineFrom.Y).ToInches();
        double along = (px * dx + py * dy) / lengthSquared;

        return new Point2(
            Length.FromInches(lineFrom.X.ToInches() + (2 * along * dx) - px, Rounding.HalfToEven),
            Length.FromInches(lineFrom.Y.ToInches() + (2 * along * dy) - py, Rounding.HalfToEven));
    }

    /// <summary>
    /// Whether a place's coordinates are exact integers: a node's and a segment's always, a box's
    /// features and centre when the box is on one of the 24 orientations (design &#xA7;5.3).
    /// </summary>
    private static bool ExactPlace(Sketch sketch, PlaceRef reference) => reference switch
    {
        // A segment's ends are node positions, which are exact integers whatever they are.
        NodeRef or SegmentRef => true,

        // A strut's end is its stored point (assembly-model §3a.5).
        StrutEndRef => true,
        CenterRef or FeatureRef => sketch.Find<Box>(reference.Owner)?.Rotation.IsRightAngleMultiple ?? false,
        _ => false,
    };

    private static bool ExactParam(Sketch sketch, ParamRef reference)
    {
        switch (reference)
        {
            case BoxWidthRef or BoxHeightRef or BoxDepthRef:
                // What the user typed, stored as typed, whatever the rotation.
                return true;

            case SegmentLengthRef segmentLength:
            {
                return sketch.PlaceOf(new SegmentRef(segmentLength.Segment)).Count == 1;
            }

            default:
                return false;
        }
    }

    /// <summary>How far a relationship is from holding, and whether it must hold exactly.</summary>
    private readonly record struct Residual(
        Length PositionalResidual,
        Angle AngularResidual,
        bool IsAngular,
        bool Exact,
        bool Evaluable)
    {
        /// <summary>A relationship whose error is a distance.</summary>
        public static Residual FromDistance(Length residual, bool exact)
            => new(residual, Angle.Zero, IsAngular: false, exact, Evaluable: true);

        /// <summary>A relationship whose error is an angle, with the distance that angle produces.</summary>
        public static Residual FromAngle(Angle residual, Length deviation, bool exact)
            => new(deviation, residual, IsAngular: true, exact, Evaluable: true);

        /// <summary>A relationship this build cannot evaluate, and so cannot call satisfied.</summary>
        public static readonly Residual NotEvaluable
            = new(Length.Zero, Angle.Zero, IsAngular: false, Exact: false, Evaluable: false);
    }
}
