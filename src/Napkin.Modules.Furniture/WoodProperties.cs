using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Napkin.Modules.Furniture;

/// <summary>
/// One species' clear-wood properties at 12 % moisture content, read from the Wood Handbook's
/// Table 5–3b (docs/design/furniture-checks.md §3, slice A).
/// </summary>
/// <param name="Name">The name the picker shows: "Oak, northern red".</param>
/// <param name="Group">"hardwood" or "softwood", as the table groups it.</param>
/// <param name="SpecificGravity">Ovendry weight over the volume at 12 % MC (the table's footnote b), exact as printed.</param>
/// <param name="ModulusOfElasticityPsi">Static bending E at 12 % MC in lbf/in², as printed (10⁶ lbf/in² × 1,000,000).</param>
/// <param name="Page">The page the row is printed on: "5–10".</param>
/// <param name="TableRow">The table's own row label, to find it again.</param>
public sealed record WoodSpecies(string Name, string Group, decimal SpecificGravity, long ModulusOfElasticityPsi, string Page, string TableRow);

/// <summary>
/// The cited species table the furniture checks read (<c>Data/wood-properties.json</c>). A species
/// is found only by its exact name: free text on a part ("red oak", "SPF") is never matched to a row,
/// so a check says "Input missing: species" rather than guessing.
/// </summary>
public sealed class WoodProperties
{
    WoodProperties(ImmutableArray<WoodSpecies> species, string source, string where)
    {
        Species = species;
        Source = source;
        Where = where;
    }

    /// <summary>Every row, in the table's order.</summary>
    public ImmutableArray<WoodSpecies> Species { get; }

    /// <summary>The source, as one citation line.</summary>
    public string Source { get; }

    /// <summary>Where in the source, with the footnotes that qualify the numbers.</summary>
    public string Where { get; }

    /// <summary>The table napkin ships, read once.</summary>
    public static WoodProperties Shipped { get; } = Load();

    /// <summary>The row whose name is exactly <paramref name="name"/> (ignoring case and surrounding spaces), or null.</summary>
    public WoodSpecies? Find(string? name)
    {
        string key = (name ?? string.Empty).Trim();
        return key.Length == 0 ? null : Species.FirstOrDefault(row => string.Equals(row.Name, key, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What the part panel says under a part's species: the row's cited values, or that typed text is
    /// kept as typed and never interpreted; nothing when the species is empty.
    /// </summary>
    public string Describe(string? species)
    {
        string typed = (species ?? string.Empty).Trim();
        if (typed.Length == 0)
        {
            return string.Empty;
        }

        return Find(typed) is { } row
            ? string.Create(CultureInfo.InvariantCulture, $"Specific gravity {row.SpecificGravity} and bending E {row.ModulusOfElasticityPsi:N0} lbf/in² at 12 % moisture, clear wood (Wood Handbook, Table 5–3b, p. {row.Page}).")
            : $"\"{typed}\" is not in napkin's species table: kept as typed and never interpreted, so a check that needs the species will say it is missing.";
    }

    static WoodProperties Load()
    {
        using Stream stream = typeof(WoodProperties).Assembly.GetManifestResourceStream("Napkin.Modules.Furniture.Data.wood-properties.json")
                              ?? throw new InvalidOperationException("The embedded wood-properties.json could not be opened: a bug in this build.");
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement root = document.RootElement;
        JsonElement citation = root.GetProperty("citation");
        string source = $"{citation.GetProperty("standard").GetString()}, {citation.GetProperty("publisher").GetString()} ({citation.GetProperty("designation").GetString()}), retrieved {citation.GetProperty("retrieved").GetString()}";
        ImmutableArray<WoodSpecies> species =
        [
            .. root.GetProperty("entries").EnumerateArray().Select(entry => new WoodSpecies(
                entry.GetProperty("name").GetString()!,
                entry.GetProperty("group").GetString()!,
                decimal.Parse(entry.GetProperty("specificGravity12").GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture),
                entry.GetProperty("modulusOfElasticity12Psi").GetInt64(),
                entry.GetProperty("page").GetString()!,
                entry.GetProperty("tableRow").GetString()!)),
        ];
        return new WoodProperties(species, source, citation.GetProperty("where").GetString()!);
    }
}
