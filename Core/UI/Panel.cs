using Microsoft.Xna.Framework;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.UI;

public class Panel : UIElement {
    public Color BackgroundColor { get; set; } = new Color(40, 40, 40);
    public Color BorderColor { get; set; } = Color.Gray;
    public float BorderThickness { get; set; } = 1f;

    public Panel(Vector2 position, Vector2 size) {
        Position = position;
        Size = size;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        
        if (BackgroundColor != Color.Transparent)
            batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity);
            
        if (BorderColor != Color.Transparent && BorderThickness > 0)
            batch.BorderRectangle(absPos, Size, BorderColor * AbsoluteOpacity, thickness: BorderThickness);
    }
}
