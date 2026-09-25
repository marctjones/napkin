using System.Text;
using System.Text.Json.Nodes;

namespace Napkin.Core.RulesEngine.Tests;

/// <summary>
/// Loading, validation and errors-as-values (design §1, §9), over the synthetic fixtures —
/// SYNTHETIC TEST DATA - NOT CODE VALUES. Each refused pack is a one-edit mutation of a valid one,
/// so the edit is the only thing wrong with it.
/// </summary>
public class LoaderTests
{
    private static InMemoryPackSource Edit(string path, Action<JsonNode> edit)
    {
        InMemoryPackSource source = Fx.Source();
        JsonNode node = JsonNode.Parse(source.Text(path))!;
        edit(node);
        return source.With(path, node.ToJsonString());
    }

    private static InMemoryPackSource EditText(string path, string find, string replace)
    {
        InMemoryPackSource source = Fx.Source();
        string text = source.Text(path);
        Assert.Contains(find, text, StringComparison.Ordinal);
        return source.With(path, text.Replace(find, replace, StringComparison.Ordinal));
    }

    private static JsonNode Row(JsonNode table, string id)
        => table["rows"]!.AsArray().Single(r => (string)r!["id"]! == id)!;

    private static PackLoadResult.Invalid Refused(IPackSource source, string pack, string fragment)
    {
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(source, pack));
        Assert.True(
            invalid.Problems.Any(p => p.ToString().Contains(fragment, StringComparison.Ordinal)),
            $"expected a problem containing \"{fragment}\"; got:{Environment.NewLine}{invalid}");
        return invalid;
    }

    [Fact]
    [Trait("Feature", "RUL-001")]
    public void A_pack_loads_from_its_directory_and_reports_its_identity()
    {
        LoadedPack pack = Fx.Load("us-zz-base");
        PackManifest m = pack.Manifest;
        Assert.Equal("us-zz-base", m.Id);
        Assert.Equal(1, m.Revision);
        Assert.Equal(new Jurisdiction("US", "ZZ", null), m.Jurisdiction);
        Assert.Equal(new BaseCode("ICC", "IRC", 2099), m.BaseCode);
        Assert.Equal("IRC 2099", m.BaseCode.ToString());
        Assert.Equal(new DateOnly(2099, 1, 1), m.Adoption.InForceFrom);
        Assert.Null(m.Adoption.InForceTo);
        Assert.Equal(AppliesTo.PermitApplicationDate, m.Adoption.AppliesTo);
        Assert.Equal(ReviewStatus.Unreviewed, m.Review.Status);
        Assert.Equal(ValueList.Of("zz-base-2099", "amendments"), m.Layers);
        SourceDocument source = Assert.Single(m.Sources);
        Assert.Equal("synthetic printing, no errata", source.Printing);

        HeaderSizingTable table = Assert.Single(pack.Tables);
        Assert.Equal(Fx.Table, table.Designation);
        Assert.Equal(WallKind.ExteriorBearing, table.WallKind);
        Assert.Equal(20, table.Rows.Count);
        Assert.All(table.Rows, r => Assert.Equal(CitationLayer.ModelCode, r.Layer));
        Assert.Equal(["supports", "groundSnowLoad", "buildingWidth", "headerSpan", "ultimateWindSpeed"], table.RequiredInputs);
        Assert.Equal(new AdoptedCodeRef("us-zz-base", 1, "ZZ BASE TEST", "IRC 2099", ReviewStatus.Unreviewed), pack.Code);
        Assert.Contains("UNREVIEWED", pack.Code.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Lengths_are_stored_exactly_as_integer_units()
    {
        HeaderRow row = Fx.Load("us-zz-base").Tables[0].Rows.Single(r => r.Id == "roof.s33.w22.m1");
        // 7'-7" = 91 inches = 91 * 1024 units, an integer; there is no floating-point step anywhere.
        Assert.Equal(ColumnType.Length, row.Inputs["headerSpan"].Type);
        Assert.Equal(91L * 1024, row.Inputs["headerSpan"].Magnitude);
        Assert.Equal(22L * 12 * 1024, row.Inputs["buildingWidth"].Magnitude);
        Assert.Equal(CellValue.Whole(ColumnType.Psf, 33), row.Inputs["groundSnowLoad"]);
        Assert.Equal("7'-7\"", row.Inputs["headerSpan"].ToString());
        Assert.Equal("33 psf", row.Inputs["groundSnowLoad"].ToString());
        Assert.Equal("150 mph", CellValue.Whole(ColumnType.Mph, 150).ToString());
    }

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void State_overlay_adds_amends_and_deletes_rows_and_amends_the_table()
    {
        HeaderSizingTable table = Fx.Load("us-zz-state").Tables[0];
        Assert.Equal(20, table.Rows.Count); // 20 base + 1 added - 1 deleted

        HeaderRow added = table.Rows.Single(r => r.Id == "roof.s99.w44.m4");
        Assert.Equal(CitationLayer.StateAmendment, added.Layer);
        Assert.Equal(new MemberSpec(4, "2x13"), added.Header);
        Assert.Equal("zz-synthetic-state", added.Source.SourceId);
        Assert.Equal("synthetic state p. 1, added row", added.Source.Location);

        HeaderRow amended = table.Rows.Single(r => r.Id == "roof.s33.w22.m1");
        Assert.Equal(CitationLayer.StateAmendment, amended.Layer);
        Assert.Equal(new MemberSpec(1, "2x8"), amended.Header);

        Assert.DoesNotContain(table.Rows, r => r.Id == "floor.s99.w44.m2");

        Assert.Equal("SYNTHETIC TEST TABLE (state amended) - NOT CODE VALUES", table.Title);
        Assert.Equal(CitationLayer.StateAmendment, table.Layer);
        Assert.Equal(11L * 12 * 1024, table.Inputs.Single(c => c.Name == "buildingWidth").Domain!.Min.Magnitude);

        HeaderRow untouched = table.Rows.Single(r => r.Id == "roof.s66.w44.m2");
        Assert.Equal(CitationLayer.ModelCode, untouched.Layer);
        Assert.Equal("zz-synthetic-base", untouched.Source.SourceId);
    }

    [Fact]
    [Trait("Feature", "RUL-006")]
    public void Municipal_overlay_composes_over_the_state_overlay()
    {
        HeaderSizingTable town = Fx.Load("us-zz-town").Tables[0];
        HeaderSizingTable state = Fx.Load("us-zz-state").Tables[0];

        HeaderRow amended = town.Rows.Single(r => r.Id == "roof.s66.w22.m2");
        Assert.Equal(CitationLayer.MunicipalAmendment, amended.Layer);
        Assert.Equal(new MemberSpec(2, "2x10"), amended.Header);
        Assert.Equal("zz-synthetic-town", amended.Source.SourceId);

        // The state's operations flow through, still cited as the state's.
        Assert.Equal(CitationLayer.StateAmendment, town.Rows.Single(r => r.Id == "roof.s99.w44.m4").Layer);

        // No-change: every row the town does not touch equals the state's composed row (design §6.2).
        Assert.Equal(
            state.Rows.Where(r => r.Id != "roof.s66.w22.m2").Select(r => (r.Id, r.Header, r.JackStuds, r.KingStuds, r.Layer, r.Source)),
            town.Rows.Where(r => r.Id != "roof.s66.w22.m2").Select(r => (r.Id, r.Header, r.JackStuds, r.KingStuds, r.Layer, r.Source)));
    }

    [Fact]
    public void An_empty_overlay_is_the_identity()
    {
        HeaderSizingTable composed = Fx.Load("us-zz-base").Tables[0];
        InMemoryPackSource source = Fx.Source();
        JsonNode table = JsonNode.Parse(source.Text(Fx.TablePath))!;
        Assert.Equal(
            table["rows"]!.AsArray().Select(r => (string)r!["id"]!).Order(StringComparer.Ordinal),
            composed.Rows.Select(r => r.Id));
    }

    [Fact]
    public void A_pack_with_no_tables_loads_and_is_honestly_empty()
    {
        LoadedPack pack = Fx.Load("us-zz-empty");
        Assert.Empty(pack.Tables);
    }

    [Fact]
    public void Loading_is_deterministic_and_independent_of_row_order_in_the_file()
    {
        LoadedPack a = Fx.Load("us-zz-state");
        InMemoryPackSource shuffled = Edit(Fx.TablePath, t =>
        {
            JsonArray rows = t["rows"]!.AsArray();
            List<JsonNode> list = [.. rows.Select(r => r!.DeepClone())];
            rows.Clear();
            foreach (JsonNode r in list.AsEnumerable().Reverse())
            {
                rows.Add(r);
            }
        });
        LoadedPack b = Fx.Loaded(PackLoader.Load(shuffled, "us-zz-state"));
        Assert.Equal(a.Tables[0].Rows.Select(r => (r.Id, r.Header, r.Source)), b.Tables[0].Rows.Select(r => (r.Id, r.Header, r.Source)));
        Assert.Equal(a.Manifest, b.Manifest);
    }

    [Fact]
    public void A_utf8_byte_order_mark_is_accepted()
    {
        InMemoryPackSource source = Fx.Source();
        byte[] body = Encoding.UTF8.GetBytes(source.Text("packs/us-zz-base/pack.json"));
        source.With("packs/us-zz-base/pack.json", [0xEF, 0xBB, 0xBF, .. body]);
        Fx.Loaded(PackLoader.Load(source, "us-zz-base"));
    }

    // ---- errors are values: every refusal below is one edit to a valid pack ----

    [Fact]
    public void Unknown_schema_version_is_refused_with_both_numbers()
    {
        PackLoadResult.Invalid invalid = Refused(
            Edit("packs/us-zz-base/pack.json", p => p["schemaVersion"] = 2),
            "us-zz-base",
            "unsupported pack schema version 2; this napkin reads schema version 1 only");
        Assert.Equal("packs/us-zz-base/pack.json", Assert.Single(invalid.Problems).File);

        Refused(Edit(Fx.TablePath, t => t["schemaVersion"] = 7), "us-zz-base", "unsupported pack schema version 7");
        Refused(Edit(Fx.TablePath, t => t.AsObject().Remove("schemaVersion")), "us-zz-base", "schemaVersion: missing required field");
    }

    [Fact]
    public void Corrupt_json_is_refused_naming_the_file()
    {
        PackLoadResult.Invalid invalid = Refused(Fx.Source().With(Fx.TablePath, "{ \"schemaVersion\": 1, "), "us-zz-base", "not valid JSON");
        Assert.Contains(invalid.Problems, p => p.File == Fx.TablePath);
        Refused(Fx.Source().With(Fx.TablePath, "{ \"schemaVersion\": 1, } "), "us-zz-base", "not valid JSON");
        Refused(Fx.Source().With(Fx.TablePath, "{ // comment\n \"schemaVersion\": 1 }"), "us-zz-base", "not valid JSON");
        Refused(Fx.Source().With(Fx.TablePath, "[]"), "us-zz-base", "must be a JSON object");
    }

    [Fact]
    public void An_oversized_file_is_refused_before_it_is_read()
    {
        string big = "{\"schemaVersion\": 1, \"notes\": \"" + new string('x', (int)PackLoader.MaxFileBytes) + "\"}";
        Refused(Fx.Source().With(Fx.TablePath, big), "us-zz-base", $"a pack file may be at most {PackLoader.MaxFileBytes} bytes");
    }

    [Fact]
    public void A_duplicate_key_is_refused()
    {
        Refused(
            EditText("packs/us-zz-base/pack.json", "\"revision\": 1,", "\"revision\": 1, \"revision\": 2,"),
            "us-zz-base",
            "revision: duplicate key");
    }

    [Fact]
    public void Unknown_fields_are_refused()
    {
        Refused(Edit("packs/us-zz-base/pack.json", p => p["colour"] = "red"), "us-zz-base", "colour: unknown field");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["extra"] = 1), "us-zz-base", "extra: unknown field");
    }

    [Fact]
    public void A_non_integer_number_is_refused_not_rounded()
    {
        Refused(
            EditText(Fx.TablePath, "\"groundSnowLoad\": 33,", "\"groundSnowLoad\": 33.0,"),
            "us-zz-base",
            "must be a whole number written without a decimal point or exponent (found 33.0)");
        Refused(EditText(Fx.TablePath, "\"jackStuds\": 2,", "\"jackStuds\": 2e0,"), "us-zz-base", "(found 2e0)");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["jackStuds"] = "1"), "us-zz-base", "jackStuds: must be a whole number");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["jackStuds"] = -1), "us-zz-base", "must be at least 0");
    }

    [Fact]
    public void An_off_grid_or_unreadable_length_is_refused()
    {
        Refused(
            Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["headerSpan"] = "7ft 7-1/3in"),
            "us-zz-base",
            "is not exact on the 1/1024-inch grid");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["headerSpan"] = "seven feet"), "us-zz-base", "is not a length");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["headerSpan"] = "-7ft 7in"), "us-zz-base", "is negative");
    }

    [Fact]
    public void Unknown_kind_enum_values_and_inputs_are_refused()
    {
        Refused(Edit(Fx.TablePath, t => t["kind"] = "wall-bracing"), "us-zz-base", "unknown table kind 'wall-bracing'");
        Refused(Edit(Fx.TablePath, t => t["wallKind"] = "garden-wall"), "us-zz-base", "'garden-wall' is not one of");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m1")["supports"] = "test-deck"), "us-zz-base", "'test-deck' is not one of the column's declared values");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![0]!["name"] = "roofPitch"), "us-zz-base", "'roofPitch' is not an input napkin can supply");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![1]!["type"] = "mph"), "us-zz-base", "'groundSnowLoad' is 'psf', not 'mph'");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![1]!["band"] = "capacity"), "us-zz-base", "only 'headerSpan' is a capacity column");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![1]!["band"] = "exact"), "us-zz-base", "'exact' bands are for category columns");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![3]!["band"] = "upper-bound"), "us-zz-base", "'headerSpan' must be a 'capacity' column");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![0]!["band"] = "upper-bound"), "us-zz-base", "a category column must use 'exact' bands");
        Refused(Edit(Fx.TablePath, t => t["inputs"]!.AsArray().RemoveAt(3)), "us-zz-base", "exactly one 'headerSpan' capacity column");
        Refused(Edit(Fx.TablePath, t => t["inputs"]!.AsArray().Add(t["inputs"]![1]!.DeepClone())), "us-zz-base", "column 'groundSnowLoad' is declared twice");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![1]!.AsObject().Remove("domain")), "us-zz-base", "domain: missing required field");
        Refused(Edit(Fx.TablePath, t => t["inputs"]![1]!["domain"]!["min"] = 100), "us-zz-base", "min 100 psf is above max 99 psf");
        Refused(Edit(Fx.TablePath, t => t["outputs"]!.AsArray().RemoveAt(2)), "us-zz-base", "outputs are exactly header (member), jackStuds (count), kingStuds (count)");
    }

    [Fact]
    public void Every_row_needs_a_location()
    {
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s66.w22.m2").AsObject().Remove("location")), "us-zz-base", "location: missing required field");
    }

    [Fact]
    public void An_unclassified_footnote_makes_the_table_invalid()
    {
        PackLoadResult.Invalid invalid = Refused(
            Edit(Fx.TablePath, t => t["footnotes"]![0]!.AsObject().Remove("encodedAs")),
            "us-zz-base",
            "footnote is not classified");
        Assert.Contains(invalid.Problems, p => p.Table == Fx.Table && p.RowOrOperation == "footnote a");

        Refused(Edit(Fx.TablePath, t => t["footnotes"]![0]!["encodedAs"] = "interpolate"), "us-zz-base", "'interpolate' is not one of");
        Refused(Edit(Fx.TablePath, t => t["footnotes"]![2]!.AsObject().Remove("limit")), "us-zz-base", "limit: missing required field");
        Refused(Edit(Fx.TablePath, t => t["footnotes"]![0]!["limit"] = new JsonObject()), "us-zz-base", "only an 'as-limit' footnote has a limit");
        Refused(Edit(Fx.TablePath, t => t["footnotes"]![2]!["limit"]!["input"] = "roofPitch"), "us-zz-base", "'roofPitch' is not an input napkin can supply");
        Refused(Edit(Fx.TablePath, t => t["footnotes"]![2]!["limit"]!["equals"] = "x"), "us-zz-base", "a limit has exactly one of");
        Refused(Edit(Fx.TablePath, t => t["footnotes"]![3]!.AsObject()["appliesTo"] = "table"), "us-zz-base", "applies to the whole table; rows list only row footnotes");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s99.w44.m3").AsObject().Remove("footnotes")), "us-zz-base", "footnote 'd' applies to rows, but no row lists it");
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s99.w44.m3")["footnotes"] = new JsonArray("z")), "us-zz-base", "footnote 'z' is not declared by the table");
        Refused(Edit(Fx.TablePath, t => t["footnotes"]!.AsArray().Add(t["footnotes"]![0]!.DeepClone())), "us-zz-base", "footnote 'a' is listed twice");
    }

    [Fact]
    public void Footnote_limits_are_typed_by_their_input()
    {
        Refused(
            Edit(Fx.TablePath, t => t["footnotes"]![2]!["limit"] = new JsonObject { ["input"] = "supports", ["above"] = 3 }),
            "us-zz-base",
            "'supports' is a category; use 'equals'");
        Refused(
            Edit(Fx.TablePath, t => t["footnotes"]![2]!["limit"] = new JsonObject { ["input"] = "groundSnowLoad", ["equals"] = "x" }),
            "us-zz-base",
            "'groundSnowLoad' is not a category; use 'above'");
        Refused(
            Edit(Fx.TablePath, t => t["footnotes"]![2]!["limit"] = new JsonObject { ["input"] = "supports", ["equals"] = "test-deck" }),
            "us-zz-base",
            "'test-deck' is not one of the column's values");
    }

    [Fact]
    public void Band_gaps_and_overlaps_are_rejected_at_load()
    {
        // A duplicate bound in one cell: two rows give the same span for the same inputs.
        Refused(
            Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m2")["headerSpan"] = "7ft 7in"),
            "us-zz-base",
            "overlap: rows 'roof.s33.w22.m1', 'roof.s33.w22.m2' have the same inputs and the same headerSpan 7'-7\"");

        // A hole in the grid: the 66 psf band has no 44 ft rows.
        Refused(
            Edit(Fx.TablePath, t =>
            {
                JsonArray rows = t["rows"]!.AsArray();
                foreach (JsonNode r in rows.Where(r => ((string)r!["id"]!).StartsWith("roof.s66.w44", StringComparison.Ordinal)).ToList()!)
                {
                    rows.Remove(r);
                }
            }),
            "us-zz-base",
            "gap: for rows with supports=test-roof, no rows for groundSnowLoad ≤ 66 psf; buildingWidth ≤ 44'-0\"");

        // The bands stop short of the declared domain.
        Refused(
            Edit(Fx.TablePath, t => t["inputs"]![1]!["domain"]!["max"] = 120),
            "us-zz-base",
            "gap: for rows with supports=test-roof, the 'groundSnowLoad' bands stop at 99 psf but the column's domain declares max 120 psf");

        // A value outside the declared domain.
        Refused(Edit(Fx.TablePath, t => Row(t, "roof.s33.w22.m3")["headerSpan"] = "20ft 0in"), "us-zz-base", "is outside the column's declared domain");

        // A declared category no row covers.
        Refused(
            Edit(Fx.TablePath, t => t["inputs"]![0]!["values"]!.AsArray().Add("test-deck")),
            "us-zz-base",
            "column 'supports' declares 'test-deck' but no row covers it");

        Refused(Edit(Fx.TablePath, t => t["rows"]!.AsArray().Add(Row(t, "roof.s33.w22.m1").DeepClone())), "us-zz-base", "row id 'roof.s33.w22.m1' appears twice");
    }

    [Fact]
    public void Composition_conflicts_are_refused_never_last_one_wins()
    {
        // amend of a missing row
        Refused(
            Edit(Fx.StateOverlayPath, o => o["operations"]![2]!["row"]!["id"] = "roof.missing"),
            "us-zz-state",
            "amend: row 'roof.missing' does not exist in the layer below");

        // add of an existing row
        Refused(
            Edit(Fx.StateOverlayPath, o => o["operations"]![1]!["row"]!["id"] = "roof.s33.w44.m1"),
            "us-zz-state",
            "add: row 'roof.s33.w44.m1' already exists in the layer below; use amend");

        // delete of a missing row
        Refused(Edit(Fx.StateOverlayPath, o => o["operations"]![3]!["rowId"] = "floor.none"), "us-zz-state", "delete: row 'floor.none' does not exist in the layer below");

        // two operations on one row
        Refused(
            Edit(Fx.StateOverlayPath, o => o["operations"]![3]!["rowId"] = "roof.s33.w22.m1"),
            "us-zz-state",
            "two operations in one overlay target row 'roof.s33.w22.m1'");

        // an unknown operation
        Refused(Edit(Fx.StateOverlayPath, o => o["operations"]![3]!["op"] = "replace"), "us-zz-state", "'replace' is not one of");

        // a dangling source on an operation
        Refused(Edit(Fx.StateOverlayPath, o => o["operations"]![1]!["source"] = "zz-nowhere"), "us-zz-state", "source 'zz-nowhere' is not listed in the manifest's sources");
    }

    private static JsonObject AddTableOp()
    {
        JsonNode table = JsonNode.Parse(Fx.Source().Text(Fx.TablePath))!;
        foreach (string key in new[] { "schemaVersion", "notes", "table", "source" })
        {
            table.AsObject().Remove(key);
        }

        return new JsonObject { ["op"] = "add-table", ["source"] = "zz-synthetic-state", ["location"] = "x", ["table"] = table };
    }

    private static string OverlayWith(params JsonObject[] operations)
        => new JsonObject
        {
            ["schemaVersion"] = 1,
            ["table"] = Fx.Table,
            ["source"] = "zz-synthetic-state",
            ["location"] = "x",
            ["operations"] = new JsonArray([.. operations]),
        }.ToJsonString();

    [Fact]
    public void Table_operations_follow_the_same_rules()
    {
        JsonObject DeleteTable() => new() { ["op"] = "delete-table", ["source"] = "zz-synthetic-state", ["location"] = "x" };

        Refused(Fx.Source().With(Fx.StateOverlayPath, OverlayWith(AddTableOp())), "us-zz-state", "add-table: table 'TEST-HEADER-TABLE' already exists");
        Refused(Fx.Source().With(Fx.StateOverlayPath, OverlayWith(DeleteTable(), DeleteTable())), "us-zz-state", "a second table operation on the same table");

        // delete-table removes the table: the pack loads and has nothing to answer from.
        Assert.Empty(Fx.Loaded(PackLoader.Load(Fx.Source().With(Fx.StateOverlayPath, OverlayWith(DeleteTable())), "us-zz-state")).Tables);

        JsonObject deleteRow = new() { ["op"] = "delete", ["source"] = "zz-synthetic-state", ["location"] = "x", ["rowId"] = "floor.s99.w44.m2" };
        Refused(
            Fx.Source().With(Fx.StateOverlayPath, OverlayWith(DeleteTable(), deleteRow)),
            "us-zz-state",
            "delete-table: cannot be combined with row operations");

        // amend-table carries metadata only; rows in it are refused.
        Refused(
            Edit(Fx.StateOverlayPath, o => o["operations"]![0]!["table"]!["rows"] = new JsonArray()),
            "us-zz-state",
            "rows: unknown field");

        // add-table over a base without the table: a table the model-code layer lacks.
        HeaderSizingTable composed = Assert.Single(
            Fx.Loaded(PackLoader.Load(Fx.Source().Without(Fx.TablePath).With(Fx.StateOverlayPath, OverlayWith(AddTableOp())), "us-zz-state")).Tables);
        Assert.Equal(CitationLayer.StateAmendment, composed.Layer);
        Assert.All(composed.Rows, r => Assert.Equal(CitationLayer.StateAmendment, r.Layer));

        // Without the base table, amending or row-editing it is refused.
        Refused(Fx.Source().Without(Fx.TablePath), "us-zz-state", "amend-table: table 'TEST-HEADER-TABLE' does not exist in the layer below");
        Refused(Fx.Source().Without(Fx.TablePath).With(Fx.StateOverlayPath, OverlayWith(DeleteTable())), "us-zz-state", "delete-table: table 'TEST-HEADER-TABLE' does not exist");
        Refused(
            Edit(Fx.StateOverlayPath, o => o["operations"]!.AsArray().RemoveAt(0)).Without(Fx.TablePath),
            "us-zz-state",
            "a new table is added with add-table");
    }

    [Fact]
    public void Not_amended_must_be_stated_with_an_overlay_file()
    {
        Refused(
            Fx.Source().Without("packs/us-zz-base/amendments/test-header-table.json").With("packs/us-zz-base/amendments/other.json", "{\"schemaVersion\":1,\"table\":\"OTHER\",\"source\":\"zz-synthetic-state\",\"location\":\"x\",\"operations\":[]}"),
            "us-zz-base",
            "no overlay file for table 'TEST-HEADER-TABLE' in layer 'amendments'");
        Refused(
            Fx.Source().With("packs/us-zz-base/amendments/second.json", Fx.Source().Text("packs/us-zz-base/amendments/test-header-table.json")),
            "us-zz-base",
            "a second overlay for table 'TEST-HEADER-TABLE' in this layer");
    }

    [Fact]
    public void Manifest_references_must_resolve()
    {
        Refused(Edit("packs/us-zz-base/pack.json", p => p["layers"]![0] = "zz-missing"), "us-zz-base", "base layer 'zz-missing' does not resolve");
        Refused(Edit("packs/us-zz-base/pack.json", p => p["layers"]![0] = "../up"), "us-zz-base", "is not a base layer id");
        Refused(Edit("packs/us-zz-base/pack.json", p => p["layers"]![1] = "fixes"), "us-zz-base", "overlay 'fixes' does not resolve");
        Refused(Edit("packs/us-zz-base/pack.json", p => p["layers"]![1] = "../../x"), "us-zz-base", "is not an overlay directory");
        Refused(Edit("packs/us-zz-town/pack.json", p => p["layers"]![1] = "us-zz-gone/amendments"), "us-zz-town", "overlay 'us-zz-gone/amendments' does not resolve");
        Refused(Edit(Fx.TablePath, t => t["source"] = "zz-nowhere"), "us-zz-base", "source 'zz-nowhere' is not listed");
        Refused(Edit("packs/us-zz-base/pack.json", p => p["id"] = "us-zz-other"), "us-zz-base", "id 'us-zz-other' does not match its directory");
        Refused(Edit("layers/zz-base-2099/layer.json", l => l["id"] = "zz-other"), "us-zz-base", "id 'zz-other' does not match its directory");
        Refused(Edit("layers/zz-base-2099/layer.json", l => l["baseCode"]!["year"] = 2098), "us-zz-base", "layer is IRC 2098 but pack 'us-zz-base' declares baseCode IRC 2099");
        Refused(Fx.Source(), "not a pack id", "is not a pack id");
        Refused(Fx.Source(), "us-zz-nothing", "file not found");
    }

    [Fact]
    public void Manifest_fields_are_checked()
    {
        const string P = "packs/us-zz-base/pack.json";
        Refused(Edit(P, p => p["sources"]![0]!["sha256"] = "UNVERIFIED"), "us-zz-base", "must be 64 lower-case hex digits");
        Refused(Edit(P, p => p["sources"]!.AsArray().Add(p["sources"]![0]!.DeepClone())), "us-zz-base", "'zz-synthetic-state' is listed twice");
        Refused(Edit(P, p => p["sources"] = new JsonArray()), "us-zz-base", "must have at least 1 item");
        Refused(Edit(P, p => p["jurisdiction"]!["country"] = "CA"), "us-zz-base", "napkin encodes US codes only");
        Refused(Edit(P, p => p["jurisdiction"]!["state"] = "zz"), "us-zz-base", "must be two capital letters");
        Refused(Edit(P, p => p["adoption"]!["inForce"]!["to"] = "2098-01-01"), "us-zz-base", "'to' is before 'from'");
        Refused(Edit(P, p => p["adoption"]!["inForce"]!["from"] = "Jan 1 2099"), "us-zz-base", "is not a date written yyyy-MM-dd");
        Refused(Edit(P, p => p["adoption"]!["appliesTo"] = "see-notes"), "us-zz-base", "transitionNotes: required when appliesTo is 'see-notes'");
        Refused(Edit(P, p => p["baseCode"]!["code"] = "IBC"), "us-zz-base", "only 'IRC' is encoded");
        Refused(Edit(P, p => p["baseCode"]!["publisher"] = "XYZ"), "us-zz-base", "only 'ICC' model codes are encoded");
        Refused(Edit(P, p => p["revision"] = 0), "us-zz-base", "must be at least 1");
        Refused(Edit(P, p => p["layers"] = new JsonArray()), "us-zz-base", "layers: must have at least 1 item");
        Refused(Edit(P, p => p["adoption"]!["name"] = ""), "us-zz-base", "must not be empty");
        Refused(Edit(P, p => p["adoption"]!["transitionNotes"] = 3), "us-zz-base", "must be a string or null");
    }

    [Fact]
    public void A_signed_off_pack_must_name_an_existing_checklist()
    {
        const string P = "packs/us-zz-base/pack.json";
        Refused(Edit(P, p => p["review"]!["status"] = "signed-off"), "us-zz-base", "a signed-off pack must name its review checklist");
        Refused(
            Edit(P, p => p["review"] = new JsonObject { ["status"] = "signed-off", ["checklist"] = "reviews/us-zz-base.md" }),
            "us-zz-base",
            "'reviews/us-zz-base.md' does not exist under the packs root");

        InMemoryPackSource ok = Edit(P, p => p["review"] = new JsonObject { ["status"] = "signed-off", ["checklist"] = "reviews/us-zz-base.md" })
            .With("reviews/us-zz-base.md", "# Review (synthetic)");
        LoadedPack pack = Fx.Loaded(PackLoader.Load(ok, "us-zz-base"));
        Assert.Equal(ReviewStatus.SignedOff, pack.Code.Review);
        Assert.DoesNotContain("UNREVIEWED", pack.Code.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_problem_is_collected_not_just_the_first()
    {
        InMemoryPackSource source = Edit(Fx.TablePath, t =>
        {
            Row(t, "roof.s33.w22.m1")["headerSpan"] = "7ft 7-1/3in";
            Row(t, "roof.s66.w22.m1")["jackStuds"] = 1.5;
            t["footnotes"]![0]!.AsObject().Remove("encodedAs");
        });
        PackLoadResult.Invalid invalid = Fx.Invalid(PackLoader.Load(source, "us-zz-base"));
        Assert.True(invalid.Problems.Count >= 3, invalid.ToString());
        Assert.Contains(invalid.Problems, p => p.RowOrOperation == "roof.s33.w22.m1");
        Assert.Contains(invalid.Problems, p => p.RowOrOperation == "roof.s66.w22.m1");
        Assert.Contains(invalid.Problems, p => p.Message.Contains("not classified", StringComparison.Ordinal));
        Assert.Contains("pack 'us-zz-base' is invalid:", invalid.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Two_tables_for_one_wall_kind_are_refused()
    {
        InMemoryPackSource source = Fx.Source();
        source.With("layers/zz-base-2099/tables/second.json", source.Text(Fx.TablePath).Replace("\"TEST-HEADER-TABLE\"", "\"TEST-HEADER-TABLE-2\"", StringComparison.Ordinal))
            .With("packs/us-zz-base/amendments/second.json", source.Text("packs/us-zz-base/amendments/test-header-table.json").Replace("\"TEST-HEADER-TABLE\"", "\"TEST-HEADER-TABLE-2\"", StringComparison.Ordinal));
        Refused(source, "us-zz-base", "both declare wallKind 'exterior-bearing'");

        source.With("layers/zz-base-2099/tables/second.json", source.Text(Fx.TablePath));
        Refused(source, "us-zz-base", "table 'TEST-HEADER-TABLE' is also defined in");
    }

    [Fact]
    public void Directory_and_memory_sources_list_the_same_files()
    {
        DirectoryPackSource disk = new(Fx.Root);
        InMemoryPackSource memory = Fx.Source();
        foreach (string dir in new[] { string.Empty, "packs", "layers/zz-base-2099", "packs/us-zz-state/amendments", "nowhere" })
        {
            Assert.Equal(disk.ListFiles(dir), memory.ListFiles(dir));
            Assert.Equal(disk.ListDirectories(dir), memory.ListDirectories(dir));
            Assert.Equal(disk.DirectoryExists(dir), memory.DirectoryExists(dir));
        }

        Assert.Equal(disk.FileLength(Fx.TablePath), memory.FileLength(Fx.TablePath));
        Assert.Null(disk.FileLength("nowhere.json"));
        Assert.Equal(disk.ReadFile(Fx.TablePath), memory.ReadFile(Fx.TablePath));
    }
}
