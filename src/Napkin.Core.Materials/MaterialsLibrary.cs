using System.Collections.Immutable;
using System.Reflection;

namespace Napkin.Core.Materials;

/// <summary>
/// The reference tables, loaded and ready to look something up in: what a "2x4" actually measures,
/// what a sheet of plywood is, and where each of those was read from.
/// </summary>
/// <remarks>
/// <para>
/// Built only by <see cref="MaterialsReader"/>, so there is no way to hold a library whose rows
/// did not come through the strict loader and no way to hold one whose rows are uncited.
/// </para>
/// <para>
/// Lookup is by the normalised nominal name (<see cref="NominalName"/>), which is unique across
/// every table: "2x4", "2 x 4" and "2×4" are one item, and two tables cannot both claim a name.
/// </para>
/// </remarks>
public sealed class MaterialsLibrary
{
    private readonly ImmutableDictionary<string, StockItem> _byKey;

    internal MaterialsLibrary(ImmutableArray<StockTable> tables, ImmutableArray<SpacingTable> spacingTables)
    {
        Tables = tables;
        SpacingTables = spacingTables;
        Items = [.. tables.SelectMany(table => table.Items)];
        SupportSpacings = [.. spacingTables.SelectMany(table => table.Spacings)];
        _byKey = Items.ToImmutableDictionary(item => item.Key, StringComparer.Ordinal);
    }

    /// <summary>The stock tables, in the order they were read.</summary>
    public ImmutableArray<StockTable> Tables { get; }

    /// <summary>The support-spacing tables, in the order they were read.</summary>
    public ImmutableArray<SpacingTable> SpacingTables { get; }

    /// <summary>Every stock item in every table, in the order they were read.</summary>
    public ImmutableArray<StockItem> Items { get; }

    /// <summary>
    /// Every centre-to-centre framing spacing the library carries, in the order they were read.
    /// </summary>
    /// <remarks>
    /// Not stock — you cannot buy a spacing — so it is deliberately not in <see cref="Items"/> and
    /// has no drawer in the picker. See <see cref="SupportSpacing"/> for where the numbers come
    /// from.
    /// </remarks>
    public ImmutableArray<SupportSpacing> SupportSpacings { get; }

    /// <summary>
    /// The spacings for one end use — "Wall" gives the stud spacings — shortest first.
    /// </summary>
    /// <param name="endUse">"Wall", "Roof", "Subfloor" or "Single Floor", ignoring case.</param>
    public ImmutableArray<SupportSpacing> SpacingsFor(string endUse)
        => [.. SupportSpacings
            .Where(spacing => string.Equals(spacing.EndUse, endUse, StringComparison.OrdinalIgnoreCase))
            .OrderBy(spacing => spacing.Spacing.Units)];

    /// <summary>
    /// The tables napkin ships, read from the data files embedded in this assembly.
    /// </summary>
    /// <remarks>
    /// A refusal here is a bug in the shipped data, not something a user did, so it is surfaced by
    /// a test (<c>MAT-005</c>) rather than by an error dialog. <see cref="Shipped"/> throws on one
    /// for the same reason.
    /// </remarks>
    public static MaterialsLoadResult LoadShipped()
    {
        Assembly assembly = typeof(MaterialsLibrary).Assembly;
        string[] names = [.. assembly.GetManifestResourceNames().Where(name => name.EndsWith(".json", StringComparison.Ordinal))];
        Array.Sort(names, StringComparer.Ordinal);

        return MaterialsReader.ReadAll(
        [
            .. names.Select(name => new MaterialsReader.NamedSource(
                ShortName(name),
                () => assembly.GetManifestResourceStream(name)
                      ?? throw new InvalidOperationException($"The embedded data file {name} could not be opened."))),
        ]);
    }

    /// <summary>The shipped tables, loaded once.</summary>
    /// <exception cref="InvalidOperationException">
    /// The shipped data files did not load, which is a bug in this build and not a user's doing.
    /// The message is the whole refusal, problem by problem.
    /// </exception>
    public static MaterialsLibrary Shipped => ShippedHolder.Instance;

    /// <summary>Finds a stock item by however a person spelled its name.</summary>
    /// <param name="name">"2x4", "2 x 4", "2×4" — anything that normalises to a name a table carries.</param>
    /// <param name="item">The item, when one was found.</param>
    public bool TryFind(string? name, out StockItem item)
    {
        string key = NominalName.Normalize(name);
        if (key.Length > 0 && _byKey.TryGetValue(key, out StockItem? found))
        {
            item = found;
            return true;
        }

        item = null!;
        return false;
    }

    /// <summary>
    /// Finds a stock item by name, but only inside one category — what the picker does once a
    /// drawer is open, and what a caller that will cast the result should use.
    /// </summary>
    /// <param name="category">The drawer to look in.</param>
    /// <param name="name">However the name was spelled.</param>
    /// <param name="item">The item, when one was found in that category.</param>
    public bool TryFind(StockCategory category, string? name, out StockItem item)
    {
        if (TryFind(name, out StockItem found) && found.Category == category)
        {
            item = found;
            return true;
        }

        item = null!;
        return false;
    }

    /// <summary>Finds a lumber item by name — a 2x4, a 1x6, a 6x6 timber, a 5/4x6 deck board.</summary>
    /// <param name="name">However the name was spelled.</param>
    /// <param name="lumber">The item, when one was found and it is lumber.</param>
    public bool TryFindLumber(string? name, out LumberStock lumber)
    {
        if (TryFind(name, out StockItem item) && item is LumberStock found)
        {
            lumber = found;
            return true;
        }

        lumber = null!;
        return false;
    }

    /// <summary>
    /// Everything in one category, in the order the tables list it — the second level of the
    /// picker, which is a text list and not a grid of icons (issue #7).
    /// </summary>
    /// <param name="category">The drawer.</param>
    public ImmutableArray<StockItem> InCategory(StockCategory category)
        => [.. Items.Where(item => item.Category == category)];

    /// <summary>The categories that actually hold something, in enum order.</summary>
    /// <remarks>
    /// The picker's top row is built from this rather than from the enum, so a category whose
    /// table has not been authored yet does not show an icon that opens an empty list.
    /// </remarks>
    public ImmutableArray<StockCategory> Categories
        => [.. Enum.GetValues<StockCategory>().Where(category => Items.Any(item => item.Category == category))];

    private static string ShortName(string resourceName)
    {
        const string marker = ".data.";
        int at = resourceName.IndexOf(marker, StringComparison.Ordinal);
        return at < 0 ? resourceName : resourceName[(at + marker.Length)..];
    }

    private static class ShippedHolder
    {
        public static readonly MaterialsLibrary Instance = LoadShipped() switch
        {
            MaterialsLoaded loaded => loaded.Library,
            MaterialsRefused refused => throw new InvalidOperationException(refused.Summary),
            var other => throw new InvalidOperationException($"Unknown load result {other.GetType().Name}."),
        };
    }
}
