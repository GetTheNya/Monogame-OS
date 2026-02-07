using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.Designer;

namespace TheGame.Core.UI.Controls;

public class ColorPicker : ValueControl<Color> {
    private float _h, _s, _v, _a;
    private bool _isDraggingHue;
    private bool _isDraggingSaturation;
    private bool _isDraggingValue;
    private bool _isDraggingAlpha;

    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ColorPicker() : this(Vector2.Zero, 60) { }

    public ColorPicker(Vector2 position, float radius = 60f) : base(position, new Vector2(radius * 2 + 40, radius * 2 + 100f), Color.White) {
        UpdateHSVFromColor(Value);
    }

    public override void SetValue(Color value, bool notify = true) {
        if (Equals(Value, value)) return;
        base.SetValue(value, notify);
        UpdateHSVFromColor(value);
    }

    private void UpdateHSVFromColor(Color c) {
        _a = c.A / 255f;
        ColorToHSV(c, out _h, out _s, out _v);
    }

    private void UpdateColorFromHSV() {
        var c = ColorFromHSV(_h, _s, _v);
        c.A = (byte)(_a * 255f);
        // Use base.SetValue to avoid recursive HSV update if we want to preserve precision
        // but actually it's fine to just set Value.
        base.SetValue(c, true);
    }

    protected override void UpdateInput() {
        if (DesignMode.SuppressNormalInput(this)) return;

        Vector2 mousePos = InputManager.MousePosition.ToVector2();
        float radius = (Size.X - 40f) / 2f;
        Vector2 center = AbsolutePosition + new Vector2(radius + 20f, radius + 20f);

        if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left) && IsMouseOver) {
            float dist = Vector2.Distance(mousePos, center);
            
            // Hue Check
            if (dist > radius - 15f && dist < radius + 15f) {
                _isDraggingHue = true;
            } else {
                // Check sliders
                float startY = AbsolutePosition.Y + radius * 2f + 40f;
                if (mousePos.Y >= startY && mousePos.Y < startY + 15f) _isDraggingSaturation = true;
                else if (mousePos.Y >= startY + 20f && mousePos.Y < startY + 35f) _isDraggingValue = true;
                else if (mousePos.Y >= startY + 40f && mousePos.Y < startY + 55f) _isDraggingAlpha = true;
            }
        }

        if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
            if (_isDraggingHue) {
                Vector2 dir = mousePos - center;
                // Mirrored (CCW) mapping:
                // Flip X because the shader's visual ring is mirrored horizontally
                dir.X = -dir.X;
                _h = (MathF.Atan2(dir.Y, dir.X) / (MathF.PI * 2f) + 0.25f + 1f) % 1f;

                UpdateColorFromHSV();
                InputManager.IsMouseConsumed = true;
            } else if (_isDraggingSaturation) {
                _s = MathHelper.Clamp((mousePos.X - AbsolutePosition.X - 10f) / (Size.X - 20f), 0f, 1f);
                UpdateColorFromHSV();
                InputManager.IsMouseConsumed = true;
            } else if (_isDraggingValue) {
                _v = MathHelper.Clamp((mousePos.X - AbsolutePosition.X - 10f) / (Size.X - 20f), 0f, 1f);
                UpdateColorFromHSV();
                InputManager.IsMouseConsumed = true;
            } else if (_isDraggingAlpha) {
                _a = MathHelper.Clamp((mousePos.X - AbsolutePosition.X - 10f) / (Size.X - 20f), 0f, 1f);

                UpdateColorFromHSV();
                InputManager.IsMouseConsumed = true;
            }
        } else {
            _isDraggingHue = false;
            _isDraggingSaturation = false;
            _isDraggingValue = false;
            _isDraggingAlpha = false;
        }

        base.UpdateInput();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;

        // Draw background panel
        batch.FillRectangle(absPos, Size, CurrentBackgroundColor * AbsoluteOpacity, rounded: 10f);
        batch.BorderRectangle(absPos, Size, BorderColor * AbsoluteOpacity, rounded: 10f, thickness: 1f);

        float radius = (Size.X - 40f) / 2f;
        Vector2 center = absPos + new Vector2(radius + 20f, radius + 20f);

        // Draw Hue Ring (Mirrored visually in ShapeBatch)
        batch.DrawRainbowCircle(center, radius, BorderColor * AbsoluteOpacity, thickness: 15f);

        // Hue indicator
        // Match the mirrored orientation: Top is -PI/2, increasing CCW
        float angle = -(_h * MathF.PI * 2f) - MathF.PI / 2f;
        Vector2 hueIndicatorPos = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        batch.FillCircle(hueIndicatorPos, 10f, Color.White * AbsoluteOpacity);
        batch.BorderCircle(hueIndicatorPos, 10f, Color.Black * 0.5f * AbsoluteOpacity, thickness: 2f);

        // Preview box in center
        var previewColor = Value;
        previewColor.A = 255;
        batch.FillCircle(center, radius - 20f, previewColor * AbsoluteOpacity);
        batch.BorderCircle(center, radius - 20f, BorderColor * AbsoluteOpacity, thickness: 2f);

        // Sliders
        float startY = absPos.Y + radius * 2f + 40f;
        DrawSlider(batch, "S", new Vector2(absPos.X + 10, startY), _s, ColorFromHSV(_h, 1f, 1f));
        DrawSlider(batch, "V", new Vector2(absPos.X + 10, startY + 20), _v, Color.White);
        DrawSlider(batch, "A", new Vector2(absPos.X + 10, startY + 40), _a, Color.Gray * 1.5f);
    }


    private void DrawSlider(ShapeBatch batch, string label, Vector2 pos, float value, Color color) {
        float width = Size.X - 20f;
        float height = 12f;
        
        // Track
        batch.FillRectangle(pos, new Vector2(width, height), BackgroundColor * AbsoluteOpacity, rounded: 6f);
        batch.FillRectangle(pos, new Vector2(width * value, height), color * AbsoluteOpacity, rounded: 6f);
        batch.BorderRectangle(pos, new Vector2(width, height), BorderColor * AbsoluteOpacity, rounded: 6f);

        // Thumb
        batch.FillCircle(new Vector2(pos.X + width * value, pos.Y + height / 2f), 8f, Color.White * AbsoluteOpacity);
        batch.BorderCircle(new Vector2(pos.X + width * value, pos.Y + height / 2f), 8f, BorderColor * AbsoluteOpacity);
    }


    // Helper methods for HSV/RGB conversion
    public static void ColorToHSV(Color color, out float h, out float s, out float v) {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        h = 0;
        if (delta > 0) {
            if (max == r) h = (g - b) / delta + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / delta + 2;
            else h = (r - g) / delta + 4;
            h /= 6;
        }

        s = (max == 0) ? 0 : delta / max;
        v = max;
    }

    public static Color ColorFromHSV(float h, float s, float v) {
        int i = (int)(h * 6);
        float f = h * 6 - i;
        float p = v * (1 - s);
        float q = v * (1 - f * s);
        float t = v * (1 - (1 - f) * s);

        float r, g, b;
        switch (i % 6) {
            case 0: r = v; g = t; b = p; break;
            case 1: r = q; g = v; b = p; break;
            case 2: r = p; g = v; b = t; break;
            case 3: r = p; g = q; b = v; break;
            case 4: r = t; g = p; b = v; break;
            case 5: r = v; g = p; b = q; break;
            default: r = g = b = 0; break;
        }

        return new Color(r, g, b);
    }
}
