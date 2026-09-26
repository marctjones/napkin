using Avalonia.Interactivity;

using Napkin.Core.Project;
using Napkin.Interop.Dxf;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>File → Export plan as DXF… (#23): the plan written as DXF 2000 by <see cref="PlanDxf"/>.</summary>
public partial class MainWindow
{
    /// <summary>The Export plan as DXF menu entry.</summary>
    public Avalonia.Controls.MenuItem ExportDxfMenuEntry => ExportDxfMenuItem;

    void OnExportDxfClicked(object? sender, RoutedEventArgs e) => _ = ExportDxfAsync();

    /// <summary>Asks where, writes the plan there, and says what happened.</summary>
    /// <returns>Whether a file was written.</returns>
    public async Task<bool> ExportDxfAsync()
    {
        string name = Editor.Design.Name;
        foreach (string ending in (string[])[".scene.json", ".json"])
        {
            if (name.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^ending.Length];
            }
        }

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
