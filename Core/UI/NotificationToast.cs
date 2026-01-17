using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core.Animation;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

/// <summary>
/// A toast popup that displays a notification in the bottom-right corner.
/// </summary>
public class NotificationToast : UIElement {
    private const float ToastWidth = 350f;
    private const float ToastPadding = 15f;
    private const float IconSize = 48f;
    private const float DisplayDuration = 5f;

    private Notification _notification;
    private float _timer = 0f;
    private float _animatedX = 0f;
    private float _animatedY = 0f;
    private float _targetY = 0f;
    private string _wrappedTitle;
    private string _wrappedText;
    private bool _isClosing = false;
    private List<Button> _actionButtons = new();
    private Button _closeButton;

    // Swipe support
    private bool _isDragging = false;
    private float _dragStartMouseX;
    private float _swipeOffset = 0f;

    // Hover state
    private bool _isHovered = false;

    public Action<NotificationToast> OnDismissed;
    public string NotificationId => _notification.Id;
    public bool IsHovered => _isHovered;

    public NotificationToast(Notification notification, float yOffset) {
        _notification = notification;
        _targetY = yOffset;
        _animatedY = yOffset;

        // Wrap text and calculate size
        float textAvailableWidth = ToastWidth - (ToastPadding * 2 + IconSize + 10f) - 30f; // 30 for close button
        var titleFont = GameContent.FontSystem.GetFont(18);
        var bodyFont = GameContent.FontSystem.GetFont(14);
        
        _wrappedTitle = TextHelper.WrapText(titleFont, _notification.Title, textAvailableWidth);
        _wrappedText = TextHelper.WrapText(bodyFont, _notification.Text, textAvailableWidth);

        // Calculate height based on content
        float titleHeight = string.IsNullOrEmpty(_wrappedTitle) ? 0 : titleFont.MeasureString(_wrappedTitle).Y;
        float bodyHeight = string.IsNullOrEmpty(_wrappedText) ? 0 : bodyFont.MeasureString(_wrappedText).Y;
        
        float contentHeight = ToastPadding * 2 + Math.Max(IconSize, titleHeight + (titleHeight > 0 && bodyHeight > 0 ? 4 : 0) + bodyHeight);
        if (_notification.Actions.Count > 0) contentHeight += 40f;

        Size = new Vector2(ToastWidth, Math.Max(80f, contentHeight));
        
        var viewport = G.GraphicsDevice.Viewport;
        _animatedX = ToastWidth + 20f; // Start off-screen
        Position = new Vector2(viewport.Width - ToastWidth - 20 + _animatedX, viewport.Height - 60 - _animatedY - Size.Y);

        // Close button (X)
        _closeButton = new Button(new Vector2(ToastWidth - 30, 5), new Vector2(24, 24), "×") {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(200, 50, 50)
        };
        _closeButton.OnClickAction = Close;
        AddChild(_closeButton);

        // Create action buttons
        float btnX = IconSize + ToastPadding * 2;
        float btnY = Size.Y - 28f - ToastPadding;
        foreach (var action in _notification.Actions) {
            var btn = new Button(new Vector2(btnX, btnY), new Vector2(80, 28), action.Label) {
                BackgroundColor = new Color(60, 60, 60)
            };
            var capturedAction = action;
            btn.OnClickAction = () => {
                capturedAction.OnClick?.Invoke();
                Close();
            };
            AddChild(btn);
            _actionButtons.Add(btn);
            btnX += 85;
        }

        // Animate in
        Tweener.To(this, v => _animatedX = v, _animatedX, 0f, 0.3f, Easing.EaseOutQuad);
    }

    public void UpdateTargetPosition(float yOffset) {
        if (Math.Abs(_targetY - yOffset) > 1f) {
            Tweener.To(this, v => _animatedY = v, _animatedY, yOffset, 0.2f, Easing.EaseOutQuad);
        }
        _targetY = yOffset;
    }

    public override void Update(GameTime gameTime) {
        var viewport = G.GraphicsDevice.Viewport;
        
        // Update position FIRST so Bounds is correct
        Position = new Vector2(viewport.Width - ToastWidth - 20 + _animatedX + _swipeOffset, 
                               viewport.Height - 60 - _animatedY - Size.Y);

        if (_isClosing) {
            base.Update(gameTime);
            return;
        }

        // Calculate hover bounds manually
        var hoverBounds = new Rectangle(
            (int)Position.X, (int)Position.Y,
            (int)Size.X, (int)Size.Y
        );
        _isHovered = hoverBounds.Contains(InputManager.MousePosition);

        // Only count timer if not hovered and not dragging
        if (!_isHovered && !_isDragging) {
            _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_timer >= DisplayDuration) {
                Close();
                base.Update(gameTime);
                return;
            }
        }
        
