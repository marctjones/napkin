using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;
using Napkin.Core.RulesEngine;
using Napkin.Modules.Furniture;

namespace Napkin.Modules.Building.Tests;

/// <summary>
/// Code-check routing by side and bearing (docs/design/renovation-sketches.md §4.3, test 5), the
/// framing diff (§6.3, test 6) and worked example 2 against its hand-derived expectations (test 10),
/// under the SYNTHETIC pack <c>us-zz-reno</c> (NOT CODE VALUES) and the shipped Connecticut pack.
/// </summary>
public class RenovationFramingTests
{
    static readonly MaterialsLibrary Library = MaterialsLibrary.Shipped;
    static readonly LayerId WallLayer = LayerId.New();
    static readonly LayerId OpeningLayer = LayerId.New();
    static readonly CodePacks Reno = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "reno")]);
    static readonly CodePacks One = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "CodePacks", "one")]);
    static readonly CodePacks Shipped = CodePacks.Discover([Path.Combine(AppContext.BaseDirectory, "RealPacks")]);
    static readonly CodeChoice RenoCode = new("us-zz-reno", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));
    static readonly CodeChoice FrameCode = new("us-zz-frame", 1, CodeMode.Locked, new DateOnly(2026, 9, 25));

    static Length In(long whole, long numerator = 0, long denominator = 1) => Length.Inches(whole, numerator, denominator);

    /// <summary>Example 2: a 12 ft 2x4 wall, 8 ft tall, and a 3 ft × 4 ft window, sill 3 ft, centred.</summary>
    static (Sketch Sketch, Box Wall, Box Window) Example2(Phase wall, Phase window, WallInputs? inputs, CodeChoice? code = null)
    {
        Box wallBox = new Box(EntityId.New(), WallLayer, Point3.Origin, In(144), In(3, 1, 2), In(96), BoxFace.Top, Angle.Zero)
        {
            Name = "Wall 1",
            Phase = wall,
            WallInputs = inputs,
        };
        Box windowBox = new Box(EntityId.New(), OpeningLayer, new Point3(In(54), Length.Zero, In(36)), In(36), In(3, 1, 2), In(48), BoxFace.Top, Angle.Zero)
        {
            Name = "Window 1",
            Phase = window,
        };
        Sketch sketch = Sketch.Empty
            .WithLayer(new Layer(WallLayer, BuildingLayers.Wall))
            .WithLayer(new Layer(OpeningLayer, BuildingLayers.Opening))
            .WithEntity(wallBox)
            .WithEntity(windowBox) with
        {
            Code = code ?? RenoCode,
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 },
        };
        return (sketch, wallBox, windowBox);
    }

    static WallInputs Said(WallSide? side, bool? bearing, TypedHeader? header = null)
        => new("zz-roof", null) { Side = side, Bearing = bearing, Header = header };

    static OpeningCheck Only(Sketch sketch, CodePacks packs) => Assert.Single(CodeCheck.Of(sketch, packs));

    // ---- Test 5: the five rows of §4.3 ------------------------------------------------------

    [Fact]
    public void The_synthetic_renovation_pack_loads()
    {
        Assert.Empty(Reno.Invalid.SelectMany(invalid => invalid.Problems));
        Assert.Equal("us-zz-reno", Assert.Single(Reno.Loaded).Manifest.Id);
    }

    [Fact]
    public void An_exterior_bearing_wall_asks_the_exterior_table()
    {
        (Sketch sketch, _, _) = Example2(Phase.New, Phase.New, Said(WallSide.Exterior, true));

        HeaderResult.Sized sized = Assert.IsType<HeaderResult.Sized>(Only(sketch, Reno).Result);
        Assert.Equal(new MemberSpec(2, "2x6"), sized.Header);
        Assert.Equal("ZZ-RENO-HEADER", sized.Citation.Table);
        Assert.Equal("Header (2) 2x6, 1 jack stud and 1 king stud each side.", CodeCheck.Words(Only(sketch, Reno), Library).Headline);
        Assert.Equal("(2) 2x6, 1 jack and 1 king each side (Table ZZ-RENO-HEADER row reno.a)", CodeCheck.Short(Only(sketch, Reno)));
    }

    [Fact]
    public void An_interior_bearing_wall_asks_the_interior_table_and_a_pack_without_one_says_no_data_naming_it()
    {
        (Sketch sketch, _, _) = Example2(Phase.New, Phase.New, Said(WallSide.Interior, true));

        HeaderResult.Sized sized = Assert.IsType<HeaderResult.Sized>(Only(sketch, Reno).Result);
        Assert.Equal(new MemberSpec(2, "2x8"), sized.Header);
        Assert.Equal("ZZ-RENO-INTERIOR", sized.Citation.Table);

        Sketch framed = sketch with { Code = FrameCode };
        HeaderResult.NoData none = Assert.IsType<HeaderResult.NoData>(Only(framed, One).Result);
        Assert.Equal(NoDataReason.NoTableForWallKind, none.Reason);
        Assert.Contains("no header table for interior-bearing walls", none.Explanation, StringComparison.Ordinal);
        Assert.Equal(["zz-roof"], CodeCheck.SupportsChoices(Reno.Loaded[0], WallKind.InteriorBearing));
        Assert.Empty(CodeCheck.SupportsChoices(One.Loaded.Single(pack => pack.Manifest.Id == "us-zz-frame"), WallKind.InteriorBearing));
    }

    [Fact]
    public void A_wall_that_is_not_bearing_is_not_checked_and_its_openings_take_the_header_typed()
    {
        (Sketch sketch, Box wall, _) = Example2(Phase.New, Phase.New, Said(WallSide.Exterior, false, new TypedHeader(2, "2x6")));

        OpeningCheck check = Only(sketch, Reno);
        Assert.Null(check.Result);
        Assert.Equal("Wall 1 is marked not bearing, so napkin does not size this header from the code. Header: (2) 2x6, your choice.", check.NotChecked);
        Assert.Equal("Not checked: " + check.NotChecked, CodeCheck.Words(check, Library).Headline);
        Assert.Equal("not checked: not bearing, (2) 2x6 your choice", CodeCheck.Short(check));
        Assert.StartsWith("Wall 1 is marked not bearing, so napkin does not size this header from the code. No header chosen", CodeCheck.NotBearingText(new Wall(wall with { WallInputs = null })), StringComparison.Ordinal);

        // The frame uses the typed header with one jack and one king each side, said so.
        WallFraming framing = FramingList.Frame(sketch, new Wall(wall), Library, CodeCheck.Framing(CodeCheck.Of(sketch, Reno), Library));
        Assert.Equal(new FramingPiece(FramingRole.Header, 2, In(39), Library.Items.OfType<LumberStock>().Single(l => l.Name == "2x6")), Assert.Single(framing.Pieces, piece => piece.Role == FramingRole.Header));
        Assert.Equal(2, framing.Count(FramingRole.JackStud));
        Assert.Equal(2, framing.Count(FramingRole.KingStud));
        Assert.Contains(CodeCheck.ChosenHeaderJacks, framing.Notes);
        Assert.DoesNotContain(FramingOptions.PlaceholderJacks, framing.Notes);

        // Whatever the side, and even with no code at all.
        (Sketch interior, _, _) = Example2(Phase.New, Phase.New, Said(WallSide.Interior, false));
        Assert.Null(Only(interior with { Code = null }, CodePacks.None).Result);

        // No header chosen: the header buys nothing and says so.
        OpeningCheck none = Only(interior, Reno);
        Assert.Equal("Wall 1 is marked not bearing, so napkin does not size this header from the code. No header chosen: choose one under Header in the wall's panel; until then the header buys nothing.", none.NotChecked);
        Assert.Equal("not checked: not bearing, no header chosen", CodeCheck.Short(none));
        WallFraming bare = FramingList.Frame(interior, Assert.Single(Wall.All(interior)), Library, CodeCheck.Framing(CodeCheck.Of(interior, Reno), Library));
        Assert.Null(Assert.Single(bare.Pieces, piece => piece.Role == FramingRole.Header).Stock);

        // A lumber the library does not carry buys nothing either.
        (Sketch odd, _, _) = Example2(Phase.New, Phase.New, Said(WallSide.Interior, false, new TypedHeader(1, "3x9")));
        Assert.Null(Assert.Single(FramingList.Frame(odd, Assert.Single(Wall.All(odd)), Library, CodeCheck.Framing(CodeCheck.Of(odd, Reno), Library)).Pieces, piece => piece.Role == FramingRole.Header).Stock);
    }

    [Theory]
    [InlineData(null, true, "side", "Say whether Wall 1 is exterior or interior (Part panel).")]
    [InlineData(WallSide.Exterior, null, "bearing", "Say whether Wall 1 is bearing (Part panel).")]
    [InlineData(null, null, "side,bearing", "Say whether Wall 1 is exterior or interior (Part panel). Say whether Wall 1 is bearing (Part panel).")]
    public void A_side_or_a_bearing_not_said_is_an_input_missing_naming_it(WallSide? side, bool? bearing, string inputs, string sentence)
    {
        (Sketch sketch, _, _) = Example2(Phase.New, Phase.New, Said(side, bearing));

        HeaderResult.InputMissing missing = Assert.IsType<HeaderResult.InputMissing>(Only(sketch, Reno).Result);
        Assert.Equal(inputs.Split(','), missing.Inputs);
        Assert.Equal(sentence, missing.Explanation);
        Assert.Equal("ZZ RENO", missing.Code.ShortName);

        Assert.StartsWith("not checked: ", CodeCheck.Short(Only(sketch, Reno)), StringComparison.Ordinal);
        Assert.Contains(side is null ? "which side the wall is on" : "whether the wall is bearing", CodeCheck.Short(Only(sketch, Reno)), StringComparison.Ordinal);

        // With no wall inputs at all, the same.
        (Sketch nothing, _, _) = Example2(Phase.New, Phase.New, null);
        Assert.IsType<HeaderResult.InputMissing>(Only(nothing, Reno).Result);
    }

    [Fact]
    public void Checks_that_are_not_checked_are_not_changes_of_a_result()
    {
        (Sketch bearing, _, _) = Example2(Phase.New, Phase.New, Said(WallSide.Exterior, true));
        (Sketch notBearing, _, _) = Example2(Phase.New, Phase.New, Said(WallSide.Exterior, false));

        Assert.Empty(CodeCheck.Changes(CodeCheck.Of(bearing, Reno), CodeCheck.Of(notBearing, Reno)));
        Assert.Empty(CodeCheck.Report(CodeCheck.Of(notBearing, Reno), CodeCheck.Of(bearing, Reno)).Changes);
    }

    // ---- Test 6: the framing diff ------------------------------------------------------------

    static WallDiff Diff(Sketch sketch, CodePacks? packs = null) => Assert.Single(FramingDiff.Of(sketch, Library, packs ?? Reno));

    static (FramingRole, int, Length, string?)[] Rows(ImmutableArray<FramingPiece> pieces)
        => [.. pieces.Select(piece => (piece.Role, piece.Quantity, piece.Length, piece.Stock?.Name))];

    [Fact]
    public void A_new_window_in_an_existing_wall_is_new_material_and_two_studs_out()
    {
        (Sketch sketch, _, _) = Example2(Phase.Existing, Phase.New, Said(WallSide.Exterior, true));

        WallDiff diff = Diff(sketch);

        // Worked in samples/window-in-existing-wall.design.md.
        Assert.Equal(
            [
                (FramingRole.KingStud, 2, In(91, 1, 2), "2x4"),
                (FramingRole.JackStud, 2, In(82, 1, 2), "2x4"),
                (FramingRole.Header, 2, In(39), "2x6"),
                (FramingRole.RoughSill, 1, In(36), "2x4"),
                (FramingRole.CrippleBelow, 2, In(33), "2x4"),
                (FramingRole.CrippleAbove, 2, In(3, 1, 2), "2x4"),
            ],
            Rows(diff.New));
        Assert.Equal([(FramingRole.Stud, 2, In(91, 1, 2), "2x4")], Rows(diff.Out));
        Assert.True(diff.FromExisting);
        Assert.Equal("new — 2 king studs, 2 jack studs, header, sill, 4 cripples; out — 2 studs", diff.Sentence);
        Assert.Equal("assuming a regular 16\" layout in the existing wall", diff.Assumption);
        Assert.Equal(
            ["Wall 1: 2 studs 7'-7 1/2\" (2x4) come out (assuming a regular 16\" layout in the existing wall)"],
            FramingDiff.Demolition([diff]).Select(line => line.Text));
    }

    [Fact]
    public void A_new_wall_is_all_new_and_a_demolished_wall_all_out()
    {
        (Sketch fresh, Box wall, _) = Example2(Phase.New, Phase.New, Said(WallSide.Exterior, true));
        WallDiff all = Diff(fresh);
        WallFraming frame = FramingList.Frame(fresh, new Wall(wall), Library, CodeCheck.Framing(CodeCheck.Of(fresh, Reno), Library));
        Assert.Equal(Rows(frame.Pieces).OrderBy(row => row.Item1).ThenByDescending(row => row.Item3), Rows(all.New));
        Assert.Empty(all.Out);
        Assert.False(all.FromExisting);
        Assert.Equal("nothing", all.Sentence[(all.Sentence.LastIndexOf('—') + 2)..]);

        // Demolish the wall (the window with it): nothing is new, its whole frame as it is comes out:
        // ten studs, three plates — the window was new, so it is not in the wall as it is.
        (Sketch gone, _, _) = Example2(Phase.Demolish, Phase.Demolish, Said(WallSide.Exterior, true));
        WallDiff out_ = Diff(gone);
        Assert.Empty(out_.New);
        Assert.Equal(
            [
                (FramingRole.BottomPlate, 1, In(144), "2x4"),
                (FramingRole.TopPlate, 2, In(144), "2x4"),
                (FramingRole.KingStud, 2, In(91, 1, 2), "2x4"),
                (FramingRole.JackStud, 2, In(82, 1, 2), "2x4"),
                (FramingRole.Header, 2, In(39), "2x6"),
                (FramingRole.RoughSill, 1, In(36), "2x4"),
                (FramingRole.CrippleBelow, 2, In(33), "2x4"),
                (FramingRole.CrippleAbove, 2, In(3, 1, 2), "2x4"),
                (FramingRole.Stud, 8, In(91, 1, 2), "2x4"),
            ],
            Rows(out_.Out).OrderBy(row => row.Item1 == FramingRole.Stud ? 1 : 0).ToArray());
        Assert.StartsWith("new — nothing; out — 3 plates, ", out_.Sentence, StringComparison.Ordinal);
        Assert.True(out_.Changes);
    }

    [Fact]
    public void An_existing_wall_with_no_change_is_on_neither_list_and_a_closed_up_window_fills_with_studs()
    {
        (Sketch still, _, _) = Example2(Phase.Existing, Phase.Existing, Said(WallSide.Exterior, true));
        WallDiff none = Diff(still);
        Assert.False(none.Changes);
        Assert.Empty(FramingDiff.CutRows([none]));
        Assert.Empty(FramingDiff.Demolition([none]));

        // Close the window up: the studs that fill it are new, its header, kings, jacks, sill and
        // cripples come out — nothing special-cased.
        (Sketch closed, _, _) = Example2(Phase.Existing, Phase.Demolish, Said(WallSide.Exterior, true));
        WallDiff filled = Diff(closed);
        Assert.Equal([(FramingRole.Stud, 2, In(91, 1, 2), "2x4")], Rows(filled.New));
        Assert.Equal(
            [FramingRole.KingStud, FramingRole.JackStud, FramingRole.Header, FramingRole.RoughSill, FramingRole.CrippleBelow, FramingRole.CrippleAbove],
            filled.Out.Select(piece => piece.Role));
        Assert.Equal("new — 2 studs; out — 2 king studs, 2 jack studs, header, sill, 4 cripples", filled.Sentence);

        // With no code the old header was never sized: it comes out with no lumber named.
        WallDiff unsized = Diff(closed with { Code = null }, CodePacks.None);
        Assert.Contains("Wall 1: 1 header 3'-3\" come out (assuming a regular 16\" layout in the existing wall)", FramingDiff.Demolition([unsized]).Select(line => line.Text));
    }

    [Fact]
    public void A_diff_needs_one_side()
        => Assert.Throws<ArgumentException>(() => FramingDiff.Between(null, null));

    // ---- Test 10: example 2's sample, against its hand-derived expectations -------------------

    static (Sketch Sketch, JsonElement Expected) Sample()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "samples");
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(directory, "window-in-existing-wall.scene.json"))).Sketch;
        using JsonDocument expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "window-in-existing-wall.expected.json")));
        return (sketch, expected.RootElement.Clone());
    }

    /// <summary>The sample under the synthetic pack: its code swapped, what the wall supports and the snow typed here, not in the sample.</summary>
    static Sketch UnderReno(Sketch sketch)
    {
        Box wall = Assert.Single(Wall.All(sketch)).Box;
        return sketch.WithEntity(wall with { WallInputs = wall.WallInputs! with { Supports = "zz-roof" } }) with
        {
            Code = RenoCode,
            Site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 },
        };
    }

    [Fact]
    public void Example_2s_framing_diff_is_the_expectations_row_for_row()
    {
        (Sketch sample, JsonElement expected) = Sample();
        WallDiff diff = Diff(UnderReno(sample));
        JsonElement framing = expected.GetProperty("framingDiff");

        static (string, int, long, string) Row(JsonElement row)
            => (row.GetProperty("role").GetString()!, row.GetProperty("quantity").GetInt32(), row.GetProperty("lengthUnits").GetInt64(), row.GetProperty("stock").GetString()!);

        Assert.Equal(framing.GetProperty("new").EnumerateArray().Select(Row), diff.New.Select(piece => (piece.Role.ToString(), piece.Quantity, piece.Length.Units, piece.Stock!.Name)));
        Assert.Equal(framing.GetProperty("out").EnumerateArray().Select(Row), diff.Out.Select(piece => (piece.Role.ToString(), piece.Quantity, piece.Length.Units, piece.Stock!.Name)));
        Assert.Equal(framing.GetProperty("sentence").GetString(), diff.Sentence);
        Assert.Equal(framing.GetProperty("assumption").GetString(), diff.Assumption);
        Assert.Equal(expected.GetProperty("demolition").EnumerateArray().Select(line => line.GetString()), FramingDiff.Demolition([diff]).Select(line => line.Text));

        // The new material bought: boards per lumber, as the shopping list says them.
        ImmutableArray<ShoppingListRow> boards = ShoppingList.Of(FramingDiff.CutRows([diff]));
        foreach (JsonProperty lumber in framing.GetProperty("boards").EnumerateObject())
        {
            Assert.Equal(lumber.Value.GetString(), boards.Single(row => row.Material == lumber.Name).BuyText);
        }

        Assert.Equal(2, boards.Length);
    }

    [Fact]
    public void Example_2s_checks_are_the_expectations_under_both_packs_and_both_bearing_answers()
    {
        (Sketch sample, JsonElement expected) = Sample();
        JsonElement words = expected.GetProperty("codeCheck");

        Assert.Equal(Phase.Existing, Assert.Single(Wall.All(sample)).Box.Phase);
        Assert.Equal(words.GetProperty("synthetic").GetString(), CodeCheck.Words(Only(UnderReno(sample), Reno), Library).Headline);
        Assert.StartsWith(words.GetProperty("shipped").GetString()!, CodeCheck.Words(Only(sample, Shipped), Library).Headline, StringComparison.Ordinal);

        Box wall = Assert.Single(Wall.All(sample)).Box;
        Sketch notBearing = sample.WithEntity(wall with { WallInputs = wall.WallInputs! with { Bearing = false } });
        Assert.Equal(words.GetProperty("notBearing").GetString(), CodeCheck.Words(Only(notBearing, Shipped), Library).Headline);
        Sketch unsaid = sample.WithEntity(wall with { WallInputs = wall.WallInputs! with { Bearing = null } });
        Assert.Equal("Not checked: " + words.GetProperty("bearingNotSaid").GetString(), CodeCheck.Words(Only(unsaid, Shipped), Library).Headline);
        Assert.Equal("not checked: whether the wall is bearing not entered", CodeCheck.Short(Only(unsaid, Shipped)));
        Assert.StartsWith("Table ZZ-RENO-HEADER, ", CodeCheck.Words(Only(UnderReno(unsaid), Reno), Library).Citation, StringComparison.Ordinal);

        // Bracing on the wall as it will be: two 54 in segments either side of the new window.
        WallLine line = WallLine.Of(sample, new Wall(wall));
        Assert.Equal(
            expected.GetProperty("bracingSegments").EnumerateArray().Select(segment => (segment.GetProperty("label").GetString(), segment.GetProperty("lengthUnits").GetInt64())),
            line.Segments.Select(segment => ((string?)segment.Label, segment.Length.Units)));
    }
}
