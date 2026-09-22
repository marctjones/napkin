using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Napkin.App.Designs;
using Napkin.App.GuiTests.Harness;
using Napkin.App.Viewing;
using Napkin.Core.Geometry;
using Xunit;
using Design = Napkin.App.Designs.Design;

namespace Napkin.App.GuiTests.Unit;

/// <summary>
/// What a script or an accessibility client finds when it walks the real window: the canvas as an
/// element, one element per part inside it, and the menus and tool buttons named (issue #64).
/// </summary>
/// <remarks>
/// <para>
/// <strong>This is Avalonia's own peer tree, queried in process.</strong> That is one level of
/// proof: it is the exact tree both platform bridges publish — <c>Avalonia.Win32.Automation</c> to
/// Windows UI Automation and <c>Avalonia.Native</c>'s <c>IAvnAutomationPeer</c> to macOS
/// accessibility — and it is built by the shipped controls in a real, shown window. It is
/// <em>not</em> proof that a real screen reader or UIA client reads it correctly on a real desktop;
/// nothing headless can be. docs/testing/gui-automation.md says so in as many words.
/// </para>
/// <para>
/// These are not workflows: a peer tree is not something a person does with a mouse, so they claim
/// no feature id and cannot move the GUI ratchet.
/// </para>
/// </remarks>
public class CanvasAutomationTests
{
    [Fact]
    public void The_canvas_is_an_element_a_client_can_reach_and_name()
    {
        HeadlessWindow.Run(window =>
        {
            AutomationPeer root = ControlAutomationPeer.CreatePeerForElement(window);
            AutomationPeer canvas = Peer(window.Canvas);

            // Before this seam existed the canvas was a NoneAutomationPeer: IsControlElement false,
            // so a client walking the control view never saw it at all.
            Assert.IsType<CanvasAutomationPeer>(canvas);
            Assert.True(canvas.IsControlElement());
            Assert.Equal(AutomationControlType.Group, canvas.GetAutomationControlType());
            Assert.Equal("Drawing", canvas.GetName());
            Assert.Equal("DrawingCanvas", canvas.GetAutomationId());
            Assert.True(Reaches(root, canvas), "the canvas peer is not reachable from the window.");
        });
    }

    [Fact]
    public void Every_part_in_the_sketch_is_an_element_of_the_canvas()
    {
        HeadlessWindow.Run(window =>
        {
            Design design = EditingBuilder.Design(
                EditingBuilder.At(0, 0, 24, 12),
                EditingBuilder.At(30, 0, 6, 6),
                EditingBuilder.At(0, 20, 48, 2));
            window.Editor.Open(design);
            HeadlessWindow.Settle();

            IReadOnlyList<AutomationPeer> parts = Peer(window.Canvas).GetChildren();

            Assert.Equal(3, parts.Count);
            Assert.Equal(
                ["Part 1", "Part 2", "Part 3"],
                parts.Select(part => part.GetName()));

            // Ids are the entity ids in full, and distinct — an automation id is how a client says
            // "this part and not that one".
            Assert.Equal(
                [.. Enumerable.Range(0, 3).Select(i => EditingBuilder.Id(i).Value.ToString("D"))],
                parts.Select(part => part.GetAutomationId()));
            Assert.Equal(3, parts.Select(part => part.GetAutomationId()).Distinct().Count());

            foreach (AutomationPeer part in parts)
            {
                Assert.Same(Peer(window.Canvas), part.GetParent());
                Assert.True(part.IsControlElement());
                Assert.Equal(AutomationControlType.Custom, part.GetAutomationControlType());
                Assert.Equal("part", part.GetLocalizedControlType());
                Assert.Empty(part.GetChildren());
            }
        });
    }

    [Fact]
    public void A_part_reads_out_the_size_its_dimension_labels_show()
    {
        HeadlessWindow.Run(window =>
        {
            window.Editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 48, 24)));
            HeadlessWindow.Settle();

            AutomationPeer part = Assert.Single(Peer(window.Canvas).GetChildren());
            IValueProvider value = Assert.IsAssignableFrom<IValueProvider>(
                part.GetProvider<IValueProvider>());

            Assert.Equal("4'-0\" × 2'-0\"", value.Value);
            Assert.True(value.IsReadOnly);

            // A client cannot type a dimension through this: that gesture has its own field, its
            // own validation and its own refusal, and a silent no-op here would read as success.
            Assert.Throws<NotSupportedException>(() => value.SetValue("3'"));

