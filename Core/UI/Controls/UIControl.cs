using System;
using Microsoft.Xna.Framework;
using TheGame.Core.Animation;
using TheGame.Core.Designer;

namespace TheGame.Core.UI.Controls;

public enum ControlState {
    Normal,
    Hovered,
    Pressed,
    Disabled
}

public abstract class UIControl : UIElement {
    public ControlState ControlState {
        get {
            if (!IsActive || !IsEnabled) return ControlState.Disabled;
            if (_isPressed) return ControlState.Pressed;
            if (IsMouseOver) return ControlState.Hovered;
            return ControlState.Normal;
        }
    }

    // Default styling colors that many controls use
    public Color AccentColor { get; set; } = new Color(0, 120, 215);
    public Color BackgroundColor { get; set; } = new Color(40, 40, 40);
    public Color HoverColor { get; set; } = new Color(60, 60, 60);
    public Color PressedColor { get; set; } = new Color(30, 30, 30);
    public Color BorderColor { get; set; } = Color.Gray * 0.5f;

    // Animated State
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public Color CurrentBackgroundColor { get; protected set; }
    [DesignerIgnoreProperty] [DesignerIgnoreJsonSerialization]
    public float Scale { get; private set; } = 0.98f;
    public bool EnableScaleAnimation { get; set; } = true;
    private ControlState _lastState = ControlState.Normal;

    protected UIControl(Vector2 position, Vector2 size) : base(position, size) {
        CurrentBackgroundColor = BackgroundColor;
        if (!EnableScaleAnimation) Scale = 1.0f;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Animate Color
        Color targetColor = BackgroundColor;
        if (ControlState == ControlState.Pressed) targetColor = PressedColor;
        else if (ControlState == ControlState.Hovered) targetColor = HoverColor;
        else if (ControlState == ControlState.Disabled) targetColor = BackgroundColor * 0.5f;

        CurrentBackgroundColor = Color.Lerp(CurrentBackgroundColor, targetColor, MathHelper.Clamp(dt * 15f, 0, 1));

        // Animate Scale
        float targetScale = 0.98f;
        if (EnableScaleAnimation) {
            targetScale = ControlState == ControlState.Hovered ? 1.0f : 0.98f;
            if (ControlState == ControlState.Pressed) targetScale = 0.96f;
        } else {
            targetScale = 1.0f;
        }
        
        Scale = MathHelper.Lerp(Scale, targetScale, MathHelper.Clamp(dt * 15f, 0, 1));
        
        if (ControlState != _lastState) {
            OnStateChanged(ControlState);
            _lastState = ControlState;
        }
    }

    protected virtual void OnStateChanged(ControlState state) { }
}