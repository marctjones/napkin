using Napkin.Core.Geometry;

namespace Napkin.Core.Project.Tests;

/// <summary>
/// The two hand-crafted designs in <c>samples/</c>, loaded by the reader and compared with the
/// expectations that were worked out by hand and committed before the reader existed (issue #37).
/// </summary>
/// <remarks>
/// When one of these fails, the first question is which side is wrong. An expectation is changed
/// only after the arithmetic has been re-done by hand and the new derivation written into the
/// expectations file — never by copying what napkin printed.
/// </remarks>
public sealed class SampleFixtureTests
{
    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "PRJ-001")]
    public void Sample_loads_with_the_entities_the_expectations_state(string fixture)
    {
        (Sketch sketch, Expectations expected) = Load(fixture);

        Assert.Equal(expected.FormatVersion, SceneReader.FormatVersion);
        Assert.Equal(Length.UnitsPerInch, expected.UnitsPerInch);

        Assert.Equal(expected.Counts.Entities, sketch.Entities.Count);
        Assert.Equal(expected.Counts.Relationships, sketch.Relationships.Count);
        Assert.Equal(expected.Counts.Layers, sketch.Layers.Count);
        Assert.Equal(expected.Counts.Boxes, sketch.Entities.Values.OfType<Box>().Count());
        Assert.Equal(expected.Counts.Dimensions, sketch.Entities.Values.OfType<Dimension>().Count());
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [InlineData("rounded-corner-table")]
    [Trait("Feature", "PRJ-001")]
    public void Every_part_is_where_and_what_the_expectations_say(string fixture)
    {
        (Sketch sketch, Expectations expected) = Load(fixture);

        foreach (ExpectedBox part in expected.Boxes)
        {
            Box box = Assert.IsType<Box>(sketch.Find(new EntityId(Guid.Parse(part.Id))));

            // Integer units throughout: a part's size and position are compared exactly, never as
            // inches and never with a tolerance.
            // Format version 2 put the name in the file, so the two places it is written — the
            // scene and the expectations that were derived by hand before the scene existed — are
            // held to each other rather than allowed to drift apart.
            Assert.Equal(part.Name, box.Name);

            Assert.Equal(part.AnchorXUnits, box.Anchor.X.Units);
            Assert.Equal(part.AnchorYUnits, box.Anchor.Y.Units);
            Assert.Equal(part.WidthUnits, box.Width.Units);
            Assert.Equal(part.HeightUnits, box.Height.Units);
            Assert.Equal(part.RotationArcseconds, box.Rotation.Arcseconds);

            // In space (format version 4, docs/design/assembly-model.md §9 case 22): the height
            // above the floor, the size along the box's own Z and the face that is up, each worked
            // out by hand in the sample's design file.
            Assert.Equal(part.AnchorZUnits, box.Anchor.Z.Units);
            Assert.Equal(part.DepthUnits, box.Depth.Units);
            Assert.Equal(part.FaceUp, FaceName(box.FaceUp));
        }

        Assert.Equal(expected.Boxes.Count, sketch.Entities.Values.OfType<Box>().Count());
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "PRJ-001")]
    public void The_design_measures_what_the_expectations_say_overall(string fixture)
    {
        (Sketch sketch, Expectations expected) = Load(fixture);

        IReadOnlyList<Box> boxes = [.. sketch.Entities.Values.OfType<Box>()];
        Length west = boxes.Min(box => box.Anchor.X);
        Length south = boxes.Min(box => box.Anchor.Y);
        Length east = boxes.Max(box => box.Anchor.X + box.Width);
        Length north = boxes.Max(box => box.Anchor.Y + box.Height);

        Assert.Equal(expected.Overall.WidthUnits, (east - west).Units);
        Assert.Equal(expected.Overall.DepthUnits, (north - south).Units);
        Assert.Equal(expected.Overall.WidthText, (east - west).Format(LengthFormat.Default).Text);
        Assert.Equal(expected.Overall.DepthText, (north - south).Format(LengthFormat.Default).Text);
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "PRJ-001")]
    public void The_parts_list_has_the_quantities_the_expectations_say(string fixture)
    {
        (Sketch sketch, Expectations expected) = Load(fixture);

        Dictionary<(long Width, long Height), int> actual = [];
        foreach (Box box in sketch.Entities.Values.OfType<Box>())
        {
            (long, long) size = (box.Width.Units, box.Height.Units);
            actual[size] = actual.TryGetValue(size, out int count) ? count + 1 : 1;
        }

        Assert.Equal(expected.PartsList.Count, actual.Count);

        foreach (ExpectedPart part in expected.PartsList)
        {
            (long, long) size = (part.PlanWidthUnits, part.PlanHeightUnits);
            Assert.True(
                actual.TryGetValue(size, out int quantity),
                $"{part.Name}: no part in the file is {part.PlanWidthUnits} by {part.PlanHeightUnits} units.");
            Assert.Equal(part.Quantity, quantity);

            Assert.Equal(part.PlanWidthText, new Length(part.PlanWidthUnits).Format(LengthFormat.Default).Text);
            Assert.Equal(part.PlanHeightText, new Length(part.PlanHeightUnits).Format(LengthFormat.Default).Text);
        }
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "PRJ-001")]
    public void The_relationships_are_the_kinds_and_counts_the_expectations_say(string fixture)
    {
        (Sketch sketch, Expectations expected) = Load(fixture);

        Dictionary<string, int> actual = [];
        foreach (Relationship relationship in sketch.RelationshipsInOrder)
        {
            string kind = KindOf(relationship);
            actual[kind] = actual.TryGetValue(kind, out int count) ? count + 1 : 1;
        }

        Assert.Equal(expected.RelationshipKinds.OrderBy(pair => pair.Key), actual.OrderBy(pair => pair.Key));
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [Trait("Feature", "PRJ-001")]
    public void Every_dimension_label_reads_as_the_expectations_say(string fixture)
    {
        (Sketch sketch, Expectations expected) = Load(fixture);

        Assert.Equal(expected.DimensionLabels.Count, sketch.Entities.Values.OfType<Dimension>().Count());

        foreach (ExpectedLabel label in expected.DimensionLabels)
        {
            Dimension dimension = Assert.IsType<Dimension>(sketch.Find(new EntityId(Guid.Parse(label.Id))));

            Assert.Equal(label.Name, dimension.Name);

            // A dimension never stores a number: its value is computed from what it measures
            // (geometry design §3.3), which is what the expectations file states.
            Length measured = Measure(sketch, dimension);
            Assert.Equal(label.ValueUnits, measured.Units);

            FormattedLength text = measured.Format(LengthFormat.Default);
            Assert.Equal(label.Text, text.Text);
            Assert.True(text.IsExact, $"{label.Name} does not display exactly at 1/16 inch.");

            Assert.Equal(label.Driving, dimension.Drives is not null);
        }
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [InlineData("rounded-corner-table")]
    [InlineData("bookcase")]
    [InlineData("bench")]
    [InlineData("lying-beam")]
    [InlineData("chain-of-five")]
    [InlineData("fraction-stress")]
    [InlineData("scale-extremes")]
    [InlineData("framing-16-oc")]
    [InlineData("l-bracket")]
    [InlineData("overlap")]
    [InlineData("picture-frame")]
    [InlineData("stocked-bench")]
    [InlineData("diy-coffee-table-drawers")]
    [Trait("Feature", "PRJ-003")]
    public void A_sample_satisfies_its_own_relationships_and_validates(string fixture)
    {
        (Sketch sketch, _) = Load(fixture);

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        Assert.True(RelationshipChecker.Check(sketch).AllHold, RelationshipChecker.Check(sketch).ToString());
    }

    [Theory]
    [InlineData("coffee-table")]
    [InlineData("wall-with-window")]
    [InlineData("rounded-corner-table")]
    [InlineData("bookcase")]
    [InlineData("bench")]
    [InlineData("lying-beam")]
    [InlineData("chain-of-five")]
    [InlineData("fraction-stress")]
    [InlineData("scale-extremes")]
    [InlineData("framing-16-oc")]
    [InlineData("l-bracket")]
    [InlineData("overlap")]
    [InlineData("picture-frame")]
    [InlineData("stocked-bench")]
    [InlineData("diy-coffee-table-drawers")]
    [Trait("Feature", "PRJ-001")]
    public void Reading_a_sample_twice_gives_the_same_value(string fixture)
    {
        (Sketch first, _) = Load(fixture);
        (Sketch second, _) = Load(fixture);

        Assert.Equal(first, second);
    }

    /// <summary>
    /// docs/design/assembly-model.md §9.1 case 18 is worked on this sample's top: loaded from the
    /// file, its solid is two caps of four straight runs and four quarter-circle arcs, and eight
    /// sides — the four edges' quads, each <c>Of</c> its face, and the four corners' curved patches,
    /// <c>Of</c> none. Every X and Y is on the 1&#x2033; grid; Z is the underside at 16640 or the
    /// top at 17408. The geometry tests hold the same box's solid point for point.
    /// </summary>
    [Fact]
    public void The_rounded_corner_tables_top_extrudes_into_the_solid_case_18_works()
    {
        (Sketch sketch, _) = Load("rounded-corner-table");
        Box top = Assert.IsType<Box>(sketch.Find(new EntityId(Guid.Parse("50000000-0000-4000-8000-000000000001"))));

        Solid solid = top.Solid();

        Assert.Equal(10, solid.Faces.Length);
        foreach (SolidFace cap in solid.Faces[..2])
        {
            Assert.Equal(4, cap.Boundary.Count(segment => segment is StraightSegment3));
            Assert.Equal(4, cap.Boundary.Count(segment => segment is ArcByCenter3));
        }

        Assert.Equal(
            new BoxFace?[] { BoxFace.Bottom, BoxFace.Top, BoxFace.South, null, BoxFace.East, null, BoxFace.North, null, BoxFace.West, null },
            solid.Faces.Select(face => face.Of));

        foreach (SolidSegment segment in solid.Faces.SelectMany(face => face.Boundary))
        {
            Point3[] points = segment is ArcByCenter3 arc ? [arc.From, arc.To, arc.Center] : [segment.From, segment.To];
            foreach (Point3 point in points)
            {
                Assert.Equal(0, point.X.Units % 1024);
                Assert.Equal(0, point.Y.Units % 1024);
                Assert.Contains(point.Z.Units, new long[] { 16640, 17408 });
            }
        }
    }

    internal static string SampleDirectory => Path.Combine(AppContext.BaseDirectory, "samples");

    private static (Sketch Sketch, Expectations Expected) Load(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(Path.Combine(SampleDirectory, $"{fixture}.scene.json"));
        Loaded loaded = Assert.IsType<Loaded>(result);
        Expectations expected = Expectations.Read(Path.Combine(SampleDirectory, $"{fixture}.expected.json"));

        Assert.Equal(fixture, expected.Fixture);
        return (loaded.Sketch, expected);
    }

    private static Length Measure(Sketch sketch, Dimension dimension) => dimension.Measures switch
    {
        ParamMeasurand param => sketch.ValueOf(param.Param),
        AxisMeasurand axis => sketch.PlanPoint(axis.To).Component(axis.Axis)
                              - sketch.PlanPoint(axis.From).Component(axis.Axis),
        _ => throw new InvalidOperationException($"Unknown measurand {dimension.Measures}."),
    };

    /// <summary>
    /// A face as the expectations spell it — spelled out here rather than read from the reader's
    /// own table, for the reason <see cref="KindOf"/> is.
    /// </summary>
    private static string FaceName(BoxFace face) => face switch
    {
        BoxFace.South => "south",
        BoxFace.East => "east",
        BoxFace.North => "north",
        BoxFace.West => "west",
        BoxFace.Bottom => "bottom",
        BoxFace.Top => "top",
        _ => face.ToString(),
    };

    /// <summary>
    /// The relationship kind names the expectations use. Spelled out here rather than read from
    /// the reader's own table, so that the fixtures check the format's names rather than agreeing
    /// with whatever the reader happens to call them.
    /// </summary>
    private static string KindOf(Relationship relationship) => relationship switch
    {
        Anchored => "anchored",
        Coincident => "coincident",
        Horizontal => "horizontal",
        Vertical => "vertical",
        Flush => "flush",
        AxisDistance => "axisDistance",
        ParamValue => "paramValue",
        EqualParam => "equalParam",
        Centered => "centered",
        _ => relationship.GetType().Name,
    };
}
