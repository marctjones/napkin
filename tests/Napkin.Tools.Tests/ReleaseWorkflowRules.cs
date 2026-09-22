using System.Text;
using System.Text.RegularExpressions;

namespace Napkin.Tools.Tests;

/// <summary>
/// The rules the release workflow must keep (issue #38), applied to its YAML as plain text. No
/// YAML parser is involved: the rules are about a handful of strings that must be there and a
/// handful that must never be, and a text check cannot be talked out of them by a clever parse.
/// </summary>
/// <remarks>
/// Comment lines are dropped first, so the workflow can say in a comment what it does not do
/// ("no notarization here") without tripping its own check.
/// </remarks>
internal static class ReleaseWorkflowRules
{
    public const string WorkflowPath = ".github/workflows/release.yml";
    public const string CiWorkflowPath = ".github/workflows/ci.yml";

    /// <summary>Reads a repository file, with line endings normalised to LF.</summary>
    public static string Read(string relativePath)
    {
        var layout = RepoLayout.Discover(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException(
                $"{RepoLayout.RootMarker} not found above {AppContext.BaseDirectory}.");
        return File.ReadAllText(Path.Combine(layout.Root, relativePath)).Replace("\r\n", "\n");
    }

    /// <summary>Every rule the workflow breaks; empty when it keeps them all.</summary>
    /// <param name="workflow">The release workflow's text.</param>
    /// <param name="ciWorkflow">
    /// ci.yml's text. When given, an action the release workflow shares with it must be pinned to
    /// the same major version.
    /// </param>
    public static IReadOnlyList<string> Violations(string workflow, string? ciWorkflow = null)
    {
        var violations = new List<string>();
        var code = WithoutComments(workflow);

        // A backslash-continued shell command is one statement.
        var joined = Regex.Replace(code, @"\\\n[ \t]*", " ");

        CheckPreRelease(joined, violations);
        CheckNoSigning(joined, violations);
        CheckAdHocSignatureIsAsserted(code, violations);
        CheckPermissions(code, violations);
        CheckTriggers(code, violations);
        CheckActions(code, ciWorkflow is null ? null : WithoutComments(ciWorkflow), violations);
        return violations;
    }

    private static string WithoutComments(string yaml) =>
        string.Join("\n", yaml.Split('\n').Where(line => !line.TrimStart().StartsWith('#')));

    // -- pre-release, never latest ------------------------------------------------------------

    private static void CheckPreRelease(string joined, List<string> violations)
    {
        var creates = joined.Split('\n')
            .Where(statement => Regex.IsMatch(statement, @"\bgh\s+release\s+create\b"))
            .ToList();

        if (creates.Count == 0)
        {
            violations.Add("No `gh release create` statement: nothing creates the release.");
        }

        foreach (var statement in creates)
        {
            if (!Regex.IsMatch(statement, @"(?<![\w-])--prerelease(?![\w=-])"))
            {
                violations.Add("`gh release create` has no --prerelease: every napkin release is a pre-release (DESIGN.md §12).");
            }

            if (!Regex.IsMatch(statement, @"(?<![\w-])--latest=false(?![\w-])"))
            {
                violations.Add("`gh release create` has no --latest=false: no napkin release is ever marked latest (DESIGN.md §12).");
            }
        }

        if (Regex.IsMatch(joined, @"(?<![\w-])--prerelease=false"))
        {
            violations.Add("--prerelease=false appears: a release must not be presented as stable.");
        }

        if (Regex.IsMatch(joined, @"(?<![\w-])--latest(?!=false)(?![\w-])"))
        {
            violations.Add("--latest without =false appears: a release must not be marked latest.");
        }

        if (Regex.IsMatch(joined, @"\bgh\s+release\s+edit\b"))
        {
            violations.Add("`gh release edit` appears: a release is created as a pre-release and left alone.");
        }
    }

    // -- no signing, no notarization, nothing that strips the signature ------------------------

    private static readonly (string Pattern, string What)[] ForbiddenEverywhere =
    {
        (@"\bnotarytool\b", "notarytool: napkin does no notarization (DESIGN.md §6.6, §11)"),
        (@"\baltool\b", "altool: napkin does no notarization (DESIGN.md §6.6, §11)"),
        (@"\bstapler\b", "stapler: napkin does no notarization (DESIGN.md §6.6, §11)"),
        (@"\bsigntool\b", "signtool: napkin does no code signing (DESIGN.md §6.6, §11)"),
        (@"Set-AuthenticodeSignature", "Set-AuthenticodeSignature: napkin does no code signing (DESIGN.md §6.6, §11)"),
        (@"\bproductsign\b", "productsign: napkin does no code signing (DESIGN.md §6.6, §11)"),
        (@"\bsecurity\s+(?:import|create-keychain|unlock-keychain)\b", "security import: no signing identity is ever loaded (DESIGN.md §6.6, §11)"),
        (@"--remove-signature", "codesign --remove-signature: the arm64 ad-hoc signature must not be stripped (DESIGN.md §6.6)"),
        (@"\binstall_name_tool\b", "install_name_tool: editing a Mach-O invalidates its signature (DESIGN.md §6.6)"),
    };

    private static void CheckNoSigning(string joined, List<string> violations)
    {
        foreach (var (pattern, what) in ForbiddenEverywhere)
        {
            if (Regex.IsMatch(joined, pattern, RegexOptions.IgnoreCase))
            {
                violations.Add($"Forbidden: {what}.");
            }
        }

        // Signing with anything but the ad-hoc identity "-". `--sign` is checked anywhere;
        // the short `-s` only within a codesign command, where it means the same thing.
        foreach (Match match in IdentityFlag(@"--sign(?:=|\s+)").Matches(joined))
        {
            ReportIdentity(match.Groups[2].Value, violations);
        }

        foreach (var statement in Regex.Split(joined, @"[\n|;&]+"))
        {
            if (!Regex.IsMatch(statement, @"\bcodesign\b"))
            {
                continue;
            }

            foreach (Match match in IdentityFlag(@"-[A-Za-z]*s\s+").Matches(statement))
            {
                ReportIdentity(match.Groups[2].Value, violations);
            }
        }
    }

    private static Regex IdentityFlag(string flag) =>
        new(@"(?<![\w-])" + flag + @"([""']?)([^\s""']*)\1");

    private static void ReportIdentity(string identity, List<string> violations)
    {
        if (identity != "-")
        {
            violations.Add(
                $"codesign with identity '{identity}': only the ad-hoc identity '-' is allowed; napkin does no Developer ID signing (DESIGN.md §6.6).");
        }
    }

    private static void CheckAdHocSignatureIsAsserted(string code, List<string> violations)
    {
        if (!Regex.IsMatch(code, @"codesign\s+-dvv"))
        {
            violations.Add("The macOS build no longer inspects the binary with `codesign -dvv` (REL-003).");
        }

        if (!code.Contains("Signature=adhoc", StringComparison.Ordinal))
        {
            violations.Add("The macOS build no longer asserts `Signature=adhoc` on the arm64 binary (REL-003).");
        }

        if (!code.Contains("Authority=", StringComparison.Ordinal))
        {
            violations.Add("The macOS build no longer asserts there is no `Authority=` (Developer ID) on the binaries (REL-003).");
        }
    }

    // -- permissions --------------------------------------------------------------------------

    private static void CheckPermissions(string code, List<string> violations)
    {
        var jobsAt = Regex.Match(code, @"(?m)^jobs:\s*$");
        if (!jobsAt.Success)
        {
            violations.Add("No `jobs:` section found.");
            return;
        }

        var beforeJobs = code[..jobsAt.Index];
        if (!Regex.IsMatch(beforeJobs, @"(?m)^permissions:\s*\n\s+contents:\s*read\s*$"))
        {
            violations.Add("The workflow's default permissions are not exactly `contents: read`.");
        }

        if (HasWrite(beforeJobs))
        {
            violations.Add("Write permission is granted for the whole workflow; only the publishing job may have it.");
        }

        var jobs = Jobs(code);
        var publishing = jobs
            .Where(job => Regex.IsMatch(job.Value, @"\bgh\s+release\s+create\b"))
            .Select(job => job.Key)
            .ToList();

        foreach (var (name, text) in jobs)
        {
            var publishes = publishing.Contains(name);
            if (HasWrite(text) && !publishes)
            {
                violations.Add($"Job '{name}' has write permission but is not the publishing job.");
            }

            if (publishes)
            {
                var condition = Regex.Match(text, @"(?m)^    if:\s*(.+)$");
                if (!condition.Success
                    || !(condition.Groups[1].Value.Contains("is_release", StringComparison.Ordinal)
                        || condition.Groups[1].Value.Contains("refs/tags/", StringComparison.Ordinal)))
                {
                    violations.Add(
                        $"Publishing job '{name}' can run on something other than a pushed tag: its `if:` must test is_release or refs/tags/.");
                }
            }
        }
    }

    private static bool HasWrite(string text) =>
        Regex.IsMatch(text, @"contents:\s*write\b") || Regex.IsMatch(text, @"permissions:\s*write-all\b");

    /// <summary>The text of each job, keyed by its id. A job header is a two-space-indented key under `jobs:`.</summary>
    private static Dictionary<string, string> Jobs(string code)
    {
        var jobs = new Dictionary<string, string>();
        string? current = null;
        var buffer = new StringBuilder();
        var inJobs = false;

        void Flush()
        {
            if (current is not null)
            {
                jobs[current] = buffer.ToString();
            }

            buffer.Clear();
        }

        foreach (var line in code.Split('\n'))
        {
            if (!inJobs)
            {
                inJobs = Regex.IsMatch(line, @"^jobs:\s*$");
                continue;
            }

            var header = Regex.Match(line, @"^  ([A-Za-z0-9_-]+):\s*$");
            if (header.Success)
            {
                Flush();
                current = header.Groups[1].Value;
                continue;
            }

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
            {
                Flush();
                current = null;
                continue;
            }

            buffer.Append(line).Append('\n');
        }

        Flush();
        return jobs;
    }

    // -- triggers -----------------------------------------------------------------------------

    private static void CheckTriggers(string code, List<string> violations)
    {
        var jobsAt = Regex.Match(code, @"(?m)^jobs:\s*$");
        var head = jobsAt.Success ? code[..jobsAt.Index] : code;

        if (!Regex.IsMatch(head, @"tags:\s*\[[^\]]*['""]v\*['""][^\]]*\]"))
        {
            violations.Add("The workflow is not triggered by a pushed tag matching 'v*'.");
        }

        if (!Regex.IsMatch(head, @"(?m)^  workflow_dispatch:"))
        {
            violations.Add("No workflow_dispatch trigger: there is no way to rehearse a release by hand.");
        }

        // A one-element flow list on purpose: it is what keeps the pull-request dry run from
        // firing on unrelated changes.
        if (!Regex.IsMatch(
                head,
                @"(?m)^  pull_request:\s*\n\s+paths:\s*\[\s*['""]\.github/workflows/release\.yml['""]\s*\]\s*$"))
        {
            violations.Add(
                "pull_request must be limited to `paths: ['.github/workflows/release.yml']` (a one-element list), so only a change to this workflow dry-runs it.");
        }
    }

    // -- actions ------------------------------------------------------------------------------

    private static void CheckActions(string code, string? ci, List<string> violations)
    {
        var used = Actions(code);
        foreach (var (action, version) in used)
        {
            if (!Regex.IsMatch(action, @"^actions/[A-Za-z0-9._-]+$"))
            {
                violations.Add($"Action '{action}' is not one of GitHub's own (actions/*): no third-party actions.");
            }

            if (!Regex.IsMatch(version, @"^v\d+$"))
            {
                violations.Add($"Action '{action}@{version}' is not pinned to a major version like v4.");
            }
        }

        if (ci is null)
        {
            return;
        }

        var inCi = Actions(ci);
        foreach (var (action, version) in used)
        {
            if (inCi.TryGetValue(action, out var ciVersion) && ciVersion != version)
            {
                violations.Add($"Action '{action}' is @{version} here but @{ciVersion} in ci.yml: use the same major version.");
            }
        }
    }

    private static Dictionary<string, string> Actions(string code)
    {
        var actions = new Dictionary<string, string>();
        foreach (Match match in Regex.Matches(code, @"(?m)^\s*(?:-\s+)?uses:\s*([^\s@]+)@(\S+)\s*$"))
        {
            actions[match.Groups[1].Value] = match.Groups[2].Value;
        }

        return actions;
    }
}
