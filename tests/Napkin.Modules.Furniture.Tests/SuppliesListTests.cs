using System.Collections.Immutable;
using System.Text.Json;

using Napkin.Core.Geometry;
using Napkin.Core.Project;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Hardware, supplies and the shopping list's sections below the boards (docs/design/joinery-and-fasteners.md
/// &#xA7;7.5, &#xA7;8). Expectations are hand arithmetic in comments or the hand-derived sample file.
/// </summary>
public sealed class SuppliesListTests
{
    private static readonly PlanAxes Axes = new(PartDimension.Length, PartDimension.Thickness);

    private static Box Part(int id, string name, int quantity, params HardwareItem[] hardware) => new(
        new EntityId(new Guid(id, 0, 0, new byte[8])),
        LayerId.Default,
        Point3.Origin,
        new Length(1024),
        new Length(1024),
        new Length(1024),
        BoxFace.Top,
        Angle.Zero)
    {
        Name = name,
        Part = new Part(null, null, quantity, Axes) { Hardware = [.. hardware] },
    };

    private static Sketch Of(params Box[] boxes) => boxes.Aggregate(Sketch.Empty, (sketch, box) => sketch.WithEntity(box));

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void Hardware_is_summed_by_exact_name_times_the_parts_quantity()
    {
        // Door: 2 hinges x 2 copies = 4; Cabinet: 3 hinges x 1 = 3, and a "hinge" (other case) x 1 is its own line.
        // "Hinge" total 4 + 3 = 7 in the order first seen (the door has the lower id); "Shelf pin" 4 x 1 = 4.
        Sketch sketch = Of(
            Part(1, "Door", 2, new HardwareItem("Hinge", 2)),
            Part(2, "Cabinet", 1, new HardwareItem("Hinge", 3), new HardwareItem("Shelf pin", 4)),
            Part(3, "Other", 1, new HardwareItem("hinge", 1)),
            Part(4, "Plain", 1));

        ImmutableArray<HardwareRow> rows = HardwareList.Of(sketch);

        Assert.Equal([("Hinge", 7), ("Shelf pin", 4), ("hinge", 1)], rows.Select(row => (row.Name, row.Count)));
        Assert.Equal("Door, Cabinet", rows[0].For);
        Assert.Equal("4 + 3 = 7", rows[0].Derivation);
    }

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void The_glue_line_counts_glued_joints_and_says_nothing_without_joints()
    {
        Assert.Empty(SuppliesList.Of(Sketch.Empty));

        Joint Joint(int id, bool glue) => new(
            new RelationshipId(new Guid(id, 1, 0, new byte[8])),
            new FeatureRef(new EntityId(Guid.NewGuid()), BoxFeature.Face(BoxFace.East)),
            new FeatureRef(new EntityId(Guid.NewGuid()), BoxFeature.Face(BoxFace.West)),
            JointType.Butt,
            null,
            Fastening.None,
            glue);

        // One joint, glued: "1 of 1 joint" (singular). Three joints, two glued: "2 of 3 joints".
        Assert.Equal("Glue: 1 of 1 joint", Assert.Single(SuppliesList.Of(Sketch.Empty.WithRelationship(Joint(1, true)))).Item);
        Sketch three = Sketch.Empty.WithRelationship(Joint(1, true)).WithRelationship(Joint(2, false)).WithRelationship(Joint(3, true));
        Assert.Equal("Glue: 2 of 3 joints", Assert.Single(SuppliesList.Of(three)).Item);
    }

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void Typed_supplies_follow_hardware_and_keep_their_notes()
    {
        Sketch sketch = Of(Part(1, "Door", 1, new HardwareItem("Hinge", 2))) with
        {
            Supplies = [new SupplyLine("Finish", "one quart"), new SupplyLine("Sandpaper", string.Empty)],
        };

        Assert.Equal(
            [
                ExtraSection.Hardware, ExtraSection.Supplies, ExtraSection.Supplies,
            ],
            SuppliesList.Of(sketch).Select(row => row.Section));
        Assert.Equal(["Hinge", "Finish", "Sandpaper"], SuppliesList.Of(sketch).Select(row => row.Item));
        Assert.Equal("one quart", SuppliesList.Of(sketch)[1].For);
        Assert.Equal(string.Empty, SuppliesList.Of(sketch)[1].CountText);
    }

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void The_csv_is_the_rows_field_for_field_and_parses_back()
    {
        Sketch sketch = Of(Part(1, "Door", 1, new HardwareItem("Hinge, 3 in", 2))) with { Supplies = [new SupplyLine("Glue \"PVA\"", "1 pt")] };
        ImmutableArray<ExtraRow> rows = SuppliesList.Of(sketch);

        string[][] parsed = [.. CutListCsv.Parse(SuppliesList.ToCsv(rows)).Select(line => line.ToArray())];

        Assert.Equal(SuppliesList.Statement, parsed[0][0]);
        Assert.Equal(SuppliesList.Header.Split(','), parsed[1]);
        Assert.Equal(rows.Select(row => SuppliesList.Fields(row).ToArray()), parsed.Skip(2));
        Assert.Equal(["Hardware", "Hinge, 3 in", string.Empty, "2", string.Empty, string.Empty, "Door"], parsed[2]);
    }

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void Every_fastener_kind_has_a_name_and_an_empty_design_lists_nothing()
    {
        Assert.Empty(SuppliesList.Of(Sketch.Empty));
        Assert.Equal(
            ["Pocket screw", "Wood screw", "Brad", "Nail", "Dowel", "Biscuit", "Tabletop clip"],
            Enum.GetValues<FastenerKind>().Select(SuppliesList.KindName));
    }

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void The_coffee_table_sample_lists_its_hardware_supplies_and_csv_as_worked_by_hand()
    {
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("diy-coffee-table-drawers"))).Sketch;
        using FileStream stream = File.OpenRead(Path.Combine(ExpectedFixture.Directory, "diy-coffee-table-drawers.expected.json"));
        JsonElement expected = JsonDocument.Parse(stream).RootElement;

        ImmutableArray<HardwareRow> hardware = HardwareList.Of(sketch);
        JsonElement[] wantHardware = [.. expected.GetProperty("hardware").EnumerateArray()];
        Assert.Equal(wantHardware.Length, hardware.Length);
        for (int i = 0; i < hardware.Length; i++)
        {
            Assert.Equal(wantHardware[i].GetProperty("name").GetString(), hardware[i].Name);
            Assert.Equal(wantHardware[i].GetProperty("count").GetInt32(), hardware[i].Count);
            Assert.Equal(wantHardware[i].GetProperty("for").GetString(), hardware[i].For);
            Assert.False(string.IsNullOrWhiteSpace(wantHardware[i].GetProperty("derivation").GetString()));
        }

        ImmutableArray<ExtraRow> rows = SuppliesList.Of(sketch);
        JsonElement[] wantSupplies = [.. expected.GetProperty("supplies").EnumerateArray()];
        ExtraRow[] supplies = [.. rows.Where(row => row.Section == ExtraSection.Supplies)];
        Assert.Equal(wantSupplies.Select(want => want.GetProperty("item").GetString()), supplies.Select(row => row.Item));
        Assert.Equal(wantSupplies.Select(want => want.GetProperty("note").GetString()), supplies.Select(row => row.For));

        string[] wantCsv = [.. expected.GetProperty("suppliesCsv").EnumerateArray().Select(line => line.GetString()!)];
        Assert.Equal(wantCsv, SuppliesList.ToCsv(rows).TrimEnd('\n').Split('\n'));
    }

    [Fact]
    [Trait("Feature", "CUT-013")]
    public void With_no_size_chosen_the_sample_says_so_and_has_no_pack_arithmetic()
    {
        Sketch sketch = Assert.IsType<Loaded>(SceneReader.ReadFile(ExpectedFixture.ScenePath("diy-coffee-table-drawers"))).Sketch with { FastenerChoices = [] };

        ExtraRow[] fasteners = [.. SuppliesList.Of(sketch).Where(row => row.Section == ExtraSection.Fasteners)];

        // The counts do not need a size (27, 6, 8, 24, 10); only the size and pack columns change.
        Assert.Equal(["27", "6", "8", "24", "10"], fasteners.Select(row => row.CountText));
        Assert.All(fasteners, row =>
        {
            Assert.Equal("size not chosen", row.Size);
            Assert.Equal(string.Empty, row.PackText);
            Assert.Equal(string.Empty, row.PacksText);
        });
    }
}
