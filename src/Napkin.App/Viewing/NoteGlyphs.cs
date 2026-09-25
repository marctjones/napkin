using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// The six small pencil glyphs a note is drawn with (docs/design/renovation-sketches.md §8): a
/// plain circle, an outlet (a circle with the two slots), a switch (an S), a light (a circle
/// crossed), a supply (a triangle) and a drain (a circle with a dot). Presentation only.
/// </summary>
internal static class NoteGlyphs
{
    /// <summary>Draws a symbol centred on a point, <paramref name="radius"/> pixels across its half.</summary>
    public static void Draw(DrawingContext context, Point centre, NoteSymbol symbol, Color ink, double radius = 5)
    {
        Pen pen = new(new SolidColorBrush(ink), 1.2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        Point At(double x, double y) => new(centre.X + (x * radius), centre.Y + (y * radius));
        switch (symbol)
        {
            case NoteSymbol.Outlet:
                context.DrawEllipse(null, pen, centre, radius, radius);
                context.DrawLine(pen, At(-0.35, -0.45), At(-0.35, 0.45));
                context.DrawLine(pen, At(0.35, -0.45), At(0.35, 0.45));
                break;

            case NoteSymbol.Switch:
                Polyline(context, pen, [At(0.6, -0.8), At(-0.1, -0.95), At(-0.6, -0.55), At(-0.35, -0.1), At(0.35, 0.1), At(0.6, 0.55), At(0.1, 0.95), At(-0.6, 0.8)]);
                break;

            case NoteSymbol.Light:
                context.DrawEllipse(null, pen, centre, radius, radius);
                context.DrawLine(pen, At(-0.7, -0.7), At(0.7, 0.7));
                context.DrawLine(pen, At(-0.7, 0.7), At(0.7, -0.7));
                break;

            case NoteSymbol.Supply:
                Polyline(context, pen, [At(0, -1), At(0.95, 0.75), At(-0.95, 0.75), At(0, -1)]);
                break;

            case NoteSymbol.Drain:
                context.DrawEllipse(null, pen, centre, radius, radius);
                context.DrawEllipse(new SolidColorBrush(ink), null, centre, radius * 0.3, radius * 0.3);
                break;

            default:
                context.DrawEllipse(null, pen, centre, radius * 0.8, radius * 0.8);
                break;
        }
    }

    static void Polyline(DrawingContext context, Pen pen, Point[] points)
    {
        StreamGeometry geometry = new();
        using (StreamGeometryContext figure = geometry.Open())
        {
            figure.BeginFigure(points[0], isFilled: false);
            for (int i = 1; i < points.Length; i++)
            {
                figure.LineTo(points[i]);
            }

            figure.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }
}

/// <summary>A note symbol's glyph as a small control, for the note panel's symbol buttons.</summary>
internal sealed class NoteGlyphIcon : Control
{
    /// <summary>The symbol drawn.</summary>
    public NoteSymbol Symbol { get; init; }

    /// <summary>The ink it is drawn in.</summary>
    public Color Ink { get; set; } = Color.Parse("#3B3B40");

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(16, 16);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        NoteGlyphs.Draw(context, new Point(Bounds.Width / 2, Bounds.Height / 2), Symbol, Ink, 6);
    }
}
