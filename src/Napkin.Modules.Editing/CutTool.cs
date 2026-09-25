using System.Diagnostics.CodeAnalysis;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// What a press on a target in the shape workshop is holding down as well as the button.
/// </summary>
/// <remarks>
/// The doc leaves "a modifier" and "another modifier" unnamed
/// (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.2); this is where the choice is written down.
/// Shift constrains, which is what Shift does in every drawing program there has ever been, and
/// the one that changes the <em>kind</em> of cut gets a key of its own.
/// </remarks>
[Flags]
public enum CutModifiers
{
    /// <summary>Nothing held: a corner is clipped, an edge is curved.</summary>
    None = 0,

    /// <summary>Shift: the two setbacks of a corner clip stay equal — a 45&#xB0; cut.</summary>
    Equal = 1,

    /// <summary>Alt: a corner is rounded instead of clipped.</summary>
    Round = 2,
}

/// <summary>What a press on one of the workshop's targets would do.</summary>
public enum CutGesture
{
    /// <summary>Nothing: the pointer is not on a target.</summary>
    None,

    /// <summary>Clip the corner: a <see cref="CornerCut"/>.</summary>
    Clip,

    /// <summary>Round the corner: a <see cref="RoundedCorner"/>.</summary>
    Round,

    /// <summary>Curve the edge: a <see cref="CurvedEdge"/>.</summary>
    Curve,
}

/// <summary>
/// The shape workshop's tool: press on a corner or an edge midpoint, drag, and a cut follows the
/// pointer (<c>docs/design/shaped-parts-model.md</c> &#xA7;7.2).
/// </summary>
/// <remarks>
/// <para>
/// <strong>Pure, like <see cref="RectangleTool"/>.</strong> It holds the blank as it was when the
/// button went down, the site that was pressed, and the grid step — no pixels, no control, no
/// sketch. The workshop feeds it model points in the blank's <em>own</em> frame and asks what cut
/// they make; the answer is a <see cref="Cut"/> value, and the request that carries it is a
/// <see cref="SetCut"/> like any other edit (CVS-005).
/// </para>
/// <para>
/// <strong>The blank's own frame.</strong> The workshop draws the blank unrotated and anchored at
/// the origin (&#xA7;7.1), which is the frame a cut is named in anyway (&#xA7;1.3), so nothing here
/// ever rotates anything. <see cref="Local"/> is how the caller gets that copy of a box, and
/// <see cref="TargetPoint"/> answers where a target sits in it — through
/// <see cref="BoxGeometry.GripPoint"/>, which already knows the four corners and the four edge
/// midpoints.
/// </para>
/// <para>
/// <strong>A candidate always fits what it can.</strong> Setbacks are snapped to the grid and held
/// inside the blank, so dragging past the far edge stops at it rather than producing a cut the
/// updater will refuse on every pointer sample. What the clamp cannot know about — a cut at the
/// other end of the same edge, a curve facing it across the blank — is still the updater's to
/// refuse, and it names the site when it does (&#xA7;2.2).
/// </para>
/// </remarks>
public sealed class CutTool
{
    Box _blank = null!;
    CutSite _site;
    double _gridStepInches = 1;

    /// <summary>Whether a cut is being dragged out now.</summary>
    public bool IsCutting { get; private set; }

    /// <summary>The site the press landed on.</summary>
    public CutSite Site => _site;

    /// <summary>The cut as it stands, or <see langword="null"/> when the drag has made nothing yet.</summary>
    public Cut? Candidate { get; private set; }

    /// <summary>
    /// A box in its own frame: anchored at the origin and unrotated, cuts and part and all.
    /// </summary>
    public static Box Local(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return box with { Anchor = Point3.Origin, FaceUp = BoxFace.Top, Rotation = Angle.Zero };
    }

