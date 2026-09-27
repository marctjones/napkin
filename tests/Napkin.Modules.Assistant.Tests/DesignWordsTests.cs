using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Modules.Building;
using Napkin.Modules.Editing;

namespace Napkin.Modules.Assistant.Tests;

/// <summary>The design in words (docs/design/llm-assistant.md §3.1): one line per entity, the inputs as entered, nothing derived.</summary>
public class DesignWordsTests
{
    private static readonly LayerId WallLayer = new(Guid.Parse("71000000-0000-4000-8000-000000000001"));
    private static readonly LayerId OpeningLayer = new(Guid.Parse("71000000-0000-4000-8000-000000000002"));
    private static readonly LayerId RoomLayer = new(Guid.Parse("71000000-0000-4000-8000-000000000003"));

    private static Length In(long inches) => new(inches * Length.UnitsPerInch);

    private static EntityId Id(int n) => new(Guid.Parse($"72000000-0000-4000-8000-{n:D12}"));

    private static Box Box(int n, LayerId layer, long x, long y, long width, long height, long depth)
        => Box(n, layer, x, y, In(width), In(height), In(depth));

    private static Box Box(int n, LayerId layer, long x, long y, Length width, Length height, Length depth)
        => Napkin.Core.Geometry.Box.AsDrawn(Id(n), layer, new Point2(In(x), In(y)), width, height, depth, Angle.Zero);

