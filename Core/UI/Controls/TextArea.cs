using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A multiline text editing control similar to a Windows TextBox with multiline support.
/// </summary>
public class TextArea : UIControl {
    private List<string> _lines = new() { "" };
    private int _cursorLine = 0;
    private int _cursorCol = 0;
    private int _selStartLine = 0;
    private int _selStartCol = 0;
    private float _scrollOffset = 0f;
    private float _cursorTimer = 0f;
    private bool _showCursor = true;

    public string Text {
        get => string.Join("\n", _lines);
        set {
            _lines = new List<string>((value ?? "").Split('\n'));
            if (_lines.Count == 0) _lines.Add("");
            _cursorLine = 0;
            _cursorCol = 0;
            _selStartLine = 0;
            _selStartCol = 0;
            OnTextChanged?.Invoke(Text);
        }
    }

    public string Placeholder { get; set; } = "";
    public Color TextColor { get; set; } = Color.White;
    public Color FocusedBorderColor { get; set; } = new Color(0, 120, 215);
    public int FontSize { get; set; } = 16;
    public bool DrawBackground { get; set; } = true;

    public Action<string> OnTextChanged { get; set; }

    public TextArea(Vector2 position, Vector2 size) : base(position, size) {
        ConsumesInput = true;
        BackgroundColor = new Color(30, 30, 30);
        BorderColor = new Color(60, 60, 60);
    }

    protected override void UpdateInput() {
        base.UpdateInput();

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            IsFocused = IsMouseOver;
            if (IsFocused) {
                SetCursorFromMouse();
                _selStartLine = _cursorLine;
                _selStartCol = _cursorCol;
            }
        }

        if (IsFocused) {
            // Handle scroll
            int scroll = InputManager.ScrollDelta;
            if (scroll != 0 && IsMouseOver) {
                _scrollOffset -= scroll * 3;
                ClampScroll();
            }

            bool shift = InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift);
            bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || InputManager.IsKeyDown(Keys.RightControl);

            // Navigation
            if (InputManager.IsKeyRepeated(Keys.Left)) MoveCursor(-1, 0, shift);
            if (InputManager.IsKeyRepeated(Keys.Right)) MoveCursor(1, 0, shift);
            if (InputManager.IsKeyRepeated(Keys.Up)) MoveCursor(0, -1, shift);
            if (InputManager.IsKeyRepeated(Keys.Down)) MoveCursor(0, 1, shift);

            if (InputManager.IsKeyJustPressed(Keys.Home)) {
                _cursorCol = 0;
                if (!shift) { _selStartLine = _cursorLine; _selStartCol = _cursorCol; }
            }
            if (InputManager.IsKeyJustPressed(Keys.End)) {
                _cursorCol = _lines[_cursorLine].Length;
                if (!shift) { _selStartLine = _cursorLine; _selStartCol = _cursorCol; }
            }

            // Select all
            if (ctrl && InputManager.IsKeyJustPressed(Keys.A)) {
                _selStartLine = 0; _selStartCol = 0;
                _cursorLine = _lines.Count - 1;
                _cursorCol = _lines[_cursorLine].Length;
            }

            // Backspace
            if (InputManager.IsKeyRepeated(Keys.Back)) {
                if (HasSelection()) DeleteSelection();
                else if (_cursorCol > 0) {
                    _lines[_cursorLine] = _lines[_cursorLine].Remove(_cursorCol - 1, 1);
                    _cursorCol--;
                } else if (_cursorLine > 0) {
                    _cursorCol = _lines[_cursorLine - 1].Length;
                    _lines[_cursorLine - 1] += _lines[_cursorLine];
                    _lines.RemoveAt(_cursorLine);
                    _cursorLine--;
                }
                ResetSelection();
                OnTextChanged?.Invoke(Text);
            }

