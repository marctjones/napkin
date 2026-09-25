namespace Napkin.Core.Geometry.Tests;

/// <summary>The structural request that adds a layer (#18: a wall tool's first wall on a design with no "Wall" layer).</summary>
public class AddLayerTests
{
    [Fact]
    public void ALayerIsAddedAfterTheOthersAndMovesNothing()
    {
        Layer wall = new(LayerId.New(), "Wall");
        Solved solved = Assert.IsType<Solved>(DirectUpdater.Instance.Apply(Sketch.Empty, new AddLayer(wall)));

        Assert.Equal([Layer.Default, wall], solved.Sketch.Layers);
        Assert.Equal(ChangeSet.Empty, solved.Changes);
    }

    [Fact]
    public void ALayerAlreadyThereIsRefused()
    {
        Rejected rejected = Assert.IsType<Rejected>(DirectUpdater.Instance.Apply(Sketch.Empty, new AddLayer(Layer.Default)));
        Assert.Equal(RejectionReason.DuplicateEntity, rejected.Reason);
    }
}
