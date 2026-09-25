using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// Rooms, their bounding walls and openings (docs/design/renovation-sketches.md §4.2, test 4), the
/// area takeoff (§5, test 7), out-of-square (§5.6, test 8), bays (§5.2, test 9) and worked example
/// 1 against its hand-derived expectations (test 10). Every number is by hand in the comments or in
/// samples/basement-room.design.md.
/// </summary>
public class RoomTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    static (Sketch Sketch, JsonElement Expected) Sample()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "samples");
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(directory, "basement-room.scene.json"))).Sketch;
        using JsonDocument expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "basement-room.expected.json")));
        return (sketch, expected.RootElement.Clone());
    }

    static Room TheRoom(Sketch sketch) => Assert.Single(Room.All(sketch));

    static ImmutableArray<TakeoffLine> Takeoff(Sketch sketch) => AreaTakeoff.All(sketch, Library, CodePacks.None);

    // ---- Test 4: bounding walls and the room's openings ---------------------------------------

    [Fact]
    public void Example_1s_four_walls_bound_the_room_by_their_inside_faces_and_both_openings_are_its()
    {
        (Sketch sketch, _) = Sample();
        Room room = TheRoom(sketch);

        ImmutableArray<BoundingWall> bounding = RoomBounds.Of(sketch, room);

        // Each inside face runs the room's whole edge: 168 along south and north, 144 along west and east.
        Assert.Equal(
            [("Wall, south", RoomSide.South, 168L), ("Wall, north", RoomSide.North, 168L), ("Wall, west", RoomSide.West, 144L), ("Wall, east", RoomSide.East, 144L)],
            bounding.Select(b => (b.Wall.Name, b.Side, b.Along.Units / 1024)));
        Assert.Equal(["Door 1", "Window 1"], RoomBounds.Openings(sketch, room).Select(opening => opening.Name));
        Assert.Empty(RoomBounds.NearMisses(sketch, room));
        Assert.Equal((In(168), In(144), In(96), In(624)), (room.Length, room.Width, room.Height, room.Perimeter));
    }

    [Fact]
    public void An_opening_straddling_the_edges_end_is_not_the_rooms_and_a_room_short_of_a_wall_is_told_to_snap()
    {
        (Sketch sketch, _) = Sample();
        Room room = TheRoom(sketch);

        // Shrink the room to 58" east–west from the west wall: its south edge runs 3 1/2..61 1/2, and
        // the door at 60..96 straddles its end — the door is not this room's.
        Sketch small = sketch.WithEntity(room.Box with { Width = In(58) });
        Assert.Equal(["Window 1"], RoomBounds.Openings(small, TheRoom(small)).Select(opening => opening.Name));

        // Move the room up one unit: the south wall no longer bounds it, and the room is told so.
        Sketch off = sketch.WithEntity(room.Box with { Anchor = room.Box.Anchor with { Y = room.Box.Anchor.Y + new Length(1) } });
        Assert.DoesNotContain(RoomBounds.Of(off, TheRoom(off)), b => b.Wall.Name == "Wall, south");
        Assert.Contains("not bounded by Wall, south — snap it to the wall", RoomBounds.NearMisses(off, TheRoom(off)));
    }

    [Fact]
    public void A_room_with_no_bounding_wall_takes_off_its_own_perimeter_and_says_so()
    {
        LayerId roomLayer = LayerId.New();
        Box box = Box.AsDrawn(EntityId.New(), roomLayer, Point2.Origin, In(120), In(96), In(96), Angle.Zero) with
        {
            Room = RoomInputs.None with { Drywall = RoomSurfaces.Walls },
        };
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(roomLayer, BuildingLayers.Room)).WithEntity(box);

        ImmutableArray<TakeoffLine> lines = Takeoff(sketch);

        // P = 2(120 + 96) = 432", × 96 = 41472 sq in = 288 sq ft; nothing subtracted.
        Assert.Equal("walls 288 sq ft less openings 0 = 288 sq ft; ceiling 80 sq ft; perimeter 36'-0\"", lines[0].Shown);
        Assert.Equal("no walls bound this room; openings are not subtracted", lines[0].Note);
        Assert.Equal("walls, 288 sq ft", lines[1].Shown);
        Assert.Equal("type the sheet size you will buy", lines[1].Note);
        Assert.Null(lines[1].Count);
    }

    // ---- Test 7: every line of §10.2, exactly -------------------------------------------------

    [Fact]
    public void Example_1s_takeoff_is_the_expectations_line_for_line()
    {
        (Sketch sketch, JsonElement expected) = Sample();

        ImmutableArray<TakeoffLine> lines = Takeoff(sketch);

        JsonElement[] rows = [.. expected.GetProperty("areaTakeoff").EnumerateArray()];
        Assert.Equal(rows.Length, lines.Length);
        for (int i = 0; i < rows.Length; i++)
        {
            Assert.Equal(rows[i].GetProperty("finish").GetString(), lines[i].Finish.ToString());
            Assert.Equal(Int128.Parse(rows[i].GetProperty("exactUnits").GetString()!, System.Globalization.CultureInfo.InvariantCulture), lines[i].Exact);
            Assert.Equal(rows[i].GetProperty("count").ValueKind == JsonValueKind.Null ? null : rows[i].GetProperty("count").GetInt64(), lines[i].Count);
            Assert.Equal(rows[i].GetProperty("shown").GetString(), lines[i].Shown);
        }

        Assert.Equal(AreaTakeoff.SheetsByArea, lines[1].Note);
        Assert.StartsWith("10 % allowance, " + AreaTakeoff.Allowance, lines[5].Note, StringComparison.Ordinal);
        Assert.Equal("not allowing for corners or waste", lines[6].Note);
        Assert.Equal("Drywall: walls and ceiling, 558 sq ft; 18 sheets 4'-0\" × 8'-0\" (sheets by area — a layout may need more)", lines[1].Text);
    }

    [Fact]
    public void Two_drywall_pools_would_buy_one_more_sheet_and_napkin_counts_one()
    {
        // Walls 390 / 32 = 12.19 → 13 and ceiling 168 / 32 = 5.25 → 6 would be 19; one pool of 558 is 18.
        (Sketch sketch, _) = Sample();
        Assert.Equal(18, Takeoff(sketch).Single(line => line.Finish == Finish.Drywall).Count);
    }

    [Fact]
    public void Nothing_typed_shows_the_area_alone_and_says_what_to_type()
    {
        (Sketch sketch, _) = Sample();
        Room room = TheRoom(sketch);
        Sketch bare = sketch.WithEntity(room.Box with
        {
            Room = room.Box.Room! with
            {
                Sheet = null, InsulationCoverage = null, PaintCoats = null, PaintCoverage = null, FlooringBox = null, BaseboardStick = null,
                Paint = RoomSurfaces.Walls, Drywall = RoomSurfaces.Walls, FlooringWaste = 0,
            },
        });

        ImmutableArray<TakeoffLine> lines = Takeoff(bare);

        Assert.Equal(("walls, 390 sq ft", "type the sheet size you will buy"), (lines[1].Shown, lines[1].Note));
        Assert.Equal(("exterior walls, 390 sq ft", "type the coverage from the package"), (lines[2].Shown, lines[2].Note));
        Assert.Equal(("walls, 390 sq ft to cover", "type the number of coats (1 used); type the coverage from your can"), (lines[4].Shown, lines[4].Note));
        Assert.Equal(("168 sq ft", "0 % allowance, typed; rounded up; type the coverage from the box"), (lines[5].Shown, lines[5].Note));
        Assert.Equal(("49'-0\"", "napkin has read no stock-length list for trim"), (lines[6].Shown, lines[6].Note));
        Assert.All(lines.Where(line => line.Finish != Finish.InsulationBays), line => Assert.Null(line.Count));
    }

    [Fact]
    public void Insulation_on_all_walls_by_bays_only_and_on_no_wall_with_a_wall_not_framed()
    {
        (Sketch sketch, _) = Sample();
        Room room = TheRoom(sketch);

        // By bays alone: one line, the 37 bays.
        Sketch bays = sketch.WithEntity(room.Box with { Room = room.Box.Room! with { Insulation = InsulatedWalls.All, InsulationBy = InsulationBy.Bays } });
        TakeoffLine line = Assert.Single(Takeoff(bays), l => l.Finish is Finish.Insulation or Finish.InsulationBays);
        Assert.Equal("all walls, 37 bays, 7'-7 1/2\" tall, at 16\" on centre", line.Shown);

        // Mark the north wall interior: insulating exterior walls leaves it out — 390 − 168 × 8 = 390 − 112 = 278 sq ft,
        // 278 / 40 = 6.95 → 7 bags; its 11 bays go too: 26.
        Box north = Wall.All(sketch).Single(wall => wall.Name == "Wall, north").Box;
        Sketch interior = sketch.WithEntity(north with { WallInputs = north.WallInputs! with { Side = WallSide.Interior } });
        ImmutableArray<TakeoffLine> lines = Takeoff(interior);
        Assert.Equal(("exterior walls, 278 sq ft; 7 bags at 40 sq ft", 7L), (lines[2].Shown, lines[2].Count));
        Assert.Equal(26, lines[3].Count);

        // A wall napkin does not frame (a 5" thickness no library lumber has) has no bays and says so.
        Box east = Wall.All(sketch).Single(wall => wall.Name == "Wall, east").Box;
        // Thickened outward, so its inside face stays on the room's edge.
        Sketch odd = sketch.WithEntity(east with { Height = In(5), Anchor = east.Anchor with { X = In(176, 1, 2) } });
        Assert.Contains("Wall, east is not framed by napkin, so it has no bays here", Takeoff(odd)[3].Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_new_room_is_taken_off()
    {
        (Sketch sketch, _) = Sample();
        Room room = TheRoom(sketch);

        Assert.Empty(Takeoff(sketch.WithEntity(room.Box with { Phase = Phase.Existing })));
        Assert.Empty(Takeoff(sketch.WithEntity(room.Box with { Phase = Phase.Demolish })));
    }

    [Fact]
    public void The_takeoff_goes_to_csv_under_its_own_header()
    {
        (Sketch sketch, _) = Sample();
        string csv = AreaTakeoff.ToCsv(Takeoff(sketch).Take(2));

        Assert.StartsWith(
            "Area takeoff\nRoom,Finish,Quantity,Count,Note\nRoom,Surfaces,\"walls 416 sq ft less openings 26 = 390 sq ft; ceiling 168 sq ft; perimeter 52'-0\"\"\",,\n",
            csv,
            StringComparison.Ordinal);
        Assert.Contains("Room,Drywall,\"walls and ceiling, 558 sq ft; 18 sheets 4'-0\"\" × 8'-0\"\"\",18,sheets by area — a layout may need more\n", csv, StringComparison.Ordinal);
        Assert.Equal("Surfaces", AreaTakeoff.Word(Finish.Surfaces));
        Assert.Throws<ArgumentOutOfRangeException>(() => AreaTakeoff.Word((Finish)99));
    }

    [Theory]
    [InlineData(0L, "0")]
    [InlineData(150994944L * 558, "558")]
    [InlineData(150994944L * 1748 / 10, "174.8")]
    [InlineData(150994944L * 17 + 75497472, "17.5")]
    [InlineData(150994944L / 20 - 1, "0")]
    [InlineData((150994944L / 20) + 1, "0.1")]
    public void An_area_alone_rounds_once_to_one_decimal(long units, string text)
        => Assert.Equal(text, AreaTakeoff.SquareFeet(units));

    // ---- Test 8: out of square ----------------------------------------------------------------

    static Room Measured(long length, long width, MeasuredRoom measured)
        => new(Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, In(length), In(width), In(96), Angle.Zero) with
        {
            Room = RoomInputs.None with { Measured = measured },
        });

    [Fact]
    public void Unequal_opposite_sides_and_unequal_diagonals_are_out_of_square()
    {
        Room sides = Measured(168, 144, MeasuredRoom.None with { South = In(168), North = In(169), West = In(144), East = In(144) });
        Assert.Equal(["measured south 14'-0\", north 14'-1\": out of square; drawn as 14'-0\""], OutOfSquare.Of(sides));

        Room ends = Measured(168, 144, MeasuredRoom.None with { West = In(144), East = In(145) });
        Assert.Equal(["measured west 12'-0\", east 12'-1\": out of square; drawn as 12'-0\""], OutOfSquare.Of(ends));

        Room diagonals = Measured(168, 144, MeasuredRoom.None with { Diagonal1 = In(221), Diagonal2 = In(222, 1, 2) });
        Assert.Equal(["diagonals differ by 1 1/2\": out of square"], OutOfSquare.Of(diagonals));

        Room same = Measured(168, 144, MeasuredRoom.None with { Diagonal1 = In(221), Diagonal2 = In(221), South = In(168) });
        Assert.Empty(OutOfSquare.Of(same));
    }

    [Fact]
    public void One_diagonal_is_compared_by_its_square_and_a_true_3_4_5_room_is_not_flagged()
    {
        // 12 × 16 ft, diagonal 20 ft: 144² + 192² = 20736 + 36864 = 57600 = 240². Square.
        Assert.Empty(OutOfSquare.Of(Measured(192, 144, MeasuredRoom.None with { Diagonal1 = In(240) })));
        Assert.Empty(OutOfSquare.Of(Measured(192, 144, MeasuredRoom.None with { Diagonal2 = In(240) })));

        // 12 × 14 ft: 144² + 168² = 48960; 221² = 48841 is short, 222² = 49284 long.
        Assert.Equal(["diagonal 18'-5\" measures short for 12'-0\" × 14'-0\""], OutOfSquare.Of(Measured(168, 144, MeasuredRoom.None with { Diagonal1 = In(221) })));
        Assert.Equal(["diagonal 18'-6\" measures long for 12'-0\" × 14'-0\""], OutOfSquare.Of(Measured(168, 144, MeasuredRoom.None with { Diagonal2 = In(222) })));

        // A measured room's takeoff is from the drawn size and says so.
        (Sketch sketch, _) = Sample();
        Room room = TheRoom(sketch);
        Sketch measured = sketch.WithEntity(room.Box with { Room = room.Box.Room! with { Measured = MeasuredRoom.None with { Diagonal1 = In(221) } } });
        Assert.Equal("from the drawn size", Takeoff(measured)[0].Note);
    }

    // ---- Test 9 and 10: bays, and the frame bought ---------------------------------------------

    [Fact]
    public void Example_1s_bays_are_37()
        => Assert.Equal(37, Takeoff(Sample().Sketch).Single(line => line.Finish == Finish.InsulationBays).Count);

    [Fact]
    public void Example_1s_frame_is_the_expectations_and_buys_its_boards()
    {
        (Sketch sketch, JsonElement expected) = Sample();
        JsonElement framing = expected.GetProperty("framing");
        FramingOptions options = CodeCheck.Framing(CodeCheck.Of(sketch, CodePacks.None), Library);

        foreach (JsonElement wall in framing.GetProperty("walls").EnumerateArray())
        {
            Wall actual = Wall.All(sketch).Single(w => w.Name == wall.GetProperty("name").GetString());
            Assert.Equal(wall.GetProperty("summary").GetString(), FramingList.Frame(sketch, actual, Library, options).Summary);
        }

        ImmutableArray<WallDiff> diffs = FramingDiff.Of(sketch, Library, CodePacks.None);
        Assert.All(diffs, diff => Assert.Empty(diff.Out));
        ImmutableArray<ShoppingListRow> boards = ShoppingList.Of(FramingDiff.CutRows(diffs));
        foreach (JsonProperty lumber in framing.GetProperty("boards").EnumerateObject())
        {
            ShoppingListRow row = boards.Single(r => r.Material == lumber.Name);
            Assert.Equal(lumber.Value.GetString(), row.BuyText);
            Assert.Equal(framing.GetProperty("boardFeet").GetProperty(lumber.Name).GetString(), row.BoughtText);
        }

        // 67 pieces of 2x4 (the design file counts them) and 4 of 2x6.
        Assert.Equal(67, FramingDiff.CutRows(diffs).Where(row => row.Material == "2x4").Sum(row => row.Quantity));
        Assert.Equal(4, FramingDiff.CutRows(diffs).Where(row => row.Material == "2x6").Sum(row => row.Quantity));
    }
}
