using System;
using System.Collections.Generic;

namespace TheGame.Core.OS.History;

/// <summary>
/// A command that groups multiple smaller commands together to be executed/undone as a single unit.
/// </summary>
public class CompositeCommand : ICommand {
    private readonly List<ICommand> _commands = new();
    
    public string Description { get; }
    public bool ModifiesDocument { get; private set; } = true;

    public bool HasCommands => _commands.Count > 0;

    public CompositeCommand(string description) {
        Description = description;
    }

    public void Add(ICommand command) {
        if (command == null) return;
        _commands.Add(command);
        if (command.ModifiesDocument) ModifiesDocument = true;
    }

    public void Execute() {
        foreach (var command in _commands) {
            command.Execute();
        }
    }

    public void Undo() {
        // Undo in reverse order
        for (int i = _commands.Count - 1; i >= 0; i--) {
            _commands[i].Undo();
        }
    }

    public bool CanMerge(ICommand other) => false; // Composite commands generally don't merge

    public void MergeWith(ICommand other) => throw new NotSupportedException();

    public void Dispose() {
        foreach (var command in _commands) {
            command.Dispose();
        }
        _commands.Clear();
    }
}
