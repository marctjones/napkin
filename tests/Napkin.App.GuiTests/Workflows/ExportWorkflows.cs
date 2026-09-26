using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.IO;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Napkin.Interop.Dxf;
using Napkin.Modules.Editing;

using Xunit;

using Point = Avalonia.Point;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// File → Export plan as DXF… (#23), driven the way a person drives it, and the file read back with
/// the same library a CAD program would read it with.
/// </summary>
public class ExportWorkflows
{
    [GuiWorkflow("GUI-SHELL-07")]
    public void Export_the_coffee_table_and_a_new_sketch_as_DXF_and_read_them_back() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        string folder = Directory.CreateTempSubdirectory("napkin-dxf-").FullName;
        ScriptedExport picker = new(folder);
        window.ExportPicker = picker;

        OpenSample(app, window, "Coffee table");
        Export(app, window);
        app.Expect("the coffee table's plan is written, its dimensions reading as the canvas labels them", () =>
        {
            Assert.Equal("Coffee table.dxf", picker.Suggested);
            CadDocument read = Read(picker.Last!);
            Assert.Equal(ACadVersion.AC1015, read.Header.Version);
            Assert.Equal(window.CurrentDesign!.Sketch.Entities.Values.Count(entity => entity is Box), read.Entities.OfType<LwPolyline>().Count());
            Assert.Equal(
                window.Canvas.Measurements().Select(measurement => measurement.Label(window.Canvas.LabelFormat)),
                read.Entities.OfType<TextEntity>().Select(text => text.Value));
            Assert.Empty(read.Entities.OfType<ACadSharp.Entities.Dimension>());
            Assert.StartsWith("Exported the plan as DXF 2000 to Coffee table.dxf", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // A new sheet, one part drawn with R and a drag, and the plan exported again.
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        app.Press(Key.R);
        app.Drag(At(window, Point2.Inches(0, 0)), At(window, Point2.Inches(12, 6)), At(window, Point2.Inches(24, 12)));
        Box drawn = Assert.Single(window.CurrentDesign!.Sketch.Entities.Values.OfType<Box>());
        Export(app, window);
        app.Expect("the new part is one closed outline at its own corners, and there is no dimension layer", () =>
        {
            CadDocument read = Read(picker.Last!);
            LwPolyline outline = Assert.Single(read.Entities.OfType<LwPolyline>());
            Assert.True(outline.IsClosed);
            Assert.Equal(
                PlanShape.Outline(drawn).Segments.Select(segment => (segment.From.X.ToInches(), segment.From.Y.ToInches())),
                outline.Vertices.Select(vertex => (vertex.Location.X, vertex.Location.Y)));
            Assert.False(read.Layers.Contains(PlanDxf.DimensionLayer));
        });

        Directory.Delete(folder, recursive: true);
    });

    static void Export(AppDriver app, MainWindow window)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.ExportDxfMenuEntry));
    }

    static CadDocument Read(string path)
    {
        using DxfReader reader = new(path);
        return reader.Read();
    }

    static void OpenSample(AppDriver app, MainWindow window, string sample)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.SamplesMenuItem));
        MenuItem item = window.GetVisualDescendants().OfType<MenuItem>().Single(candidate => (candidate.Header as string) == sample);
        app.Click(CentreOf(window, item));
    }

    static Point At(MainWindow window, Point2 world)
    {
        Point onCanvas = window.Canvas.View.ToScreen(world);
        Point origin = window.Canvas.TranslatePoint(new Point(0, 0), window)!.Value;
        return new Point(onCanvas.X + origin.X, onCanvas.Y + origin.Y);
    }

    static Point CentreOf(Visual root, Visual control)
    {
        Point topLeft = control.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in this window.");
        return topLeft + new Point(control.Bounds.Width / 2, control.Bounds.Height / 2);
    }

    /// <summary>An export picker that answers with a file in the workflow's folder, named as suggested.</summary>
    sealed class ScriptedExport(string folder) : IExportFilePicker
    {
        public string? Suggested { get; private set; }

        public string? Last { get; private set; }

        public Task<string?> PickDxfDestinationAsync(string suggestedName)
        {
            Suggested = suggestedName;
            Last = Path.Combine(folder, suggestedName);
            return Task.FromResult<string?>(Last);
        }
    }
}
