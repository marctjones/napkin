using System.Collections.Immutable;
using Avalonia;
using Avalonia.Media;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>A relationship's glyph where the plan last drew it.</summary>
/// <param name="Glyph">Which relationship, of what kind, and where in the world.</param>
/// <param name="At">Its centre on the screen.</param>
public readonly record struct PlacedRelationshipGlyph(RelationshipGlyph Glyph, Point At);

/// <summary>
/// The plan's relationship glyphs as last drawn (#156, CVS-012): a mark across the line two flush
/// sides share, a dot on a corner two parts share. Hovering one says the sentence its row in the
/// relationship list says, so a person need not look away from the drawing to read it.
/// </summary>
/// <remarks>
/// Where each glyph goes is <see cref="RelationshipGlyphs"/>' (pure, in Editing); this places them
/// on the screen, draws them and answers what is under the pointer, as <see cref="JointMarkerLayer"/>
/// does for joints. A joint's marker is drawn over a glyph and its tooltip wins.
/// </remarks>
public sealed class RelationshipGlyphLayer
{
    /// <summary>A glyph's radius on the screen, in pixels.</summary>
    public const double Radius = 4.5;

    /// <summary>How far from a glyph's centre the pointer still counts as on it, in pixels.</summary>
    const double Reach = Radius + 3;

    Sketch? _sketch;
    IReadOnlyList<RelationshipGlyph> _glyphs = [];

    /// <summary>The glyphs as last placed.</summary>
    public ImmutableArray<PlacedRelationshipGlyph> Placed { get; private set; } = [];

    /// <summary>Places a design's glyphs on the screen.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="toScreen">Plan inches, east and north, to the screen.</param>
    public void Update(Sketch sketch, Func<double, double, Point> toScreen)
    {
        // Where a relationship is in the world only changes with the sketch; a redraw for a pointer
        // move or a pan reuses it.
        if (!ReferenceEquals(sketch, _sketch))
        {
            _sketch = sketch;
            _glyphs = RelationshipGlyphs.Of(sketch);
        }

        Placed = [.. _glyphs.Select(glyph => new PlacedRelationshipGlyph(glyph, toScreen(glyph.XInches, glyph.YInches)))];
    }

    /// <summary>The glyph under a screen point — the nearest one in reach — or null.</summary>
    public PlacedRelationshipGlyph? At(Point point)
    {
        PlacedRelationshipGlyph? nearest = null;
        double best = Reach;
        foreach (PlacedRelationshipGlyph placed in Placed)
        {
            double distance = Point.Distance(point, placed.At);
            if (distance <= best)
            {
                nearest = placed;
                best = distance;
            }
        }

        return nearest;
    }

    /// <summary>What the glyph under a point says: its relationship's row in the list, word for word; null off every glyph.</summary>
    /// <param name="point">The pointer.</param>
    /// <param name="editor">Whose relationship list the sentence comes from.</param>
    public string? TipAt(Point point, DesignEditor editor) =>
        At(point) is { } hit
            ? editor.RelationshipEntries().FirstOrDefault(entry => entry.Id == hit.Glyph.Id)?.Text
            : null;

    /// <summary>Draws the glyphs: a paper disc with two short strokes along a flush line, an inked dot on a shared corner.</summary>
    /// <param name="context">Where to draw.</param>
    /// <param name="ink">The relationship ink, the dimensions' colour.</param>
    /// <param name="paper">The paper, which rings each glyph so it reads over a line.</param>
    public void Draw(DrawingContext context, Color ink, Color paper)
    {
        IBrush inkBrush = new SolidColorBrush(ink), paperBrush = new SolidColorBrush(paper);
        Pen outline = new(inkBrush, 1), stroke = new(inkBrush, 1.2, lineCap: PenLineCap.Round);
        foreach (PlacedRelationshipGlyph placed in Placed)
        {
            Point centre = placed.At;
            if (placed.Glyph.Kind == GlyphKind.Coincident)
            {
                context.DrawEllipse(paperBrush, null, centre, Radius - 1, Radius - 1);
                context.DrawEllipse(inkBrush, null, centre, Radius - 2.5, Radius - 2.5);
                continue;
            }

            // The screen runs north up, so the line's north component flips.
            Vector along = new(placed.Glyph.AlongX, -placed.Glyph.AlongY);
            Vector across = new(-along.Y, along.X);
            context.DrawEllipse(paperBrush, outline, centre, Radius, Radius);
            foreach (double side in (double[])[-1.3, 1.3])
            {
                Point middle = centre + (across * side);
                context.DrawLine(stroke, middle - (along * 2.4), middle + (along * 2.4));
            }
        }
    }
}
