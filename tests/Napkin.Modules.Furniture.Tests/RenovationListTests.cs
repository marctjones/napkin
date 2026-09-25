using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;

using Xunit;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// What the lists list when a design says what is already there and what comes out
/// (docs/design/renovation-sketches.md §6.2, test 11): the cut list and the hardware are New parts
/// only, a joint's fasteners are bought when at least one of its parts is New, and a demolished
/// part is counted under Demolition. Every count is by hand in a comment.
/// </summary>
public sealed class RenovationListTests
{
    private static Length In(double inches) => new((long)(inches * 1024));

    private static readonly PlanAxes LengthWidth = new(PartDimension.Length, PartDimension.Width);

    /// <summary>
    /// A post and three rails butted to its east face with pocket screws, each 3/4 thick and 5 1/2
    /// long at the joint (3 screws a joint, max(2, ceil(5.5 / 2)) = 3); a shelf that is not joined.
    /// </summary>
    private static (Sketch Sketch, Box Post, Box[] Rails, Box Shelf) Bench(Phase post, Phase rail1, Phase rail2, Phase rail3, Phase shelf)
    {
        int next = 0;
        Sketch sketch = Sketch.Empty;

        Box Add(string name, double x0, double x1, double y1, double z0, double z1, int quantity, Phase phase, PlanAxes axes)
        {
            Box box = new(
                new EntityId(new Guid(++next, 0, 0, new byte[8])),
                LayerId.Default,
                new Point3(In(x0), Length.Zero, In(z0)),
                In(x1 - x0),
                In(y1),
                In(z1 - z0),
                BoxFace.Top,
                Angle.Zero)
            {
                Name = name,
                Phase = phase,
                Part = new Part(null, null, quantity, axes) { Hardware = [new HardwareItem("Pull", 1)] },
            };
            sketch = sketch.WithEntity(box);
            return box;
        }

        PlanAxes lengthThickness = new(PartDimension.Length, PartDimension.Thickness);
        Box postBox = Add("Post", 0, 1.5, 1.5, 0, 40, 1, post, lengthThickness);
        Box[] rails =
        [
            Add("Rail", 1.5, 7.5, 0.75, 0, 5.5, 1, rail1, lengthThickness),
            Add("Rail", 1.5, 7.5, 0.75, 10, 15.5, 1, rail2, lengthThickness),
            Add("Rail", 1.5, 7.5, 0.75, 20, 25.5, 1, rail3, lengthThickness),
        ];
        foreach (Box rail in rails)
        {
            sketch = sketch.WithRelationship(new Joint(
                new RelationshipId(new Guid(++next, 1, 0, new byte[8])),
                new FeatureRef(postBox.Id, BoxFeature.Face(BoxFace.East)),
                new FeatureRef(rail.Id, BoxFeature.Face(BoxFace.West)),
                JointType.Butt,
                null,
                new Fastening(FasteningKind.PocketScrews, null, BoxFace.Top),
                true));
        }

        Box shelfBox = Add("Shelf", 30, 60, 10, 0, 0.75, 2, shelf, LengthWidth);
        return (sketch, postBox, rails, shelfBox);
    }

    [Fact]
    public void An_all_new_design_reads_exactly_as_before_renovation()
    {
        (Sketch sketch, _, _, _) = Bench(Phase.New, Phase.New, Phase.New, Phase.New, Phase.New);

        // Post, a row of three rails, and the shelf of two: three rows.
        Assert.Equal(3, CutList.Of(sketch, MaterialsLibrary.Shipped).Length);
        Assert.Empty(Demolition.Boxes(sketch));
        Assert.Null(Demolition.Header(sketch, Demolition.Boxes(sketch)));

        // Three joints of 3 pocket screws: 9. Hardware: 1 pull on each of 6 pieces (post, 3 rails, 2 shelves).
        Assert.Equal(9, Assert.Single(FastenerList.Of(sketch)).Count);
        Assert.Equal(6, Assert.Single(HardwareList.Of(sketch)).Count);
        Assert.Contains(SuppliesList.Of(sketch), row => row.Item == "Glue: 3 of 3 joints");
    }

