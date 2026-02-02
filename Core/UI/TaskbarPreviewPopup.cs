using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Animation;
using TheGame.Core.OS;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

/// <summary>
/// A popup that shows window previews when hovering over taskbar buttons.
/// </summary>
public class TaskbarPreviewPopup : Panel {
    private Process _process;
    private const float PreviewMaxWidth = 200f;
    private const float PreviewMaxHeight = 150f;
    private const float Padding = 10f;
    private const float TitleHeight = 25f;
    
    private List<WindowPreviewItem> _previews = new();
    private bool _isPinned;
    public bool IsPinned => _isPinned;
    
    private Vector2 _lastButtonAbsPos;
    private float _lastButtonWidth;

    public TaskbarPreviewPopup() : base(Vector2.Zero, Vector2.Zero) {
        BackgroundColor = new Color(25, 25, 25, 240);
        BorderColor = new Color(60, 60, 60);
        BorderThickness = 1f;
        IsVisible = false;
        ConsumesInput = true;
    }

    public void Show(Process process, Vector2 buttonAbsolutePosition, float buttonWidth, bool pin = false) {
        if (process == null) return;
        
        // If we are already pinned, keep it pinned even if this call comes from a hover (pin = false)
        bool newPinState = _isPinned || pin;

        // If we are pinning, force a rebuild even if it's the same process
        // to ensure we switch from hover-mode to pin-mode correctly if needed
        // Also don't return if we are currently fading out (Opacity < 1)
        if (_process == process && IsVisible && _isPinned == newPinState && Opacity >= 1.0f) return;

        _process = process;
        _isPinned = newPinState;
        _lastButtonAbsPos = buttonAbsolutePosition;
        _lastButtonWidth = buttonWidth;
        
        Rebuild();

        // Position above the button
        float totalWidth = _previews.Count * (PreviewMaxWidth + Padding) + Padding;
        float totalHeight = PreviewMaxHeight + TitleHeight + Padding * 2;
        
        Size = new Vector2(totalWidth, totalHeight);
        
        // Calculate position relative to parent
        Vector2 parentAbs = Parent?.AbsolutePosition ?? Vector2.Zero;
        float relX = (buttonAbsolutePosition.X - parentAbs.X) + (buttonWidth - totalWidth) / 2f;
        float relY = (buttonAbsolutePosition.Y - parentAbs.Y) - totalHeight - 5;
        
        Position = new Vector2(relX, relY);
        
        // Clamp to screen (using absolute for clamping logic)
        var viewport = G.GraphicsDevice.Viewport;
        float absX = Position.X + parentAbs.X;
        
        if (absX < 5) Position = new Vector2(5 - parentAbs.X, Position.Y);
        if (absX + Size.X > viewport.Width - 5) Position = new Vector2(viewport.Width - Size.X - 5 - parentAbs.X, Position.Y);
 
        IsVisible = true;
        // If already visible, just update opacity if needed
        if (Opacity < 1.0f) {
            Tweener.CancelAll(this);
            Tweener.To(this, o => Opacity = o, Opacity, 1f, 0.2f, Easing.EaseOutQuad);
        }
    }
 
    public void Hide(bool force = false) {
        if (!IsVisible) return;
        if (_isPinned && !force) return;
        
        // If already fading out, don't restart unless forcing
        if (!force && Tweener.IsAnimating(this) && Opacity < 1.0f) return;

        Tweener.CancelAll(this);
        Tweener.To(this, o => Opacity = o, Opacity, 0f, 0.15f, Easing.EaseInQuad).OnCompleteAction(() => {
            IsVisible = false;
            _process = null;
            _isPinned = false;
        });
    }
 
