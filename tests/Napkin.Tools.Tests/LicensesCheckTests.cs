using Napkin.Tools.Commands;
using Napkin.Tools.Licenses;

namespace Napkin.Tools.Tests;

/// <summary>
/// <c>licenses check</c> (#2): every restored package's declared license against the committed
/// allowlist, with a written exception the only way past it.
/// </summary>
public class LicensesCheckTests
{
    static readonly HashSet<string> Allowed = new(["MIT", "Apache-2.0", "BSD-3-Clause", "LGPL-2.1-only"], StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("MIT", true)]
    [InlineData("mit", true)]
    [InlineData("GPL-3.0-only", false)]
    [InlineData("MIT OR GPL-3.0-only", true)]
    [InlineData("GPL-3.0-only OR MIT", true)]
    [InlineData("MIT AND GPL-3.0-only", false)]
    [InlineData("MIT AND Apache-2.0", true)]
    [InlineData("(MIT OR GPL-2.0-only) AND BSD-3-Clause", true)]
    [InlineData("(GPL-2.0-only OR SSPL-1.0) AND MIT", false)]
    [InlineData("LGPL-2.1-only WITH Classpath-exception-2.0", true)]
    [InlineData("(MIT)", true)]
    [InlineData("", false)]
    [InlineData("MIT OR", false)]
    [InlineData("(MIT", false)]
    [InlineData("MIT)", false)]
    [InlineData("MIT WITH", false)]
    [InlineData("MIT WITH OR", false)]
    [InlineData("AND MIT", false)]
    [InlineData("MIT Apache-2.0", false)]
    public void An_expression_is_allowed_only_when_the_allowlist_satisfies_it(string expression, bool expected) =>
        Assert.Equal(expected, SpdxExpression.IsAllowed(expression, Allowed));

    [Theory]
    [InlineData("<package><metadata><license type=\"expression\">MIT</license></metadata></package>", DeclaredKind.Expression, "MIT")]
    [InlineData("<license type=\"file\">LICENSE.txt</license><licenseUrl>https://aka.ms/deprecateLicenseUrl</licenseUrl>", DeclaredKind.File, "LICENSE.txt")]
    [InlineData("<licenseUrl>https://example.org/license</licenseUrl>", DeclaredKind.Url, "https://example.org/license")]
    [InlineData("<licenseUrl> </licenseUrl>", DeclaredKind.None, "")]
    [InlineData("<license type=\"other\">x</license>", DeclaredKind.None, "")]
    [InlineData("<metadata></metadata>", DeclaredKind.None, "")]
    public void A_nuspec_declares_an_expression_a_file_a_url_or_nothing(string nuspec, DeclaredKind kind, string value) =>
        Assert.Equal((kind, value), LicenseScan.Parse(nuspec));

