using System;
using System.Collections.Generic;

namespace TheGame.Core.OS;

/// <summary>
/// Interface for a terminal backend that can manage input/output streams for processes.
/// </summary>
public interface ITerminal {
    /// <summary> The buffer of lines currently in the terminal. </summary>
    IReadOnlyList<TerminalLine> Lines { get; }

    /// <summary> Maximum number of lines to keep in the buffer. </summary>
    int BufferHeight { get; }

    /// <summary> Event triggered when the line buffer changes. </summary>
    event Action OnBufferChanged;

    /// <summary> Returns true if the last line in the buffer was terminated by a newline. </summary>
    bool IsLastLineComplete { get; }

    /// <summary> Attaches a process to this terminal, redirecting its standard I/O. </summary>
    void AttachProcess(Process process);

    /// <summary> Sends input text to the currently active process in this terminal. </summary>
    void SendInput(string text);

    /// <summary> Writes a line to the terminal buffer. </summary>
    void WriteLine(string text, Microsoft.Xna.Framework.Color? color = null);

    /// <summary> Clears the terminal buffer. </summary>
    void Clear();
}
