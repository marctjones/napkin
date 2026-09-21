using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Napkin.Tools.Coverage;

/// <summary>
/// Reads coverlet's Cobertura reports and merges them per assembly.
/// </summary>
/// <remarks>
/// A solution-wide <c>dotnet test</c> writes one report per test project, and the VSTest data
/// collector additionally copies each one into its own attachment directory — so the same lines
/// show up several times over. Rates therefore cannot be averaged or summed; the merge is a union
/// keyed by (assembly, class, file, line), which is correct whether a line is reported once, twice
/// or by two different test projects that both exercise it.
/// </remarks>
public static class CoverageMerger
{
    /// <summary>The file coverlet writes; the name is fixed by the collector.</summary>
    public const string ReportFileName = "coverage.cobertura.xml";

    /// <summary>coverlet writes branch totals as `condition-coverage="25% (1/4)"`.</summary>
    private static readonly Regex ConditionCoverage =
        new(@"\((?<covered>\d+)/(?<total>\d+)\)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Every <see cref="ReportFileName"/> under <paramref name="directory"/>, sorted.</summary>
    public static IReadOnlyList<string> FindReports(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = Directory.GetFiles(directory, ReportFileName, SearchOption.AllDirectories);
        Array.Sort(found, StringComparer.Ordinal);
        return found;
    }

    /// <summary>
    /// Merges the given Cobertura reports into one tally per assembly, ordered by assembly name.
    /// </summary>
    /// <exception cref="InputException">A report is not readable as Cobertura XML.</exception>
    public static IReadOnlyList<AssemblyCoverage> Merge(IEnumerable<string> reportPaths)
    {
        // (assembly, class, file, line) -> the best observation of that line across all reports.
        var lines = new Dictionary<LineKey, LineTally>();
        var assemblies = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in reportPaths)
        {
            XDocument document;
            try
            {
                document = XDocument.Load(path);
            }
            catch (Exception exception) when (exception is System.Xml.XmlException or IOException)
            {
                throw new InputException($"{path}: not readable as a Cobertura report — {exception.Message}");
            }

            var coverage = document.Root
                ?? throw new InputException($"{path}: the Cobertura report is empty.");
            if (coverage.Name.LocalName != "coverage")
            {
                throw new InputException(
                    $"{path}: expected a <coverage> root element, found <{coverage.Name.LocalName}>.");
            }

            foreach (var package in coverage.Elements("packages").Elements("package"))
            {
                var assembly = (string?)package.Attribute("name");
                if (string.IsNullOrEmpty(assembly))
                {
                    continue;
                }

                // Packages with no classes are real information: the assembly was instrumented and
                // has no coverable lines. Record the name so it is reported N/A rather than missing.
                assemblies.Add(assembly);

                foreach (var type in package.Elements("classes").Elements("class"))
                {
                    var typeName = (string?)type.Attribute("name") ?? string.Empty;
                    var file = (string?)type.Attribute("filename") ?? string.Empty;

                    // The <class><lines> block already aggregates every method's lines, so reading
                    // it (and not <methods>) counts each line exactly once.
                    foreach (var line in type.Elements("lines").Elements("line"))
                    {
                        Accumulate(lines, assembly, typeName, file, line);
                    }
                }
            }
        }

        var totals = assemblies.ToDictionary(
            name => name,
            _ => new Totals(),
            StringComparer.Ordinal);

        foreach (var (key, tally) in lines)
        {
            var total = totals[key.Assembly];
            total.LinesCoverable++;
            if (tally.Hits > 0)
            {
                total.LinesCovered++;
            }

            total.BranchesTotal += tally.BranchesTotal;
            total.BranchesCovered += tally.BranchesCovered;
        }

        return totals
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new AssemblyCoverage(
                pair.Key,
                pair.Value.LinesCovered,
                pair.Value.LinesCoverable,
                pair.Value.BranchesCovered,
                pair.Value.BranchesTotal))
            .ToList();
    }

    private static void Accumulate(
        Dictionary<LineKey, LineTally> lines,
        string assembly,
        string typeName,
        string file,
        XElement line)
    {
        var numberText = (string?)line.Attribute("number");
        if (!int.TryParse(numberText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return;
        }

        _ = int.TryParse(
            (string?)line.Attribute("hits"),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var hits);

        var branchesCovered = 0;
        var branchesTotal = 0;
        var condition = (string?)line.Attribute("condition-coverage");
        if (condition is not null)
        {
            var match = ConditionCoverage.Match(condition);
            if (match.Success)
            {
                branchesCovered = int.Parse(match.Groups["covered"].Value, CultureInfo.InvariantCulture);
                branchesTotal = int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture);
            }
        }

        var key = new LineKey(assembly, typeName, file, number);
        if (lines.TryGetValue(key, out var existing))
        {
            // Union: a line is covered if any report saw it covered, and a branch outcome is taken
            // if any report saw it taken. Totals agree across reports, so max is a safe merge.
            lines[key] = new LineTally(
                Math.Max(existing.Hits, hits),
                Math.Max(existing.BranchesCovered, branchesCovered),
                Math.Max(existing.BranchesTotal, branchesTotal));
        }
        else
        {
            lines[key] = new LineTally(hits, branchesCovered, branchesTotal);
        }
    }

    private readonly record struct LineKey(string Assembly, string Type, string File, int Number);

    private readonly record struct LineTally(int Hits, int BranchesCovered, int BranchesTotal);

    private sealed class Totals
    {
        public int LinesCovered;
        public int LinesCoverable;
        public int BranchesCovered;
        public int BranchesTotal;
    }
}
