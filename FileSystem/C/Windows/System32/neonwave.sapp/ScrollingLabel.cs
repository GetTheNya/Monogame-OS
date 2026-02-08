using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core.UI;
using TheGame.Graphics;
using TheGame;

namespace NeonWave;

/// <summary>
/// A label that scrolls its text horizontally if it doesn't fit within the specified width.
/// </summary>
public class ScrollingLabel : UIElement {
    public string Text { get; set; }
    public Color Color { get; set; } = Color.White;
    public int FontSize { get; set; } = 20;
    public float MaxWidth { get; set; } = 200;
    public float ScrollSpeed { get; set; } = 50f;

    private float _scrollOffset = 0;
    private float _pauseTimer = 0;
    private bool _isPausing = true;

    public ScrollingLabel(Vector2 position, string text, float maxWidth) : base(position, new Vector2(maxWidth, 30)) {
        Text = text;
        MaxWidth = maxWidth;
        ConsumesInput = false;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (string.IsNullOrEmpty(Text) || GameContent.FontSystem == null) return;

        var font = GameContent.FontSystem.GetFont(FontSize);
        if (font == null) return;

        Vector2 size = font.MeasureString(Text);
        Size = new Vector2(MaxWidth, size.Y);

        if (size.X > MaxWidth) {
            if (_isPausing) {
                _pauseTimer += dt;
                if (_pauseTimer > 2.0f) {
                    _isPausing = false;
                    _pauseTimer = 0;
                }
            } else {
                _scrollOffset += dt * ScrollSpeed;
                if (_scrollOffset > size.X - MaxWidth + 40) { // Add some padding before reset
                    _isPausing = true;
                    _scrollOffset = 0;
                }
            }
        } else {
            _scrollOffset = 0;
            _isPausing = true;
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible || string.IsNullOrEmpty(Text) || GameContent.FontSystem == null) return;

        var font = GameContent.FontSystem.GetFont(FontSize);
        if (font == null) return;

        // Use scissor-like clipping if the framework supports it, 
        // or just rely on the Window's content RT clipping.
        // Since this is drawn inside a Window (WindowBase.DrawWindowToRT), 
        // it's already being drawn to an off-screen RT.
        
        // However, we want to clip specifically to MaxWidth.
        // We can use a simple offset and rely on the fact that we're in a RT.
        // But to be safe, we'll just draw.
        
        font.DrawText(batch, Text, AbsolutePosition - new Vector2(_scrollOffset, 0), Color * AbsoluteOpacity);
    }
}
