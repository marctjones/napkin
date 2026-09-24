using Avalonia;
using Avalonia.Media;

namespace Napkin.App.Viewing;

/// <summary>The sheet the drawing is on (#142). Cosmetic only; every sheet but <see cref="Screen"/> is light whatever the theme.</summary>
public enum SketchPaper
{
    /// <summary>The clean on-screen look, in the theme's colours.</summary>
    Screen,

    /// <summary>Off-white with a fine blue-green square grid, a heavier line every fifth.</summary>
    Graph,

    /// <summary>A near-white sheet with no grid.</summary>
    Plain,

    /// <summary>A warm off-white paper napkin: a faint embossed diamond pattern and a darker border.</summary>
    Napkin,
}

/// <summary>The pencil the lines are drawn with (#142).</summary>
public enum SketchLine
{
    /// <summary>The clean on-screen line.</summary>
    Clean,

    /// <summary>A fine graphite line, slightly irregular and pressed twice.</summary>
    Pencil,

    /// <summary>A broad flat line in a warm dark grey-brown, with a soft edge.</summary>
    Carpenter,
}

/// <summary>The two independent choices that make up the sketch look. The default is the clean look.</summary>
public readonly record struct SketchLook(SketchPaper Paper = SketchPaper.Screen, SketchLine Line = SketchLine.Clean)
{
    /// <summary>Whether this is the ordinary clean look, which draws exactly as before.</summary>
    public bool IsClean => Paper == SketchPaper.Screen && Line == SketchLine.Clean;
}

/// <summary>The sheet and ink colours of the sketch look. Fixed: paper is light in either theme.</summary>
public static class SketchColours
{
    public static readonly Color Graph = Color.Parse("#F5F6EE");
    public static readonly Color GraphMinor = Color.Parse("#C4DCD8");
    public static readonly Color GraphMajor = Color.Parse("#8DB9B3");
    public static readonly Color Plain = Color.Parse("#FBFBF8");
    public static readonly Color PlainGround = Color.Parse("#E6E6DF");
    public static readonly Color Napkin = Color.Parse("#F1E8D6");
    public static readonly Color NapkinGround = Color.Parse("#DDCFB4");
    public static readonly Color NapkinBorder = Color.Parse("#D3C2A2");

    /// <summary>Graphite.</summary>
    public static readonly Color PencilInk = Color.Parse("#3B3B40");

    /// <summary>The carpenter's pencil: warm, dark, grey-brown.</summary>
    public static readonly Color CarpenterInk = Color.Parse("#4A3F36");

    /// <summary>The colour of the sheet itself.</summary>
    public static Color Sheet(SketchPaper paper) => paper switch
    {
        SketchPaper.Graph => Graph,
        SketchPaper.Plain => Plain,
        SketchPaper.Napkin => Napkin,
        _ => throw new ArgumentOutOfRangeException(nameof(paper)),
    };

    /// <summary>The ink of a line, or null for the clean line, which keeps the palette's own colours.</summary>
    public static Color? Ink(SketchLine line) => line switch
    {
        SketchLine.Pencil => PencilInk,
        SketchLine.Carpenter => CarpenterInk,
        _ => null,
    };
}

/// <summary>
/// The geometry of a hand-drawn line: a straight segment made slightly irregular, the same way every
/// time. Pure and screen-space; it draws nothing and touches no model value.
/// </summary>
public static class SketchStroke
{
    /// <summary>A segment shorter than this many pixels is drawn straight.</summary>
    public const double ShortSegment = 6;

    /// <summary>A long segment is broken into pieces about this many pixels long, each end wandering on its own.</summary>
    public const double Spacing = 40;

    /// <summary>The furthest, in pixels, a point of a line of this kind strays from the straight line.</summary>
    public static double MaxOffset(SketchLine line) => line switch
    {
        SketchLine.Pencil => 1.2,
        SketchLine.Carpenter => 1.8,
        _ => 0,
    };

    /// <summary>
    /// The points of the line from <paramref name="a"/> to <paramref name="b"/>: both ends exactly where they
    /// were asked, the points between them off the straight line by at most <see cref="MaxOffset"/>. The
    /// same inputs give the same points; <paramref name="seed"/> (see <see cref="SeedOf"/>) picks which
    /// irregularity this line has. Drawn from <paramref name="b"/> to <paramref name="a"/> it is the same curve, reversed.
    /// </summary>
    public static IReadOnlyList<Point> Wobble(Point a, Point b, int seed, SketchLine line)
    {
        double amplitude = MaxOffset(line);
        double length = Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));
        if (amplitude <= 0 || length < ShortSegment)
        {
            return [a, b];
        }

        // Always computed from the end that sorts first, so the line does not depend on which way it was asked for.
        bool reversed = b.X < a.X || (b.X == a.X && b.Y < a.Y);
        Point from = reversed ? b : a;
        Point to = reversed ? a : b;

        int pieces = Math.Max(2, (int)Math.Ceiling(length / Spacing));
        double dx = (to.X - from.X) / length;
        double dy = (to.Y - from.Y) / length;
        Point[] points = new Point[pieces + 1];
        points[0] = from;
        points[pieces] = to;
        for (int i = 1; i < pieces; i++)
        {
            double t = (double)i / pieces;
            double offset = amplitude * Unit(seed, i);
            points[i] = new Point(
                from.X + ((to.X - from.X) * t) - (dy * offset),
                from.Y + ((to.Y - from.Y) * t) + (dx * offset));
        }

        if (reversed)
        {
            Array.Reverse(points);
        }

        return points;
    }

    /// <summary>
    /// A stable seed for a segment from its ends in model space (inches), the same whichever end is
    /// first, so a line keeps its irregularity as the view pans and zooms and never shimmers.
    /// </summary>
    public static int SeedOf(double ax, double ay, double bx, double by)
    {
        long x1 = (long)Math.Round(ax * 100), y1 = (long)Math.Round(ay * 100);
        long x2 = (long)Math.Round(bx * 100), y2 = (long)Math.Round(by * 100);
        if (x2 < x1 || (x2 == x1 && y2 < y1))
        {
            (x1, y1, x2, y2) = (x2, y2, x1, y1);
        }

        unchecked
        {
            uint h = 2166136261;
            foreach (long v in new[] { x1, y1, x2, y2 })
            {
                h = (h ^ (uint)v) * 16777619;
                h = (h ^ (uint)(v >> 32)) * 16777619;
            }

            return (int)h;
        }
    }

    /// <summary>A repeatable number in [-1, 1] for a seed and an index.</summary>
    static double Unit(int seed, int index)
    {
        unchecked
        {
            uint h = (uint)seed + ((uint)index * 0x9E3779B9);
            h ^= h >> 16;
            h *= 0x85EBCA6B;
            h ^= h >> 13;
            h *= 0xC2B2AE35;
            h ^= h >> 16;
            return (h / (double)uint.MaxValue * 2) - 1;
        }
    }
}
