using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core.Animation;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

/// <summary>
/// A slide-out panel showing notification history, similar to Windows 10 Action Center.
/// </summary>
public class NotificationHistoryPanel : UIElement {
    private const float PanelWidth = 380f;
    private const float HeaderHeight = 50f;
    private const float ItemHeight = 80f;
    private const float Padding = 15f;

    private float _slideOffset;
    private bool _isOpen = false;
    private bool _isAnimating = false;
    private bool _isClearing = false;
    private ScrollPanel _scrollPanel;
    private Label _headerLabel;
    private Button _clearButton;
    private List<NotificationHistoryItem> _items = new();

    public bool IsOpen => _isOpen;

    public NotificationHistoryPanel() {
        var viewport = G.GraphicsDevice.Viewport;
        Size = new Vector2(PanelWidth, viewport.Height - 40);
        _slideOffset = PanelWidth;
        Position = new Vector2(viewport.Width - PanelWidth + _slideOffset, 0);

        // Header
        _headerLabel = new Label(new Vector2(Padding, 15), "Notifications") { FontSize = 20, Color = Color.White };
        AddChild(_headerLabel);

        _clearButton = new Button(new Vector2(PanelWidth - 100, 12), new Vector2(85, 30), "Clear all") {
            BackgroundColor = new Color(60, 60, 60)
        };
        _clearButton.OnClickAction = ClearAllAnimated;
        AddChild(_clearButton);

        // Scrollable content area
        _scrollPanel = new ScrollPanel(new Vector2(0, HeaderHeight), new Vector2(PanelWidth, Size.Y - HeaderHeight));
        AddChild(_scrollPanel);

        // Subscribe to notification events
        NotificationManager.Instance.OnNotificationAdded += _ => { if (_isOpen && !_isClearing) RefreshContent(); };
        NotificationManager.Instance.OnHistoryCleared += () => { if (!_isClearing) RefreshContent(); };

        RefreshContent();
    }

    public void Toggle() {
        if (_isAnimating) {
            ForceClose();
            return;
        }
        
        if (_isOpen) Close();
        else Open();
    }

    public void Open() {
        if (_isOpen || _isAnimating) return;
        _isOpen = true;
        _isAnimating = true;
        NotificationManager.Instance.MarkAllAsRead();
        RefreshContent();
        var tween = Tweener.To(this, v => _slideOffset = v, _slideOffset, 0f, 0.25f, Easing.EaseOutQuad);
        tween.Tag = "slide";
        tween.OnComplete = () => { _isAnimating = false; };
    }

    public void Close() {
        if (!_isOpen || _isAnimating) return;
        _isOpen = false;
        _isAnimating = true;
        var tween = Tweener.To(this, v => _slideOffset = v, _slideOffset, PanelWidth, 0.2f, Easing.EaseInQuad);
        tween.Tag = "slide";
        tween.OnComplete = () => { _isAnimating = false; };
    }

    private void ForceClose() {
        _isOpen = false;
        _isAnimating = true;
        Tweener.CancelAll(this, "slide");
        var tween = Tweener.To(this, v => _slideOffset = v, _slideOffset, PanelWidth, 0.2f, Easing.EaseInQuad);
        tween.Tag = "slide";
        tween.OnComplete = () => { _isAnimating = false; };
    }

    private void ClearAllAnimated() {
        if (_isClearing || _items.Count == 0) return;
        _isClearing = true;

        int total = _items.Count;
        int completed = 0;
        
        for (int i = 0; i < _items.Count; i++) {
            var item = _items[i];
            float delay = i * 0.05f;
            item.StartDelayedAnimateOut(delay, () => {
                completed++;
                if (completed >= total) {
                    NotificationManager.Instance.ClearHistory();
                    _isClearing = false;
                    RefreshContent();
                }
            });
        }
    }

