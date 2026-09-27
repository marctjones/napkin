namespace Napkin.Interop.Pdf;

/// <summary>
/// Hatching, as the cut layout on screen hatches an offcut: parallel lines rising at 45°, a fixed
/// step apart, across a rectangle and not across the rectangles cut out of it (the pieces on a
/// sheet). Excise.Core's graphics have no clipping path, so each line is clipped here, exactly.
/// </summary>
internal static class Hatch
{
    /// <summary>The pieces of the hatch lines inside <paramref name="area"/> and outside every hole.</summary>
    /// <param name="area">The rectangle hatched.</param>
    /// <param name="holes">Rectangles left clear, such as the pieces on a sheet.</param>
    /// <param name="step">How far apart the lines are, measured along the area's bottom edge.</param>
    public static IReadOnlyList<PageSegment> Lines(PageRect area, IReadOnlyList<PageRect> holes, double step)
    {
        ArgumentNullException.ThrowIfNull(holes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        List<PageSegment> lines = [];
        if (area.Width <= 0 || area.Height <= 0)
        {
            return lines;
        }

        // Each line is y − bottom = x − c, rising right; c runs from far enough left that the first
        // line clips the area's top-left corner to the area's right edge, as the screen's do.
        for (double c = area.Left - area.Height; c < area.Right; c += step)
        {
            if (Inside(area, c) is not (double from, double to))
            {
                continue;
            }

            List<(double From, double To)> kept = [(from, to)];
            foreach (PageRect hole in holes)
            {
                if (Inside(hole, c, area.Bottom) is (double cutFrom, double cutTo))
                {
                    kept = [.. kept.SelectMany(piece => Without(piece, cutFrom, cutTo))];
                }
            }

            lines.AddRange(kept.Select(piece => new PageSegment(new(piece.From, area.Bottom + piece.From - c), new(piece.To, area.Bottom + piece.To - c))));
        }

        return lines;
    }

    /// <summary>The x range where the line through (c, <paramref name="baseY"/>) at 45° lies in a rectangle; null where it misses.</summary>
    static (double From, double To)? Inside(PageRect rect, double c, double? baseY = null)
    {
        double b = baseY ?? rect.Bottom;
        // y = b + x − c lies in [bottom, top] for x in [bottom − b + c, top − b + c].
        double from = Math.Max(rect.Left, rect.Bottom - b + c), to = Math.Min(rect.Right, rect.Top - b + c);
        return to - from > 1e-9 ? (from, to) : null;
    }

    static IEnumerable<(double From, double To)> Without((double From, double To) piece, double cutFrom, double cutTo)
    {
        if (cutFrom > piece.From + 1e-9)
        {
            yield return (piece.From, Math.Min(piece.To, cutFrom));
        }

        if (cutTo < piece.To - 1e-9)
        {
            yield return (Math.Max(piece.From, cutTo), piece.To);
        }
    }
}
