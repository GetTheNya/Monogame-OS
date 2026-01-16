using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class Label : UIElement {
    public string Text { get; set; }
    public Color Color { get; set; } = Color.White;
    public Color TextColor { get => Color; set => Color = value; }
    public int FontSize { get; set; } = 20;

    public Label(Vector2 position, string text) : base(position, Vector2.Zero) {
        Text = text;
        ConsumesInput = false;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update size based on text measurement
        if (!string.IsNullOrEmpty(Text) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(FontSize);
            if (font != null) {
                Size = font.MeasureString(Text);
            }
        } else {
            Size = Vector2.Zero;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible || string.IsNullOrEmpty(Text)) return;

        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(FontSize);
            if (font != null) {
                font.DrawText(batch, Text, AbsolutePosition, Color * AbsoluteOpacity);
            }
        }
    }
}
