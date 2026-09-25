using System.Collections.Immutable;
using Napkin.Core.Geometry;
using Xunit;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// The small values the editor holds and opens: a <see cref="Design"/> and its labels, the blank
/// sheet, a load failure's list of problems, the layer name a part is styled by, and the three
/// dimension words a scene file uses.
/// </summary>
public class DesignModelTests
{
    static Box BoxOn(LayerId layer, string name = "") =>
        Box.AsDrawn(EntityId.New(), layer, Point2.Inches(0, 0), Length.Inches(10), Length.Inches(4), Length.Inches(1), Angle.Zero)
            with { Name = name };

    [Fact]
    public void A_named_design_labels_exactly_the_entities_that_carry_a_name()
    {
        Box named = BoxOn(LayerId.Default, "Top");
        Box anonymous = BoxOn(LayerId.Default);
        Sketch sketch = Sketch.Empty.WithEntity(named).WithEntity(anonymous);

        Design design = Design.Named("Table", sketch);

        Assert.Equal("Table", design.Name);
        Assert.Equal(["Top"], design.Labels.Values);
        Assert.Equal("Top", design.Labels[named.Id]);
        Assert.False(design.Labels.ContainsKey(anonymous.Id));
    }

    [Fact]
    public void A_named_design_needs_a_sketch()
    {
        Assert.Throws<ArgumentNullException>(() => Design.Named("Table", null!));
    }

    [Fact]
    public void An_unlabelled_design_has_no_labels()
    {
        Design design = Design.Unlabelled("Loose", Sketch.Empty.WithEntity(BoxOn(LayerId.Default, "Top")));

        Assert.Empty(design.Labels);
    }

    [Fact]
    public void A_label_comes_from_the_entity_s_own_name_first_then_the_design_s_labels_then_nothing()
    {
        Box named = BoxOn(LayerId.Default, "Leg");
        Box anonymous = BoxOn(LayerId.Default);
        Sketch sketch = Sketch.Empty.WithEntity(named).WithEntity(anonymous);
        ImmutableDictionary<EntityId, string> labels = ImmutableDictionary<EntityId, string>.Empty
            .Add(named.Id, "Stale label")
            .Add(anonymous.Id, "Part 2");

        Design design = new("Table", sketch, labels);

        // The name on the entity wins over a label the design kept for it.
        Assert.Equal("Leg", design.LabelFor(named.Id));
        // With no name of its own the design's label is used.
        Assert.Equal("Part 2", design.LabelFor(anonymous.Id));
        // An id the sketch does not hold and the design never labelled has no label.
        Assert.Null(design.LabelFor(EntityId.New()));
    }

    [Fact]
    public void A_label_is_kept_for_an_entity_the_sketch_no_longer_holds()
    {
        EntityId gone = EntityId.New();
        Design design = new("Table", Sketch.Empty, ImmutableDictionary<EntityId, string>.Empty.Add(gone, "Old part"));

        Assert.Equal("Old part", design.LabelFor(gone));
    }

    [Fact]
    public void The_blank_sheet_is_an_empty_untitled_design_with_a_parts_layer_to_draw_on()
    {
        NewSheet source = new();
        Design design = source.Load();

        Assert.Equal(NewSheet.UntitledName, source.Name);
        Assert.Equal(NewSheet.UntitledName, design.Name);
        Assert.False(string.IsNullOrWhiteSpace(source.Description));
        Assert.Empty(design.Sketch.Entities);
        Assert.Empty(design.Sketch.Relationships);
        Assert.Empty(design.Labels);
        Assert.Contains(design.Sketch.Layers, layer => layer.Name == DesignLayers.Parts);
    }

