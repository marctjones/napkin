using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;

using Napkin.Core.Geometry;
using Napkin.Core.Project;
using Napkin.Modules.Editing;

namespace Napkin.Interop.Dxf.Tests;

/// <summary>
/// The plan as DXF (#23), written and read back with the same library: what napkin put in is what
/// a CAD program reading the file gets — at napkin's coordinates, on napkin's layers, with every
/// dimension as plain lines and text and no associative DIMENSION.
/// </summary>
public class PlanDxfTests
{
    static readonly LengthFormat Labels = new FeetInchesFormat(16);

    static Sketch Sample(string name)
        => Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", $"{name}.scene.json"))).Sketch;

    static CadDocument RoundTrip(Sketch sketch)
    {
        using MemoryStream stream = new();
        PlanDxf.Write(sketch, Labels, stream);
        stream.Position = 0;
        using DxfReader reader = new(stream);
        return reader.Read();
    }

    [Fact]
    [Trait("Feature", "IOP-001")]
    public void The_file_is_DXF_2000_in_inches()
    {
        CadDocument read = RoundTrip(Sample("coffee-table"));

        Assert.Equal(ACadVersion.AC1015, read.Header.Version);
        Assert.Equal(ACadSharp.Types.Units.UnitsType.Inches, read.Header.InsUnits);
    }

    [Fact]
    [Trait("Feature", "IOP-001")]
    public void Every_box_is_one_closed_outline_at_its_own_plan_corners_on_its_own_layer()
    {
        Sketch sketch = Sample("coffee-table");
        CadDocument read = RoundTrip(sketch);

        Box[] boxes = [.. sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id)];
        LwPolyline[] outlines = [.. read.Entities.OfType<LwPolyline>()];
        Assert.Equal(boxes.Length, outlines.Length);
        for (int i = 0; i < boxes.Length; i++)
        {
            Assert.True(outlines[i].IsClosed);
            Assert.Equal(sketch.Layers.Single(layer => layer.Id == boxes[i].Layer).Name, outlines[i].Layer.Name);
            (double X, double Y)[] expected = [.. PlanShape.Outline(boxes[i]).Segments.Select(segment => (segment.From.X.ToInches(), segment.From.Y.ToInches()))];
            Assert.Equal(expected, outlines[i].Vertices.Select(vertex => (vertex.Location.X, vertex.Location.Y)));
        }
    }

    [Fact]
    [Trait("Feature", "IOP-002")]
    public void Dimensions_are_lines_and_text_that_read_as_the_canvas_does_and_never_a_DIMENSION()
    {
        Sketch sketch = Sample("coffee-table");
        CadDocument read = RoundTrip(sketch);

        DimensionMeasurement[] dimensions = [.. PlanDimensions.Measure(sketch)];
        Assert.NotEmpty(dimensions);
        Assert.Empty(read.Entities.OfType<ACadSharp.Entities.Dimension>());
        Assert.Equal(
            dimensions.Select(dimension => dimension.Label(Labels)),
            read.Entities.OfType<TextEntity>().Select(text => text.Value));
        Assert.All(read.Entities.OfType<TextEntity>(), text => Assert.Equal(PlanDxf.DimensionLayer, text.Layer.Name));

        // Each dimension: its line, two extension lines and two arrowheads of two strokes: seven lines.
        Assert.Equal(dimensions.Length * 7, read.Entities.OfType<Line>().Count(line => line.Layer.Name == PlanDxf.DimensionLayer));

        // The first dimension's line runs exactly where the canvas puts it.
        DimensionMeasurement first = dimensions[0];
        Line drawn = read.Entities.OfType<Line>().First();
        Assert.Equal((first.LineFrom.X.ToInches(), first.LineFrom.Y.ToInches()), (drawn.StartPoint.X, drawn.StartPoint.Y));
        Assert.Equal((first.LineTo.X.ToInches(), first.LineTo.Y.ToInches()), (drawn.EndPoint.X, drawn.EndPoint.Y));
    }

    [Fact]
    [Trait("Feature", "IOP-001")]
    public void A_rounded_corner_is_an_arc_a_quarter_turn_round()
    {
        CadDocument read = RoundTrip(Sample("rounded-corner-table"));

        // A quarter turn's bulge is tan(90° / 4) either way round.
        double[] bulges = [.. read.Entities.OfType<LwPolyline>().SelectMany(outline => outline.Vertices).Select(vertex => vertex.Bulge).Where(bulge => bulge != 0)];
        Assert.NotEmpty(bulges);
        Assert.All(bulges, bulge => Assert.Equal(Math.Tan(Math.PI / 8), Math.Abs(bulge), 9));
    }

    [Fact]
    [Trait("Feature", "IOP-001")]
    public void An_angled_part_is_its_plan_outline()
    {
        Sketch sketch = Sample("splayed-bench");
        CadDocument read = RoundTrip(sketch);

        Strut[] legs = [.. sketch.Entities.Values.OfType<Strut>().OrderBy(leg => leg.Id)];
        LwPolyline[] outlines = [.. read.Entities.OfType<LwPolyline>()];
        Assert.Equal(sketch.Entities.Values.Count(entity => entity is Box or Strut), outlines.Length);
        LwPolyline firstLeg = outlines[sketch.Entities.Values.OrderBy(entity => entity.Id).ToList().FindIndex(entity => entity is Strut)];
        Assert.Equal(StrutSolid.PlanOutline(legs[0]), firstLeg.Vertices.Select(vertex => (vertex.Location.X, vertex.Location.Y)));
    }

    [Fact]
    public void Arcs_through_three_points_turn_the_way_through_the_middle_one()
    {
        Point2 P(double x, double y) => Point2.Inches((long)x, (long)y);

        // A half circle over the top, from east to west: counter-clockwise, +180°.
        Assert.Equal(Math.PI, PlanDxf.Sweep(new ArcThrough(P(1, 0), P(0, 1), P(-1, 0))), 9);

        // The same ends under the bottom: clockwise, −180°.
        Assert.Equal(-Math.PI, PlanDxf.Sweep(new ArcThrough(P(1, 0), P(0, -1), P(-1, 0))), 9);

        // A rounded corner turning clockwise, and a straight run.
        Assert.Equal(-Math.PI / 2, PlanDxf.Sweep(new ArcByCenter(P(0, 1), P(1, 0), P(0, 0))), 9);
        Assert.Equal(0, PlanDxf.Sweep(new StraightSegment(P(0, 0), P(1, 0))));
    }

    [Theory]
    [InlineData(0, 0.25)]
    [InlineData(10, 0.25)]
    [InlineData(100, 2)]
    [InlineData(1000, 12)]
    public void Marks_are_a_fiftieth_of_the_drawing_kept_between_a_quarter_inch_and_a_foot(double side, double expected)
        => Assert.Equal(expected, PlanDxf.MarkSize(side == 0 ? [] : [(0, 0), (side, side / 2)]), 9);

    [Fact]
    public void A_sketch_with_no_dimensions_has_no_dimension_layer()
    {
        Sketch sketch = Sample("splayed-bench");
        Assert.Empty(PlanDimensions.Measure(sketch));

        CadDocument read = RoundTrip(sketch);

        Assert.False(read.Layers.Contains(PlanDxf.DimensionLayer));
    }
}
