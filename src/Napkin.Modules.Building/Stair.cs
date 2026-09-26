using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building;

/// <summary>A deck's stair laid out (§4.2): its risers, rise each, treads, run, diagonal, the stringer board and the sentence to lay it out by.</summary>
/// <param name="Risers">How many risers.</param>
/// <param name="RiseEach">Each riser's rise, in 1/1024″, exact: the deck's height ÷ risers.</param>
/// <param name="Treads">Risers − 1.</param>
/// <param name="TotalRun">Treads × the tread run.</param>
/// <param name="Diagonal">The diagonal of the whole rise and run, by integer square root, rounded up once to the grid and then up to a sixteenth.</param>
/// <param name="Board">The stringer board: the diagonal plus one tread run, napkin's allowance for the end cuts.</param>
/// <param name="Layout">The plain-words layout sentence.</param>
/// <param name="Pieces">The stringers and the tread boards.</param>
public sealed record StairLayout(int Risers, ExactFraction RiseEach, int Treads, Length TotalRun, Length Diagonal, Length Board, string Layout, ImmutableArray<FramingPiece> Pieces);

/// <summary>
/// A deck's stair, napkin's layout (docs/design/deck-and-porch.md §4.2): risers typed or the fewest the
/// pack allows, the rise as an exact fraction, the diagonal by integer square root, and the stringer
/// board as the diagonal plus one tread, napkin's allowance, said.
/// </summary>
public static class StairFraming
{
    static readonly LengthFormat Sixteenths = new FeetInchesFormat(16);

    /// <summary>The stair's layout, or why there is none (null for a deck with no stair).</summary>
    public static (StairLayout? Layout, string? Problem) Of(DeckFraming framing, LoadedPack? pack, MaterialsLibrary library)
    {
        ArgumentNullException.ThrowIfNull(framing);
        ArgumentNullException.ThrowIfNull(library);
        DeckInputs inputs = framing.Deck.Box.Deck!;
        if (inputs.Stair is not { } stair)
        {
            return (null, null);
        }

        if (stair.Edge == framing.Ledger)
        {
            return (null, "The stair cannot be on the house side: choose another edge.");
        }

        if (!library.TryFindLumber(stair.Stringer, out LumberStock stringer) || !library.TryFindLumber(inputs.Decking, out LumberStock board))
        {
            return (null, $"{(library.TryFindLumber(stair.Stringer, out _) ? inputs.Decking : stair.Stringer)} is not in the materials library, so the stair cannot be laid out.");
        }

        Length rise = framing.Deck.Height;
        Length? maximum = pack?.Deck.GuardStair?.Stair?.MaximumRiser;
        int? risers = stair.Risers ?? (maximum is { } most ? (int)((rise.Units + most.Units - 1) / most.Units) : null);
        if (risers is not { } n)
        {
            return (null, "Type the riser count: the adopted code's pack gives no maximum riser to work it out from.");
        }

        ExactFraction each = new(rise.Units, n);
        int treads = n - 1;
        Length run = stair.Run * treads;
        Length diagonal = UpToSixteenth(new Length((long)CeilingRoot(((Int128)rise.Units * rise.Units) + ((Int128)run.Units * run.Units))));
        Length cut = diagonal + stair.Run;
        Length? stock = stringer.StandardLengths.Where(length => length >= cut).Cast<Length?>().Min();

        string riseText = Text(Nearest(each));
        string exactly = each.Denominator == 1 && Nearest(each).Units % 64 == 0 ? string.Empty : $" (≈, exactly {Text(rise)} ÷ {n})";
        string runText = Text(stair.Run);
        string boardWords = stock is { } bought ? $"so a {Text(bought)} board is enough" : $"and no stocked {stringer.Name} is that long";
        string layout = $"Lay out {n} risers of {riseText}{exactly} and {treads} treads of {runText} on a {stringer.Name} with the square at {riseText.TrimEnd('"')} and {runText.TrimEnd('"')}; "
                        + $"the diagonal of the whole rise and run is {Text(diagonal)}, {boardWords} (the diagonal plus one tread, napkin's allowance for the end cuts).";

        ImmutableArray<FramingPiece> pieces =
        [
            new(FramingRole.Stringer, stair.Stringers, cut, stringer),
            new(FramingRole.Tread, treads * stair.TreadBoards, stair.Width, board),
        ];
        return (new StairLayout(n, each, treads, run, diagonal, cut, layout, pieces), null);
    }

    /// <summary>The least whole number whose square is at least <paramref name="square"/>: an integer square root, rounded up.</summary>
    public static Int128 CeilingRoot(Int128 square)
    {
        if (square <= 0)
        {
            return 0;
        }

        // Newton's method from above, on whole numbers: converges to the floor of the root.
        Int128 x = square, y = (x + 1) / 2;
        while (y < x)
        {
            x = y;
            y = (x + (square / x)) / 2;
        }

        return x * x == square ? x : x + 1;
    }

    static Length UpToSixteenth(Length length) => new((length.Units + 63) / 64 * 64);

    static Length Nearest(ExactFraction units) => new((long)((units.Numerator + (units.Denominator / 2)) / units.Denominator));

    static string Text(Length length) => length.Format(Sixteenths).Text.TrimStart('≈');
}
