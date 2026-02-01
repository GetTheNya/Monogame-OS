using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;
using System.IO;

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
    public string WorkingDirectory { get; set; } = "C:\\";
    
    private class CommandCall {
        public string CommandLine;
        public string RedirectionPath;
        public bool AppendRedirection;
    }

    private class PipelineJob {
        public List<CommandCall> Commands = new();
        public string Operator; // "", "&&", "||", or ";"
    }

    private Queue<PipelineJob> _commandQueue = new();
    private List<Process> _activePipeline = new();
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
        _activeReader = new TerminalReader();
    }

    public void AttachProcess(Process process) {
        if (process == null) return;
        lock (_activePipeline) {
            if (!_activePipeline.Contains(process)) {
                _activePipeline.Add(process);
                _activeProcess = process;
                ProcessManager.Instance.OnProcessTerminated += OnProcessTerminated;
            }
        }
    }

    private void DetachActiveProcess() {
        // No longer using single active process tracking in the same way, 
        // but we still want to clean up if needed.
        _activeProcess = null;
    }

    private void OnProcessTerminated(Process process) {
        lock (_activePipeline) {
            if (_activePipeline.Contains(process)) {
                _activePipeline.Remove(process);
                
                // The exit code of the pipeline is usually the exit code of the LAST process
                // (though some shells have pipefail option)
                // We'll just take the exit code of any terminating process for now, 
                // but RunNextCommand only triggers when the WHOLE pipeline is empty.
                _lastExitCode = process.ExitCode;

                if (_activePipeline.Count == 0) {
                    _activeProcess = null;
                    RunNextCommand();
                }
            }
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

        var job = _commandQueue.Dequeue();
        
        // Check conditional operators
        if (job.Operator == "&&" && _lastExitCode != 0) {
            // Skip this command and all subsequent conditional ones
            ClearRemainingConditionalCommands();
            RunNextCommand();
            return;
        }
        if (job.Operator == "||" && _lastExitCode == 0) {
            // Skip
            RunNextCommand();
            return;
        }

        // Start the pipeline
        StartPipeline(job);
    }

    private void ClearRemainingConditionalCommands() {
        while (_commandQueue.Count > 0 && (_commandQueue.Peek().Operator == "&&" || _commandQueue.Peek().Operator == "||")) {
            _commandQueue.Dequeue();
        }
    }

    private void StartPipeline(PipelineJob job) {
        if (job.Commands.Count == 0) {
            RunNextCommand();
            return;
        }

        lock (_activePipeline) {
            _activePipeline.Clear();
            
            TextReader lastIn = _activeReader;

            for (int i = 0; i < job.Commands.Count; i++) {
                var cmdCall = job.Commands[i];
                bool isLast = (i == job.Commands.Count - 1);
                
                TextWriter currentOut = null;
                TextReader nextIn = null;

                if (!string.IsNullOrEmpty(cmdCall.RedirectionPath)) {
                    currentOut = new VfsWriter(cmdCall.RedirectionPath, cmdCall.AppendRedirection);
                } else if (!isLast) {
                    var bridge = new PipeBridge();
                    currentOut = bridge.Writer;
                    nextIn = bridge.Reader;
                } else {
                    currentOut = new TerminalWriter(AddLine, Color.White, "STDOUT");
                }

                var proc = StartProcess(cmdCall.CommandLine, lastIn, currentOut);
                if (proc != null) {
                    _activePipeline.Add(proc);
                }
                
                lastIn = nextIn;
            }

            if (_activePipeline.Count > 0) {
                _activeProcess = _activePipeline.Last(); // For legacy tracking, use the last one
            } else {
                RunNextCommand();
            }
        }
    }

    private Process StartProcess(string commandLine, TextReader inputOverride = null, TextWriter outputOverride = null) {
        var matches = Regex.Matches(commandLine, @"[\""].+?[\""]|[^ ]+");
        var parts = matches.Cast<Match>()
                         .Select(m => m.Value.Trim('"'))
                         .ToArray();

        if (parts.Length == 0) return null;

        string command = parts[0];
        string[] args = parts.Skip(1).ToArray();

        string appId = ResolveAppId(command);

        if (appId != null) {
            Process startedProcess = null;
            var process = ProcessManager.Instance.StartProcess(appId, args, (p) => {
                startedProcess = p;
                
                ResetColor();
                p.WorkingDirectory = this.WorkingDirectory;
                p.IO.In = inputOverride ?? _activeReader;
                p.IO.Out = outputOverride ?? new TerminalWriter(AddLine, Color.White, "STDOUT");
                p.IO.Error = new TerminalWriter(AddLine, Color.Red, "STDERR");
                
                ProcessManager.Instance.OnProcessTerminated += OnProcessTerminated;
            });
            
            var finalProcess = process ?? startedProcess;

            if (finalProcess == null) {
                 AddLine($"{command} failed to start\n", Color.Red, "SYSTEM");
                 _lastExitCode = 1;
            }
            return finalProcess;
        } else {
            AddLine($"{command} is not recognized as an internal or external command\n", Color.Red, "SYSTEM");
            _lastExitCode = 1;
            return null;
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

		// 4. Try in System32\TerminalApps
        string system32TerminalRoot = "C:\\Windows\\System32\\TerminalApps\\";
        string sys32TerminalPath = system32TerminalRoot + command;
        string fromSys32Terminal = AppLoader.Instance.GetAppIdFromPath(sys32TerminalPath);
        if (fromSys32Terminal != null) return fromSys32Terminal;

        // 5. Try in System32
        string system32Root = "C:\\Windows\\System32\\";
        string sys32Path = system32Root + command;
        string fromSys32 = AppLoader.Instance.GetAppIdFromPath(sys32Path);
        if (fromSys32 != null) return fromSys32;
        
        if (!command.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
            string sys32Sapp = AppLoader.Instance.GetAppIdFromPath(sys32Path + ".sapp");
            if (sys32Sapp != null) return sys32Sapp;
        }

        // 6. Check if it's just a raw AppId that hasn't been mapped to a path yet (unlikely but safe)
        if (ProcessManager.Instance.GetProcessByAppId(command.ToUpper()) != null) return command.ToUpper();

        return null;
    }

    private List<PipelineJob> ParseCommands(string input) {
        // 1. Split by sequential/logical operators: &&, ||, ;
        // We'll treat ; and & as sequential for now as per current code.
        var tokens = Regex.Split(input, @"(&&|\|\||;)");
        return CorrectParseAssociations(tokens);
    }

    private List<PipelineJob> CorrectParseAssociations(string[] tokens) {
        var results = new List<PipelineJob>();
        string nextOp = "";
        
        for (int i = 0; i < tokens.Length; i++) {
            string t = tokens[i].Trim();
            if (string.IsNullOrEmpty(t)) continue;

            if (t == "&&" || t == "||" || t == ";") {
                nextOp = t;
            } else {
                results.Add(new PipelineJob { 
                    Commands = ParsePipeline(t), 
                    Operator = nextOp 
                });
                nextOp = ""; 
            }
        }
        return results;
    }

    private List<CommandCall> ParsePipeline(string input) {
        var results = new List<CommandCall>();
        // Split by pipe
        var parts = Regex.Split(input, @"(?<![<>])\|(?![<>])"); // Simple pipe split
        
        foreach (var part in parts) {
            string cmd = part.Trim();
            if (string.IsNullOrEmpty(cmd)) continue;

            var call = new CommandCall { CommandLine = cmd };
            
            // Handle redirection
            if (cmd.Contains(">>")) {
                int idx = cmd.IndexOf(">>");
                call.CommandLine = cmd.Substring(0, idx).Trim();
                call.RedirectionPath = cmd.Substring(idx + 2).Trim();
                call.AppendRedirection = true;
            } else if (cmd.Contains(">")) {
                int idx = cmd.IndexOf(">");
                call.CommandLine = cmd.Substring(0, idx).Trim();
                call.RedirectionPath = cmd.Substring(idx + 1).Trim();
                call.AppendRedirection = false;
            }
            
            results.Add(call);
        }
        return results;
    }

    public bool IsProcessRunning => _activePipeline.Count > 0;

    public void TerminateActiveProcess() {
        lock (_activePipeline) {
            foreach (var p in _activePipeline.ToList()) {
                p.Terminate();
            }
        }
    }

    public void SendSignal(string signal) {
        lock (_activePipeline) {
            if (_activePipeline.Count > 0) {
                DebugLogger.Log($"TerminalBackend: Sending signal {signal} to pipeline");
                if (signal == "SIGINT" || signal == "CTRL+C") {
                    AddLine("^C", Color.Yellow, "SYSTEM");
                }
                foreach (var p in _activePipeline.ToList()) {
                    Shell.Process.SendSignal(p, signal);
                }
            }
        }
    }

    public void SendInput(string text) {
        if (_activeReader == null) return;
        
        // Handle special signals like Ctrl+C
        if (text == "\x03") { // ETX (Ctrl+C)
            SendSignal("CTRL+C");
            return;
        }

        _activeReader.EnqueueInput(text);
    }

    public void EchoInput(string text) {
        // Echo input to the buffer so it's visible to the user
        // We trim the newline from the text if present because WriteLine adds one
        string echo = text.EndsWith("\n") ? text.Substring(0, text.Length - 1) : text;
        
        // Use a special color (e.g., LightGray) to distinguish from app output if desired,
        // or just White for traditional terminal look.
        WriteLine(echo, Color.White);
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
