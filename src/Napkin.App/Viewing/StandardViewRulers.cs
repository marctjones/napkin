using Avalonia;
using Napkin.Core.Geometry;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>A ruler mark of a standard view, where it falls on the screen.</summary>
/// <param name="Screen">Its screen coordinate: x on the top ruler, y on the left one.</param>
/// <param name="Mark">What it is and what it reads, in world values.</param>
public readonly record struct PlacedTick(double Screen, RulerTick Mark);

/// <summary>A grid line of a standard view, where it falls on the screen.</summary>
/// <param name="Screen">Its screen coordinate: x for a line up the screen, y for one across it.</param>
/// <param name="Line">Its world value and weight.</param>
public readonly record struct PlacedGridLine(double Screen, GridLine Line);

/// <summary>
/// The rulers and grid of a standard view seen through a camera (docs/design/standard-views.md §5.2):
/// the world values <see cref="ViewGrid"/> and <see cref="RulerTicks"/> choose, put where the camera
/// projects them. Pure, so a test reads the numbers on the rulers without a window.
/// </summary>
/// <param name="Top">The top ruler's marks, left to right on the screen.</param>
/// <param name="Left">The left ruler's marks, top to bottom on the screen.</param>
/// <param name="Across">The grid lines running up the screen, one per value along screen right, left to right.</param>
/// <param name="Down">The grid lines running across the screen, one per value along screen up, top to bottom.</param>
public sealed record StandardViewRulers(
    IReadOnlyList<PlacedTick> Top,
    IReadOnlyList<PlacedTick> Left,
    IReadOnlyList<PlacedGridLine> Across,
    IReadOnlyList<PlacedGridLine> Down)
{
    /// <summary>The rulers and the grid a camera looking along a view shows.</summary>
    public static StandardViewRulers Of(Camera camera, StandardView view)
    {
        (SignedAxis right, SignedAxis up, _) = StandardViewFrame.Axes(view);
        Vector3d rightVector = StandardViews.Along(right), upVector = StandardViews.Along(up);
        Vector3d topLeft = camera.OnCenterPlane(new Point(0, 0));
        Vector3d bottomRight = camera.OnCenterPlane(new Point(camera.Viewport.Width, camera.Viewport.Height));

        (double lowAcross, double highAcross) = ViewGrid.WorldSpan(right, Vector3d.Dot(topLeft, rightVector), Vector3d.Dot(bottomRight, rightVector));
        (double lowDown, double highDown) = ViewGrid.WorldSpan(up, Vector3d.Dot(bottomRight, upVector), Vector3d.Dot(topLeft, upVector));

        double X(double world) => camera.Project(camera.Center + (rightVector * (ViewGrid.InView(right, world) - Vector3d.Dot(camera.Center, rightVector)))).X;
        double Y(double world) => camera.Project(camera.Center + (upVector * (ViewGrid.InView(up, world) - Vector3d.Dot(camera.Center, upVector)))).Y;

        double ppi = camera.PixelsPerInch;
        return new StandardViewRulers(
            [.. RulerTicks.Ticks(lowAcross, highAcross, ppi).Select(mark => new PlacedTick(X(mark.Inches), mark)).OrderBy(placed => placed.Screen)],
            [.. RulerTicks.Ticks(lowDown, highDown, ppi).Select(mark => new PlacedTick(Y(mark.Inches), mark)).OrderBy(placed => placed.Screen)],
            [.. ViewGrid.Lines(view, right.Axis, lowAcross, highAcross, ppi).Select(line => new PlacedGridLine(X(line.World), line)).OrderBy(placed => placed.Screen)],
            [.. ViewGrid.Lines(view, up.Axis, lowDown, highDown, ppi).Select(line => new PlacedGridLine(Y(line.World), line)).OrderBy(placed => placed.Screen)]);
    }
}
