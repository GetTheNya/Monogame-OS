using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A multiline text editing control similar to a Windows TextBox with multiline support.
/// </summary>
public class TextArea : ValueControl<string> {
    private List<string> _lines = new() { "" };
    private int _cursorLine = 0;
    private int _cursorCol = 0;
    private int _selStartLine = 0;
    private int _selStartCol = 0;
    private float _scrollOffset = 0f;
    private float _targetScrollOffset = 0f;
    private float _scrollOffsetX = 0f;
    private float _targetScrollOffsetX = 0f;
    private float _cursorTimer = 0f;
    private bool _showCursor = true;
    private bool _isWordSelecting = false;
    private float _cachedMaxWidth = 0f;
    private bool _maxWidthDirty = true;
    private static readonly RasterizerState _scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

    public new string Value {
        get => string.Join("\n", _lines);
        set {
            _lines = new List<string>((value ?? "").Split('\n'));
            if (_lines.Count == 0) _lines.Add("");
            _cursorLine = 0;
            _cursorCol = 0;
            _selStartLine = 0;
            _selStartCol = 0;
            _maxWidthDirty = true;
            base.Value = Value; // Trigger OnValueChanged
        }
    }
    
    // Backwards compatibility alias
    public string Text {
        get => Value;
        set => Value = value;
    }

    public string Placeholder { get; set; } = "";
    public Color TextColor { get; set; } = Color.White;
    public Color FocusedBorderColor { get; set; } = new Color(0, 120, 215);
    public int FontSize { get; set; } = 16;
    public bool DrawBackground { get; set; } = true;

    // OnTextChanged is now inherited as OnValueChanged from ValueControl<string>

    public TextArea(Vector2 position, Vector2 size) : base(position, size, "") {
        BackgroundColor = new Color(30, 30, 30);
        BorderColor = new Color(60, 60, 60);
    }

    protected override void OnHover() {
        CustomCursor.Instance.SetCursor(CursorType.Beam);
        base.OnHover();
    }

    protected override void UpdateInput() {
        base.UpdateInput();

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            if (IsMouseOver) {
                SetCursorFromMouse();
                _selStartLine = _cursorLine;
                _selStartCol = _cursorCol;
                _isWordSelecting = false;
                InputManager.IsMouseConsumed = true; // Consume mouse input
            }
        }

