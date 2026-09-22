using System.Text.RegularExpressions;

namespace Napkin.Tools.Tests;

/// <summary>
/// The first-run documentation (issue #27) has to exist, say what a homeowner needs, and stay
/// reachable: linked from the README and from every release's notes, with no dead links inside the
/// repository. Reachability of external addresses is a network question and is not asked here.
/// </summary>
public class FirstRunDocsTests
{
    private static string Read(string relative) => ReleaseWorkflowRules.Read(relative);

    [Fact]
    public void The_release_notes_link_the_first_run_page_and_carry_the_source_offer()
    {
        var notes = Read(".github/release-notes-template.md");

        Assert.Contains("{{SERVER}}/{{REPO}}/blob/{{TAG}}/docs/first-run.md", notes);
        Assert.Contains("{{SERVER}}/{{REPO}}/blob/{{TAG}}/LICENSE", notes);
        Assert.Contains("{{SERVER}}/{{REPO}}/archive/refs/tags/{{TAG}}.zip", notes);
        Assert.Contains("{{SHA}}", notes);
        Assert.Contains("GNU Affero General Public License v3.0", notes);
        Assert.Contains("pre-release", notes);
    }

    [Fact]
    public void The_first_run_page_covers_both_platforms_the_checksums_and_reporting_problems()
    {
        var page = Read("docs/first-run.md");

        // macOS 15 and later.
        Assert.Contains("Privacy & Security", page);
        Assert.Contains("Open Anyway", page);
        Assert.Contains("macOS 15", page);

        // Windows.
        Assert.Contains("More info", page);
        Assert.Contains("Run anyway", page);

        // Verifying a download, on each system.
        Assert.Contains("SHA256SUMS.txt", page);
        Assert.Contains("shasum -a 256", page);
        Assert.Contains("Get-FileHash", page);

        // Source, what beta means, and where to report.
        Assert.Contains("LICENSE", page);
        Assert.Contains("What \"beta\" means here", page);
        Assert.Contains("https://github.com/marctjones/napkin/issues", page);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("docs/first-run.md")]
    [InlineData("docs/release.md")]
    public void Every_relative_link_in_these_pages_points_at_a_file_that_exists(string page)
    {
        var layout = RepoLayout.Discover(AppContext.BaseDirectory)!;
        var directory = Path.GetDirectoryName(Path.Combine(layout.Root, page))!;

        var missing = new List<string>();
        foreach (Match link in Regex.Matches(Read(page), @"\]\(([^)\s]+)\)"))
        {
            var target = link.Groups[1].Value;
            if (target.StartsWith('#')
                || target.StartsWith("http://", StringComparison.Ordinal)
                || target.StartsWith("https://", StringComparison.Ordinal)
                || target.StartsWith("mailto:", StringComparison.Ordinal))
            {
                continue;
            }

            var path = target.Split('#')[0];
            var full = Path.GetFullPath(Path.Combine(directory, path));
            if (!File.Exists(full) && !Directory.Exists(full))
            {
                missing.Add($"{page}: {target}");
            }
        }

        Assert.Empty(missing);
    }
}
