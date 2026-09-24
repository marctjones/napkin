using Avalonia.Media;

namespace Napkin.App.Viewing;

/// <summary>
/// The colours of the workbench chrome (menu bar, status bar, title bar), the same in both themes.
/// </summary>
/// <remarks>
/// The Skeptical Engineering brand book says the workbench does not change when the lights go
/// down, but the vendored token file gives <c>SeBenchColor</c> #1c1c1c in its Light dictionary and
/// #141413 in its Dark one. napkin follows the brand book: one bench colour, the Light value, held
/// in <c>NapkinBenchColor</c> (Styles/NapkinLook.axaml) and mirrored here so a test can pin it.
/// See docs/design/napkin-look.md.
/// </remarks>
public static class ChromeColours
{
    public static readonly Color Bench = Color.Parse("#1C1C1C");
    public static readonly Color BenchHeading = Color.Parse("#E0E0D8");
    public static readonly Color BenchText = Color.Parse("#A0A090");
    /// <summary>A note lying on the napkin sheet: lighter than the sheet (mirrors NapkinNoteColor).</summary>
    public static readonly Color NapkinNote = Color.Parse("#F8F2E6");

    /// <summary>The fill of a note on a sheet: the napkin gets a lighter card, the other sheets their own colour.</summary>
    public static Color Note(SketchPaper paper, Color sheet) => paper == SketchPaper.Napkin ? NapkinNote : sheet;

    /// <summary>The rule around a note on a sheet, or null on the screen look, which keeps its palette's.</summary>
    public static Color? NoteRule(SketchPaper paper) => paper == SketchPaper.Napkin ? SketchColours.NapkinBorder : null;

    public static readonly Color BenchRule = Color.Parse("#505048");
}
