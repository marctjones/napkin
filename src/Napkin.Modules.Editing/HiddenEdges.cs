namespace Napkin.Modules.Editing;

/// <summary>
/// A corner of a face as a standard view sees it: <see cref="U"/> along screen right and
/// <see cref="V"/> along screen up, in inches of the world axes the view shows, and how
/// <see cref="Nearness"/> it is to the eye (larger is nearer), in inches along the view axis.
/// </summary>
public readonly record struct FlatVertex(double U, double V, double Nearness);

/// <summary>
/// A face the eye can see, in a standard view: its corners in order and which of its edges are
/// real (a ruling between the strips of a curved face is not, and gets no line at all).
/// </summary>
/// <param name="Corners">The corners, in order round the face; at least three.</param>
/// <param name="EdgeDrawn">For each corner, whether the edge from it to the next is a real edge.</param>
public sealed record FlatFace(IReadOnlyList<FlatVertex> Corners, IReadOnlyList<bool> EdgeDrawn);

/// <summary>A piece of one face's edge, in view coordinates.</summary>
/// <param name="Face">The index of the face in the list given to <see cref="HiddenEdges.Split"/>.</param>
/// <param name="Edge">Which of its edges: the one starting at this corner.</param>
/// <param name="From">Where the piece starts.</param>
/// <param name="To">Where it ends.</param>
public readonly record struct FlatSegment(int Face, int Edge, FlatVertex From, FlatVertex To)
{
    /// <summary>How long the piece is, on the view plane, in inches.</summary>
    public double Length => Math.Sqrt(((To.U - From.U) * (To.U - From.U)) + ((To.V - From.V) * (To.V - From.V)));
}

/// <summary>Every real edge of every face, split into what the eye sees and what a nearer face hides.</summary>
/// <param name="Visible">Drawn solid.</param>
/// <param name="Hidden">Drawn as light dashes, beneath the solid ones; never where a solid edge lies.</param>
public sealed record EdgeSplit(IReadOnlyList<FlatSegment> Visible, IReadOnlyList<FlatSegment> Hidden);

/// <summary>
/// Hidden edges in a standard view (docs/design/standard-views.md §2.3): each face's real edges,
/// less what every strictly nearer face covers. Pure, and exact enough to test by hand.
/// </summary>
/// <remarks>
/// <para>
/// A face hides an edge only when it is nearer than the edge by more than
/// <see cref="DepthTolerance"/> everywhere: two flush faces never hide each other. The subtraction is
/// general segment-against-polygon clipping — every crossing of the segment with the nearer face's
/// outline, sorted, each piece's midpoint tested for being inside — so an outline with a notch
/// (not convex) is right. A point on a nearer face's outline is behind it; the nearer face's own edge
/// is drawn there.
/// </para>
/// <para>
/// A hidden piece lying along a visible one is dropped (§2.3: "solid is drawn over the dash") — the
/// coffee table's far legs, standing exactly behind the near ones, show no dash at all.
/// </para>
/// </remarks>
public static class HiddenEdges
{
    /// <summary>How much nearer a face must be to hide anything: 1/2048″, finer than any length napkin stores.</summary>
    public const double DepthTolerance = 1.0 / 2048;

    /// <summary>How close on the view plane two points are to count as the same, in inches.</summary>
    public const double PlaneTolerance = 1e-7;

    /// <summary>Splits every real edge of <paramref name="faces"/> into its visible and hidden pieces.</summary>
    public static EdgeSplit Split(IReadOnlyList<FlatFace> faces)
    {
        ArgumentNullException.ThrowIfNull(faces);
        double[] nearest = [.. faces.Select(face => face.Corners.Max(corner => corner.Nearness))];

        List<FlatSegment> visible = [];
        List<FlatSegment> hidden = [];
        for (int f = 0; f < faces.Count; f++)
        {
            FlatFace face = faces[f];
            int count = face.Corners.Count;
            for (int e = 0; e < count; e++)
            {
                if (!face.EdgeDrawn[e])
                {
                    continue;
                }

                FlatVertex a = face.Corners[e], b = face.Corners[(e + 1) % count];
                if (Distance(a, b) <= PlaneTolerance)
                {
                    continue;
                }

                // Any face that reaches nearer than the edge's farthest point might hide some of it;
                // which pieces it hides is judged piece by piece against its plane (Pieces).
                double edgeFarthest = Math.Min(a.Nearness, b.Nearness);
                List<FlatFace> nearer = [.. faces.Where((other, o) => o != f && nearest[o] > edgeFarthest + DepthTolerance)];
                foreach ((double from, double to, bool isHidden) in Pieces(a, b, nearer))
                {
                    FlatSegment piece = new(f, e, At(a, b, from), At(a, b, to));
                    (isHidden ? hidden : visible).Add(piece);
                }
            }
        }

        return new EdgeSplit(visible, [.. hidden.SelectMany(piece => Uncovered(piece, visible))]);
    }

