namespace Napkin.Modules.Editing;

/// <summary>
/// The kinds of line a drawing is made of (#134, the basics of ASME Y14.2): what a person reads as
/// the part, what is behind it, and what measures it. Marks that belong to the user interface — the
/// selection, a snap, a handle, the grid — are not drawing lines and are not here.
/// </summary>
public enum LineKind
{
    /// <summary>An edge the eye sees: thick, solid.</summary>
    Visible,

    /// <summary>An edge behind a nearer part: lighter, medium dashes, drawn under the visible ones.</summary>
    Hidden,

    /// <summary>A dimension line: thin, solid.</summary>
    Dimension,

    /// <summary>An extension line, from the part out to its dimension line: thin, solid.</summary>
    Extension,

    /// <summary>A centre line: thin, long–short chain. Not drawn anywhere yet; here so the table is whole.</summary>
    Centre,
}

/// <summary>How one kind of line is drawn: its weight, its dashes and how strongly it is inked.</summary>
/// <param name="Pixels">
/// The weight <em>in pixels</em>: a line's weight is a matter of legibility, not of scale, so it never
/// changes with the zoom.
/// </param>
/// <param name="Dashes">On, off, on, off… in pixels; empty for a solid line.</param>
/// <param name="Opacity">How much of the line's ink is laid down, 0–1.</param>
public sealed record LineStyle(double Pixels, IReadOnlyList<double> Dashes, double Opacity)
{
    /// <summary>Whether the line is broken into dashes.</summary>
    public bool IsDashed => Dashes.Count > 0;
}

/// <summary>
/// The one table of line kinds (#134) that the plan and the standard views draw with, in both themes;
/// the colour is the theme's, the weight and the dashes are the table's. Where lines overlap the
/// visible one wins: hidden lines are drawn first, and never where a visible one lies
/// (<see cref="HiddenEdges"/>).
/// </summary>
public static class DrawingLines
{
    static readonly LineStyle VisibleLine = new(1.4, [], 1);
    static readonly LineStyle HiddenLine = new(1, [3, 3], 0.35);
    static readonly LineStyle ThinLine = new(0.8, [], 1);
    static readonly LineStyle CentreLine = new(0.8, [12, 3, 3, 3], 1);

    /// <summary>Every kind, in the table's order.</summary>
    public static IReadOnlyList<LineKind> All { get; } =
        [LineKind.Visible, LineKind.Hidden, LineKind.Dimension, LineKind.Extension, LineKind.Centre];

    /// <summary>How a kind of line is drawn.</summary>
    public static LineStyle Of(LineKind kind) => kind switch
    {
        LineKind.Visible => VisibleLine,
        LineKind.Hidden => HiddenLine,
        LineKind.Dimension or LineKind.Extension => ThinLine,
        LineKind.Centre => CentreLine,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a kind of line."),
    };
}
