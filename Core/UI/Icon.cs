using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;

namespace TheGame.Core.UI;

public class Icon : UIElement, IDisposable {
    public Texture2D Texture { get; set; }
    public Rectangle? SourceRect { get; set; }
    public Color Tint { get; set; } = Color.White;

    /// <summary>
    /// If true, draws a magenta box when Texture is null.
    /// </summary>
    public bool ShowPlaceholder { get; set; } = true;

    /// <summary>
    /// If true, maintains the texture's aspect ratio within the Size bounds.
    /// Otherwise stretches to fill.
    /// </summary>
    public bool KeepAspectRatio { get; set; } = true;

    public Icon(Vector2 position, Vector2 size, Texture2D texture = null) {
        Position = position;
        Size = size;
        Texture = texture;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        if (Texture != null) {
            if (KeepAspectRatio) {
                float scale = Math.Min(Size.X / Texture.Width, Size.Y / Texture.Height);
                float drawW = Texture.Width * scale;
                float drawH = Texture.Height * scale;
                Vector2 drawPos = absPos + new Vector2((Size.X - drawW) / 2, (Size.Y - drawH) / 2);
                batch.DrawTexture(Texture, drawPos, Tint * AbsoluteOpacity, scale);
            } else {
                // ShapeBatch doesn't have a direct (xy, size) overload, 
                // but we can calculate scale OR use spriteBatch since it's already Begin'd
                spriteBatch.Draw(Texture, new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y), Tint * AbsoluteOpacity);
            }
        } else if (ShowPlaceholder) {
            // Placeholder
            batch.FillRectangle(absPos, Size, Color.Magenta); // Missing texture color
            batch.BorderRectangle(absPos, Size, Color.Black, 1f);
        }
    }

    public void Dispose() {
        Texture?.Dispose();
    }
}
