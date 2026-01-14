using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// A panel that provides blurred background effects for child windows.
/// Each child element receives a fresh blur snapshot of the scene behind it.
/// </summary>
public class BlurredWindowLayerPanel : Panel {
    private RenderTarget2D _sceneTarget;
    private RenderTarget2D _blurTarget;
    private Effect _blurEffect;

    /// <summary>
    /// Blur intensity. Higher values = more blur. Default: 2.5
    /// </summary>
    public float BlurStrength { get; set; } = 2.5f;

    /// <summary>
    /// Resolution scale for blur target (0.0-1.0). Lower = faster but blockier. Default: 0.5
    /// </summary>
    public float ResolutionScale { get; set; } = 0.5f;

    public BlurredWindowLayerPanel(Vector2 pos, Vector2 size) : base(pos, size) {
        BackgroundColor = Color.Transparent;
        BorderThickness = 0;
        ConsumesInput = false;
        _blurEffect = G.ContentManager.Load<Effect>("Blur");
    }

    private void EnsureRenderTargets(GraphicsDevice gd) {
        var viewport = gd.Viewport;
        if (_sceneTarget == null || _sceneTarget.Width != viewport.Width || _sceneTarget.Height != viewport.Height) {
            _sceneTarget?.Dispose();
            _blurTarget?.Dispose();
            _sceneTarget = new RenderTarget2D(gd, viewport.Width, viewport.Height, false, SurfaceFormat.Color, DepthFormat.None);
            _blurTarget = new RenderTarget2D(gd, (int)(viewport.Width * ResolutionScale), (int)(viewport.Height * ResolutionScale), false, SurfaceFormat.Color, DepthFormat.None);
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;
        var gd = G.GraphicsDevice;
        EnsureRenderTargets(gd);

        foreach (var child in Children) {
            if (!child.IsVisible) continue;

            // End current batches to capture scene state
            batch.End();
            spriteBatch.End();

            // Get current render target (scene so far)
            var currentScene = gd.GetRenderTargets()[0].RenderTarget as Texture2D;
            if (currentScene != null) {
                // Render blurred version to blur target
                gd.SetRenderTarget(_blurTarget);
                gd.Clear(Color.Transparent);

                _blurEffect.Parameters["TextureSize"]?.SetValue(new Vector2(currentScene.Width, currentScene.Height));
                _blurEffect.Parameters["BlurStrength"]?.SetValue(BlurStrength);
                Matrix projection = Matrix.CreateOrthographicOffCenter(0, _blurTarget.Width, _blurTarget.Height, 0, 0, 1);
                _blurEffect.Parameters["MatrixTransform"]?.SetValue(projection);

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, _blurEffect);
                spriteBatch.Draw(currentScene, _blurTarget.Bounds, Color.White);
                spriteBatch.End();

                // Restore original render target
                gd.SetRenderTarget(currentScene as RenderTarget2D);
            }

            // Provide blurred background to ShapeBatch for glass effects
            batch.BlurredBackground = _blurTarget;

            // Resume batches and draw child
            batch.Begin();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            child.Draw(spriteBatch, batch);
        }

        // Final batch state restoration
        batch.End();
        spriteBatch.End();
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
    }
}
