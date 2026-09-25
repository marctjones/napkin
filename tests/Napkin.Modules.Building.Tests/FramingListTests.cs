using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// The frame a wall implies, checked against counts and lengths worked out by hand in the comments,
/// never read back from napkin's own output. t is a 2x's dressed thickness, 1 1/2 in (PS 20-25
/// Table 3, as the shipped library carries it).
/// </summary>
public class FramingListTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;

    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static Sketch Empty() => Sketch.Empty
        .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
        .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening));

    static Box WallBox(Length length, Length thickness, Length height, Angle? rotation = null)
        => new Box(EntityId.New(), WallLayer, Point3.Origin, length, thickness, height, BoxFace.Top, rotation ?? Angle.Zero) with { Name = "Wall 1" };

    static Box OpeningBox(Box wall, Length offset, Length width, Length sill, Length height, string name = "Window 1")
        => new Box(
            EntityId.New(),
            OpeningLayer,
            wall.World(new Vector3(offset, Length.Zero, sill)),
            width,
            wall.Height,
            height,
            BoxFace.Top,
            wall.Rotation) with { Name = name };

    static WallFraming Frame(Sketch sketch, FramingOptions? options = null)
        => Assert.Single(FramingList.Of(sketch, Library, options));

    static FramingPiece Piece(WallFraming framing, FramingRole role) => Assert.Single(framing.Pieces, piece => piece.Role == role);

    static Sketch Sample()
        => Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", "wall-with-window.scene.json"))).Sketch;

    [Fact]
    public void TheSampleWallIsFramedWithItsWindow()
    {
        // The sample: a 144 in wall, 5 1/2 in thick (so 2x6), 96 in tall; a 36 in opening at 54 in,
        // sill 36 in, 42 in tall (samples/wall-with-window.design.md).
        WallFraming framing = Frame(Sample());

        Assert.Equal("2x6", framing.Stock!.Name);
        Assert.Empty(framing.Problems);

        // Layout studs at 0, 16, …, 128 (k·16 + 1 1/2 ≤ 144 → k ≤ 8), 9 of them, plus the end stud
        // at 142 1/2: 10. The opening's zone is [54 − 3, 90 + 3) = [51, 93), which takes the studs
        // at 64 and 80 (48's body ends at 49 1/2, 96 starts past 93): 10 − 2 = 8 studs.
        // Each 96 − 3 × 1 1/2 = 91 1/2 long.
        Assert.Equal(new FramingPiece(FramingRole.Stud, 8, In(91, 1, 2), framing.Stock), Piece(framing, FramingRole.Stud));
        Assert.Equal(new FramingPiece(FramingRole.KingStud, 2, In(91, 1, 2), framing.Stock), Piece(framing, FramingRole.KingStud));

        // Jacks: one each side (the placeholder), from the bottom plate to the opening's top,
        // 36 + 42 − 1 1/2 = 76 1/2.
        Assert.Equal(new FramingPiece(FramingRole.JackStud, 2, In(76, 1, 2), framing.Stock), Piece(framing, FramingRole.JackStud));

        // Header slot over the jacks: 36 + 2 × 1 1/2 = 39, no stock until the code check sizes it.
        Assert.Equal(new FramingPiece(FramingRole.Header, 1, In(39), null), Piece(framing, FramingRole.Header));

        // Rough sill the opening's width, 36; cripples under it at the layout positions wholly inside
        // [54, 90): 64 and 80, each 36 − 2 × 1 1/2 = 33 long.
        Assert.Equal(new FramingPiece(FramingRole.RoughSill, 1, In(36), framing.Stock), Piece(framing, FramingRole.RoughSill));
        Assert.Equal(new FramingPiece(FramingRole.CrippleBelow, 2, In(33), framing.Stock), Piece(framing, FramingRole.CrippleBelow));

        // One bottom plate and two top plates, the wall's length.
        Assert.Equal(new FramingPiece(FramingRole.BottomPlate, 1, In(144), framing.Stock), Piece(framing, FramingRole.BottomPlate));
        Assert.Equal(new FramingPiece(FramingRole.TopPlate, 2, In(144), framing.Stock), Piece(framing, FramingRole.TopPlate));
        Assert.DoesNotContain(framing.Pieces, piece => piece.Role == FramingRole.CrippleAbove);

        // Room for the header: 96 − 3 (top plates) − 78 (the opening's top) = 15.
        OpeningFraming opening = Assert.Single(framing.Openings);
        Assert.Equal(In(15), opening.HeaderRoom);
        Assert.Equal(In(39), opening.HeaderLength);
        Assert.Equal(1, opening.JacksPerSide);
        Assert.Null(opening.HeaderDepth);
        Assert.Null(opening.Refusal);
        Assert.Equal(OpeningKind.Window, opening.Opening.Kind);

        Assert.Equal(
            "8 studs, 2 king studs, 2 jack studs, 2 cripples below, 1 rough sill, 3 plates, 1 header (not yet sized)",
            framing.Summary);
    }

    [Fact]
    public void ALongWallOnTheLayoutEndsOnALayoutStud()
    {
        // samples/framing-16-oc: 385 1/2 in, 2x4. 24 × 16 = 384 = 385 1/2 − 1 1/2, so the 25th layout
        // stud is the end stud and none is added: 25 studs, each 96 − 4 1/2 = 91 1/2.
        Box wall = WallBox(In(385, 1, 2), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall));

        Assert.Equal("2x4", framing.Stock!.Name);
        Assert.Equal(25, framing.Count(FramingRole.Stud));
        Assert.Equal("25 studs, 3 plates", framing.Summary);
    }

    [Fact]
    public void AWallOffTheLayoutGetsAnEndStud()
    {
        // 144 in, no opening: 0…128 is 9 layout studs, plus the end stud at 142 1/2 = 10
        // (floor(144 / 16) + 1).
        WallFraming framing = Frame(Empty().WithEntity(WallBox(In(144), In(3, 1, 2), In(96))));
        Assert.Equal(10, framing.Count(FramingRole.Stud));
    }

    [Fact]
    public void TheSpacingIsTheCallersChoice()
    {
        // 24 in: layout 0, 24, …, 120 (k·24 + 1 1/2 ≤ 144 → k ≤ 5), 6, plus the end stud, 7. The
        // sample opening's zone [51, 93) takes the one at 72: 6 studs. One cripple, at 72.
        Box wall = WallBox(In(144), In(5, 1, 2), In(96));
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(42)));
        WallFraming framing = Frame(sketch, new FramingOptions { Spacing = In(24) });

        Assert.Equal(6, framing.Count(FramingRole.Stud));
        Assert.Equal(1, framing.Count(FramingRole.CrippleBelow));
        Assert.Equal(In(24), framing.Spacing);
    }

    [Fact]
    public void TwoOpeningsEachTakeTheirOwnStuds()
    {
        // Windows 24 wide at 20 and at 100: zones [17, 47) and [97, 127). Of the 10 positions they
        // take 16 (body 16–17 1/2 touches 17), 32, 96 (touches 97) and 112: 6 studs. 48 and 128
        // are clear. Cripples: 32 inside [20, 44), 112 inside [100, 124): 2.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        Sketch sketch = Empty()
            .WithEntity(wall)
            .WithEntity(OpeningBox(wall, In(20), In(24), In(36), In(42)))
            .WithEntity(OpeningBox(wall, In(100), In(24), In(36), In(42), "Window 2"));
        WallFraming framing = Frame(sketch);

        Assert.Empty(framing.Problems);
        Assert.Equal(6, framing.Count(FramingRole.Stud));
        Assert.Equal(4, framing.Count(FramingRole.KingStud));
        Assert.Equal(4, framing.Count(FramingRole.JackStud));
        Assert.Equal(2, framing.Count(FramingRole.CrippleBelow));
        Assert.Equal(new FramingPiece(FramingRole.Header, 2, In(27), null), Piece(framing, FramingRole.Header));
        Assert.Equal(new FramingPiece(FramingRole.RoughSill, 2, In(24), framing.Stock), Piece(framing, FramingRole.RoughSill));
        Assert.Equal(["Window 1", "Window 2"], framing.Openings.Select(opening => opening.Opening.Name));
    }

    [Fact]
    public void OpeningsTooCloseForTheirStudsAreRefused()
    {
        // Zones [17, 47) and [45, 75) overlap: the second is refused and frames nothing.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        Sketch sketch = Empty()
            .WithEntity(wall)
            .WithEntity(OpeningBox(wall, In(20), In(24), In(36), In(42)))
            .WithEntity(OpeningBox(wall, In(48), In(24), In(36), In(42), "Window 2"));
        WallFraming framing = Frame(sketch);

        Assert.Equal(["Window 2: it is too close to Window 1 for both openings' king and jack studs"], framing.Problems);
        Assert.Equal(2, framing.Count(FramingRole.KingStud));
        Assert.Equal(1, framing.Count(FramingRole.Header));
    }

    [Fact]
    public void AnOpeningAtTheCornerIsRefused()
    {
        // At 0, the west king and jack would stand at −3 to 0, outside the wall. Refused; the wall
        // is framed as if it were not there: 10 studs.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(0), In(36), In(36), In(42))));

        string problem = Assert.Single(framing.Problems);
        Assert.Equal("Window 1: it is too near the wall's end for its king and jack studs, which need 3\" each side", problem);
        Assert.Equal(10, framing.Count(FramingRole.Stud));
        Assert.Equal(0, framing.Count(FramingRole.KingStud));
        Assert.NotNull(Assert.Single(framing.Openings).Refusal);
    }

    [Fact]
    public void AnOpeningWiderThanTheWallIsRefused()
    {
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(-3), In(150), In(36), In(42))));

        Assert.Equal(["Window 1: it is 12'-6\" wide, as wide as the wall or wider"], framing.Problems);
    }

    [Fact]
    public void AZeroLengthWallFramesNothing()
    {
        WallFraming framing = Frame(Empty().WithEntity(WallBox(Length.Zero, In(3, 1, 2), In(96))));

        Assert.Empty(framing.Pieces);
        Assert.Equal(["the wall is 0\" long, too short for its two end studs"], framing.Problems);
        Assert.Equal(framing.Problems[0], framing.Summary);
    }

    [Fact]
    public void AWallTooShortForItsPlatesFramesNothing()
    {
        // Three plates are 4 1/2 in: a 4 1/2 in wall has no room for a stud.
        WallFraming framing = Frame(Empty().WithEntity(WallBox(In(48), In(3, 1, 2), In(4, 1, 2))));
        Assert.Equal(["the wall is 4 1/2\" tall, no taller than its three plates"], framing.Problems);
    }

    [Fact]
    public void AThicknessNoStockHasIsRefused()
    {
        WallFraming framing = Frame(Empty().WithEntity(WallBox(In(144), In(4), In(96))));

        Assert.Null(framing.Stock);
        Assert.Equal(
            ["no framing lumber in the library is 4\" deep; make the wall 3 1/2\" (2x4) or 5 1/2\" (2x6) thick"],
            framing.Problems);
    }

    [Fact]
    public void ASpacingNoWiderThanAStudIsRefused()
    {
        WallFraming framing = Frame(Empty().WithEntity(WallBox(In(144), In(3, 1, 2), In(96))), new FramingOptions { Spacing = In(1, 1, 2) });
        Assert.Equal(["a stud spacing of 1 1/2\" leaves no room between studs"], framing.Problems);
    }

    [Fact]
    public void ADoorHasNoSillAndNoCripplesBelow()
    {
        // Sill 0, 36 × 82 at 54: jacks 82 − 1 1/2 = 80 1/2; room 96 − 3 − 82 = 11.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), Length.Zero, In(82), "Door 1")));

        OpeningFraming door = Assert.Single(framing.Openings);
        Assert.Equal(OpeningKind.Door, door.Opening.Kind);
        Assert.Equal(In(11), door.HeaderRoom);
        Assert.Equal(new FramingPiece(FramingRole.JackStud, 2, In(80, 1, 2), framing.Stock), Piece(framing, FramingRole.JackStud));
        Assert.Equal(0, framing.Count(FramingRole.RoughSill));
        Assert.Equal(0, framing.Count(FramingRole.CrippleBelow));
        Assert.Equal(8, framing.Count(FramingRole.Stud));
    }

    [Fact]
    public void AWindowSillTooLowForARoughSillIsRefused()
    {
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(2), In(42))));
        Assert.Equal(["Window 1: its sill is 2\" up, too low for a rough sill on the bottom plate (3\" at least)"], framing.Problems);
    }

    [Fact]
    public void AnOpeningIntoTheTopPlatesIsRefused()
    {
        // Top 36 + 58 = 94 > 96 − 3 = 93.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(58))));
        Assert.Equal(["Window 1: it reaches into the top plates"], framing.Problems);
    }

    [Fact]
    public void TheCodeCheckHooksSetJacksAndHeaderDepth()
    {
        // Two jacks each side: zone [54 − 4 1/2, 90 + 4 1/2) = [49 1/2, 94 1/2), still taking only
        // 64 and 80 (48's body ends at 49 1/2): 8 studs. Jacks 4 × 76 1/2; header 36 + 4 × 1 1/2 = 42.
        // A 9 in header in the 15 in room leaves 6 in cripples above, at the layout positions over
        // [52 1/2, 91 1/2): 64 and 80.
        Box wall = WallBox(In(144), In(5, 1, 2), In(96));
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(42)));
        WallFraming framing = Frame(sketch, new FramingOptions { JacksPerSide = _ => 2, HeaderDepth = _ => In(9) });

        Assert.Equal(8, framing.Count(FramingRole.Stud));
        Assert.Equal(new FramingPiece(FramingRole.JackStud, 4, In(76, 1, 2), framing.Stock), Piece(framing, FramingRole.JackStud));
        Assert.Equal(new FramingPiece(FramingRole.Header, 1, In(42), null), Piece(framing, FramingRole.Header));
        Assert.Equal(new FramingPiece(FramingRole.CrippleAbove, 2, In(6), framing.Stock), Piece(framing, FramingRole.CrippleAbove));
        Assert.Equal(In(9), Assert.Single(framing.Openings).HeaderDepth);
    }

    [Fact]
    public void AHeaderDeeperThanTheRoomIsRefused()
    {
        Box wall = WallBox(In(144), In(5, 1, 2), In(96));
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(42)));
        WallFraming framing = Frame(sketch, new FramingOptions { HeaderDepth = _ => In(16) });
        Assert.Equal(["Window 1: a 1'-4\" header does not fit the 1'-3\" above it"], framing.Problems);
    }

    [Fact]
    public void NoJackStudsIsRefused()
    {
        Box wall = WallBox(In(144), In(5, 1, 2), In(96));
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(42)));
        WallFraming framing = Frame(sketch, new FramingOptions { JacksPerSide = _ => 0 });
        Assert.Equal(["Window 1: an opening needs at least one jack stud each side"], framing.Problems);
    }

    [Fact]
    public void ATurnedWallFramesTheSame()
    {
        // The same wall and window turned a quarter: the opening is found in the wall's own frame.
        Box wall = WallBox(In(144), In(5, 1, 2), In(96), Angle.Right);
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(42)));
        WallFraming framing = Frame(sketch);

        Assert.Equal(8, framing.Count(FramingRole.Stud));
        Assert.Equal(In(54), Assert.Single(framing.Openings).Opening.Offset);
    }

    [Fact]
    public void AWallAtAnOddAngleIsRefused()
    {
        WallFraming framing = Frame(Empty().WithEntity(WallBox(In(144), In(3, 1, 2), In(96), Angle.Degrees(30))));
        Assert.Equal(["napkin frames a wall only standing as drawn, turned by a right angle at most"], framing.Problems);
    }

    [Fact]
    public void AnOpeningOffTheWallIsNotInIt()
    {
        // Not flush with the wall's faces (moved 1 in north), or turned differently: not the wall's.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        Box off = OpeningBox(wall, In(54), In(36), In(36), In(42)) with { Anchor = new Point3(In(54), In(1), In(36)) };
        Box turned = OpeningBox(wall, In(54), In(36), In(36), In(42)) with { Rotation = Angle.Right };
        Box beyond = OpeningBox(wall, In(150), In(36), In(36), In(42));
        Box thinner = OpeningBox(wall, In(54), In(36), In(36), In(42)) with { Height = In(3) };
        Sketch sketch = Empty().WithEntity(wall).WithEntity(off).WithEntity(turned).WithEntity(beyond).WithEntity(thinner);

        Assert.Empty(Frame(sketch).Openings);
        Assert.Null(Opening.Find(sketch, off.Id));
        Assert.Null(FramingList.For(sketch, off.Id, Library));
    }

    [Fact]
    public void AWallAndItsOpeningAreFoundByEitherId()
    {
        Sketch sketch = Sample();
        Wall wall = Assert.Single(Wall.All(sketch));
        Opening opening = Assert.Single(Opening.In(sketch, wall));

        Assert.Equal(wall.Id, FramingList.For(sketch, wall.Id, Library)!.Wall.Id);
        Assert.Equal(wall.Id, FramingList.For(sketch, opening.Id, Library)!.Wall.Id);
        Assert.Equal("Wall", wall.Name);
        Assert.Equal("Opening", opening.Name);
    }

    [Fact]
    public void AnUnnamedWallAndOpeningAreCalledByWhatTheyAre()
    {
        Box wall = WallBox(In(144), In(3, 1, 2), In(96)) with { Name = string.Empty };
        Box door = OpeningBox(wall, In(54), In(36), Length.Zero, In(80)) with { Name = string.Empty };
        Box window = OpeningBox(wall, In(100), In(24), In(36), In(24)) with { Name = string.Empty };
        Sketch sketch = Empty().WithEntity(wall).WithEntity(door).WithEntity(window);

        WallFraming framing = Frame(sketch);
        Assert.Equal("Wall", framing.Wall.Name);
        Assert.Equal(["Door", "Window"], framing.Openings.Select(opening => opening.Opening.Name));
    }

    [Fact]
    public void ABoxOnAnotherLayerIsNeitherWallNorOpening()
    {
        LayerId parts = LayerId.New();
        Box box = new Box(EntityId.New(), parts, Point3.Origin, In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero) with { Name = "Shelf" };
        Sketch sketch = Empty().WithLayer(new Layer(parts, "Parts")).WithEntity(box);

        Assert.Empty(FramingList.Of(sketch, Library));
        Assert.Null(FramingList.For(sketch, box.Id, Library));
        Assert.Null(FramingList.For(sketch, EntityId.New(), Library));
    }

    [Theory]
    [InlineData(FramingRole.BottomPlate, "bottom plate", "bottom plates")]
    [InlineData(FramingRole.TopPlate, "top plate", "top plates")]
    [InlineData(FramingRole.Stud, "stud", "studs")]
    [InlineData(FramingRole.KingStud, "king stud", "king studs")]
    [InlineData(FramingRole.JackStud, "jack stud", "jack studs")]
    [InlineData(FramingRole.Header, "header", "headers")]
    [InlineData(FramingRole.RoughSill, "rough sill", "rough sills")]
    [InlineData(FramingRole.CrippleBelow, "cripple below", "cripples below")]
    [InlineData(FramingRole.CrippleAbove, "cripple above", "cripples above")]
    public void EveryRoleHasAName(FramingRole role, string one, string many)
    {
        Assert.Equal(one, FramingList.Label(role, 1));
        Assert.Equal(many, FramingList.Label(role, 2));
    }

    [Fact]
    public void AnUnknownRoleHasNoName()
        => Assert.Throws<ArgumentOutOfRangeException>(() => FramingList.Label((FramingRole)99, 1));

    [Fact]
    public void ALayoutStudOnTheOpeningsSideIsACripple()
    {
        // A 32 in window at 48: cripple positions wholly inside [48, 80) are 48 (its body 48–49 1/2)
        // and 64; 80's body starts at the far side. 2 cripples.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(48), In(32), In(36), In(42))));
        Assert.Equal(2, framing.Count(FramingRole.CrippleBelow));
    }

    [Fact]
    public void AnOpeningWhoseStudsJustFitAtTheEndIsFramed()
    {
        // At 105, 36 wide: its zone ends at 105 + 36 + 3 = 144, the wall's end exactly.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(105), In(36), In(36), In(42))));
        Assert.Empty(framing.Problems);
        Assert.Equal(2, framing.Count(FramingRole.KingStud));
    }

    [Fact]
    public void OpeningsWhoseStudsJustTouchAreBothFramed()
    {
        // Zones [17, 47) and [47, 77): they meet but do not overlap.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        Sketch sketch = Empty()
            .WithEntity(wall)
            .WithEntity(OpeningBox(wall, In(20), In(24), In(36), In(42)))
            .WithEntity(OpeningBox(wall, In(50), In(24), In(36), In(42), "Window 2"));
        Assert.Empty(Frame(sketch).Problems);
        Assert.Equal(4, Frame(sketch).Count(FramingRole.KingStud));
    }

    [Fact]
    public void AnOpeningUpToThePlatesLeavesNoRoomForAHeader()
    {
        // Top 36 + 57 = 93 = 96 − 3: no room at all.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(57))));
        Assert.Equal(["Window 1: it leaves no room for a header under the top plates"], framing.Problems);
    }

    [Fact]
    public void ASillOnTheBottomPlateHasNoCripples()
    {
        // Sill 3 = 2 × 1 1/2: the rough sill lies on the bottom plate, and no cripple fits under it.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(3), In(42))));
        Assert.Empty(framing.Problems);
        Assert.Equal(1, framing.Count(FramingRole.RoughSill));
        Assert.Equal(0, framing.Count(FramingRole.CrippleBelow));
    }

    [Fact]
    public void ALayoutStudEndingOnTheFarSideIsACripple()
    {
        // A 31 1/2 in window at 50: inside [50, 81 1/2) are 64 and 80 (its body 80–81 1/2). 2.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        WallFraming framing = Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(50), In(31, 1, 2), In(36), In(42))));
        Assert.Equal(2, framing.Count(FramingRole.CrippleBelow));
    }

    [Fact]
    public void AnOpeningWhoseStudsJustFitAtTheStartIsFramed()
    {
        // At 3: its king and jack stand at 0 to 3, the wall's start exactly.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        Assert.Empty(Frame(Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(3), In(36), In(36), In(42)))).Problems);
    }

    [Fact]
    public void AHeaderFillingTheRoomLeavesNoCripplesAbove()
    {
        Box wall = WallBox(In(144), In(5, 1, 2), In(96));
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(54), In(36), In(36), In(42)));
        WallFraming framing = Frame(sketch, new FramingOptions { HeaderDepth = _ => In(15) });
        Assert.Empty(framing.Problems);
        Assert.Equal(0, framing.Count(FramingRole.CrippleAbove));
    }

    [Fact]
    public void CripplesAboveStandOverTheWholeHeader()
    {
        // A 30 1/2 in window at 49 1/2 with one jack: the header runs 48 to 81 1/2. Layout positions
        // wholly over it are 48, 64 and 80 (its body ends at 81 1/2 exactly): 3 above. Below, inside
        // [49 1/2, 80), only 64: 1.
        Box wall = WallBox(In(144), In(3, 1, 2), In(96));
        Sketch sketch = Empty().WithEntity(wall).WithEntity(OpeningBox(wall, In(49, 1, 2), In(30, 1, 2), In(36), In(42)));
        WallFraming framing = Frame(sketch, new FramingOptions { HeaderDepth = _ => In(9) });
        Assert.Equal(3, framing.Count(FramingRole.CrippleAbove));
        Assert.Equal(1, framing.Count(FramingRole.CrippleBelow));
    }

    [Fact]
    public void TheFrameBuysBoardsThroughTheShoppingList()
    {
        // The sample's 2x6 pieces, longest first, first fit over 6'…16' (ShoppingList §4 step 2):
        // three 144s take three 12' boards; ten 91 1/2s (8 studs, 2 kings) each take an 8' board,
        // leaving 4 1/2; two 76 1/2 jacks fit no leftover, so two more 8' (19 1/2 left each); the
        // 36 sill fits neither leftover, so a 6' (36 left); the first 33 cripple fits that (3 left),
        // the second needs another 6'. 2 × 6', 12 × 8', 3 × 12'. The header has no stock: nothing
        // bought for it, said so.
        ImmutableArray<CutListRow> rows = FramingList.CutRows(FramingList.Of(Sample(), Library));
        ImmutableArray<ShoppingListRow> shopping = ShoppingList.Of(rows);

        Assert.Equal(2, shopping.Length);
        Assert.Equal("2x6", shopping[0].Material);
        Assert.Equal("2 × 6'-0\", 12 × 8'-0\", 3 × 12'-0\"", shopping[0].BuyText);
        Assert.StartsWith("Wall bottom plate × 1, Wall top plate × 2, Wall stud × 8, Wall king stud × 2", shopping[0].For, StringComparison.Ordinal);
        Assert.Equal(ShoppingListKind.NothingToBuy, shopping[1].Kind);
        Assert.Equal("header, not yet sized", shopping[1].Material);
        Assert.Equal("Wall header × 1", shopping[1].For);

        CutListRow stud = Assert.Single(rows, row => row.Label == "Wall stud");
        Assert.Equal(In(5, 1, 2), stud.Width);
        Assert.Equal(In(1, 1, 2), stud.Thickness);
        Assert.Equal([Assert.Single(Wall.All(Sample())).Id], stud.Members);
    }
}
