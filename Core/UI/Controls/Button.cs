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

    public Button(Vector2 position, Vector2 size, string text = "") : base(position, size) {
        Text = text;
    }

    protected override void OnClick() {
        DebugLogger.Log($"Button Clicked: {Text}");
        TheGame.Core.OS.Shell.PlaySound("C:\\Windows\\Media\\click.wav", 0.5f);
        OnClickAction?.Invoke();
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

        float padding = 5f * Scale;
        float iconSize = 0f;

        // Draw Icon if present
        if (Icon != null) {
            iconSize = size.Y - (padding * 2);
            var iconRect = new Rectangle((int)(drawPos.X + padding), (int)(drawPos.Y + padding), (int)iconSize, (int)iconSize);
            spriteBatch.Draw(Icon, iconRect, Color.White * AbsoluteOpacity);
        }

        // Text (Centering logic with truncation)
        if (!string.IsNullOrEmpty(Text) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont((int)(20 * Scale));
            if (font != null) {
                float contentStartX = drawPos.X + padding + iconSize + (iconSize > 0 ? padding : 0);
                float remainingWidth = size.X - (contentStartX - drawPos.X) - padding;

                if (remainingWidth > 5) {
                    string textToDraw = Text;
                    var textSize = font.MeasureString(textToDraw);

                    // Truncate if too long
                    if (textSize.X > remainingWidth) {
                        while (textToDraw.Length > 0 && font.MeasureString(textToDraw + "...").X > remainingWidth) {
                            textToDraw = textToDraw.Substring(0, textToDraw.Length - 1);
                        }

                        textToDraw += "...";
                        textSize = font.MeasureString(textToDraw);
                    }

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
