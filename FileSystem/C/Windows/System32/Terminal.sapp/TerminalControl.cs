using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame;
using FontStashSharp;


namespace TerminalApp;

public class TerminalControl : TextArea {
    private TerminalBackend _backend;
    private bool _needsSync = false;
    private readonly object _syncLock = new();
    private string _currentInput = "";
    private string _stashInput = "";
    private List<string> _commandHistory = new();
    private int _historyIndex = -1; // -1 = live typing, 0 to Count-1 = browsing history
    
    private string _currentDir = "C:\\";
    private string _promptSuffix = "> ";

    public class TerminalSettings {
        public List<string> History { get; set; } = new();
    }

    public TerminalBackend Backend => _backend;
    public string CurrentDirectory => _currentDir;

    public TerminalControl(Vector2 position, Vector2 size) : base(position, size) {
        _backend = new TerminalBackend();
        _backend.OnBufferChanged += () => { lock (_syncLock) { _needsSync = true; } };
        
        BackgroundColor = Color.Black * 0.8f;
        TextColor = Color.LightGray;
        FontSize = 14;
        DrawBackground = true;

        _backend.WriteLine("HentOS [Version 1.0.462]", Color.Gray);
        _backend.WriteLine("(C) 2026 Developed by GetTheNya. All rights reserved.", Color.Gray);
        _backend.WriteLine("");
        
        WordWrap = true; // Enable word wrap by default for HentOS Terminal

        SyncLines();
        ResetSelection();
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        bool sync = false;
        lock (_syncLock) {
            if (_needsSync) {
                sync = true;
                _needsSync = false;
            }
        }
        
        if (sync) {
            SyncLines();
        }
    }

    public void LoadSettings(TheGame.Core.OS.Process process) {
        if (process == null) return;
        var settings = Shell.AppSettings.Load<TerminalSettings>(process);
        _commandHistory = settings.History ?? new();
        _historyIndex = _commandHistory.Count;
    }

    private void SyncLines() {
        bool hadSelection = HasSelection();
        var backendLines = _backend.Lines;
        
        // Build base lines from backend
        _lines = backendLines.Select(l => l.Text).ToList();
        if (_lines.Count == 0) _lines.Add("");
        
        // Add the active input line
        if (!_backend.IsProcessRunning) {
            // Always put the shell prompt on a new line if there's any backend content
            if (backendLines.Count > 0) {
                _lines.Add(GetPrompt() + _currentInput);
            } else {
                _lines[0] = GetPrompt() + _currentInput;
            }
        } else if (_backend.IsLastLineComplete) {
            _lines.Add(_currentInput);
        } else {
            // Append input to the last incomplete backend line
            _lines[^1] += _currentInput;
        }
        
        _cursorLine = _lines.Count - 1;
        _cursorCol = _lines[_cursorLine].Length;
        
        if (!hadSelection) ResetSelection();
        _maxWidthDirty = true;
        UpdateVisualLines();
        EnsureCursorVisible();
    }

    private string GetPrompt() => _currentDir + _promptSuffix;

    protected override bool CanEditAt(int line, int col) {
        if (line != _lines.Count - 1) return false;
        if (!_backend.IsProcessRunning) {
            return col >= GetPrompt().Length;
        } else {
            if (_backend.IsLastLineComplete) return true;
            var bl = _backend.Lines;
            if (bl.Count > 0) return col >= bl.Last().Text.Length;
            return true;
        }
    }

    protected override void MoveCursor(int dx, int dy, bool select) {
        base.MoveCursor(dx, dy, select);
        if (!select && !CanEditAt(_cursorLine, _cursorCol)) {
            _cursorLine = _lines.Count - 1;
            _cursorCol = Math.Clamp(_cursorCol, 0, _lines[_cursorLine].Length);

            int minCol = 0;
            if (!_backend.IsProcessRunning) {
                minCol = GetPrompt().Length;
            } else if (!_backend.IsLastLineComplete) {
                var bl = _backend.Lines;
                if (bl.Count > 0) minCol = bl.Last().Text.Length;
            }

            if (_cursorCol < minCol) _cursorCol = minCol;
            
            EnsureCursorVisible();
            ResetSelection();
        }
    }

