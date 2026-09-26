using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>What the window says about the standard views (#185), written out by hand.</summary>
public class StandardViewWordsTests
{
    [Fact]
    [Trait("Feature", "VIEW-012")]
    public void A_view_names_itself_in_its_hint_and_its_refusal()
    {
        Assert.Equal("Front view: read-only for now — pan, zoom and select; 1 for the plan or 7 for 3D to edit.", StandardViewWords.ReadOnlyHint(StandardView.Front));
        Assert.Equal("Left view: read-only for now — pan, zoom and select; 1 for the plan or 7 for 3D to edit.", StandardViewWords.ReadOnlyHint(StandardView.Left));
        Assert.Equal("Not in a Back view yet — 1 for the plan or 7 for 3D.", StandardViewWords.NotInView(StandardView.Back));
        Assert.Equal("Not in a Bottom view yet — 1 for the plan or 7 for 3D.", StandardViewWords.NotInView(StandardView.Bottom));
    }

    [Fact]
    [Trait("Feature", "VIEW-012")]
    public void Hidden_edges_say_where_they_are_drawn_and_whether_they_are_on()
    {
        Assert.Equal("Hidden edges are drawn in Bottom, Front, Back, Left and Right — 3 for Front.", StandardViewWords.HiddenEdgesElsewhere);
        Assert.Equal("Hidden edges: shown as light dashes.", StandardViewWords.HiddenEdgesShown);
        Assert.Equal("Hidden edges: not shown.", StandardViewWords.HiddenEdgesNotShown);
    }

    [Fact]
    [Trait("Feature", "VIEW-013")]
    public void The_sheet_says_what_it_shows_where_to_draw_and_how_it_is_arranged()
    {
        Assert.Equal("Sheet: Top above Front, Right beside it, 3D in the corner — pick a part in any of them; 1–7 or V for one view.", StandardViewWords.SheetHint);
        Assert.Equal("Not on the sheet — 1 for the plan or 7 for 3D.", StandardViewWords.NotOnSheet);
        Assert.Equal("Third-angle projection", StandardViewWords.ThirdAngle);
        Assert.Equal("Not in the Parts view — 1 for the plan or 7 for 3D.", StandardViewWords.NotInPartsView);
    }
}