    /// <summary>Every site the workshop offers as a target, in the order they are drawn.</summary>
    public static IEnumerable<CutSite> Targets()
    {
        foreach (BoxCorner corner in (BoxCorner[])
                 [BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
        {
            yield return CutSite.Corner(corner);
        }

        foreach (BoxEdge edge in (BoxEdge[])[BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West])
        {
            yield return CutSite.Edge(edge);
        }
    }

    /// <summary>Where a site's target sits on a blank, in the blank's own frame.</summary>
    /// <param name="blank">The blank, as <see cref="Local"/> gives it.</param>
    /// <param name="site">The corner or edge.</param>
    public static Point2 TargetPoint(Box blank, CutSite site) =>
        BoxGeometry.GripPoint(blank, GripFor(site));

    /// <summary>The grip <see cref="BoxGeometry"/> knows a site as.</summary>
    public static BoxGrip GripFor(CutSite site) => site.AsCorner switch
    {
        BoxCorner.SouthWest => BoxGrip.SouthWest,
        BoxCorner.SouthEast => BoxGrip.SouthEast,
        BoxCorner.NorthEast => BoxGrip.NorthEast,
        BoxCorner.NorthWest => BoxGrip.NorthWest,
        _ => site.AsEdge switch
        {
            BoxEdge.South => BoxGrip.South,
            BoxEdge.East => BoxGrip.East,
            BoxEdge.North => BoxGrip.North,
            _ => BoxGrip.West,
        },
    };

    /// <summary>What a press on a site with these modifiers held would do.</summary>
    public static CutGesture GestureFor(CutSite site, CutModifiers modifiers)
    {
        if (!site.IsCorner)
        {
            return CutGesture.Curve;
        }

        return modifiers.HasFlag(CutModifiers.Round) ? CutGesture.Round : CutGesture.Clip;
    }

    /// <summary>
    /// The site whose target is nearest a point and within a tolerance of it, or
    /// <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// Corners are tried before edge midpoints, for the same reason
    /// <see cref="BoxGeometry.GripAt"/> does it: on a narrow blank the two can overlap, and a
    /// corner is the one a person aimed at.
    /// </remarks>
    public static CutSite? TargetAt(Box blank, Point2 point, Length tolerance)
    {
        ArgumentNullException.ThrowIfNull(blank);

        foreach (CutSite site in Targets())
        {
            Point2 at = TargetPoint(blank, site);
            if (Length.Abs(point.X - at.X) <= tolerance && Length.Abs(point.Y - at.Y) <= tolerance)
            {
                return site;
            }
        }

        return null;
    }

    /// <summary>Starts a cut at a site.</summary>
    /// <param name="blankAtPress">
    /// The blank as it was when the button went down, in its own frame. Measuring from it rather
    /// than from the blank as it stands is what stops a cut the updater refused from accumulating.
    /// </param>
    /// <param name="site">The corner or edge that was pressed.</param>
    /// <param name="gridStepInches">The grid step in force, which every setback lands on.</param>
    public void Begin(Box blankAtPress, CutSite site, double gridStepInches)
    {
        ArgumentNullException.ThrowIfNull(blankAtPress);

        _blank = blankAtPress;
        _site = site;
        _gridStepInches = gridStepInches;
        Candidate = null;
        IsCutting = true;
    }

    /// <summary>Moves the pointer, and works out the cut it now asks for.</summary>
    /// <param name="point">Where the pointer is, in the blank's own frame.</param>
    /// <param name="modifiers">What is held down with the button.</param>
    public void MoveTo(Point2 point, CutModifiers modifiers)
    {
        if (IsCutting)
        {
            Candidate = CandidateFor(_blank, _site, point, modifiers, _gridStepInches);
        }
    }

    /// <summary>Abandons the cut. Nothing is made.</summary>
    public void Cancel()
    {
        IsCutting = false;
        Candidate = null;
    }

    /// <summary>
    /// Ends the drag, and gives back the one request that makes the cut.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> when the gesture was a click, or moved less than the grid step, and
    /// so asked for nothing.
    /// </returns>
    public bool TryComplete([NotNullWhen(true)] out SetCut? request)
    {
        request = null;
        bool wasCutting = IsCutting;
        Cut? candidate = Candidate;

        IsCutting = false;
        Candidate = null;

        if (!wasCutting || candidate is null)
        {
            return false;
        }

        request = new SetCut(_blank.Id, candidate);
        return true;
    }

    /// <summary>
    /// The cut a pointer position asks for: the whole of the gesture, as a function.
    /// </summary>
    /// <param name="blank">The blank, in its own frame.</param>
    /// <param name="site">The corner or edge being cut.</param>
    /// <param name="point">Where the pointer is, in the blank's own frame.</param>
    /// <param name="modifiers">What is held down with the button.</param>
    /// <param name="gridStepInches">The grid step every setback lands on.</param>
    /// <returns>The cut, or <see langword="null"/> when the pointer has not moved far enough.</returns>
    public static Cut? CandidateFor(
        Box blank,
        CutSite site,
        Point2 point,
        CutModifiers modifiers,
        double gridStepInches)
    {
        ArgumentNullException.ThrowIfNull(blank);

        Length step = new(SnapGrid.UnitsPerStep(gridStepInches));
        return site.AsCorner is { } corner
            ? AtCorner(blank, corner, point, modifiers, gridStepInches, step)
            : AtEdge(blank, site.AsEdge!.Value, point, gridStepInches, step);
    }

    /// <summary>
    /// A corner: the two setbacks follow the pointer, or one radius does when the round modifier
    /// is held, or both setbacks stay equal when the 45&#xB0; one is.
    /// </summary>
    static Cut? AtCorner(
        Box blank,
        BoxCorner corner,
        Point2 point,
        CutModifiers modifiers,
        double gridStepInches,
        Length step)
    {
        Point2 at = blank.Corner(corner);
        Length dx = Length.Abs(point.X - at.X);
        Length dy = Length.Abs(point.Y - at.Y);

        bool round = modifiers.HasFlag(CutModifiers.Round);
        if (round || modifiers.HasFlag(CutModifiers.Equal))
        {
            // One number for both: the mean of the two reaches, so a diagonal drag does what it
            // looks like it does rather than following whichever axis moved less.
            Length mean = (dx + dy).Divide(2, Rounding.HalfAwayFromZero);
            Length held = Hold(SnapGrid.Snap(mean, gridStepInches), step, Length.Min(blank.Width, blank.Height));
            if (held <= Length.Zero)
            {
                return null;
            }

            return round
                ? new RoundedCorner(corner, held)
                : new CornerCut(corner, held, held);
        }

        Length alongX = Hold(SnapGrid.Snap(dx, gridStepInches), step, blank.Width);
        Length alongY = Hold(SnapGrid.Snap(dy, gridStepInches), step, blank.Height);

        // A press that has gone nowhere is a click, and a click cuts nothing — the same rule the
        // rectangle tool has for a drag with no area in it.
        return dx < step && dy < step
            ? null
            : new CornerCut(corner, alongX, alongY);
    }

    /// <summary>
    /// An edge: the depth follows the pointer, and which way the edge bows follows which side of
    /// the edge the pointer is on — into the part is a scallop, away from it is a bow.
    /// </summary>
    static Cut? AtEdge(Box blank, BoxEdge edge, Point2 point, double gridStepInches, Length step)
    {
        Point2 middle = TargetPoint(blank, CutSite.Edge(edge));
        Vector2 fromEdge = point - middle;

        // How far into the part the pointer has gone, positive inwards. The workshop's blank is
        // unrotated, so the inward normal of each edge is one axis with one sign.
        Length inwards = edge switch
        {
            BoxEdge.South => fromEdge.Dy,
            BoxEdge.North => -fromEdge.Dy,
            BoxEdge.West => fromEdge.Dx,
            _ => -fromEdge.Dx,
        };

        Bow bow = inwards > Length.Zero ? Bow.Inward : Bow.Outward;
        Length across = edge is BoxEdge.South or BoxEdge.North ? blank.Height : blank.Width;

        // An inward curve has to leave something in the middle of the blank, so it stops one unit
        // short of the far side; an outward one may reach it (§1.6 invariant 7).
        Length limit = bow == Bow.Inward ? across - new Length(1) : across;
        Length depth = Hold(SnapGrid.Snap(Length.Abs(inwards), gridStepInches), step, limit);

        return Length.Abs(inwards) < step || depth <= Length.Zero
            ? null
            : new CurvedEdge(edge, bow, depth);
    }

    /// <summary>A value on the grid, at least one step and no more than what the blank allows.</summary>
    static Length Hold(Length value, Length step, Length limit) =>
        Length.Min(Length.Max(value, step), limit);
}
