using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.Input;
using TheGame.Core.UI;
using TheGame.Core.Animation;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame;
using FontStashSharp;

namespace ScreenCapture;

public class CapturePreview : UIElement {
    private readonly Texture2D _texture;
    private readonly string _virtualPath;
    private readonly DynamicSpriteFont _font;
    private float _timer = 5f;
    private bool _isClosing = false;
    private float _opacity = 0f;
    private Tween _fadeTween;

    public event Action OnClosed;

    public CapturePreview(Texture2D texture, string virtualPath) {
        _texture = texture;
        _virtualPath = virtualPath;
        _opacity = 0f;

        _font = GameContent.FontSystem.GetFont(16);
        
        // Calculate size (max 250px width, preserving aspect ratio)
        float maxWidth = 250f;
        float scale = Math.Min(1f, maxWidth / _texture.Width);
        Size = new Vector2(_texture.Width * scale, _texture.Height * scale);
        
        // Initial animation (local tween)
        _fadeTween = new Tween(this, f => _opacity = f, 0f, 1f, 0.3f, Easing.EaseOutQuad);
        
        // Register as overlay to get input
        Shell.AddOverlayElement(this);
    }

    public override void Update(GameTime gameTime) {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Update local tween
        _fadeTween?.Update(dt);

        if (!_isClosing) {
            // Manual hover detection (bypasses static UIManager state which might be different in this context)
            // We check !InputManager.IsMouseConsumed to respect Z-order (top-most elements update first)
            IsMouseOver = Bounds.Contains(InputManager.MousePosition) && !InputManager.IsMouseConsumed;

            // Hover logic
            if (IsMouseOver) {
                _timer = 5f; // Reset timer
                InputManager.IsMouseConsumed = true; // Consume hover
                
                if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                    OnClick();
                }
            } else {
                _timer -= dt;
                if (_timer <= 0) {
                    StartClosing();
                }
            }
        }

        // We don't call base.Update because it calls UpdateInput which uses UIManager.IsHovered
        // But we still need to update children if there were any (there aren't)
    }

    public void Close() => StartClosing();

    private void StartClosing() {
        if (_isClosing) return;
        _isClosing = true;

        // Animate out (local tween)
        _fadeTween = new Tween(this, f => _opacity = f, _opacity, 0f, 0.2f, Easing.EaseInQuad);
        _fadeTween.OnCompleteAction(() => {
            Shell.RemoveOverlayElement(this);
            OnClosed?.Invoke();
        });
    }

    protected override void OnClick() {
        // Open in image viewer
        Shell.Execute(_virtualPath);

        // Hide immediately
        StartClosing();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (_texture == null || _texture.IsDisposed) return;

        Vector2 pos = AbsolutePosition;
        Rectangle destRect = new Rectangle((int)pos.X, (int)pos.Y, (int)Size.X, (int)Size.Y);

        // Draw background/shadow
        shapeBatch.FillRectangle(new Vector2(pos.X - 2, pos.Y - 2), Size + new Vector2(4, 4), new Color(0, 0, 0, (int)(100 * _opacity)));
        
        // Draw image
        spriteBatch.Draw(_texture, destRect, Color.White * _opacity);

        //If mouse over preview draw path
        if (IsMouseOver && _font != null) {
            var truncatedText = TextHelper.TruncateWithEllipsis(_font, _virtualPath, Size.X);
            _font.DrawText(spriteBatch, truncatedText, AbsolutePosition, Color.White * _opacity);
        }
        
        // Draw border
        shapeBatch.BorderRectangle(pos, Size, Color.White * 0.5f * _opacity, 1f);
    }

    public void Dispose() {
        _texture?.Dispose();
    }
}
