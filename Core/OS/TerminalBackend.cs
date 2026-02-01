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
    private static readonly Color _ansiResetColor = new Color(0, 0, 0, 0); // Special marker for "default"
    private Color _currentAnsiColor = _ansiResetColor;

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
        
        ResetColor(); // Reset color for new process
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
        StartProcess(pending.Command);
    }

    private void ClearRemainingConditionalCommands() {
        while (_commandQueue.Count > 0 && (_commandQueue.Peek().Operator == "&&" || _commandQueue.Peek().Operator == "||")) {
            _commandQueue.Dequeue();
        }
    }

    private void StartProcess(string commandLine) {
        var parts = Regex.Matches(commandLine, @"[\""].+?[\""]|[^ ]+")
                         .Select(m => m.Value.Trim('"'))
                         .ToArray();

        if (parts.Length == 0) return;

        string command = parts[0];
        string[] args = parts.Skip(1).ToArray();

        string appId = ResolveAppId(command);

        if (appId != null) {
            // Start process and attach I/O BEFORE it initializes
            // Use a local capture to handle quick-exiting processes (race condition fix)
            Process startedProcess = null;
            var process = ProcessManager.Instance.StartProcess(appId, args, (p) => {
                startedProcess = p;
                AttachProcess(p);
            });
            
            var finalProcess = process ?? startedProcess;

            if (finalProcess == null) {
                 AddLine($"{command} failed to start\n", Color.Red, "SYSTEM");
                 _lastExitCode = 1;
                 RunNextCommand();
            }
        } else {
            AddLine($"{command} is not recognized as an internal or external command\n", Color.Red, "SYSTEM");
            _lastExitCode = 1;
            RunNextCommand();
        }
    }

    private string ResolveAppId(string command) {
        // 1. Try as exact AppId
        if (AppLoader.Instance.GetAppDirectory(command.ToUpper()) != null) return command.ToUpper();
        
        // 2. Try by directory path
        string fromPath = AppLoader.Instance.GetAppIdFromPath(command);
        if (fromPath != null) return fromPath;

        // 3. Try by local name with .sapp
        if (!command.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
            string withSapp = AppLoader.Instance.GetAppIdFromPath(command + ".sapp");
            if (withSapp != null) return withSapp;
        }

        // 4. Try in System32
        string system32Root = "C:\\Windows\\System32\\";
        string sys32Path = system32Root + command;
        string fromSys32 = AppLoader.Instance.GetAppIdFromPath(sys32Path);
        if (fromSys32 != null) return fromSys32;
        
        if (!command.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
            string sys32Sapp = AppLoader.Instance.GetAppIdFromPath(sys32Path + ".sapp");
            if (sys32Sapp != null) return sys32Sapp;
        }

        // 5. Check if it's just a raw AppId that hasn't been mapped to a path yet (unlikely but safe)
        if (ProcessManager.Instance.GetProcessByAppId(command.ToUpper()) != null) return command.ToUpper();

        return null;
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
            DebugLogger.Log($"TerminalBackend: Sending signal {signal} to {_activeProcess.AppId}");
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
            _currentAnsiColor = Color.White;
        }
        OnBufferChanged?.Invoke();
    }

    public void ResetColor() {
        _currentAnsiColor = _ansiResetColor;
    }

    private bool _isLastLineComplete = true;
    public bool IsLastLineComplete => _isLastLineComplete;

    public void WriteLine(string text, Color? color = null) {
        AddLine((text ?? "") + "\n", color ?? Color.White, "SYSTEM");
    }

    private void AddLine(string text, Color defaultColor, string source) {
        if (text == null) return;
        
        lock (_lineLock) {
            string[] rawLines = text.Split('\n');
            bool endsWithNewline = text.EndsWith("\n");
            
            for (int i = 0; i < rawLines.Length; i++) {
                // Skip the trailing empty string produced by Split('\n') when text ends with \n
                if (i == rawLines.Length - 1 && rawLines[i] == "" && endsWithNewline) break;

                string lineContent = rawLines[i].Replace("\r", "");
                var segments = ProcessAnsiColors(lineContent, defaultColor);

                if (!_isLastLineComplete && _lines.Count > 0) {
                    // Append to last line
                    var lastLine = _lines[^1];
                    if (lastLine.Segments == null) lastLine.Segments = new List<TerminalSegment>();
                    lastLine.Segments.AddRange(segments);
                    _lines[^1] = lastLine;
                } else {
                    // New line
                    _lines.Add(new TerminalLine(segments, source));
                }

                _isLastLineComplete = (i < rawLines.Length - 1) || endsWithNewline;
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
        
        var regex = new Regex("\x1b\\[[0-9;]*m");
        var matches = regex.Matches(text);
        
        if (matches.Count == 0) {
            Color colorToUse = (_currentAnsiColor == _ansiResetColor) ? defaultColor : _currentAnsiColor;
            results.Add(new TerminalSegment(text, colorToUse));
            return results;
        }

        int lastIndex = 0;
        foreach (Match match in matches) {
            // Add text before the match using current state
            if (match.Index > lastIndex) {
                string snippet = text.Substring(lastIndex, match.Index - lastIndex);
                if (!string.IsNullOrEmpty(snippet)) {
                    Color colorToUse = (_currentAnsiColor == _ansiResetColor) ? defaultColor : _currentAnsiColor;
                    results.Add(new TerminalSegment(snippet, colorToUse));
                }
            }

            // Update color state
            string codeContent = match.Value.TrimStart('\x1b', '[').TrimEnd('m');
            if (string.IsNullOrEmpty(codeContent) || codeContent == "0") {
                _currentAnsiColor = _ansiResetColor;
            } else {
                var parts = codeContent.Split(';');
                foreach (var part in parts) {
                    if (int.TryParse(part, out int code)) {
                        if (code == 0) _currentAnsiColor = _ansiResetColor;
                        else _currentAnsiColor = GetAnsiColor(code, _currentAnsiColor);
                    }
                }
            }
            
            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length) {
            Color colorToUse = (_currentAnsiColor == _ansiResetColor) ? defaultColor : _currentAnsiColor;
            results.Add(new TerminalSegment(text.Substring(lastIndex), colorToUse));
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
