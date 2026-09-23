using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Modules.Furniture;

namespace Napkin.App.Editing;

/// <summary>
/// A face a part can be set down on in the 3D view (#74): one face of a part, or the floor.
/// </summary>
/// <param name="Target">The part whose face it is, or <see langword="null"/> for the floor.</param>
/// <param name="TargetFace">That face, in the part's own frame; <see langword="null"/> for the floor.</param>
/// <param name="Normal">The world axis the face faces along.</param>
/// <param name="Positive">Whether it faces the positive way along it — the side a part is set down on.</param>
/// <param name="Coordinate">Where the face is along its axis, exactly.</param>
public sealed record PlacementFace(EntityId? Target, BoxFace? TargetFace, Axis Normal, bool Positive, Length Coordinate)
{
    /// <summary>The floor: the plan datum, Z = 0, faced from above.</summary>
    public static PlacementFace Floor { get; } = new(null, null, Axis.Z, true, Length.Zero);

    /// <summary>A face of a part, as its orientation turns it.</summary>
    public static PlacementFace Of(Box box, BoxFace face)
    {
        ArgumentNullException.ThrowIfNull(box);

        (Axis axis, bool positive) = box.Orientation.Normal(face);
        return new PlacementFace(box.Id, face, axis, positive, SpaceSnapResolver.FaceCoordinate(box, face));
    }

    /// <summary>
    /// The face's plane, as two world axes: for a level face east–west then north–south, for an
    /// upright one across then up — the first is where a board's length goes when nothing says.
    /// </summary>
    public (Axis U, Axis V) Plane => Normal switch
    {
        Axis.Z => (Axis.X, Axis.Y),
        Axis.Y => (Axis.X, Axis.Z),
        _ => (Axis.Y, Axis.Z),
    };

    /// <summary>A point in the face's plane, as the two coordinates across it.</summary>
    public Point2 InPlane(Point3 point) => new(point.Component(Plane.U), point.Component(Plane.V));
}

/// <summary>A part as it would be placed: the box, what it is cut from, and what it caught on the way.</summary>
/// <param name="Box">The box, with its id, name, place, sizes and orientation.</param>
/// <param name="Face">The face it rests on.</param>
/// <param name="Part">What part it is, for a stock size; <see langword="null"/> for a plain board.</param>
/// <param name="Stock">The stock it is cut from; <see langword="null"/> for a plain board.</param>
/// <param name="Snaps">The relationships its edges caught against other parts' faces.</param>
public sealed record PlacementPreview(Box Box, PlacementFace Face, Part? Part, StockItem? Stock, ImmutableList<Relationship> Snaps);

/// <summary>
/// Placing a part in the 3D view on the face under the pointer (#74,
/// <c>docs/design/assembly-model.md</c> &#xA7;6 as amended): a stock size or a plain board, resting on
/// that face or on the floor, lying flat against it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The plan's tools, in the face's plane.</strong> The shape is worked out by
/// <see cref="StockTool"/> — or <see cref="RectangleTool"/>, for a plain board — fed the press and the
/// pointer as coordinates across the face (<see cref="PlacementFace.Plane"/>), so which of a stock's
/// sizes the drag decides, and which the yard fixes, is said in one place for both views. The result
/// is lifted into space: the part's local X and Y along the face's two axes, so its thickness runs
/// along the face's normal, and its low corner on the face's side of it.
/// </para>
/// <para>
/// <strong>A click places a default</strong>, which the plan's tools cannot, because nothing in a
/// click states a length: 24″ of a stock size, or a plain board 24″ × 12″. A drag states it, as it
/// does in the plan.
/// </para>
/// <para>
/// <strong>Dropping it</strong> adds the part with its stock (<see cref="StockAssignment"/>, as the
/// plan's tool does), then states <c>Flush(target face, the part's face against it)</c> and whatever
/// its edges caught — all one undo step, each relationship put to the updater as a snap's is. The
/// floor is not an entity, so a part set on it is held by nothing (&#xA7;6).
/// </para>
/// </remarks>
public sealed class PlacementTool
{
    /// <summary>How long a part placed with a click is, before anything says otherwise.</summary>
    public static readonly Length DefaultLength = Length.Inches(24);

