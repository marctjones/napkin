using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// A part's cut does not depend on how it is turned or where it sits (#95).
/// </summary>
/// <remarks>
/// A leg turned to lie on its side is the same piece of wood, so it must be the same cut. The 3D
/// view gave a box 24 orientations (<c>FaceUp</c> and a spin, <c>docs/design/assembly-model.md</c>
/// §1.3) after the cut list was written. The cut list reads the box's <em>local</em> width, height
/// and depth, so this holds by construction — these tests hold the construction to it, and add the
/// physical check that does not lean on the same reading: the box's extents in the world are, in
/// some order, exactly the length, width and thickness the row reports.
/// </remarks>
public sealed class CutListOrientationTests
{
    private static MaterialsLibrary Library => MaterialsLibrary.Shipped;

    /// <summary>A 2x4 stud 29 1/4" long, by its nominal name: 1 1/2" by 3 1/2" is the library's to say.</summary>
    private static Sketch Stud => Design.WithParts((
        "Stud",
        Length.Inches(29, 1, 4).Units,
        1536,
        new Piece("2x4", null, 1, new Length(3584), new PlanAxes(PartDimension.Length, PartDimension.Thickness))));

    /// <summary>A 3/4" board, 48" by 5 1/2".</summary>
    private static Sketch Board => Design.WithParts((
        "Board",
        Length.Inches(48).Units,
        Length.Inches(5, 1, 2).Units,
        new Piece(null, null, 1, new Length(768), new PlanAxes(PartDimension.Length, PartDimension.Width))));

    /// <summary>A 1/4" sheet part, 24" by 48".</summary>
    private static Sketch Sheet => Design.WithParts((
        "Back panel",
        Length.Inches(48).Units,
        Length.Inches(24).Units,
        new Piece(null, null, 1, new Length(256), new PlanAxes(PartDimension.Length, PartDimension.Width))));

    public static IEnumerable<object[]> Parts => [["stud"], ["board"], ["sheet"]];

    private static Sketch Named(string name) => name switch
    {
        "stud" => Stud,
        "board" => Board,
        _ => Sheet,
    };

    /// <summary>All 24 orientations: six faces up, four quarter turns of spin each.</summary>
    private static IEnumerable<Orientation> Every24()
    {
        foreach (BoxFace face in Enum.GetValues<BoxFace>())
        {
            for (int quarter = 0; quarter < 4; quarter++)
            {
                yield return new Orientation(face, Angle.Zero.Rotate90(quarter));
            }
        }
    }

    private static readonly Point3[] Positions =
    [
        Point3.Origin,
        new(Length.Inches(37, 3, 8), Length.Inches(-12), Length.Inches(5, 1, 16)),
        new(Length.Inches(-100), Length.Inches(200, 1, 2), Length.Inches(-3)),
    ];

    private static Sketch Placed(Sketch sketch, Orientation orientation, Point3 anchor)
    {
        Box box = sketch.Entities.Values.OfType<Box>().Single();
        return sketch.WithEntity(box with { FaceUp = orientation.FaceUp, Rotation = orientation.Rotation, Anchor = anchor });
    }

    [Fact]
    public void The_orientations_under_test_are_twenty_four_distinct_ones()
    {
        Assert.Equal(24, Every24().Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Parts))]
    public void The_cut_list_row_is_identical_in_every_orientation_and_position(string part)
    {
        Sketch original = Named(part);
        ImmutableArray<CutListRow> expected = CutList.Of(original, Library);
        CutListRow expectedRow = Assert.Single(expected);

        foreach (Orientation orientation in Every24())
        {
            foreach (Point3 position in Positions)
            {
                CutListRow row = Assert.Single(CutList.Of(Placed(original, orientation, position), Library));

                Assert.True(
                    expectedRow.Equals(row),
                    $"{part} in {orientation} at {position} cut as {row.Length}x{row.Width}x{row.Thickness}, not {expectedRow.Length}x{expectedRow.Width}x{expectedRow.Thickness}.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Parts))]
    public void The_extents_in_the_world_are_the_rows_three_sizes_in_some_order(string part)
    {
        Sketch original = Named(part);
        CutListRow row = Assert.Single(CutList.Of(original, Library));
        long[] sizes = [.. new[] { row.Length.Units, row.Width.Units, row.Thickness.Units }.Order()];

        foreach (Orientation orientation in Every24())
        {
            Box box = Placed(original, orientation, Positions[1]).Entities.Values.OfType<Box>().Single();
            List<Point3> corners =
            [
                .. Enum.GetValues<BoxCorner>().SelectMany(corner => new[]
                {
                    box.Vertex(corner, BoxLevel.Bottom),
                    box.Vertex(corner, BoxLevel.Top),
                }),
            ];

            long[] extents =
            [
                .. new[]
                {
                    corners.Max(c => c.X.Units) - corners.Min(c => c.X.Units),
                    corners.Max(c => c.Y.Units) - corners.Min(c => c.Y.Units),
                    corners.Max(c => c.Z.Units) - corners.Min(c => c.Z.Units),
                }.Order(),
            ];

            Assert.True(sizes.SequenceEqual(extents), $"{part} in {orientation}: world extents {string.Join("x", extents)} vs cut {string.Join("x", sizes)}.");
        }
    }

    [Fact]
    public void The_stud_is_cut_at_the_libraries_true_size_not_the_nominal_one()
    {
        CutListRow row = Assert.Single(CutList.Of(Stud, Library));

        Assert.Equal(Length.Inches(29, 1, 4), row.Length);
        Assert.Equal(Length.Inches(1, 1, 2), row.Thickness);
        Assert.Equal(Length.Inches(3, 1, 2), row.Width);
        Assert.Equal("2x4", row.Material);
    }

    [Theory]
    [InlineData(Axis.X)]
    [InlineData(Axis.Y)]
    [InlineData(Axis.Z)]
    public void Turning_a_part_about_any_axis_and_back_leaves_the_cut_list_as_it_was(Axis axis)
    {
        Sketch original = Stud;
        ImmutableArray<CutListRow> expected = CutList.Of(original, Library);

        Orientation orientation = Orientation.AsDrawn;
        for (int step = 1; step <= 4; step++)
        {
            orientation = orientation.TurnedAbout(axis, 1);
            ImmutableArray<CutListRow> now = CutList.Of(Placed(original, orientation, Positions[1]), Library);
            Assert.True(expected.SequenceEqual(now), $"after {step} quarter turn(s) about {axis}: {orientation}.");
        }

        Assert.Equal(Orientation.AsDrawn, orientation);
    }
}