    /// <summary>The pieces of the segment a→b, as parameter ranges, each marked hidden or not.</summary>
    static List<(double From, double To, bool Hidden)> Pieces(FlatVertex a, FlatVertex b, List<FlatFace> nearer)
    {
        List<double> cuts = [0, 1];
        foreach (FlatFace face in nearer)
        {
            int count = face.Corners.Count;
            for (int i = 0; i < count; i++)
            {
                cuts.AddRange(Crossings(a, b, face.Corners[i], face.Corners[(i + 1) % count]));
            }

            // Where the edge passes through the face's plane, nearer on one side and not on the
            // other: the difference in nearness is linear along the edge, so it is one parameter.
            double atA = NearnessAt(face, a) - a.Nearness, atB = NearnessAt(face, b) - b.Nearness;
            if (double.IsFinite(atA) && double.IsFinite(atB) && Math.Sign(atA) != Math.Sign(atB))
            {
                cuts.Add(atA / (atA - atB));
            }
        }

        double[] sorted = [.. cuts.Where(t => t >= 0 && t <= 1).Order()];
        List<(double From, double To, bool Hidden)> pieces = [];
        for (int i = 0; i + 1 < sorted.Length; i++)
        {
            double from = sorted[i], to = sorted[i + 1];
            if ((to - from) * Distance(a, b) <= PlaneTolerance)
            {
                continue;
            }

            FlatVertex middle = At(a, b, (from + to) / 2);
            bool isHidden = nearer.Any(face => Inside(middle, face) && NearnessAt(face, middle) > middle.Nearness + DepthTolerance);
            if (pieces.Count > 0 && pieces[^1].Hidden == isHidden && Math.Abs(pieces[^1].To - from) < 1e-12)
            {
                pieces[^1] = (pieces[^1].From, to, isHidden);
            }
            else
            {
                pieces.Add((from, to, isHidden));
            }
        }

        return pieces;
    }

    /// <summary>Where along a→b (as a parameter) the segment meets c→d: none, one crossing, or the two ends of an overlap.</summary>
    static IEnumerable<double> Crossings(FlatVertex a, FlatVertex b, FlatVertex c, FlatVertex d)
    {
        double rx = b.U - a.U, ry = b.V - a.V;
        double sx = d.U - c.U, sy = d.V - c.V;
        double denominator = (rx * sy) - (ry * sx);
        double qx = c.U - a.U, qy = c.V - a.V;
        double lengthSquared = (rx * rx) + (ry * ry);
        if (Math.Abs(denominator) < 1e-15)
        {
            // Parallel: only a collinear overlap matters, and its ends are the cuts.
            if (Math.Abs((qx * ry) - (qy * rx)) > PlaneTolerance * Math.Sqrt(lengthSquared))
            {
                yield break;
            }

            yield return ((qx * rx) + (qy * ry)) / lengthSquared;
            yield return (((d.U - a.U) * rx) + ((d.V - a.V) * ry)) / lengthSquared;
            yield break;
        }

        double t = ((qx * sy) - (qy * sx)) / denominator;
        double u = ((qx * ry) - (qy * rx)) / denominator;
        if (u >= -1e-12 && u <= 1 + 1e-12)
        {
            yield return t;
        }
    }

