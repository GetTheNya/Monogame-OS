using Microsoft.Xna.Framework;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;

namespace TheGame.Core.UI;

public class UIManager {
    private UIElement _root;
    private TooltipManager _tooltipManager;

    public static UIElement FocusedElement { get; private set; }
    public static UIElement HoveredElement { get; private set; }
    
    public static bool IsHovered(UIElement element) {
        if (element == null) return false;
        var current = HoveredElement;
        while (current != null) {
            if (current == element) return true;
            current = current.Parent;
        }
        return false;
    }
    
    public static void SetFocus(UIElement element) {
        if (FocusedElement == element) return;
        
        if (FocusedElement != null) FocusedElement.IsFocused = false;
        FocusedElement = element;
        if (FocusedElement != null) FocusedElement.IsFocused = true;
    }

    public UIManager() {
        // A root container that covers the whole screen
        _root = new RootElement();
        _tooltipManager = new TooltipManager();
    }

    public void AddElement(UIElement element) {
        // Simple add, no robust Z-order management here yet (handled by drawing order)
        _root.AddChild(element);
    }
    
    public void RemoveElement(UIElement element) {
        _root.RemoveChild(element);
    }

    public void Update(GameTime gameTime) {
        try {
            _root.Size = new Vector2(G.GraphicsDevice.Viewport.Width, G.GraphicsDevice.Viewport.Height);

            // Update global hover state using the recursive hit-test logic
            HoveredElement = _root.GetElementAt(TheGame.Core.Input.InputManager.MousePosition.ToVector2());

            // Handle focus on click
            if (TheGame.Core.Input.InputManager.IsAnyMouseButtonJustPressed(TheGame.Core.Input.MouseButton.Left)) {
                UIElement clicked = FindElementAtPosition(_root, TheGame.Core.Input.InputManager.MousePosition.ToVector2());
                
                // Traverse up to find the actual element that handles input (for focus logic)
                UIElement effectiveElement = clicked;
                while (effectiveElement != null && !effectiveElement.ConsumesInput) {
                    effectiveElement = effectiveElement.Parent;
                }

                if (effectiveElement != null) {
                    // We hit something that consumes input.
                    // If the deepest clicked element is focusable, focus it.
                    // Otherwise, if any parent is focusable, maybe focus it? 
                    // Usually we just want to focus the specific control.
                    if (clicked != null && clicked.CanFocus) {
                        SetFocus(clicked);
                    }
                    // Else: We hit a non-focusable consuming element (like a panel background).
                    // We PRESERVE focus instead of clearing it.
                } else {
                    // Clicked on raw background or non-consuming area
                    SetFocus(null);
                }
            }

            _root.Update(gameTime);
            _tooltipManager.Update(gameTime, _root);
        } catch (Exception ex) {
            if (!TheGame.Core.OS.CrashHandler.TryHandleAnyAppException(ex)) {
                throw;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        try {
            // We iterate through root children and flush between them to ensure strict layering
            // Use ToList() to prevent "Collection was modified" if a child is added/removed during Draw
            var layers = _root.Children;
            foreach (var layer in layers) {
                if (!layer.IsVisible) continue;
                
                layer.Draw(spriteBatch, shapeBatch);
                
                // Flush batches to "bake in" the layer before starting the next one
                shapeBatch.End();
                spriteBatch.End();
                
                shapeBatch.Begin();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            }

            // Draw tooltips last (always on top)
            _tooltipManager.Draw(spriteBatch, shapeBatch);
        } catch (Exception ex) {
            if (!TheGame.Core.OS.CrashHandler.TryHandleAnyAppException(ex)) {
                throw;
            }
        }
    }

    private UIElement FindElementAtPosition(UIElement parent, Vector2 pos) {
        return parent.GetElementAt(pos);
    }

    // Invisible root container
    private class RootElement : UIElement {
        protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
            // Do nothing
        }
        
        protected override void UpdateInput() {
            // Root element covers screen but should not consume input.
            ConsumesInput = false;
        }
    }
}