            // The same length the canvas would draw beside it.
            Box box = window.Editor.Design.Box(0);
            Assert.Equal("4'-0\"", box.Width.Format(window.Canvas.LabelFormat).Text);
            Assert.Equal("2'-0\"", box.Height.Format(window.Canvas.LabelFormat).Text);
        });
    }

    [Fact]
    public void A_parts_rectangle_covers_the_pixels_it_is_drawn_in_and_follows_the_view()
    {
        HeadlessWindow.Run(window =>
        {
            window.Editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 48, 24)));
            HeadlessWindow.Settle();

            AutomationPeer canvas = Peer(window.Canvas);
            AutomationPeer part = Assert.Single(canvas.GetChildren());

            AssertClose(Drawn(window, canvas), part.GetBoundingRectangle());

            // The rectangle a Windows client is actually handed is
            // Peer.ToScreen(Peer.GetBoundingRectangle()), and ToScreenCore's default — the one a
            // peer deriving straight from AutomationPeer would inherit, because the hook is
            // private protected — returns null, which the Win32 bridge publishes as an empty
            // rectangle. This assertion is what holds PartAutomationPeer to owning a control.
            Assert.NotNull(part.ToScreen(part.GetBoundingRectangle()));

            // Panning moves the part's element with its pixels, because the rectangle is read from
            // the view transform every time rather than captured when the element was made.
            Rect before = part.GetBoundingRectangle();
            window.Canvas.PanByFraction(0.25, 0);
            HeadlessWindow.Settle();

            Rect after = part.GetBoundingRectangle();
            Assert.NotEqual(before.X, after.X);
            AssertClose(Drawn(window, canvas), after);

            window.Canvas.ZoomIn();
            HeadlessWindow.Settle();
            AssertClose(Drawn(window, canvas), part.GetBoundingRectangle());
        });
    }

    [Fact]
    public void Drawing_a_part_adds_an_element_and_deleting_one_takes_it_away()
    {
        HeadlessWindow.Run(window =>
        {
            window.Editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
            HeadlessWindow.Settle();

            AutomationPeer canvas = Peer(window.Canvas);
            int changes = 0;
            canvas.ChildrenChanged += (_, _) => changes++;

            Assert.Single(canvas.GetChildren());

            EntityId added = EntityId.New();
            window.Editor.Apply(
                new AddEntity(new Box(
                    added,
                    LayerId.Default,
                    Point2.Inches(40, 0),
                    Length.Inches(6),
                    Length.Inches(6),
                    Angle.Zero)),
                "Drew a part");
            HeadlessWindow.Settle();

            Assert.Equal(1, changes);
            Assert.Equal(2, canvas.GetChildren().Count);
            Assert.Contains(
                added.Value.ToString("D"),
                canvas.GetChildren().Select(part => part.GetAutomationId()));

            // A part that only moves is the same part: the tree does not change, because a name, a
            // value and a rectangle are all read live.
            window.Editor.Apply(
                new Drag(added, new Vector2(Length.Inches(3), Length.Zero)),
                "Moved a part");
            HeadlessWindow.Settle();
            Assert.Equal(1, changes);

            window.Editor.Apply(new RemoveEntity(added), "Deleted a part");
            HeadlessWindow.Settle();

            Assert.Equal(2, changes);
            Assert.Single(canvas.GetChildren());
        });
    }

    [Fact]
    public void Opening_a_different_design_replaces_the_elements()
    {
        HeadlessWindow.Run(window =>
        {
            AutomationPeer canvas = Peer(window.Canvas);
            int before = canvas.GetChildren().Count;

            window.Editor.Open(EditingBuilder.Design(EditingBuilder.At(0, 0, 24, 12)));
            HeadlessWindow.Settle();

            Assert.NotEqual(before, canvas.GetChildren().Count);
            Assert.Equal("Part 1", Assert.Single(canvas.GetChildren()).GetName());
        });
    }

    /// <summary>
    /// Avalonia already names the standard chrome from its <c>Header</c> and <c>Content</c> text,
    /// access-key underscore stripped, so no <c>AutomationProperties.Name</c> was added for it.
    /// This is the test that says so, and that fails if that ever stops being true.
    /// </summary>
    [Fact]
    public void Menu_items_and_tool_buttons_are_named_without_being_told_to()
    {
        HeadlessWindow.Run(window =>
        {
            AutomationPeer root = ControlAutomationPeer.CreatePeerForElement(window);

            Assert.Equal("File", Named(root, "FileMenu"));
            Assert.Equal("Draw", Named(root, "DrawMenu"));
            Assert.Equal("Samples", Named(root, "SamplesMenu"));
            Assert.Equal("View", Named(root, "ViewMenu"));
            Assert.Equal("Select", Named(root, "SelectToolButton"));
            Assert.Equal("Rectangle", Named(root, "RectangleToolButton"));
        });
    }

    /// <summary>
    /// A submenu's items are not in the tree until the menu is opened — they have no control to
    /// have a peer for before then. This says what a client really sees, rather than asserting a
    /// tree napkin does not build.
    /// </summary>
    [Fact]
    public void A_submenus_items_are_named_once_the_menu_is_open()
    {
        HeadlessWindow.Run(window =>
        {
            AutomationPeer root = ControlAutomationPeer.CreatePeerForElement(window);
            Assert.Null(Find(root, "ZoomToFitMenuItem", 0));

            MenuItem view = window.MenuBar.Items.OfType<MenuItem>().Single(
                item => item.Name == "ViewMenu");
            view.IsSubMenuOpen = true;
            HeadlessWindow.Settle();

            Assert.Equal("Zoom to Fit", Named(root, "ZoomToFitMenuItem"));
            Assert.Equal("Zoom In", Named(root, "ZoomInMenuItem"));
            Assert.Equal("Zoom Out", Named(root, "ZoomOutMenuItem"));
        });
    }

    /// <summary>
    /// The Samples menu's items are built in the code-behind, one per shipped scene file, with no
    /// <c>x:Name</c> and no automation id — so this is the one place where a name has to come from
    /// the <c>Header</c> the code set, and it does.
    /// </summary>
    [Fact]
    public void The_samples_built_in_code_are_named_too()
    {
        HeadlessWindow.Run(window =>
        {
            AutomationPeer root = ControlAutomationPeer.CreatePeerForElement(window);
            window.SamplesMenuItem.IsSubMenuOpen = true;
            HeadlessWindow.Settle();

            AutomationPeer samples = Find(root, "SamplesMenu", 0)
                ?? throw new InvalidOperationException("No Samples menu.");

            List<string?> named = [];
            Collect(samples, named, 0);

            Assert.NotEmpty(window.Samples);
            foreach (IDesignSource sample in window.Samples)
            {
                Assert.Contains(sample.Name, named);
            }
        });
    }

    static void Collect(AutomationPeer peer, List<string?> names, int depth)
    {
        if (peer.GetAutomationControlType() == AutomationControlType.MenuItem)
        {
            names.Add(peer.GetName());
        }

        if (depth > 25)
        {
            return;
        }

        foreach (AutomationPeer child in peer.GetChildren())
        {
            Collect(child, names, depth + 1);
        }
    }

    static AutomationPeer Peer(Control control) =>
        ControlAutomationPeer.CreatePeerForElement(control);

    /// <summary>Where the canvas would draw the one part of a one-part design.</summary>
    static Rect Drawn(MainWindow window, AutomationPeer canvas)
    {
        Box box = window.Editor.Design.Box(0);
        ViewTransform view = window.Canvas.View;
        Rect local = new Rect(
            view.ToScreen(box.Corner(BoxCorner.SouthWest)),
            view.ToScreen(box.Corner(BoxCorner.NorthEast))).Normalize();

        Rect frame = canvas.GetBoundingRectangle();
        return local.Translate(new Vector(frame.X, frame.Y));
    }

    static void AssertClose(Rect expected, Rect actual)
    {
        Assert.True(
            Math.Abs(expected.X - actual.X) < 0.01
            && Math.Abs(expected.Y - actual.Y) < 0.01
            && Math.Abs(expected.Width - actual.Width) < 0.01
            && Math.Abs(expected.Height - actual.Height) < 0.01,
            $"expected {expected}, got {actual}");
    }

    static string? Named(AutomationPeer root, string automationId) =>
        (Find(root, automationId, 0)
            ?? throw new InvalidOperationException($"No element with automation id {automationId}."))
        .GetName();

    static AutomationPeer? Find(AutomationPeer peer, string automationId, int depth)
    {
        if (peer.GetAutomationId() == automationId)
        {
            return peer;
        }

        return depth > 25
            ? null
            : peer.GetChildren()
                .Select(child => Find(child, automationId, depth + 1))
                .FirstOrDefault(found => found is not null);
    }

    static bool Reaches(AutomationPeer from, AutomationPeer target, int depth = 0) =>
        ReferenceEquals(from, target)
        || (depth <= 25 && from.GetChildren().Any(child => Reaches(child, target, depth + 1)));
}