    /// <summary>
    /// Whether a nearer face covers a point: inside its outline or on it. On it counts, since a far edge
    /// lying along a nearer face's edge is behind that edge; it is then dropped as coincident with the
    /// solid edge, not dashed.
    /// </summary>
    static bool Inside(FlatVertex point, FlatFace face)
    {
        int count = face.Corners.Count;
        bool inside = false;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            FlatVertex p = face.Corners[i], q = face.Corners[j];
            if (DistanceToSegment(point, q, p) <= PlaneTolerance)
            {
                return true;
            }

            if ((p.V > point.V) != (q.V > point.V)
                && point.U < ((q.U - p.U) * (point.V - p.V) / (q.V - p.V)) + p.U)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>
    /// How near a face is at a point of the view plane: the depth of its plane there
    /// (<c>docs/design/angled-parts.md</c> &#xA7;4). Every face is planar, so its nearness is linear in
    /// (U, V); a face parallel to the view is the same everywhere, which is every face of the axis
    /// views, and a strut's long face slopes. A face seen edge-on covers nothing.
    /// </summary>
    static double NearnessAt(FlatFace face, FlatVertex point)
    {
        FlatVertex a = face.Corners[0];
        double nu = 0, nv = 0, nw = 0;
        for (int i = 1; i + 1 < face.Corners.Count; i++)
        {
            FlatVertex b = face.Corners[i], c = face.Corners[i + 1];
            double bu = b.U - a.U, bv = b.V - a.V, bw = b.Nearness - a.Nearness;
            double cu = c.U - a.U, cv = c.V - a.V, cw = c.Nearness - a.Nearness;
            nu += (bv * cw) - (bw * cv);
            nv += (bw * cu) - (bu * cw);
            nw += (bu * cv) - (bv * cu);
        }

        return Math.Abs(nw) < 1e-12
            ? double.NegativeInfinity
            : a.Nearness - (((nu * (point.U - a.U)) + (nv * (point.V - a.V))) / nw);
    }

    /// <summary>What is left of a hidden piece where no visible piece lies along it.</summary>
    static IEnumerable<FlatSegment> Uncovered(FlatSegment piece, List<FlatSegment> visible)
    {
        double length = piece.Length;
        if (length <= PlaneTolerance)
        {
            yield break;
        }

        List<(double From, double To)> covered = [];
        foreach (FlatSegment solid in visible)
        {
            if (DistanceToLine(solid.From, piece.From, piece.To) > PlaneTolerance
                || DistanceToLine(solid.To, piece.From, piece.To) > PlaneTolerance)
            {
                continue;
            }

            double s = Parameter(solid.From, piece), t = Parameter(solid.To, piece);
            covered.Add((Math.Min(s, t), Math.Max(s, t)));
        }

        double at = 0;
        foreach ((double from, double to) in covered.OrderBy(range => range.From))
        {
            if (from > at && (from - at) * length > PlaneTolerance)
            {
                yield return piece with { From = At(piece.From, piece.To, at), To = At(piece.From, piece.To, Math.Min(from, 1)) };
            }

            at = Math.Max(at, to);
            if (at >= 1)
            {
                yield break;
            }
        }

        if ((1 - at) * length > PlaneTolerance)
        {
            yield return at <= 0 ? piece : piece with { From = At(piece.From, piece.To, at) };
        }
    }

    static double Parameter(FlatVertex point, FlatSegment along)
    {
        double rx = along.To.U - along.From.U, ry = along.To.V - along.From.V;
        return (((point.U - along.From.U) * rx) + ((point.V - along.From.V) * ry)) / ((rx * rx) + (ry * ry));
    }

    static FlatVertex At(FlatVertex a, FlatVertex b, double t) =>
        new(a.U + ((b.U - a.U) * t), a.V + ((b.V - a.V) * t), a.Nearness + ((b.Nearness - a.Nearness) * t));

    static double Distance(FlatVertex a, FlatVertex b) =>
        Math.Sqrt(((b.U - a.U) * (b.U - a.U)) + ((b.V - a.V) * (b.V - a.V)));

    static double DistanceToLine(FlatVertex point, FlatVertex a, FlatVertex b) =>
        Math.Abs(((b.U - a.U) * (point.V - a.V)) - ((b.V - a.V) * (point.U - a.U))) / Distance(a, b);

    static double DistanceToSegment(FlatVertex point, FlatVertex a, FlatVertex b)
    {
        double length = Distance(a, b);
        double t = Math.Clamp((((point.U - a.U) * (b.U - a.U)) + ((point.V - a.V) * (b.V - a.V))) / (length * length), 0, 1);
        return Distance(point, At(a, b, t));
    }
}