        if (IsFocused) {
            // Drag selection: Use ignoreConsumed: true so we don't block ourselves if we already consumed the mouse this frame
            if (_isPressed && InputManager.IsMouseButtonDown(MouseButton.Left)) {
                InputManager.IsMouseConsumed = true;
                if (!_isWordSelecting) {
                    SetCursorFromMouse();
                }
            } else {
                _isWordSelecting = false;
            }

            if (InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true) && IsMouseOver) {
                SelectWordAtCursor();
                _isWordSelecting = true;
                InputManager.IsMouseConsumed = true;
            }
        }

        if (IsFocused) {
            // Handle scroll
            int scroll = InputManager.ScrollDelta;
            if (scroll != 0 && IsMouseOver) {
                var font = GameContent.FontSystem?.GetFont(FontSize);
                float scrollAmount = (font?.LineHeight ?? 20) * 3;
                _targetScrollOffset -= (scroll / 120f) * scrollAmount;
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

            // Select all / Copy / Cut / Paste
            if (ctrl && InputManager.IsKeyJustPressed(Keys.A)) SelectAll();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.C)) Copy();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.X)) Cut();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.V)) Paste();

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
                _maxWidthDirty = true;
                OnValueChanged?.Invoke(Value);
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
                _maxWidthDirty = true;
                OnValueChanged?.Invoke(Value);
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
                _maxWidthDirty = true;
                OnValueChanged?.Invoke(Value);
            }

            // Tab
            if (InputManager.IsKeyJustPressed(Keys.Tab)) {
                if (HasSelection()) DeleteSelection();
                _lines[_cursorLine] = _lines[_cursorLine].Insert(_cursorCol, "    ");
                _cursorCol += 4;
                ResetSelection();
                _maxWidthDirty = true;
                OnValueChanged?.Invoke(Value);
            }

            // Character input
            foreach (var c in InputManager.GetTypedChars()) {
                if (char.IsControl(c)) continue;
                if (HasSelection()) DeleteSelection();
                _lines[_cursorLine] = _lines[_cursorLine].Insert(_cursorCol, c.ToString());
                _cursorCol++;
                ResetSelection();
                _maxWidthDirty = true;
                OnValueChanged?.Invoke(Value);
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
        local.X += _scrollOffsetX;

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

    public override Vector2? GetCaretPosition() {
        if (GameContent.FontSystem == null) return null;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float lineHeight = font.LineHeight;
        float cursorX = _cursorCol == 0 ? 0 : font.MeasureString(_lines[_cursorLine].Substring(0, _cursorCol)).X;
        float cursorY = _cursorLine * lineHeight;
        
        return AbsolutePosition + new Vector2(5 - _scrollOffsetX + cursorX, 5 - _scrollOffset + cursorY);
    }

    public void SelectAll() {
        _selStartLine = 0;
        _selStartCol = 0;
        _cursorLine = _lines.Count - 1;
        _cursorCol = _lines[_cursorLine].Length;
    }

    public override void Copy() {
        if (!HasSelection()) return;
        GetSelectionRange(out int sl, out int sc, out int el, out int ec);
        
        StringBuilder sb = new();
        for (int i = sl; i <= el; i++) {
            int start = (i == sl) ? sc : 0;
            int end = (i == el) ? ec : _lines[i].Length;
            sb.Append(_lines[i].Substring(start, end - start));
            if (i < el) sb.Append("\n");
        }
        
        string appId = GetOwnerProcess()?.AppId ?? "Unknown";
        ClipboardManager.Instance.SetData(sb.ToString(), ClipboardContentType.Text, appId);
    }

    public override void Cut() {
        if (!HasSelection()) return;
        Copy();
        DeleteSelection();
    }

    public override void Paste() {
        string text = Shell.Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) return;

        if (HasSelection()) DeleteSelection();

        string[] newLines = text.Split('\n');
        string currentLine = _lines[_cursorLine];
        string before = currentLine.Substring(0, _cursorCol);
        string after = currentLine.Substring(_cursorCol);

        if (newLines.Length == 1) {
            _lines[_cursorLine] = before + newLines[0] + after;
            _cursorCol += newLines[0].Length;
        } else {
            _lines[_cursorLine] = before + newLines[0];
            for (int i = 1; i < newLines.Length - 1; i++) {
                _lines.Insert(_cursorLine + i, newLines[i]);
            }
            _lines.Insert(_cursorLine + newLines.Length - 1, newLines[^1] + after);
            _cursorLine += newLines.Length - 1;
            _cursorCol = newLines[^1].Length;
        }

        ResetSelection();
        _maxWidthDirty = true;
        OnValueChanged?.Invoke(Value);
        EnsureCursorVisible();
    }

    private void SelectWordAtCursor() {
        string line = _lines[_cursorLine];
        if (string.IsNullOrEmpty(line)) return;

        int start = _cursorCol;
        int end = _cursorCol;

        // Find word start
        while (start > 0 && IsWordChar(line[start - 1])) start--;
        // Find word end
        while (end < line.Length && IsWordChar(line[end])) end++;

        _selStartLine = _cursorLine;
        _selStartCol = start;
        _cursorCol = end;
    }

    private bool IsWordChar(char c) {
        return char.IsLetterOrDigit(c) || c == '_';
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

    public override bool HasSelection() => _cursorLine != _selStartLine || _cursorCol != _selStartCol;

    private void ResetSelection() {
        _selStartLine = _cursorLine;
        _selStartCol = _cursorCol;
    }

    public override void DeleteSelection() {
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
        OnValueChanged?.Invoke(Value);
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

        if (cursorY < _targetScrollOffset) _targetScrollOffset = cursorY;
        if (cursorY + lineHeight > _targetScrollOffset + Size.Y - 10) _targetScrollOffset = cursorY + lineHeight - Size.Y + 10;

        float cursorX = _cursorCol == 0 ? 0 : font.MeasureString(_lines[_cursorLine].Substring(0, _cursorCol)).X;
        if (cursorX < _targetScrollOffsetX) _targetScrollOffsetX = cursorX;
        if (cursorX > _targetScrollOffsetX + Size.X - 20) _targetScrollOffsetX = cursorX - Size.X + 20;

        ClampScroll();
    }

    private void ClampScroll() {
        if (GameContent.FontSystem == null) return;
        var font = GameContent.FontSystem.GetFont(FontSize);
        float totalHeight = _lines.Count * font.LineHeight;
        _targetScrollOffset = Math.Clamp(_targetScrollOffset, 0, Math.Max(0, totalHeight - Size.Y + 10));

        if (_maxWidthDirty) {
            float maxW = 0;
            foreach (var l in _lines) {
                float w = font.MeasureString(l).X;
                if (w > maxW) maxW = w;
            }
            _cachedMaxWidth = maxW;
            _maxWidthDirty = false;
        }
        _targetScrollOffsetX = Math.Clamp(_targetScrollOffsetX, 0, Math.Max(0, _cachedMaxWidth - Size.X + 20));
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (IsFocused) {
            _cursorTimer += dt;
            if (_cursorTimer >= 0.5f) { _showCursor = !_showCursor; _cursorTimer = 0f; }

            // Auto-scroll logic while dragging
            if (_isPressed && InputManager.IsMouseButtonDown(MouseButton.Left) && !_isWordSelecting) {
                // Input consumption is handled in UpdateInput()
                var mousePos = InputManager.MousePosition.ToVector2();
                var absPos = AbsolutePosition;
                float scrollSpeed = 500f * dt;
                bool moved = false;

                if (mousePos.Y < absPos.Y) { _targetScrollOffset -= scrollSpeed; moved = true; }
                else if (mousePos.Y > absPos.Y + Size.Y) { _targetScrollOffset += scrollSpeed; moved = true; }
                
                if (mousePos.X < absPos.X) { _targetScrollOffsetX -= scrollSpeed; moved = true; }
                else if (mousePos.X > absPos.X + Size.X) { _targetScrollOffsetX += scrollSpeed; moved = true; }

                if (moved) {
                    ClampScroll();
                    // Set scroll immediately so SetCursorFromMouse uses current view
                    _scrollOffset = _targetScrollOffset;
                    _scrollOffsetX = _targetScrollOffsetX;
                    SetCursorFromMouse(); 
                }
            }
        } else {
            _showCursor = false;
        }

        // Smooth scroll
        _scrollOffset = MathHelper.Lerp(_scrollOffset, _targetScrollOffset, MathHelper.Clamp(dt * 15f, 0, 1));
        if (Math.Abs(_scrollOffset - _targetScrollOffset) < 0.1f) _scrollOffset = _targetScrollOffset;

        _scrollOffsetX = MathHelper.Lerp(_scrollOffsetX, _targetScrollOffsetX, MathHelper.Clamp(dt * 15f, 0, 1));
        if (Math.Abs(_scrollOffsetX - _targetScrollOffsetX) < 0.1f) _scrollOffsetX = _targetScrollOffsetX;
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

        // Clip region using GraphicsDevice scissor
        var oldScissor = G.GraphicsDevice.ScissorRectangle;
        // Float to int conversion can be tricky, use Floor and Ceiling for safety
        int scX = (int)Math.Floor(absPos.X + 2);
        int scY = (int)Math.Floor(absPos.Y + 2);
        int scW = (int)Math.Ceiling(Size.X - 4);
        int scH = (int)Math.Ceiling(Size.Y - 4);
        var scissor = new Rectangle(scX, scY, scW, scH);
        
        // Intersect with existing scissor if any
        scissor = Rectangle.Intersect(oldScissor, scissor);
        
        // We ensure we don't have an empty/invalid scissor that would cause crash or no drawing
        if (scissor.Width <= 0 || scissor.Height <= 0) return;

        // End current batches to apply scissor
        batch.End();
        spriteBatch.End();
        
        var oldState = G.GraphicsDevice.ScissorRectangle;
        G.GraphicsDevice.ScissorRectangle = scissor;
        
        var rasterizerState = _scissorRasterizer;
        G.GraphicsDevice.RasterizerState = rasterizerState;
        
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState);

        float textX = absPos.X + 5 - _scrollOffsetX;
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

        // Draw lines (optimized: only visible lines)
        int firstLine = (int)Math.Floor(_scrollOffset / lineHeight);
        int lastLine = (int)Math.Ceiling((_scrollOffset + Size.Y) / lineHeight);
        firstLine = Math.Clamp(firstLine, 0, _lines.Count - 1);
        lastLine = Math.Clamp(lastLine, 0, _lines.Count - 1);

        for (int i = firstLine; i <= lastLine; i++) {
            float y = textY + i * lineHeight;
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
        
        batch.End();
        spriteBatch.End();

        G.GraphicsDevice.ScissorRectangle = oldState;
        
        // Resume batches for subsequent UI elements
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
    }
}
