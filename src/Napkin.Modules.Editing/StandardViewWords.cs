namespace Napkin.Modules.Editing;

/// <summary>
/// What the window says about the standard views (docs/design/standard-views.md §4.2, §5.4, §2.3), in
/// one place, so the sentence a person reads and the one a test checks are the same line (#185).
/// </summary>
public static class StandardViewWords
{
    /// <summary>What a view says on arriving: what can be done in it, and where to go to edit.</summary>
    public static string ReadOnlyHint(StandardView view) =>
        $"{StandardViewFrame.Name(view)} view: read-only for now — pan, zoom and select; 1 for the plan or 7 for 3D to edit.";

    /// <summary>Why a drawing tool does nothing in a view, and where it does something: the hint and the tool's tooltip.</summary>
    public static string NotInView(StandardView view) =>
        $"Not in a {StandardViewFrame.Name(view)} view yet — 1 for the plan or 7 for 3D.";

    /// <summary>What H says outside the views that draw hidden edges.</summary>
    public const string HiddenEdgesElsewhere = "Hidden edges are drawn in Bottom, Front, Back, Left and Right — 3 for Front.";

    /// <summary>What H or View ▸ Hidden edges says on turning them on.</summary>
    public const string HiddenEdgesShown = "Hidden edges: shown as light dashes.";

    /// <summary>What it says on turning them off.</summary>
    public const string HiddenEdgesNotShown = "Hidden edges: not shown.";

    /// <summary>What the sheet says on arriving (§11): what it shows, and how to get back to one view.</summary>
    public const string SheetHint = "Sheet: Top above Front, Right beside it, 3D in the corner — pick a part in any of them; 1–7 or V for one view.";

    /// <summary>Why a drawing tool does nothing on the sheet, and where it does something.</summary>
    public const string NotOnSheet = "Not on the sheet — 1 for the plan or 7 for 3D.";

    /// <summary>The sheet's note on how its views are arranged (§11.6).</summary>
    public const string ThirdAngle = "Third-angle projection";
}
