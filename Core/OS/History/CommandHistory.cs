using System;
using System.Collections.Generic;
using System.Linq;

namespace TheGame.Core.OS.History;

/// <summary>
/// Manages a stack of undo and redo commands for a specific context (e.g., a document or designer tab).
/// </summary>
public class CommandHistory {
    private readonly Stack<ICommand> _undoStack = new();
    private readonly Stack<ICommand> _redoStack = new();
    private int _lastSavedIndex = 0;
    private int _currentIndex = 0;

    /// <summary>
    /// Maximum number of commands to keep in history.
    /// </summary>
    public int Limit { get; set; } = 100;

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Returns true if the current state differs from the last saved state.
    /// </summary>
    public bool IsDirty => _currentIndex != _lastSavedIndex;

    public event Action OnHistoryChanged;

    /// <summary>
    /// Executes a command and adds it to the undo stack.
    /// Clears the redo stack.
    /// </summary>
    public void Execute(ICommand command) {
        Execute(command, true);
    }

    private void Execute(ICommand command, bool runExecute) {
        if (command == null) return;

        // Try to merge with the last command
        if (_undoStack.Count > 0 && _undoStack.Peek().CanMerge(command)) {
            if (runExecute) command.Execute(); // Still execute it so the effects happen
            _undoStack.Peek().MergeWith(command);
            command.Dispose();
        } else {
            if (runExecute) command.Execute();
            _undoStack.Push(command);
            _currentIndex++;

            // Enforce limit
            if (_undoStack.Count > Limit) {
                TrimHistory();
            }
        }

        // Executing a new command always clears redo history
        ClearRedoStack();
        OnHistoryChanged?.Invoke();
    }

    public void NotifyChanged() {
        OnHistoryChanged?.Invoke();
    }

    private void TrimHistory() {
        var list = _undoStack.ToList();
        while (list.Count > Limit) {
            var oldest = list.Last();
            oldest.Dispose();
            list.RemoveAt(list.Count - 1);
        }
        _undoStack.Clear();
        for (int i = list.Count - 1; i >= 0; i--) {
            _undoStack.Push(list[i]);
        }
    }

    /// <summary>
    /// Undoes the last command.
    /// </summary>
    public void Undo() {
        if (!CanUndo) return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);
        _currentIndex--;

        OnHistoryChanged?.Invoke();
    }

    /// <summary>
    /// Redoes the last undone command.
    /// </summary>
    public void Redo() {
        if (!CanRedo) return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);
        _currentIndex++;

        OnHistoryChanged?.Invoke();
    }

    /// <summary>
    /// Returns the command that would be undone.
    /// </summary>
    public ICommand PeekUndo() => CanUndo ? _undoStack.Peek() : null;

    /// <summary>
    /// Returns the command that would be redone.
    /// </summary>
    public ICommand PeekRedo() => CanRedo ? _redoStack.Peek() : null;

    /// <summary>
    /// Marks the current state as saved.
    /// </summary>
    public void MarkAsSaved() {
        _lastSavedIndex = _currentIndex;
        OnHistoryChanged?.Invoke();
    }

    /// <summary>
    /// Clears the entire history.
    /// </summary>
    public void Clear() {
        foreach (var cmd in _undoStack) cmd.Dispose();
        foreach (var cmd in _redoStack) cmd.Dispose();
        _undoStack.Clear();
        _redoStack.Clear();
        _currentIndex = 0;
        _lastSavedIndex = 0;
        OnHistoryChanged?.Invoke();
    }

    private void ClearRedoStack() {
        foreach (var cmd in _redoStack) cmd.Dispose();
        _redoStack.Clear();
    }

    #region Transaction Support

    private CompositeCommand _activeTransaction;

    public void BeginTransaction(string description) {
        if (_activeTransaction != null) {
            // End previous transaction if nested - nested transactions not supported for simplicity
            EndTransaction();
        }
        _activeTransaction = new CompositeCommand(description);
    }

    public void EndTransaction() {
        if (_activeTransaction == null) return;

        if (_activeTransaction.HasCommands) {
            // Execute the group as a single unit in history, 
            // but DON'T re-run the commands because they were already executed in AddOrExecute
            Execute(_activeTransaction, false);
        } else {
            _activeTransaction.Dispose();
        }
        _activeTransaction = null;
    }

    /// <summary>
    /// Adds a command to the active transaction or executes it immediately if no transaction is active.
    /// </summary>
    public void AddOrExecute(ICommand command) {
        if (_activeTransaction != null) {
            _activeTransaction.Add(command);
            command.Execute(); // Execute immediately so following commands in transaction see the side effects
        } else {
            Execute(command);
        }
    }

    #endregion
}
