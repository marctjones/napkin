using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Napkin.Core.Geometry;

namespace Napkin.App.Viewing;

/// <summary>
/// What a script or an accessibility client sees when it looks at the drawing: the canvas, and one
/// element inside it for every part in the sketch.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why this exists.</strong> <see cref="CanvasView"/> draws every part itself, so without a
/// peer the whole design is one opaque rectangle — in fact worse than opaque, because
/// <see cref="Control"/>'s default peer is a <see cref="NoneAutomationPeer"/>, which reports
/// <c>IsControlElement == false</c> and so does not appear in a client's control view at all. The
/// menus and the tool buttons are standard controls and Avalonia already names them; the drawing is
/// where the design actually lives, and it needed this seam (issue #64).
/// </para>
/// <para>
/// <strong>It reads the sketch, it does not copy it.</strong> Every child asks
/// <see cref="CanvasView"/> for the same box, the same rectangle and the same formatted lengths that
/// <see cref="CanvasView.Render"/> draws, every time it is asked. Nothing here is a second store of
/// the design that could drift from the one on screen.
/// </para>
/// <para>
/// <strong>Children follow the sketch.</strong> The canvas raises
/// <see cref="CanvasView.PartsChanged"/> when the set of parts changes — a part drawn, a part
/// deleted, a different design opened — and this peer invalidates its children, which raises
/// <c>ChildrenChanged</c> to whoever is listening. A part that merely moves or is resized does not
/// change the tree: its name, value and rectangle are read live, so nothing needs invalidating.
/// </para>
/// </remarks>
public sealed class CanvasAutomationPeer : ControlAutomationPeer
{
    readonly Dictionary<EntityId, PartAutomationPeer> _parts = [];

    /// <inheritdoc cref="CanvasAutomationPeer"/>
    /// <param name="owner">The canvas this peer speaks for.</param>
    public CanvasAutomationPeer(CanvasView owner)
        : base(owner) => owner.PartsChanged += OnPartsChanged;

    /// <summary>The canvas, typed.</summary>
    public new CanvasView Owner => (CanvasView)base.Owner;

    /// <inheritdoc/>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
    {
        List<AutomationPeer> children = [];
        HashSet<EntityId> present = [];

        foreach (EntityId part in Owner.PartsInOrder())
        {
            present.Add(part);
            if (!_parts.TryGetValue(part, out PartAutomationPeer? peer))
            {
                peer = new PartAutomationPeer(Owner, part);
                _parts[part] = peer;
            }

            children.Add(peer);
        }

        // A peer is kept for as long as its part is, so a client that is holding one keeps talking
        // about the same part. Once the part is gone the peer is dropped with it.
        foreach (EntityId gone in _parts.Keys.Where(id => !present.Contains(id)).ToList())
        {
            _parts.Remove(gone);
        }

        return children;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A group rather than a pane: what it is to a client is a container whose children are the
    /// interesting things, which is what <see cref="AutomationControlType.Group"/> means.
    /// </remarks>
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Group;

    /// <inheritdoc/>
    protected override string? GetNameCore() =>
        base.GetNameCore() is { Length: > 0 } name ? name : "Drawing";

    void OnPartsChanged(object? sender, EventArgs e) => InvalidateChildren();
}

/// <summary>One part of the design, as an automation element.</summary>
/// <remarks>
/// <para>
/// <strong>Why it is a <see cref="ControlAutomationPeer"/> over the canvas rather than a bare
/// <see cref="AutomationPeer"/>.</strong> A part has no <see cref="Control"/> of its own — it is
/// drawn. The obvious shape, deriving <see cref="AutomationPeer"/> directly, cannot report a usable
/// bounding rectangle to a Windows client: the hook that turns a peer's top-level rectangle into
/// screen coordinates, <c>AutomationPeer.ToScreenCore</c>, is <c>private protected</c> in Avalonia
/// 12.1.2 and its default returns <see langword="null"/>, and that version's
/// <c>Avalonia.Win32.Automation.AutomationNode.GetBoundingRectangle</c> is
/// <c>Peer.ToScreen(Peer.GetBoundingRectangle()) ?? default</c> — so a bare peer would publish an
/// empty rectangle over UIA. (The macOS bridge reads <c>GetBoundingRectangle</c> directly and
/// converts on its own side, so it would not have minded; Windows decides this.) Deriving from
/// <see cref="ControlAutomationPeer"/> with the canvas as the owner inherits the working
/// conversion — the part's rectangle is the canvas's window, so the canvas is the right thing to
/// convert against — and every other inherited answer that would be about the canvas rather than
/// the part is overridden below.
/// </para>
/// <para>
/// The cost of that choice is that <see cref="ControlAutomationPeer"/> subscribes to its owner, and
/// a peer for a deleted part stays subscribed. Peers only exist once something has asked the canvas
/// for its automation tree, so that cost is paid only while an accessibility client or a script is
/// attached.
/// </para>
/// </remarks>
public sealed class PartAutomationPeer : ControlAutomationPeer, IValueProvider
{
    readonly EntityId _part;

