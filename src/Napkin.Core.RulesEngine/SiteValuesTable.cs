using System.Text.Json;

namespace Napkin.Core.RulesEngine;

/// <summary>One municipality's design values, as the adopted code prints them (#210).</summary>
/// <param name="Name">The municipality, as printed.</param>
/// <param name="UltimateWindSpeedMph">Ultimate design wind speed, Vult.</param>
/// <param name="NominalWindSpeedMph">Nominal design wind speed, Vasd; carried as printed, not used.</param>
/// <param name="GroundSnowLoadPsf">Ground snow load, pg.</param>
/// <param name="HurricaneProne">Whether the table marks it hurricane-prone; carried as printed, not used.</param>
/// <param name="Source">Where the row is printed.</param>
public sealed record MunicipalitySite(
    string Name,
    int UltimateWindSpeedMph,
    int NominalWindSpeedMph,
    int GroundSnowLoadPsf,
    bool HurricaneProne,
    SourceRef Source);

/// <summary>
/// A pack's per-municipality site values (<c>packs/&lt;id&gt;/site-values.json</c>, #210): what the
/// adopted code publishes town by town, and the statewide values it prints once. napkin offers them
/// with their citation; the person accepts or types their own, and nothing is ever filled silently.
/// </summary>
/// <param name="Title">The table's title as printed.</param>
/// <param name="SeismicDesignCategory">The statewide seismic design category, or null when the code prints none.</param>
/// <param name="SeismicSource">Where it is printed, or null with it.</param>
/// <param name="Municipalities">The towns, in the order printed.</param>
public sealed record SiteValuesTable(
    string Title,
    string? SeismicDesignCategory,
    SourceRef? SeismicSource,
    ValueList<MunicipalitySite> Municipalities)
{
    /// <summary>The file a pack's site values are read from, beside its <c>pack.json</c>.</summary>
    public const string FileName = "site-values.json";

    /// <summary>The kind the file declares.</summary>
    public const string Kind = "site-values";

    /// <summary>A town by name, ignoring case; null when the table has no such town.</summary>
    public MunicipalitySite? Find(string name)
        => Municipalities.FirstOrDefault(town => string.Equals(town.Name, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Reads <see cref="SiteValuesTable"/>: strict like every pack file — unknown fields, a missing citation, a duplicate town or a value below 1 are load problems.</summary>
internal static class SiteValuesReader
{
    public static SiteValuesTable? Read(
        IPackSource source, string file, IReadOnlyDictionary<string, SourceDocument> sources, ProblemList problems)
    {
        JsonObj? root = ManifestReader.OpenVersioned(source, file, problems, out JsonDocument? document);
        using (document)
        {
            if (root is null)
            {
                return null;
            }

            int before = problems.Count;
            string? kind = root.String("kind");
            if (kind is not null && kind != SiteValuesTable.Kind)
            {
                problems.Add(root.Where, $"kind: {SiteValuesTable.FileName} is '{SiteValuesTable.Kind}', not '{kind}'.");
            }

            root.String("notes", required: false);
            string? title = root.String("title");
            SourceDocument? doc = TableReader.ResolveSource(root.String("source"), sources, "source", root.Where, problems);

            string? seismic = null;
            string? seismicAt = null;
            if (root.Obj("statewide") is { } statewide)
            {
                if (statewide.Obj("seismicDesignCategory", required: false) is { } category)
                {
                    seismic = category.String("value");
                    seismicAt = category.String("location");
                    category.Done();
                }

                statewide.Done();
            }

            List<(string Name, int Vult, int Vasd, int Pg, bool Hurricane, string Location)> towns = [];
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<JsonElement>? rows = root.Array("municipalities", minItems: 1);
            for (int i = 0; rows is not null && i < rows.Count; i++)
            {
                string path = $"municipalities[{i}]";
                if (JsonObj.Create(rows[i], path, root.Where, problems) is not { } row)
                {
                    continue;
                }

                string? name = row.String("name");
                int? vult = row.Int("ultimateWindSpeedMph", min: 1);
                int? vasd = row.Int("nominalWindSpeedMph", min: 1);
                int? pg = row.Int("groundSnowLoadPsf", min: 1);
                bool? hurricane = Bool(row, "hurricaneProne");
                string? location = row.String("location");
                row.Done();

                if (name is not null && !names.Add(name))
                {
                    problems.Add(root.Where, $"{path}.name: '{name}' is listed twice.");
                }

                if (name is not null && vult is not null && vasd is not null && pg is not null && hurricane is not null && location is not null)
                {
                    towns.Add((name, vult.Value, vasd.Value, pg.Value, hurricane.Value, location));
                }
            }

            root.Done();
            if (problems.Count > before || title is null || doc is null)
            {
                return null;
            }

            return new SiteValuesTable(
                title,
                seismic,
                seismicAt is null ? null : TableTyper.SourceOf(doc, seismicAt),
                towns.Select(town => new MunicipalitySite(town.Name, town.Vult, town.Vasd, town.Pg, town.Hurricane, TableTyper.SourceOf(doc, town.Location))).ToValueList());
        }
    }

    static bool? Bool(JsonObj row, string name)
    {
        JsonElement? e = row.Get(name);
        switch (e?.ValueKind)
        {
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case null:
                return null;
            default:
                row.Problems.Add(row.Where, $"{row.Child(name)}: must be true or false.");
                return null;
        }
    }
}
