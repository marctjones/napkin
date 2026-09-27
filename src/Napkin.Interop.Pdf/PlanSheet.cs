using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Interop.Pdf;

/// <summary>
/// What every page's title block says (#25; docs/design/permit-set.md §4 builds on it): the
/// project, the date, the adopted code when one is chosen, and napkin's scope disclaimer, printed in
/// full (DESIGN.md §7: the disclaimer travels with the sheet, not only the window). The scale and the
/// sheet's number and title are the writer's, page by page.
/// </summary>
/// <param name="ProjectName">The design's name.</param>
/// <param name="Date">The day the sheet was made.</param>
/// <param name="Code">The adopted code line (<see cref="SheetNotes.CodeLine"/>), or null with none chosen.</param>
/// <param name="Disclaimer">The scope disclaimer; napkin's one sentence unless a sheet set adds to it.</param>
public sealed record TitleBlock(string ProjectName, DateOnly Date, string? Code, string Disclaimer = ScopeDisclaimer.Text);

/// <summary>
/// The plan-and-elevations sheet (#25): the design's Top, Front and Right views where the on-screen
/// sheet puts them (third-angle, <c>SheetLayout</c>), at one true scale, with the rules engine's
/// results as notes and a title block on every page.
/// </summary>
/// <param name="Title">The title block.</param>
/// <param name="Format">How lengths are written: the scale's words, as the canvas writes a length.</param>
/// <param name="Views">The views, each placed in the pane the layout gives its <see cref="DrawingView.View"/>.</param>
/// <param name="NotesHeading">What the notes are headed with (<see cref="SheetNotes.Heading"/>).</param>
/// <param name="Notes">The rules engine's results (<see cref="SheetNotes.Of"/>); empty for none.</param>
public sealed record PlanSheet(
    TitleBlock Title,
    LengthFormat Format,
    IReadOnlyList<DrawingView> Views,
    string NotesHeading,
    IReadOnlyList<SheetNote> Notes);
