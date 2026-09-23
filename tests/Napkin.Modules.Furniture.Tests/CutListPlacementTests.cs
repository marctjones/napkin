using System.Collections.Immutable;

using Napkin.Core.Geometry;
using Napkin.Core.Materials;
using Napkin.Core.Project;

namespace Napkin.Modules.Furniture.Tests;

/// <summary>
/// Property P16 of <c>docs/design/assembly-model.md</c> &#xA7;9.2, with &#xA7;9.1 case 20's sweep over
/// the samples: <strong>the cut list is blind to placement.</strong> For every box of every
/// sample, every one of the 24 orientations and a move in all three axes that the direct updater
/// accepts leaves <see cref="CutList.Of"/> — and the CSV made from it — equal to what it was.
/// </summary>
/// <remarks>
/// <para>
/// A cut list reads each part's three stored sizes through its plan axes and never its anchor,
/// its face-up or its spin (&#xA7;5, <c>CUT-002</c>). A request that changes only those, then,
/// can change nothing on the list; if one does, something read a coordinate it should not have.
/// </para>
/// <para>
/// Each sample is swept twice: as drawn, where a box held by a flush or a coincident refuses to
/// turn (<see cref="RejectionReason.OrientationWithRelationships"/>) and a move can carry a rigid
/// group with it; and with every relationship taken away, where every box turns every way. A
/// refused request is skipped — the property is about requests that succeed — and the test
/// insists that enough succeed for the sweep to mean something.
/// </para>
/// <para>
/// &#xA7;9.2 states the property for the shopping list too. There is no shopping list in this build
/// (#9 has not landed), so only the cut list and its export are checked here; the shopping list
/// is to consume the same rows, and the day it exists it belongs in this sweep.
/// </para>
/// </remarks>
public sealed class CutListPlacementTests
{
    private static readonly Angle[] QuarterTurns =
    [
        Angle.Zero,
        new Angle(Angle.RightAngleArcseconds),
        new Angle(2 * Angle.RightAngleArcseconds),
        new Angle(3 * Angle.RightAngleArcseconds),
    ];

    [Theory]
    [InlineData("coffee-table", false)]
    [InlineData("coffee-table", true)]
    [InlineData("rounded-corner-table", false)]
    [InlineData("rounded-corner-table", true)]
    [InlineData("wall-with-window", false)]
    [InlineData("wall-with-window", true)]
    [Trait("Feature", "CUT-002")]
    public void Turning_any_box_any_of_the_24_ways_leaves_the_cut_list_as_it_was(string fixture, bool unrelated)
    {
        Sketch sketch = Sample(fixture, unrelated);
        string before = Snapshot(sketch);
        int solved = 0;

        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            foreach (BoxFace up in Enum.GetValues<BoxFace>())
            {
                foreach (Angle spin in QuarterTurns)
                {
                    if (DirectUpdater.Instance.Apply(sketch, new SetOrientation(box.Id, up, spin)) is Succeeded turned)
                    {
                        solved++;
                        Assert.Equal(up, turned.Sketch.Find<Box>(box.Id)!.FaceUp);
                        Assert.Equal(before, Snapshot(turned.Sketch));
                    }
                }
            }
        }

        // Unrelated, every box turns every way (the one it is already in included); as drawn, the
        // rounded table's legs and aprons are free and turn, and the other two samples hold every
        // box by something, so there the sweep is all refusals — still a statement, but not this
        // property's, which is why each sample is also swept unrelated.
        int boxes = sketch.Entities.Values.OfType<Box>().Count();
        if (unrelated)
        {
            Assert.Equal(24 * boxes, solved);
        }
    }

    [Theory]
    [InlineData("coffee-table", false)]
    [InlineData("coffee-table", true)]
    [InlineData("rounded-corner-table", false)]
    [InlineData("rounded-corner-table", true)]
    [InlineData("wall-with-window", false)]
    [InlineData("wall-with-window", true)]
    [Trait("Feature", "CUT-002")]
    public void Moving_any_box_in_all_three_axes_leaves_the_cut_list_as_it_was(string fixture, bool unrelated)
    {
        Sketch sketch = Sample(fixture, unrelated);
        string before = Snapshot(sketch);
        int solved = 0;

        foreach (Box box in sketch.Entities.Values.OfType<Box>().OrderBy(box => box.Id))
        {
            foreach (Vector3 by in new[]
                     {
                         new Vector3(new Length(1024), new Length(-2048), new Length(4096)),
                         new Vector3(Length.Zero, Length.Zero, new Length(-16640)),
                     })
            {
                if (DirectUpdater.Instance.Apply(sketch, new SetPosition(box.Id, box.Anchor + by)) is Succeeded moved)
                {
                    solved++;
                    Assert.Equal(before, Snapshot(moved.Sketch));
                }
            }
        }

        Assert.True(solved > 0, $"No move of any box in {fixture} succeeded, so the sweep said nothing.");
    }

    /// <summary>
    /// A sample as drawn, or with every relationship taken away and every dimension with them —
    /// a driving dimension names a relationship, and a dimension on a box that turns could leave
    /// the plan (invariant 13), neither of which is what this sweep is about.
    /// </summary>
    private static Sketch Sample(string fixture, bool unrelated)
    {
        LoadResult result = SceneReader.ReadFile(ExpectedFixture.ScenePath(fixture));
        Sketch sketch = Assert.IsType<Loaded>(result).Sketch;
        if (!unrelated)
        {
            return sketch;
        }

        foreach (RelationshipId id in sketch.Relationships.Keys)
        {
            sketch = sketch.WithoutRelationship(id);
        }

        foreach (Dimension dimension in sketch.Entities.Values.OfType<Dimension>())
        {
            sketch = sketch.WithoutEntity(dimension.Id);
        }

        Assert.True(sketch.Validate().IsValid, sketch.Validate().ToString());
        return sketch;
    }

    /// <summary>
    /// Everything a cut-list row says, spelled out — its sequences as sequences, since a record's
    /// equality compares an <see cref="ImmutableArray{T}"/> by reference — and the CSV besides.
    /// </summary>
    private static string Snapshot(Sketch sketch)
    {
        ImmutableArray<CutListRow> rows = CutList.Of(sketch, MaterialsLibrary.Shipped);

        IEnumerable<string> lines = rows.Select(row => string.Join(
            " | ",
            row.Label,
            row.Quantity,
            row.Length.Units,
            row.Width.Units,
            row.Thickness.Units,
            row.Material,
            row.Unresolved,
            row.Stock?.ToString() ?? "no stock",
            string.Join(", ", row.Cuts),
            row.PlanAxes,
            string.Join(", ", row.Members),
            string.Join(" ", row.CutText)));

        return string.Join("\n", lines) + "\n--\n" + CutListCsv.ToCsv(rows);
    }
}
