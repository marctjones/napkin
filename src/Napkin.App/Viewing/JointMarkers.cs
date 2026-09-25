using System.Collections.Immutable;
using System.Globalization;

using Avalonia;
using Avalonia.Media;

using Napkin.Core.Geometry;
using Napkin.Modules.Furniture;

namespace Napkin.App.Viewing;

/// <summary>What a key does to the selected joint.</summary>
public enum JointCommand
{
    /// <summary>Open it for editing (Enter).</summary>
    Edit,

    /// <summary>Remove it (Delete).</summary>
    Delete,
}

/// <summary>One joint's marker in the world: where it sits, which way its tick points, and what it says (joinery note &#xA7;5.2).</summary>
/// <param name="Id">The joint.</param>
/// <param name="Centre">The centre of the contact rectangle, or of the inserted part's face when the parts no longer touch.</param>
/// <param name="Toward">The middle of the inserted part, which the tick points at.</param>
/// <param name="Letter">B butt, G groove, R rabbet, L half-lap, T tabletop.</param>
/// <param name="Satisfied">Solid when the faces touch; hollow when they no longer do.</param>
public readonly record struct JointMarker(RelationshipId Id, Point3 Centre, Point3 Toward, char Letter, bool Satisfied);

/// <summary>A marker as placed on the screen, after the view has projected it and dropped the ones that would overlap.</summary>
/// <param name="Marker">The marker.</param>
/// <param name="At">Where its centre is on screen.</param>
/// <param name="Toward">Where its tick points, on screen.</param>
public readonly record struct PlacedJointMarker(JointMarker Marker, Point At, Point Toward);

/// <summary>
/// The joint markers both views draw: a small circle in the dimension ink with a one-letter code and a tick
/// toward the inserted part, a fixed size on the screen however far the model is zoomed, solid while the
/// parts touch and hollow when they do not. Nothing about a joint is drawn as geometry (Simplicity rule 1).
/// </summary>
public static class JointMarkers
{
    /// <summary>The circle's radius, in screen pixels.</summary>
    public const double Radius = 8;

    /// <summary>The joints of a design, in id order.</summary>
    /// <param name="sketch">The design.</param>
    public static ImmutableArray<JointMarker> Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);

        List<JointMarker> markers = [];
        foreach (Joint joint in sketch.RelationshipsInOrder.OfType<Joint>())
        {
            if (sketch.Find<Box>(joint.Inserted.Box) is not { } inserted
                || joint.Inserted.Feature.Faces is not [var face]
                || !inserted.Orientation.IsExact)
            {
                continue;
            }

            (Point3 low, Point3 high) = JointGeometry.Extent(inserted);
            Point3 middle = new(
                RelationshipChecker.Midpoint(low.X, high.X),
                RelationshipChecker.Midpoint(low.Y, high.Y),
                RelationshipChecker.Midpoint(low.Z, high.Z));

            Point3 centre = JointGeometry.Contact(sketch, joint) is { } contact ? contact.Centre : OnFace(inserted, face, middle, low, high);
            markers.Add(new JointMarker(joint.Id, centre, middle, JointTooltip.Letter(joint.Type), JointGeometry.IsSatisfied(sketch, joint)));
        }

        return [.. markers];
    }

    private static double Distance(double dx, double dy) => Math.Sqrt((dx * dx) + (dy * dy));

    private static Point3 OnFace(Box box, BoxFace face, Point3 middle, Point3 low, Point3 high)
    {
        (Axis axis, bool positive) = box.Orientation.Normal(face);
        Length plane = positive ? high.Component(axis) : low.Component(axis);
        return axis switch
        {
            Axis.X => middle with { X = plane },
            Axis.Y => middle with { Y = plane },
            _ => middle with { Z = plane },
        };
    }

    /// <summary>
    /// Places markers on the screen in id order and drops any within two radii of one already placed, so
    /// that zoomed out to where two would overlap, only one is drawn.
    /// </summary>
    /// <param name="markers">The joints' markers.</param>
    /// <param name="project">World to screen.</param>
    /// <param name="keep">A marker that is always placed, first (the selected one), or null.</param>
    public static ImmutableArray<PlacedJointMarker> Layout(IEnumerable<JointMarker> markers, Func<Point3, Point> project, RelationshipId? keep)
    {
        ArgumentNullException.ThrowIfNull(markers);
        ArgumentNullException.ThrowIfNull(project);

        List<PlacedJointMarker> placed = [];
        foreach (JointMarker marker in markers.OrderBy(m => m.Id == keep ? 0 : 1))
        {
            Point at = project(marker.Centre);
            if (placed.Any(other => Distance(other.At.X - at.X, other.At.Y - at.Y) < 2 * Radius))
            {
                continue;
            }

            placed.Add(new PlacedJointMarker(marker, at, project(marker.Toward)));
        }

        return [.. placed.OrderBy(m => m.Marker.Id)];
    }

    /// <summary>The marker under a screen point, the nearest within its radius and a little more, or null.</summary>
    /// <param name="placed">The markers as drawn.</param>
    /// <param name="point">The screen point.</param>
    public static PlacedJointMarker? HitTest(ImmutableArray<PlacedJointMarker> placed, Point point)
    {
        PlacedJointMarker? best = null;
        double nearest = Radius + 3;
        foreach (PlacedJointMarker marker in placed)
        {
            double distance = Distance(marker.At.X - point.X, marker.At.Y - point.Y);
            if (distance <= nearest)
            {
                nearest = distance;
                best = marker;
            }
        }

        return best;
    }

    /// <summary>Draws the placed markers.</summary>
    /// <param name="context">Where to draw.</param>
    /// <param name="placed">The markers as placed.</param>
    /// <param name="ink">The dimension ink.</param>
    /// <param name="paper">The paper colour a solid marker's letter is written in.</param>
    /// <param name="selection">The selection's colour.</param>
    /// <param name="selected">The selected joint, or null.</param>
    public static void Draw(DrawingContext context, ImmutableArray<PlacedJointMarker> placed, Color ink, Color paper, Color selection, RelationshipId? selected)
    {
        foreach (PlacedJointMarker item in placed)
        {
            bool picked = item.Marker.Id == selected;
            Color colour = picked ? selection : ink;
            Point at = item.At;

            Vector toward = item.Toward - at;
            if (toward.Length > Radius + 2)
            {
                Vector unit = toward / toward.Length;
                context.DrawLine(new Pen(new SolidColorBrush(colour), 1.4), at + (unit * Radius), at + (unit * (Radius + 6)));
            }

            if (picked)
            {
                context.DrawEllipse(null, new Pen(new SolidColorBrush(selection, 0.45), 3), at, Radius + 3, Radius + 3);
            }

            context.DrawEllipse(
                item.Marker.Satisfied ? new SolidColorBrush(colour) : new SolidColorBrush(paper, 0.85),
                new Pen(new SolidColorBrush(colour), 1.4),
                at,
                Radius,
                Radius);

            FormattedText letter = new(
                item.Marker.Letter.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Typeface.Default,
                11,
                new SolidColorBrush(item.Marker.Satisfied ? paper : colour));
            context.DrawText(letter, new Point(at.X - (letter.Width / 2), at.Y - (letter.Height / 2)));
        }
    }
}

