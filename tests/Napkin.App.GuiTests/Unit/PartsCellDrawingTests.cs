using System.Collections.Immutable;

using Avalonia;
using Avalonia.Headless.XUnit;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Furniture;

using Xunit;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// One Parts view cell, built (docs/design/parts-view.md §2, §7.3): what it draws, where and at what
/// size, measured on the geometry the view will paint. Within a pixel, as §7.3 says.
/// </summary>
public class PartsCellDrawingTests
{
    static ImmutableArray<PartsCell> Sheet(string sample) => PartsSheet.Of(CutList.Of(
        Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(RepositoryLayout.RepositoryRoot, "samples", sample + ".scene.json"))).Sketch,
        MaterialsLibrary.Shipped));

    static double ScaleOf(ImmutableArray<PartsCell> cells) => PartsScale.Sheet(cells.Select(PartsPicture.Of))!.Value;

    static PartsCellDrawn Built(ImmutableArray<PartsCell> cells, string label) =>
        PartsCellDrawing.Build(cells.Single(cell => cell.Row.Label == label), ScaleOf(cells), new Point(0, 0));

    [AvaloniaFact]
    public void The_forty_foot_plate_spans_the_drawing_and_the_shim_is_drawn_at_the_floor_and_flagged()
    {
        ImmutableArray<PartsCell> cells = Sheet("scale-extremes");
        PartsCellDrawn plate = Built(cells, "Plate"), shim = Built(cells, "Shim");

        Assert.Equal(PartsScale.DrawingWidth, plate.Outline.Bounds.Width, tolerance: 1);
        Assert.False(plate.NotToScale);
        Assert.Equal(PartsScale.FloorPixels, shim.Outline.Bounds.Width, tolerance: 1);
        Assert.True(shim.NotToScale);
        Assert.EndsWith("not to scale", Assert.Single(shim.Texts, text => text.Role == PartsTextRole.Details).Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_coffee_tables_top_fills_the_drawings_height_and_a_leg_is_the_tops_length_times_sixteen_and_a_quarter_over_forty_eight()
    {
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        PartsCellDrawn top = Built(cells, "Top"), leg = Built(cells, "Leg");

        Assert.Equal(PartsScale.DrawingHeight, top.Outline.Bounds.Height, tolerance: 1);
        Assert.Equal(top.Outline.Bounds.Width * (16.25 / 48), leg.Outline.Bounds.Width, tolerance: 1);
        Assert.False(leg.NotToScale);

        // Drawn where the builder says, centred in the cell's drawing area.
        Assert.Equal(top.Drawn.Width, top.Outline.Bounds.Width, 6);
        Assert.Equal(PartsScale.Inset + (PartsScale.DrawingWidth / 2), top.Drawn.Center.X, 6);
        Assert.Equal(PartsCellDrawing.LabelRow + (PartsScale.DrawingHeight / 2), top.Drawn.Center.Y, 6);
    }

    [AvaloniaFact]
    public void The_dimensions_run_along_the_piece_and_read_as_the_cut_list_writes_them()
    {
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        PartsCellDrawn leg = Built(cells, "Leg");

        Assert.Equal(CutListCsv.Text(Length.Inches(16, 1, 4)), leg.Length.Label);
        Assert.Equal(CutListCsv.Text(Length.Inches(2, 1, 2)), leg.Width.Label);
        Assert.Equal(leg.Drawn.Width, leg.Length.To.X - leg.Length.From.X, 6);
        Assert.Equal(leg.Drawn.Height, leg.Width.To.Y - leg.Width.From.Y, 6);
        Assert.True(leg.Length.From.Y > leg.Drawn.Bottom, "the length is not below the piece.");
        Assert.True(leg.Width.From.X > leg.Drawn.Right, "the width is not beside the piece.");
    }

    [AvaloniaFact]
    public void A_cell_says_its_name_its_count_and_how_it_is_shown()
    {
        ImmutableArray<PartsCell> cells = Sheet("coffee-table");
        PartsCellDrawn leg = Built(cells, "Leg"), apron = Built(cells, "Apron, long");

        Assert.Equal("Leg", Assert.Single(leg.Texts, text => text.Role == PartsTextRole.Label).Text);
        Assert.Equal("×4", Assert.Single(leg.Texts, text => text.Role == PartsTextRole.Badge).Text);
        Assert.EndsWith(" thick", Assert.Single(leg.Texts, text => text.Role == PartsTextRole.Details).Text, StringComparison.Ordinal);
        Assert.EndsWith(" wide — shown on edge", Assert.Single(apron.Texts, text => text.Role == PartsTextRole.Details).Text, StringComparison.Ordinal);
        Assert.Empty(leg.Texts.Where(text => text.Role == PartsTextRole.Cut));
    }

    [AvaloniaFact]
    public void A_part_drawn_long_way_up_is_turned_so_its_longer_side_lies_across_the_cell()
    {
        // The rounded-corner table's shaped top, whichever way its plan runs, is drawn longer side across.
        ImmutableArray<PartsCell> cells = Sheet("rounded-corner-table");
        foreach (PartsCell cell in cells)
        {
            PartsCellDrawn drawn = PartsCellDrawing.Build(cell, ScaleOf(cells), new Point(0, 0));
            Assert.True(drawn.Outline.Bounds.Width >= drawn.Outline.Bounds.Height - 0.5, $"{cell.Row.Label} is drawn taller than it is wide.");
            Assert.Equal(drawn.Drawn.Width, drawn.Outline.Bounds.Width, tolerance: 1);
        }

        Sketch rail = Sketch.Empty.WithEntity(Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, Length.Inches(3), Length.Inches(30), Length.Inches(0, 3, 4), Angle.Zero) with
        {
            Name = "Rail",
            Part = new Part(null, null, 1, new PlanAxes(PartDimension.Width, PartDimension.Length)),
        });
        ImmutableArray<PartsCell> turned = PartsSheet.Of(CutList.Of(rail, MaterialsLibrary.Shipped));
        PartsCellDrawn built = PartsCellDrawing.Build(turned[0], ScaleOf(turned), new Point(0, 0));
        Assert.True(PartsPicture.Of(turned[0]).QuarterTurn);
        Assert.Equal(PartsScale.DrawingWidth, built.Outline.Bounds.Width, tolerance: 1);
        Assert.Equal(PartsScale.DrawingWidth * 3 / 30, built.Outline.Bounds.Height, tolerance: 1);
    }

    [AvaloniaFact]
    public void Cut_lines_follow_the_details_one_to_a_line()
    {
        ImmutableArray<PartsCell> cells = Sheet("rounded-corner-table");
        PartsCell shaped = cells.First(cell => !cell.Row.CutText.IsEmpty);
        PartsCellDrawn drawn = PartsCellDrawing.Build(shaped, ScaleOf(cells), new Point(10, 20));

        ImmutableArray<PartsText> cuts = [.. drawn.Texts.Where(text => text.Role == PartsTextRole.Cut)];
        Assert.Equal(PartsCellText.Cuts(shaped), cuts.Select(text => text.Text));
        PartsText details = Assert.Single(drawn.Texts, text => text.Role == PartsTextRole.Details);
        Assert.Equal(details.At.Y + PartsCellDrawing.LineHeight, cuts[0].At.Y, 6);
        Assert.Equal(10 + PartsScale.Inset, cuts[0].At.X, 6);
    }
}
