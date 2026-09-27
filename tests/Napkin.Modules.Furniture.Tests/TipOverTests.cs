using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// The tip-over estimate (docs/design/furniture-checks.md §4, #219), worked by hand in exact fractions
/// on a small oak chest: 30" wide, 18" deep, 36" tall, one drawer 12" deep opening 12". Northern red
/// oak at 12 % MC is 62.43 × 0.63 × 1.12 = 44.050608 lb/ft³ (Wood Handbook Eq. 4-14 with Table 5–3b).
/// </summary>
public class TipOverTests
{
    static readonly LayerId Parts = LayerId.New();

    static Length In(decimal inches) => Length.FromInches((double)inches, Rounding.HalfToEven);

    static Box Part(string name, decimal x, decimal y, decimal z, decimal dx, decimal dy, decimal dz, string? species = "Oak, northern red", DrawerMark? drawer = null, string? stock = null, int quantity = 1)
        => new(EntityId.New(), Parts, new Point3(In(x), In(y), In(z)), In(dx), In(dy), In(dz), BoxFace.Top, Angle.Zero)
        {
            Name = name,
            Part = new Part(stock, species, quantity, new PlanAxes(PartDimension.Length, PartDimension.Width)) { Drawer = drawer },
        };

    static Sketch Chest(FurnitureKind kind = FurnitureKind.ClothingStorage, bool anchored = false, bool drawer = true, string backSpecies = "Oak, northern red", decimal footDepth = 18)
    {
        Box front = Part("Drawer front", 0.75m, 0, 20, 28.5m, 0.75m, 12, drawer: drawer ? new DrawerMark(In(12)) : null);
        Box bottom = Part("Drawer bottom", 0.75m, 0.75m, 20, 28.5m, 16, 0.5m);
        Sketch sketch = Sketch.Empty.WithLayer(new Layer(Parts, "Parts"))
            .WithEntity(Part("Side, west", 0, 0, 0, 0.75m, footDepth, 36))
            .WithEntity(Part("Side, east", 29.25m, 0, 0, 0.75m, footDepth, 36))
            .WithEntity(Part("Top", 0.75m, 0, 35.25m, 28.5m, 18, 0.75m))
            .WithEntity(Part("Bottom", 0.75m, 0, footDepth == 18 ? 0 : 1, 28.5m, 18, 0.75m))
            .WithEntity(Part("Back", 0.75m, 17.25m, 0.75m, 28.5m, 0.75m, 34.5m, backSpecies))
            .WithEntity(front)
            .WithEntity(bottom)
            .WithRelationship(new Joint(
                new RelationshipId(Guid.NewGuid()),
                new FeatureRef(front.Id, BoxFeature.Face(BoxFace.North)),
                new FeatureRef(bottom.Id, BoxFeature.Face(BoxFace.South)),
                JointType.Butt,
                null,
                Fastening.None,
                true));
        return sketch with { Furniture = new FurnitureMarks(kind, anchored) };
    }

    [Fact]
    public void Northern_red_oak_weighs_44_05_lb_a_cubic_foot_at_12_percent()
        => Assert.Equal(44.050608m, TipOver.DensityLbPerFt3(WoodProperties.Shipped.Find("Oak, northern red")!));

    [Fact]
    [Trait("Feature", "FURN-003")]
    public void The_oak_chest_stands_open_and_pulled_but_tips_with_a_child_on_its_open_drawer()
    {
        TipOverReport report = TipOver.Of(Chest(), MaterialsLibrary.Shipped);

        // 36" tall, 75.54 lb, 28.5 × 16.75 × 12 = 3.315 ft³ of drawer: in scope.
        Assert.StartsWith("This is a clothing storage unit as ASTM F2057-23 defines it (≈36 in. tall, ≈76 lb, ≈3.3 ft³ of drawers;", report.Scope, StringComparison.Ordinal);
        Assert.EndsWith("if sold, 16 CFR 1261 applies.", report.Scope, StringComparison.Ordinal);

        // By hand: Σ W(y − 0), the drawer's parts 12" forward; fill 8.5 × 3.315 ft³ at y = 67/8 − 12; pull 10 × 36;
        // tilt sin θ = 0.43/18, lever (y)cos θ − z sin θ, 60 lb at y = −12, z = 32.
        Assert.Equal([533.83m, 275.98m, -163.91m], report.Lines.Select(line => Math.Round(line.Margin!.Value, 2)));
        Assert.Equal("§9.2.1, every drawer open and filled at 8.5 lb/ft³ of its whole box (more than its inside, so it errs toward tipping): stands, with ≈534 lb·in to spare.", report.Lines[0].Text);
        Assert.Equal("§9.2.2, 10 lbf pulled forward 36 in. up, drawers open and empty (88 FR 28405 does not say; napkin opens them): stands, with ≈276 lb·in to spare.", report.Lines[1].Text);
        Assert.Equal("§9.2.3, 60 lb on the front edge of Drawer front's open drawer, the unit tilted forward on a 0.43\" block, drawers otherwise empty: tips, short by ≈164 lb·in.", report.Lines[2].Text);
        Assert.Equal("Tip-over estimate — approximates ASTM F2057-23 §9.2 as described at 88 FR 28405, never 'complies'.", report.Notes[0]);
    }

