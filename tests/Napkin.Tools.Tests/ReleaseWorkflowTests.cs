namespace Napkin.Tools.Tests;

/// <summary>
/// The release workflow (issue #38) is checked as text: it must keep creating pre-releases that
/// are never "latest", and it must never gain a signing or notarization step (DESIGN.md §6.6,
/// §11). The rules live in <see cref="ReleaseWorkflowRules"/>; every rule is tested here against
/// a copy of the real workflow with the offending change made, so the check cannot quietly stop
/// checking.
/// </summary>
public class ReleaseWorkflowTests
{
    private static string Workflow() => ReleaseWorkflowRules.Read(ReleaseWorkflowRules.WorkflowPath);

    private static IReadOnlyList<string> Violations(string workflow) =>
        ReleaseWorkflowRules.Violations(workflow, ReleaseWorkflowRules.Read(ReleaseWorkflowRules.CiWorkflowPath));

    /// <summary>Replaces text that must be there, so a test never passes over an edit that changed nothing.</summary>
    private static string Mutate(string workflow, string from, string to)
    {
        Assert.Contains(from, workflow);
        return workflow.Replace(from, to);
    }

    /// <summary>Adds one step, as a shell command, at the end of the last job.</summary>
    private static string WithStep(string workflow, string command) =>
        workflow.TrimEnd('\n') + "\n      - run: " + command + "\n";

    private static void AssertViolation(IReadOnlyList<string> violations, string mentions) =>
        Assert.Contains(violations, violation => violation.Contains(mentions, StringComparison.Ordinal));

    [Fact]
    [Trait("Feature", "REL-002")]
    public void The_release_workflow_keeps_every_rule()
    {
        Assert.Empty(Violations(Workflow()));
    }

    [Fact]
    [Trait("Feature", "REL-002")]
    public void Removing_the_prerelease_flag_fails_the_check()
    {
        var violations = Violations(Mutate(Workflow(), "--prerelease", ""));

        AssertViolation(violations, "--prerelease");
    }

    [Fact]
    [Trait("Feature", "REL-002")]
    public void Removing_the_never_latest_flag_fails_the_check()
    {
        var violations = Violations(Mutate(Workflow(), "--latest=false", ""));

        AssertViolation(violations, "--latest=false");
    }

    [Fact]
    [Trait("Feature", "REL-002")]
    public void Marking_a_release_latest_or_stable_fails_the_check()
    {
        AssertViolation(Violations(Mutate(Workflow(), "--latest=false", "--latest=true")), "--latest");
        AssertViolation(Violations(Mutate(Workflow(), "--latest=false", "--latest")), "--latest");
        AssertViolation(Violations(WithStep(Workflow(), "gh release create v1 --prerelease=false --latest=false")), "--prerelease=false");
        AssertViolation(Violations(WithStep(Workflow(), "gh release edit v1 --prerelease=false")), "gh release edit");
    }

    [Fact]
    public void A_workflow_that_no_longer_creates_a_release_fails_the_check()
    {
        var violations = Violations(Mutate(Workflow(), "gh release create", "gh release view"));

        AssertViolation(violations, "nothing creates the release");
    }

    [Theory]
    [InlineData("codesign --force --sign \"Developer ID Application: Someone (ABCDE12345)\" out/Napkin.App")]
    [InlineData("codesign --force --sign=\"Developer ID Application: Someone\" out/Napkin.App")]
    [InlineData("codesign --sign 3F5A0C9B7E out/Napkin.App")]
    [InlineData("codesign -f -s \"Developer ID Application: Someone\" out/Napkin.App")]
    [InlineData("codesign -fs 3F5A0C9B7E out/Napkin.App")]
    [InlineData("xcrun notarytool submit napkin.zip --wait")]
    [InlineData("xcrun altool --notarize-app -f napkin.zip")]
    [InlineData("xcrun stapler staple napkin.dmg")]
    [InlineData("signtool sign /fd SHA256 Napkin.App.exe")]
    [InlineData("Set-AuthenticodeSignature Napkin.App.exe -Certificate $cert")]
    [InlineData("security import cert.p12 -k build.keychain")]
    [InlineData("security create-keychain -p x build.keychain")]
    [InlineData("codesign --remove-signature out/Napkin.App")]
    [InlineData("install_name_tool -add_rpath @loader_path out/Napkin.App")]
    public void A_signing_notarization_or_signature_stripping_step_fails_the_check(string command)
    {
        var violations = Violations(WithStep(Workflow(), command));

        Assert.NotEmpty(violations);
    }

