using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public enum TextAlign { Center, Left, Right }

public class Button : UIControl {
    public string Text { get; set; } = "";
    public Texture2D Icon { get; set; }
    public Action OnClickAction { get; set; }
    public Color TextColor { get; set; } = Color.White;
    public TextAlign TextAlign { get; set; } = TextAlign.Center;
    public int FontSize { get; set; } = 20;
    public Vector2 Padding { get; set; } = new Vector2(5, 5);

    public Button(Vector2 position, Vector2 size, string text = "") : base(position, size) {
        Text = text;
    }

    protected override void OnClick() {
        TheGame.Core.OS.Shell.Audio.PlaySound("C:\\Windows\\Media\\click.wav", 0.5f);
        try {
            OnClickAction?.Invoke();
        } catch (Exception ex) {
            var process = GetOwnerProcess();
            if (process != null && TheGame.Core.OS.CrashHandler.IsAppException(ex, process)) {
                TheGame.Core.OS.CrashHandler.HandleAppException(process, ex);
            } else {
                throw;
            }
        }
        base.OnClick();
    }

    protected override void OnHover() {
        CustomCursor.Instance.SetCursor(CursorType.Link);
        base.OnHover();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        // Scale logic
        Vector2 size = Size * Scale;
        Vector2 offset = (Size - size) / 2f;
        Vector2 drawPos = absPos + offset;

        // Background
        batch.FillRectangle(drawPos, size, CurrentBackgroundColor * AbsoluteOpacity, rounded: 3f);

        // Border
        batch.BorderRectangle(drawPos, size, BorderColor * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        float pX = Padding.X * Scale;
        float pY = Padding.Y * Scale;
        float iconSize = 0f;

        // Draw Icon if present
        if (Icon != null) {
            iconSize = size.Y - (pY * 2);
            var iconPos = new Vector2(drawPos.X + pX, drawPos.Y + pY);
            float scale = iconSize / Icon.Width;
            batch.DrawTexture(Icon, iconPos, Color.White * AbsoluteOpacity, scale);
        }

        // Text (Centering logic with truncation)
        if (!string.IsNullOrEmpty(Text) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont((int)(FontSize * Scale));
            if (font != null) {
                float contentStartX = drawPos.X + pX + iconSize + (iconSize > 0 ? pX : 0);
                float remainingWidth = size.X - (contentStartX - drawPos.X) - pX;

                if (remainingWidth > 5) {
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
