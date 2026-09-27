namespace Napkin.Interop.Pdf;

/// <summary>A point on the page, in PDF points from its bottom-left corner, y up.</summary>
/// <param name="X">Points from the left edge.</param>
/// <param name="Y">Points from the bottom edge.</param>
public readonly record struct PagePoint(double X, double Y);

/// <summary>A straight piece of line on the page, from one point to another.</summary>
/// <param name="From">Where it starts.</param>
/// <param name="To">Where it ends.</param>
public readonly record struct PageSegment(PagePoint From, PagePoint To);

/// <summary>
/// A dashed line as the solid pieces it is made of. Excise.Core's pen has no dash pattern yet
/// (marctjones/excise#1851), so the sheet draws each dash as its own short solid line — still
/// vector, only more of it. The pattern restarts at the start of every line, as each line on the
/// screen is drawn with its own pen (the dash offset is zero).
/// </summary>
public static class Dashes
{
    /// <summary>
    /// The "on" pieces of a line from <paramref name="from"/> to <paramref name="to"/> dashed with
    /// <paramref name="pattern"/> (on, off, on, off…, in points, repeated to the end, as a PDF dash
    /// array reads); the whole line for an empty pattern.
    /// </summary>
    public static IReadOnlyList<PageSegment> Along(PagePoint from, PagePoint to, IReadOnlyList<double> pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        double dx = to.X - from.X, dy = to.Y - from.Y;
        double length = Math.Sqrt((dx * dx) + (dy * dy));
        if (pattern.Count == 0 || length == 0)
        {
            return length == 0 ? [] : [new PageSegment(from, to)];
        }

        if (pattern.Any(step => !(step >= 0) || double.IsInfinity(step)) || !(pattern.Sum() > 0))
        {
            throw new ArgumentException("A dash pattern is finite lengths, none negative, not all zero.", nameof(pattern));
        }

        PagePoint At(double distance) => distance >= length
            ? to
            : new(from.X + (dx * distance / length), from.Y + (dy * distance / length));

        List<PageSegment> pieces = [];
        double at = 0;
        for (int step = 0; at < length; step++)
        {
            double next = Math.Min(at + pattern[step % pattern.Count], length);
            if (step % 2 == 0 && next > at)
            {
                pieces.Add(new PageSegment(At(at), At(next)));
            }

            at = next;
        }

        return pieces;
    }
}
