using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class LoadingSpinner : UIElement {
    public Color Color { get; set; } = new Color(0, 120, 215);
    public float Thickness { get; set; } = 3f;
    public float Speed { get; set; } = 1f;
    
    private float _rotation = 0f;
    private float _timer = 0f;
    private float _currentArcSize = 45f;

    [Obsolete("For Designer/Serialization use only")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public LoadingSpinner() : this(Vector2.Zero, Vector2.Zero) {}

    public LoadingSpinner(Vector2 position, Vector2 size) : base(position, size) {
        ConsumesInput = false;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (!IsActive) return;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _timer += dt * Speed;

        // Linear rotation
        _rotation += dt * Speed * 5f; // Base rotation speed
        if (_rotation > MathHelper.TwoPi) _rotation -= MathHelper.TwoPi;

        // Dynamic arc size oscillation (between ~45 and ~270 degrees)
        // We use a sine wave to oscillate the size
        float oscillation = (float)Math.Sin(_timer * 2f); // Frequency of size change
        _currentArcSize = 157.5f + oscillation * 112.5f; // Range: [45, 270]
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        Vector2 center = AbsolutePosition + Size / 2f;
        float radius = Math.Min(Size.X, Size.Y) / 2f - Thickness;
        
        if (radius <= 0) return;

        float opacity = AbsoluteOpacity;
        
        // Convert degrees to radians for DrawArc
        float startAngle = _rotation;
        float endAngle = _rotation + MathHelper.ToRadians(_currentArcSize);

        batch.BorderArc(center, radius, startAngle, endAngle, Color * opacity, Thickness);
    }
}
