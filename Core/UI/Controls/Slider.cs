using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.Designer;

namespace TheGame.Core.UI.Controls;

public class Slider : ValueControl<float> {
    private bool _isDraggingSlider;
    public bool IsDragging => _isDraggingSlider;

    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Slider() : this(Vector2.Zero, 0) {}

    public Slider(Vector2 position, float width) : base(position, new Vector2(width, 20)) {
    }

    private float _thumbScale = 1.0f;
    private float _visualValue = 0f;

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float targetScale = (ControlState == ControlState.Hovered || _isDraggingSlider) ? 1.3f : 1.0f;
        _thumbScale = MathHelper.Lerp(_thumbScale, targetScale, MathHelper.Clamp(dt * 15f, 0, 1));

        // Smoothly follow actual value for that "sliding" effect
        float followSpeed = _isDraggingSlider ? 40f : 15f;
        _visualValue = MathHelper.Lerp(_visualValue, Value, MathHelper.Clamp(dt * followSpeed, 0, 1));
    }

    protected override void UpdateInput() {
        if (DesignMode.SuppressNormalInput(this)) return;

        if (IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            _isDraggingSlider = true;
            // First click on track - update value immediately so it starts sliding
            float localX = InputManager.MousePosition.X - AbsolutePosition.X;
            Value = MathHelper.Clamp(localX / Size.X, 0f, 1f);
        }

        if (_isDraggingSlider) {
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                float localX = InputManager.MousePosition.X - AbsolutePosition.X;
                Value = MathHelper.Clamp(localX / Size.X, 0f, 1f);
                InputManager.IsMouseConsumed = true;
            } else {
                _isDraggingSlider = false;
            }
        }

        base.UpdateInput();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;
        float trackHeight = 4f;
        Vector2 trackPos = absPos + new Vector2(0, (Size.Y - trackHeight) / 2f);

        // Track
        batch.FillRectangle(trackPos, new Vector2(Size.X, trackHeight), BackgroundColor * AbsoluteOpacity, rounded: trackHeight / 2f);

        // Fill (Visual)
        batch.FillRectangle(trackPos, new Vector2(Size.X * _visualValue, trackHeight), AccentColor * AbsoluteOpacity, rounded: trackHeight / 2f);

        // Thumb (Visual)
        float thumbRadius = 8f * _thumbScale;
        Vector2 thumbPos = trackPos + new Vector2(Size.X * _visualValue, trackHeight / 2f);
        batch.FillCircle(thumbPos, thumbRadius, Color.White * AbsoluteOpacity);
        batch.BorderCircle(thumbPos, thumbRadius, BorderColor * AbsoluteOpacity, thickness: 1f);
    }
}
