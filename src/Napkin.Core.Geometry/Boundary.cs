using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>The north–south half of a survey bearing's quadrant: "N" or "S".</summary>
public enum NorthSouth
{
    /// <summary>Measured from north.</summary>
    North,

    /// <summary>Measured from south.</summary>
    South,
}

/// <summary>The east–west half of a survey bearing's quadrant: "E" or "W".</summary>
public enum EastWest
{
    /// <summary>Turned toward east.</summary>
    East,

    /// <summary>Turned toward west.</summary>
    West,
}

/// <summary>What a setback line is called on the lot (permit-set §5.3): zoning's words, typed by the person.</summary>
public enum SetbackKind
{
    /// <summary>A front setback.</summary>
    Front,

    /// <summary>A side setback.</summary>
    Side,

    /// <summary>A rear setback.</summary>
    Rear,
}

/// <summary>A survey bearing as a survey prints it: "N 12°34'56" E" (permit-set §5.2).</summary>
/// <param name="From">North or south, the meridian it is measured from.</param>
/// <param name="Angle">The angle from that meridian, 0° to 90° inclusive, exact in arcseconds.</param>
/// <param name="Toward">East or west, the way it turns.</param>
public sealed record Bearing(NorthSouth From, Angle Angle, EastWest Toward)
{
    /// <summary>
    /// The azimuth, clockwise from north: N a E = a, S a E = 180° − a, S a W = 180° + a, N a W = 360° − a.
    /// </summary>
    public Angle Azimuth => (From, Toward) switch
    {
        (NorthSouth.North, EastWest.East) => Angle,
        (NorthSouth.South, EastWest.East) => Angle.Straight - Angle,
        (NorthSouth.South, EastWest.West) => Angle.Straight + Angle,
        _ => -Angle,
    };
}

/// <summary>A setback on one course, typed by the person and labelled: zoning, not the building code (§5.3).</summary>
/// <param name="Distance">How far in from the course, longer than zero.</param>
/// <param name="Kind">Front, side or rear.</param>
public sealed record Setback(Length Distance, SetbackKind Kind);

/// <summary>One course of a boundary: a bearing and a distance, as the survey prints them, and an optional setback.</summary>
/// <param name="Bearing">The course's bearing.</param>
/// <param name="Distance">Its length, longer than zero.</param>
/// <param name="Setback">The setback typed for this line, or null.</param>
public sealed record Course(Bearing Bearing, Length Distance, Setback? Setback);

/// <summary>
/// A lot's property lines as a survey prints them (docs/design/permit-set.md §5.2, format version 15):
/// the point of beginning and its courses. The corners are derived, never stored: each course's end
/// is the running sum from the point of beginning of distance × (sin, cos) of the course's world
/// direction (its bearing's azimuth plus the site's north, clockwise from +Y), computed in double and
/// rounded to the 1/1024" grid once per corner. The closure error — the last corner's distance from
/// the point of beginning — is reported, never adjusted away; the grid adds at most √2/1024" to it.
/// </summary>
/// <param name="Id">The boundary's id.</param>
/// <param name="Layer">Its layer: Site.</param>
/// <param name="Start">The point of beginning, placed by the person.</param>
/// <param name="Courses">The courses, in the survey's order; at least three.</param>
public sealed record Boundary(EntityId Id, LayerId Layer, Point2 Start, ImmutableArray<Course> Courses) : Entity(Id, Layer)
{
    /// <summary>The corners: the point of beginning, then each course's end; the last is where the survey closes, or does not.</summary>
    public ImmutableArray<Point2> Corners(Angle north)
    {
        ImmutableArray<Point2>.Builder corners = ImmutableArray.CreateBuilder<Point2>(Courses.Length + 1);
        corners.Add(Start);
        double x = Start.X.Units, y = Start.Y.Units;
        foreach (Course course in Courses)
        {
            double radians = (course.Bearing.Azimuth + north).Arcseconds * Math.PI / 648000d;
            x += course.Distance.Units * Math.Sin(radians);
            y += course.Distance.Units * Math.Cos(radians);
            corners.Add(new Point2(new Length((long)Math.Round(x, MidpointRounding.ToEven)), new Length((long)Math.Round(y, MidpointRounding.ToEven))));
        }

        return corners.MoveToImmutable();
    }

    /// <summary>How far the last corner lands from the point of beginning, rounded once: zero when the survey closes on the grid.</summary>
    public Length ClosureError(Angle north)
    {
        Point2 last = Corners(north)[^1];
        double dx = last.X.Units - Start.X.Units, dy = last.Y.Units - Start.Y.Units;
        return new Length((long)Math.Round(Math.Sqrt((dx * dx) + (dy * dy)), MidpointRounding.ToEven));
    }

    /// <inheritdoc/>
    public override Entity OnLayer(LayerId layer) => this with { Layer = layer };

    /// <summary>Equality by value, the courses compared as a sequence.</summary>
    public bool Equals(Boundary? other)
        => base.Equals(other) && Start == other!.Start && Courses.SequenceEqual(other.Courses);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(base.GetHashCode());
        hash.Add(Start);
        foreach (Course course in Courses)
        {
            hash.Add(course);
        }

        return hash.ToHashCode();
    }
}

/// <summary>The rules a boundary keeps (§5.2).</summary>
public static class BoundaryRules
{
    /// <summary>Why these courses are refused, in words, or null when they are fine.</summary>
    public static string? Refusal(ImmutableArray<Course> courses)
    {
        if (courses.Length < 3)
        {
            return "a lot's boundary has at least three courses";
        }

        if (courses.Any(course => course.Distance <= Length.Zero))
        {
            return "every course is longer than zero";
        }

        if (courses.Any(course => course.Bearing.Angle.Arcseconds > Angle.Right.Arcseconds))
        {
            return "a bearing's angle is from 0° to 90°";
        }

        return courses.Any(course => course.Setback is { Distance: var d } && d <= Length.Zero)
            ? "a setback is longer than zero"
            : null;
    }
}
