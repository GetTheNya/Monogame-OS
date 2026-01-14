using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.Input;
using TheGame.Core.Animation;

namespace TheGame.Core.UI.Controls;

public class Switch : ValueControl<bool> {
    public string Label { get; set; }
    public Color TextColor { get; set; } = Color.White;

    private float _thumbOffset = 0f; // 0 to 1

    public Switch(Vector2 position, string label = "") : base(position, new Vector2(40, 20)) {
        Label = label;
    }

    private bool _didDrag;
    private bool _isDragging;
    private float _dragStartX;

    protected override void UpdateInput() {
        base.UpdateInput(); // handle hover/_isPressed/click

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left) && IsMouseOver) {
            float localX = InputManager.MousePosition.X - AbsolutePosition.X;
            // Only start tracking a potential drag if we clicked the switch track area (0-44px)
            _isDragging = (localX <= 44);
            _didDrag = false;
            _dragStartX = InputManager.MousePosition.X;
        }

        if (_isDragging) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                // Only follow mouse if we have moved enough to consider it a deliberate drag
                if (!_didDrag && Math.Abs(InputManager.MousePosition.X - _dragStartX) > 2f) {
                    _didDrag = true;
                }

                if (_didDrag) {
                    float localX = InputManager.MousePosition.X - AbsolutePosition.X;
                    _thumbOffset = MathHelper.Clamp((localX - 4f) / 32f, 0f, 1f);

                    // Update state visually while dragging
                    bool newValue = _thumbOffset > 0.5f;
                    if (Value != newValue) {
                        Value = newValue;
                    }
                }

                InputManager.IsMouseConsumed = true;
            } else {
                _isDragging = false;
            }
        }
    }

    public override void Update(GameTime gameTime) {
        // Measure text hit box
        if (GameContent.FontSystem != null && !string.IsNullOrEmpty(Label)) {
            var font = GameContent.FontSystem.GetFont(20);
            var size = font.MeasureString(Label);
            Size = new Vector2(48 + size.X, 20);
        }

        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Only lerp if NOT dragging. If dragging, offset is set by mouse.
        if (!_isDragging) {
            float target = Value ? 1f : 0f;
            _thumbOffset = MathHelper.Lerp(_thumbOffset, target, MathHelper.Clamp(dt * 15f, 0, 1));
        }
    }

    protected override void OnClick() {
        // Only toggle if we didn't drag enough for a deliberate slide
        if (!_didDrag) {
            Value = !Value;
        }

        base.OnClick();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;

        // Track
        Color trackColor = Color.Lerp(CurrentBackgroundColor, AccentColor, _thumbOffset);
        batch.FillRectangle(absPos, new Vector2(40, 20), trackColor * AbsoluteOpacity, rounded: 10f);

        // Thumb
        float thumbSize = 16f * Scale;
        float minX = absPos.X + 2;
        float maxX = absPos.X + 40 - thumbSize - 2;
        float thumbX = MathHelper.Lerp(minX, maxX, _thumbOffset);

        batch.FillRectangle(new Vector2(thumbX, absPos.Y + 2), new Vector2(thumbSize, thumbSize), Color.White * AbsoluteOpacity, rounded: thumbSize / 2f);

        // Draw Label
        if (!string.IsNullOrEmpty(Label) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(20);
            float labelY = (Size.Y - font.LineHeight) / 2f;
            font.DrawText(batch, Label, absPos + new Vector2(48, labelY), TextColor * AbsoluteOpacity);
        }
    }
}