    [Fact]
    public void A_tree_whose_packages_are_all_allowed_passes_and_lists_each_verdict()
    {
        using var tree = Tree(("Good", "1.0.0", "<license type=\"expression\">MIT</license>"), ("Read", "2.0.0", "<license type=\"file\">LICENSE</license>"));

        var (code, output, error) = Run(tree.Path, "--list");

        Assert.Equal(ExitCode.Ok, code);
        Assert.Equal(string.Empty, error);
        Assert.Contains("Good 1.0.0: MIT — ok", output, StringComparison.Ordinal);
        Assert.Contains("Read 2.0.0: file LICENSE — exception (MIT, as read)", output, StringComparison.Ordinal);
        Assert.Contains("passed — 2 package(s), 1 by a written exception", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_package_with_no_license_or_one_outside_the_list_or_an_unread_url_fails_naming_each()
    {
        using var tree = Tree(
            ("Bare", "1.0.0", "<metadata/>"),
            ("Copyleft", "3.1.0", "<license type=\"expression\">GPL-3.0-only</license>"),
            ("Linked", "0.9.0", "<licenseUrl>https://example.org/l</licenseUrl>"),
            ("Read", "9.9.9", "<license type=\"file\">LICENSE</license>"));

        var (code, output, error) = Run(tree.Path, "--list");

        Assert.Equal(ExitCode.GateFailed, code);
        Assert.Contains("Bare 1.0.0 declares no license (used by src/App)", error, StringComparison.Ordinal);
        Assert.Contains("Copyleft 3.1.0 declares GPL-3.0-only, outside the allowlist", error, StringComparison.Ordinal);
        Assert.Contains("Linked 0.9.0 declares only url https://example.org/l, which a person must read", error, StringComparison.Ordinal);
        // An exception names one version; another version of the same package is a new read.
        Assert.Contains("Read 9.9.9 declares only file LICENSE", error, StringComparison.Ordinal);
        Assert.Contains("FAILED — 4 of 4 package(s)", error, StringComparison.Ordinal);
        Assert.Contains("Bare 1.0.0: none — REFUSED", output, StringComparison.Ordinal);
        Assert.Contains("the exception for Read 2.0.0 matches no restored package", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_package_missing_from_every_package_folder_counts_as_declaring_nothing()
    {
        using var tree = Tree(("Gone", "1.0.0", null));

        var (code, _, error) = Run(tree.Path);

        Assert.Equal(ExitCode.GateFailed, code);
        Assert.Contains("Gone 1.0.0 declares no license", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_project_reference_is_not_a_package_and_a_package_used_twice_is_listed_once_with_both_projects()
    {
        using var tree = Tree(("Good", "1.0.0", "<license type=\"expression\">MIT</license>"));
        string folder = Path.Combine(tree.Path, "packages");
        tree.Write("tests/AppTests/obj/project.assets.json", Assets(folder, ("Good", "1.0.0"), ("App", null)));

        var (code, output, _) = Run(tree.Path, "--list");

        Assert.Equal(ExitCode.Ok, code);
        Assert.Contains("passed — 1 package(s)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("App ", output, StringComparison.Ordinal);
        Assert.Equal(["src/App", "tests/AppTests"], LicenseScan.Of(tree.Path).Single().Projects);
    }

    [Fact]
    public void Worktrees_and_artifacts_outside_src_tests_and_tools_are_not_the_build()
    {
        using var tree = Tree(("Good", "1.0.0", "<license type=\"expression\">MIT</license>"));
        tree.Write(".claude/worktrees/x/src/App/obj/project.assets.json", Assets(Path.Combine(tree.Path, "packages"), ("Bad", "1.0.0")));

        Assert.Equal(ExitCode.Ok, Run(tree.Path).Code);
    }

    [Fact]
    public void With_nothing_restored_it_is_an_input_error_not_a_pass()
    {
        using var tree = Fixture.NewDirectory();
        tree.Write(LicensePolicy.RelativePath, Policy());
        Directory.CreateDirectory(Path.Combine(tree.Path, "src"));

        var (code, _, error) = Run(tree.Path);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains("run `dotnet restore napkin.sln` first", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, "no license policy at")]
    [InlineData("{ \"allowed\": [] }", "is not a license policy")]
    [InlineData("{ \"allowed\": [], \"exceptions\": [ { \"package\": \"A\", \"version\": \"1\", \"license\": \"MIT\", \"justification\": \" \" } ] }", "empty \"justification\"")]
    [InlineData("not json", "is not a license policy")]
    public void A_missing_or_malformed_policy_is_an_input_error(string? policy, string message)
    {
        using var tree = Fixture.NewDirectory();
        if (policy is not null)
        {
            tree.Write(LicensePolicy.RelativePath, policy);
        }

        var (code, _, error) = Run(tree.Path);

        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains(message, error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_assets_file_that_is_not_json_or_has_no_libraries_is_an_input_error()
    {
        using var broken = Fixture.NewDirectory();
        broken.Write(LicensePolicy.RelativePath, Policy());
        broken.Write("src/App/obj/project.assets.json", "{");
        Assert.Equal(ExitCode.InputError, Run(broken.Path).Code);

        using var empty = Fixture.NewDirectory();
        empty.Write(LicensePolicy.RelativePath, Policy());
        empty.Write("src/App/obj/project.assets.json", "{}");
        var (code, _, error) = Run(empty.Path);
        Assert.Equal(ExitCode.InputError, code);
        Assert.Contains("has no \"libraries\"", error, StringComparison.Ordinal);
    }

    [Fact]
    public void The_committed_policy_reads_and_every_exception_has_a_reason()
    {
        string root = RepoLayout.Discover(AppContext.BaseDirectory)!.Root;
        LicensePolicy policy = LicensePolicy.Load(Path.Combine(root, LicensePolicy.RelativePath));

        Assert.Contains("MIT", policy.Allowed);
        Assert.DoesNotContain("GPL-3.0-only", policy.Allowed);
        Assert.All(policy.Exceptions, exception => Assert.False(string.IsNullOrWhiteSpace(exception.Justification)));
    }

    [Fact]
    public void Help_prints_the_usage()
    {
        var output = new StringWriter();
        Assert.Equal(ExitCode.Ok, Cli.Run(["licenses", "check", "--help"], output, new StringWriter()));
        Assert.Contains("licenses check", output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>A repository with one project, src/App, restoring the given packages from a local folder.</summary>
    static Fixture.Scratch Tree(params (string Id, string Version, string? Nuspec)[] packages)
    {
        var tree = Fixture.NewDirectory();
        string folder = Path.Combine(tree.Path, "packages");
        foreach ((string id, string version, string? nuspec) in packages)
        {
            if (nuspec is not null)
            {
                tree.Write($"packages/{id.ToLowerInvariant()}/{version}/{id.ToLowerInvariant()}.nuspec", nuspec);
            }
        }

        tree.Write("src/App/obj/project.assets.json", Assets(folder, [.. packages.Select(package => (package.Id, (string?)package.Version))]));
        tree.Write(LicensePolicy.RelativePath, Policy());
        return tree;
    }

    static string Assets(string folder, params (string Id, string? Version)[] libraries)
    {
        IEnumerable<string> entries = libraries.Select(library => library.Version is { } version
            ? $"\"{library.Id}/{version}\": {{ \"type\": \"package\", \"path\": \"{library.Id.ToLowerInvariant()}/{version}\" }}"
            : $"\"{library.Id}/1.0.0\": {{ \"type\": \"project\", \"path\": \"../{library.Id}/{library.Id}.csproj\" }}");
        return $"{{ \"libraries\": {{ {string.Join(", ", entries)} }}, \"packageFolders\": {{ \"{folder.Replace("\\", "\\\\", StringComparison.Ordinal)}\": {{}} }} }}";
    }

    static string Policy() => """
        { "allowed": ["MIT", "Apache-2.0"],
          "exceptions": [ { "package": "Read", "version": "2.0.0", "license": "MIT, as read", "justification": "a person read the file" } ] }
        """;

    static (int Code, string Output, string Error) Run(string root, params string[] extra)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = Cli.Run(["licenses", "check", "--root", root, .. extra], output, error);
        return (code, output.ToString(), error.ToString());
    }
}
