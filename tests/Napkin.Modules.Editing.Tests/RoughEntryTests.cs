using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Rough entry (<c>docs/design/sketch-mode.md</c> &#xA7;2, &#xA7;7.1): the rough step at a zoom,
/// worked by hand from the ladder and <see cref="SnapGrid.MinimumSpacingPixels"/> = 14; the plank a
/// rough rectangle is; what a rough drop states (nothing); and the status bar's words.
/// </summary>
public class RoughEntryTests
{
    // §2.1's table. The precise step is the finest rung at least 14 px apart; the rough step is the
    // rung above it, never below 1".
    // 60 ppi: 1/4" is 15 px -> precise 1/4"; the rung above is 1/2", floored to 1".
    // 30 ppi: 1/4" is 7.5 px, 1/2" is 15 px -> precise 1/2"; above is 1".
    // 14 ppi: 1/2" is 7 px, 1" is 14 px -> precise 1"; above is 3".
    // 5 ppi: 1" is 5 px, 3" is 15 px -> precise 3"; above is 6".
    // 2.5 ppi: 3" is 7.5 px, 6" is 15 px -> precise 6"; above is 12".
    // 1.2 ppi: 6" is 7.2 px, 12" is 14.4 px -> precise 12"; above is 24".
    [Theory]
    [Trait("Feature", "CVS-013")]
    [InlineData(60, 0.25, 1)]
    [InlineData(30, 0.5, 1)]
    [InlineData(14, 1, 3)]
    [InlineData(5, 3, 6)]
    [InlineData(2.5, 6, 12)]
    [InlineData(1.2, 12, 24)]
    public void The_rough_step_is_one_rung_coarser_never_below_an_inch(double pixelsPerInch, double precise, double rough)
    {
        Assert.Equal(precise, SnapGrid.StepInches(pixelsPerInch));
        Assert.Equal(rough, SnapGrid.RoughStepInches(pixelsPerInch));
    }

    [Fact]
    public void At_the_top_of_the_ladder_the_rough_step_is_the_top_rung()
    {
        // So far out that 12000" is the precise step: there is no rung above it.
        Assert.Equal(12000, SnapGrid.RoughStepInches(0.0001));
    }

    [Fact]
    public void For_every_rung_the_rough_step_is_on_the_ladder_at_least_an_inch_and_a_multiple_up_to_48_inches()
    {
        foreach (double precise in SnapGrid.Ladder)
        {
            // A zoom at which this rung is exactly the precise step: 14 px per step.
            double pixelsPerInch = SnapGrid.MinimumSpacingPixels / precise;
            Assert.Equal(precise, SnapGrid.StepInches(pixelsPerInch));

            double rough = SnapGrid.RoughStepInches(pixelsPerInch);
            Assert.Contains(rough, SnapGrid.Ladder);
            Assert.True(rough >= 1);
            Assert.True(rough >= precise);
            if (precise <= 48)
            {
                double ratio = rough / precise;
                Assert.Equal(Math.Round(ratio), ratio);
            }
        }
    }

    [Theory]
    [InlineData(true, EntryMode.Precise, 0.25)]
    [InlineData(true, EntryMode.Rough, 1)]
    [InlineData(false, EntryMode.Precise, 1.0 / 1024)]
    [InlineData(false, EntryMode.Rough, 1.0 / 1024)]
    public void The_snap_step_reads_the_mode_and_snapping_off_wins(bool snapToGrid, EntryMode mode, double expected)
        => Assert.Equal(expected, SnapGrid.SnapStepInches(snapToGrid, mode, 60));

    [Fact]
    [Trait("Feature", "CVS-013")]
    public void At_60_ppi_a_drag_from_0_3_to_10_6_is_11_inches_rough_and_10_and_a_quarter_precise()
    {
        // Rough, 1": 0.3 -> 0 and 10.6 -> 11, so 11" wide. Precise, 1/4": 0.3 -> 1/4 and 10.6 -> 10 1/2, so 10 1/4".
        Length from = Length.FromInches(0.3, Rounding.HalfToEven);
        Length to = Length.FromInches(10.6, Rounding.HalfToEven);
        double rough = SnapGrid.SnapStepInches(true, EntryMode.Rough, 60);
        double precise = SnapGrid.SnapStepInches(true, EntryMode.Precise, 60);

        Assert.Equal(Length.Inches(11), SnapGrid.Snap(to, rough) - SnapGrid.Snap(from, rough));
        Assert.Equal(Length.Inches(10, 1, 4), SnapGrid.Snap(to, precise) - SnapGrid.Snap(from, precise));
    }

    [Fact]
    [Trait("Feature", "CVS-014")]
    public void A_rough_rectangle_is_a_plank_marked_rough_and_a_precise_one_a_plain_box()
    {
        Box rough = Drawn(EntryMode.Rough, Point2.Inches(0, 0), Point2.Inches(48, 12));
        Box precise = Drawn(EntryMode.Precise, Point2.Inches(0, 0), Point2.Inches(48, 12));

        Assert.Equal(new Part(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)) { Rough = true }, rough.Part);
        Assert.Equal(Box.DefaultDepth, rough.Depth);
        Assert.Equal(Length.Inches(0, 3, 4), rough.Depth);
        Assert.Null(precise.Part);

        // Its finished sizes, in cut-list order: 48 long, 12 wide, 3/4 thick.
        Assert.Equal(new FinishedSize(Length.Inches(48), Length.Inches(12), Length.Inches(0, 3, 4)), rough.Part!.SizeOn(rough));
    }

    [Fact]
    [Trait("Feature", "CVS-014")]
    public void A_tall_plank_runs_its_length_north_south_and_a_square_runs_it_east_west()
    {
        Box tall = Drawn(EntryMode.Rough, Point2.Inches(0, 0), Point2.Inches(4, 16));
        Box square = Drawn(EntryMode.Rough, Point2.Inches(0, 0), Point2.Inches(6, 6));

        Assert.Equal(new PlanAxes(PartDimension.Width, PartDimension.Length), tall.Part!.PlanAxes);
        Assert.Equal(new FinishedSize(Length.Inches(16), Length.Inches(4), Length.Inches(0, 3, 4)), tall.Part.SizeOn(tall));
        Assert.Equal(new PlanAxes(PartDimension.Length, PartDimension.Width), square.Part!.PlanAxes);
    }

    [Fact]
    public void A_rough_drop_states_nothing_it_caught_and_a_precise_one_everything()
    {
        Relationship caught = new Flush(
            RelationshipId.New(),
            new FeatureRef(EntityId.New(), BoxFeature.Face(BoxFace.East)),
            new FeatureRef(EntityId.New(), BoxFeature.Face(BoxFace.West)));

        Assert.Empty(RoughEntry.Stated(EntryMode.Rough, [caught]));
        Assert.Equal([caught], RoughEntry.Stated(EntryMode.Precise, [caught]));
    }

    [Fact]
    public void The_status_bar_names_the_mode_in_capitals()
    {
        Assert.Equal("ROUGH", RoughEntry.Word(EntryMode.Rough));
        Assert.Equal("PRECISE", RoughEntry.Word(EntryMode.Precise));
    }

    [Fact]
    public void The_readouts_are_the_label_format_in_cut_list_order()
    {
        LengthFormat format = new FeetInchesFormat(16);
        Box leg = Drawn(EntryMode.Rough, Point2.Inches(2, 0), Point2.Inches(6, 16));
        Box plain = Drawn(EntryMode.Precise, Point2.Inches(0, 0), Point2.Inches(4, 16));

        // The leg is 4 wide east-west and 16 tall north-south: its length is the 16, then the 4, then 3/4.
        Assert.Equal($"Leg 1  {Length.Inches(16).Format(format).Text} × {Length.Inches(4).Format(format).Text} × {Length.Inches(0, 3, 4).Format(format).Text}", RoughEntry.SelectedReadout(leg, "Leg 1", format));
        Assert.Equal("Leg 1  1'-4\" × 4\" × 3/4\"", RoughEntry.SelectedReadout(leg, "Leg 1", format));
        // Not a part: width, height, depth as stored.
        Assert.Equal("Box  4\" × 1'-4\" × 3/4\"", RoughEntry.SelectedReadout(plain, "Box", format));
        Assert.Equal("4'-0\" × 1'-0\"", RoughEntry.DrawingReadout(Length.Inches(48), Length.Inches(12), format));
    }

    [Fact]
    public void The_editor_starts_precise_and_switching_is_not_an_undo_step()
    {
        DesignEditor editor = new();
        int raised = 0;
        editor.EntryModeChanged += (_, _) => raised++;

        Assert.Equal(EntryMode.Precise, editor.EntryMode);
        editor.EntryMode = EntryMode.Rough;
        editor.EntryMode = EntryMode.Rough;

        Assert.Equal(EntryMode.Rough, editor.EntryMode);
        Assert.Equal(1, raised);
        Assert.False(editor.History.CanUndo);
        Assert.False(editor.HasUnsavedChanges);

        // Opening another design keeps the person's mode: the design has none.
        editor.Open(Design.Unlabelled("Other", Sketch.Empty));
        Assert.Equal(EntryMode.Rough, editor.EntryMode);
    }

    [Fact]
    public void A_2x4_placed_rough_keeps_its_stock_and_is_marked_rough()
    {
        Assert.True(MaterialsLibrary.Shipped.TryFind(StockCategory.DimensionalLumber, "2x4", out StockItem item));
        StockTool tool = new();
        Assert.True(tool.Arm(item));
        tool.Begin(Point2.Inches(0, 0));
        tool.MoveTo(Point2.Inches(36, 0));

        EntityId id = EntityId.New();
        Assert.True(tool.TryComplete(Sketch.Empty, LayerId.Default, id, "Part 1", out Request? request, EntryMode.Rough));
        DesignEditor editor = new();
        Assert.IsAssignableFrom<Succeeded>(editor.Apply(request, "Placed a 2x4"));

        Part part = editor.Sketch.Find<Box>(id)!.Part!;
        Assert.Equal("2x4", part.Stock);
        Assert.True(part.Rough);
    }

    [Fact]
    public void A_plain_board_placed_rough_is_a_plank_and_nothing_holds_it()
    {
        Box top = new(EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 16), Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), BoxFace.Top, Angle.Zero);
        Sketch table = Sketch.Empty.WithEntity(top);
        PlacementTool tool = new();
        tool.ArmPlainBoard();
        Point3 at = Point3.Inches(20, 10, 16) with { Z = Length.Inches(16, 3, 4) };
        PlacementPreview preview = tool.Shape(table, PlacementFace.Of(top, BoxFace.Top), at, at, LayerId.Default, EntityId.New(), "Part 1", 1, Length.Inches(0, 1, 4))!;

        (Request precise, var preciseHolds) = PlacementTool.Requests(table, preview);
        (Request rough, var roughHolds) = PlacementTool.Requests(table, preview, EntryMode.Rough);

        Assert.Null(Assert.IsType<Box>(Assert.IsType<AddEntity>(precise).Entity).Part);
        Assert.NotEmpty(preciseHolds);
        Box plank = Assert.IsType<Box>(Assert.IsType<AddEntity>(rough).Entity);
        Assert.Equal(RoughEntry.Plank(plank.Width, plank.Height), plank.Part);
        Assert.Empty(roughHolds);
    }

    [Fact]
    public void A_2x4_placed_rough_in_3d_is_marked_rough_and_nothing_holds_it()
    {
        Assert.True(MaterialsLibrary.Shipped.TryFind(StockCategory.DimensionalLumber, "2x4", out StockItem item));
        Box top = new(EntityId.New(), LayerId.Default, Point3.Inches(0, 0, 16), Length.Inches(48), Length.Inches(24), Length.Inches(0, 3, 4), BoxFace.Top, Angle.Zero);
        Sketch table = Sketch.Empty.WithEntity(top);
        PlacementTool tool = new();
        Assert.True(tool.Arm(item));
        Point3 at = Point3.Inches(20, 10, 16) with { Z = Length.Inches(16, 3, 4) };
        EntityId id = EntityId.New();
        PlacementPreview preview = tool.Shape(table, PlacementFace.Of(top, BoxFace.Top), at, at, LayerId.Default, id, "Part 1", 1, Length.Inches(0, 1, 4))!;

        (Request add, var holds) = PlacementTool.Requests(table, preview, EntryMode.Rough);
        Sketch placed = Assert.IsAssignableFrom<Succeeded>(DirectUpdater.Instance.Apply(table, add)).Sketch;

        Assert.Empty(holds);
        Part part = placed.Find<Box>(id)!.Part!;
        Assert.Equal("2x4", part.Stock);
        Assert.True(part.Rough);
    }

    [Fact]
    public void Q_toggles_rough_in_the_editing_keys()
        => Assert.Equal(EditCommand.ToggleRough, KeyMaps.Edit.Find(new Keystroke(KeyName.Q, KeyMods.None)));

    static Box Drawn(EntryMode mode, Point2 from, Point2 to)
    {
        RectangleTool tool = new();
        tool.Begin(from);
        tool.MoveTo(to);
        Assert.True(tool.TryComplete(LayerId.Default, EntityId.New(), "Part 1", out Request? request, mode));
        return Assert.IsType<Box>(Assert.IsType<AddEntity>(request).Entity);
    }
}
