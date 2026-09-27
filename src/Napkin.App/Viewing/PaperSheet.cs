using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Interop.Pdf;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// The on-screen sheet as paper (#25): each standard view built by the very calls a locked
/// <see cref="ModelView"/> draws with — <see cref="StandardViewEdges.Of"/> over
/// <see cref="ModelScene.Of(Sketch)"/> for its edges, split visible and hidden, and
/// <see cref="DimensionLayout.Measure(Sketch, StandardView)"/> for its dimensions — handed to
/// <see cref="SheetPdf"/> in the view's own inches. Nothing here measures or splits anything itself,
/// so the screen and the sheet cannot draw a view differently.
/// </summary>
public static class PaperSheet
{
    /// <summary>The views the sheet shows, in its panes' reading order: those <see cref="SheetLayout"/> locks a pane to.</summary>
    public static IReadOnlyList<StandardView> Views { get; } =
        [.. SheetLayout.Panes(2, 2).Select(pane => pane.View).OfType<StandardView>()];

    /// <summary>The whole sheet: the three views, the notes and the title block.</summary>
    /// <param name="sketch">The design, as the canvas shows it.</param>
    /// <param name="projectName">What the title block calls it.</param>
    /// <param name="date">The day it is printed.</param>
    /// <param name="format">How lengths are written, as the canvas writes them.</param>
    /// <param name="packs">The code packs napkin found.</param>
    /// <param name="hiddenEdges">Whether hidden edges are drawn, as View ▸ Hidden edges has them on screen.</param>
    public static PlanSheet Of(Sketch sketch, string projectName, DateOnly date, LengthFormat format, CodePacks packs, bool hiddenEdges)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(packs);
        ModelScene scene = ModelScene.Of(sketch);
        return new PlanSheet(
            new TitleBlock(projectName, date, SheetNotes.CodeLine(sketch, packs)),
            format,
            [.. Views.Select(view => View(sketch, scene, view, format, hiddenEdges))],
            SheetNotes.Heading(sketch, packs),
            SheetNotes.Of(sketch, packs, MaterialsLibrary.Shipped));
    }

    /// <summary>One view of a design as the sheet draws it.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="view">Which view.</param>
    /// <param name="format">How its dimensions' labels are written.</param>
    /// <param name="hiddenEdges">Whether its hidden edges are drawn.</param>
    public static DrawingView View(Sketch sketch, StandardView view, LengthFormat format, bool hiddenEdges)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(format);
        return View(sketch, ModelScene.Of(sketch), view, format, hiddenEdges);
    }

    static DrawingView View(Sketch sketch, ModelScene scene, StandardView view, LengthFormat format, bool hiddenEdges)
    {
        StandardViewEdges edges = StandardViewEdges.Of(scene, StandardViews.CameraFor(view, Camera.Isometric()), view);

        // An opening's outline is dashed, as the locked view strokes its layer's dashed style.
        Dictionary<LayerId, string> layerNames = sketch.Layers.ToDictionary(layer => layer.Id, layer => layer.Name);
        bool[] opening =
        [
            .. edges.Polygons.Select(polygon => sketch.Find(polygon.Box) is { } entity
                && DesignLayers.StyleName(sketch, entity, layerNames) == DesignLayers.Opening),
        ];

        static DrawingPoint At(FlatVertex vertex) => new(vertex.U, vertex.V);
        List<DrawingLine> lines = [];
        if (hiddenEdges)
        {
            lines.AddRange(edges.Edges.Hidden.Select(piece => new DrawingLine(LineKind.Hidden, At(piece.From), At(piece.To))));
        }

        lines.AddRange(edges.Edges.Visible.Select(piece => new DrawingLine(LineKind.Visible, At(piece.From), At(piece.To), opening[piece.Face])));

        static DrawingPoint Seen(ViewPoint point) => new(point.Along, point.Across);
        return new DrawingView(
            view,
            lines,
            [
                .. DimensionLayout.Measure(sketch, view).Select(dimension => new DrawingDimension(
                    dimension.Label(format),
                    Seen(dimension.From),
                    Seen(dimension.To),
                    Seen(dimension.LineFrom),
                    Seen(dimension.LineTo))),
            ]);
    }
}
