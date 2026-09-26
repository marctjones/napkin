using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The coffee table's cut list, compared row for row against the expectations a person worked out
/// by hand from <c>samples/coffee-table.design.md</c> (issue #8's worked example, <c>CUT-004</c>).
/// </summary>
/// <remarks>
/// When one of these fails, the first question is which side is wrong. An expectation is changed
/// only after the arithmetic has been re-done by hand and the new derivation written into the
/// expectations file — never by copying what napkin printed (<c>samples/README.md</c>).
/// </remarks>
public sealed class SampleCutListTests
{
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
    [InlineData("splayed-bench")]
    [InlineData("splayed-footstool")]
    [InlineData("angled-shelf")]
    [InlineData("raked-chair-frame")]
    [Trait("Feature", "CUT-004")]
    public void A_samples_cut_list_is_the_one_the_expectations_state(string fixture)
    {
        (ImmutableArray<CutListRow> rows, ExpectedFixture expected) = Load(fixture);

        Assert.Equal(expected.CutList.Count, rows.Length);

        for (int i = 0; i < rows.Length; i++)
        {
            CutListRow row = rows[i];
            ExpectedCutRow want = expected.CutList[i];

            Assert.Equal(want.Label, row.Label);
            Assert.Equal(want.Quantity, row.Quantity);

            // Integer units, compared exactly — never inches and never with a tolerance.
            Assert.Equal(want.LengthUnits, row.Length.Units);
            Assert.Equal(want.WidthUnits, row.Width.Units);
            Assert.Equal(want.ThicknessUnits, row.Thickness.Units);

            Assert.Equal(want.LengthText, CutListCsv.Text(row.Length));
            Assert.Equal(want.WidthText, CutListCsv.Text(row.Width));
            Assert.Equal(want.ThicknessText, CutListCsv.Text(row.Thickness));

            Assert.Equal(want.Material, row.Material);
            Assert.Equal(want.Unresolved, row.Unresolved);
            Assert.Equal(want.Members, row.Members.Select(id => id.Value.ToString("D")));

            // A fixture of plain rectangles says nothing about cuts, which is the same statement
            // as an empty list: no sentence is a sentence about nothing to do.
            Assert.Equal(want.Cuts ?? [], row.CutText);

            // Likewise joinery: a fixture with no joints says nothing, and says it by an empty list.
            Assert.Equal(want.Joinery ?? [], row.JointText);

            Assert.False(string.IsNullOrWhiteSpace(want.Derivation), $"{want.Label} has no derivation.");
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
    [InlineData("splayed-bench")]
    [InlineData("splayed-footstool")]
    [InlineData("angled-shelf")]
    [InlineData("raked-chair-frame")]
    [Trait("Feature", "CUT-004")]
    public void A_samples_csv_is_the_one_the_expectations_state(string fixture)
    {
        (ImmutableArray<CutListRow> rows, ExpectedFixture expected) = Load(fixture);

        Assert.Equal(expected.Csv, CutListCsv.ToCsv(rows));
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_coffee_tables_four_rows_come_out_largest_first()
    {
        (ImmutableArray<CutListRow> rows, _) = Load("coffee-table");

        Assert.Equal(["Top", "Apron, long", "Leg", "Apron, short"], rows.Select(row => row.Label));
        Assert.Equal([1, 2, 4, 2], rows.Select(row => row.Quantity));

        // Nine boxes in the file, nine pieces on the bench.
        Assert.Equal(9, rows.Sum(row => row.Quantity));
        Assert.Equal(9, rows.Sum(row => row.Members.Length));
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_wall_has_nothing_to_cut()
    {
        // Both of its boxes are "part": null, which is a statement and not a gap: a wall and an
        // opening are things you build, not pieces you cut.
        (ImmutableArray<CutListRow> rows, _) = Load("wall-with-window");

        Assert.Empty(rows);
    }

    [Fact]
    [Trait("Feature", "CUT-004")]
    public void The_rounded_corner_tables_top_is_the_shape_and_the_sentence_its_expectations_state()
    {
        // Test 18 (shaped-parts-model.md §9.1). The one part this fixture exists for, checked both
        // ways round: the shape the cuts leave, and the words the cut list says to make it with.
        // Both sides of the comparison were worked out by hand from the design — the outline from
        // §1.5's walk — and neither was taken from napkin's own output (samples/README.md).
        Sketch sketch = Read("rounded-corner-table");
        ExpectedFixture expected = ExpectedFixture.Read("rounded-corner-table");
        ExpectedOutline want = Assert.IsType<ExpectedOutline>(expected.TopOutline);

        Box top = sketch.Entities.Values.OfType<Box>()
            .Single(box => string.Equals(box.Name, "Top", StringComparison.Ordinal));

        ImmutableArray<OutlineSegment> segments = top.Outline().Segments;

        Assert.Equal(want.Segments.Count, segments.Length);

        for (int i = 0; i < segments.Length; i++)
        {
            ExpectedSegment piece = want.Segments[i];

            Assert.Equal(piece.FromXUnits, segments[i].From.X.Units);
            Assert.Equal(piece.FromYUnits, segments[i].From.Y.Units);
            Assert.Equal(piece.ToXUnits, segments[i].To.X.Units);
            Assert.Equal(piece.ToYUnits, segments[i].To.Y.Units);

            if (string.Equals(piece.Kind, "arc", StringComparison.Ordinal))
            {
                ArcByCenter arc = Assert.IsType<ArcByCenter>(segments[i]);

                Assert.Equal(piece.CenterXUnits, arc.Center.X.Units);
                Assert.Equal(piece.CenterYUnits, arc.Center.Y.Units);
            }
            else
            {
                Assert.IsType<StraightSegment>(segments[i]);
            }

            Assert.False(
                string.IsNullOrWhiteSpace(piece.Derivation),
                $"Segment {i} of the top's outline has no derivation.");
        }

        // The boundary closes: the last segment ends where the first one starts.
        Assert.Equal(segments[0].From, segments[^1].To);

        // And the row for it: the blank's own three dimensions, plus the one sentence.
        CutListRow row = CutList.Of(sketch, MaterialsLibrary.Shipped)[0];

        Assert.Equal("Top", row.Label);
        Assert.Equal(expected.CutList[0].Cuts, row.CutText);
        Assert.Equal("Round all four corners to a 1\"" + " radius.", Assert.Single(row.CutText));
    }

    private static (ImmutableArray<CutListRow> Rows, ExpectedFixture Expected) Load(string fixture)
    {
        ExpectedFixture expected = ExpectedFixture.Read(fixture);

        return (CutList.Of(Read(fixture), MaterialsLibrary.Shipped), expected);
    }

    private static Sketch Read(string fixture)
    {
        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture));

        // A refusal says what is wrong with the file, which is what a failing fixture needs to
        // print rather than "expected Loaded, got Refused".
        if (result is Refused refused)
        {
            Assert.Fail($"{fixture}.scene.json was refused: {refused.Summary}");
        }

        Loaded loaded = Assert.IsType<Loaded>(result);

        Assert.Equal(ExpectedFixture.Read(fixture).FormatVersion, SceneReader.FormatVersion);

        return loaded.Sketch;
    }
}
