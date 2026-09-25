# The napkin look

How napkin wears the Skeptical Engineering (skpt.cl) design system with the napkin sheet and the
carpenter's pencil, which are the defaults (View > Sketch keeps Screen/Clean, Graph and Plain). Issue #143.
Source of the rules: the design repo's `docs/brand-book.md` and `docs/avalonia-usage.md`. Styles live in
`src/Napkin.App/Styles/NapkinLook.axaml`; the vendored `Theme/*` is untouched.

## What the skpt.cl theme does to the app, in plain words

- **Fonts.** Plex Sans 13 for controls and body, Plex Mono for the title bar, tabs and text entry
  (12.5 for data), the status bar's machine values in mono. No new font files.
- **Shape.** Near-square: buttons radius 4, text boxes and combo boxes 3, panels 4. Nothing is a pill
  (the "N relationships" badge was, and is now 4).
- **Lines, not layers.** 1px rules between and around things; no drop shadows (only menus and tooltips,
  which really float, keep an edge).
- **Moss** marks the selected and the active: the armed tool, the selected tab's pipe, checkboxes,
  text selection, the focus ring. Fluent's accent chrome ignores `SystemAccentColor`, so each is set by name.
- **Bench and paper.** Tools and navigation (menu bar, status bar, title bar) are the dark bench; the
  work is on paper. The pair is the identity: a document on a desk.
- **Icons** are the vendored workbench set; the toolbar's own glyphs are drawn in the pencil colour.
- **Focus** is a 2px moss outline (the `SystemControlFocusVisual*` brushes), on everything.

## Decisions

1. **The bench is one colour in both themes, `#1c1c1c`** (`NapkinBenchColor`, mirrored in
   `ChromeColours.Bench`). "The workbench does not change when the lights go down."
2. **The sheet is light in both themes.** Napkin, Graph and Plain are paper, so the canvases, and the
   *notes lying on them* (toolbar, stock drawer, Part and Relationships panels, dimension editor, message
   panels, shape workshop), stay light, and those notes are put in the light theme
   (`ThemeVariantScope.RequestedThemeVariant`) so their buttons, text and icons stay dark-on-paper even
   when the window is dark. A napkin on the bench.
3. **So the theme toggle affects** what is not on the sheet: the cut-list window and its tabs and tables,
   dialogs, menus, flyouts, tooltips, and the Screen sheet (which keeps its theme-following palette, dark
   ground in Dark). Dark was judged worth keeping on this basis: the cut list is read at the bench,
   and Screen + Dark is a full dark drawing. A dark chalk-on-slate sheet was not built (no request).
4. **Notes follow the sheet, not the theme.** On Napkin a note is `#F8F2E6` (lighter than the sheet
   `#F1E8D6`) with the sheet's fold colour `#D3C2A2` as its 1px rule; on Graph and Plain a note is the sheet colour
   with the sheet's grid line as its rule; on Screen the theme palette as before.
5. **Title bar (macOS only).** `ExtendClientAreaToDecorationsHint` with the system traffic lights kept;
   a 30px bench bar (`TitleBar`) carries the title in Plex Mono 13 Medium, clears 80px on the left for the
   buttons, and drags the window. Avalonia 12 has no `ExtendClientAreaChromeHints`; system chrome is the
   default when extended. Unverified on screen (headless frames cannot show it). Other platforms unchanged.

## Token inconsistency

`SkepticalTheme.axaml` gives `SeBenchColor` `#1c1c1c` in its Light dictionary and `#141413` in its Dark
one, but the brand book says chrome is identical in both. napkin does not use `SeBench*` for chrome; it
uses `NapkinBench*` (Light values). The token file should be corrected upstream.

## Element to colour

| Element | Light theme | Dark theme | Notes |
|---|---|---|---|
| Menu bar, status bar, title bar | `NapkinBenchBrush` `#1c1c1c`, text `NapkinBenchHeadingBrush` / `NapkinBenchTextBrush` | same | 1px `NapkinBenchRuleBrush` on the status bar's top edge |
| Canvas ground, Napkin | `#F1E8D6` | same | pencil `#4A3F36` |
| Canvas ground, Graph / Plain | `#F5F6EE` / `#FBFBF8` | same | ink `#3B3B40` (pencil) |
| Canvas ground, Screen | `SePaperBrush`-family (`CanvasPalette.Light`) | `CanvasPalette.Dark` | the only sheet that follows the theme |
| Notes on Napkin | fill `NapkinNoteBrush`, rule `NapkinNoteRuleBrush`, ink pencil | same | light theme scope inside |
| Notes on Graph / Plain | sheet colour, grid-line rule | same | |
| Notes on Screen | palette background / grid | palette background / grid | theme scope not forced |
| Buttons (dialogs, tables) | `SePaperSunkBrush`, 1px `SeRuleBrush`, radius 4; hover `SeRuleSoftBrush` | tokens' Dark values | |
| Buttons on a note | transparent until pointed at, then a warm wash | same | armed tool: moss wash + moss border |
| Text and combo boxes | radius 3, 1px rule, moss focus | same | mono 12.5 |
| Cut list window, tabs | `SePaperBrush`, `SeInkBodyBrush`; selected tab `SeMossInkBrush` with moss pipe | Dark values of the same tokens | mono tab headers |
| Tooltips | paper, 1px `SeRuleBrush`, no shadow | bench-dark equivalents | `ToolTip*` resources |
| Focus ring | `SeFocusColor` 2px | `SeFocusColor` (Dark value) | |

## Contrast (unit-tested, `ChromeColoursTests`)

Bench heading on bench 12.8:1, bench text 6.4:1, carpenter ink on the napkin 8.4:1 and on a note 9.2:1,
pencil ink on Graph and Plain above 9:1. Ink-label and bench-quiet are never used for readable text.

## Verifying

`GUI-SET-06` saves frames (default look, Light then Dark, plan, 3D, both cut-list tabs, then Screen/Clean)
to `artifacts/gui-frames/` and asserts the bench and the sheet are identical across themes.
