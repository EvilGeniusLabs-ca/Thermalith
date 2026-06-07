using System.Text.Json;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;

namespace Thermalith.Core.History;

/// <summary>
/// Snapshot-based undo/redo (build spec §6.4). A <see cref="LabelDocument"/> is small and fully
/// JSON-serializable, so each committed edit clones the whole document — dead simple, no command
/// deltas. Snapshots are pushed at <b>gesture boundaries</b> (once per user action, not per frame),
/// so cost stays one clone per action. Two stacks, undo + redo.
/// </summary>
public sealed class SnapshotHistory
{
    private readonly Stack<LabelDocument> _undo = new();
    private readonly Stack<LabelDocument> _redo = new();
    private readonly int _maxDepth;

    public SnapshotHistory(LabelDocument initial, int maxDepth = 200)
    {
        if (maxDepth < 1) throw new ArgumentOutOfRangeException(nameof(maxDepth));
        _maxDepth = maxDepth;
        Current = Clone(initial);
    }

    /// <summary>The live document. Replaced (not mutated) by <see cref="Commit"/>, <see cref="Undo"/>, <see cref="Redo"/>.</summary>
    public LabelDocument Current { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    /// <summary>Commit a new document state at a gesture boundary: the prior state becomes undoable; redo is cleared.</summary>
    public void Commit(LabelDocument next)
    {
        _undo.Push(Current);
        TrimDepth();
        _redo.Clear();
        Current = Clone(next);
    }

    /// <summary>Restore the previous snapshot. No-op (returns Current) when there is nothing to undo.</summary>
    public LabelDocument Undo()
    {
        if (!CanUndo) return Current;
        _redo.Push(Current);
        Current = _undo.Pop();
        return Current;
    }

    /// <summary>Re-apply the most recently undone snapshot. No-op when there is nothing to redo.</summary>
    public LabelDocument Redo()
    {
        if (!CanRedo) return Current;
        _undo.Push(Current);
        Current = _redo.Pop();
        return Current;
    }

    private void TrimDepth()
    {
        if (_undo.Count <= _maxDepth) return;
        // Drop the oldest snapshot by rebuilding the stack without its bottom entry.
        var kept = _undo.ToArray();           // top → bottom
        _undo.Clear();
        for (var i = kept.Length - 2; i >= 0; i--)
            _undo.Push(kept[i]);
    }

    /// <summary>Deep clone via the canonical JSON round-trip — every model is JSON-serializable (§6.4).</summary>
    public static LabelDocument Clone(LabelDocument doc)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(doc, LabelJson.Options);
        return JsonSerializer.Deserialize<LabelDocument>(json, LabelJson.Options)
            ?? throw new InvalidOperationException("Document clone failed to round-trip.");
    }
}
