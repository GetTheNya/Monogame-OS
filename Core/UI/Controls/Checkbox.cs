using System;
using System.ComponentModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class Checkbox : ValueControl<bool> {
    public string Label { get; set; }
    public Color TextColor { get; set; } = Color.White;

    [Obsolete("For Designer/Serialization use only", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Checkbox() : this(Vector2.Zero, "Checkbox") { }

    public Checkbox(Vector2 position, string label = "") : base(position, new Vector2(20, 20)) {
        Label = label;
    }

    private float _checkAlpha = 0f;

    public override void Update(GameTime gameTime) {
        // Measure text to expand hit box BEFORE UpdateInput (which is in base.Update)
        if (GameContent.FontSystem != null && !string.IsNullOrEmpty(Label)) {
            var font = GameContent.FontSystem.GetFont(20);
            var size = font.MeasureString(Label);
            Size = new Vector2(28 + size.X, 20);
        }

        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        float target = Value ? 1f : 0f;
        _checkAlpha = MathHelper.Lerp(_checkAlpha, target, MathHelper.Clamp(dt * 15f, 0, 1));
    }

    protected override void OnClick() {
        Value = !Value;
        base.OnClick();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;

        // Draw Box
        batch.FillRectangle(absPos, new Vector2(20, 20), CurrentBackgroundColor * AbsoluteOpacity, rounded: 3f);
        batch.BorderRectangle(absPos, new Vector2(20, 20), BorderColor * AbsoluteOpacity, thickness: 1f, rounded: 3f);

        // Draw Checkmark
        if (_checkAlpha > 0.01f) {
            float size = 12f * _checkAlpha;
            float offset = (12f - size) / 2f;
            batch.FillRectangle(absPos + new Vector2(4 + offset, 4 + offset), new Vector2(size, size), AccentColor * (AbsoluteOpacity * _checkAlpha), rounded: 2f);
        }

        // Draw Label
        if (!string.IsNullOrEmpty(Label) && GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(20);
            float textY = (Size.Y - font.LineHeight) / 2f;
            font.DrawText(batch, Label, absPos + new Vector2(28, textY), TextColor * AbsoluteOpacity);
        }
    }
}
