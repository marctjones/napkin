using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>
/// One of the three finished dimensions a part has. The box's width and height are two of them and
/// its <see cref="Box.Depth"/> is the third.
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
/// The remaining name — the one neither axis claims — lies along local Z, and its value is the
/// box's <see cref="Box.Depth"/> (<c>docs/design/assembly-model.md</c> §1.2). Nothing is stored
/// twice: all three are stored parameters of one box, which is what keeps <c>CUT-002</c> true by
/// construction — there is no second copy of a dimension that could drift from the box's.
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

    /// <summary>The one name neither axis claims: the one along local Z, whose value is <see cref="Box.Depth"/>.</summary>
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
/// What a <see cref="Box"/> needs to be a piece somebody cuts: which of its three sizes are the
/// part's length, width and thickness, and which stock it comes from.
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
/// <param name="PlanAxes">Which of the three dimensions the box's stored width and height are; the third is its depth.</param>
public sealed record Part(
    string? Stock,
    string? Species,
    int Quantity,
    PlanAxes PlanAxes)
{
    /// <inheritdoc cref="Part(string?, string?, int, PlanAxes)"/>
    public int Quantity { get; init; } = Quantity >= 1
        ? Quantity
        : throw new ArgumentOutOfRangeException(
            nameof(Quantity),
            Quantity,
            "A part stands for at least one piece.");

    /// <summary>Counted hardware typed onto this part: slides, pulls, hinges (joinery note &#xA7;7.5). In the order typed.</summary>
    public ImmutableList<HardwareItem> Hardware { get; init; } = [];

    /// <summary>Equality by value, with the hardware compared as a sequence (an <see cref="ImmutableList{T}"/> compares by reference).</summary>
    public bool Equals(Part? other)
        => other is not null
           && Stock == other.Stock
           && Species == other.Species
           && Quantity == other.Quantity
           && PlanAxes == other.PlanAxes
           && Hardware.SequenceEqual(other.Hardware);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(Stock);
        hash.Add(Species);
        hash.Add(Quantity);
        hash.Add(PlanAxes);
        foreach (HardwareItem item in Hardware)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }

    /// <summary>
    /// The three finished dimensions of this part on <paramref name="box"/>.
    /// </summary>
    /// <remarks>
    /// Reads the box's <em>stored</em> <see cref="Box.Width"/>, <see cref="Box.Height"/> and
    /// <see cref="Box.Depth"/> by <see cref="PlanAxes"/>, and never the distance between its
    /// derived corners, so a part typed 10&#x2033; wide still measures 10&#x2033; after it is
    /// rotated 37&#xB0; (<c>CUT-002</c>, geometry model §2.3). Placement —
    /// <see cref="Box.Anchor"/>, <see cref="Box.FaceUp"/>, <see cref="Box.Rotation"/> — is not read
    /// at all, so a leg standing up and a leg lying down are the same leg
    /// (<c>docs/design/assembly-model.md</c> §5).
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
            : box.Depth;
}
