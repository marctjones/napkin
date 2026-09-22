using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Napkin.App.Editing;
using Napkin.App.GuiTests.Harness;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// The one thing in the application that replaces a sketch.
/// </summary>
/// <remarks>
/// Every edit is a request, the design is replaced only when the updater accepts it, and one
/// gesture is announced once however many requests it took — which is the seam #11's undo stack
/// hangs on (design &#xA7;2.4: a stack of immutable sketch values, no inverse commands).
/// </remarks>
public class DesignEditorTests
{
    [Fact]
    [Trait("Feature", "CVS-005")]
    public void An_accepted_result_replaces_the_design_and_a_refused_one_does_not()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        Sketch before = editor.Sketch;

        editor.Apply(new Drag(EditingBuilder.Id(0), new Vector2(Length.Inches(5), Length.Zero)), "Moved Part 1");
        Assert.NotSame(before, editor.Sketch);
        Assert.Equal(Length.Inches(5).Units, editor.Sketch.Find<Box>(EditingBuilder.Id(0))!.Anchor.X.Units);

        Sketch moved = editor.Sketch;
        editor.Apply(new RemoveEntity(EntityId.New()), "Deleted something");

        Assert.Same(moved, editor.Sketch);
        Assert.Equal(EditSeverity.Problem, editor.LastMessage!.Severity);
    }

    [Fact]
    public void A_new_part_gets_a_name_so_nothing_on_screen_is_a_GUID()
    {
        DesignEditor editor = new();
        editor.Open(Design.Unlabelled("Untitled", Sketch.Empty));

        EntityId first = EntityId.New();
        editor.Apply(
            new AddEntity(new Box(first, LayerId.Default, Point2.Origin, Length.Inches(10), Length.Inches(10), Angle.Zero)),
            "Drew a part");

        EntityId second = EntityId.New();
        editor.Apply(
            new AddEntity(new Box(second, LayerId.Default, Point2.Inches(20, 0), Length.Inches(10), Length.Inches(10), Angle.Zero)),
            "Drew a part");

        Assert.Equal("Part 1", editor.NameOf(first));
        Assert.Equal("Part 2", editor.NameOf(second));
    }

    [Fact]
    public void Opening_a_file_with_no_names_in_it_numbers_what_it_has()
    {
        // A design whose entities carry no name — one built in code, or a file whose names are all
        // empty strings — would otherwise be listed by GUID, so the editor numbers what it has.
        // The sketch itself is untouched: the application may not build one (CVS-005), and a name
        // that is not in the file belongs in the design's own side table until a SetName request
        // puts it on the entity.
        DesignEditor editor = new();
        Sketch sketch = Sketch.Empty
            .WithEntity(new Box(EditingBuilder.Id(0), LayerId.Default, Point2.Origin, Length.Inches(10), Length.Inches(10), Angle.Zero))
            .WithEntity(new Box(EditingBuilder.Id(1), LayerId.Default, Point2.Inches(20, 0), Length.Inches(10), Length.Inches(10), Angle.Zero));

        editor.Open(Design.Unlabelled("From a file", sketch));

        Assert.Equal("Part 1", editor.NameOf(EditingBuilder.Id(0)));
        Assert.Equal("Part 2", editor.NameOf(EditingBuilder.Id(1)));
        Assert.Same(sketch, editor.Sketch);
    }

    [Fact]
    public void One_gesture_is_announced_once_however_many_requests_it_took()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));

        List<GestureCommitted> committed = [];
        editor.GestureCommitted += (_, gesture) => committed.Add(gesture);

        Design before = editor.Design;
        editor.BeginGesture("Moved Part 1");
        for (int i = 0; i < 20; i++)
        {
            editor.ApplyQuietly(new Drag(EditingBuilder.Id(0), new Vector2(Length.Inches(1), Length.Zero)));
        }

        Assert.Empty(committed);
        editor.EndGesture();

        GestureCommitted one = Assert.Single(committed);
        Assert.Equal("Moved Part 1", one.What);
        Assert.Same(before, one.Before);
        Assert.Same(editor.Design, one.After);

        // Both ends are whole, immutable designs, twenty pointer samples apart: that is the undo
        // entry #11 pushes, and there is nothing to invert.
        Assert.Equal(Length.Zero.Units, one.Before.Sketch.Find<Box>(EditingBuilder.Id(0))!.Anchor.X.Units);
        Assert.Equal(Length.Inches(20).Units, one.After.Sketch.Find<Box>(EditingBuilder.Id(0))!.Anchor.X.Units);
    }

    [Fact]
    public void A_gesture_that_changed_nothing_is_not_announced()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));

        List<GestureCommitted> committed = [];
        editor.GestureCommitted += (_, gesture) => committed.Add(gesture);

        editor.BeginGesture("Moved Part 1");
        editor.ApplyQuietly(new Drag(EditingBuilder.Id(0), Vector2.Zero));
        editor.EndGesture();

        Assert.Empty(committed);
    }

    [Fact]
    public void Deleting_a_part_takes_the_selection_with_it()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
        editor.Select(EditingBuilder.Id(0));

        Assert.Equal(EditingBuilder.Id(0), editor.OnlySelected);
        editor.Apply(new RemoveEntity(EditingBuilder.Id(0)), "Deleted Part 1");

        Assert.Empty(editor.Selection);
        Assert.Null(editor.OnlySelectedBox);
    }

    [Fact]
    public void Selecting_something_that_is_not_there_selects_nothing()
    {
        DesignEditor editor = new();
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));

        editor.Select(EntityId.New());
        Assert.Empty(editor.Selection);
    }

    [Fact]
    public void New_parts_go_on_the_layer_called_Parts_when_there_is_one()
    {
        DesignEditor editor = new();
        editor.Open(Napkin.App.Designs.NewSheet.Empty());

        LayerId chosen = editor.LayerForNewParts();
        Assert.Equal(
            "Parts",
            editor.Sketch.Layers.Single(layer => layer.Id == chosen).Name);

        editor.Open(Design.Unlabelled("Plain", Sketch.Empty));
        Assert.Equal(LayerId.Default, editor.LayerForNewParts());
    }

    [Fact]
    [Trait("Feature", "CVS-008")]
    public void An_UnderConstrained_result_is_carried_as_a_hint_not_an_error()
    {
        // The direct updater never returns this; a solver will. The canvas holds an
        // IGeometryUpdater and nothing else (design §4.3), so this is also the test that it
        // really does.
        DesignEditor editor = new(new AlwaysUnderConstrained());
        editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));

        UpdateResult result = editor.Apply(
            new Drag(EditingBuilder.Id(0), new Vector2(Length.Inches(1), Length.Zero)),
            "Moved Part 1");

        Assert.IsType<UnderConstrained>(result);
        Assert.Equal(EditSeverity.Hint, editor.LastMessage!.Severity);
        Assert.Null(editor.LastMessage.OfferToRemove);
    }

    /// <summary>An updater that applies nothing and calls every success under-constrained.</summary>
    sealed class AlwaysUnderConstrained : IGeometryUpdater
    {
        public ImmutableHashSet<Type> SupportedRelationships => DirectUpdater.Instance.SupportedRelationships;

        public UpdateResult Apply(Sketch sketch, Request request) => new UnderConstrained(
            sketch,
            ChangeSet.Empty,
            new FreedomReport([.. sketch.Entities.Keys], 2));
    }
}

