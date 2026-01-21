using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using System;

namespace TheGame.Core.UI.Controls;

public class LevelMeter : UIControl {
    private float _level = 0f;
    private float _peak = 0f;
    private float _visualLevel = 0f;
    private float _visualPeak = 0f;
    
    public float Level { 
        get => _level; 
        set => _level = MathHelper.Clamp(value, 0, 1); 
    }
    
    public float Peak { 
        get => _peak; 
        set => _peak = MathHelper.Clamp(value, 0, 1); 
    }

    public LevelMeter(Vector2 position, Vector2 size) : base(position, size) {
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Visual smoothing
        // Attack is instant (or very fast)
        if (_level > _visualLevel) {
            _visualLevel = _level;
        } else {
            // Smooth decay: approx 15% towards target per frame
            _visualLevel = MathHelper.Lerp(_visualLevel, _level, 10f * dt);
        }

        if (_peak > _visualPeak) {
            _visualPeak = _peak;
        } else {
            _visualPeak = MathHelper.Lerp(_visualPeak, _peak, 8f * dt);
        }
    }

    public override void Draw(SpriteBatch sb, ShapeBatch sbatch) {
        if (!IsVisible) return;

        float opacity = AbsoluteOpacity;
        if (opacity < 0.001f) return;

        Vector2 absPos = AbsolutePosition;
        
        // Background
        sbatch.FillRectangle(absPos, Size, new Color(30, 30, 30) * opacity);

        // Level bar with segmented look or gradient
        if (_visualLevel > 0.001f) {
            float levelWidth = Size.X * _visualLevel;
            
            // Draw in segments or simple gradient
            Color lowColor = new Color(0, 200, 0) * opacity;
            Color midColor = new Color(200, 200, 0) * opacity;
            Color highColor = new Color(200, 0, 0) * opacity;

            if (_visualLevel < 0.7f) {
                sbatch.FillRectangle(absPos, new Vector2(levelWidth, Size.Y), lowColor);
            } else if (_visualLevel < 0.9f) {
                sbatch.FillRectangle(absPos, new Vector2(Size.X * 0.7f, Size.Y), lowColor);
                sbatch.FillRectangle(absPos + new Vector2(Size.X * 0.7f, 0), new Vector2(levelWidth - Size.X * 0.7f, Size.Y), midColor);
            } else {
                sbatch.FillRectangle(absPos, new Vector2(Size.X * 0.7f, Size.Y), lowColor);
                sbatch.FillRectangle(absPos + new Vector2(Size.X * 0.7f, 0), new Vector2(Size.X * 0.2f, Size.Y), midColor);
                sbatch.FillRectangle(absPos + new Vector2(Size.X * 0.9f, 0), new Vector2(levelWidth - Size.X * 0.9f, Size.Y), highColor);
            }
        }

        // Peak line
        float peakX = Math.Min(Size.X - 2, Size.X * _visualPeak);
        sbatch.FillRectangle(absPos + new Vector2(peakX, 0), new Vector2(2, Size.Y), Color.White * 0.8f * opacity);
        
        // Border
        sbatch.BorderRectangle(absPos, Size, Color.White * 0.1f * opacity, 1f);
    }
}
