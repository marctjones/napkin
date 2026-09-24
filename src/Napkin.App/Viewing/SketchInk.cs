using Avalonia;
using Avalonia.Media;

namespace Napkin.App.Viewing;

/// <summary>Draws the sketch look's lines and sheets (#142). Presentation only; see <see cref="SketchStroke"/> for the geometry.</summary>
internal static class SketchInk
{
    /// <summary>One hand-drawn line. The clean line is not drawn here.</summary>
    public static void Stroke(
        DrawingContext context,
        SketchLine line,
        Color ink,
        bool dashed,
        Point a,
        Point b,
        int seed)
    {
        if (line == SketchLine.Carpenter)
        {
            IReadOnlyList<Point> points = SketchStroke.Wobble(a, b, seed, line);
            Polyline(context, points, Pen(ink, 0.3, 3.6, dashed));
            Polyline(context, points, Pen(ink, 0.92, 2.4, dashed));
            return;
        }

        // Pencil: pressed twice, the second pass not quite where the first was.
        Polyline(context, SketchStroke.Wobble(a, b, seed, line), Pen(ink, 0.85, 0.9, dashed));
        Polyline(context, SketchStroke.Wobble(a, b, unchecked((seed * 31) + 17), line), Pen(ink, 0.55, 0.7, dashed));
    }

    static Pen Pen(Color ink, double opacity, double width, bool dashed) => new(
        new SolidColorBrush(ink, opacity),
        width,
        dashed ? new DashStyle([4 * 1.2 / width, 3 * 1.2 / width], 0) : null,
        PenLineCap.Square,
        PenLineJoin.Round);

    static void Polyline(DrawingContext context, IReadOnlyList<Point> points, Pen pen)
    {
        if (points.Count == 2)
        {
            context.DrawLine(pen, points[0], points[1]);
            return;
        }

        StreamGeometry geometry = new();
        using (StreamGeometryContext figure = geometry.Open())
        {
            figure.BeginFigure(points[0], isFilled: false);
            for (int i = 1; i < points.Count; i++)
            {
                figure.LineTo(points[i]);
            }

            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    /// <summary>The sheet's own marks: the napkin's embossed diamonds and border. The other sheets are flat colour.</summary>
    public static void Paper(DrawingContext context, SketchPaper paper, Rect viewport)
    {
        if (paper != SketchPaper.Napkin)
        {
            return;
        }

        const double pitch = 22;
        StreamGeometry diamonds = new();
        using (StreamGeometryContext figure = diamonds.Open())
        {
            int row = 0;
            for (double y = 11; y < viewport.Height; y += pitch, row++)
            {
                for (double x = row % 2 == 0 ? 11 : 22; x < viewport.Width; x += pitch * 2)
                {
                    figure.BeginFigure(new Point(x, y - 3.2), isFilled: true);
                    figure.LineTo(new Point(x + 2.2, y));
                    figure.LineTo(new Point(x, y + 3.2));
                    figure.LineTo(new Point(x - 2.2, y));
                    figure.EndFigure(isClosed: true);
                }
            }
        }

        // Embossed: a light edge one way, a shade the other.
        using (context.PushTransform(Matrix.CreateTranslation(0.8, 0.8)))
        {
            context.DrawGeometry(new SolidColorBrush(Colors.White, 0.7), null, diamonds);
        }

        using (context.PushTransform(Matrix.CreateTranslation(-0.5, -0.5)))
        {
            context.DrawGeometry(new SolidColorBrush(Color.Parse("#B9A47A"), 0.28), null, diamonds);
        }

        context.DrawRectangle(
            null,
            new Pen(new SolidColorBrush(SketchColours.NapkinBorder), 5),
            viewport.Deflate(2.5));
    }
}
