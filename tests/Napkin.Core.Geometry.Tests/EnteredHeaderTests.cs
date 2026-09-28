using Napkin.Core.Geometry;

namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// A header row entered by hand (docs/design/manual-code-values.md §7.1, #246): part of its box's
/// value, set, edited and removed by one request each, never on a wall, deck or roof, and refused
/// by the same rules the loader uses. Every value is synthetic.
/// </summary>
public class EnteredHeaderTests
{
    static readonly IGeometryUpdater Updater = DirectUpdater.Instance;

    internal static EnteredHeader Row() => new(
        2,
        "2x99",
        1,
        2,
        new EnteredCitation("SYNTHETIC TEST DATA - NOT CODE VALUES", "Table T-99", "p. 99, row 9", null),
        "A. Person",
        new DateOnly(2026, 9, 27),
        new EnteredHeaderInputs("us-zz-test", WallSide.Exterior, "test-roof", Length.Inches(36), 99, null, null, null, null, null));

    static Box Plain() => new(new EntityId(new Guid("00000000-0000-4000-8000-000000000001")), LayerId.Default, Point3.Origin, Length.Inches(36), Length.Inches(4), Length.Inches(48), BoxFace.Top, Angle.Zero);

    [Fact]
    public void A_boxs_entered_row_is_part_of_its_value()
    {
        Box plain = Plain();
        Box entered = plain with { EnteredHeader = Row() };
        Assert.NotEqual(plain, entered);
        Assert.NotEqual(plain.GetHashCode(), entered.GetHashCode());
        Assert.Equal(entered, plain with { EnteredHeader = Row() });
        Assert.Equal(entered.GetHashCode(), (plain with { EnteredHeader = Row() }).GetHashCode());
        Assert.NotEqual(entered, plain with { EnteredHeader = Row() with { KingStuds = 3 } });
    }

    [Fact]
    public void A_row_is_set_edited_and_removed_one_request_each()
    {
        SketchBuilder builder = new();
        EntityId opening = builder.AddBox(0, 0, 36, 4);

        Solved set = Assert.IsType<Solved>(Updater.Apply(builder.Sketch, new SetEnteredHeader(opening, Row())));
        Assert.Equal(Row(), set.Sketch.Find<Box>(opening)!.EnteredHeader);
        Assert.Equal([opening], set.Changes.Modified);

        EnteredHeader edited = Row() with { Plies = 3 };
        Solved edit = Assert.IsType<Solved>(Updater.Apply(set.Sketch, new SetEnteredHeader(opening, edited)));
        Assert.Equal(edited, edit.Sketch.Find<Box>(opening)!.EnteredHeader);

        Solved removed = Assert.IsType<Solved>(Updater.Apply(edit.Sketch, new SetEnteredHeader(opening, null)));
        Assert.Null(removed.Sketch.Find<Box>(opening)!.EnteredHeader);

        // Moves nothing: the box is exactly as it was apart from the row.
        Assert.Equal(builder.BoxOf(opening), removed.Sketch.Find<Box>(opening));
    }

    [Fact]
    public void A_row_is_refused_on_a_wall_a_deck_or_a_roof_on_no_box_and_when_the_rules_refuse_it()
    {
        SketchBuilder builder = new();
        EntityId opening = builder.AddBox(0, 0, 36, 4);

        Sketch wall = builder.Sketch.WithEntity(builder.BoxOf(opening) with { WallInputs = new WallInputs("roof", null) });
        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(wall, new SetEnteredHeader(opening, Row()))).Reason);
        Assert.Equal(RejectionReason.UnknownEntity, Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetEnteredHeader(EntityId.New(), Row()))).Reason);
        EntityId node = builder.AddNode(0, 0);
        Assert.Equal(RejectionReason.DanglingReference, Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetEnteredHeader(node, Row()))).Reason);
        Assert.Equal(RejectionReason.NonPositiveSize, Assert.IsType<Rejected>(Updater.Apply(builder.Sketch, new SetEnteredHeader(opening, Row() with { Plies = 0 }))).Reason);
    }

    public static TheoryData<EnteredHeader, string> Refused() => new()
    {
        { Row() with { Plies = 0 }, "ply" },
        { Row() with { JackStuds = -1 }, "stud count" },
        { Row() with { KingStuds = -1 }, "stud count" },
        { Row() with { Lumber = " " }, "lumber" },
        { Row() with { Citation = Row().Citation with { Code = "" } }, "code" },
        { Row() with { Citation = Row().Citation with { Table = " " } }, "table" },
        { Row() with { Citation = Row().Citation with { Location = "" } }, "page and row" },
        { Row() with { EnteredBy = " " }, "who entered it" },
        { Row() with { For = Row().For with { Supports = "" } }, "supports" },
        { Row() with { For = Row().For with { Pack = "US ZZ" } }, "pack id" },
        { Row() with { For = Row().For with { Pack = "-zz" } }, "pack id" },
        { Row() with { For = Row().For with { Pack = "" } }, "pack id" },
        { Row() with { For = Row().For with { Span = Length.Zero } }, "span" },
        { Row() with { For = Row().For with { GroundSnowLoadPsf = -1 } }, "not negative" },
        { Row() with { For = Row().For with { UltimateWindSpeedMph = -1 } }, "not negative" },
        { Row() with { For = Row().For with { RoofLiveLoadPsf = -1 } }, "not negative" },
        { Row() with { For = Row().For with { FrostDepth = new Length(-1) } }, "frost depth" },
        { Row() with { For = Row().For with { BuildingWidth = Length.Zero } }, "building width" },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Each_rule_refuses_its_own_field(EnteredHeader row, string named)
        => Assert.Contains(named, EnteredHeaderRules.Refusal(row), StringComparison.Ordinal);

    [Fact]
    public void A_row_every_rule_accepts_is_accepted_zero_studs_and_every_site_value_included()
    {
        Assert.Null(EnteredHeaderRules.Refusal(Row()));
        Assert.Null(EnteredHeaderRules.Refusal(Row() with
        {
            JackStuds = 0,
            KingStuds = 0,
            For = Row().For with { FrostDepth = Length.Zero, BuildingWidth = Length.Inches(1), UltimateWindSpeedMph = 0, RoofLiveLoadPsf = 0, SeismicDesignCategory = "Z" },
        }));
        Assert.Throws<ArgumentNullException>(() => EnteredHeaderRules.Refusal(null!));
    }
}
