using Napkin.Core.Geometry;
using Napkin.Core.RulesEngine;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The sentences the window writes about walls and codes, composed here so they are tested without
/// a window: the stud spacing picker's words, and the status line after the adopted code changes.
/// </summary>
public class BuildingWordsTests
{
    static readonly AdoptedCodeRef Code = new("us-zz-brace", 2, "ZZ BRACE B", "IRC 2021", ReviewStatus.Unreviewed);

    static readonly HeaderResult Header = new HeaderResult.NoData(NoDataReason.NoPackSelected, null, WallKind.ExteriorBearing, "none");

    static readonly BracingResult Bracing = new BracingResult.NoData(BracingNoDataReason.NoPackSelected, null, "none");

    [Theory]
    [InlineData(16, 0, "16\" on centre")]
    [InlineData(24, 0, "24\" on centre")]
    [InlineData(19, 3, "19 3/16\" on centre")]
    public void A_spacing_reads_in_inches_on_centre_never_feet(long whole, long sixteenths, string expected) =>
        Assert.Equal(expected, FramingList.SpacingWords(Length.Inches(whole, sixteenths, 16)));

    [Fact]
    public void Nothing_changed_under_a_new_code_says_none_three_times()
    {
        string said = CodeCheck.SwitchSummary(Code, Headers(), Bracings());

        Assert.Equal(
            "Now checking against ZZ BRACE B (IRC 2021, pack us-zz-brace rev 2): every result recomputed; "
            + "none changed, none newly flagged, none can no longer be computed.",
            said);
    }

    [Fact]
    public void With_no_code_it_says_none_resolves()
    {
        Assert.StartsWith("No code resolves now: every result recomputed;", CodeCheck.SwitchSummary(null, Headers(), Bracings()));
    }

    [Fact]
    public void Headers_and_bracing_are_counted_together_and_citation_only_changes_are_not_changes()
    {
        string said = CodeCheck.SwitchSummary(
            Code,
            Headers(ChangeKind.SizedToSized, ChangeKind.SizedToOutOfScope, ChangeKind.NoAnswerToOutOfScope,
                ChangeKind.SizedToNoAnswer, ChangeKind.OutOfScopeToNoAnswer, ChangeKind.CitationOnly, ChangeKind.NoAnswerChanged),
            Bracings(BracingChangeKind.PassToFail, BracingChangeKind.ToNoAnswer, BracingChangeKind.CitationOnly, BracingChangeKind.NoAnswerChanged));

        // Headers: 5 of 7 are real changes; bracing: 2 of 4. Flagged: 2 headers + 1 bracing. Lost: 2 + 1.
        Assert.EndsWith("7 changed, 3 newly flagged, 3 can no longer be computed.", said);
    }

    [Fact]
    public void One_of_each_is_counted_as_one()
    {
        string said = CodeCheck.SwitchSummary(Code, Headers(ChangeKind.SizedToOutOfScope), Bracings(BracingChangeKind.ToNoAnswer));

        Assert.EndsWith("2 changed, 1 newly flagged, 1 can no longer be computed.", said);
    }

    static RecomputeReport Headers(params ChangeKind[] kinds) =>
        new(new ValueList<ResultChange>(kinds.Select(kind => new ResultChange(EntityId.New(), Header, Header, kind))), 0);

    static BracingRecomputeReport Bracings(params BracingChangeKind[] kinds) =>
        new(new ValueList<BracingChange>(kinds.Select(kind => new BracingChange(EntityId.New(), Bracing, Bracing, kind))), 0);
}
