using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;

namespace TheGame.Core.OS;

/// <summary>
/// A robust backend for a terminal application, managing process I/O and buffering.
/// </summary>
public class TerminalBackend : ITerminal {
    private readonly List<TerminalLine> _lines = new();
    private readonly object _lineLock = new();
    private readonly int _bufferHeight;
    
    private Process _activeProcess;
    private TerminalReader _activeReader;
    
    private struct PendingCommand {
        public string Command;
        public string Operator; // "&", "&&", "||", or ""
    }
    private Queue<PendingCommand> _commandQueue = new();
    private int _lastExitCode = 0;

    public IReadOnlyList<TerminalLine> Lines {
        get {
            lock (_lineLock) {
                return _lines.ToList();
            }
        }
    }

    public int BufferHeight => _bufferHeight;

    public event Action OnBufferChanged;

    public TerminalBackend(int bufferHeight = 1000) {
        _bufferHeight = bufferHeight;
    }

    public void AttachProcess(Process process) {
        if (process == null) return;
        
        DetachActiveProcess();

        _activeProcess = process;
        _activeReader = new TerminalReader();
        
        // Setup Standard I/O
        process.IO.In = _activeReader;
        process.IO.Out = new TerminalWriter(AddLine, Color.White, "STDOUT");
        process.IO.Error = new TerminalWriter(AddLine, Color.Red, "STDERR");
        
        ProcessManager.Instance.OnProcessTerminated += OnProcessTerminated;
    }

    private void DetachActiveProcess() {
        if (_activeProcess != null) {
            ProcessManager.Instance.OnProcessTerminated -= OnProcessTerminated;
            _activeProcess = null;
        }
    }

    private void OnProcessTerminated(Process process) {
        if (process == _activeProcess) {
            _lastExitCode = process.ExitCode;
            DetachActiveProcess();
            RunNextCommand();
        }
    }

    public void ExecuteCommand(string input) {
        if (string.IsNullOrWhiteSpace(input)) return;

        var commands = ParseCommands(input);
        foreach (var cmd in commands) {
            _commandQueue.Enqueue(cmd);
        }

        if (_activeProcess == null) {
            RunNextCommand();
        }
    }

    private void RunNextCommand() {
        if (_commandQueue.Count == 0) {
            // No more commands, notify UI that we're ready for new input
            OnBufferChanged?.Invoke(); 
            return;
        }

        var pending = _commandQueue.Dequeue();
        
        // Check conditional operators
        if (pending.Operator == "&&" && _lastExitCode != 0) {
            // Skip this command and all subsequent conditional ones
            ClearRemainingConditionalCommands();
            RunNextCommand();
            return;
        }
        if (pending.Operator == "||" && _lastExitCode == 0) {
            // Skip
            RunNextCommand();
            return;
        }

        // Start the process
        // Note: TerminalBackend needs a way to start processes.
        // Usually, the terminal app calls Shell.Process.Start or similar.
        // We'll add a delegate or use Shell.Process directly if we assume it's available.
        StartProcess(pending.Command);
    }

    private void ClearRemainingConditionalCommands() {
        while (_commandQueue.Count > 0 && (_commandQueue.Peek().Operator == "&&" || _commandQueue.Peek().Operator == "||")) {
            _commandQueue.Dequeue();
        }
    }

    private void StartProcess(string commandLine) {
        // Simple command line parser: "app arg1 arg2"
        string[] parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return;

        string appId = parts[0];
        string[] args = parts.Skip(1).ToArray();

        var process = Shell.Process.Manager.StartProcess(appId, args);
        if (process != null) {
            AttachProcess(process);
        } else {
            AddLine($"{appId} is not recognized as an internal or external command", Color.Red, "SYSTEM");
            _lastExitCode = 1;
            RunNextCommand();
        }
    }

    private List<PendingCommand> ParseCommands(string input) {
        var results = new List<PendingCommand>();
        
        // Very basic parsing for &, &&, ||
        // A real parser would handle quotes, but let's keep it simple for now as requested.
        string[] tokens = Regex.Split(input, @"(&&|\|\||&)");
        
        string lastOp = "";
        foreach (var token in tokens) {
            string t = token.Trim();
            if (t == "&&" || t == "||" || t == "&") {
                lastOp = t;
            } else if (!string.IsNullOrWhiteSpace(t)) {
                results.Add(new PendingCommand { Command = t, Operator = lastOp });
                lastOp = "&"; // Default following operator is sequential if not specified (wait, no)
                // Actually, the operator applies to the NEXT command.
                // Example: "A && B" -> A runs first, then B runs if A succeeds.
                // So "&&" belongs to B.
            }
        }

        // Wait, the logic above is slightly flawed. 
        // "A && B || C"
        // Tokens: "A", "&&", "B", "||", "C"
        // Correct associations:
        // A: ""
        // B: "&&"
        // C: "||"
        
        return CorrectParseAssociations(tokens);
    }

