using System.Collections.Immutable;

namespace Napkin.Core.Geometry;

/// <summary>Which way a deck's joists run (<c>docs/design/deck-and-porch.md</c> §2.3). M11 builds <see cref="Out"/> only.</summary>
public enum JoistDirection
{
    /// <summary>Out from the house, perpendicular to the ledger.</summary>
    Out,
}

/// <summary>A built-up beam: how many plies of which lumber.</summary>
/// <param name="Plies">1 to 3.</param>
/// <param name="Lumber">The lumber as typed ("2x10"); not checked against the library, as a part's stock is not.</param>
public sealed record BeamSpec(int Plies, string Lumber);

/// <summary>A deck's guard, as the person typed it (§4.1). Every length is theirs; the code's numbers only come from a pack.</summary>
/// <param name="Height">The guard's height above the deck surface.</param>
/// <param name="PostSpacing">The most the posts may be apart.</param>
/// <param name="BalusterGap">The clear gap between balusters.</param>
/// <param name="BottomClearance">The gap under the bottom rail.</param>
/// <param name="Post">Post lumber.</param>
/// <param name="Rail">Rail lumber.</param>
/// <param name="Cap">Cap lumber.</param>
/// <param name="Baluster">Baluster lumber.</param>
public sealed record GuardInputs(
    Length Height, Length PostSpacing, Length BalusterGap, Length BottomClearance, string Post, string Rail, string Cap, string Baluster);

/// <summary>Which edge of a deck, by compass (§4.2).</summary>
public enum DeckEdge
{
    /// <summary>The north edge.</summary>
    North,

    /// <summary>The south edge.</summary>
    South,

    /// <summary>The east edge.</summary>
    East,

    /// <summary>The west edge.</summary>
    West,
}

/// <summary>A deck's stair, as the person typed it (§4.2).</summary>
/// <param name="Edge">Which edge it leaves from; the ledger's edge is refused at check time, not here.</param>
/// <param name="At">Where along that edge it starts, from the edge's west or south end.</param>
/// <param name="Width">How wide it is.</param>
/// <param name="Run">Each tread's run.</param>
/// <param name="Risers">How many risers, or null for napkin to work it out.</param>
/// <param name="Stringers">How many stringers, at least two.</param>
/// <param name="Stringer">Stringer lumber.</param>
/// <param name="TreadBoards">Boards per tread, at least one.</param>
public sealed record StairInputs(
    DeckEdge Edge, Length At, Length Width, Length Run, int? Risers, int Stringers, string Stringer, int TreadBoards);

/// <summary>
/// What the person entered for a box that is a deck (format version 13, <c>docs/design/deck-and-porch.md</c> §2.2,
/// §7). Nothing here is a code value; the frame is derived from it every time, and the checks read
/// the adopted code's pack.
/// </summary>
/// <param name="JoistDirection">Which way the joists run.</param>
/// <param name="JoistSpacing">Joist spacing on centre.</param>
/// <param name="Joist">Joist, ledger and rim lumber.</param>
/// <param name="Beam">The beam.</param>
/// <param name="Post">Post lumber.</param>
/// <param name="PostCount">How many posts carry the beam, at least two.</param>
/// <param name="Cantilever">How far the joists run past the beam, zero or more.</param>
/// <param name="Decking">Decking board as typed.</param>
/// <param name="DeckingGap">The gap between decking boards, zero or more.</param>
/// <param name="Blocking">Whether there is a row of blocking at mid-span.</param>
/// <param name="Supports">What the deck supports, as the pack's joist table names it; null until entered.</param>
/// <param name="Species">The species, as the pack's span tables name it; null until entered.</param>
/// <param name="FootingDepth">How deep the footings go below grade; null until entered.</param>
/// <param name="Guard">The guard, or null for none.</param>
/// <param name="Stair">The stair, or null for none.</param>
public sealed record DeckInputs(
    JoistDirection JoistDirection,
    Length JoistSpacing,
    string Joist,
    BeamSpec Beam,
    string Post,
    int PostCount,
    Length Cantilever,
    string Decking,
    Length DeckingGap,
    bool Blocking,
    string? Supports,
    string? Species,
    Length? FootingDepth,
    GuardInputs? Guard,
    StairInputs? Stair)
{
    /// <summary>Hardware lines typed for the deck, counted as a part's are.</summary>
    public ImmutableList<HardwareItem> Hardware { get; init; } = [];

    /// <inheritdoc/>
    public bool Equals(DeckInputs? other)
        => other is not null
           && JoistDirection == other.JoistDirection && JoistSpacing == other.JoistSpacing && Joist == other.Joist
           && Beam == other.Beam && Post == other.Post && PostCount == other.PostCount && Cantilever == other.Cantilever
           && Decking == other.Decking && DeckingGap == other.DeckingGap && Blocking == other.Blocking
           && Supports == other.Supports && Species == other.Species && FootingDepth == other.FootingDepth
           && Guard == other.Guard && Stair == other.Stair && Hardware.SequenceEqual(other.Hardware);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        hash.Add(JoistSpacing);
        hash.Add(Joist);
        hash.Add(Beam);
        hash.Add(PostCount);
        hash.Add(Decking);
        hash.Add(Supports);
        hash.Add(Guard);
        hash.Add(Stair);
        foreach (HardwareItem item in Hardware)
        {
            hash.Add(item);
        }

        return hash.ToHashCode();
    }
}

/// <summary>A roof's covering, as the person typed it from the package (§5.3).</summary>
/// <param name="Name">What it is.</param>
/// <param name="Coverage">Whole square feet one unit covers, or null when not typed.</param>
/// <param name="Waste">Waste allowance, a whole percent, zero or more.</param>
public sealed record Roofing(string Name, int? Coverage, int Waste);

/// <summary>What a shed roof's low end sits on (§5.3).</summary>
public abstract record RoofLowEnd;

/// <summary>A wall: the porch's front wall, by id.</summary>
/// <param name="Wall">The wall's box.</param>
public sealed record WallLowEnd(EntityId Wall) : RoofLowEnd;

/// <summary>A beam on posts: an open porch roof.</summary>
/// <param name="Beam">The beam.</param>
/// <param name="Post">Post lumber.</param>
/// <param name="PostCount">How many posts, at least two.</param>
public sealed record BeamLowEnd(BeamSpec Beam, string Post, int PostCount) : RoofLowEnd;

/// <summary>
/// What the person entered for a box that is a shed roof (format version 13, §5.3, §7). The rise is
/// the box's depth; the pitch is derived and never stored.
/// </summary>
/// <param name="RafterSpacing">Rafter spacing on centre.</param>
/// <param name="Rafter">Rafter lumber.</param>
/// <param name="Ledger">Ledger lumber.</param>
/// <param name="Overhang">The eave overhang, horizontal, zero or more.</param>
/// <param name="Blocking">Whether there is blocking at the plate.</param>
/// <param name="Sheathing">The sheathing panel as typed, or null.</param>
/// <param name="Roofing">The roofing.</param>
/// <param name="LowEnd">What the low end sits on.</param>
public sealed record RoofInputs(
    Length RafterSpacing, string Rafter, string Ledger, Length Overhang, bool Blocking, string? Sheathing, Roofing Roofing, RoofLowEnd LowEnd);

/// <summary>What fills an opening (§5.2): glass, a screen, or a solid door.</summary>
public enum OpeningFill
{
    /// <summary>Glazing: a window, or a glass door.</summary>
    Glass,

    /// <summary>Insect screen.</summary>
    Screen,

    /// <summary>A solid door or panel.</summary>
    Solid,
}