    [Theory]
    [InlineData("codesign --force --sign - out/Napkin.App")]
    [InlineData("codesign --force --sign \"-\" out/Napkin.App")]
    [InlineData("codesign -f -s - out/Napkin.App")]
    [InlineData("codesign -dvv out/Napkin.App")]
    [InlineData("codesign --verify --strict out/Napkin.App")]
    public void Ad_hoc_signing_and_inspecting_a_signature_are_allowed(string command)
    {
        Assert.Empty(Violations(WithStep(Workflow(), command)));
    }

    [Fact]
    public void A_comment_may_mention_what_the_pipeline_does_not_do()
    {
        var withComment = Workflow()
            + "\n      # No codesign --sign \"Developer ID\", no notarytool, no signtool, no security import.\n";

        Assert.Empty(Violations(withComment));
    }

    [Fact]
    public void Dropping_the_ad_hoc_signature_assertion_fails_the_check()
    {
        AssertViolation(Violations(Mutate(Workflow(), "Signature=adhoc", "Signature=whatever")), "Signature=adhoc");
        AssertViolation(Violations(Mutate(Workflow(), "codesign -dvv", "codesign -d")), "codesign -dvv");
        AssertViolation(Violations(Mutate(Workflow(), "Authority=", "Author=")), "Authority=");
    }

    [Fact]
    public void Write_permission_anywhere_but_the_publishing_job_fails_the_check()
    {
        var onBuild = Mutate(
            Workflow(),
            "    timeout-minutes: 30\n",
            "    timeout-minutes: 30\n    permissions:\n      contents: write\n");
        AssertViolation(Violations(onBuild), "Job 'build'");

        var wholeWorkflow = Mutate(Workflow(), "permissions:\n  contents: read\n", "permissions:\n  contents: write\n");
        AssertViolation(Violations(wholeWorkflow), "default permissions");
    }

    [Fact]
    public void A_publishing_job_that_can_run_without_a_tag_fails_the_check()
    {
        var violations = Violations(
            Mutate(Workflow(), "if: needs.version.outputs.is_release == 'true'", "if: always()"));

        AssertViolation(violations, "pushed tag");
    }

    [Fact]
    public void Losing_a_trigger_or_widening_the_dry_run_fails_the_check()
    {
        AssertViolation(Violations(Mutate(Workflow(), "tags: ['v*']", "tags: ['*']")), "tag matching 'v*'");
        AssertViolation(Violations(Mutate(Workflow(), "  workflow_dispatch:\n", "")), "workflow_dispatch");
        AssertViolation(
            Violations(Mutate(Workflow(), "paths: ['.github/workflows/release.yml']", "paths: ['**']")),
            "pull_request must be limited");
    }

    [Fact]
    public void A_third_party_action_or_an_unpinned_one_fails_the_check()
    {
        AssertViolation(
            Violations(Mutate(Workflow(), "actions/upload-artifact@v4", "softprops/action-gh-release@v2")),
            "softprops/action-gh-release");
        AssertViolation(
            Violations(Mutate(Workflow(), "actions/download-artifact@v4", "actions/download-artifact@main")),
            "not pinned to a major version");
    }

    [Fact]
    public void An_action_at_a_different_major_version_than_ci_fails_the_check()
    {
        var violations = Violations(Mutate(Workflow(), "actions/checkout@v7", "actions/checkout@v3"));

        AssertViolation(violations, "same major version");
    }

    [Fact]
    public void Every_artifact_carries_the_licence_the_offer_of_source_and_the_first_run_page()
    {
        var workflow = Workflow();

        Assert.Contains("cp LICENSE ", workflow);
        Assert.Contains("SOURCE.txt", workflow);
        Assert.Contains("docs/first-run.md", workflow);
        Assert.Contains("GNU Affero General Public License", workflow);
    }

    [Fact]
    public void The_workflow_builds_the_three_runtime_identifiers_self_contained_and_unsigned_by_us()
    {
        var workflow = Workflow();

        foreach (var rid in new[] { "win-x64", "osx-arm64", "osx-x64" })
        {
            Assert.Contains($"rid: {rid}", workflow);
        }

        Assert.Contains("--self-contained true", workflow);
        Assert.Contains("-p:PublishSingleFile=true", workflow);
        Assert.Contains("napkin-$VERSION-$RID", workflow);
        Assert.Contains("SHA256SUMS.txt", workflow);
        Assert.Contains("v$VERSION", workflow);
    }
}