    [Fact]
    public void The_cut_list_and_the_hardware_are_new_parts_only_and_a_joint_with_one_new_part_is_fastened()
    {
        // The post is already there; rail 1 is new, rail 2 already there, rail 3 comes out; the shelf is new.
        (Sketch sketch, Box post, Box[] rails, Box shelf) = Bench(Phase.Existing, Phase.New, Phase.Existing, Phase.Demolish, Phase.New);

        ImmutableArray<CutListRow> rows = CutList.Of(sketch, MaterialsLibrary.Shipped);

        // Only the new rail and the new shelf are cut.
        Assert.Equal([[shelf.Id], [rails[0].Id]], rows.Select(row => row.Members.ToArray()));
        Assert.DoesNotContain(rows, row => row.Members.Contains(post.Id));

        // Existing post – new rail 1: bought, 3. Existing – existing rail 2: already fastened.
        // Existing – demolished rail 3: nothing new, not bought. 3 in all.
        FastenerRow screws = Assert.Single(FastenerList.Of(sketch));
        Assert.Equal(3, screws.Count);
        Assert.Contains(SuppliesList.Of(sketch), row => row.Item == "Glue: 1 of 1 joint");

        // One pull on rail 1, one on each of the two new shelves: 3.
        Assert.Equal(3, Assert.Single(HardwareList.Of(sketch)).Count);
    }

    [Fact]
    public void A_demolished_part_is_counted_under_demolition_by_name_and_quantity()
    {
        (Sketch sketch, _, Box[] rails, Box shelf) = Bench(Phase.Existing, Phase.Demolish, Phase.Demolish, Phase.New, Phase.Demolish);
        Box wall = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, In(144), In(3.5), In(96), Angle.Zero) with
        {
            Name = "Wall 1",
            Phase = Phase.Demolish,
        };
        Box unnamed = Box.AsDrawn(EntityId.New(), LayerId.Default, Point2.Origin, In(10), In(10), In(1), Angle.Zero) with { Phase = Phase.Demolish };
        sketch = sketch.WithEntity(wall).WithEntity(unnamed);

        ImmutableArray<DemolitionLine> lines = Demolition.Boxes(sketch);

        // Rails 1 and 2 are one line of two; the shelf's quantity is 2; the wall and the unnamed box by name.
        Assert.Equal(
            [("Rail", 2), ("Shelf", 2), ("Wall 1", 1), ("Box", 1)],
            lines.Select(line => (line.Item, line.Count)).OrderBy(line => line.Item == "Rail" ? 0 : line.Item == "Shelf" ? 1 : line.Item == "Wall 1" ? 2 : 3));
        Assert.Equal(["Rail × 2", "Shelf × 2"], lines.Where(line => line.Count > 1).Select(line => line.Text).Order());
        Assert.Equal("Only what is New is listed; 6 items to remove are under Demolition.", Demolition.Header(sketch, lines));
        Assert.Equal("Wall 1 (assumes)", new DemolitionLine("Wall 1", 1, "assumes").Text);
        Assert.Equal(rails[2].Id, Assert.Single(CutList.Of(sketch, MaterialsLibrary.Shipped)).Members.Single());
        Assert.Equal(shelf.Name, "Shelf");
    }

    [Fact]
    public void The_header_says_when_nothing_or_one_thing_comes_out_and_the_csv_has_its_own_header()
    {
        (Sketch sketch, _, _, _) = Bench(Phase.Existing, Phase.New, Phase.New, Phase.New, Phase.New);
        Assert.Equal("Only what is New is listed; nothing comes out.", Demolition.Header(sketch, []));
        Assert.Equal("Only what is New is listed; 1 item to remove is under Demolition.", Demolition.Header(sketch, [new DemolitionLine("Post", 1, string.Empty)]));

        string csv = Demolition.ToCsv([new DemolitionLine("Wall 1: stud", 2, "assuming a regular 16\" layout"), new DemolitionLine("Shelf", 1, string.Empty)]);
        Assert.Equal("Demolition\nItem,Count,Note\nWall 1: stud,2,\"assuming a regular 16\"\" layout\"\nShelf,1,\n", csv);
    }
}
