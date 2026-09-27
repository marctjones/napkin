using Napkin.Core.Geometry;
using Napkin.Core.Materials;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Shelf sag (docs/design/furniture-checks.md §3, slice B), worked by hand in exact fractions: a
/// 3/4" × 11 1/4" shelf dadoed into two sides 36" apart, 15 lb/sq ft (KCMA's shelf test load).
/// W = 15 × 36 × 11.25 / 144 = 42.1875 lb; I = 11.25 × 0.75³ / 12 = 0.3955078125 in⁴.
/// </summary>
public class ShelfSagTests
{
    static readonly LayerId Parts = LayerId.New();

    static Length In(decimal inches) => Length.FromInches((double)inches, Rounding.HalfToEven);

    static Box Part(string name, decimal x, decimal y, decimal z, decimal dx, decimal dy, decimal dz, string? species = null, string? stock = null)
        => new(EntityId.New(), Parts, new Point3(In(x), In(y), In(z)), In(dx), In(dy), In(dz), BoxFace.Top, Angle.Zero)
        {
            Name = name,
            Part = new Part(stock, species, 1, new PlanAxes(PartDimension.Length, PartDimension.Width)),
        };

    static int next;

    static Joint Join(Box receiving, BoxFace receivingFace, Box inserted, BoxFace insertedFace, JointType type = JointType.Groove) => new(
        new RelationshipId(new Guid(++next, 7, 0, new byte[8])),
        new FeatureRef(receiving.Id, BoxFeature.Face(receivingFace)),
        new FeatureRef(inserted.Id, BoxFeature.Face(insertedFace)),
        type,
        type == JointType.Groove ? In(0.25m) : null,
        Fastening.None,
        true);