            // Delete
            if (InputManager.IsKeyRepeated(Keys.Delete)) {
                if (HasSelection()) DeleteSelection();
                else if (_cursorCol < _lines[_cursorLine].Length) {
                    _lines[_cursorLine] = _lines[_cursorLine].Remove(_cursorCol, 1);
                } else if (_cursorLine < _lines.Count - 1) {
                    _lines[_cursorLine] += _lines[_cursorLine + 1];
                    _lines.RemoveAt(_cursorLine + 1);
                }
                OnTextChanged?.Invoke(Text);
            }

            // Enter
            if (InputManager.IsKeyJustPressed(Keys.Enter)) {
                if (HasSelection()) DeleteSelection();
                string remainder = _lines[_cursorLine].Substring(_cursorCol);
                _lines[_cursorLine] = _lines[_cursorLine].Substring(0, _cursorCol);
                _lines.Insert(_cursorLine + 1, remainder);
                _cursorLine++;
                _cursorCol = 0;
                ResetSelection();
                OnTextChanged?.Invoke(Text);
            }

            // Tab
            if (InputManager.IsKeyJustPressed(Keys.Tab)) {
                if (HasSelection()) DeleteSelection();
                _lines[_cursorLine] = _lines[_cursorLine].Insert(_cursorCol, "    ");
                _cursorCol += 4;
                ResetSelection();
                OnTextChanged?.Invoke(Text);
            }

            // Character input
            foreach (var c in InputManager.GetTypedChars()) {
                if (char.IsControl(c)) continue;
                if (HasSelection()) DeleteSelection();
                _lines[_cursorLine] = _lines[_cursorLine].Insert(_cursorCol, c.ToString());
                _cursorCol++;
                ResetSelection();
                OnTextChanged?.Invoke(Text);
            }

