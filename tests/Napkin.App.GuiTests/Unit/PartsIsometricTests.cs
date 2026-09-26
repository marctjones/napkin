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

/// <summary>The Parts view's 3D option (docs/design/parts-view.md §3, §7.3): one scale, inside the cell.</summary>
public class PartsIsometricTests
{
    static ImmutableArray<PartsCell> Sheet(Sketch sketch) => PartsSheet.Of(CutList.Of(sketch, MaterialsLibrary.Shipped));

    static ImmutableArray<PartsCell> Sheet(string sample) => Sheet(
        Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(RepositoryLayout.RepositoryRoot, "samples", sample + ".scene.json"))).Sketch);

    static Rect Drawing => new(PartsScale.Inset, PartsCellDrawing.LabelRow, PartsScale.DrawingWidth, PartsScale.DrawingHeight);

    [AvaloniaTheory]
    [InlineData("coffee-table")]
    [InlineData("scale-extremes")]
    [InlineData("rounded-corner-table")]
    [InlineData("stocked-bench")]
    [InlineData("diy-coffee-table-drawers")]
    public void Every_cells_solid_is_drawn_inside_its_drawing_at_the_sheets_one_scale(string sample)
    {
        ImmutableArray<PartsCell> cells = Sheet(sample);
        double sheet = PartsIsometric.Sheet(cells)!.Value;
        Rect room = Drawing.Inflate(0.5);

        foreach (PartsCell cell in cells)
        {
            PartsIsometricDrawn drawn = PartsIsometric.Build(cell, sheet, new Point(0, 0));
            Assert.NotEmpty(drawn.Faces);
            Assert.All(drawn.Faces.SelectMany(face => face.Points), point => Assert.True(room.Contains(point), $"{cell.Row.Label} reaches {point}, outside {Drawing}."));
            if (!drawn.NotToScale)
            {
                Assert.Equal(sheet, drawn.PixelsPerInch);
            }
        }

        // The largest piece fills its drawing one way or the other: the scale is the largest that fits.
        Assert.Contains(cells, cell =>
        {
            Rect drawn = PartsIsometric.Build(cell, sheet, new Point(0, 0)).Drawn;
            return Math.Abs(drawn.Width - PartsScale.DrawingWidth) < 0.5 || Math.Abs(drawn.Height - PartsScale.DrawingHeight) < 0.5;
        });
    }

    [AvaloniaFact]
    public void Two_pieces_the_same_size_are_drawn_the_same_size()
    {
        // A slat and a rail with one size, the rail in a stock the library does not know, so the cut
        // list keeps them as two rows (it would merge two equal parts of one stock): two cells, one drawing.
        Piece board = new(20, 3);
        ImmutableArray<PartsCell> cells = Sheet(Sketch.Empty
            .WithEntity(board.Box("Slat", 0))
            .WithEntity(board.Box("Rail", 30) with { Part = new Part("Ash board", null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)) })
            .WithEntity(new Piece(40, 10).Box("Top", 60)));
        double sheet = PartsIsometric.Sheet(cells)!.Value;
        Rect slat = PartsIsometric.Build(cells.Single(cell => cell.Row.Label == "Slat"), sheet, new Point(0, 0)).Drawn;
        Rect rail = PartsIsometric.Build(cells.Single(cell => cell.Row.Label == "Rail"), sheet, new Point(0, 0)).Drawn;

        Assert.Equal(slat.Width, rail.Width, 9);
        Assert.Equal(slat.Height, rail.Height, 9);
        Assert.Equal(3, cells.Length);
    }

    [AvaloniaFact]
    public void The_shim_beside_the_plate_is_drawn_at_the_floor_and_flagged_in_3D_too()
    {
        ImmutableArray<PartsCell> cells = Sheet("scale-extremes");
        double sheet = PartsIsometric.Sheet(cells)!.Value;
        PartsIsometricDrawn shim = PartsIsometric.Build(cells.Single(cell => cell.Row.Label == "Shim"), sheet, new Point(0, 0));

        Assert.True(shim.NotToScale);
        Assert.Equal(PartsScale.FloorPixels, Math.Max(shim.Drawn.Width, shim.Drawn.Height), 6);
        Assert.EndsWith("not to scale", shim.Texts.Single(text => text.Role == PartsTextRole.Details).Text, StringComparison.Ordinal);
        Assert.Null(PartsIsometric.Sheet([]));
    }

    /// <summary>A flat 3/4" board in plan, width by height inches.</summary>
    sealed record Piece(long Width, long Height)
    {
        public Box Box(string name, long x) => Napkin.Core.Geometry.Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Inches(x, 0), Length.Inches(Width), Length.Inches(Height), Length.Inches(0, 3, 4), Angle.Zero) with
        {
            Name = name,
            Part = new Part(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
    }
}