/// <summary>
/// The markers a view last drew, which is what a pointer can pick and hover: each view owns one, updates it
/// as it draws and asks it what is under the pointer.
/// </summary>
public sealed class JointMarkerLayer
{
    /// <summary>The markers as last placed.</summary>
    public ImmutableArray<PlacedJointMarker> Placed { get; private set; } = [];

    /// <summary>Places the markers of a design for a view.</summary>
    /// <param name="sketch">The design.</param>
    /// <param name="project">World to screen.</param>
    /// <param name="selected">The selected joint, always placed.</param>
    public void Update(Sketch sketch, Func<Point3, Point> project, RelationshipId? selected)
    {
        // Where a joint is in the world only changes with the sketch; a redraw for a pointer move or a pan reuses it.
        if (!ReferenceEquals(sketch, _sketch))
        {
            _sketch = sketch;
            _markers = JointMarkers.Of(sketch);
        }

        Placed = JointMarkers.Layout(_markers, project, selected);
    }

    Sketch? _sketch;
    ImmutableArray<JointMarker> _markers = [];

    /// <summary>The marker under a screen point, or null.</summary>
    /// <param name="point">The point.</param>
    public PlacedJointMarker? At(Point point) => JointMarkers.HitTest(Placed, point);

    string? _tip;

    /// <summary>Puts a tooltip on a control only when it differs from the last one put, so a pointer crossing the paper does not keep resetting it.</summary>
    /// <param name="host">The view.</param>
    /// <param name="tip">The text, or null for none.</param>
    public void ShowTip(Avalonia.Controls.Control host, string? tip)
    {
        if (tip != _tip)
        {
            _tip = tip;
            Avalonia.Controls.ToolTip.SetTip(host, tip);
        }
    }

    /// <summary>The tooltip for the marker under a point, or null.</summary>
    /// <param name="point">The point.</param>
    /// <param name="sketch">The design.</param>
    /// <param name="nameOf">What to call a part.</param>
    public string? TipAt(Point point, Sketch sketch, Func<EntityId, string> nameOf)
        => At(point) is { } hit && sketch.Relationships.GetValueOrDefault(hit.Marker.Id) is Joint joint
            ? JointTooltip.Of(sketch, joint, nameOf)
            : null;
}