/// <summary>
/// CVS-005's other half: no UI code path constructs a <see cref="Sketch"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every other test in this suite shows that an edit <em>does</em> go through the updater. This
/// one shows there is no way round it, by reading the application's own source: nothing under
/// <c>src/Napkin.App</c> may build a sketch or write an entity or a relationship into one, except
/// the blank sheet <em>File &#x2192; New</em> starts from, which is the empty sketch and a layer
/// to draw on.
/// </para>
/// <para>
/// A source scan rather than a reflection check, because what is being ruled out is a line of
/// code somebody might write, and the failure message has to name the file it is in.
/// </para>
/// </remarks>
public class NoSketchConstructionTests
{
    /// <summary>
    /// The ways a sketch can be built or written to. <c>Sketch.Empty</c> is not one of them: it is
    /// a value, and it is what a blank sheet starts from.
    /// </summary>
    static readonly string[] Forbidden =
    [
        "new Sketch(",
        ".WithEntity(",
        ".WithRelationship(",
        ".WithoutEntity(",
        ".WithoutRelationship(",
        ".WithLayer(",
    ];

    /// <summary>
    /// The one file allowed to build one: <em>File &#x2192; New</em>'s blank sheet. There is no
    /// <c>AddLayer</c> request in <c>Core.Geometry</c>, so a layer a new part can go on can only
    /// come from the sketch a design source produced.
    /// </summary>
    const string TheBlankSheet = "NewSheet.cs";

    [Fact]
    [Trait("Feature", "CVS-005")]
    public void No_code_in_the_application_builds_a_sketch()
    {
        string source = Path.Combine(RepositoryLayout.RepositoryRoot, "src", "Napkin.App");
        Assert.True(Directory.Exists(source), $"{source} is not there.");

        List<string> offences = [];
        foreach (string file in Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories))
        {
            if (Path.GetFileName(file) == TheBlankSheet || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (IsCommentOrDoc(line))
                {
                    continue;
                }

                foreach (string forbidden in Forbidden)
                {
                    if (line.Contains(forbidden, StringComparison.Ordinal))
                    {
                        offences.Add(
                            $"{Path.GetRelativePath(RepositoryLayout.RepositoryRoot, file)}:{i + 1}: {line.Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offences.Count == 0,
            "The application must change geometry only through IGeometryUpdater (CVS-005), but:"
            + Environment.NewLine
            + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void The_scan_would_notice_a_line_that_broke_the_rule()
    {
        // A guard on the guard: a scan that matched nothing would pass for ever.
        string[] lines =
        [
            "        Sketch worse = sketch.WithEntity(box with { Width = wider });",
            "        // sketch.WithEntity(...) would be the wrong way to do this",
        ];

        List<string> caught =
        [
            .. lines.Where(line => !IsCommentOrDoc(line))
                .Where(line => Forbidden.Any(forbidden => line.Contains(forbidden, StringComparison.Ordinal))),
        ];

        Assert.Single(caught);
    }

    static bool IsCommentOrDoc(string line)
    {
        string trimmed = line.TrimStart();
        return trimmed.StartsWith("//", StringComparison.Ordinal)
               || trimmed.StartsWith("///", StringComparison.Ordinal)
               || trimmed.StartsWith("*", StringComparison.Ordinal)
               || trimmed.StartsWith("/*", StringComparison.Ordinal)
               || Regex.IsMatch(trimmed, @"^<.*>");
    }
}
