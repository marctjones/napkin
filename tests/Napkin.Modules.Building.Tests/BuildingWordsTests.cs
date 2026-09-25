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

    // #179: the fixed sentences the GUI workflows now reach through these formatters, pinned word for word here.
    [Fact]
    public void The_code_windows_notes_read_as_written()
    {
        Assert.Equal("ZZ BRACE B (IRC 2021, pack us-zz-brace rev 2)", CodeCheck.PackLabel(Code));
        Assert.Equal("Code check under ZZ BRACE B (IRC 2021, pack us-zz-brace rev 2)", CodeCheck.UnderHeading(Code));
        Assert.Equal("Code check", CodeCheck.UnderHeading(null));
        Assert.Equal("Locked on 2026-09-05 to pack us-zz-frame revision 1.", CodeCheck.LockedNote(new DateOnly(2026, 9, 5), "us-zz-frame", 1));
        Assert.Equal(
            "Following pack us-zz-frame: a newer revision is used when one is installed, and napkin says what changed.",
            CodeCheck.FollowingNote("us-zz-frame"));
        Assert.Equal(
            "CT 2022 has no header table loaded, so there is nothing to choose from yet (docs/rules-engine.md).",
            CodeCheck.NoHeaderTableTip("CT 2022"));
    }

    [Fact]
    public void A_headers_sentences_count_studs_in_the_singular_and_plural()
    {
        Assert.Equal("Header (1) 2x8, 1 jack stud and 1 king stud each side.", CodeCheck.HeaderText("(1) 2x8", 1, 1));
        Assert.Equal("Header (2) 2x10, 1 jack stud and 2 king studs each side.", CodeCheck.HeaderText("(2) 2x10", 1, 2));
        Assert.Equal("Header for Window 1 is now beyond Table ZZ-HEADER: get it engineered.", CodeCheck.NowBeyondText("Window 1", "ZZ-HEADER"));
        Assert.Equal("1 header piece (2x8)", FramingList.HeaderPieces(1, "2x8"));
        Assert.Equal("2 header pieces (2x10)", FramingList.HeaderPieces(2, "2x10"));
        Assert.Equal("1 header (not yet sized)", FramingList.HeaderPieces(1, null));
        Assert.Equal("2 headers (not yet sized)", FramingList.HeaderPieces(2, null));
    }

    [Fact]
    public void A_braced_lines_sentences_read_as_written()
    {
        Length required = Length.FeetInches(6, 6);
        Assert.Equal("Braced length 10'-0\" of 6'-6\" required: passes (ZZ-BRACE.1).", BracingCheck.PassesText(Length.Feet(10), required, "ZZ-BRACE.1"));
        Assert.Equal(
            "Braced length 5'-0\" of 6'-6\" required: SHORT by 1'-6\" (ZZ-BRACE.1).",
            BracingCheck.FailsText(Length.Feet(5), required, Length.FeetInches(1, 6), "ZZ-BRACE.1"));
        Assert.Equal(
            "Wall 1's braced line now passes, braced 8'-0\" of 6'-6\" required (ZZ-BRACE.1).",
            BracingCheck.NowPassesText("Wall 1", Length.Feet(8), required, "ZZ-BRACE.1"));
        Assert.Equal(
            "Wall 1's braced line is now SHORT by 1'-6\", braced 5'-0\" of 6'-6\" required (ZZ-BRACE.1).",
            BracingCheck.NowFailsText("Wall 1", Length.Feet(5), required, Length.FeetInches(1, 6), "ZZ-BRACE.1"));
        Assert.Equal("not braced", BracingCheck.NotBraced);
        Assert.Equal("zz-board (not in this code)", BracingCheck.NotInThisCode("zz-board"));
        Assert.Equal(
            "CT 2022 has no wall-bracing provisions loaded, so there is no method to choose (docs/rules-engine.md).",
            BracingCheck.NoProvisionsTip("CT 2022"));
    }
}