    private string ExtractInputFromLine() {
        if (_lines.Count == 0) return "";
        string lastLine = _lines[^1];
        
        if (!_backend.IsProcessRunning) {
            string prompt = GetPrompt();
            if (lastLine.StartsWith(prompt)) {
                return lastLine.Substring(prompt.Length);
            }
        } else {
            if (!_backend.IsLastLineComplete) {
                var bl = _backend.Lines;
                if (bl.Count > 0) {
                    string prompt = bl.Last().Text;
                    if (lastLine.StartsWith(prompt)) {
                        return lastLine.Substring(prompt.Length);
                    }
                }
            } else {
                return lastLine;
            }
        }
        return _currentInput; 
    }

    protected override void OnEnterPressed() {
        string rawInput = ExtractInputFromLine();
        string cmd = rawInput.Trim();

        _cursorLine = _lines.Count - 1;
        _cursorCol = _lines[_cursorLine].Length;

        // Clear input state first to avoid race conditions with termination callbacks
        _currentInput = "";
        _stashInput = "";
        _historyIndex = -1;

        if (!_backend.IsProcessRunning) {
            _backend.WriteLine("\u001b[32m" + GetPrompt() + "\u001b[0m" + rawInput);

            if (!string.IsNullOrEmpty(cmd)) {
                _commandHistory.Remove(cmd);
                _commandHistory.Add(cmd);

                // Save history
                var process = GetOwnerProcess();
                if (process != null) {
                    Shell.AppSettings.Save(process, new TerminalSettings { History = _commandHistory });
                }
            }
            
            if (!string.IsNullOrEmpty(cmd)) {
                if (!HandleInternalCommand(cmd)) {
                    _backend.ExecuteCommand(cmd);
                }
            }
        } else {
            // Echo input to backend and seal the line
            _backend.WriteLine(rawInput);
            
            // Forward input to process
            _backend.SendInput(rawInput + "\n");
        }

        SyncLines();
        if (!_backend.IsProcessRunning) {
            _cursorCol = GetPrompt().Length;
        } else {
            if (_backend.IsLastLineComplete) _cursorCol = 0;
            else {
                var bl = _backend.Lines;
                _cursorCol = bl.Count > 0 ? bl.Last().Text.Length : 0;
            }
        }
        ResetSelection();
        EnsureCursorVisible();
    }

    private bool HandleInternalCommand(string cmd) {
        string[] parts = cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;
        string name = parts[0].ToLower();

        if (name == "exit" || name == "quit") {
            GetOwnerWindow()?.Close();
            return true;
        }

        if (name == "cls" || name == "clear") {
            _backend.Clear();
            return true;
        }
        
        if (name == "history") {
            _backend.WriteLine("--- Terminal History ---", Color.Gray);
            for (int i = 0; i < _commandHistory.Count; i++) {
                _backend.WriteLine($"{i + 1}: {_commandHistory[i]}");
            }
            return true;
        }

        if (name == "cd") {
            if (parts.Length > 1) {
                string target = parts[1];
                string resolved = VirtualFileSystem.Instance.ResolvePath(_currentDir, target);
                if (VirtualFileSystem.Instance.IsDirectory(resolved)) {
                    _currentDir = VirtualFileSystem.Instance.GetActualCasing(resolved);
                    _backend.WorkingDirectory = _currentDir;
                } else {
                    _backend.WriteLine($"The system cannot find the path specified: {target}", Color.Red);
                }
            }
            return true;
        }
        
        return false;
    }