    [Fact]
    public void Unmarked_it_gets_the_scope_flag_only_and_anchored_it_still_gets_the_estimate()
    {
        TipOverReport unmarked = TipOver.Of(Chest(FurnitureKind.None), MaterialsLibrary.Shipped);
        Assert.StartsWith("This is a clothing storage unit", unmarked.Scope, StringComparison.Ordinal);
        Assert.Empty(unmarked.Lines);

        TipOverReport anchored = TipOver.Of(Chest(anchored: true), MaterialsLibrary.Shipped);
        Assert.Equal(3, anchored.Lines.Length);
        Assert.Contains("Anchored to the wall; the test is of the unit unanchored", anchored.Notes[0], StringComparison.Ordinal);
    }

    [Fact]
    public void A_part_without_a_density_gives_no_margins_and_says_which()
    {
        TipOverReport report = TipOver.Of(Chest(backSpecies: "oak"), MaterialsLibrary.Shipped);
        Assert.Equal("Clothing storage scope: napkin cannot say — input missing: density, for Back (a sheet good or a species not in the table).", report.Scope);
        TipOverLine line = Assert.Single(report.Lines);
        Assert.Null(line.Margin);
        Assert.StartsWith("Tip-over: not estimated — input missing: density, for Back.", line.Text, StringComparison.Ordinal);

        // The DIY coffee table said to be clothing storage: its plywood has no cited density.
        Sketch diy = Assert.IsType<Loaded>(SceneReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "samples", "diy-coffee-table-drawers.scene.json"))).Sketch;
        TipOverReport table = TipOver.Of(diy with { Furniture = new FurnitureMarks(FurnitureKind.ClothingStorage, false) }, MaterialsLibrary.Shipped);
        Assert.Contains("Drawer bottom, A", table.Scope, StringComparison.Ordinal);
        Assert.Null(Assert.Single(table.Lines).Margin);
    }

    [Fact]
    public void With_no_drawer_the_child_test_is_not_estimated_and_the_chest_is_out_of_scope()
    {
        TipOverReport report = TipOver.Of(Chest(drawer: false), MaterialsLibrary.Shipped);
        Assert.StartsWith("Not a clothing storage unit by F2057-23's scope (≈36 in. tall, ≈76 lb, ≈0.0 ft³ of drawers;", report.Scope, StringComparison.Ordinal);
        Assert.Equal("§9.2.3: not estimated — no part is marked a drawer, so there is no open drawer to stand the weight on.", report.Lines[2].Text);
        Assert.Null(report.Lines[2].Margin);
    }

    [Fact]
    public void A_footprint_no_deeper_than_the_block_is_not_estimated_and_copies_are_noted()
    {
        // Sides only 1/4" deep on the floor, the bottom lifted clear of it.
        TipOverReport shallow = TipOver.Of(Chest(footDepth: 0.25m), MaterialsLibrary.Shipped);
        Assert.StartsWith("Tip-over: not estimated — what stands on the floor is not deeper than the 0.43\" test block.", Assert.Single(shallow.Lines).Text, StringComparison.Ordinal);

        Sketch pairs = Chest();
        Box top = pairs.Entities.Values.OfType<Box>().Single(box => box.Name == "Top");
        TipOverReport report = TipOver.Of(pairs.WithEntity(top with { Part = top.Part! with { Quantity = 2 } }), MaterialsLibrary.Shipped);
        Assert.Contains("Top stands for 2 pieces, weighed where it is drawn.", report.Notes);

        Assert.Equal(string.Empty, TipOver.Of(Sketch.Empty, MaterialsLibrary.Shipped).Scope);
    }
}