    /// <summary>How wide a sheet good or a plain board placed with a click is.</summary>
    public static readonly Length DefaultWidth = Length.Inches(12);

    readonly StockTool _stock = new();
    readonly RectangleTool _rectangle = new();

    /// <summary>The stock size held, or <see langword="null"/>.</summary>
    public StockItem? Stock => _stock.Stock;

    /// <summary>Whether a plain board — the rectangle tool's — is held.</summary>
    public bool PlainBoard { get; private set; }

    /// <summary>Whether anything is held to place.</summary>
    public bool IsArmed => Stock is not null || PlainBoard;

    /// <summary>What is held, in words: "a 2x4", "a plain board".</summary>
    public string Holding => Stock is { } stock ? $"a {stock.Name}" : "a plain board";

    /// <summary>Picks up a stock size, or puts everything down with null.</summary>
    /// <returns><see langword="false"/> when the item cannot be placed (a fastener) and nothing was picked up.</returns>
    public bool Arm(StockItem? item)
    {
        PlainBoard = false;
        return _stock.Arm(item);
    }

    /// <summary>Picks up a plain board.</summary>
    public void ArmPlainBoard()
    {
        _stock.Arm(null);
        PlainBoard = true;
    }

    /// <summary>Puts down whatever is held.</summary>
    public void Disarm() => Arm(null);

    /// <summary>
    /// The part as it would be placed on a face, pressed at one point of it and now at another —
    /// the same point for a click, which places the default size.
    /// </summary>
    /// <param name="sketch">The drawing.</param>
    /// <param name="face">The face it rests on.</param>
    /// <param name="from">Where the press was, on the face, already on the grid.</param>
    /// <param name="to">Where the pointer is now, on the face, already on the grid.</param>
    /// <param name="layer">The layer a new part goes on.</param>
    /// <param name="id">The id the new part will have.</param>
    /// <param name="name">What to call it.</param>
    /// <param name="gridStepInches">The grid step in force.</param>
    /// <param name="radius">How near another part's face its edges have to come to catch.</param>
    /// <returns><see langword="null"/> when nothing is held, or the drag has not gone anywhere a part could be.</returns>
    public PlacementPreview? Shape(
        Sketch sketch,
        PlacementFace face,
        Point3 from,
        Point3 to,
        LayerId layer,
        EntityId id,
        string name,
        double gridStepInches,
        Length radius)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(face);

        if (!IsArmed)
        {
            return null;
        }

        Point2 press = face.InPlane(from);
        Point2 now = face.InPlane(to);
        bool click = press == now;
        if (click)
        {
            // Nothing in a click states a size, so the default does — along the face's first axis,
            // centred on the pointer, which is where the eye puts it.
            Vector2 size = new(DefaultLength, Stock is null || !FixesWidth(Stock) ? DefaultWidth : Length.Zero);
            press -= new Vector2(size.Dx.Divide(2, Rounding.HalfToEven), size.Dy.Divide(2, Rounding.HalfToEven));
            now = press + size;
        }

        Point2 low;
        Length along, across, thickness;
        Part? part = null;
        if (Stock is { } stock)
        {
            _stock.Begin(press);
            _stock.MoveTo(now);
            bool shaped = _stock.TryShape(out low, out along, out across, out PlanAxes axes);
            _stock.Cancel();
            if (!shaped)
            {
                return null;
            }

            thickness = StockAssignment.Fixes(stock).First(fixes => fixes.Dimension == PartDimension.Thickness).Value;
            part = new Part(stock.Name, Species: null, Quantity: 1, axes);
        }
        else
        {
            _rectangle.Begin(press);
            _rectangle.MoveTo(now);
            bool shaped = _rectangle.TryRectangle(out low, out along, out across);
            _rectangle.Cancel();
            if (!shaped)
            {
                return null;
            }

            thickness = Box.DefaultDepth;
        }