    /// <summary>Two 3/4" sides 36" apart and a shelf between them, dadoed at both ends.</summary>
    static (Sketch Sketch, Box Shelf) Bookcase(string? species = "Oak, northern red", string? stock = null, bool bothEnds = true, JointType type = JointType.Groove)
    {
        Box west = Part("Side, west", 0, 0, 0, 0.75m, 11.25m, 30);
        Box east = Part("Side, east", 36.75m, 0, 0, 0.75m, 11.25m, 30);
        Box shelf = Part("Shelf", 0.75m, 0, 15, 36, 11.25m, 0.75m, species, stock);
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Parts, "Parts")).WithEntity(west).WithEntity(east).WithEntity(shelf)
            .WithRelationship(Join(west, BoxFace.East, shelf, BoxFace.West, type));
        return (bothEnds ? sketch.WithRelationship(Join(east, BoxFace.West, shelf, BoxFace.East, type)) : sketch, shelf);
    }

    static ShelfSagEstimate Estimate(Sketch sketch, Box shelf, decimal load = ShelfSag.KcmaLoadPsf, Length? limit = null)
    {
        (ShelfSagEstimate? estimate, string? missing) = ShelfSag.Estimate(ShelfSag.Of(sketch, shelf)!, load, limit, MaterialsLibrary.Shipped);
        Assert.Null(missing);
        return estimate!;
    }

    [Fact]
    [Trait("Feature", "FURN-001")]
    public void A_dadoed_oak_shelf_sags_three_hundredths_now_and_six_and_a_half_after_years()
    {
        (Sketch sketch, Box shelf) = Bookcase();
        ShelfSpan span = ShelfSag.Of(sketch, shelf)!;
        Assert.Equal((In(36), In(11.25m), In(0.75m)), (span.Span, span.Width, span.Thickness));

        // E_L = 1.1 × 1,820,000 = 2,002,000; G = 0.081 × E_L (Table 5–1 "Oak, red", GLT the smaller); A′ = 5/6 × 11.25 × 0.75.
        // δ = 5/384 × 42.1875 × 36³ / (2,002,000 × 0.3955078125) + 1/8 × 42.1875 × 36 / (162,162 × 7.03125) = 0.0325341325…
        ShelfSagEstimate estimate = Estimate(sketch, shelf);
        Assert.Equal(0.0325341325m, Math.Round(estimate.Instant, 10));
        Assert.Equal(0.0650682651m, Math.Round(estimate.LongTerm, 10));
        Assert.True(estimate.WithShear);
        Assert.Null(estimate.WithinLimit);
        Assert.Equal(
            "Shelf sag ≈ 0.03\" now, ≈ 0.07\" after years, under 15 lb/sq ft, KCMA's shelf test load (kcma.org, retrieved 2026-09-26). "
            + "An engineering estimate: Oak, northern red (Table 5–3b p. 5–10), span 3'-0\", Wood Handbook Eq. 9-2 with Table 9-1, bending and shear, "
            + "G from Table 5–1's \"Oak, red\" row; long-term twice the instant, per chapter 5's creep sentence.",
            estimate.Text);
    }

    [Fact]
    [Trait("Feature", "FURN-001")]
    public void A_species_table_5_1_lacks_is_bending_only_with_the_printed_E()
    {
        // Eastern white pine: δ = 5/384 × 42.1875 × 36³ / (1,240,000 × 0.3955078125) = 0.0522580645…
        (Sketch sketch, Box shelf) = Bookcase("Pine, eastern white");
        ShelfSagEstimate estimate = Estimate(sketch, shelf, limit: In(0.125m));
        Assert.Equal(0.0522580645m, Math.Round(estimate.Instant, 10));
        Assert.False(estimate.WithShear);
        Assert.True(estimate.WithinLimit);
        Assert.Contains("≈ 0.10\" after years, under 15 lb/sq ft, KCMA's", estimate.Text, StringComparison.Ordinal);
        Assert.Contains("; within the 1/8\" you typed", estimate.Text, StringComparison.Ordinal);
        Assert.Contains("bending only (Table 5–1 has no row for it", estimate.Text, StringComparison.Ordinal);

        // 0.1045… over a typed 1/16": over; a load other than KCMA's is said without the attribution.
        ShelfSagEstimate over = Estimate(sketch, shelf, 40, In(0.0625m));
        Assert.False(over.WithinLimit);
        Assert.Contains("under 40 lb/sq ft; over the 1/16\" you typed", over.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_species_or_on_a_sheet_good_there_is_no_estimate_and_it_says_why()
    {
        (Sketch typed, Box redOak) = Bookcase("red oak");
        Assert.Equal("Shelf sag: input missing — species. Pick one from the table; typed text is never interpreted.", ShelfSag.Estimate(ShelfSag.Of(typed, redOak)!, 15, null, MaterialsLibrary.Shipped).Missing);

        (Sketch sheet, Box plywood) = Bookcase(stock: "3/4 plywood");
        Assert.StartsWith("Shelf sag: not estimated — 3/4 plywood is a sheet good", ShelfSag.Estimate(ShelfSag.Of(sheet, plywood)!, 15, null, MaterialsLibrary.Shipped).Missing, StringComparison.Ordinal);
    }

    [Fact]
    public void Only_a_part_lying_flat_and_jointed_at_both_ends_is_a_shelf()
    {
        (Sketch sketch, Box shelf) = Bookcase();
        Assert.Null(ShelfSag.Of(sketch, sketch.Entities.Values.OfType<Box>().Single(box => box.Name == "Side, west")));
        Assert.Null(ShelfSag.Of(Bookcase(bothEnds: false).Sketch, Bookcase(bothEnds: false).Shelf));
        (Sketch oneEnd, Box single) = Bookcase(bothEnds: false);
        Assert.Null(ShelfSag.Of(oneEnd, single));
        Assert.Null(ShelfSag.Of(sketch, shelf with { Part = null }));

        // A butt at both ends is a shelf too; a half-lap is not one of the shelf joints.
        (Sketch butts, Box butted) = Bookcase(type: JointType.Butt);
        Assert.Equal(In(36), ShelfSag.Of(butts, butted)!.Span);
        (Sketch laps, Box lapped) = Bookcase(type: JointType.HalfLap);
        Assert.Null(ShelfSag.Of(laps, lapped));
    }

    [Fact]
    public void A_shelf_spanning_north_south_reads_its_span_along_y()
    {
        Box south = Part("Side, south", 0, 0, 0, 11.25m, 0.75m, 30);
        Box north = Part("Side, north", 0, 24.75m, 0, 11.25m, 0.75m, 30);
        Box shelf = Part("Shelf", 0, 0.75m, 15, 11.25m, 24, 0.75m, "Oak, white");
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Parts, "Parts")).WithEntity(south).WithEntity(north).WithEntity(shelf)
            .WithRelationship(Join(south, BoxFace.North, shelf, BoxFace.South))
            .WithRelationship(Join(north, BoxFace.South, shelf, BoxFace.North));
        ShelfSpan span = ShelfSag.Of(sketch, shelf)!;
        Assert.Equal((In(24), In(11.25m)), (span.Span, span.Width));

        // White oak's Table 5–1 row prints a dash for GLT: GLR alone is the ratio.
        Assert.Equal(0.086m, WoodProperties.Shipped.Find("Oak, white")!.Ratios!.Least);
        Assert.True(Estimate(sketch, shelf).WithShear);
    }

    [Theory]
    [InlineData(0.004, "under 0.01\"")]
    [InlineData(0.005, "0.01\"")]
    [InlineData(0.125, "0.13\"")]
    public void A_deflection_reads_to_two_places(double inches, string words) => Assert.Equal(words, ShelfSag.Words((decimal)inches));
}
