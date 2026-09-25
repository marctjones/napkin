namespace Napkin.Core.Geometry.Tests;

/// <summary>
/// The building inputs a person types (issues #18, #19): the project's code choice and site values,
/// and a wall's inputs on its box. Each is set by its own request, exactly, moving nothing.
/// </summary>
public class BuildingInputsTests
{
    [Fact]
    [Trait("Feature", "BLD-001")]
    public void The_code_and_the_site_are_set_exactly_and_move_nothing()
    {
        SketchBuilder builder = new();
        EntityId id = builder.AddBox(0, 0, 144, 4);
        DirectUpdater updater = new();
        CodeChoice code = new("us-zz-test", 1, CodeMode.Following, null);
        SiteValues site = SiteValues.NotEntered with { GroundSnowLoadPsf = 30 };

        Solved chosen = Assert.IsType<Solved>(updater.Apply(builder.Sketch, new SetCode(code)));
        Solved sited = Assert.IsType<Solved>(updater.Apply(chosen.Sketch, new SetSite(site)));

        Assert.Equal(code, sited.Sketch.Code);
        Assert.Equal(30, sited.Sketch.Site.GroundSnowLoadPsf);
        Assert.Null(sited.Sketch.Site.UltimateWindSpeedMph);
        Assert.Equal(builder.Sketch.Find(id), sited.Sketch.Find(id));
        Assert.True(sited.Changes.IsEmpty);
        Assert.NotEqual(builder.Sketch, sited.Sketch);

        Solved cleared = Assert.IsType<Solved>(updater.Apply(sited.Sketch, new SetCode(null)));
        Assert.Null(cleared.Sketch.Code);
    }

    [Fact]
    [Trait("Feature", "BLD-005")]
    public void A_walls_bracing_assignments_compare_by_value_in_order_and_ride_on_its_box()
    {
        EntityId opening = EntityId.New();
        BracingAssignment first = new(null, opening, "zz-panel");
        BracingAssignment second = new(opening, null, "zz-board");
        WallInputs one = new(null, null, [first, second]);
        WallInputs same = new(null, null, [first, second]);
        WallInputs swapped = new(null, null, [second, first]);

        Assert.Equal(one, same);
        Assert.Equal(one.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(one, swapped);
        Assert.NotEqual(one, new WallInputs(null, null));
        Assert.False(one.Equals(null));
        Assert.Same(one, one.OrNull());
        Assert.Null(new WallInputs(null, null).OrNull());
        Assert.Empty(new WallInputs("zz-roof", null, default).Bracing);

        SketchBuilder builder = new();
        EntityId id = builder.AddBox(0, 0, 144, 4);
        Solved set = Assert.IsType<Solved>(new DirectUpdater().Apply(builder.Sketch, new SetWallInputs(id, one)));
        Assert.Equal(one, set.Sketch.Find<Box>(id)!.WallInputs);
        Assert.NotEqual(builder.Sketch, set.Sketch);
    }

    [Fact]
    [Trait("Feature", "BLD-001")]
    public void A_walls_inputs_ride_on_its_box_and_nothing_entered_is_null()
    {
        SketchBuilder builder = new();
        EntityId id = builder.AddBox(0, 0, 144, 4);
        DirectUpdater updater = new();

        Solved set = Assert.IsType<Solved>(updater.Apply(builder.Sketch, new SetWallInputs(id, new WallInputs("test-roof", null))));
        Assert.Equal(new WallInputs("test-roof", null), set.Sketch.Find<Box>(id)!.WallInputs);
        Assert.Equal([id], set.Changes.Modified);

        // Both fields null is the same as none: the one spelling of "nothing entered".
        Solved emptied = Assert.IsType<Solved>(updater.Apply(set.Sketch, new SetWallInputs(id, new WallInputs(null, null))));
        Assert.Null(emptied.Sketch.Find<Box>(id)!.WallInputs);

        Rejected spacing = Assert.IsType<Rejected>(updater.Apply(builder.Sketch, new SetWallInputs(id, new WallInputs(null, Length.Zero))));
        Assert.Equal(RejectionReason.NonPositiveSize, spacing.Reason);
        Rejected missing = Assert.IsType<Rejected>(updater.Apply(builder.Sketch, new SetWallInputs(new EntityId(Guid.NewGuid()), null)));
        Assert.Equal(RejectionReason.UnknownEntity, missing.Reason);
        EntityId node = builder.AddNode(1, 1);
        Rejected notABox = Assert.IsType<Rejected>(updater.Apply(builder.Sketch, new SetWallInputs(node, null)));
        Assert.Equal(RejectionReason.DanglingReference, notABox.Reason);
    }
}
