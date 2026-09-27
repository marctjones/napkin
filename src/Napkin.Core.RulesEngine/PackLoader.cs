using System.Text.Json;
using System.Text.RegularExpressions;

namespace Napkin.Core.RulesEngine;

/// <summary>
/// Loads an adopted-code pack: reads <c>packs/&lt;id&gt;/pack.json</c>, its model-code base layer
/// under <c>layers/</c> and its overlays, composes them and validates the result. Strict and
/// total: a pack is valid entirely or not at all, and every problem is collected (design §9).
/// </summary>
public static partial class PackLoader
{
    /// <summary>The only pack file format version this build reads. No converters exist (beta policy).</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>The largest pack file the loader will read.</summary>
    public const long MaxFileBytes = 1_048_576;

    /// <summary>Loads a pack from a packs root folder on disk (the folder holding <c>layers/</c> and <c>packs/</c>).</summary>
    public static PackLoadResult Load(string packsRoot, string packId)
        => Load(new DirectoryPackSource(packsRoot), packId);

    /// <summary>Loads a pack from a source. Never throws for a bad pack; problems are values.</summary>
    public static PackLoadResult Load(IPackSource source, string packId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(packId);

        ProblemList problems = new();
        string manifestFile = $"packs/{packId}/pack.json";
        if (!PackIdPattern().IsMatch(packId))
        {
            problems.Add(new Where(manifestFile), $"'{packId}' is not a pack id (lower case: country-state-designation, e.g. us-ct-2022).");
            return Invalid(packId, problems);
        }

        PackManifest? manifest = ManifestReader.Read(source, manifestFile, problems);
        if (manifest is null)
        {
            return Invalid(packId, problems);
        }

        if (manifest.Id != packId)
        {
            problems.Add(new Where(manifestFile), $"id '{manifest.Id}' does not match its directory 'packs/{packId}'.");
        }

        Dictionary<string, RawTable> tables = new(StringComparer.Ordinal);
        BracingProvisions? bracing = LoadBaseLayer(source, manifest, manifestFile, tables, problems, out DeckProvisions deck);

        List<PendingAmendment> pending = [];
        foreach (string entry in manifest.Layers.Skip(1))
        {
            ApplyOverlayLayer(source, manifest, manifestFile, entry, tables, pending, problems);
        }

        List<HeaderSizingTable> typed = [];
        foreach (RawTable table in tables.Values.OrderBy(t => t.Designation, StringComparer.Ordinal))
        {
            HeaderSizingTable? result = TableTyper.Type(table, problems);
            if (result is not null)
            {
                typed.Add(result);
            }
        }

        foreach (IGrouping<WallKind, HeaderSizingTable> group in typed.GroupBy(t => t.WallKind).Where(g => g.Count() > 1))
        {
            problems.Add(
                new Where(manifestFile),
                $"tables {string.Join(" and ", group.Select(t => $"'{t.Designation}'"))} both declare wallKind '{Vocabulary.WallKindName(group.Key)}'; one table per wall kind.");
        }

        // Per-municipality site values (#210), when the pack carries them beside pack.json.
        string siteFile = $"packs/{packId}/{SiteValuesTable.FileName}";
        SiteValuesTable? site = source.FileLength(siteFile) is null
            ? null
            : SiteValuesReader.Read(source, siteFile, manifest.Sources.ToDictionary(s => s.Id, StringComparer.Ordinal), problems);

        // A frost line depth the pack's own document prints (deck-and-porch §3.4), beside pack.json.
        string frostFile = $"packs/{packId}/frost.json";
        FrostProvision? frost = source.FileLength(frostFile) is null
            ? null
            : DeckReader.ReadFrost(source, frostFile, manifest.Sources.ToDictionary(s => s.Id, StringComparer.Ordinal), problems);

        return problems.Count > 0
            ? Invalid(packId, problems)
            : new PackLoadResult.Loaded(new LoadedPack(manifest, typed.ToValueList(), pending.ToValueList(), bracing) { Site = site, Deck = deck, Frost = frost });
    }

    private static PackLoadResult.Invalid Invalid(string packId, ProblemList problems)
        => new(packId, problems.Items.ToValueList());

