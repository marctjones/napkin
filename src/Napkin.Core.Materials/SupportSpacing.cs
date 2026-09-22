using System.Collections.Immutable;

using Napkin.Core.Geometry;

namespace Napkin.Core.Materials;

/// <summary>
/// One centre-to-centre spacing of framing members, for one end use — "Wall - 24" is studs at
/// 24 inches on centre.
/// </summary>
/// <remarks>
/// <para>
/// A spacing is not a stock item: you cannot buy one, it has no cross-section, and it belongs to
/// no drawer of the picker. It is carried here because the materials library is where a sourced
/// dimensional fact lives, and because both the cut list (how many studs in a wall) and the later
/// fastener schedule need it.
/// </para>
/// <para>
/// <strong>Where 16 and 24 come from here.</strong> Not from the IRC, whose tables napkin does not
/// transcribe yet — the copyright stance on that is Marc's (CLAUDE.md) and it blocks the code
/// packs. From PS 2-18 §4.1.3, which defines a panel's span rating as "an index number, based on
/// customary inch units, that identifies the recommended maximum center-to-center support spacing
/// for the specified end use under normal use conditions", and from the span ratings that
/// standard's own performance tables are written against. "Wall - 16" and "Wall - 24" are span
/// ratings in PS 2-18 Table 5; the spacing each one names is the stud spacing.
/// </para>
/// </remarks>
public sealed record SupportSpacing
{
    /// <summary>The span rating as the standard writes it — "Wall - 24", "Roof - 32".</summary>
    public required string Name { get; init; }

    /// <summary>The spelling this is keyed by (<see cref="NominalName.Normalize"/>).</summary>
    public required string Key { get; init; }

    /// <summary>What the members carry — "Wall", "Roof", "Subfloor", "Single Floor".</summary>
    public required string EndUse { get; init; }

    /// <summary>The centre-to-centre spacing of the supports.</summary>
    public required Length Spacing { get; init; }

    /// <summary>The source this row was read from.</summary>
    public required Citation Source { get; init; }

    /// <summary>How the number follows from the cited cell.</summary>
    public required string Derivation { get; init; }

    /// <summary>The spacing as a person says it: "24" o.c.".</summary>
    public string SpacingText => $"{Spacing.Format(StockItem.SizeFormat).Text} o.c.";
}

/// <summary>One data file's worth of support spacings.</summary>
/// <param name="Id">The table's stable id, unique across the library.</param>
/// <param name="Title">What the table is, for a person reading a sources list.</param>
/// <param name="Source">The citation it was authored from, and every row's default.</param>
/// <param name="File">The data file it was read from.</param>
/// <param name="Spacings">The rows, in the order the file wrote them. Never empty.</param>
public sealed record SpacingTable(
    string Id,
    string Title,
    Citation Source,
    string File,
    ImmutableArray<SupportSpacing> Spacings);
