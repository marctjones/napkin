using System.Collections.Immutable;
using System.Globalization;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture;

/// <summary>One piece on a sheet, where it is cut from.</summary>
/// <param name="Label">The cut-list row's label.</param>
/// <param name="Along">Its size along the sheet's long side.</param>
/// <param name="Across">Its size across the sheet.</param>
/// <param name="Turned">Whether its width, not its length, runs along the sheet.</param>
/// <param name="X">Its start along the sheet's long side, from the sheet's end.</param>
/// <param name="Y">Its start across the sheet, from the sheet's edge.</param>
public readonly record struct SheetPiece(string Label, Length Along, Length Across, bool Turned, Length X, Length Y);

/// <summary>One strip ripped from a sheet along its long side, and the pieces crosscut from it.</summary>
/// <param name="Y">Where the strip starts across the sheet.</param>
/// <param name="Width">How wide it is ripped: its widest piece.</param>
/// <param name="Pieces">The pieces, in the order they are crosscut.</param>
/// <param name="Crosscuts">The crosscuts this strip takes (the board rule, <see cref="CutLayout.TryFit"/>).</param>
public sealed record SheetStrip(Length Y, Length Width, ImmutableArray<SheetPiece> Pieces, int Crosscuts)
{
    /// <summary>Pieces narrower than the strip: each takes one more rip to its own width.</summary>
    public int Trims => Pieces.Count(piece => piece.Across < Width);
}

/// <summary>One sheet to buy, with the strips ripped from it.</summary>
/// <param name="Number">1, 2, 3 ... within its panel and species.</param>
/// <param name="Long">The sheet's long side.</param>
/// <param name="Short">The sheet's short side.</param>
/// <param name="Strips">The strips, in the order they are ripped.</param>
/// <param name="Rips">The rips that separate the strips (the board rule across the sheet).</param>
/// <param name="Kerf">The blade's kerf used.</param>
public sealed record PlannedSheet(int Number, Length Long, Length Short, ImmutableArray<SheetStrip> Strips, int Rips, Length Kerf)
{
    /// <summary>Every piece on the sheet, strip by strip.</summary>
    public IEnumerable<SheetPiece> Pieces => Strips.SelectMany(strip => strip.Pieces);

    /// <summary>Every saw cut: the rips, each strip's crosscuts, and each narrower piece's trim.</summary>
    public int Cuts => Rips + Strips.Sum(strip => strip.Crosscuts + strip.Trims);

    /// <summary>The sheet's area, in square 1/1024″.</summary>
    public Int128 SheetArea => Area.Of(Long, Short);

    /// <summary>The pieces' area.</summary>
    public Int128 PiecesArea => Pieces.Aggregate(Int128.Zero, (sum, piece) => sum + Area.Of(piece.Along, piece.Across));

    /// <summary>Everything on the sheet that is not a piece: kerf and offcuts.</summary>
    public Int128 WasteArea => SheetArea - PiecesArea;
}

/// <summary>A piece no sheet holds, and why.</summary>
/// <param name="Label">The cut-list row's label.</param>
/// <param name="Length">The piece's length.</param>
/// <param name="Width">The piece's width.</param>
/// <param name="AgainstGrain">True when it would fit turned, but its grain forbids turning it.</param>
public readonly record struct RefusedSheetPiece(string Label, Length Length, Length Width, bool AgainstGrain);

/// <summary>The sheets of one panel and species, and the pieces no sheet holds.</summary>
/// <param name="Panel">The sheet good.</param>
/// <param name="Species">The species, or empty.</param>
/// <param name="Sheets">The sheets, in the order they were opened.</param>
/// <param name="Refused">The pieces no sheet holds, in the order refused.</param>
public sealed record PanelLayout(PanelStock Panel, string Species, ImmutableArray<PlannedSheet> Sheets, ImmutableArray<RefusedSheetPiece> Refused);

