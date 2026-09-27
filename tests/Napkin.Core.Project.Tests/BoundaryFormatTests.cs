using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// Scene format 15 (docs/design/permit-set.md §6, #223): a lot's boundary as the survey prints it and
/// the site's north, written every time and read back equal; each refusal names its field.
/// </summary>
public class BoundaryFormatTests
{
    const string Course = """{ "bearing": { "from": "N", "angle": 0, "toward": "E" }, "distance": 614400, "setback": { "distance": 122880, "kind": "side" } }""";
    const string East = """{ "bearing": { "from": "N", "angle": 324000, "toward": "E" }, "distance": 1228800, "setback": null }""";
    const string South = """{ "bearing": { "from": "S", "angle": 0, "toward": "E" }, "distance": 614400, "setback": null }""";
    const string West = """{ "bearing": { "from": "N", "angle": 324000, "toward": "W" }, "distance": 1228800, "setback": { "distance": 307200, "kind": "front" } }""";

    static string Lot(string courses = $"{Course}, {East}, {South}, {West}") => Scenes.OneBox.With(
        "\"entities\": [",
        "\"entities\": [ { \"id\": \"0192f1a0-0000-4000-8000-0000000000b1\", \"type\": \"boundary\", \"layer\": \"00000000-0000-0000-0000-000000000001\", \"name\": \"Lot\", \"phase\": \"existing\", "
        + $"\"start\": {{ \"x\": 0, \"y\": 0 }}, \"courses\": [ {courses} ] }},");

    [Fact]
    [Trait("Feature", "SITE-004")]
    public void A_boundary_and_north_load_as_written_and_round_trip()
    {
        Sketch sketch = Scenes.Accept(Lot().With("\"north\": 0", "\"north\": 3600"));
        Boundary lot = Assert.Single(sketch.Entities.Values.OfType<Boundary>());
        Assert.Equal(4, lot.Courses.Length);
        Assert.Equal(new Course(new Bearing(NorthSouth.North, Angle.Zero, EastWest.East), Length.Feet(50), new Setback(Length.Feet(10), SetbackKind.Side)), lot.Courses[0]);
        Assert.Equal(new Setback(Length.Feet(25), SetbackKind.Front), lot.Courses[3].Setback);
        Assert.Equal(Angle.Degrees(1), sketch.Site.North);
        Assert.Equal(sketch, Scenes.Accept(SceneWriter.WriteToText(sketch)));
    }

    [Fact]
    public void Each_bad_course_is_refused_by_name()
    {
        Scenes.RefuseWith(Lot().With("\"from\": \"N\", \"angle\": 0", "\"from\": \"X\", \"angle\": 0"), LoadProblemKind.UnknownValue, "X");
        Scenes.RefuseWith(Lot().With("\"toward\": \"W\"", "\"toward\": \"Q\""), LoadProblemKind.UnknownValue, "Q");
        Scenes.RefuseWith(Lot().With("\"kind\": \"side\"", "\"kind\": \"back\""), LoadProblemKind.UnknownValue, "back");
        Scenes.RefuseWith(Lot().With("\"angle\": 0, \"toward\": \"E\" }, \"distance\": 614400, \"setback\": {", "\"angle\": 324001, \"toward\": \"E\" }, \"distance\": 614400, \"setback\": {"), LoadProblemKind.InvalidValue, "angle");
        Scenes.RefuseWith(Lot($"{Course}, {East}, {South.Replace("614400", "0", StringComparison.Ordinal)}, {West}"), LoadProblemKind.InvalidValue, "distance");
        Scenes.RefuseWith(Lot().With("\"distance\": 122880, \"kind\": \"side\"", "\"distance\": 0, \"kind\": \"side\""), LoadProblemKind.InvalidValue, "setback");
        Scenes.RefuseWith(Lot($"{Course}, {East}"), LoadProblemKind.InvalidValue, "courses");
        Scenes.RefuseWith(Lot().With("\"distance\": 1228800, \"setback\": null }, {", "\"distance\": 1228800, \"setback\": null, \"monument\": \"iron pin\" }, {"), LoadProblemKind.UnknownField, "monument");
        Scenes.RefuseWith(Lot().With("\"from\": \"S\", \"angle\": 0, \"toward\": \"E\"", "\"from\": \"S\", \"angle\": 0, \"toward\": \"E\", \"minutes\": 3"), LoadProblemKind.UnknownField, "minutes");
        Scenes.RefuseWith(Scenes.OneBox.With("\"north\": 0, ", string.Empty), LoadProblemKind.MissingField, "north");
    }
}
