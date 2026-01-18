using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using System.Linq;

namespace TheGame.Core.UI;

public class TooltipManager {
    private Tooltip _tooltip;
    private UIElement _hoveredElement;
    private float _hoverTimer = 0f;
    private bool _isShowing = false;

    public TooltipManager() {
        _tooltip = new Tooltip();
    }

    public void Update(GameTime gameTime, UIElement root) {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        Vector2 mousePos = InputManager.MousePosition.ToVector2();

        // Find the top-most element with a tooltip
        UIElement topTooltipElement = FindTopTooltipElement(root);

        if (topTooltipElement != null) {
            if (topTooltipElement != _hoveredElement) {
                // Hovered a DIFFERENT element
                if (_isShowing) {
                    _tooltip.AnimateOut(null);
                    _isShowing = false;
                }
                _hoveredElement = topTooltipElement;
                _hoverTimer = 0f;
            } else {
                // Hovering the SAME element
                if (!_isShowing) {
                    _hoverTimer += dt;
                    if (_hoverTimer >= _hoveredElement.TooltipDelay) {
                        ShowTooltip(mousePos);
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

    private UIElement FindTopTooltipElement(UIElement parent) {
        if (!parent.IsVisible || !parent.IsActive) return null;

        // Traverse children in reverse (top to bottom)
        for (int i = parent.Children.Count - 1; i >= 0; i--) {
            var child = parent.Children[i];
            var result = FindTopTooltipElement(child);
            if (result != null) return result;
        }

        // Check if this parent itself is hovered and has a tooltip
        if (!string.IsNullOrEmpty(parent.Tooltip) && parent.Bounds.Contains(InputManager.MousePosition)) {
            // Note: We only check self if none of the children matched.
            // This ensures we get the most specific element.
            return parent;
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
