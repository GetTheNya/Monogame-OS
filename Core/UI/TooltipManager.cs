using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using System.Linq;

namespace TheGame.Core.UI;

public class TooltipManager {
    private Tooltip _tooltip;
    private ITooltipTarget _hoveredElement;
    private float _hoverTimer = 0f;
    private bool _isShowing = false;

    public TooltipManager() {
        _tooltip = new Tooltip();
    }

    public void Update(GameTime gameTime, UIElement root) {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector2 mousePos = InputManager.MousePosition.ToVector2();

        // Find the top-most target with a tooltip
        ITooltipTarget topTooltipTarget = FindTopTooltipTarget(root);

        if (topTooltipTarget != null) {
            if (topTooltipTarget != _hoveredElement) {
                // Hovered a DIFFERENT target
                if (_isShowing) {
                    _tooltip.AnimateOut(null);
                    _isShowing = false;
                }
                _hoveredElement = topTooltipTarget;
                _hoverTimer = 0f;
            } else {
                // Hovering the SAME target
                if (!_isShowing) {
                    _hoverTimer += dt;
                    if (_hoverTimer >= _hoveredElement.TooltipDelay) {
                        ShowTooltip(mousePos);
                    }
                } else {
                    // Already showing - update text if it changed dynamically
                    _tooltip.SetText(_hoveredElement.Tooltip);
                    
                    // Also check if text disappeared while hovered
                    if (string.IsNullOrEmpty(_hoveredElement.Tooltip)) {
                        _tooltip.AnimateOut(null);
                        _isShowing = false;
                    }
                }
            }
        } else {
            // Hovering NOTHING
            if (_isShowing) {
                _tooltip.AnimateOut(null);
                _isShowing = false;
            }
            _hoveredElement = null;
            _hoverTimer = 0f;
        }
        
        if (_tooltip.IsVisible) {
            _tooltip.Update(gameTime);
        }
    }

    private ITooltipTarget FindTopTooltipTarget(UIElement parent) {
        if (!parent.IsVisible || !parent.IsActive) return null;

        // Traverse children in reverse (top to bottom)
        for (int i = parent.Children.Count - 1; i >= 0; i--) {
            var child = parent.Children[i];
            var result = FindTopTooltipTarget(child);
            if (result != null) return result;
        }

        // Check if this parent itself is hovered
        if (parent.Bounds.Contains(InputManager.MousePosition)) {
            // Check if it's a sub-element provider (e.g., SystemTray)
            if (parent is ITooltipSubElementProvider provider) {
                var subTarget = provider.FindTooltipSubElement(InputManager.MousePosition.ToVector2());
                if (subTarget != null && !string.IsNullOrEmpty(subTarget.Tooltip)) {
                    return subTarget;
                }
            }

            // Check if this parent itself has a tooltip
            if (!string.IsNullOrEmpty(parent.Tooltip)) {
                return parent;
            }
        }

        return null;
    }

    private void ShowTooltip(Vector2 mousePos) {
        if (_hoveredElement == null || string.IsNullOrEmpty(_hoveredElement.Tooltip)) return;

        _tooltip.SetText(_hoveredElement.Tooltip);
        UpdateTooltipPosition(mousePos);
        _tooltip.AnimateIn();
        _isShowing = true;
    }

    private void UpdateTooltipPosition(Vector2 mousePos) {
        // Offset from cursor
        Vector2 pos = mousePos + new Vector2(12, 12);

        // Screen clamping
        var viewport = G.GraphicsDevice.Viewport;
        if (pos.X + _tooltip.Size.X > viewport.Width) {
            pos.X = mousePos.X - _tooltip.Size.X - 4f;
        }
        if (pos.Y + _tooltip.Size.Y > viewport.Height) {
            pos.Y = mousePos.Y - _tooltip.Size.Y - 4f;
        }

        _tooltip.Position = pos;
    }

    public void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        _tooltip.Draw(spriteBatch, shapeBatch);
    }
}
