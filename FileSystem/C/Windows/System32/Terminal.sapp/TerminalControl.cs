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


namespace TerminalApp;

public class TerminalControl : TextArea {
    private TerminalBackend _backend;
    private string _currentInput = "";
    private string _stashInput = "";
    private List<string> _commandHistory = new();
    private int _historyIndex = -1; // -1 = live typing, 0 to Count-1 = browsing history
    
    private string _currentDir = "C:\\";
    private string _promptSuffix = "> ";

    public TerminalBackend Backend => _backend;
    public string CurrentDirectory => _currentDir;

    public TerminalControl(Vector2 position, Vector2 size) : base(position, size) {
        _backend = new TerminalBackend();
        _backend.OnBufferChanged += SyncLines;
        
        BackgroundColor = Color.Black * 0.8f;
        TextColor = Color.LightGray;
        FontSize = 14;
        DrawBackground = true;

        _backend.WriteLine("Antigravity OS [Version 1.0.462]", Color.Gray);
        _backend.WriteLine("(c) 2026 Google Deepmind. All rights reserved.", Color.Gray);
        _backend.WriteLine("");
        
        SyncLines();
        ResetSelection();
    }

    private void SyncLines() {
        bool hadSelection = HasSelection();
        var backendLines = _backend.Lines;
        _lines = backendLines.Select(l => l.Text).ToList();
        
        if (!_backend.IsProcessRunning) {
            _lines.Add(GetPrompt() + _currentInput);
        } else {
            if (_backend.IsLastLineComplete || _lines.Count == 0 || backendLines.Count == 0) {
                _lines.Add(_currentInput);
            } else {
                string lastBackend = backendLines.Last().Text;
                _lines[^1] = lastBackend + _currentInput;
            }
        }
        
        _cursorLine = _lines.Count - 1;
        _cursorCol = Math.Clamp(_cursorCol, 0, _lines[_cursorLine].Length);
        
        if (!hadSelection) ResetSelection();
        _maxWidthDirty = true;
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
        
        _cursorLine = _lines.Count - 1;
        _cursorCol = _lines[_cursorLine].Length;

        if (!_backend.IsProcessRunning) {
            _backend.WriteLine("\u001b[32m" + GetPrompt() + "\u001b[0m" + rawInput);

            string cmd = rawInput.Trim();
            if (!string.IsNullOrEmpty(cmd)) {
                _commandHistory.Remove(cmd);
                _commandHistory.Add(cmd);
            }
            
            _currentInput = "";
            _stashInput = "";
            _historyIndex = -1;
            
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
            _currentInput = "";
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

        // Ensure prompt is present if not running a process
        if (_lines.Count > 0) {
            if (!_backend.IsProcessRunning) {
                string prompt = GetPrompt();
                if (!_lines[^1].StartsWith(prompt)) {
                    _lines[^1] = prompt + _currentInput;
                }
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

        if (GameContent.FontSystem == null) return;
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
            for (int i = sl; i <= el; i++) {
                if (i < 0 || i >= _lines.Count) continue;
                float y = textY + i * lineHeight;
                if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;

                int startC = (i == sl) ? sc : 0;
                int endC = (i == el) ? ec : _lines[i].Length;

                float x1 = (startC <= 0) ? 0 : font.MeasureString(_lines[i].Substring(0, Math.Min(_lines[i].Length, startC))).X;
                float x2 = (endC <= 0) ? 0 : font.MeasureString(_lines[i].Substring(0, Math.Min(_lines[i].Length, endC))).X;

                batch.FillRectangle(new Vector2(textX + x1, y), new Vector2(x2 - x1, lineHeight), FocusedBorderColor * 0.3f * AbsoluteOpacity);
            }
        }

        var backendLines = _backend.Lines;
        int firstLine = (int)Math.Floor(_scrollOffset / lineHeight);
        int lastLine = (int)Math.Ceiling((_scrollOffset + Size.Y) / lineHeight);
        firstLine = Math.Clamp(firstLine, 0, _lines.Count - 1);
        lastLine = Math.Clamp(lastLine, 0, _lines.Count - 1);
        
        for (int i = firstLine; i <= lastLine; i++) {
            float y = textY + i * lineHeight;
            if (i < backendLines.Count) {
                var bl = backendLines[i];
                float segmentX = textX;
                if (bl.Segments != null) {
                    foreach (var seg in bl.Segments) {
                        font.DrawText(batch, seg.Text, new Vector2(segmentX, y), seg.Color * AbsoluteOpacity);
                        segmentX += font.MeasureString(seg.Text).X;
                    }
                }

                // If this is the last line and it's incomplete, draw the current input at the end
                if (i == backendLines.Count - 1 && !_backend.IsLastLineComplete) {
                    font.DrawText(batch, _currentInput, new Vector2(segmentX, y), Color.White * AbsoluteOpacity);
                }
            } else if (i == backendLines.Count) {
                // Input line
                if (!_backend.IsProcessRunning) {
                    string prompt = GetPrompt();
                    font.DrawText(batch, prompt, new Vector2(textX, y), Color.Lime * AbsoluteOpacity);
                    float promptW = font.MeasureString(prompt).X;
                    font.DrawText(batch, _currentInput, new Vector2(textX + promptW, y), Color.White * AbsoluteOpacity);
                } else {
                    font.DrawText(batch, _currentInput, new Vector2(textX, y), Color.White * AbsoluteOpacity);
                }
            }
        }

        // Cursor
        if (_showCursor && IsFocused) {
            float cX = textX;
            if (_cursorLine >= 0 && _cursorLine < _lines.Count) {
                cX += font.MeasureString(_lines[_cursorLine].Substring(0, Math.Min(_lines[_cursorLine].Length, _cursorCol))).X;
            }
            float cY = textY + _cursorLine * lineHeight;
            batch.FillRectangle(new Vector2(cX, cY + 2), new Vector2(8, lineHeight - 4), Color.White * 0.7f * AbsoluteOpacity);
        }

        spriteBatch.End();
        batch.End();
        
        G.GraphicsDevice.ScissorRectangle = oldScissor;
        spriteBatch.Begin();
        batch.Begin();
    }
}
