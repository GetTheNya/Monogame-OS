using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.UI;
using System.ComponentModel;

namespace TheGame.Core.UI.Controls;

public class ProgressBar : ValueControl<float> {
    public float MinValue { get; set; } = 0f;
    public float MaxValue { get; set; } = 1f;
    public Color ProgressColor { get; set; } = new Color(0, 200, 0);
    public string TextFormat { get; set; } = "{0}%"; // {0} = percentage, {1} = value, {2} = max
    public float FillPadding { get; set; } = 2f;
    public bool EnableAnimations { get; set; } = true;
    public Color TextColor { get; set; } = Color.White;
    public int FontSize { get; set; } = 14;

    private float _visualValue = 0f;
    private float _shimmerTimer = 0f;
    private float _shimmerOffset = 0f;

    [Obsolete("For Designer/Serialization use only", error: true)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ProgressBar() : this(Vector2.Zero, Vector2.Zero) { }

    public ProgressBar(Vector2 position, Vector2 size, float value = 0f) : base(position, size, value) {
        _visualValue = value;
        ConsumesInput = false;
        EnableScaleAnimation = false;
        HoverColor = BackgroundColor;
        PressedColor = BackgroundColor;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (EnableAnimations) {
            // Smooth value transition
            _visualValue = MathHelper.Lerp(_visualValue, Value, MathHelper.Clamp(dt * 10f, 0, 1));
            
            // Shimmer effect animation (Smoother non-linear cycle)
            // Disable shimmer if at 100%
            if (_visualValue < MaxValue - 0.001f) {
                _shimmerTimer += dt;
                
                float cycleTime = 2.5f; // Slower cycle
                float sweepDuration = 1.5f;
                float t = _shimmerTimer % cycleTime;
                
                if (t < sweepDuration) {
                    float normalizedT = t / sweepDuration;
                    // Perlin Smoothstep: 6t^5 - 15t^4 + 10t^3
                    float smoothedT = normalizedT * normalizedT * normalizedT * (normalizedT * (6 * normalizedT - 15) + 10);
                    _shimmerOffset = -0.6f + smoothedT * 2.4f;
                } else {
                    _shimmerOffset = -0.6f; 
                }
            } else {
                _shimmerOffset = -0.6f;
                // We keep _shimmerTimer as is, or reset it. 
                // Resetting it ensures that when we resume, it starts from a pause or a fresh sweep.
                _shimmerTimer = 0f; 
            }
        } else {
            _visualValue = Value;
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        Vector2 size = Size * Scale;
        Vector2 offset = (Size - size) / 2f;
        Vector2 drawPos = absPos + offset;

        float opacity = AbsoluteOpacity;

        // Background / Frame
        batch.FillRectangle(drawPos, size, BackgroundColor * opacity, rounded: 4f);
        batch.BorderRectangle(drawPos, size, BorderColor * opacity, thickness: 1f, rounded: 4f);

        // Calculate progress percentage
        float range = MaxValue - MinValue;
        float percent = range > 0 ? (MathHelper.Clamp(_visualValue, MinValue, MaxValue) - MinValue) / range : 0;

        // Fill Progress
        if (percent > 0) {
            float p = FillPadding * Scale;
            Vector2 fillSize = new Vector2((size.X - p * 2) * percent, size.Y - p * 2);
            Vector2 fillPos = drawPos + new Vector2(p, p);
            
            if (fillSize.X > 1) {
                // Main progress fill
                batch.FillRectangle(fillPos, fillSize, ProgressColor * opacity, rounded: 2f);

                // Shimmer Effect Overlay (Broad Smoother Gradient)
                // Only draw shimmer if not at 100%
                if (EnableAnimations && _visualValue < MaxValue - 0.001f) {
                    float shimmerWidth = size.X * 1.2f; // Wider shimmer
                    float shimmerCenter = drawPos.X + (size.X * _shimmerOffset);
                    
                    // Draw a soft 'glow' by using a few segments with lower alpha towards the edges
                    int segments = 10;
                    for (int i = 0; i < segments; i++) {
                        float segmentT = i / (float)(segments - 1);
                        // Alpha follows a sine curve: 0 at edges, max at center
                        float segmentAlpha = (float)Math.Sin(segmentT * Math.PI) * 0.35f;
                        
                        float segmentWidth = shimmerWidth / segments;
                        float segmentX = shimmerCenter - (shimmerWidth / 2f) + (i * segmentWidth);
                        
                        float startX = Math.Max(fillPos.X, segmentX);
                        float endX = Math.Min(fillPos.X + fillSize.X, segmentX + segmentWidth - 1f); // 1px gap

                        if (endX > startX) {
                            batch.FillRectangle(new Vector2(startX, fillPos.Y), new Vector2(endX - startX, fillSize.Y), Color.White * segmentAlpha * opacity);
                        }
                    }
                }
            }
        }

        // Text Overlay
        if (!string.IsNullOrEmpty(TextFormat) && GameContent.FontSystem != null) {
            float displayPercent = range > 0 ? (MathHelper.Clamp(Value, MinValue, MaxValue) - MinValue) / range : 0;
            string text = string.Format(TextFormat, (int)(displayPercent * 100), Value, MaxValue);
            
            var font = GameContent.FontSystem.GetFont((int)(FontSize * Scale));
            if (font != null) {
                var textSize = font.MeasureString(text);
                Vector2 textPos = drawPos + (size - textSize) / 2f;
                font.DrawText(batch, text, textPos, TextColor * opacity);
            }
        }
    }
}
