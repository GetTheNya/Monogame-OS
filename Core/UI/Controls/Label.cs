using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Designer;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class Label : UIElement {
    public string Text { get; set; }
    public Color Color { get; set; } = Color.White;
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public Color TextColor { get => Color; set => Color = value; }
    public int FontSize { get; set; } = 20;
    public bool UseBoldFont { get; set; } = false;

    [Obsolete("For Designer/Serialization use only", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Label() : this(Vector2.Zero, "Label") { }

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

        var activeFontSystem = UseBoldFont ? GameContent.BoldFontSystem : GameContent.FontSystem;

        if (activeFontSystem != null) {
            var font = activeFontSystem.GetFont(FontSize);
    
            if (font != null) {
                font.DrawText(batch, Text, AbsolutePosition, Color * AbsoluteOpacity);
            }
        }
    }
}
