using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Modules.Building;

/// <summary>One property line, measured (docs/design/permit-set.md §5.3).</summary>
/// <param name="Index">Which course, from 1.</param>
/// <param name="Course">The course.</param>
/// <param name="From">Its start, derived.</param>
/// <param name="To">Its end, derived.</param>
/// <param name="Nearest">The structure's box nearest to it, or null when there is no structure.</param>
/// <param name="Distance">The least distance from any corner of the structure to the line, rounded once; null with no structure.</param>
/// <param name="Exact">Whether the line runs along a world axis, so the distance needs no ≈.</param>
/// <param name="Text">The line the panel and the sheet show.</param>
public sealed record PropertyLine(int Index, Course Course, Point2 From, Point2 To, Box? Nearest, Length? Distance, bool Exact, string Text)
{
    /// <summary>Whether the structure is at least the typed setback from this line; null when there is no setback or no structure.</summary>
    public bool? Clear => Course.Setback is { } setback && Distance is { } d ? d >= setback.Distance : null;
}

/// <summary>A site plan's measurements (§5.2, §5.3): the corners, the closure error, and each line's distance from the structure.</summary>
/// <param name="Boundary">The boundary.</param>
/// <param name="Corners">Its corners, derived.</param>
/// <param name="Closure">The closure error.</param>
/// <param name="Lines">Each course, measured.</param>
/// <param name="ClosureText">What the panel says about the closure.</param>
public sealed record SitePlanMeasure(Boundary Boundary, ImmutableArray<Point2> Corners, Length Closure, ImmutableArray<PropertyLine> Lines, string ClosureText);

/// <summary>
/// The site plan's arithmetic (permit-set §5.3): distances from the structure — every New or Existing
/// wall, deck and roof, by its four plan corners — to each property line, in double, rounded once,
/// with ≈ unless the line runs along a world axis; each compared only with the setback the person
/// typed. A setback is zoning, not the building code.
/// </summary>
public static class SitePlan
{
    /// <summary>What the panel says once: setbacks are zoning, and napkin compares only with what was typed.</summary>
    public const string ZoningNote = "Setbacks are zoning, not the building code: napkin compares the distances only with the setbacks you typed, and says nothing about whether the lot conforms.";

    static readonly FeetInchesFormat Feet = new(16);

    /// <summary>The first boundary's measurements, or null when the design has none.</summary>
    public static SitePlanMeasure? Of(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        if (sketch.Entities.Values.OfType<Boundary>().OrderBy(boundary => boundary.Id).FirstOrDefault() is not { } boundary)
        {
            return null;
        }

        Angle north = sketch.Site.North;
        ImmutableArray<Point2> corners = boundary.Corners(north);
        Length closure = boundary.ClosureError(north);

        Box[] structure =
        [
            .. sketch.Entities.Values.OfType<Box>()
                .Where(box => box.Phase is Phase.New or Phase.Existing && (Wall.Is(sketch, box) || Deck.Is(sketch, box) || Roof.Is(sketch, box)))
                .OrderBy(box => box.Id),
        ];

        List<PropertyLine> lines = [];
        for (int i = 0; i < boundary.Courses.Length; i++)
        {
            Course course = boundary.Courses[i];
            Point2 from = corners[i], to = corners[i + 1];
            bool exact = (course.Bearing.Azimuth + north).IsRightAngleMultiple;
            string label = course.Setback is { } s ? $"{Word(s.Kind)} line" : $"line {i + 1}";

            Box? nearest = null;
            double least = double.MaxValue;
            foreach (Box box in structure)
            {
                foreach (BoxCorner corner in (BoxCorner[])[BoxCorner.SouthWest, BoxCorner.SouthEast, BoxCorner.NorthEast, BoxCorner.NorthWest])
                {
                    double d = PointToSegment(box.Corner(corner), from, to);
                    if (d < least)
                    {
                        least = d;
                        nearest = box;
                    }
                }
            }

            if (nearest is null)
            {
                lines.Add(new PropertyLine(i + 1, course, from, to, null, null, exact, $"{Capital(label)}: no wall, deck or roof is drawn to measure from."));
                continue;
            }

            Length distance = new((long)Math.Round(least, MidpointRounding.ToEven));
            string measured = $"{(exact ? string.Empty : "≈ ")}{distance.Format(Feet).Text}";
            string verdict = course.Setback is { } setback
                ? distance >= setback.Distance
                    ? $" (setback {setback.Distance.Format(Feet).Text}: clear)"
                    : $" (setback {setback.Distance.Format(Feet).Text}: short by {(setback.Distance - distance).Format(Feet).Text})"
                : string.Empty;
            string name = nearest.Name.Length > 0 ? nearest.Name : "The structure";
            lines.Add(new PropertyLine(i + 1, course, from, to, nearest, distance, exact, $"{name} to {label} {measured}{verdict}"));
        }

        string closureText = closure == Length.Zero
            ? "The courses close on the point of beginning."
            : $"The courses do not close: the last corner is {closure.Format(Feet).Text} from the point of beginning. napkin reports this, as the survey's own closure is reported, and never adjusts it.";
        return new SitePlanMeasure(boundary, corners, closure, [.. lines], closureText);
    }

    /// <summary>A bearing as a survey prints it: "N 12°34'56" E".</summary>
    public static string Words(Bearing bearing)
    {
        ArgumentNullException.ThrowIfNull(bearing);
        long seconds = bearing.Angle.Arcseconds;
        return $"{(bearing.From == NorthSouth.North ? "N" : "S")} {seconds / 3600}°{seconds / 60 % 60:00}'{seconds % 60:00}\" {(bearing.Toward == EastWest.East ? "E" : "W")}";
    }

    static string Word(SetbackKind kind) => kind switch
    {
        SetbackKind.Front => "front",
        SetbackKind.Side => "side",
        _ => "rear",
    };

    static string Capital(string text) => char.ToUpperInvariant(text[0]) + text[1..];

    static double PointToSegment(Point2 p, Point2 a, Point2 b)
    {
        double px = p.X.Units, py = p.Y.Units, ax = a.X.Units, ay = a.Y.Units, bx = b.X.Units, by = b.Y.Units;
        double dx = bx - ax, dy = by - ay;
        double t = Math.Clamp((((px - ax) * dx) + ((py - ay) * dy)) / ((dx * dx) + (dy * dy)), 0, 1);
        double cx = ax + (t * dx) - px, cy = ay + (t * dy) - py;
        return Math.Sqrt((cx * cx) + (cy * cy));
    }
}
