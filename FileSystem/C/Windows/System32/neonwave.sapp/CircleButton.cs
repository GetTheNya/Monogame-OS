using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core;
using TheGame.Graphics;
using TheGame;
using FontStashSharp;

namespace NeonWave;

public class CircleButton : Button {
    public Color NeonColor { get; set; } = Color.Cyan;
    public Color GlowColor { get; set; } = Color.Cyan * 0.3f;

    public CircleButton(Vector2 position, Vector2 size, string text = "") : base(position, size, text) {
        BackgroundColor = Color.Transparent;
        BorderColor = Color.Transparent;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        float radius = Math.Min(Size.X, Size.Y) / 2f * Scale;
        Vector2 center = AbsolutePosition + (Size * Scale) / 2f;

        bool isPressed = ControlState == ControlState.Pressed;
        bool isHovered = ControlState == ControlState.Hovered;

        // Glow
        shapeBatch.FillCircle(center, radius + 4, GlowColor * AbsoluteOpacity);
        
        // Background
        Color bg = isPressed ? Color.White * 0.2f : (isHovered ? Color.White * 0.1f : Color.Black * 0.5f);
        shapeBatch.FillCircle(center, radius, bg * AbsoluteOpacity);
        
        // Border (Neon)
        shapeBatch.BorderCircle(center, radius, (isPressed ? Color.White : NeonColor) * AbsoluteOpacity, 2f);

        // Content (Icon/Text)
        if (!string.IsNullOrEmpty(Text)) {
            var font = GameContent.FontSystem?.GetFont((int)(FontSize * Scale));
            if (font != null) {
                Vector2 textSize = font.MeasureString(Text);
                font.DrawText(shapeBatch, Text, center - textSize / 2f, (isPressed ? Color.Black : Color.White) * AbsoluteOpacity);
            }
        }
    }
}
