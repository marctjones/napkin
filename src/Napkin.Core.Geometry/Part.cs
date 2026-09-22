using System.Globalization;

namespace Napkin.Core.Geometry;

/// <summary>
/// One of the three finished dimensions a part has. A plan view holds two of them; the third is
/// the one <see cref="Part.OutOfPlane"/> carries.
/// </summary>
public enum PartDimension
{
    /// <summary>How long the piece is: the dimension a cut list is sorted by.</summary>
    Length,

    /// <summary>How wide the piece is: the face a board shows.</summary>
    Width,

    /// <summary>How thick the piece is.</summary>
    Thickness,
}

/// <summary>
/// Which of a part's three finished dimensions lies along the box's local X (its stored
/// <see cref="Box.Width"/>) and which along its local Y (its stored <see cref="Box.Height"/>).
/// </summary>
/// <remarks>
/// <para>
/// The remaining name — the one neither axis claims — is the out-of-plane dimension, and its value
/// is the one number the part stores itself (<c>docs/design/parts-and-cut-list.md</c> §1.1).
/// Nothing is stored twice: two of the three come from the box's parameters and one from the part,
/// which is what keeps <c>CUT-002</c> true by construction — there is no second copy of an in-plan
/// dimension that could drift from the box's.
/// </para>
/// <para>
/// A box 40&#x2033; &#xD7; 3/4&#x2033; could be a 40&#x2033;-long piece on edge or a 3/4&#x2033;
/// rip; only the person drawing knows, so this is stated rather than guessed at.
/// </para>
/// </remarks>
public readonly record struct PlanAxes
{
    /// <summary>Names the two in-plan dimensions.</summary>
    /// <param name="x">The dimension the box's stored width is.</param>
    /// <param name="y">The dimension the box's stored height is.</param>
    /// <exception cref="ArgumentException"><paramref name="x"/> and <paramref name="y"/> are the same name.</exception>
    public PlanAxes(PartDimension x, PartDimension y)
    {
        if (x == y)
        {
            throw new ArgumentException(
                $"A part's two plan axes must name different dimensions; both name {x}.",
                nameof(y));
        }

        X = x;
        Y = y;
    }

    /// <summary>The dimension the box's stored <see cref="Box.Width"/> is.</summary>
    public PartDimension X { get; }

    /// <summary>The dimension the box's stored <see cref="Box.Height"/> is.</summary>
    public PartDimension Y { get; }

    /// <summary>The one name neither axis claims, whose value is <see cref="Part.OutOfPlane"/>.</summary>
    public PartDimension OutOfPlane
        => (PartDimension.Length != X && PartDimension.Length != Y) ? PartDimension.Length
            : (PartDimension.Width != X && PartDimension.Width != Y) ? PartDimension.Width
            : PartDimension.Thickness;
}

/// <summary>
/// A part's three finished dimensions, in the order a cut list is read at a bench.
/// </summary>
/// <param name="Length">How long the piece is.</param>
/// <param name="Width">How wide the piece is.</param>
/// <param name="Thickness">How thick the piece is.</param>
public readonly record struct FinishedSize(Length Length, Length Width, Length Thickness);

/// <summary>
/// What a <see cref="Box"/> needs to be a piece somebody cuts: the dimension a plan view cannot
/// hold, which of the three the plan's two axes are, and which stock it comes from.
/// </summary>
/// <remarks>
/// <para>
/// A part is not a new entity type and not a joinery model: a tenon, a dado and a mitre change a
/// finished length and napkin does not know about them, so the cut list lists the stored size with
/// no allowance and says so (<c>docs/design/parts-and-cut-list.md</c> §1.3).
/// </para>
/// <para>
/// <see cref="Stock"/> is a <em>name</em>, not an id, and is never validated here or by the file
/// reader: a project drawn against a stock table a later build renames still opens, and the cut
/// list says the name did not resolve rather than guessing or dropping the row (§2.2).
/// </para>
/// </remarks>
/// <param name="Stock">The nominal name the materials library resolves — "2x4" — or <see langword="null"/> for a part whose dimensions are its own.</param>
/// <param name="Species">Free text, set in the properties panel after placing. Never interpreted by this build.</param>
/// <param name="Quantity">How many identical copies this one box stands for; at least 1.</param>
/// <param name="OutOfPlane">The value of the one dimension the plan cannot show; greater than zero.</param>
/// <param name="PlanAxes">Which of the three dimensions the box's stored width and height are.</param>
public sealed record Part(
    string? Stock,
    string? Species,
    int Quantity,
    Length OutOfPlane,
    PlanAxes PlanAxes)
{
    /// <inheritdoc cref="Part(string?, string?, int, Length, PlanAxes)"/>
    public int Quantity { get; init; } = Quantity >= 1
        ? Quantity
        : throw new ArgumentOutOfRangeException(
            nameof(Quantity),
            Quantity,
            "A part stands for at least one piece.");

    /// <inheritdoc cref="Part(string?, string?, int, Length, PlanAxes)"/>
    public Length OutOfPlane { get; init; } = OutOfPlane > Length.Zero
        ? OutOfPlane
        : throw new ArgumentOutOfRangeException(
            nameof(OutOfPlane),
            OutOfPlane.Units.ToString(CultureInfo.InvariantCulture),
            "A part's out-of-plane dimension must be greater than zero.");

    /// <summary>
    /// The three finished dimensions of this part on <paramref name="box"/>.
    /// </summary>
    /// <remarks>
    /// Reads the box's <em>stored</em> <see cref="Box.Width"/> and <see cref="Box.Height"/> and
    /// never the distance between its derived corners, so a part typed 10&#x2033; wide still
    /// measures 10&#x2033; after it is rotated 37&#xB0; (<c>CUT-002</c>, geometry model §2.3).
    /// <see cref="Box.Rotation"/> is not read at all.
    /// </remarks>
    /// <param name="box">The box this part is on.</param>
    public FinishedSize SizeOn(Box box)
    {
        ArgumentNullException.ThrowIfNull(box);

        return new FinishedSize(
            Value(PartDimension.Length, box),
            Value(PartDimension.Width, box),
            Value(PartDimension.Thickness, box));
    }

    private Length Value(PartDimension which, Box box)
        => which == PlanAxes.X ? box.Width
            : which == PlanAxes.Y ? box.Height
            : OutOfPlane;
}