    /// <summary>Loads the base layer's tables into <paramref name="tables"/>, and returns its wall-bracing provisions, if any.</summary>
    private static BracingProvisions? LoadBaseLayer(
        IPackSource source, PackManifest manifest, string manifestFile, Dictionary<string, RawTable> tables, ProblemList problems, out DeckProvisions deck)
    {
        deck = DeckProvisions.None;
        string layerId = manifest.Layers[0];
        if (!LayerIdPattern().IsMatch(layerId))
        {
            problems.Add(new Where(manifestFile), $"layers[0]: '{layerId}' is not a base layer id (lower case letters, digits, '.', '-').");
            return null;
        }

        string layerFile = $"layers/{layerId}/layer.json";
        if (!source.DirectoryExists($"layers/{layerId}"))
        {
            problems.Add(new Where(manifestFile), $"layers[0]: base layer '{layerId}' does not resolve (no directory layers/{layerId}).");
            return null;
        }

        LayerManifest? layer = ManifestReader.ReadLayer(source, layerFile, problems);
        if (layer is null)
        {
            return null;
        }

        if (layer.Id != layerId)
        {
            problems.Add(new Where(layerFile), $"id '{layer.Id}' does not match its directory 'layers/{layerId}'.");
        }

        if (layer.Code != manifest.BaseCode)
        {
            problems.Add(new Where(layerFile), $"layer is {layer.Code} but pack '{manifest.Id}' declares baseCode {manifest.BaseCode}.");
        }

        Dictionary<string, SourceDocument> sources = layer.Sources.ToDictionary(s => s.Id, StringComparer.Ordinal);
        string tablesDir = $"layers/{layerId}/tables";
        foreach (string name in source.ListFiles(tablesDir).Where(IsJson))
        {
            string file = $"{tablesDir}/{name}";
            RawTable? table = TableReader.ReadTableFile(source, file, sources, problems);
            if (table is null)
            {
                continue;
            }

            if (!tables.TryAdd(table.Designation, table))
            {
                problems.Add(new Where(file, table.Designation), $"table '{table.Designation}' is also defined in {tables[table.Designation].Meta.File}.");
            }
        }

        // Wall bracing (docs/rules-engine.md): at most one provisions file per base layer. Overlays
        // cannot amend it yet; a pack with other provisions uses another base layer.
        string bracingDir = $"layers/{layerId}/bracing";
        List<string> bracingFiles = [.. source.ListFiles(bracingDir).Where(IsJson)];
        if (bracingFiles.Count > 1)
        {
            problems.Add(new Where(bracingDir), $"{bracingFiles.Count} files under bracing/; a base layer has at most one set of wall-bracing provisions.");
            return null;
        }

        deck = DeckReader.Read(source, $"layers/{layerId}/deck", sources, problems);
        return bracingFiles.Count == 1 ? BracingReader.Read(source, $"{bracingDir}/{bracingFiles[0]}", sources, problems) : null;
    }

