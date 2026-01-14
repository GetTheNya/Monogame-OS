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
            // We need to use SpriteBatch for textures.
            // Assumption: SpriteBatch.Begin() has been called by Root or Scene.
            // DesktopScene calls _uiManager.Draw, which calls Root.Draw...
            // But we don't have a global Begin() for SpriteBatch yet in DesktopScene/UIManager!
            // Wait, standard MonoGame SpriteBatch needs Begin() before Draw().
            // DesktopScene has `_uiManager.Draw(spriteBatch, shapeBatch)`.
            // Does it Begin/End?
            // UIManager.Draw calls `_root.Draw`.
            // The method signature assumes passed batches are ready? 
            // ShapeBatch handles Begin/End internally often? Or we should manage it.
            // ShapeBatch in TheGame usually requires Begin/End.
            // In DesktopScene:
            /*
                shapeBatch.Begin();
                ...
                _uiManager.Draw(spriteBatch, shapeBatch);
                ...
                shapeBatch.End();
            */
            // But SpriteBatch is currently unused in DesktopScene logic until now.
            // Check DesktopScene.Draw again.
            
            spriteBatch.Draw(Texture, destRect, SourceRect, Tint);
        } else {
            // Placeholder
            batch.FillRectangle(absPos, Size, Color.Magenta); // Missing texture color
            batch.BorderRectangle(absPos, Size, Color.Black, 1f);
        }
    }
}
