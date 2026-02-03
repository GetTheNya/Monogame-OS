using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Graphics;

namespace TheGame.Core.UI;

/// <summary>
/// A window without any chrome (title bar, buttons, borders).
/// Perfect for transparent overlays, custom UI designs, or desktop widgets.
/// </summary>
public class BorderlessWindow : WindowBase {
    public Color BackgroundColor { get; set; } = Color.Transparent;

    public BorderlessWindow() : base(Vector2.Zero, new Vector2(400, 300)) { }

    public BorderlessWindow(Vector2 position, Vector2 size) : base(position, size) { }

    public override UIElement GetElementAt(Vector2 pos) {
        if (!IsVisible || !Bounds.Contains(pos)) return null;

        for (int i = Children.Count - 1; i >= 0; i--) {
            var found = Children[i].GetElementAt(pos);
            if (found != null) return found;
        }

        return ConsumesInput ? this : null;
    }

    public override Vector2 GetChildOffset(UIElement child) {
        return Vector2.Zero;
    }

    protected override void DrawWindowToRT(SpriteBatch spriteBatch, ShapeBatch globalBatch) {
        globalBatch.End();
        spriteBatch.End();

        var gd = G.GraphicsDevice;
        var screenAbsPos = RawAbsolutePosition;
        var windowW = (int)Size.X;
        var windowH = (int)Size.Y;

        if (windowW <= 0 || windowH <= 0) {
            globalBatch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            return;
        }

        EnsureRenderTarget(gd, windowW, windowH);
        var previousTargets = gd.GetRenderTargets();
        RenderTarget2D previousTarget = previousTargets.Length > 0 ? previousTargets[0].RenderTarget as RenderTarget2D : null;
        var previousViewport = gd.Viewport;

        gd.SetRenderTarget(_windowRenderTarget);
        gd.Viewport = new Viewport(0, 0, _windowRenderTarget.Width, _windowRenderTarget.Height);
        gd.Clear(Color.Transparent);

        UIElement.RenderOffset = screenAbsPos;
        _contentBatch.BlurUVOffset = screenAbsPos;
        _contentBatch.ScreenSizeOverride = new Vector2(previousViewport.Width, previousViewport.Height);

        try {
            _contentBatch.Begin(null, null);
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            // Draw Background
            if (BackgroundColor.A > 0) {
                _contentBatch.FillRectangle(AbsolutePosition, Size, BackgroundColor * AbsoluteOpacity);
            }

            // Custom draw
            OnDraw(spriteBatch, _contentBatch);

            // Draw children
            foreach (var child in Children) {
                child.Draw(spriteBatch, _contentBatch);
            }

            _contentBatch.End();
            spriteBatch.End();
        } finally {
            UIElement.RenderOffset = Vector2.Zero;
            _contentBatch.BlurUVOffset = Vector2.Zero;
            _contentBatch.ScreenSizeOverride = null;
        }

        gd.SetRenderTarget(previousTarget);
        gd.Viewport = previousViewport;

        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);
        spriteBatch.Draw(_windowRenderTarget, screenAbsPos, Color.White * AbsoluteOpacity);
        spriteBatch.End();

        globalBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }
}