        // Start drag on mouse press (BEFORE base.Update so buttons don't consume the click first)
        if (!_isDragging && _isHovered && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            // Check if clicking close button or action buttons
            var closeBtnBounds = new Rectangle(
                (int)(Position.X + _closeButton.Position.X),
                (int)(Position.Y + _closeButton.Position.Y),
                (int)_closeButton.Size.X, (int)_closeButton.Size.Y
            );
            bool clickedButton = closeBtnBounds.Contains(InputManager.MousePosition);
            
            foreach (var btn in _actionButtons) {
                var btnBounds = new Rectangle(
                    (int)(Position.X + btn.Position.X),
                    (int)(Position.Y + btn.Position.Y),
                    (int)btn.Size.X, (int)btn.Size.Y
                );
                if (btnBounds.Contains(InputManager.MousePosition)) {
                    clickedButton = true;
                    break;
                }
            }
            
            if (!clickedButton) {
                _isDragging = true;
                _dragStartMouseX = InputManager.MousePosition.X;
                _swipeOffset = 0f;
                InputManager.IsMouseConsumed = true;
            }
        }

        // During drag
        if (_isDragging) {
            InputManager.IsMouseConsumed = true;
            
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                float delta = InputManager.MousePosition.X - _dragStartMouseX;
                _swipeOffset = Math.Max(0, delta); // Only allow swiping right
            } else {
                // Mouse released - check if swiped enough
                _isDragging = false;
                if (_swipeOffset > ToastWidth * 0.25f) {
                    SwipeDismiss();
                } else {
                    // Snap back
                    Tweener.To(this, v => _swipeOffset = v, _swipeOffset, 0f, 0.15f, Easing.EaseOutQuad);
                }
            }
        }

        base.Update(gameTime);
    }

    private void SwipeDismiss() {
        if (_isClosing) return;
        _isClosing = true;
        Tweener.To(this, v => _swipeOffset = v, _swipeOffset, ToastWidth + 50f, 0.15f, Easing.EaseInQuad)
            .OnComplete = () => OnDismissed?.Invoke(this);
    }

    public void Close() {
        if (_isClosing) return;
        _isClosing = true;
        Tweener.To(this, v => _animatedX = v, _animatedX, ToastWidth + 20f, 0.2f, Easing.EaseInQuad)
            .OnComplete = () => OnDismissed?.Invoke(this);
    }

    public override void Draw(SpriteBatch sb, ShapeBatch batch) {
        if (!IsVisible) return;

        var absPos = AbsolutePosition;

        // Background with slight transparency based on swipe
        float swipeAlpha = 1f - (_swipeOffset / (ToastWidth + 50f)) * 0.5f;
        batch.FillRectangle(absPos, Size, new Color(40, 40, 40, (int)(240 * swipeAlpha)), rounded: 8f);
        batch.BorderRectangle(absPos, Size, new Color(80, 80, 80) * swipeAlpha, thickness: 1f, rounded: 8f);

        // Icon
        if (_notification.Icon != null) {
            sb.Draw(_notification.Icon, new Rectangle((int)(absPos.X + ToastPadding), (int)(absPos.Y + ToastPadding), (int)IconSize, (int)IconSize), Color.White * swipeAlpha);
        } else {
            // Default notification icon placeholder
            batch.FillRectangle(absPos + new Vector2(ToastPadding, ToastPadding), new Vector2(IconSize, IconSize), new Color(0, 120, 215) * swipeAlpha, rounded: 6f);
        }

        // Text
        if (GameContent.FontSystem != null) {
            float textX = ToastPadding + IconSize + 10f;
            var titleFont = GameContent.FontSystem.GetFont(18);
            var bodyFont = GameContent.FontSystem.GetFont(14);

            Vector2 titleSize = Vector2.Zero;
            if (!string.IsNullOrEmpty(_wrappedTitle)) {
                titleFont.DrawText(batch, _wrappedTitle, absPos + new Vector2(textX, ToastPadding), Color.White * swipeAlpha);
                titleSize = titleFont.MeasureString(_wrappedTitle);
            }
            
            if (!string.IsNullOrEmpty(_wrappedText)) {
                float bodyY = ToastPadding + (titleSize.Y > 0 ? titleSize.Y + 4f : 0);
                bodyFont.DrawText(batch, _wrappedText, absPos + new Vector2(textX, bodyY), Color.LightGray * swipeAlpha);
            }
        }

        base.Draw(sb, batch);
    }
}