    private void NavigateHistory(int delta) {
        if (_commandHistory.Count == 0) return;

        if (_historyIndex == -1) {
            if (delta > 0) return; // Can't go down from stash
            _stashInput = _currentInput;
            _historyIndex = _commandHistory.Count - 1;
        } else {
            int newIndex = _historyIndex + delta;
            if (newIndex >= 0 && newIndex < _commandHistory.Count) {
                _historyIndex = newIndex;
            } else if (newIndex >= _commandHistory.Count) {
                _historyIndex = -1;
                _currentInput = _stashInput;
                SyncLines();
                _cursorCol = _lines[^1].Length;
                ResetSelection();
                EnsureCursorVisible();
                return;
            } else {
                return; // At oldest command
            }
        }

        _currentInput = _commandHistory[_historyIndex];
        SyncLines();
        _cursorCol = _lines[^1].Length;
        ResetSelection();
        EnsureCursorVisible();
    }



    protected override void UpdateInput() {
        if (!IsFocused) return;

        // Reset history if typing
        var typed = InputManager.GetTypedChars();
        if (typed.Any() || InputManager.IsKeyJustPressed(Keys.Back) || InputManager.IsKeyJustPressed(Keys.Delete)) {
            _historyIndex = -1;
            if (!CanEditAt(_cursorLine, _cursorCol)) {
                _cursorLine = _lines.Count - 1;
                _cursorCol = _lines[_cursorLine].Length;
                ResetSelection();
            }
        }

        // Handle Enter
        if (InputManager.IsKeyJustPressed(Keys.Enter)) {
            if (!_backend.IsProcessRunning) {
                OnEnterPressed();
            } else {
                // Echo the current input to the terminal buffer so it remains visible
                _backend.EchoInput(_currentInput);
                
                _backend.SendInput(_currentInput + "\n");
                _currentInput = "";
                SyncLines();
            }
            return;
        }

        // Handle history navigation
        if (!_backend.IsProcessRunning) {
            if (InputManager.IsKeyRepeated(Keys.Up)) {
                NavigateHistory(-1);
                InputManager.IsKeyboardConsumed = true;
                return;
            }
            if (InputManager.IsKeyRepeated(Keys.Down)) {
                NavigateHistory(1);
                InputManager.IsKeyboardConsumed = true;
                return;
            }
        }

        if (InputManager.IsKeyJustPressed(Keys.Home)) {
            _cursorLine = _lines.Count - 1;
            _cursorCol = GetPrompt().Length;
            ResetSelection();
            return;
        }

        // Let TextArea handle standard cursor moves and typing
        base.UpdateInput();

        _currentInput = ExtractInputFromLine();

        // Keep _lines[^1] in sync with _currentInput for cursor positioning and base TextArea logic
        if (_lines.Count > 0) {
            if (!_backend.IsProcessRunning) {
                string prompt = GetPrompt();
                _lines[^1] = prompt + _currentInput;
            } else {
                if (_backend.IsLastLineComplete) {
                    _lines[^1] = _currentInput;
                } else {
                    var bl = _backend.Lines;
                    string prefix = bl.Count > 0 ? bl.Last().Text : "";
                    _lines[^1] = prefix + _currentInput;
                }
            }
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!DrawBackground) return;
        
        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity, rounded: 3f);
        batch.BorderRectangle(absPos, Size, (IsFocused ? FocusedBorderColor : BorderColor) * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        if (GameContent.FontSystem == null || _visualLines.Count == 0) return;
        var font = GameContent.FontSystem.GetFont(FontSize); 
        float lineHeight = font.LineHeight;

        var oldScissor = G.GraphicsDevice.ScissorRectangle;
        var scissor = new Rectangle((int)absPos.X + 5, (int)absPos.Y + 5, (int)Size.X - 10, (int)Size.Y - 10);
        scissor = Rectangle.Intersect(oldScissor, scissor);
        if (scissor.Width <= 0 || scissor.Height <= 0) return;

        spriteBatch.End();
        batch.End();
        
        G.GraphicsDevice.ScissorRectangle = scissor;
        var rs = new RasterizerState { ScissorTestEnable = true };
        
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rs);
        batch.Begin();