    /// <inheritdoc cref="PartAutomationPeer"/>
    /// <param name="canvas">The canvas the part is drawn on.</param>
    /// <param name="part">Which part.</param>
    public PartAutomationPeer(CanvasView canvas, EntityId part)
        : base(canvas) => _part = part;

    /// <summary>The canvas, typed.</summary>
    public new CanvasView Owner => (CanvasView)base.Owner;

    /// <summary>Which entity of the sketch this element stands for.</summary>
    public EntityId Part => _part;

    /// <summary>
    /// The part's size, formatted the way the canvas writes a dimension label — including the
    /// <c>&#x2248;</c> that marks a value the text does not state exactly.
    /// </summary>
    public string? Value => Owner.PartSize(_part);

    /// <summary>
    /// Always. Typing a dimension is a real editing gesture with its own field, its own validation
    /// and its own refusal message; pretending a client could set this string would be a silent
    /// lie about what happened.
    /// </summary>
    public bool IsReadOnly => true;

    /// <inheritdoc/>
    public void SetValue(string? value) => throw new NotSupportedException(
        "A part's size cannot be set through automation. Select the part and use the dimension " +
        "editor, which validates the text and reports what the update did.");

    /// <inheritdoc/>
    /// <remarks>
    /// The name the rest of the application uses for the part — its label when the design has one,
    /// and its id when it does not (<c>DesignEditor.NameOf</c>). When #7/#8 give parts real names
    /// this follows them without changing.
    /// </remarks>
    protected override string? GetNameCore() => Owner.PartName(_part);

    /// <inheritdoc/>
    /// <remarks>
    /// How the part is turned, as the mark drawn inside it says (#82): "↑ length" for a leg lying on
    /// its side, "turned over", or nothing for a part as drawn.
    /// </remarks>
    protected override string? GetHelpTextCore() => Owner.PartStance(_part);

    /// <inheritdoc/>
    /// <remarks>
    /// The whole entity id, not <see cref="EntityId.ToString"/>'s first eight characters: that is a
    /// display abbreviation and two parts of one design can share it, while an automation id is
    /// what a client uses to say "this one and not that one".
    /// </remarks>
    protected override string GetAutomationIdCore() => _part.Value.ToString("D");

    /// <inheritdoc/>
    protected override string GetClassNameCore() => "Part";

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.Custom;

    /// <inheritdoc/>
    protected override string GetLocalizedControlTypeCore() => "part";

    /// <inheritdoc/>
    /// <remarks>
    /// In top-level coordinates, which is the space every Avalonia peer reports in and the space
    /// the platform bridges convert to screen coordinates from. This is the same transform
    /// <see cref="ControlAutomationPeer"/> applies to a control's own bounds, applied to the
    /// rectangle the canvas draws the part in — so a part's element sits exactly over its pixels,
    /// and follows them when the view is panned or zoomed.
    /// </remarks>
    protected override Rect GetBoundingRectangleCore()
    {
        if (Owner.PartRectangle(_part) is not { } drawn)
        {
            return default;
        }

        if (TopLevel.GetTopLevel(Owner) is not { } root || Owner.TransformToVisual(root) is not { } transform)
        {
            return default;
        }

        return drawn.TransformToAABB(transform);
    }

    /// <inheritdoc/>
    /// <remarks>A part is drawn, not composed of controls, so it has nothing below it.</remarks>
    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    /// <inheritdoc/>
    /// <remarks>
    /// The canvas takes the focus, not a part in it; selecting a part is a separate idea with its
    /// own gestures. Saying so keeps a client from offering a focus it would not get.
    /// </remarks>
    protected override bool IsKeyboardFocusableCore() => false;

    /// <inheritdoc/>
    protected override bool HasKeyboardFocusCore() => false;

    /// <inheritdoc/>
    protected override void SetFocusCore() => throw new NotSupportedException(
        "A part cannot take the keyboard focus; the canvas it is drawn on does.");

    /// <inheritdoc/>
    protected override object? GetProviderCore(Type providerType) =>
        providerType == typeof(IValueProvider) ? this : base.GetProviderCore(providerType);
}
