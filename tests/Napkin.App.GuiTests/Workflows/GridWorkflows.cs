using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Settings;
using Napkin.Core.Geometry;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>The grid's lines and the grid's snapping are two settings, each remembered (#113).</summary>
public class GridWorkflows
{
    [GuiWorkflow("GUI-SET-03")]
    public void Hiding_the_grid_does_not_stop_snapping_and_snapping_off_does_not_hide_the_grid() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        Point[] drag = [new(620, 300), new(700, 340), new(803, 391)];

        OpenSample(app, window, "Coffee table");

        app.Expect("the grid shows and snapping is on by default; the status bar names the step", () =>
        {
            Assert.True(window.Canvas.ShowGrid);
            Assert.True(window.Canvas.SnapToGrid);
            Assert.NotNull(window.FindControl<MenuItem>("GridMenuItem")!.Icon);
            Assert.NotNull(window.FindControl<MenuItem>("SnapToGridMenuItem")!.Icon);
            Assert.Contains("Snap ", window.FindControl<TextBlock>("ZoomText")!.Text);
            Assert.True(LinesInPatch(app, window) > 0, "no grid lines are drawn.");
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("GridMenuItem")!));

        app.Expect("the lines are gone, the menu says so, and snapping is untouched", () =>
        {
            Assert.False(window.Canvas.ShowGrid);
            Assert.Null(window.FindControl<MenuItem>("GridMenuItem")!.Icon);
            Assert.True(window.Canvas.SnapToGrid);
            Assert.True(window.Settings.Current.SnapToGrid);
            Assert.False(window.Settings.Current.ShowGrid);
            Assert.Equal(0, LinesInPatch(app, window));
        });

        // With the lines hidden, a drawn part still lands on the grid.
        HashSet<EntityId> known = [.. window.CurrentDesign!.Sketch.Entities.Keys];
        app.Press(Key.R);
        app.Drag(drag);

        app.Expect("the part drawn with the grid hidden is on the grid", () =>
        {
            Box drawn = NewBox(window, known);
            long step = SnapGrid.UnitsPerStep(window.Canvas.GridStepInches);
            Assert.All(new[] { drawn.Anchor.X.Units, drawn.Anchor.Y.Units, drawn.Width.Units, drawn.Height.Units }, units => Assert.Equal(0, units % step));
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("SnapToGridMenuItem")!));

        app.Expect("snapping is off, the grid is still hidden, and the status bar no longer names a step", () =>
        {
            Assert.False(window.Canvas.SnapToGrid);
            Assert.False(window.Canvas.ShowGrid);
            Assert.DoesNotContain("Snap", window.FindControl<TextBlock>("ZoomText")!.Text);
        });

        known = [.. window.CurrentDesign!.Sketch.Entities.Keys];
        app.Press(Key.R);
        app.Drag(drag);

        app.Expect("the part drawn with snapping off is not on the grid", () =>
        {
            Box drawn = NewBox(window, known);
            long step = SnapGrid.UnitsPerStep(window.Canvas.GridStepInches);
            Assert.Contains(
                new[] { drawn.Anchor.X.Units, drawn.Anchor.Y.Units, drawn.Width.Units, drawn.Height.Units },
                units => units % step != 0);
        });

        // G brings the lines back, in the plan and in the 3D view, without touching snapping.
        app.Press(Key.G);
        app.Press(Key.V);

        app.Expect("G showed the grid again on both views, and snapping stayed off", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.True(window.Canvas.ShowGrid);
            Assert.True(window.Model.ShowGrid);
            Assert.False(window.Model.SnapToGrid);
            Assert.True(window.Settings.Current.ShowGrid);
            Assert.False(window.Settings.Current.SnapToGrid);
        });

        app.Press(Key.G);

        app.Expect("G in the 3D view hides the ground grid there too", () => Assert.False(window.Model.ShowGrid));

        app.Expect("a new window on the same settings has both choices: no grid, no snapping", () =>
        {
            var next = new MainWindow(new SettingsStore(window.Settings.Location));
            try
            {
                next.Show();
                Assert.False(next.Canvas.ShowGrid);
                Assert.False(next.Canvas.SnapToGrid);
                Assert.False(next.Model.ShowGrid);
                Assert.Null(next.FindControl<MenuItem>("GridMenuItem")!.Icon);
                Assert.Null(next.FindControl<MenuItem>("SnapToGridMenuItem")!.Icon);
            }
            finally
            {
                next.Close();
            }
        });
    });

    static Box NewBox(MainWindow window, HashSet<EntityId> known) =>
        window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>().Single(box => !known.Contains(box.Id));

    /// <summary>How many pixels of an empty stretch of the plan are not the paper: grid lines, when they show.</summary>
    static int LinesInPatch(AppDriver app, MainWindow window)
    {
        var paper = Napkin.App.Viewing.CanvasPalette.For(window.ActualThemeVariant).Background;
        return FrameSampling.Patch(app, 620, 300, 200, 100).Count(color => color != paper);
    }
}