        float textX = absPos.X + 10 - _scrollOffsetX;
        float textY = absPos.Y + 10 - _scrollOffset;

        // Selection
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            for (int i = 0; i < _visualLines.Count; i++) {
                var vl = _visualLines[i];
                float y = textY + i * lineHeight;
                if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;

                int visualLogicalLine = vl.LogicalLineIndex;
                if (visualLogicalLine < sl || visualLogicalLine > el) continue;

                int startC = 0;
                int endC = vl.Length;

                if (visualLogicalLine == sl) {
                    startC = Math.Max(0, sc - vl.StartIndex);
                }
                if (visualLogicalLine == el) {
                    endC = Math.Min(vl.Length, ec - vl.StartIndex);
                }

                if (startC >= endC && visualLogicalLine == sl && visualLogicalLine == el) continue;
                if (startC >= vl.Length) continue;
                if (endC <= 0) continue;

                string visualText = _lines[vl.LogicalLineIndex].Substring(vl.StartIndex, vl.Length);
                float x1 = startC <= 0 ? 0 : font.MeasureString(visualText.Substring(0, startC)).X;
                float x2 = endC >= vl.Length ? font.MeasureString(visualText).X : font.MeasureString(visualText.Substring(0, endC)).X;

                batch.FillRectangle(new Vector2(textX + x1, y), new Vector2(x2 - x1, lineHeight), FocusedBorderColor * 0.3f * AbsoluteOpacity);
            }
        }

        var backendLines = _backend.Lines;
        int firstVis = (int)Math.Floor(_scrollOffset / lineHeight);
        int lastVis = (int)Math.Ceiling((_scrollOffset + Size.Y) / lineHeight);
        int maxVis = Math.Max(0, _visualLines.Count - 1);
        firstVis = Math.Clamp(firstVis, 0, maxVis);
        lastVis = Math.Clamp(lastVis, 0, maxVis);
        
        for (int i = firstVis; i <= lastVis; i++) {
            if (i < 0 || i >= _visualLines.Count) continue;
            var vl = _visualLines[i];
            
            // Safety check for logical line index
            if (vl.LogicalLineIndex < 0 || vl.LogicalLineIndex >= _lines.Count) continue;

            float y = textY + i * lineHeight;

            // Is this line from the backend?
            if (vl.LogicalLineIndex < backendLines.Count) {
                var bl = backendLines[vl.LogicalLineIndex];
                DrawWrappedSegments(batch, font, bl.Segments, vl.StartIndex, vl.Length, textX, y);

                // If this is the last backend line and it's incomplete, we might need to draw input at the end
                if (vl.LogicalLineIndex == backendLines.Count - 1 && !_backend.IsLastLineComplete && _backend.IsProcessRunning) {
                     // Determine how much of the logical line is prefix (from backend)
                     // If the wrap starts AFTER prefix, we draw only input.
                     // If the wrap covers the transition, we offset.
                     int prefixLen = bl.Text.Length;
                     if (vl.StartIndex + vl.Length > prefixLen) {
                         int inputStartInVisual = Math.Max(0, prefixLen - vl.StartIndex);
                         int inputLenInVisual = vl.Length - inputStartInVisual;
                         if (inputLenInVisual > 0) {
                             string inputPart = _currentInput.Substring(0, Math.Min(_currentInput.Length, inputLenInVisual));
                             float prefixWidth = font.MeasureString(_lines[vl.LogicalLineIndex].Substring(vl.StartIndex, inputStartInVisual)).X;
                             font.DrawText(batch, inputPart, new Vector2(textX + prefixWidth, y), TextColor * AbsoluteOpacity);
                         }
                     }
                }
            } else if (vl.LogicalLineIndex == backendLines.Count) {
                // This is the active input line (Shell prompt or follow-up line after complete backend output)
                if (!_backend.IsProcessRunning) {
                    string prompt = GetPrompt();
                    string fullLine = _lines[vl.LogicalLineIndex];
                    string visualPart = fullLine.Substring(vl.StartIndex, vl.Length);

                    if (vl.StartIndex < prompt.Length) {
                        // Includes prompt part
                        int promptPartLen = Math.Min(vl.Length, prompt.Length - vl.StartIndex);
                        string promptPart = prompt.Substring(vl.StartIndex, promptPartLen);
                        font.DrawText(batch, promptPart, new Vector2(textX, y), Color.Lime * AbsoluteOpacity);
                        
                        if (vl.Length > promptPartLen) {
                             string inputPart = visualPart.Substring(promptPartLen);
                             float pWidth = font.MeasureString(promptPart).X;
                             font.DrawText(batch, inputPart, new Vector2(textX + pWidth, y), TextColor * AbsoluteOpacity);
                        }
                    } else {
                        // Entirely user input
                        font.DrawText(batch, visualPart, new Vector2(textX, y), TextColor * AbsoluteOpacity);
                    }
                } else if (_backend.IsLastLineComplete) {
                     if (vl.LogicalLineIndex >= 0 && vl.LogicalLineIndex < _lines.Count) {
                         string visualPart = _lines[vl.LogicalLineIndex].Substring(vl.StartIndex, vl.Length);
                         font.DrawText(batch, visualPart, new Vector2(textX, y), TextColor * AbsoluteOpacity);
                     }
                }
            }
        }

        // Cursor
        if (_showCursor && IsFocused) {
            int visIdx = GetVisualLineIndex(_cursorLine, _cursorCol);
            if (visIdx >= 0 && visIdx < _visualLines.Count) {
                var vl = _visualLines[visIdx];
                if (vl.LogicalLineIndex >= 0 && vl.LogicalLineIndex < _lines.Count) {
                    string lineText = _lines[vl.LogicalLineIndex];
                    int start = Math.Clamp(vl.StartIndex, 0, lineText.Length);
                    int col = Math.Clamp(_cursorCol, start, start + vl.Length);
                    string visualPart = lineText.Substring(start, col - start);
                    
                    float cursorX = font.MeasureString(visualPart).X;
                    float cursorY = textY + visIdx * lineHeight;
                    batch.FillRectangle(new Vector2(textX + cursorX, cursorY + 2), new Vector2(8, lineHeight - 4), Color.White * 0.7f * AbsoluteOpacity);
                }
            }
        }

        spriteBatch.End();
        batch.End();
        
        G.GraphicsDevice.ScissorRectangle = oldScissor;
        spriteBatch.Begin();
        batch.Begin();
    }

    private void DrawWrappedSegments(ShapeBatch batch, DynamicSpriteFont font, List<TerminalSegment> segments, int start, int length, float x, float y) {
        if (segments == null) return;
        
        int currentOffset = 0;
        float currentX = x;

        foreach (var seg in segments) {
            int segEnd = currentOffset + seg.Text.Length;
            
            // Check if this segment overlaps with the visual line range [start, start + length]
            int overlapStart = Math.Max(start, currentOffset);
            int overlapEnd = Math.Min(start + length, segEnd);

            if (overlapStart < overlapEnd) {
                int localStart = overlapStart - currentOffset;
                int localLen = overlapEnd - overlapStart;
                string part = seg.Text.Substring(localStart, localLen);
                
                // We need to know where to start drawing this part relative to the visual line start
                // (Already handled by currentX incrementing)
                
                font.DrawText(batch, part, new Vector2(currentX, y), seg.Color * AbsoluteOpacity);
                currentX += font.MeasureString(part).X;
            }
            
            currentOffset = segEnd;
            if (currentOffset >= start + length) break;
        }
    }
}