    public void Rebuild(WindowBase exclude = null) {
        ClearChildren();
        _previews.Clear();

        if (_process == null) return;

        var currentWindows = OS.Shell.WindowLayer.Children.OfType<WindowBase>().ToList();
        var procWindows = currentWindows.Where(w => w.ShowInTaskbar && w.OwnerProcess == _process && w != exclude).ToList();

        if (procWindows.Count == 0) {
            Hide(force: true);
            return;
        }

        float currentX = Padding;
        foreach (var win in procWindows) {
            var item = new WindowPreviewItem(new Vector2(currentX, Padding), new Vector2(PreviewMaxWidth, PreviewMaxHeight + TitleHeight), win);
            item.OnClickAction = () => {
                if (!win.IsVisible || win.Opacity < 0.5f) {
                    if (win is Window w) w.Restore();
                    else win.IsVisible = true;
                } else {
                    win.HandleFocus();
                }
                Hide(force: true);
            };
            AddChild(item);
            _previews.Add(item);
            currentX += PreviewMaxWidth + Padding;
        }

        // Update size and position
        float totalWidth = _previews.Count * (PreviewMaxWidth + Padding) + Padding;
        float totalHeight = PreviewMaxHeight + TitleHeight + Padding * 2;
        Size = new Vector2(totalWidth, totalHeight);

        Vector2 parentAbs = Parent?.AbsolutePosition ?? Vector2.Zero;
        float relX = (_lastButtonAbsPos.X - parentAbs.X) + (_lastButtonWidth - totalWidth) / 2f;
        float relY = (_lastButtonAbsPos.Y - parentAbs.Y) - totalHeight - 5;
        Position = new Vector2(relX, relY);

        // Clamp to screen
        var viewport = G.GraphicsDevice.Viewport;
        float absX = Position.X + parentAbs.X;
        if (absX < 5) Position = new Vector2(5 - parentAbs.X, Position.Y);
        if (absX + Size.X > viewport.Width - 5) Position = new Vector2(viewport.Width - Size.X - 5 - parentAbs.X, Position.Y);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible || Opacity <= 0) return;

        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, BackgroundColor * Opacity, rounded: 5f);
        batch.BorderRectangle(absPos, Size, BorderColor * Opacity, BorderThickness, rounded: 5f);
    }

    private class WindowPreviewItem : UIControl {
        private WindowBase _window;
        private Button _closeButton;
        public Action OnClickAction { get; set; }

        public WindowPreviewItem(Vector2 position, Vector2 size, WindowBase window) : base(position, size) {
            _window = window;
            BackgroundColor = Color.Transparent;
            HoverColor = new Color(50, 50, 50, 100);

            // Close button in top right
            _closeButton = new Button(new Vector2(size.X - 22, 3), new Vector2(19, 19), "x") {
                BackgroundColor = new Color(200, 50, 50, 0),
                HoverColor = new Color(255, 50, 50),
                TextColor = Color.White,
                OnClickAction = () => {
                    _window.Close();
                    // Find the parent popup and tell it to rebuild immediately with exclusion
                    UIElement p = Parent;
                    while (p != null && p is not TaskbarPreviewPopup) p = p.Parent;
                    if (p is TaskbarPreviewPopup popup) {
                        popup.Rebuild(exclude: _window);
                    }
                }
            };
            AddChild(_closeButton);
        }

        protected override void OnClick() {
            OnClickAction?.Invoke();
            base.OnClick();
        }

        protected override void UpdateInput() {
            // Only show close button transparency on hover of the preview item
            _closeButton.BackgroundColor = new Color(200, 50, 50, IsMouseOver ? 150 : 0);
            _closeButton.IsVisible = IsMouseOver || _closeButton.IsMouseOver;
            base.UpdateInput();
        }

        protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
            var absPos = AbsolutePosition;
            var opacity = AbsoluteOpacity;

            // Hover highlight
            if (IsMouseOver) {
                batch.FillRectangle(absPos, Size, HoverColor * opacity, rounded: 3f);
            }

            // Window title (Top)
            if (GameContent.FontSystem != null) {
                var font = GameContent.FontSystem.GetFont(16);
                string title = TextHelper.TruncateWithEllipsis(font, _window.Title, Size.X - 30); // Leave room for (x)
                font.DrawText(batch, title, absPos + new Vector2(5, 5), Color.White * opacity);
            }
 
            // Preview Texture (Center/Bottom)
            Texture2D tex = _window.Snapshot ?? _window.WindowRenderTarget;
            if (tex != null) {
                float titlePadding = 25f;
                float previewAreaHeight = Size.Y - titlePadding - 5;
                float scaleX = Size.X / tex.Width;
                float scaleY = previewAreaHeight / tex.Height;
                float scale = Math.Min(scaleX, scaleY);
 
                float drawW = tex.Width * scale;
                float drawH = tex.Height * scale;
                Vector2 drawPos = absPos + new Vector2((Size.X - drawW) / 2f, titlePadding + (previewAreaHeight - drawH) / 2f);

                // Draw texture using ShapeBatch directly to avoid batch inconsistency
                batch.DrawTexture(tex, drawPos, Color.White * opacity, scale);

                // Subtle border around preview
                batch.BorderRectangle(drawPos, new Vector2(drawW, drawH), Color.White * 0.2f * opacity, 1f);
            }
        }
    }
}