/// <summary>
/// Sheet-goods nesting (#26): which part is cut from which sheet, as a table saw cuts it — strips
/// ripped along the sheet's long side, pieces crosscut from each strip — with the saw kerf. The one
/// place that decides how many sheets a panel's parts need, so the shopping list and the layout agree.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The rules.</strong> An in-house heuristic, no library: first-fit decreasing on strips
/// (a guillotine layout, so every cut runs edge to edge). Each piece is turned so its longer side
/// runs along the sheet, unless it only fits the other way; a piece whose grain is stated
/// (<see cref="CutListRow.Grain"/>) runs with its grain along the sheet's long side and is never
/// turned — napkin's assumption that a sheet's face grain runs its long way, said in
/// <see cref="Statement"/>. Pieces are taken widest across first, then longest, ties in cut-list
/// order; each goes in the first open strip, on any open sheet, that is wide enough and still has
/// length for it; failing that, a new strip on the first sheet with width left for it; failing that,
/// a new sheet. A strip is as wide as its first (widest) piece.
/// </para>
/// <para>
/// <strong>Kerf.</strong> The board rule (<see cref="CutLayout.TryFit"/>) along each strip for the
/// crosscuts and across the sheet for the rips: the last cut may run out through the end. A piece
/// narrower than its strip takes one more rip. All arithmetic is exact on <see cref="Length"/>.
/// </para>
/// </remarks>
public static class SheetLayout
{
    /// <summary>What the sheet layout says about itself, on screen and in the exported file.</summary>
    public const string Statement =
        "Sheets: strips ripped along each sheet's long side, pieces crosscut from them (first-fit decreasing, napkin's own); "
        + "a part with its grain set keeps it along the sheet's long side — napkin assumes a sheet's face grain runs its long way.";

    /// <summary>The note on a sheet count from this layout.</summary>
    public const string SheetsByLayout = "sheets from the cut layout";

    /// <summary>Lays out the sheet parts of one panel and species.</summary>
    /// <param name="panel">The sheet good.</param>
    /// <param name="species">The species.</param>
    /// <param name="members">The cut-list rows that use it.</param>
    /// <param name="kerf">The saw kerf.</param>
    public static PanelLayout Of(PanelStock panel, string species, IEnumerable<CutListRow> members, Length kerf)
    {
        ArgumentNullException.ThrowIfNull(panel);
        ArgumentNullException.ThrowIfNull(members);
        if (kerf < Length.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(kerf), kerf, "A saw kerf cannot be negative.");
        }

        Length along = Length.Max(panel.SheetWidth, panel.SheetLength);
        Length across = Length.Min(panel.SheetWidth, panel.SheetLength);

        List<(string Label, Length Along, Length Across, bool Turned)> pieces = [];
        List<RefusedSheetPiece> refused = [];
        foreach (CutListRow row in members)
        {
            for (int copy = 0; copy < row.Quantity; copy++)
            {
                if (Orient(row, along, across) is { } placed)
                {
                    pieces.Add((row.Label, placed.Along, placed.Across, placed.Turned));
                }
                else
                {
                    bool turnedFits = Fits(row.Width, row.Length, along, across) || Fits(row.Length, row.Width, along, across);
                    refused.Add(new RefusedSheetPiece(row.Label, row.Length, row.Width, AgainstGrain: turnedFits));
                }
            }
        }

        // Widest across first, then longest; OrderBy is stable, so ties keep cut-list order.
        var ordered = pieces.OrderByDescending(piece => piece.Across).ThenByDescending(piece => piece.Along).ToList();

        List<List<(Length Width, List<(string Label, Length Along, Length Across, bool Turned)> Pieces)>> sheets = [];
        foreach (var piece in ordered)
        {
            if (!IntoOpenStrip(sheets, piece, kerf, along) && !IntoNewStrip(sheets, piece, kerf, across))
            {
                sheets.Add([(piece.Across, [piece])]);
            }
        }

        List<PlannedSheet> planned = [];
        for (int i = 0; i < sheets.Count; i++)
        {
            List<SheetStrip> strips = [];
            Length y = Length.Zero;
            foreach ((Length width, var stripPieces) in sheets[i])
            {
                List<SheetPiece> placed = [];
                Length x = Length.Zero;
                foreach (var piece in stripPieces)
                {
                    placed.Add(new SheetPiece(piece.Label, piece.Along, piece.Across, piece.Turned, x, y));
                    x += piece.Along + kerf;
                }

                CutLayout.TryFit(Sum(stripPieces.Select(piece => piece.Along)), stripPieces.Count, kerf, along, out int crosscuts);
                strips.Add(new SheetStrip(y, width, [.. placed], crosscuts));
                y += width + kerf;
            }

            CutLayout.TryFit(Sum(strips.Select(strip => strip.Width)), strips.Count, kerf, across, out int rips);
            planned.Add(new PlannedSheet(i + 1, along, across, [.. strips], rips, kerf));
        }

