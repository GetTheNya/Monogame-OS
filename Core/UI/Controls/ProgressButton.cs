using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.UI;

namespace TheGame.Core.UI.Controls;

public class ProgressButton : Button {
    public float Progress { get; set; } = -1.0f;
    public Color ProgressColor { get; set; } = new Color(0, 200, 0);

    public ProgressButton(Vector2 position, Vector2 size, string text = "") : base(position, size, text) {
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        Vector2 size = Size * Scale;
        Vector2 offset = (Size - size) / 2f;
        Vector2 drawPos = absPos + offset;

        // Background
        batch.FillRectangle(drawPos, size, CurrentBackgroundColor * AbsoluteOpacity, rounded: 3f);

        // Progress Overlay
        if (Progress >= 0f) {
            float progressWidth = size.X * MathHelper.Clamp(Progress, 0f, 1f);
            if (progressWidth > 0) {
                batch.FillRectangle(drawPos, new Vector2(progressWidth, size.Y), ProgressColor * 0.3f * AbsoluteOpacity, rounded: 3f);
            }
        }

        // Border
        batch.BorderRectangle(drawPos, size, BorderColor * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        // Hover Effect Layer
        if (ControlState == ControlState.Hovered) {
            batch.FillRectangle(drawPos, size, Color.White * 0.05f * AbsoluteOpacity, rounded: 3f);
        }

        DrawContent(spriteBatch, batch, drawPos, size);
    }

    protected virtual void DrawContent(SpriteBatch spriteBatch, ShapeBatch batch, Vector2 drawPos, Vector2 size) {
        float pL = Padding.X * Scale;
        float pT = Padding.Y * Scale;
        float pR = Padding.Z * Scale;
        float pB = Padding.W * Scale;
        float iconSize = 0f;

        // Draw Icon if present
        if (Icon != null) {
            iconSize = size.Y - (pT + pB);
            float scale = Math.Min(iconSize / Icon.Width, iconSize / Icon.Height);
            float drawW = Icon.Width * scale;
            float drawH = Icon.Height * scale;
            var iconPos = new Vector2(drawPos.X + pL + (iconSize - drawW) / 2, drawPos.Y + pT + (iconSize - drawH) / 2);
            batch.DrawTexture(Icon, iconPos, Color.White * AbsoluteOpacity, scale);
        }

        // Text (Centering logic with Scrolling/Truncation)
        if (!string.IsNullOrEmpty(Text) && (GameContent.FontSystem != null || GameContent.BoldFontSystem != null)) {
            var fontSystem = UseBoldFont ? GameContent.BoldFontSystem : GameContent.FontSystem;
            var font = fontSystem?.GetFont((int)(FontSize * Scale));
            if (font != null) {
                float contentStartX = drawPos.X + pL + iconSize + (iconSize > 0 ? pL : 0);
                float remainingWidth = size.X - (contentStartX - drawPos.X) - pR;

                if (remainingWidth > 5) {
                    // We only use the fancy scissored scrolling if hovered AND it's actually overflowing
                    // Note: This logic is duplicated from Button.cs because DrawSelf is overridden.
                    // Ideally Button.cs would expose a DrawText method, but we'll stick to this for now.
                    
                    // Accessing private fields of Button is not possible, so we need to re-implement or modify Button.cs
                    // I will modify Button.cs to make its drawing logic more reusable.
                    
                    string textToDraw = TextHelper.TruncateWithEllipsis(font, Text, remainingWidth);
                    var textSize = font.MeasureString(textToDraw);

                    Vector2 textPos;
                    if (TextAlign == TextAlign.Left) {
                        textPos = new Vector2(contentStartX, drawPos.Y + (size.Y - textSize.Y) / 2f);
                    } else if (TextAlign == TextAlign.Right) {
                        textPos = new Vector2(contentStartX + remainingWidth - textSize.X, drawPos.Y + (size.Y - textSize.Y) / 2f);
                    } else {
                        textPos = new Vector2(
                            contentStartX + (remainingWidth - textSize.X) / 2f,
                            drawPos.Y + (size.Y - textSize.Y) / 2f
                        );
                    }

                    font.DrawText(batch, textToDraw, textPos, TextColor * AbsoluteOpacity);
                }
            }
        }
    }
}
