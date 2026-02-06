using System;

namespace TheGame.Core.OS.History;

/// <summary>
/// Base interface for all undoable actions in the system.
/// </summary>
public interface ICommand : IDisposable {
    /// <summary>
    /// User-friendly description of the command (e.g., "Typing", "Move Button").
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this command modifies the document state. 
    /// If false, it's considered a navigation or selection-only command.
    /// </summary>
    bool ModifiesDocument { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Reverts the effects of the command.
    /// </summary>
    void Undo();

    /// <summary>
    /// Determines if this command can be merged with another command.
    /// </summary>
    bool CanMerge(ICommand other);

    /// <summary>
    /// Merges this command with another command of the same type.
    /// </summary>
    void MergeWith(ICommand other);
}
