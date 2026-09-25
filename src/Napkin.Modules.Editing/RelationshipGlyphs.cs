using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>What a relationship glyph on the plan stands for.</summary>
public enum GlyphKind
{
    /// <summary>Two sides lie on one line (<see cref="Flush"/>): drawn across the shared line.</summary>
    Flush,

    /// <summary>Two corners are one point (<see cref="Coincident"/>): drawn on the corner.</summary>
    Coincident,
}

/// <summary>
/// A relationship's mark on the plan (#156, CVS-012): which relationship, what kind, where it sits
/// and which way its line runs, so the canvas can draw it and a hover over it can say the same
/// sentence as its row in the relationship list.
/// </summary>
/// <param name="Id">The relationship it stands for.</param>
/// <param name="Kind">Flush or coincident.</param>
/// <param name="XInches">Where it sits, east.</param>
/// <param name="YInches">Where it sits, north.</param>
/// <param name="AlongX">
/// The shared line's direction, east component, as a unit vector with <paramref name="AlongY"/>;
/// zero for a corner, which has no line.
/// </param>
/// <param name="AlongY">The shared line's direction, north component.</param>
public sealed record RelationshipGlyph(
    RelationshipId Id,
    GlyphKind Kind,
    double XInches,
    double YInches,
    double AlongX,
    double AlongY);

/// <summary>
/// Where the plan draws a glyph for each relationship it can show one for (#156): the two a snap in
/// the plan makes — a side flush with a side, a corner on a corner.
/// </summary>
/// <remarks>
/// <para>
/// A flush glyph sits at the middle of the stretch the two sides share along their line: two parts
/// butted together share the edge they meet on, and the glyph is on it. Two sides that are flush
/// but apart — two parts in a row, their fronts lined up — share no stretch; the glyph then sits in
/// the middle of the gap between them, on the line that relates them, rather than on either part.
/// </para>
/// <para>
/// A coincident glyph sits on the first corner; the second is the same point once the relationship
/// holds. Everything else — a face the plan does not show (two tops flush), a relationship on a wall
/// or a node, the other kinds — has no glyph: the relationship list still says it. This is the
/// plan's; the 3D and standard views draw no glyphs.
/// </para>
/// <para>
/// Positions are doubles, not <see cref="Length"/>s: this is where a mark is drawn, never a
/// dimension, and a side that is not axis-aligned puts it at a point no sixteenth names.
/// </para>
/// </remarks>
public static class RelationshipGlyphs
{
    /// <summary>The glyphs of a sketch's relationships, in the relationship list's order.</summary>
    public static IReadOnlyList<RelationshipGlyph> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        List<RelationshipGlyph> glyphs = [];
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            RelationshipGlyph? glyph = relationship switch
            {
                Flush { A: FeatureRef a, B: FeatureRef b } flush => FlushGlyph(sketch, flush.Id, a, b),
                Coincident { A: FeatureRef a } coincident => CornerGlyph(sketch, coincident.Id, a),
                _ => null,
            };
            if (glyph is not null)
            {
                glyphs.Add(glyph);
            }
        }

        return glyphs;
    }

    static RelationshipGlyph? FlushGlyph(Sketch sketch, RelationshipId id, FeatureRef a, FeatureRef b)
    {
        if (Side(sketch, a) is not { } first || Side(sketch, b) is not { } second)
        {
            return null;
        }

        // Measure both sides along the first one's line.
        double dx = first.ToX - first.FromX, dy = first.ToY - first.FromY;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-12)
        {
            return null;
        }

        (double ux, double uy) = (dx / length, dy / length);
        double Along(double x, double y) => ((x - first.FromX) * ux) + ((y - first.FromY) * uy);

        double secondFrom = Along(second.FromX, second.FromY), secondTo = Along(second.ToX, second.ToY);
        double low = Math.Max(0, Math.Min(secondFrom, secondTo));
        double high = Math.Min(length, Math.Max(secondFrom, secondTo));

        // Overlapping or touching: the middle of what they share. Apart: the middle of the gap,
        // which is the same expression with the ends the other way round.
        double at = (low + high) / 2;
        return new RelationshipGlyph(id, GlyphKind.Flush, first.FromX + (ux * at), first.FromY + (uy * at), ux, uy);
    }

    static RelationshipGlyph? CornerGlyph(Sketch sketch, RelationshipId id, FeatureRef a)
    {
        if (sketch.Find<Box>(a.Box) is not { } box)
        {
            return null;
        }

        Footprint footprint = box.Footprint();
        foreach (BoxCorner corner in Corners)
        {
            if (footprint.UprightAt(corner) == a.Feature)
            {
                Point2 at = footprint.Corner(corner);
                return new RelationshipGlyph(id, GlyphKind.Coincident, at.X.ToInches(), at.Y.ToInches(), 0, 0);
            }
        }

        return null;
    }

    /// <summary>The plan side a face feature is seen as, end to end, in inches; null if the plan does not show it as a side.</summary>
    static (double FromX, double FromY, double ToX, double ToY)? Side(Sketch sketch, FeatureRef feature)
    {
        if (sketch.Find<Box>(feature.Box) is not { } box)
        {
            return null;
        }

        Footprint footprint = box.Footprint();
        foreach (BoxEdge side in Sides)
        {
            if (BoxFeature.Face(footprint.FaceAt(side)) == feature.Feature)
            {
                (BoxCorner from, BoxCorner to) = Box.Ends(side);
                Point2 p = footprint.Corner(from), q = footprint.Corner(to);
                return (p.X.ToInches(), p.Y.ToInches(), q.X.ToInches(), q.Y.ToInches());
            }
        }

        return null;
    }

    static readonly BoxEdge[] Sides = [BoxEdge.South, BoxEdge.East, BoxEdge.North, BoxEdge.West];

    static readonly BoxCorner[] Corners = [BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest];
}
