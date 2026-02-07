using Microsoft.Xna.Framework;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.ComponentModel;

namespace TheGame.Core.UI;

public class Panel : UIElement {
    public Color BackgroundColor { get; set; } = new Color(40, 40, 40);
    public Color BorderColor { get; set; } = Color.Gray;
    public float BorderThickness { get; set; } = 1f;
    public float CornerRadius { get; set; } = 0f;

    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Panel() : this(Vector2.Zero, Vector2.Zero) { }

    public Panel(Vector2 position, Vector2 size) {
        Position = position;
        Size = size;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        float opacity = AbsoluteOpacity;
        
        if (BackgroundColor != Color.Transparent)
            batch.FillRectangle(absPos, Size, BackgroundColor * opacity, rounded: CornerRadius);
            
        if (BorderColor != Color.Transparent && BorderThickness > 0)
            batch.BorderRectangle(absPos, Size, BorderColor * opacity, thickness: BorderThickness, rounded: CornerRadius);
    }
}