        // Local X and Y along the face's two axes, both the positive way; local Z — the thickness —
        // then runs along the normal, one way or the other, which only decides which face touches.
        (Axis u, Axis v) = face.Plane;
        Orientation orientation = LyingOn(u, v);
        Box standing = new Box(id, layer, Point3.Origin, along, across, thickness, orientation.FaceUp, orientation.Rotation) with { Name = name };
        Vector3 toAnchor = standing.Anchor - SpaceSnapResolver.Extent(standing).Low;

        Point3 lowCorner = Point3.Origin
            .WithComponent(u, low.X)
            .WithComponent(v, low.Y)
            .WithComponent(face.Normal, face.Positive ? face.Coordinate : face.Coordinate - thickness);
        Box box = standing with { Anchor = lowCorner + toAnchor };

        ImmutableList<Relationship> snaps = [];
        if (click)
        {
            // A click's part is where the pointer put it only roughly: its edges line up with the
            // faces of parts it overlaps across the face's plane, or its corner with the grid.
            SpaceSnapPlan plan = SpaceSnapResolver.Resolve(sketch, box, box.Anchor, [u, v], gridStepInches, radius);
            box = box with { Anchor = plan.Anchor };
            snaps = plan.Relationships;
        }

        return new PlacementPreview(box, face, part, Stock, snaps);
    }

    /// <summary>The part's face that rests against the face it is placed on.</summary>
    public static BoxFace Contact(Box box, PlacementFace face)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(face);

        return SpaceSnapResolver.FaceFacing(box, face.Normal, !face.Positive);
    }

    /// <summary>
    /// What placing a preview asks of the editor: the part and its stock, as one request, and then
    /// the relationships that hold it — the flush against the face it rests on, and its snaps —
    /// each to be put to the updater as a snap's is.
    /// </summary>
    public static (Request Add, ImmutableList<Relationship> Holds) Requests(Sketch sketch, PlacementPreview preview)
    {
        ArgumentNullException.ThrowIfNull(sketch);
        ArgumentNullException.ThrowIfNull(preview);

        Box box = preview.Box;
        Request add = preview.Stock is { } stock && preview.Part is { } part
            ? Batch.Of(new AddEntity(box), StockAssignment.RequestsFor(sketch, box, part, stock))
            : new AddEntity(box);

        ImmutableList<Relationship>.Builder holds = ImmutableList.CreateBuilder<Relationship>();
        if (preview.Face is { Target: { } target, TargetFace: { } targetFace })
        {
            holds.Add(new Flush(
                RelationshipId.New(),
                new FeatureRef(target, BoxFeature.Face(targetFace)),
                new FeatureRef(box.Id, BoxFeature.Face(Contact(box, preview.Face)))));
        }

        holds.AddRange(preview.Snaps);
        return (add, holds.ToImmutable());
    }

    /// <summary>The one of the 24 orientations that sends local X along +u and local Y along +v.</summary>
    public static Orientation LyingOn(Axis u, Axis v)
    {
        foreach (BoxFace faceUp in Enum.GetValues<BoxFace>())
        {
            for (int quarters = 0; quarters < 4; quarters++)
            {
                Orientation candidate = new(faceUp, Angle.Right * quarters);
                if (candidate.Image(Axis.X) == (u, true) && candidate.Image(Axis.Y) == (v, true))
                {
                    return candidate;
                }
            }
        }

        throw new ArgumentException($"No turn of a box lays its X along {u} and its Y along {v}.");
    }

    static bool FixesWidth(StockItem stock) =>
        StockAssignment.Fixes(stock).Any(fixes => fixes.Dimension == PartDimension.Width);
}
