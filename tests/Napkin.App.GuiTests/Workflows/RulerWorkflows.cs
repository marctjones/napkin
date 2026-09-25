using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;

using Xunit;

using static Napkin.App.GuiTests.Workflows.AssemblyWorkflows;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// Rulers on the plan and a scale bar on the orthographic 3D view (#115), driven from the View menu.
/// </summary>
public class RulerWorkflows
{
    [GuiWorkflow("GUI-VIEW-06")]
    public void Rulers_run_along_the_plan_and_a_scale_bar_along_the_orthographic_3D_view() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        Control toolBar = window.FindControl<Control>("ToolBar")!;

        OpenSample(app, window, "Coffee table");

        app.Expect("the plan opens with no rulers, and the toolbar is where it always was", () =>
        {
            Assert.Empty(window.Canvas.RulerLabelsOnScreen);
            Assert.Equal(10, toolBar.Margin.Left);
            Assert.Equal(10, toolBar.Margin.Top);
            Assert.Null(window.FindControl<MenuItem>("RulersMenuItem")!.Icon);
        });

        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("RulersMenuItem")!));

        string[] before = [];
        app.Expect("the rulers show feet and inches from the origin, the menu is ticked, and the toolbar stepped in from under them", () =>
        {
            before = [.. window.Canvas.RulerLabelsOnScreen];
            Assert.Contains("0\"", before);
            Assert.True(before.Distinct().Count() >= 3, $"the rulers say only {string.Join(", ", before)}.");
            Assert.NotNull(window.FindControl<MenuItem>("RulersMenuItem")!.Icon);
            Assert.Equal(10 + CanvasView.RulerThickness, toolBar.Margin.Left);
            Assert.Equal(10 + CanvasView.RulerThickness, toolBar.Margin.Top);

            // The toolbar is on the canvas, clear of both rulers.
            Point canvas = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
            Point bar = toolBar.TranslatePoint(new Point(0, 0), window)!.Value;
            Assert.True(bar.X >= canvas.X + CanvasView.RulerThickness, "the toolbar sits on the left ruler.");
            Assert.True(bar.Y >= canvas.Y + CanvasView.RulerThickness, "the toolbar sits on the top ruler.");
        });


        app.MoveTo(OnPlan(window, new Napkin.Core.Geometry.Point2(Napkin.Core.Geometry.Length.Inches(24), Napkin.Core.Geometry.Length.Inches(12))));
        app.SaveFrame("plan-with-rulers");


        // Zoom in: the ruler follows the grid to a finer step, so what it says changes.
        app.Press(Key.Add);
        app.Press(Key.Add);
        app.Press(Key.Add);

        app.Expect("zoomed in, the rulers read a finer step than before", () =>
        {
            string[] after = [.. window.Canvas.RulerLabelsOnScreen];
            Assert.NotEmpty(after);
            Assert.NotEqual(before, after);

            // What the zoomed plan draws past its edge must not take input from the menu bar above it.
            Assert.IsNotType<CanvasView>(window.InputHitTest(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!)));
        });

        app.SaveFrame("plan-rulers-zoomed");


        // The 3D view opens in perspective: no scale bar there, and the toolbar is back where it was.
        app.Press(Key.V);

        app.Expect("in perspective the 3D view has no scale bar, and the panels are back at their margins", () =>
        {
            Assert.True(window.IsShowingModel);
            Assert.Null(window.Model.ScaleBarLabel);
            Assert.Equal(10, toolBar.Margin.Left);
            Assert.Equal(10, toolBar.Margin.Top);
        });

        app.Press(Key.O);

        app.Expect("orthographic, it shows a scale bar on the grid ladder", () =>
        {
            Assert.False(window.Model.Camera.IsPerspective);
            string label = window.Model.ScaleBarLabel ?? throw new InvalidOperationException("No scale bar.");
            Assert.Equal(ScaleBar.LabelFor(window.Model.Camera.PixelsPerInch), label);
        });

        app.SaveFrame("3d-scale-bar");
        app.Press(Key.O);

        app.Expect("back in perspective the bar goes", () => Assert.Null(window.Model.ScaleBarLabel));

        // Back to the plan: the rulers are still on. Turn them off from the menu.
        app.Press(Key.V);
        app.Click(CentreOf(window, window.FindControl<MenuItem>("ViewMenu")!));
        app.Click(CentreOf(window, window.FindControl<MenuItem>("RulersMenuItem")!));

        app.Expect("the rulers are gone, the menu is unticked, and the toolbar is where it started", () =>
        {
            Assert.Empty(window.Canvas.RulerLabelsOnScreen);
            Assert.Null(window.FindControl<MenuItem>("RulersMenuItem")!.Icon);
            Assert.Equal(10, toolBar.Margin.Left);
            Assert.Null(window.Model.ScaleBarLabel);
        });
    });
}
