using Thermalith.Core.History;
using Thermalith.Core.Model;
using Xunit;

namespace Thermalith.Core.Tests;

public class HistoryTests
{
    private static LabelDocument DocNamed(string name) => new()
    {
        Metadata = new LabelMetadata { Name = name },
        Canvas = new Canvas { WidthMm = 40, HeightMm = 30 },
    };

    [Fact]
    public void Undo_and_redo_walk_the_snapshot_stacks()
    {
        var history = new SnapshotHistory(DocNamed("v0"));
        Assert.False(history.CanUndo);

        history.Commit(DocNamed("v1"));
        history.Commit(DocNamed("v2"));
        Assert.Equal("v2", history.Current.Metadata.Name);

        Assert.Equal("v1", history.Undo().Metadata.Name);
        Assert.Equal("v0", history.Undo().Metadata.Name);
        Assert.False(history.CanUndo);

        Assert.Equal("v1", history.Redo().Metadata.Name);
        Assert.Equal("v2", history.Redo().Metadata.Name);
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void Commit_clears_the_redo_stack()
    {
        var history = new SnapshotHistory(DocNamed("v0"));
        history.Commit(DocNamed("v1"));
        history.Undo();                 // back to v0, redo has v1
        Assert.True(history.CanRedo);

        history.Commit(DocNamed("v2"));  // new branch
        Assert.False(history.CanRedo);
        Assert.Equal("v2", history.Current.Metadata.Name);
    }

    [Fact]
    public void Snapshots_are_deep_clones_not_shared_references()
    {
        var original = DocNamed("v0");
        var history = new SnapshotHistory(original);

        Assert.NotSame(original, history.Current);

        history.Commit(original with { Metadata = new LabelMetadata { Name = "v1" } });
        var restored = history.Undo();
        Assert.NotSame(original, restored);
        Assert.Equal("v0", restored.Metadata.Name);
    }
}
