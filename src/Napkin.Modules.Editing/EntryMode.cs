using Napkin.Core.Geometry;

namespace Napkin.Modules.Editing;

/// <summary>
/// How the next gesture enters a design (<c>docs/design/sketch-mode.md</c> &#xA7;1.1): a property of
/// the editing session, never of the design — a design has no mode, a person does.
/// </summary>
public enum EntryMode
{
    /// <summary>Everything napkin does by default: fine snaps, catches stated, rectangles plain boxes.</summary>
    Precise,

    /// <summary>
    /// Pencil on a napkin: big round steps, parts that just touch with nothing stated, and a
    /// rectangle drawn as a plank — a part with no stock, marked rough.
    /// </summary>
    Rough,
}

/// <summary>What Rough mode changes about a gesture, in one place for both views and every tool.</summary>
public static class RoughEntry
{
    /// <summary>The word the status bar and the toolbar name the mode by: <c>ROUGH</c> or <c>PRECISE</c>.</summary>
    public static string Word(EntryMode mode) => mode == EntryMode.Rough ? "ROUGH" : "PRECISE";

    /// <summary>
    /// What a drop states of what its snap caught (&#xA7;2.2): everything in Precise, nothing in
    /// Rough. The snap still catches in Rough, so parts line up; it just says nothing about it.
    /// </summary>
    public static IEnumerable<Relationship> Stated(EntryMode mode, IEnumerable<Relationship> caught)
        => mode == EntryMode.Rough ? [] : caught;

    /// <summary>
    /// The part a rough rectangle is (&#xA7;2.3): no stock, no species, one piece, marked rough,
    /// with the longer plan side its length — the stock tool's rule for two free dimensions; a
    /// square's width runs east–west as its length.
    /// </summary>
    public static Part Plank(Length width, Length height)
        => new(Stock: null, Species: null, Quantity: 1, PlanAxesFor(width, height)) { Rough = true };

    /// <summary>Which plan side is the length: the longer; the east–west one on a tie.</summary>
    public static PlanAxes PlanAxesFor(Length width, Length height)
        => height > width
            ? new PlanAxes(PartDimension.Width, PartDimension.Length)
            : new PlanAxes(PartDimension.Length, PartDimension.Width);

    /// <summary>The live size while dragging, for the status bar: "4' × 1'", in the label format.</summary>
    public static string DrawingReadout(Length width, Length height, LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);
        return $"{width.Format(format).Text} × {height.Format(format).Text}";
    }

    /// <summary>
    /// A selected box, for the status bar: its name and its three finished sizes in cut-list order
    /// — length, width, thickness — so the readout and the cut list say the same thing; for a box
    /// that is not a part, its width, height and depth.
    /// </summary>
    public static string SelectedReadout(Box box, string name, LengthFormat format)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(format);

        (Length a, Length b, Length c) = box.Part is { } part
            ? (part.SizeOn(box).Length, part.SizeOn(box).Width, part.SizeOn(box).Thickness)
            : (box.Width, box.Height, box.Depth);
        return $"{name}  {a.Format(format).Text} × {b.Format(format).Text} × {c.Format(format).Text}";
    }
}