            InputManager.IsKeyboardConsumed = true;
        }
    }

    private void SetCursorFromMouse() {
        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        Vector2 local = InputManager.MousePosition.ToVector2() - AbsolutePosition - new Vector2(5, 5);
        local.Y += _scrollOffset;

        int line = (int)(local.Y / lineHeight);
        line = Math.Clamp(line, 0, _lines.Count - 1);

        int col = 0;
        float bestDist = float.MaxValue;
        string lineText = _lines[line];
        for (int i = 0; i <= lineText.Length; i++) {
            float w = i == 0 ? 0 : font.MeasureString(lineText.Substring(0, i)).X;
            float dist = Math.Abs(local.X - w);
            if (dist < bestDist) { bestDist = dist; col = i; }
        }

        _cursorLine = line;
        _cursorCol = col;
    }

    private void MoveCursor(int dx, int dy, bool select) {
        if (dy != 0) {
            _cursorLine = Math.Clamp(_cursorLine + dy, 0, _lines.Count - 1);
            _cursorCol = Math.Min(_cursorCol, _lines[_cursorLine].Length);
        }
        if (dx != 0) {
            _cursorCol += dx;
            if (_cursorCol < 0 && _cursorLine > 0) {
                _cursorLine--;
                _cursorCol = _lines[_cursorLine].Length;
            } else if (_cursorCol > _lines[_cursorLine].Length && _cursorLine < _lines.Count - 1) {
                _cursorLine++;
                _cursorCol = 0;
            }
            _cursorCol = Math.Clamp(_cursorCol, 0, _lines[_cursorLine].Length);
        }
        if (!select) ResetSelection();
        EnsureCursorVisible();
    }

    private bool HasSelection() => _cursorLine != _selStartLine || _cursorCol != _selStartCol;

    private void ResetSelection() {
        _selStartLine = _cursorLine;
        _selStartCol = _cursorCol;
    }

    private void DeleteSelection() {
        GetSelectionRange(out int startLine, out int startCol, out int endLine, out int endCol);

        if (startLine == endLine) {
            _lines[startLine] = _lines[startLine].Remove(startCol, endCol - startCol);
        } else {
            string before = _lines[startLine].Substring(0, startCol);
            string after = _lines[endLine].Substring(endCol);
            _lines[startLine] = before + after;
            _lines.RemoveRange(startLine + 1, endLine - startLine);
        }

        _cursorLine = startLine;
        _cursorCol = startCol;
        ResetSelection();
        OnTextChanged?.Invoke(Text);
    }

    private void GetSelectionRange(out int startLine, out int startCol, out int endLine, out int endCol) {
        if (_cursorLine < _selStartLine || (_cursorLine == _selStartLine && _cursorCol < _selStartCol)) {
            startLine = _cursorLine; startCol = _cursorCol;
            endLine = _selStartLine; endCol = _selStartCol;
        } else {
            startLine = _selStartLine; startCol = _selStartCol;
            endLine = _cursorLine; endCol = _cursorCol;
        }
    }

    private void EnsureCursorVisible() {
        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;
        float cursorY = _cursorLine * lineHeight;

        if (cursorY < _scrollOffset) _scrollOffset = cursorY;
        if (cursorY + lineHeight > _scrollOffset + Size.Y - 10) _scrollOffset = cursorY + lineHeight - Size.Y + 10;
        ClampScroll();
    }

    private void ClampScroll() {
        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float totalHeight = _lines.Count * font.LineHeight;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, totalHeight - Size.Y + 10));
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (IsFocused) {
            _cursorTimer += dt;
            if (_cursorTimer >= 0.5f) { _showCursor = !_showCursor; _cursorTimer = 0f; }
        } else {
            _showCursor = false;
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        // Background (optional)
        if (DrawBackground) {
            batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity, rounded: 3f);
            batch.BorderRectangle(absPos, Size, (IsFocused ? FocusedBorderColor : BorderColor) * AbsoluteOpacity, thickness: 1f, rounded: 3f);
        }

        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;

        // Clip region simulation via scissor (if possible) or just draw
        float textX = absPos.X + 5;
        float textY = absPos.Y + 5 - _scrollOffset;

        // Draw selection highlight
        if (HasSelection()) {
            GetSelectionRange(out int sl, out int sc, out int el, out int ec);
            for (int i = sl; i <= el; i++) {
                float y = textY + i * lineHeight;
                if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;

                int startC = (i == sl) ? sc : 0;
                int endC = (i == el) ? ec : _lines[i].Length;

                float x1 = startC == 0 ? 0 : font.MeasureString(_lines[i].Substring(0, startC)).X;
                float x2 = endC == 0 ? 0 : font.MeasureString(_lines[i].Substring(0, endC)).X;

                batch.FillRectangle(new Vector2(textX + x1, y), new Vector2(x2 - x1, lineHeight), FocusedBorderColor * 0.3f * AbsoluteOpacity);
            }
        }

        // Draw lines
        for (int i = 0; i < _lines.Count; i++) {
            float y = textY + i * lineHeight;
            if (y + lineHeight < absPos.Y || y > absPos.Y + Size.Y) continue;

            if (!string.IsNullOrEmpty(_lines[i])) {
                font.DrawText(batch, _lines[i], new Vector2(textX, y), TextColor * AbsoluteOpacity);
            }
        }

        // Placeholder
        if (_lines.Count == 1 && string.IsNullOrEmpty(_lines[0]) && !string.IsNullOrEmpty(Placeholder)) {
            font.DrawText(batch, Placeholder, new Vector2(textX, textY), Color.Gray * 0.5f * AbsoluteOpacity);
        }

        // Cursor
        if (_showCursor && IsFocused) {
            float cursorX = _cursorCol == 0 ? 0 : font.MeasureString(_lines[_cursorLine].Substring(0, _cursorCol)).X;
            float cursorY = textY + _cursorLine * lineHeight;
            batch.FillRectangle(new Vector2(textX + cursorX, cursorY), new Vector2(2, lineHeight), FocusedBorderColor * AbsoluteOpacity);
        }
    }
}