        return new PanelLayout(panel, species, [.. planned], [.. refused]);
    }

    /// <summary>
    /// How a piece lies on the sheet: its grain along the sheet when it has one, otherwise its longer
    /// side along unless only the other way fits; null when it cannot lie on the sheet at all.
    /// </summary>
    static (Length Along, Length Across, bool Turned)? Orient(CutListRow row, Length along, Length across)
    {
        if (row.Grain == PartDimension.Length)
        {
            return Fits(row.Length, row.Width, along, across) ? (row.Length, row.Width, false) : null;
        }

        if (row.Grain == PartDimension.Width)
        {
            return Fits(row.Width, row.Length, along, across) ? (row.Width, row.Length, true) : null;
        }

        bool lengthLonger = row.Length >= row.Width;
        (Length Along, Length Across, bool Turned) preferred = lengthLonger ? (row.Length, row.Width, false) : (row.Width, row.Length, true);
        (Length Along, Length Across, bool Turned) other = lengthLonger ? (row.Width, row.Length, true) : (row.Length, row.Width, false);
        return Fits(preferred.Along, preferred.Across, along, across) ? preferred
            : Fits(other.Along, other.Across, along, across) ? other
            : null;
    }

    static bool Fits(Length pieceAlong, Length pieceAcross, Length along, Length across) => pieceAlong <= along && pieceAcross <= across;

    static bool IntoOpenStrip(
        List<List<(Length Width, List<(string Label, Length Along, Length Across, bool Turned)> Pieces)>> sheets,
        (string Label, Length Along, Length Across, bool Turned) piece,
        Length kerf,
        Length along)
    {
        foreach (var sheet in sheets)
        {
            foreach ((Length width, var pieces) in sheet)
            {
                if (piece.Across <= width
                    && CutLayout.TryFit(Sum(pieces.Select(placed => placed.Along)) + piece.Along, pieces.Count + 1, kerf, along, out _))
                {
                    pieces.Add(piece);
                    return true;
                }
            }
        }

        return false;
    }

    static bool IntoNewStrip(
        List<List<(Length Width, List<(string Label, Length Along, Length Across, bool Turned)> Pieces)>> sheets,
        (string Label, Length Along, Length Across, bool Turned) piece,
        Length kerf,
        Length across)
    {
        foreach (var sheet in sheets)
        {
            if (CutLayout.TryFit(Sum(sheet.Select(strip => strip.Width)) + piece.Across, sheet.Count + 1, kerf, across, out _))
            {
                sheet.Add((piece.Across, [piece]));
                return true;
            }
        }

        return false;
    }

    static Length Sum(IEnumerable<Length> lengths) => lengths.Aggregate(Length.Zero, (sum, length) => sum + length);

    /// <summary>An area in square feet to a tenth, rounded half up: "12.3 sq ft".</summary>
    /// <param name="area">The area, in square 1/1024″.</param>
    public static string SquareFeet(Int128 area)
    {
        Int128 perTenth = (Int128)Length.UnitsPerFoot * Length.UnitsPerFoot;
        Int128 tenths = ((area * 10) + (perTenth / 2)) / perTenth;
        return $"{(long)(tenths / 10)}.{(long)(tenths % 10)} sq ft";
    }

    /// <summary>The pieces of a sheet as a line reads them: "strip 1, 23 1/4 in: Side 30 × 23 1/4 in + ...; strip 2 ...".</summary>
    /// <param name="sheet">The sheet.</param>
    public static string Pieces(PlannedSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        return string.Join(
            "; ",
            sheet.Strips.Select((strip, index) =>
                $"strip {(index + 1).ToString(CultureInfo.InvariantCulture)}, {CutLayout.Inches(strip.Width)}: "
                + string.Join(" + ", strip.Pieces.Select(piece => $"{piece.Label} {Bare(piece.Along)} × {CutLayout.Inches(piece.Across)}{(piece.Turned ? " (turned)" : string.Empty)}"))));
    }

    /// <summary>A length as <see cref="CutLayout.Inches"/> writes it, without the unit: "23 1/4".</summary>
    static string Bare(Length length) => CutLayout.Inches(length)[..^3];
}
