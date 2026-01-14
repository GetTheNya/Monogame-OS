using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class TextInput : ValueControl<string> {
    public string Placeholder { get; set; } = "Type here...";
    public System.Action<string> OnSubmit { get; set; }

    public Color FocusedBorderColor { get; set; } = new Color(0, 120, 215);
    public Color TextColor { get; set; } = Color.White;

    private float _cursorTimer = 0f;
    private bool _showCursor = true;

    private int _cursorPos = 0;
    private int _selectionEnd = 0; // If != _cursorPos, we have a selection

    public TextInput(Vector2 position, Vector2 size) : base(position, size, "") {
        ConsumesInput = true;
    }

    protected override void UpdateInput() {
        base.UpdateInput(); // Update IsMouseOver first

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            IsFocused = IsMouseOver;
            if (IsFocused) {
                // Set cursor based on click position
                _cursorPos = GetCursorIndexFromMouse();
                _selectionEnd = _cursorPos;
            }
        }

        if (IsFocused) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                // Dragging to select
                _cursorPos = GetCursorIndexFromMouse();
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

            // Handle Backspace
            if (InputManager.IsKeyRepeated(Keys.Back)) {
                if (HasSelection()) DeleteSelection();
                else if (_cursorPos > 0) {
                    Value = Value.Remove(_cursorPos - 1, 1);
                    _cursorPos--;
                    _selectionEnd = _cursorPos;
                }
            }

            // Handle Delete
            if (InputManager.IsKeyRepeated(Keys.Delete)) {
                if (HasSelection()) DeleteSelection();
                else if (_cursorPos < Value.Length) {
                    Value = Value.Remove(_cursorPos, 1);
                }
            }

            // Handle Enter
            if (InputManager.IsKeyJustPressed(Keys.Enter)) {
                OnSubmit?.Invoke(Value);
                IsFocused = false;
            }

            // Handle Character Input
            foreach (var c in InputManager.GetTypedChars()) {
                if (char.IsControl(c)) continue;
                if (HasSelection()) DeleteSelection();
                Value = Value.Insert(_cursorPos, c.ToString());
                _cursorPos++;
                _selectionEnd = _cursorPos;
                TheGame.Core.DebugLogger.Log($"TextInput Char: {c} (Total: {Value})");
            }

            InputManager.IsKeyboardConsumed = true;
        }

        base.UpdateInput();
    }

    private int GetCursorIndexFromMouse() {
        if (GameContent.FontSystem == null || string.IsNullOrEmpty(Value)) return 0;
        var font = GameContent.FontSystem.GetFont(20);
        float localX = InputManager.MousePosition.X - (AbsolutePosition.X + 5);

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

    private bool HasSelection() => _cursorPos != _selectionEnd;

    private void DeleteSelection() {
        int start = Math.Min(_cursorPos, _selectionEnd);
        int length = Math.Abs(_cursorPos - _selectionEnd);
        Value = Value.Remove(start, length);
        _cursorPos = start;
        _selectionEnd = _cursorPos;
    }

    private void MoveCursor(int delta, bool select) {
        _cursorPos = MathHelper.Clamp(_cursorPos + delta, 0, Value.Length);
        if (!select) _selectionEnd = _cursorPos;
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
        } else {
            _showCursor = false;
        }
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
            Vector2 textPos = absPos + new Vector2(5, textY);

            // Selection
            if (HasSelection()) {
                int start = Math.Min(_cursorPos, _selectionEnd);
                int end = Math.Max(_cursorPos, _selectionEnd);

                float x1 = start == 0 ? 0 : font.MeasureString(Value.Substring(0, start)).X;
                float x2 = font.MeasureString(Value.Substring(0, end)).X;

                batch.FillRectangle(textPos + new Vector2(x1, 0), new Vector2(x2 - x1, font.LineHeight), FocusedBorderColor * (0.3f * AbsoluteOpacity));
            }

            if (string.IsNullOrEmpty(Value)) {
                font.DrawText(batch, Placeholder, textPos, Color.Gray * (0.7f * AbsoluteOpacity));
            } else {
                font.DrawText(batch, Value, textPos, TextColor * AbsoluteOpacity);
            }

            // Cursor
            if (_showCursor || (HasSelection() && IsFocused)) {
                float textWidth = _cursorPos == 0 ? 0 : font.MeasureString(Value.Substring(0, _cursorPos)).X;
                batch.FillRectangle(textPos + new Vector2(textWidth + 1, 0), new Vector2(2, font.LineHeight), FocusedBorderColor * AbsoluteOpacity);
            }
        }
    }
}
