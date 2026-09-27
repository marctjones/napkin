using Avalonia.Interactivity;

using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.App.Viewing;
using Napkin.Interop.Dxf;
using Napkin.Interop.Pdf;
using Napkin.Modules.Editing;

namespace Napkin.App;

/// <summary>
/// File → Export plan as DXF… (#23): the plan written as DXF 2000 by <see cref="PlanDxf"/>; and
/// File → Export plan as PDF… (#25): the sheet's views at true scale, written by <see cref="SheetPdf"/>;
/// and File → Print shop set… (#211): the cut list, layout and labels, written by <see cref="ShopSetPdf"/>.
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

    /// <summary>The Print shop set menu entry.</summary>
    public Avalonia.Controls.MenuItem PrintShopSetMenuEntry => PrintShopSetMenuItem;

    void OnPrintShopSetClicked(object? sender, RoutedEventArgs e) => _ = PrintShopSetAsync();

    /// <summary>
    /// Asks where, writes the shop set there (#211) — the cut list, the cut layout drawn and a label
    /// per piece, from the rows and layout the cut-list window shows, at the saw kerf set there — and
    /// says what happened.
    /// </summary>
    /// <returns>Whether a file was written.</returns>
    public async Task<bool> PrintShopSetAsync()
    {
        string name = ExportName();
        string? path;
        try
        {
            path = await ExportPicker.PickPdfDestinationAsync(name + " shop set.pdf").ConfigureAwait(true);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not printed: {exception.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            Editor.Say(EditSeverity.Hint, "Not printed — no file was chosen, so nothing was written.");
            return false;
        }

        ShopSet set = ShopSet.Of(Editor.Sketch, MaterialsLibrary.Shipped, Settings.Current.SawKerf, new TitleBlock(name, Today(), null), Editor.LabelFormat);
        Excise.Core.Document.PdfDocument document = ShopSetPdf.Document(set);
        try
        {
            using FileStream file = File.Create(path);
            document.Save(file);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not printed: {exception.Message}");
            return false;
        }

        int pages = document.PageCount;
        Editor.Say(
            EditSeverity.Done,
            $"Printed the shop set, {pages} {(pages == 1 ? "page" : "pages")}, to {Path.GetFileName(path)} in {Path.GetDirectoryName(path)}.");
        return true;
    }

    /// <summary>The Print permit set on Letter menu entry.</summary>
    public Avalonia.Controls.MenuItem PermitLetterMenuEntry => PermitLetterMenuItem;

    /// <summary>The Print permit set on Tabloid menu entry.</summary>
    public Avalonia.Controls.MenuItem PermitTabloidMenuEntry => PermitTabloidMenuItem;

    void OnPrintPermitLetterClicked(object? sender, RoutedEventArgs e) => _ = PrintPermitSetAsync(SheetPaper.Letter);

    void OnPrintPermitTabloidClicked(object? sender, RoutedEventArgs e) => _ = PrintPermitSetAsync(SheetPaper.Tabloid);

    /// <summary>What the message bar says when a design has nothing a permit set napkin makes is about.</summary>
    public const string NothingToPermit = "Not printed — a permit set needs a deck, or an opening in a wall, to show: draw one first.";

    /// <summary>
    /// Asks where, writes the design's permit set there on the chosen paper, and says what happened — and,
    /// when anything is not sized, that the set says so. A design with a deck gets the deck set (#226: S1,
    /// A2, S2, S3, C1, W1); one with an opening in a wall and no deck, the window set (#227: S1, A1, A2, S3,
    /// C1, W1).
    /// </summary>
    /// <param name="paper">Letter or Tabloid.</param>
    /// <returns>Whether a file was written.</returns>
    public async Task<bool> PrintPermitSetAsync(SheetPaper paper)
    {
        ArgumentNullException.ThrowIfNull(paper);
        bool deck = !Napkin.Modules.Building.Deck.All(Editor.Sketch).IsEmpty;
        if (!deck && Napkin.Modules.Building.CodeCheck.Of(Editor.Sketch, Packs).IsEmpty)
        {
            Editor.Say(EditSeverity.Hint, NothingToPermit);
            return false;
        }

        string name = ExportName();
        string? path;
        try
        {
            path = await ExportPicker.PickPdfDestinationAsync(name + " permit set.pdf").ConfigureAwait(true);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not printed: {exception.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            Editor.Say(EditSeverity.Hint, "Not printed — no file was chosen, so nothing was written.");
            return false;
        }

        PermitSet permit;
        Excise.Core.Document.PdfDocument document;
        if (deck)
        {
            DeckSet set = PermitPaper.DeckSet(Editor.Sketch, name, Today(), Editor.LabelFormat, Packs, paper, Settings.Current.ShowHiddenEdges);
            (permit, document) = (set.Permit, DeckSetPdf.Document(set));
        }
        else
        {
            WindowSet set = PermitPaper.WindowSet(Editor.Sketch, name, Today(), Editor.LabelFormat, Packs, paper, Settings.Current.ShowHiddenEdges);
            (permit, document) = (set.Permit, WindowSetPdf.Document(set));
        }

        try
        {
            using FileStream file = File.Create(path);
            document.Save(file);
        }
        catch (Exception exception) when (ProjectFile.IsFileException(exception))
        {
            Editor.Say(EditSeverity.Problem, $"Not printed: {exception.Message}");
            return false;
        }

        string incomplete = PermitItems.Banner(permit.Items) is { } banner ? $" {banner}." : string.Empty;
        Editor.Say(
            EditSeverity.Done,
            $"Printed the {(deck ? "deck's" : "window and door")} permit set, {document.PageCount} sheets on {paper.Name}, to {Path.GetFileName(path)} in {Path.GetDirectoryName(path)}.{incomplete}");
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