    [Fact]
    public void Each_blank_sheet_gets_its_own_parts_layer()
    {
        // Two blank sheets must not share a layer id, or a part copied between them would land on
        // a layer the other one also claims.
        LayerId first = NewSheet.Empty().Sketch.Layers.Single(layer => layer.Name == DesignLayers.Parts).Id;
        LayerId second = NewSheet.Empty().Sketch.Layers.Single(layer => layer.Name == DesignLayers.Parts).Id;

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_load_failure_with_one_message_lists_that_message_as_its_problem()
    {
        DesignLoadException failure = new("The file is empty.");

        Assert.Equal("The file is empty.", failure.Message);
        Assert.Equal(["The file is empty."], failure.Problems);
    }

    [Fact]
    public void A_load_failure_lists_every_problem_it_was_given()
    {
        DesignLoadException failure = new("2 problems.", ["Line 3: no width.", "Line 9: unknown layer."]);

        Assert.Equal("2 problems.", failure.Message);
        Assert.Equal(["Line 3: no width.", "Line 9: unknown layer."], failure.Problems);
    }

    [Fact]
    public void A_load_failure_given_an_empty_list_still_says_something()
    {
        DesignLoadException failure = new("Could not read it.", []);

        Assert.Equal(["Could not read it."], failure.Problems);
    }

    [Fact]
    public void A_load_failure_needs_its_problem_list()
    {
        Assert.Throws<ArgumentNullException>(() => new DesignLoadException("x", (IEnumerable<string>)null!));
    }

    [Fact]
    public void A_load_failure_from_an_exception_keeps_the_cause_and_lists_its_own_message()
    {
        IOException cause = new("disk gone");
        DesignLoadException failure = new("Could not open it.", cause);

        Assert.Same(cause, failure.InnerException);
        Assert.Equal(["Could not open it."], failure.Problems);
    }

    [Fact]
    public void A_part_is_styled_by_its_own_layer_s_name()
    {
        Layer parts = new(LayerId.New(), DesignLayers.Parts);
        Box part = BoxOn(parts.Id, "Top");
        Sketch sketch = Sketch.Empty.WithLayer(parts).WithEntity(part);
        Dictionary<LayerId, string> names = new() { [parts.Id] = DesignLayers.Parts };

        Assert.Equal(DesignLayers.Parts, DesignLayers.StyleName(sketch, part, names));
    }

    [Fact]
    public void An_entity_on_a_layer_with_no_known_name_gets_no_style_name()
    {
        Box part = BoxOn(LayerId.Default);
        Sketch sketch = Sketch.Empty.WithEntity(part);

        Assert.Equal(string.Empty, DesignLayers.StyleName(sketch, part, new Dictionary<LayerId, string>()));
    }

    [Fact]
    public void A_box_called_wall_is_styled_as_a_wall_whatever_layer_it_is_on()
    {
        Box wall = BoxOn(LayerId.Default, "wall");
        Sketch sketch = Sketch.Empty.WithEntity(wall);
        Dictionary<LayerId, string> names = new() { [LayerId.Default] = DesignLayers.Parts };

        Assert.Equal(DesignLayers.Wall, DesignLayers.StyleName(sketch, wall, names));
    }

    [Fact]
    public void A_box_on_a_layer_called_opening_is_styled_as_an_opening()
    {
        Layer openings = new(LayerId.New(), "OPENING");
        Box window = BoxOn(openings.Id, "Kitchen window");
        Sketch sketch = Sketch.Empty.WithLayer(openings).WithEntity(window);

        Assert.Equal(DesignLayers.Opening, DesignLayers.StyleName(sketch, window, new Dictionary<LayerId, string>()));
    }

    [Fact]
    public void Style_name_needs_all_three_arguments()
    {
        Box part = BoxOn(LayerId.Default);
        Sketch sketch = Sketch.Empty.WithEntity(part);
        Dictionary<LayerId, string> names = [];

        Assert.Throws<ArgumentNullException>(() => DesignLayers.StyleName(null!, part, names));
        Assert.Throws<ArgumentNullException>(() => DesignLayers.StyleName(sketch, null!, names));
        Assert.Throws<ArgumentNullException>(() => DesignLayers.StyleName(sketch, part, null!));
    }

    [Theory]
    [InlineData(PartDimension.Length, "Length")]
    [InlineData(PartDimension.Width, "Width")]
    [InlineData(PartDimension.Thickness, "Thickness")]
    public void A_dimension_word_reads_back_as_the_dimension_it_names(PartDimension dimension, string word)
    {
        Assert.Equal(word, SceneWords.Of(dimension));
        Assert.True(SceneWords.TryDimension(word, out PartDimension read));
        Assert.Equal(dimension, read);
        Assert.Contains(word, SceneWords.Dimensions);
    }

    [Theory]
    [InlineData("length")]
    [InlineData("Depth")]
    [InlineData("")]
    [InlineData(null)]
    public void A_word_that_is_not_exactly_one_of_the_three_is_not_a_dimension(string? word)
    {
        Assert.False(SceneWords.TryDimension(word, out _));
    }

    [Fact]
    public void A_value_outside_the_three_dimensions_has_no_word()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SceneWords.Of((PartDimension)99));
    }
}
