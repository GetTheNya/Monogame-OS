using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.ComponentModel;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Graphics;
using TheGame.Core.Designer;

namespace TheGame.Core.UI.Controls;

public class TextInput : ValueControl<string> {
    public string Placeholder { get; set; } = "Type here...";
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public System.Action<string> OnSubmit { get; set; }

    public Color FocusedBorderColor { get; set; } = new Color(0, 120, 215);
    public Color TextColor { get; set; } = Color.White;

    private float _cursorTimer = 0f;
    private bool _showCursor = true;
    private bool _isWordSelecting = false;
    private int _wordSelectAnchorStart = 0;
    private int _wordSelectAnchorEnd = 0;

    private int _cursorPos = 0;
    private int _selectionEnd = 0; // If != _cursorPos, we have a selection
    private float _scrollX = 0f;
    private float _targetScrollX = 0f;
    private static readonly RasterizerState _scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };

    // Override Value to reset cursor/selection when changed programmatically
    public override void SetValue(string value, bool notify = true) {
        value ??= "";
        if (Value == value) return;
        base.SetValue(value, notify);
        _cursorPos = value.Length;
        _selectionEnd = _cursorPos;
        _targetScrollX = 0;
    }

    [Obsolete("For Designer/Serialization use only", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public TextInput() : this(Vector2.Zero, Vector2.Zero) { }

    public TextInput(Vector2 position, Vector2 size) : base(position, size, "") {
        ConsumesInput = true;
    }

    protected override void OnHover() {
        CustomCursor.Instance.SetCursor(CursorType.Beam);
        base.OnHover();
    }

    protected override void UpdateInput() {
        if (DesignMode.SuppressNormalInput(this)) return;

        base.UpdateInput(); // Update IsMouseOver first

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            if (IsMouseOver) {
                // Set cursor based on click position
                _cursorPos = GetCursorIndexFromMouse();
                _selectionEnd = _cursorPos;
                _isWordSelecting = false;
                InputManager.IsMouseConsumed = true; // Consume mouse input
            }
        }

        if (IsFocused) {
            // Drag selection: Use ignoreConsumed: true so we don't block ourselves if we already consumed the mouse this frame
            if (_isPressed && InputManager.IsMouseButtonDown(MouseButton.Left)) {
                InputManager.IsMouseConsumed = true;
                if (!_isWordSelecting) {
                    _cursorPos = GetCursorIndexFromMouse();
                } else {
                    // Word selection drag logic
                    _cursorPos = GetCursorIndexFromMouse();

                    // Find word boundaries at current position
                    int start = _cursorPos;
                    int end = _cursorPos;
                    while (start > 0 && IsWordChar(Value[start - 1])) start--;
                    while (end < Value.Length && IsWordChar(Value[end])) end++;

                    // If dragging forward from anchor
                    if (_cursorPos >= _wordSelectAnchorStart) {
                        _selectionEnd = _wordSelectAnchorStart;
                        _cursorPos = end;
                    } else {
                        // Dragging backward
                        _selectionEnd = _wordSelectAnchorEnd;
                        _cursorPos = start;
                    }
                }
            } else {
                _isWordSelecting = false;
            }

            if (IsFocused && InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true) && IsMouseOver) {
                SelectWordAtCursor();
                _isWordSelecting = true;
                InputManager.IsMouseConsumed = true;
            }

            // Keyboard navigation & Repeat
            bool shift = InputManager.IsKeyDown(Keys.LeftShift) || InputManager.IsKeyDown(Keys.RightShift);
            bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || InputManager.IsKeyDown(Keys.RightControl);

            if (InputManager.IsKeyRepeated(Keys.Left)) MoveCursor(-1, shift);
            if (InputManager.IsKeyRepeated(Keys.Right)) MoveCursor(1, shift);
            if (InputManager.IsKeyJustPressed(Keys.Home)) {
                _cursorPos = 0;
                if (!shift) _selectionEnd = _cursorPos;
            }

            if (InputManager.IsKeyJustPressed(Keys.End)) {
                _cursorPos = Value.Length;
                if (!shift) _selectionEnd = _cursorPos;
            }

            if (ctrl && InputManager.IsKeyJustPressed(Keys.A)) {
                _selectionEnd = 0;
                _cursorPos = Value.Length;
            }
            if (ctrl && InputManager.IsKeyJustPressed(Keys.C)) Copy();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.X)) Cut();
            if (ctrl && InputManager.IsKeyJustPressed(Keys.V)) Paste();

            // Handle Backspace
            if (InputManager.IsKeyRepeated(Keys.Back)) {
                if (HasSelection()) DeleteSelection();
                else if (_cursorPos > 0) {
                    string newValue = Value.Remove(_cursorPos - 1, 1);
                    base.SetValue(newValue, true);
                    _cursorPos--;
                    _selectionEnd = _cursorPos;
                }
            }

            // Handle Delete
            if (InputManager.IsKeyRepeated(Keys.Delete)) {
                if (HasSelection()) DeleteSelection();
                else if (_cursorPos < Value.Length) {
                    string newValue = Value.Remove(_cursorPos, 1);
                    base.SetValue(newValue, true);
                }
            }

            // Handle Enter
            if (InputManager.IsKeyJustPressed(Keys.Enter)) {
                OnSubmit?.Invoke(Value);
                UIManager.SetFocus(null);
            }

            // Handle Character Input
            foreach (var c in InputManager.GetTypedChars()) {
                if (char.IsControl(c)) continue;
                if (HasSelection()) DeleteSelection();
                string newVal = Value.Insert(_cursorPos, c.ToString());
                base.SetValue(newVal, true);
                _cursorPos++;
                _selectionEnd = _cursorPos;
                TheGame.Core.DebugLogger.Log($"TextInput Char: {c} (Total: {Value})");
            }

            InputManager.IsKeyboardConsumed = true;
        }

        EnsureCursorVisible();
    }

    private int GetCursorIndexFromMouse() {
        if (GameContent.FontSystem == null || string.IsNullOrEmpty(Value)) return 0;
        var font = GameContent.FontSystem.GetFont(20);
        float localX = InputManager.MousePosition.X - (AbsolutePosition.X + 5) + _scrollX;

        float bestDist = float.MaxValue;
        int bestIdx = 0;

        for (int i = 0; i <= Value.Length; i++) {
            float symWidth = i == 0 ? 0 : font.MeasureString(Value.Substring(0, i)).X;
            float dist = Math.Abs(localX - symWidth);
            if (dist < bestDist) {
                bestDist = dist;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

    public override Vector2? GetCaretPosition() {
        if (GameContent.FontSystem == null) return null;
        var font = GameContent.FontSystem.GetFont(20);
        float textY = (Size.Y - font.LineHeight) / 2f;
        float cursorX = _cursorPos == 0 ? 0 : font.MeasureString(Value.Substring(0, _cursorPos)).X;
        
        return AbsolutePosition + new Vector2(5 - _scrollX + cursorX, textY);
    }

    public override bool HasSelection() => _cursorPos != _selectionEnd;

    public override void DeleteSelection() {
        if (string.IsNullOrEmpty(Value)) {
            _cursorPos = 0;
            _selectionEnd = 0;
            return;
        }
        
        // Clamp selection indices to valid range
        _cursorPos = Math.Clamp(_cursorPos, 0, Value.Length);
        _selectionEnd = Math.Clamp(_selectionEnd, 0, Value.Length);
        
        int start = Math.Min(_cursorPos, _selectionEnd);
        int length = Math.Abs(_cursorPos - _selectionEnd);
        
        // Ensure we don't exceed string bounds
        if (start >= Value.Length) {
            _cursorPos = Value.Length;
            _selectionEnd = _cursorPos;
            return;
        }
        
        length = Math.Min(length, Value.Length - start);
        
        if (length > 0) {
            string newVal = Value.Remove(start, length);
            base.SetValue(newVal, true);
        }
        _cursorPos = start;
        _selectionEnd = _cursorPos;
    }

    public override void Copy() {
        if (!HasSelection()) return;
        int start = Math.Min(_cursorPos, _selectionEnd);
        int length = Math.Abs(_cursorPos - _selectionEnd);
        string text = Value.Substring(start, length);
        
        string appId = GetOwnerProcess()?.AppId ?? "Unknown";
        ClipboardManager.Instance.SetData(text, ClipboardContentType.Text, appId);
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
        
        string newVal = Value.Insert(_cursorPos, text);
        base.SetValue(newVal, true);
        _cursorPos += text.Length;
        _selectionEnd = _cursorPos;
    }

    public void SelectAll() {
        _selectionEnd = 0;
        _cursorPos = Value?.Length ?? 0;
    }

    private void MoveCursor(int delta, bool select) {
        _cursorPos = MathHelper.Clamp(_cursorPos + delta, 0, Value.Length);
        if (!select) _selectionEnd = _cursorPos;
    }

    private void EnsureCursorVisible() {
        if (GameContent.FontSystem == null) return;
        if (string.IsNullOrEmpty(Value)) {
            _cursorPos = 0;
            _targetScrollX = 0;
            return;
        }
        
        // Clamp cursor position to valid range
        _cursorPos = Math.Clamp(_cursorPos, 0, Value.Length);
        
        var font = GameContent.FontSystem.GetFont(20);
        float cursorX = _cursorPos == 0 ? 0 : font.MeasureString(Value.Substring(0, _cursorPos)).X;

        if (cursorX < _targetScrollX) _targetScrollX = cursorX;
        if (cursorX > _targetScrollX + Size.X - 15) _targetScrollX = cursorX - Size.X + 15;

        float maxW = font.MeasureString(Value).X;
        _targetScrollX = Math.Clamp(_targetScrollX, 0, Math.Max(0, maxW - Size.X + 15));
    }

    private void SelectWordAtCursor() {
        if (string.IsNullOrEmpty(Value)) return;

        int start = _cursorPos;
        int end = _cursorPos;

        // Find word start
        while (start > 0 && IsWordChar(Value[start - 1])) start--;
        // Find word end
        while (end < Value.Length && IsWordChar(Value[end])) end++;

        _selectionEnd = start;
        _cursorPos = end;

        // Store anchors for word-selection dragging
        _wordSelectAnchorStart = start;
        _wordSelectAnchorEnd = end;
    }

    private bool IsWordChar(char c) {
        return char.IsLetterOrDigit(c) || c == '_';
    }

    private float _focusAnim = 0f;

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        float target = IsFocused ? 1f : 0f;
        _focusAnim = MathHelper.Lerp(_focusAnim, target, MathHelper.Clamp(dt * 15f, 0, 1));

        if (IsFocused) {
            _cursorTimer += dt;
            if (_cursorTimer >= 0.5f) {
                _showCursor = !_showCursor;
                _cursorTimer = 0f;
            }
            
            // If background should be white (like search bars or renames), don't wait for lerp
            if (BackgroundColor == Color.White) CurrentBackgroundColor = Color.White;

            // Auto-scroll when dragging horizontally
            if (_isPressed && InputManager.IsMouseButtonDown(MouseButton.Left) && !_isWordSelecting) {
                // Input consumption is handled in UpdateInput()

                var mousePos = InputManager.MousePosition.ToVector2();
                var absPos = AbsolutePosition;
                float scrollSpeed = 500f * dt;
                bool moved = false;

                if (mousePos.X < absPos.X) { _targetScrollX -= scrollSpeed; moved = true; }
                else if (mousePos.X > absPos.X + Size.X) { _targetScrollX += scrollSpeed; moved = true; }

                if (moved) {
                    float maxW = GameContent.FontSystem.GetFont(20).MeasureString(Value).X;
                    _targetScrollX = Math.Clamp(_targetScrollX, 0, Math.Max(0, maxW - Size.X + 15));
                    _scrollX = _targetScrollX;
                    _cursorPos = GetCursorIndexFromMouse();
                }
            }
        } else {
            _showCursor = false;
        }

        _scrollX = MathHelper.Lerp(_scrollX, _targetScrollX, MathHelper.Clamp(dt * 15f, 0, 1));
        if (Math.Abs(_scrollX - _targetScrollX) < 0.1f) _scrollX = _targetScrollX;
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;
        Color border = Color.Lerp(BorderColor, FocusedBorderColor, _focusAnim);

        // Background
        batch.FillRectangle(absPos, Size, CurrentBackgroundColor * AbsoluteOpacity, rounded: 3f);
        batch.BorderRectangle(absPos, Size, border * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        // Text & Selection
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(20);
            float textY = (Size.Y - font.LineHeight) / 2f;
            Vector2 textPos = absPos + new Vector2(5 - _scrollX, textY);

            // Clip region
            var oldScissor = G.GraphicsDevice.ScissorRectangle;
            int scX = (int)Math.Floor(absPos.X + 2);
            int scY = (int)Math.Floor(absPos.Y + 2);
            int scW = (int)Math.Ceiling(Size.X - 4);
            int scH = (int)Math.Ceiling(Size.Y - 4);
            var scissor = new Rectangle(scX, scY, scW, scH);
            scissor = Rectangle.Intersect(oldScissor, scissor);

            if (scissor.Width > 0 && scissor.Height > 0) {
                batch.End();
                spriteBatch.End();

                var oldState = G.GraphicsDevice.ScissorRectangle;
                G.GraphicsDevice.ScissorRectangle = scissor;

                var rasterizerState = _scissorRasterizer;
                G.GraphicsDevice.RasterizerState = rasterizerState;

                batch.Begin();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, rasterizerState);

                // Selection
                if (HasSelection() && !string.IsNullOrEmpty(Value)) {
                    int start = Math.Clamp(Math.Min(_cursorPos, _selectionEnd), 0, Value.Length);
                    int end = Math.Clamp(Math.Max(_cursorPos, _selectionEnd), 0, Value.Length);

                    float x1 = start == 0 ? 0 : font.MeasureString(Value.Substring(0, start)).X;
                    float x2 = end == 0 ? 0 : font.MeasureString(Value.Substring(0, end)).X;

                    batch.FillRectangle(textPos + new Vector2(x1, 0), new Vector2(x2 - x1, font.LineHeight), FocusedBorderColor * (0.3f * AbsoluteOpacity));
                }

                if (string.IsNullOrEmpty(Value)) {
                    font.DrawText(batch, Placeholder, textPos, Color.Gray * (0.7f * AbsoluteOpacity));
                } else {
                    font.DrawText(batch, Value, textPos, TextColor * AbsoluteOpacity);
                }

                // Cursor
                if (_showCursor || (HasSelection() && IsFocused)) {
                    int clampedCursor = Math.Clamp(_cursorPos, 0, Value?.Length ?? 0);
                    float textWidth = clampedCursor == 0 || string.IsNullOrEmpty(Value) ? 0 : font.MeasureString(Value.Substring(0, clampedCursor)).X;
                    batch.FillRectangle(textPos + new Vector2(textWidth + 1, 0), new Vector2(2, font.LineHeight), FocusedBorderColor * AbsoluteOpacity);
                }

                batch.End();
                spriteBatch.End();

                G.GraphicsDevice.ScissorRectangle = oldState;

                batch.Begin();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
            }
        }
    }
}