    private void RefreshContent() {
        if (_isClearing) return;

        // Manual removal to avoid ToArray allocation
        for (int i = _scrollPanel.Children.Count - 1; i >= 0; i--) {
            _scrollPanel.RemoveChild(_scrollPanel.Children[i]);
        }
        _items.Clear();

        float y = Padding;
        var history = NotificationManager.Instance.History;

        if (history.Count == 0) {
            _scrollPanel.AddChild(new Label(new Vector2(Padding, y), "No notifications") { FontSize = 14, Color = Color.Gray });
            _scrollPanel.UpdateContentHeight(y + 30);
            return;
        }

        // Manual date grouping to avoid GroupBy allocation
        DateTime lastDate = DateTime.MinValue;
        for (int i = 0; i < history.Count; i++) {
            var notif = history[i];
            DateTime notifDate = notif.Timestamp.Date;
            
            // Add date header if new date
            if (notifDate != lastDate) {
                lastDate = notifDate;
                string dateLabel = notifDate == DateTime.Today ? "Today" :
                                   notifDate == DateTime.Today.AddDays(-1) ? "Yesterday" :
                                   notifDate.ToString("MMMM d");
                _scrollPanel.AddChild(new Label(new Vector2(Padding, y), dateLabel) { FontSize = 12, Color = Color.Gray });
                y += 25;
            }

            var item = new NotificationHistoryItem(new Vector2(Padding, y), new Vector2(PanelWidth - Padding * 2, ItemHeight - 10), notif);
            item.OnDismiss = () => OnItemDismissed(item);
            _scrollPanel.AddChild(item);
            _items.Add(item);
            y += ItemHeight;
        }

        _scrollPanel.UpdateContentHeight(y + Padding);
    }

    private void OnItemDismissed(NotificationHistoryItem dismissedItem) {
        int index = _items.IndexOf(dismissedItem);
        if (index == -1) return;

        NotificationManager.Instance.Dismiss(dismissedItem.NotificationId);
        _items.RemoveAt(index);
        _scrollPanel.RemoveChild(dismissedItem);

        float offset = -ItemHeight;
        for (int i = index; i < _items.Count; i++) {
            var item = _items[i];
            float currentY = item.Position.Y;
            float targetY = currentY + offset;
            Tweener.To(item, v => item.Position = new Vector2(item.Position.X, v), currentY, targetY, 0.25f, Easing.EaseOutQuad);
        }

        Tweener.Delay(0.3f, RefreshContent);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        var viewport = G.GraphicsDevice.Viewport;
        Size = new Vector2(PanelWidth, viewport.Height - 40);
        _scrollPanel.Size = new Vector2(PanelWidth, Size.Y - HeaderHeight);
        Position = new Vector2(viewport.Width - PanelWidth + _slideOffset, 0);

        if (_isOpen && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            if (!Bounds.Contains(InputManager.MousePosition)) {
                ForceClose();
            }
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;
        var absPos = AbsolutePosition;
        batch.FillRectangle(absPos, Size, new Color(30, 30, 30, 245), rounded: 0f);
        batch.FillRectangle(absPos, new Vector2(Size.X, HeaderHeight), new Color(45, 45, 45), rounded: 0f);
    }
}

public class NotificationHistoryItem : UIElement {
    private Notification _notification;
    private Button _dismissBtn;
    public Action OnDismiss;

    private bool _isDragging = false;
    private float _dragStartX;
    private float _swipeOffset = 0f;
    private bool _isAnimatingOut = false;
    
    private float _delayTimer = -1f;
    private Action _delayedCallback;

    public string NotificationId => _notification.Id;

    public NotificationHistoryItem(Vector2 pos, Vector2 size, Notification notif) {
        Position = pos;
        Size = size;
        _notification = notif;

        _dismissBtn = new Button(new Vector2(size.X - 25, 5), new Vector2(20, 20), "×") {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(200, 50, 50)
        };
        _dismissBtn.OnClickAction = () => {
            if (!_isAnimatingOut) {
                AnimateOut(() => OnDismiss?.Invoke());
            }
        };
        AddChild(_dismissBtn);
    }

    public void AnimateOut(Action onComplete) {
        if (_isAnimatingOut) return;
        _isAnimatingOut = true;
        Tweener.To(this, v => _swipeOffset = v, _swipeOffset, Size.X + 30f, 0.2f, Easing.EaseInQuad)
            .OnComplete = onComplete;
    }

