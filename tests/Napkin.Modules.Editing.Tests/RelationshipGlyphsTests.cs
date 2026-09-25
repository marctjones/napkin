using Napkin.Core.Geometry;
using Napkin.Modules.Editing;
using Xunit;
using Design = Napkin.Modules.Editing.Design;

namespace Napkin.Modules.Editing.Tests;

/// <summary>
/// Where the plan puts a relationship's glyph (#156). Every expected position is worked out by hand
/// from the parts' corners.
/// </summary>
public class RelationshipGlyphsTests
{
    static readonly EntityId First = EditingBuilder.Id(0), Second = EditingBuilder.Id(1);

    static Sketch Two((Point2, Length, Length) a, (Point2, Length, Length) b, Relationship relationship) =>
        EditingBuilder.Design(a, b).Sketch.WithRelationship(relationship);

    static RelationshipGlyph Only(Sketch sketch) => Assert.Single(RelationshipGlyphs.Of(sketch));

    [Fact]
    public void Two_parts_butted_side_to_side_get_a_flush_glyph_halfway_up_the_edge_they_share()
    {
        // Part 1 is x 0–10, y 0–12; part 2 is x 10–14, y 0–12. They share x = 10 from y 0 to 12.
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.West));
        RelationshipGlyph glyph = Only(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(10, 0, 4, 12), flush));

        Assert.Equal(flush.Id, glyph.Id);
        Assert.Equal(GlyphKind.Flush, glyph.Kind);
        Assert.Equal(10, glyph.XInches, 9);
        Assert.Equal(6, glyph.YInches, 9);
        Assert.Equal(0, glyph.AlongX, 9);
        Assert.Equal(1, glyph.AlongY, 9);
    }

    [Fact]
    public void A_shorter_part_against_a_longer_one_gets_the_glyph_on_the_stretch_they_share()
    {
        // Part 2 is y 4–8 against part 1's y 0–12: the shared stretch is y 4–8, its middle y 6.
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.West));
        RelationshipGlyph glyph = Only(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(10, 4, 4, 4), flush));

        Assert.Equal(10, glyph.XInches, 9);
        Assert.Equal(6, glyph.YInches, 9);
    }

    [Fact]
    public void Two_parts_in_a_row_with_their_fronts_lined_up_get_the_glyph_in_the_gap_between_them()
    {
        // Part 1 is x 0–10, part 2 x 16–20, both from y 0: their south sides are flush on y = 0
        // and share nothing, so the glyph is halfway across the gap, x 13.
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.South), LocalFeatures.Edge(Second, BoxEdge.South));
        RelationshipGlyph glyph = Only(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(16, 0, 4, 12), flush));

        Assert.Equal(13, glyph.XInches, 9);
        Assert.Equal(0, glyph.YInches, 9);
        Assert.Equal(1, glyph.AlongX, 9);
        Assert.Equal(0, glyph.AlongY, 9);
    }

    [Fact]
    public void A_part_lined_up_before_the_first_one_gets_the_glyph_in_the_gap_on_that_side()
    {
        // The same row the other way round: part 2 (x −6 to −2) is before part 1's start, so the
        // gap is x −2 to 0 and the glyph at x −1.
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.South), LocalFeatures.Edge(Second, BoxEdge.South));
        RelationshipGlyph glyph = Only(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(-6, 0, 4, 12), flush));

        Assert.Equal(-1, glyph.XInches, 9);
        Assert.Equal(0, glyph.YInches, 9);
    }

    [Fact]
    public void A_corner_on_a_corner_gets_a_coincident_glyph_on_the_corner()
    {
        // Part 2's south-west corner sits on part 1's north-east corner, (10, 12).
        Coincident coincident = new(RelationshipId.New(), LocalFeatures.Corner(First, BoxCorner.NorthEast), LocalFeatures.Corner(Second, BoxCorner.SouthWest));
        RelationshipGlyph glyph = Only(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(10, 12, 4, 4), coincident));

        Assert.Equal(GlyphKind.Coincident, glyph.Kind);
        Assert.Equal(10, glyph.XInches, 9);
        Assert.Equal(12, glyph.YInches, 9);
        Assert.Equal(0, glyph.AlongX);
        Assert.Equal(0, glyph.AlongY);
    }

    [Fact]
    public void A_turned_part_is_measured_along_its_own_side()
    {
        // Part 1 is 10 × 4 turned a quarter turn about (0, 0): its local east side runs from (0, 10)
        // to (−4, 10). Part 2, x −4 to 0 from y 10, sits on it; the glyph is at (−2, 10), and the
        // line runs the way part 1's side does, west.
        Box turned = Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(10), Length.Inches(4), Box.DefaultDepth, Angle.Degrees(90));
        Box above = Box.AsDrawn(Second, LayerId.Default, Point2.Inches(-4, 10), Length.Inches(4), Length.Inches(6), Box.DefaultDepth, Angle.Zero);
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.South));
        Sketch sketch = Sketch.Empty.WithEntity(turned).WithEntity(above).WithRelationship(flush);

        RelationshipGlyph glyph = Only(sketch);

        Assert.Equal(-2, glyph.XInches, 9);
        Assert.Equal(10, glyph.YInches, 9);
        Assert.Equal(-1, glyph.AlongX, 9);
        Assert.Equal(0, glyph.AlongY, 9);
    }

    [Fact]
    public void Faces_the_plan_does_not_show_as_sides_get_no_glyph()
    {
        // Two tops flush: a real relationship, but no side of the plan to put it on.
        Flush tops = new(RelationshipId.New(), new FeatureRef(First, BoxFeature.Face(BoxFace.Top)), new FeatureRef(Second, BoxFeature.Face(BoxFace.Top)));
        Assert.Empty(RelationshipGlyphs.Of(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(20, 0, 4, 12), tops)));

        // One side and one top: the side alone does not make a glyph either.
        Flush mixed = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), new FeatureRef(Second, BoxFeature.Face(BoxFace.Top)));
        Assert.Empty(RelationshipGlyphs.Of(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(20, 0, 4, 12), mixed)));
    }

    [Fact]
    public void A_corner_relationship_on_something_that_is_not_a_plan_corner_gets_no_glyph()
    {
        // A top vertex is a point, but not one of the four corners the plan draws.
        Coincident vertex = new(
            RelationshipId.New(),
            new FeatureRef(First, BoxFeature.Vertex(BoxCorner.NorthEast, BoxLevel.Top)),
            LocalFeatures.Corner(Second, BoxCorner.SouthWest));
        Assert.Empty(RelationshipGlyphs.Of(Two(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(10, 12, 4, 4), vertex)));
    }

    [Fact]
    public void A_relationship_whose_part_is_gone_and_the_kinds_the_plan_does_not_mark_get_no_glyph()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(10, 0, 4, 12));
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.West));
        Coincident corner = new(RelationshipId.New(), LocalFeatures.Corner(First, BoxCorner.NorthEast), LocalFeatures.Corner(Second, BoxCorner.NorthWest));
        Sketch withoutSecond = design.Sketch.WithRelationship(flush).WithoutEntity(Second);
        Sketch withoutFirst = design.Sketch.WithRelationship(corner).WithoutEntity(First);

        Assert.Empty(RelationshipGlyphs.Of(withoutSecond));
        Assert.Empty(RelationshipGlyphs.Of(withoutFirst));

        Distance apart = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.West), Length.Inches(2));
        Assert.Empty(RelationshipGlyphs.Of(design.Sketch.WithRelationship(apart)));
    }

    [Fact]
    public void A_side_with_no_length_gets_no_glyph()
    {
        // A part squashed to no depth (north to south) has an east side that is a point: no line to mark.
        Box flat = Box.AsDrawn(First, LayerId.Default, Point2.Inches(0, 0), Length.Inches(10), Length.Zero, Box.DefaultDepth, Angle.Zero);
        Box beside = Box.AsDrawn(Second, LayerId.Default, Point2.Inches(10, 0), Length.Inches(4), Length.Inches(4), Box.DefaultDepth, Angle.Zero);
        Flush flush = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.West));

        Assert.Empty(RelationshipGlyphs.Of(Sketch.Empty.WithEntity(flat).WithEntity(beside).WithRelationship(flush)));
    }

    [Fact]
    public void Glyphs_come_in_the_relationship_lists_order()
    {
        Design design = EditingBuilder.Design(EditingBuilder.At(0, 0, 10, 12), EditingBuilder.At(10, 0, 4, 12));
        Flush side = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.East), LocalFeatures.Edge(Second, BoxEdge.West));
        Flush fronts = new(RelationshipId.New(), LocalFeatures.Edge(First, BoxEdge.South), LocalFeatures.Edge(Second, BoxEdge.South));
        Sketch sketch = design.Sketch.WithRelationship(side).WithRelationship(fronts);

        Assert.Equal(sketch.RelationshipsInOrder.Select(r => r.Id), RelationshipGlyphs.Of(sketch).Select(g => g.Id));
    }
}
