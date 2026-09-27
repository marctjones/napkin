using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;
using Napkin.Interop.Pdf;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.App.Viewing;

/// <summary>
/// A permit set's paper, as the app makes it (docs/design/permit-set.md): the results, the title
/// block — its disclaimer saying also that the site plan is not a survey (§4) — and, for the elevation,
/// the standard view facing the deck's open side, built by <see cref="PaperSheet.View"/> from the very
/// calls its locked view draws with.
/// </summary>
public static class PermitPaper
{
    /// <summary>
    /// The standard view that faces a design's first deck from its open side: the side away from its
    /// ledger (Front for a ledger on the north, Back for one on the south, Left for one on the east,
    /// Right for one on the west); Front when there is no deck or no ledger.
    /// </summary>
    /// <param name="sketch">The design.</param>
    public static StandardView ElevationFor(Sketch sketch)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return Deck.All(sketch).Select(deck => deck.Ledger(sketch).Edge).FirstOrDefault() switch
        {
            DeckEdge.South => StandardView.Back,
            DeckEdge.East => StandardView.Left,
            DeckEdge.West => StandardView.Right,
            _ => StandardView.Front,
        };
    }

    /// <summary>The title block every sheet of a permit set carries.</summary>
    public static TitleBlock Title(Sketch sketch, string projectName, DateOnly date, CodePacks packs)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        return new TitleBlock(projectName, date, SheetNotes.CodeLine(sketch, packs), $"{ScopeDisclaimer.Text} {DeckSetPdf.NotASurvey}");
    }

    /// <summary>A design's deck set.</summary>
    /// <param name="sketch">The design, as the canvas shows it.</param>
    /// <param name="projectName">What the title block calls it.</param>
    /// <param name="date">The day it is printed.</param>
    /// <param name="format">How lengths are written.</param>
    /// <param name="packs">The code packs napkin found.</param>
    /// <param name="paper">Letter or Tabloid.</param>
    /// <param name="hiddenEdges">Whether the elevation draws hidden edges, as View ▸ Hidden edges has them.</param>
    public static DeckSet DeckSet(Sketch sketch, string projectName, DateOnly date, LengthFormat format, CodePacks packs, SheetPaper paper, bool hiddenEdges)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(packs);
        MaterialsLibrary library = MaterialsLibrary.Shipped;
        return new DeckSet(
            new PermitSet(Title(sketch, projectName, date, packs), format, paper, PermitItems.Of(sketch, packs, library)),
            sketch,
            packs,
            library,
            PaperSheet.View(sketch, ElevationFor(sketch), format, hiddenEdges));
    }
}