    public void StartDelayedAnimateOut(float delay, Action onComplete) {
        if (_isAnimatingOut) return;
        _delayTimer = delay;
        _delayedCallback = onComplete;
    }

    public override void Update(GameTime gameTime) {
        if (_delayTimer >= 0) {
            _delayTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_delayTimer <= 0) {
                _delayTimer = -1f;
                AnimateOut(_delayedCallback);
            }
        }

        if (_isAnimatingOut) {
            base.Update(gameTime);
            return;
        }

        var absPos = AbsolutePosition;
        var myBounds = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        bool isHovering = myBounds.Contains(InputManager.MousePosition);

        var dismissBounds = new Rectangle(
            (int)(absPos.X + Size.X - 25),
            (int)(absPos.Y + 5),
            20, 20
        );

        if (isHovering && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            if (!dismissBounds.Contains(InputManager.MousePosition)) {
                _isDragging = true;
                _dragStartX = InputManager.MousePosition.X;
                InputManager.IsMouseConsumed = true;
            }
        }

        if (_isDragging) {
            InputManager.IsMouseConsumed = true;
            if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                float delta = InputManager.MousePosition.X - _dragStartX;
                _swipeOffset = Math.Max(0, delta);
            } else {
                _isDragging = false;
                if (_swipeOffset > Size.X * 0.3f) {
                    AnimateOut(() => OnDismiss?.Invoke());
                } else {
                    Tweener.To(this, v => _swipeOffset = v, _swipeOffset, 0f, 0.15f, Easing.EaseOutQuad);
                }
            }
        }

        if (!_isDragging && InputManager.IsMouseButtonJustReleased(MouseButton.Left) && isHovering && Math.Abs(_swipeOffset) < 5) {
            if (!dismissBounds.Contains(InputManager.MousePosition)) {
                _notification.OnClick?.Invoke();
            }
        }

        base.Update(gameTime);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var basePos = AbsolutePosition;
        var drawPos = basePos + new Vector2(_swipeOffset, 0);
        
        float alpha = 1f - (_swipeOffset / (Size.X + 30f)) * 0.5f;
        // Use explicit alpha to avoid color issues
        batch.FillRectangle(drawPos, Size, new Color(50, 50, 50, (int)(255 * alpha)), rounded: 6f);
        batch.BorderRectangle(drawPos, Size, new Color(80, 80, 80, (int)(255 * alpha)), thickness: 1f, rounded: 6f);

        float iconSize = 40f;
        float textX = 55f;

        if (_notification.Icon != null) {
            spriteBatch.Draw(_notification.Icon, new Rectangle((int)(drawPos.X + 8), (int)(drawPos.Y + 8), (int)iconSize, (int)iconSize), Color.White * alpha);
        } else {
            batch.FillRectangle(drawPos + new Vector2(8, 8), new Vector2(iconSize, iconSize), new Color(0, 120, 215, (int)(255 * alpha)), rounded: 4f);
        }

        if (GameContent.FontSystem != null) {
            var titleFont = GameContent.FontSystem.GetFont(16);
            var bodyFont = GameContent.FontSystem.GetFont(13);
            var timeFont = GameContent.FontSystem.GetFont(11);

            titleFont.DrawText(batch, _notification.Title ?? "", drawPos + new Vector2(textX, 8), Color.White * alpha);
            string shortText = _notification.Text?.Length > 40 ? _notification.Text.Substring(0, 37) + "..." : _notification.Text ?? "";
            bodyFont.DrawText(batch, shortText, drawPos + new Vector2(textX, 28), Color.LightGray * alpha);
            timeFont.DrawText(batch, _notification.Timestamp.ToString("HH:mm"), drawPos + new Vector2(textX, 48), Color.Gray * alpha);
        }

        _dismissBtn.Position = new Vector2(Size.X - 25 + _swipeOffset, 5);
        _dismissBtn.Opacity = alpha;
    }
}
