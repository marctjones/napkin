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
using Napkin.Interop.Pdf;
using Napkin.Modules.Editing;

using Xunit;

using Point = Avalonia.Point;

namespace Napkin.App.GuiTests.Workflows;

/// <summary>
/// File → Export plan as DXF… (#23) and File → Export plan as PDF… (#25), driven the way a person
/// drives them, and each file read back with a library a reader of that format would use.
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

    [GuiWorkflow("GUI-SHELL-08")]
    public void Export_the_coffee_table_as_a_PDF_sheet_and_again_with_hidden_edges_off() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        string folder = Directory.CreateTempSubdirectory("napkin-pdf-").FullName;
        ScriptedExport picker = new(folder);
        window.ExportPicker = picker;
        window.Today = () => new DateOnly(2026, 9, 27);

        OpenSample(app, window, "Coffee table");
        ExportPdf(app, window);
        app.Expect("the coffee table's sheet is one page at 1:12, titled, dated, disclaimed, its labels as the canvas reads them and its hidden edges dashed grey", () =>
        {
            Assert.Equal("Coffee table.pdf", picker.Suggested);
            (string operators, string text) = ReadPdf(picker.Last!);
            Assert.Contains("Coffee table", text, StringComparison.Ordinal);
            Assert.Contains("Scale 1:12", text, StringComparison.Ordinal);
            Assert.Contains("Date 2026-09-27", text, StringComparison.Ordinal);
            Assert.Contains("Sheet 1 of 1", text, StringComparison.Ordinal);
            Assert.Contains(Napkin.Core.RulesEngine.ScopeDisclaimer.Text, text, StringComparison.Ordinal);
            Assert.All(window.Canvas.Measurements(), measurement => Assert.Contains(measurement.Label(window.Canvas.LabelFormat), text, StringComparison.Ordinal));
            Assert.Contains("0.65 G\n1 w\n", operators, StringComparison.Ordinal);
            Assert.StartsWith("Exported the plan and elevations as a PDF sheet at 1:12 to Coffee table.pdf", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // Front, and H turns hidden edges off there; the sheet follows the choice.
        app.Press(Key.D3);
        app.Press(Key.H);
        app.Expect("hidden edges are off", () => Assert.False(window.Settings.Current.ShowHiddenEdges));
        ExportPdf(app, window);
        app.Expect("the second sheet prints no hidden edge and still every visible one", () =>
        {
            (string operators, _) = ReadPdf(picker.Last!);
            Assert.DoesNotContain("0.65 G", operators, StringComparison.Ordinal);
            Assert.Contains("0 G\n1.4 w\n", operators, StringComparison.Ordinal);
        });

        Directory.Delete(folder, recursive: true);
    });

    [GuiWorkflow("GUI-CUT-12")]
    public void Print_the_stocked_benchs_shop_set_and_a_new_sheets() => GuiWorkflow.Run(app =>
    {
        MainWindow window = (MainWindow)app.Target;
        string folder = Directory.CreateTempSubdirectory("napkin-shop-").FullName;
        ScriptedExport picker = new(folder);
        window.ExportPicker = picker;
        window.Today = () => new DateOnly(2026, 9, 27);

        OpenSample(app, window, "Stocked bench");
        PrintShopSet(app, window);
        app.Expect("the stocked bench's shop set reads its cut list, its layout and a label per piece, every page titled and disclaimed", () =>
        {
            Assert.Equal("Stocked bench shop set.pdf", picker.Suggested);
            Excise.Core.Document.PdfDocument document = Excise.Core.Document.PdfDocument.Open(File.ReadAllBytes(picker.Last!));
            string[] pages = [.. Enumerable.Range(0, document.PageCount).Select(i => new Excise.Core.Text.TextExtractor(document.Pages[i]).ExtractText())];
            Assert.All(pages, text => Assert.Contains(Napkin.Core.RulesEngine.ScopeDisclaimer.Text, text, StringComparison.Ordinal));
            string all = Squash(string.Concat(pages));
            ShopSet set = ShopSet.Of(window.Editor.Sketch, Napkin.Core.Materials.MaterialsLibrary.Shipped, window.Settings.Current.SawKerf, new TitleBlock("Stocked bench", new DateOnly(2026, 9, 27), null), window.Editor.LabelFormat);
            Assert.All(set.Rows, row => Assert.Contains(Squash(string.Concat(ShopSetPdf.Cells(row))), all, StringComparison.Ordinal));
            Assert.All(Napkin.Modules.Furniture.CutLayout.Rows(set.Layout), row => Assert.Contains(Squash(Napkin.Modules.Furniture.CutLayout.Line(Napkin.Modules.Furniture.CutLayout.Fields(row))), all, StringComparison.Ordinal));
            Assert.All(set.Labels, label => Assert.Contains(Squash(label.Part + label.Copy + label.Size), all, StringComparison.Ordinal));
            Assert.StartsWith($"Printed the shop set, {document.PageCount} pages, to Stocked bench shop set.pdf", window.MessageOnScreen, StringComparison.Ordinal);
        });

        // A new sheet has nothing to cut: one page that says so.
        app.Chord(Key.N);
        app.Click(new Point(450, 320));
        PrintShopSet(app, window);
        app.Expect("a new sheet's shop set is one page saying there is nothing to cut", () =>
        {
            Excise.Core.Document.PdfDocument document = Excise.Core.Document.PdfDocument.Open(File.ReadAllBytes(picker.Last!));
            Assert.Equal(1, document.PageCount);
            Assert.Contains(Napkin.Modules.Furniture.CutList.NothingToCut, new Excise.Core.Text.TextExtractor(document.Pages[0]).ExtractText(), StringComparison.Ordinal);
            Assert.StartsWith("Printed the shop set, 1 page, to", window.MessageOnScreen, StringComparison.Ordinal);
        });

        Directory.Delete(folder, recursive: true);
    });

    static string Squash(string text) => System.Text.RegularExpressions.Regex.Replace(text, @"\s+", string.Empty);

    static void PrintShopSet(AppDriver app, MainWindow window)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.PrintShopSetMenuEntry));
    }

    static void ExportPdf(AppDriver app, MainWindow window)
    {
        app.Click(CentreOf(window, window.FileMenuItem));
        app.Click(CentreOf(window, window.ExportPdfMenuEntry));
    }

    /// <summary>A PDF's one page read back as a reader reads it: its operators and its text.</summary>
    static (string Operators, string Text) ReadPdf(string path)
    {
        Excise.Core.Document.PdfDocument document = Excise.Core.Document.PdfDocument.Open(File.ReadAllBytes(path));
        Assert.Equal(1, document.PageCount);
        return (
            System.Text.Encoding.Latin1.GetString(document.Pages[0].GetContentStreamBytes()),
            new Excise.Core.Text.TextExtractor(document.Pages[0]).ExtractText());
    }

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

        public Task<string?> PickPdfDestinationAsync(string suggestedName) => PickDxfDestinationAsync(suggestedName);
    }
}