    private static void ApplyOverlayLayer(
        IPackSource source, PackManifest manifest, string manifestFile, string entry, Dictionary<string, RawTable> tables,
        List<PendingAmendment> pending, ProblemList problems)
    {
        if (!OverlayEntryPattern().IsMatch(entry))
        {
            problems.Add(new Where(manifestFile), $"layers: '{entry}' is not an overlay directory ('amendments', or '<pack-id>/amendments' for another pack's overlay).");
            return;
        }

        int slash = entry.IndexOf('/');
        string ownerId = slash < 0 ? manifest.Id : entry[..slash];
        string directory = slash < 0 ? $"packs/{manifest.Id}/{entry}" : $"packs/{entry}";
        if (!source.DirectoryExists(directory))
        {
            problems.Add(new Where(manifestFile), $"layers: overlay '{entry}' does not resolve (no directory {directory}).");
            return;
        }

        PackManifest? owner = ownerId == manifest.Id
            ? manifest
            : ManifestReader.Read(source, $"packs/{ownerId}/pack.json", problems);
        if (owner is null)
        {
            return;
        }

        CitationLayer layer = ownerId == manifest.Id && manifest.Jurisdiction.Municipality is not null
            ? CitationLayer.MunicipalAmendment
            : CitationLayer.StateAmendment;
        Dictionary<string, SourceDocument> sources = owner.Sources.ToDictionary(s => s.Id, StringComparer.Ordinal);

        Dictionary<string, RawOverlay> overlays = new(StringComparer.Ordinal);
        foreach (string name in source.ListFiles(directory).Where(IsJson))
        {
            string file = $"{directory}/{name}";
            RawOverlay? overlay = OverlayReader.Read(source, file, sources, layer, problems);
            if (overlay is null)
            {
                continue;
            }

            if (!overlays.TryAdd(overlay.Table, overlay))
            {
                problems.Add(new Where(file, overlay.Table), $"a second overlay for table '{overlay.Table}' in this layer (the first is {overlays[overlay.Table].File}).");
            }
        }

        // "Not amended" must be a reviewed statement, not an omission (design §1.3): every table
        // present below needs an overlay file here, even one with no operations.
        foreach (string designation in tables.Keys.Where(d => !overlays.ContainsKey(d)).Order(StringComparer.Ordinal))
        {
            problems.Add(
                new Where(directory, designation),
                $"no overlay file for table '{designation}' in layer '{entry}'; add one (with an empty operations list if the adopted text does not amend it).");
        }

        foreach (RawOverlay overlay in overlays.Values.OrderBy(o => o.Table, StringComparer.Ordinal))
        {
            Composer.Apply(overlay, tables, pending, problems);
        }
    }

    private static bool IsJson(string name) => name.EndsWith(".json", StringComparison.Ordinal);

    [GeneratedRegex("^[a-z]{2}-[a-z]{2}-[a-z0-9-]+$")]
    internal static partial Regex PackIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*$")]
    private static partial Regex LayerIdPattern();

    [GeneratedRegex("^([a-z]{2}-[a-z]{2}-[a-z0-9-]+/)?[a-z0-9][a-z0-9-]*$")]
    private static partial Regex OverlayEntryPattern();
}

/// <summary>
/// Every pack under a packs root, loaded or invalid (design §9.3): the picker lists the loaded
/// ones; the invalid ones are reported, never silently dropped.
/// </summary>
public static class PackCatalog
{
    /// <summary>Loads every directory under <c>packs/</c>, in id order.</summary>
    public static ValueList<PackLoadResult> Discover(IPackSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.ListDirectories("packs").Select(id => PackLoader.Load(source, id)).ToValueList();
    }

    /// <summary>Loads every pack under a packs root on disk.</summary>
    public static ValueList<PackLoadResult> Discover(string packsRoot) => Discover(new DirectoryPackSource(packsRoot));
}

/// <summary>A base layer's <c>layer.json</c>: which model code it transcribes and from which documents.</summary>
internal sealed record LayerManifest(string Id, BaseCode Code, ValueList<SourceDocument> Sources);

/// <summary>A table before its rows are typed: rows stay raw until the composed input declaration is known.</summary>
internal sealed class RawTable(string designation, TableMeta meta, List<RawRow> rows)
{
    public string Designation { get; } = designation;

    public TableMeta Meta { get; set; } = meta;

    public List<RawRow> Rows { get; } = rows;
}

/// <summary>A table's metadata: everything but its rows.</summary>
internal sealed record TableMeta(
    string Title,
    WallKind WallKind,
    ValueList<InputColumn> Inputs,
    ValueList<Footnote> Footnotes,
    CitationLayer Layer,
    SourceDocument Document,
    string Location,
    string File);

/// <summary>A row before typing, with the layer and document that produced it.</summary>
internal sealed record RawRow(string Id, JsonElement Element, string File, string Path, CitationLayer Layer, SourceDocument Document);

/// <summary>One overlay file: the operations on one table.</summary>
internal sealed record RawOverlay(string File, string Table, IReadOnlyList<RawOperation> Operations);

/// <summary>An overlay operation (design §1.3): add / amend / delete, on a row or on a table; or amend one footnote.</summary>
internal sealed record RawOperation(
    OpKind Op,
    int Index,
    string? RowId,
    RawRow? Row,
    TableMeta? TableMeta,
    List<RawRow>? TableRows,
    Footnote? Footnote);
