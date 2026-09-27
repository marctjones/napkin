using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;

namespace Napkin.Modules.Editing;

/// <summary>
/// A boundary's courses typed as a survey prints them (docs/design/permit-set.md §5.2): one per line,
/// "N 12°34'56" E 125.50'", with an optional setback after it, "rear 30'". Degrees, minutes and seconds
/// may be written with °, ' and " or with spaces or dashes; seconds are whole, as a survey prints them.
/// </summary>
public static partial class CourseText
{
    [GeneratedRegex("""^\s*(?<ns>[NnSs])\s*(?<d>\d{1,2})\s*(?:°|-|\s)\s*(?:(?<m>\d{1,2})\s*(?:'|-|\s)\s*(?:(?<s>\d{1,2})\s*"?\s*)?)?(?<ew>[EeWw])\s+(?<rest>.+?)\s*$""")]
    private static partial Regex CourseLine();

    [GeneratedRegex(@"^(?<distance>.+?)(?:\s+(?<kind>front|side|rear)\s+(?<setback>.+))?$", RegexOptions.IgnoreCase)]
    private static partial Regex DistanceAndSetback();

    /// <summary>Reads every non-blank line as a course; the first line that does not read is said with its number.</summary>
    public static bool TryParse(string? text, out ImmutableArray<Course> courses, out string problem)
    {
        courses = [];
        problem = string.Empty;
        ImmutableArray<Course>.Builder read = ImmutableArray.CreateBuilder<Course>();
        string[] lines = (text ?? string.Empty).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length == 0)
            {
                continue;
            }

            if (!TryParseLine(lines[i], out Course? course, out string why))
            {
                problem = $"Line {i + 1}: {why}";
                return false;
            }

            read.Add(course!);
        }

        courses = read.ToImmutable();
        return true;
    }

    /// <summary>Reads one course: a bearing, a distance, and optionally a setback's kind and distance.</summary>
    public static bool TryParseLine(string line, out Course? course, out string problem)
    {
        course = null;
        Match match = CourseLine().Match(line ?? string.Empty);
        if (!match.Success)
        {
            problem = "a course is a bearing and a distance, like N 12°34'56\" E 125.50'.";
            return false;
        }

        long degrees = long.Parse(match.Groups["d"].Value, CultureInfo.InvariantCulture);
        long minutes = match.Groups["m"].Success ? long.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        long seconds = match.Groups["s"].Success ? long.Parse(match.Groups["s"].Value, CultureInfo.InvariantCulture) : 0;
        if (minutes > 59 || seconds > 59 || (degrees * 3600) + (minutes * 60) + seconds > 324000)
        {
            problem = "a bearing is at most 90°, with minutes and seconds under 60.";
            return false;
        }

        Match rest = DistanceAndSetback().Match(match.Groups["rest"].Value);
        if (!Length.TryParse(rest.Groups["distance"].Value, out Length distance, out _) || distance <= Length.Zero)
        {
            problem = $"\"{rest.Groups["distance"].Value}\" is not a distance longer than zero.";
            return false;
        }

        Setback? setback = null;
        if (rest.Groups["kind"].Success)
        {
            if (!Length.TryParse(rest.Groups["setback"].Value, out Length back, out _) || back <= Length.Zero)
            {
                problem = $"\"{rest.Groups["setback"].Value}\" is not a setback longer than zero.";
                return false;
            }

            setback = new Setback(back, rest.Groups["kind"].Value.ToLowerInvariant() switch
            {
                "front" => SetbackKind.Front,
                "side" => SetbackKind.Side,
                _ => SetbackKind.Rear,
            });
        }

        Bearing bearing = new(
            char.ToUpperInvariant(match.Groups["ns"].Value[0]) == 'N' ? NorthSouth.North : NorthSouth.South,
            Angle.Degrees(degrees, minutes, seconds),
            char.ToUpperInvariant(match.Groups["ew"].Value[0]) == 'E' ? EastWest.East : EastWest.West);
        course = new Course(bearing, distance, setback);
        problem = string.Empty;
        return true;
    }

    /// <summary>The courses back as text, one per line, in the same form they are typed.</summary>
    public static string Of(ImmutableArray<Course> courses)
    {
        FeetInchesFormat feet = new(16);
        return string.Join("\n", courses.Select(course =>
            $"{SitePlan.Words(course.Bearing)} {course.Distance.Format(feet).Text}"
            + (course.Setback is { } s ? $" {s.Kind.ToString().ToLowerInvariant()} {s.Distance.Format(feet).Text}" : string.Empty)));
    }
}
