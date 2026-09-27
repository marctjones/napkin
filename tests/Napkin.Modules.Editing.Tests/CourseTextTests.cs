using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>A boundary's courses typed as a survey prints them (permit-set §5.2, #223).</summary>
public class CourseTextTests
{
    [Theory]
    [InlineData("N 12°34'56\" E 125.50'", NorthSouth.North, 12, 34, 56, EastWest.East)]
    [InlineData("S 12-34-56 W 125.5'", NorthSouth.South, 12, 34, 56, EastWest.West)]
    [InlineData("n 12 34 56 e 125'-6\"", NorthSouth.North, 12, 34, 56, EastWest.East)]
    [InlineData("S 90° W 125.5 ft", NorthSouth.South, 90, 0, 0, EastWest.West)]
    [InlineData("N 5°07' E 125.50'", NorthSouth.North, 5, 7, 0, EastWest.East)]
    public void A_course_reads_in_the_ways_a_survey_writes_it(string line, NorthSouth from, long d, long m, long s, EastWest toward)
    {
        Assert.True(CourseText.TryParseLine(line, out Course? course, out string problem), problem);
        Assert.Equal(new Course(new Bearing(from, Angle.Degrees(d, m, s), toward), Length.Inches(1506), null), course);
    }

    [Fact]
    [Trait("Feature", "SITE-004")]
    public void Lines_read_with_their_setbacks_and_write_back_the_same()
    {
        string typed = "N 0°00'00\" E 50' side 10'\n\nN 90°00'00\" E 100' REAR 30'\nS 0°00'00\" E 50'\nN 90°00'00\" W 100' front 25'";
        Assert.True(CourseText.TryParse(typed, out ImmutableArray<Course> courses, out string problem), problem);
        Assert.Equal(4, courses.Length);
        Assert.Equal(new Setback(Length.Feet(30), SetbackKind.Rear), courses[1].Setback);
        Assert.Null(courses[2].Setback);

        string written = CourseText.Of(courses);
        Assert.Equal("N 0°00'00\" E 50'-0\" side 10'-0\"\nN 90°00'00\" E 100'-0\" rear 30'-0\"\nS 0°00'00\" E 50'-0\"\nN 90°00'00\" W 100'-0\" front 25'-0\"", written);
        Assert.True(CourseText.TryParse(written, out ImmutableArray<Course> again, out _));
        Assert.Equal(courses, again);
    }

    [Theory]
    [InlineData("north 50'", "Line 1: a course is a bearing and a distance")]
    [InlineData("N 91 E 50'", "Line 1: a bearing is at most 90°")]
    [InlineData("N 12 60 00 E 50'", "Line 1: a bearing is at most 90°")]
    [InlineData("N 12 00 00 E fifty", "Line 1: \"fifty\" is not a distance longer than zero.")]
    [InlineData("N 12 00 00 E 0'", "Line 1: \"0'\" is not a distance longer than zero.")]
    [InlineData("N 0 E 50'\nS 0 E 50' rear none", "Line 2: \"none\" is not a setback longer than zero.")]
    public void A_line_that_does_not_read_is_said_with_its_number(string typed, string problem)
    {
        Assert.False(CourseText.TryParse(typed, out ImmutableArray<Course> courses, out string said));
        Assert.StartsWith(problem, said, StringComparison.Ordinal);
        Assert.True(courses.IsEmpty);
    }

    [Fact]
    public void Nothing_typed_is_no_courses()
    {
        Assert.True(CourseText.TryParse(null, out ImmutableArray<Course> courses, out _));
        Assert.True(courses.IsEmpty);
    }

    [Theory]
    [InlineData("0", 0, 0, 0)]
    [InlineData("12°30'", 12, 30, 0)]
    [InlineData(" 12 30 15 ", 12, 30, 15)]
    [InlineData("359-59-59", 359, 59, 59)]
    public void North_reads_as_degrees_minutes_and_seconds(string typed, long d, long m, long s)
    {
        Assert.True(CourseText.TryParseNorth(typed, out Angle north));
        Assert.Equal(Angle.Degrees(d, m, s), north);
        Assert.True(CourseText.TryParseNorth(CourseText.NorthWords(north), out Angle again));
        Assert.Equal(north, again);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("north")]
    [InlineData("360")]
    [InlineData("12 60")]
    [InlineData("12 30 60")]
    public void North_that_does_not_read_is_refused(string? typed) => Assert.False(CourseText.TryParseNorth(typed, out _));
}
