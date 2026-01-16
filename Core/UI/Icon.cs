using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;

namespace TheGame.Core.UI;

public class Icon : UIElement {
    public Texture2D Texture { get; set; }
    public Rectangle? SourceRect { get; set; }
    public Color Tint { get; set; } = Color.White;

    public Icon(Vector2 position, Vector2 size, Texture2D texture = null) {
        Position = position;
        Size = size; // Default size if not provided?
        Texture = texture;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        var destRect = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);

        if (Texture != null) {
            float scaleX = Size.X / Texture.Width;
            float scaleY = Size.Y / Texture.Height;
            // DrawTexture usually takes a single scale. If non-uniform scaling is needed, we'd need a different ShapeBatch method.
            // But usually UI icons are square/uniform.
            batch.DrawTexture(Texture, absPos, Tint * AbsoluteOpacity, scaleX);
        } else {
            // Placeholder
            batch.FillRectangle(absPos, Size, Color.Magenta); // Missing texture color
            batch.BorderRectangle(absPos, Size, Color.Black, 1f);
        }
    }
}
