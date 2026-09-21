using System.Xml.Linq;

namespace Napkin.Tools.Features;

/// <summary>
/// How a test method ended up. The order is the precedence used when folding a theory's cases
/// together: a failure outranks a pass, and a pass outranks a skip.
/// </summary>
public enum TestOutcome
{
    /// <summary>Skipped, filtered out, or never run at all.</summary>
    Skipped = 0,

    /// <summary>At least one case passed and none failed.</summary>
    Passed = 1,

    /// <summary>At least one case failed.</summary>
    Failed = 2,
}

/// <summary>
/// The outcomes read out of the TRX files a test run produced, keyed by `Type.Method`.
/// </summary>
/// <remarks>
/// A theory's cases share one method name, so their outcomes are folded together: any failure
/// makes the method failed, otherwise any pass makes it passed, otherwise it is skipped.
/// </remarks>
public sealed class TestResults
{
    private static readonly XNamespace Trx = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

    private readonly Dictionary<string, TestOutcome> outcomes = new(StringComparer.Ordinal);

    /// <summary>Every test method the run reported, with its folded outcome.</summary>
    public IReadOnlyDictionary<string, TestOutcome> Outcomes => outcomes;

    public int Count => outcomes.Count;

    /// <summary>Every `*.trx` under <paramref name="directory"/>, sorted.</summary>
    public static IReadOnlyList<string> FindTrxFiles(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.GetFiles(directory, "*.trx", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    /// <exception cref="InputException">A file is not readable as TRX.</exception>
    public static TestResults Load(IEnumerable<string> trxPaths)
    {
        var results = new TestResults();
        foreach (var path in trxPaths)
        {
            results.Add(path);
        }

        return results;
    }

    /// <summary>
    /// The outcome of a test method, or <see cref="TestOutcome.Skipped"/> when the run does not
    /// mention it — a test that did not run has not demonstrated anything.
    /// </summary>
    public TestOutcome OutcomeOf(string testId) =>
        outcomes.TryGetValue(testId, out var outcome) ? outcome : TestOutcome.Skipped;

    private void Add(string path)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or IOException)
        {
            throw new InputException($"{path}: not readable as a TRX file — {exception.Message}");
        }

        var root = document.Root ?? throw new InputException($"{path}: the TRX file is empty.");

        // Definitions carry the class and method names; results carry the outcome. They are
        // joined on the execution/test id.
        var byTestId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var unitTest in root.Descendants(Trx + "UnitTest"))
        {
            var id = (string?)unitTest.Attribute("id");
            var method = unitTest.Element(Trx + "TestMethod");
            var className = (string?)method?.Attribute("className");
            var name = (string?)method?.Attribute("name");
            if (id is null || string.IsNullOrEmpty(className) || string.IsNullOrEmpty(name))
            {
                continue;
            }

            byTestId[id] = $"{className}.{name}";
        }

        foreach (var result in root.Descendants(Trx + "UnitTestResult"))
        {
            var id = (string?)result.Attribute("testId");
            if (id is null || !byTestId.TryGetValue(id, out var testId))
            {
                continue;
            }

            Fold(testId, Translate((string?)result.Attribute("outcome")));
        }
    }

    private void Fold(string testId, TestOutcome outcome)
    {
        if (!outcomes.TryGetValue(testId, out var existing))
        {
            outcomes[testId] = outcome;
            return;
        }

        outcomes[testId] = (TestOutcome)Math.Max((int)existing, (int)outcome);
    }

    /// <summary>
    /// xunit's skipped facts come through as `NotExecuted`; anything that is neither that nor
    /// `Passed` — Failed, Timeout, Aborted, Error — counts as a failure.
    /// </summary>
    private static TestOutcome Translate(string? outcome) => outcome switch
    {
        "Passed" => TestOutcome.Passed,
        "NotExecuted" => TestOutcome.Skipped,
        _ => TestOutcome.Failed,
    };
}