    private List<PendingCommand> CorrectParseAssociations(string[] tokens) {
        var results = new List<PendingCommand>();
        string nextOp = "";
        
        for (int i = 0; i < tokens.Length; i++) {
            string t = tokens[i].Trim();
            if (string.IsNullOrEmpty(t)) continue;

            if (t == "&&" || t == "||" || t == "&") {
                nextOp = t;
            } else {
                results.Add(new PendingCommand { Command = t, Operator = nextOp });
                nextOp = ""; // Reset for next group
            }
        }
        return results;
    }

    public bool IsProcessRunning => _activeProcess != null;

    public void TerminateActiveProcess() {
        _activeProcess?.Terminate();
    }

    public void SendSignal(string signal) {
        if (_activeProcess != null) {
            Shell.Process.SendSignal(_activeProcess, signal);
        }
    }

    public void SendInput(string text) {
        if (_activeReader == null) return;
        
        // Handle special signals like Ctrl+C (can be passed as a special string or handled by UI)
        if (text == "\x03") { // ETX (Ctrl+C)
            _activeProcess?.Terminate(); // Or trigger OnSignalCancel
            AddLine("^C", Color.Yellow, "SYSTEM");
            return;
        }

        _activeReader.EnqueueInput(text);
        
        // Echo input if needed (usually handled by the terminal application)
    }

    public void Clear() {
        lock (_lineLock) {
            _lines.Clear();
        }
        OnBufferChanged?.Invoke();
    }

    public void WriteLine(string text, Color? color = null) {
        AddLine(text, color ?? Color.White, "SYSTEM");
    }

    private void AddLine(string text, Color defaultColor, string source) {
        if (text == null) return;
        
        string[] rawLines = text.Split('\n');
        
        lock (_lineLock) {
            for (int i = 0; i < rawLines.Length; i++) {
                string l = rawLines[i].Replace("\r", "");
                var segments = ProcessAnsiColors(l, defaultColor);
                
                // Special case: if this is NOT the first fragment of a split Write, 
                // and the backend supported partial lines, we would append.
                // But for now, we'll keep it simple: each Write call is at least one line.
                // UNLESS the last line didn't end with \n? 
                // Actually, the current TerminalBackend always treats AddLine as "starting" lines.
                
                _lines.Add(new TerminalLine(segments, source));
            }

            // Trim buffer
            if (_lines.Count > _bufferHeight) {
                _lines.RemoveRange(0, _lines.Count - _bufferHeight);
            }
        }
        
        OnBufferChanged?.Invoke();
    }

    private List<TerminalSegment> ProcessAnsiColors(string text, Color defaultColor) {
        var results = new List<TerminalSegment>();
        
        // Very basic ANSI color support (\u001b[XXm)
        // Regex to find ANSI escape codes
        var regex = new Regex(@"\u001b\[(\d+)m");
        var matches = regex.Matches(text);
        
        if (matches.Count == 0) {
            results.Add(new TerminalSegment(text, defaultColor));
            return results;
        }

        int lastIndex = 0;
        Color currentColor = defaultColor;

        foreach (Match match in matches) {
            // Add text before the match
            if (match.Index > lastIndex) {
                results.Add(new TerminalSegment(text.Substring(lastIndex, match.Index - lastIndex), currentColor));
            }

            // Update color
            int code = int.Parse(match.Groups[1].Value);
            currentColor = GetAnsiColor(code, defaultColor);
            
            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length) {
            results.Add(new TerminalSegment(text.Substring(lastIndex), currentColor));
        }

        return results;
    }

    private Color GetAnsiColor(int code, Color fallback) {
        return code switch {
            0 => fallback,        // Reset
            30 => Color.Black,
            31 => Color.Red,
            32 => Color.Green,
            33 => Color.Yellow,
            34 => Color.Blue,
            35 => Color.Magenta,
            36 => Color.Cyan,
            37 => Color.White,
            90 => Color.Gray,
            91 => Color.LightCoral,
            92 => Color.LightGreen,
            93 => Color.LightYellow,
            94 => Color.LightBlue,
            95 => Color.Fuchsia,
            96 => Color.LightCyan,
            97 => Color.White,
            _ => fallback
        };
    }
}
