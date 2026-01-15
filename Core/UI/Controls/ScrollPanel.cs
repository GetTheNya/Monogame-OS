using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class ScrollPanel : Panel {
    public float ScrollY { get; set; } = 0f;
    private float _contentHeight = 0f;
    private float _targetScrollY = 0f;
    
    // Cached RasterizerStates to avoid per-frame allocations
    private static readonly RasterizerState _scissorRasterizer = new RasterizerState { ScissorTestEnable = true };
    private static readonly RasterizerState _noScissorRasterizer = new RasterizerState { ScissorTestEnable = false };

    public ScrollPanel(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = Color.Transparent;
        BorderThickness = 0;
        ConsumesInput = true;
    }

    public void UpdateContentHeight(float height) {
        _contentHeight = height;
        ClampScroll();
    }

    private void ClampScroll() {
        float maxScroll = MathF.Max(0, _contentHeight - Size.Y);
        if (_targetScrollY < -maxScroll) _targetScrollY = -maxScroll;
        if (_targetScrollY > 0) _targetScrollY = 0;
    }

    public override Vector2 GetChildOffset(UIElement child) {
        return new Vector2(0, (float)Math.Round(ScrollY));
    }

    public override void Update(GameTime gameTime) {
        var absPos = AbsolutePosition;
        var myBounds = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        
        if (myBounds.Contains(InputManager.MousePosition)) {
            float scrollDelta = InputManager.ScrollDelta;
            if (scrollDelta != 0) {
                // targetScrollY should move by a controlled amount.
                // ScrollDelta is usually 120 per notch.
                // We'll move by 60 pixels per notch for a more controlled feel.
                _targetScrollY += (scrollDelta / 120f) * 60f;
                ClampScroll();
            }
        }

        // Smoothly interpolate ScrollY to targetScrollY
        // We use a lower lerp factor (0.12) for a more premium "smooth" gliding feel.
        ScrollY = MathHelper.Lerp(ScrollY, _targetScrollY, 0.12f);
        if (Math.Abs(ScrollY - _targetScrollY) < 0.1f) ScrollY = _targetScrollY;

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (!IsVisible) return;

        // Flush previous draws
        shapeBatch.End();
        spriteBatch.End();

        // Enable scissor rect
        var oldRect = spriteBatch.GraphicsDevice.ScissorRectangle;
        var absPos = AbsolutePosition;
        
        // Calculate scissor rect in screen coordinates
        Rectangle scissor = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        
        // Intersect with parent scissor if necessary
        Rectangle finalScissor = Rectangle.Intersect(oldRect, scissor);
        
        // Ensure scissor rect is valid
        if (finalScissor.Width <= 0 || finalScissor.Height <= 0) {
            finalScissor = new Rectangle(0, 0, 0, 0);
        }
        
        spriteBatch.GraphicsDevice.ScissorRectangle = finalScissor;
        
        shapeBatch.Begin(); 
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, _scissorRasterizer);

        DrawSelf(spriteBatch, shapeBatch);

        if (finalScissor.Width > 0 && finalScissor.Height > 0) {
            foreach (var child in Children) {
                child.Draw(spriteBatch, shapeBatch);
            }
        }

        shapeBatch.End();
        spriteBatch.End();

        // Restore
        spriteBatch.GraphicsDevice.ScissorRectangle = oldRect;
        
        // Restart with the PARENT's scissor state if it was active
        bool parentHadScissor = oldRect != spriteBatch.GraphicsDevice.Viewport.Bounds;
        shapeBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, parentHadScissor ? _scissorRasterizer : _noScissorRasterizer);
    }
}

