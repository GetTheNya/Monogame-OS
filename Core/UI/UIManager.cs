using Microsoft.Xna.Framework;
using TheGame.Graphics;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace TheGame.Core.UI;

public class UIManager {
    private UIElement _root;
    private TooltipManager _tooltipManager;

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
            // otherwise SpriteBatch and ShapeBatch passes will overlap across different layers (e.g. icons on top of windows)
            foreach (var layer in _root.Children) {
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

    // Invisible root container
    private class RootElement : UIElement {
        protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
            // Do nothing
        }
        
        protected override void UpdateInput() {
            // Root element covers screen but should not consume input.
            // base.UpdateInput() would consume if clicked.
        }
    }
}
