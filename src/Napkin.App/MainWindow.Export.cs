using Avalonia.Interactivity;

using Napkin.Core.Project;
using Napkin.App.Viewing;
using Napkin.Interop.Dxf;
using Napkin.Interop.Pdf;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// File → Export plan as DXF… (#23): the plan written as DXF 2000 by <see cref="PlanDxf"/>; and
/// File → Export plan as PDF… (#25): the sheet's views at true scale, written by <see cref="SheetPdf"/>.
/// </summary>
public partial class MainWindow
{
    /// <summary>The Export plan as DXF menu entry.</summary>
    public Avalonia.Controls.MenuItem ExportDxfMenuEntry => ExportDxfMenuItem;

    void OnExportDxfClicked(object? sender, RoutedEventArgs e) => _ = ExportDxfAsync();

    /// <summary>The Export plan as PDF menu entry.</summary>
    public Avalonia.Controls.MenuItem ExportPdfMenuEntry => ExportPdfMenuItem;

    /// <summary>The day a PDF sheet's title block is dated; today unless the GUI suite pins it.</summary>
    public Func<DateOnly> Today { get; set; } = () => DateOnly.FromDateTime(DateTime.Now);

    void OnExportPdfClicked(object? sender, RoutedEventArgs e) => _ = ExportPdfAsync();

    /// <summary>The design's name without its file ending: what an export is named and titled.</summary>
    string ExportName()
    {
        string name = Editor.Design.Name;
        foreach (string ending in (string[])[".scene.json", ".json"])
        {
            if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^ending.Length];
            }
        }

        return name;
    }

    /// <summary>
    /// Asks where, writes the sheet there — Top, Front and Right at one true scale, the code check's
    /// results as notes, the title block on every page — and says what happened.
    /// </summary>
    /// <returns>Whether a file was written.</returns>
    public async Task<bool> ExportPdfAsync()
    {
        string name = ExportName();
        string? path;
        try
        {
            path = await ExportPicker.PickPdfDestinationAsync(name + ".pdf").ConfigureAwait(true);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not exported: {exception.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            Editor.Say(EditSeverity.Hint, "Not exported — no file was chosen, so nothing was written.");
            return false;
        }

        PlanSheet sheet = PaperSheet.Of(Editor.Sketch, name, Today(), Editor.LabelFormat, Packs, Settings.Current.ShowHiddenEdges);
        try
        {
            using FileStream file = File.Create(path);
            SheetPdf.Write(sheet, file);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not exported: {exception.Message}");
            return false;
        }

        Editor.Say(
            EditSeverity.Done,
            $"Exported the plan and elevations as a PDF sheet at {SheetPdf.Place(sheet).Scale.Label} to {Path.GetFileName(path)} in {Path.GetDirectoryName(path)}.");
        return true;
    }

    /// <summary>Asks where, writes the plan there, and says what happened.</summary>
    /// <returns>Whether a file was written.</returns>
    public async Task<bool> ExportDxfAsync()
    {
        string name = ExportName();
        string? path;
        try
        {
            path = await ExportPicker.PickDxfDestinationAsync(name + ".dxf").ConfigureAwait(true);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not exported: {exception.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            Editor.Say(EditSeverity.Hint, "Not exported — no file was chosen, so nothing was written.");
            return false;
        }

        try
        {
            using FileStream file = File.Create(path);
            PlanDxf.Write(Editor.Sketch, Editor.LabelFormat, file);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not exported: {exception.Message}");
            return false;
        }

        Editor.Say(EditSeverity.Done, $"Exported the plan as DXF 2000 to {Path.GetFileName(path)} in {Path.GetDirectoryName(path)}.");
        return true;
    }
}
