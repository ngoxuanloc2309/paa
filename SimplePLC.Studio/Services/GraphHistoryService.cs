using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

public record GraphSnapshot(
    List<ProjectNodeData> Nodes,
    List<ProjectConnectionData> Connections,
    List<string> SelectedNodeIds,
    string? ActiveNodeId,
    string Description = "");

public record GraphClipboardData(
    List<ProjectNodeData> Nodes,
    List<ProjectConnectionData> Connections);

public class GraphHistoryService
{
    private const int MaxHistoryCount = 50;
    private readonly LinkedList<GraphSnapshot> _undoStack = new();
    private readonly Stack<GraphSnapshot> _redoStack = new();

    public event EventHandler? HistoryChanged;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;

    public void PushSnapshot(GraphSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        _undoStack.AddLast(snapshot);
        if (_undoStack.Count > MaxHistoryCount)
        {
            _undoStack.RemoveFirst();
        }

        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }

    public GraphSnapshot? Undo(GraphSnapshot currentState)
    {
        if (!CanUndo) return null;

        _redoStack.Push(currentState);
        var previousState = _undoStack.Last?.Value;
        _undoStack.RemoveLast();

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return previousState;
    }

    public GraphSnapshot? Redo(GraphSnapshot currentState)
    {
        if (!CanRedo) return null;

        _undoStack.AddLast(currentState);
        if (_undoStack.Count > MaxHistoryCount)
        {
            _undoStack.RemoveFirst();
        }

        var nextState = _redoStack.Pop();

        HistoryChanged?.Invoke(this, EventArgs.Empty);
        return nextState;
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        HistoryChanged?.Invoke(this, EventArgs.Empty);
    }
}