    /// <summary>One of each kind of thing a design can hold. Test values, not code or stock values.</summary>
    private static Design Everything()
    {
        Box wall = Box(1, WallLayer, 0, 0, In(120), new Length(3584), In(96)) with
        {
            Name = "Wall 2",
            WallInputs = new WallInputs("zz-roof", In(16), [new BracingAssignment(null, null, "ZZ-METHOD")])
            {
                Side = WallSide.Interior,
                Bearing = false,
                Header = new TypedHeader(2, "2x6"),
            },
        };
        Box door = Box(2, OpeningLayer, 24, 0, In(36), new Length(3584), In(80));
        Box loose = Box(3, OpeningLayer, 500, 500, 24, 4, 36);
        Box room = Box(4, RoomLayer, 0, 200, 144, 120, 96) with { Name = "Kitchen" };
        Box deck = Box(5, LayerId.Default, 0, -200, 144, 120, 12) with { Name = "Deck" };
        Box roof = Box(6, LayerId.Default, 0, 400, 144, 120, 12) with { Name = "Roof" };
        Box leg = Box(7, LayerId.Default, 300, 0, In(16), In(4), new Length(768)) with
        {
            Name = "Leg",
            Phase = Phase.Existing,
            Part = new Part("2x4", "oak", 2, new PlanAxes(PartDimension.Length, PartDimension.Width))
            {
                Rough = true,
                Hardware = [new HardwareItem("pull", 2)],
            },
        };
        Box top = Box(8, LayerId.Default, 300, 100, In(48), In(16), new Length(768)) with
        {
            Name = "Top",
            Part = new Part(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
        Box blank = Box(9, LayerId.Default, 400, 0, 10, 10, 1) with { Name = "Blank", Phase = Phase.Demolish };
        Strut brace = new(
            Id(10), LayerId.Default, new Point3(In(0), In(0), In(0)), new Point3(In(24), In(0), In(24)),
            EndCut.Square, EndCut.Square, Axis.X, In(3), new Length(768))
        {
            Name = "Brace",
            Part = new Part(null, null, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };
        Note note = new(Id(11), LayerId.Default, new Point2(In(1), In(1)), "glue after dry fit", NoteSymbol.None);
        Node from = new(Id(12), LayerId.Default, new Point2(In(0), In(0)));
        Node to = new(Id(13), LayerId.Default, new Point2(In(10), In(0)));
        Segment line = new(Id(14), LayerId.Default, from.Id, to.Id);

        Sketch sketch = Sketch.Empty with
        {
            Layers = [Layer.Default, new Layer(WallLayer, BuildingLayers.Wall), new Layer(OpeningLayer, BuildingLayers.Opening), new Layer(RoomLayer, BuildingLayers.Room)],
        };
        foreach (Entity entity in new Entity[] { wall, door, loose, room, deck, roof, leg, top, blank, brace, note, from, to, line })
        {
            sketch = sketch.WithEntity(entity);
        }

        return Design.Named("everything", sketch);
    }

    [Fact]
    public void Each_kind_of_entity_is_one_line_in_plain_words()
    {
        Design design = Everything();
        List<string?> lines = [.. design.Sketch.Entities.Values.OrderBy(entity => entity.Id).Select(entity => DesignWords.Line(design, entity))];

        Assert.Equal(
            [
                "Wall 2 — wall, new, interior, not bearing, supports zz-roof, studs at 1'-4\", header chosen (2) 2x6, bracing ZZ-METHOD, 10'-0\" long, 8'-0\" tall, 3 1/2\" thick.",
                "Door — door in Wall 2, solid, 3'-0\" wide, 6'-8\" tall, sill 0\", new.",
                "Opening — opening in no wall, 2'-0\" wide, 3'-0\" tall, new.",
                "Kitchen — room, 12'-0\" by 10'-0\" in plan, new.",
                "Deck — deck, 12'-0\" by 10'-0\" in plan, new.",
                "Roof — roof, 12'-0\" by 10'-0\" in plan, new.",
                "Leg — part, 1'-4\" long, 4\" wide, 3/4\" thick, 2 pieces, stock 2x4, species typed: \"oak\", rough, hardware typed: \"pull\" 2, existing.",
                "Top — part, 4'-0\" long, 1'-4\" wide, 3/4\" thick, 1 piece, no stock, new.",
                "Blank — box, 10\" by 10\" in plan, 1\" tall, to be demolished.",
                "Brace — angled part, 3\" by 3/4\" in section, no stock, new.",
                "Note — note, typed: \"glue after dry fit\", new.",
                null,
                null,
                "Line — construction line, new.",
            ],
            lines);
    }

    [Fact]
    public void An_unnamed_entity_is_called_what_it_is()
    {
        Design design = Everything();
        Sketch bare = design.Sketch;
        foreach (Entity entity in design.Sketch.Entities.Values)
        {
            bare = bare.WithEntity(entity switch
            {
                Box box => box with { Name = string.Empty },
                Strut strut => strut with { Name = string.Empty },
                _ => entity,
            });
        }

        Design unnamed = Design.Unlabelled("bare", bare);
        Assert.Equal(
            ["Wall", "Door", "Opening", "Room", "Box", "Box", "Part", "Part", "Box", "Angled part", "Note", "Point", "Point", "Line", "Something"],
            [.. bare.Entities.Keys.Order().Select(id => DesignWords.NameOf(unnamed, id)), DesignWords.NameOf(unnamed, Id(99))]);

        // A name the design has and the sketch does not is used.
        Design labelled = unnamed with { Labels = ImmutableDictionary<EntityId, string>.Empty.Add(Id(7), "Left leg") };
        Assert.Equal("Left leg", DesignWords.NameOf(labelled, Id(7)));
    }

    [Fact]
    public void A_size_is_written_as_the_panels_write_it_and_marked_when_not_exact()
    {
        Assert.Equal("4'-0\"", DesignWords.Size(In(48)));
        Assert.Equal("3 1/2\"", DesignWords.Size(new Length(3584)));
        Assert.Equal("≈0\"", DesignWords.Size(new Length(1)));
        Assert.Equal("to be demolished", DesignWords.PhaseWord(Phase.Demolish));
    }

    [Fact]
    public void Every_dimension_of_the_coffee_table_is_said_as_a_dimension()
    {
        Design design = Fixtures.Sample("coffee-table");
        List<Dimension> dimensions = [.. design.Sketch.Entities.Values.OfType<Dimension>()];
        Assert.NotEmpty(dimensions);
        Assert.All(dimensions, dimension => Assert.Matches("^.+ — (reference )?dimension, new\\.$", DesignWords.Line(design, dimension)!));
    }

    [Fact]
    public void Words_refuse_a_missing_design_or_entity()
    {
        Assert.Throws<ArgumentNullException>(() => DesignWords.NameOf(null!, Id(1)));
        Assert.Throws<ArgumentNullException>(() => DesignWords.Line(null!, new Node(Id(1), LayerId.Default, default)));
        Assert.Throws<ArgumentNullException>(() => DesignWords.Line(Everything(), null!));
    }
}
