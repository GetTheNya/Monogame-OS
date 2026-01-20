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
        CanFocus = false;
        ConsumesInput = true;

        // Header
        _headerLabel = new Label(new Vector2(Padding, 15), "Notifications") { FontSize = 20, Color = Color.White, CanFocus = false };
        AddChild(_headerLabel);

        _clearButton = new Button(new Vector2(PanelWidth - 100, 12), new Vector2(85, 30), "Clear all") {
            BackgroundColor = new Color(60, 60, 60),
            CanFocus = false
        };
        _clearButton.OnClickAction = ClearAllAnimated;
        AddChild(_clearButton);

        // Scrollable content area
        _scrollPanel = new ScrollPanel(new Vector2(0, HeaderHeight), new Vector2(PanelWidth, Size.Y - HeaderHeight)) { CanFocus = false };
        AddChild(_scrollPanel);

        // Subscribe to notification events
        NotificationManager.Instance.OnNotificationAdded += _ => { if (_isOpen && !_isClearing) RefreshContent(); };
        NotificationManager.Instance.OnHistoryCleared += () => { if (!_isClearing) RefreshContent(); };
        NotificationManager.Instance.OnNotificationDismissed += DismissItemById;

        RefreshContent();
    }

    public new void OnResize(int width, int height) {
        Size = new Vector2(PanelWidth, height - 40);
        if (!_isOpen) _slideOffset = PanelWidth;
        Position = new Vector2(width - PanelWidth + _slideOffset, 0);
        
        _scrollPanel.Size = new Vector2(PanelWidth, Size.Y - HeaderHeight);
        base.OnResize?.Invoke();
    }

    public void Toggle() {
        if (_isOpen) Close();
        else Open();
    }

    public void Open() {
        if (_isOpen && !_isAnimating) return;
        _isOpen = true;
        _isAnimating = true;
        NotificationManager.Instance.MarkAllAsRead();
        RefreshContent();
        Tweener.CancelAll(this, "slide");
        var tween = Tweener.To(this, v => _slideOffset = v, _slideOffset, 0f, 0.25f, Easing.EaseOutQuad);
        tween.Tag = "slide";
        tween.OnComplete = () => { _isAnimating = false; };
    }

    public void Close() {
        if (!_isOpen && !_isAnimating) return;
        _isOpen = false;
        _isAnimating = true;
        Tweener.CancelAll(this, "slide");
        var tween = Tweener.To(this, v => _slideOffset = v, _slideOffset, PanelWidth, 0.2f, Easing.EaseInQuad);
        tween.Tag = "slide";
        tween.OnComplete = () => { _isAnimating = false; };
    }

    private void ForceClose() {
        if (!_isOpen && _slideOffset >= PanelWidth) return;
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
            _scrollPanel.AddChild(new Label(new Vector2(Padding, y), "No notifications") { FontSize = 14, Color = Color.Gray, CanFocus = false });
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
                _scrollPanel.AddChild(new Label(new Vector2(Padding, y), dateLabel) { FontSize = 12, Color = Color.Gray, CanFocus = false });
                y += 25;
            }

            // Account for scrollbar width (8px + 2px margin on each side)
            float itemWidth = PanelWidth - Padding * 2;
            var item = new NotificationHistoryItem(new Vector2(Padding, y), itemWidth, notif);
            item.OnDismiss = () => OnItemDismissed(item);
            _scrollPanel.AddChild(item);
            _items.Add(item);
            y += item.Size.Y + 10f; // Add gap between items
        }

        _scrollPanel.UpdateContentHeight(y + Padding);
    }

    private void DismissItemById(string id) {
        if (_isClearing) return;
        var item = _items.Find(i => i.NotificationId == id);
        if (item != null) {
            item.AnimateOut(() => OnItemDismissed(item));
        }
    }

    private void OnItemDismissed(NotificationHistoryItem dismissedItem) {
        int index = _items.IndexOf(dismissedItem);
        if (index == -1) return;

        NotificationManager.Instance.Dismiss(dismissedItem.NotificationId);
        _items.RemoveAt(index);
        _scrollPanel.RemoveChild(dismissedItem);

        float offset = -(dismissedItem.Size.Y + 10f);
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
            if (!Bounds.Contains(InputManager.MousePosition) && !InputManager.IsMouseConsumed) {
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
    private List<Button> _actionButtons = new();
    public Action OnDismiss;

    private bool _isDragging = false;
    private float _dragStartX;
    private float _swipeOffset = 0f;
    private bool _isAnimatingOut = false;
    
    private float _delayTimer = -1f;
    private Action _delayedCallback;

    private string _wrappedText;
    private string _wrappedTitle;

    public string NotificationId => _notification.Id;

    public NotificationHistoryItem(Vector2 pos, float width, Notification notif) {
        Position = pos;
        _notification = notif;

        float textX = 55f;
        float textAvailableWidth = width - textX - 30f; // 30 for dismiss button
        
        var titleFont = GameContent.FontSystem.GetFont(18); // Increased
        var bodyFont = GameContent.FontSystem.GetFont(15);  // Increased
        
        _wrappedTitle = TextHelper.WrapText(titleFont, _notification.Title, textAvailableWidth);
        _wrappedText = TextHelper.WrapText(bodyFont, _notification.Text, textAvailableWidth);

        float titleHeight = string.IsNullOrEmpty(_wrappedTitle) ? 0 : titleFont.MeasureString(_wrappedTitle).Y;
        float bodyHeight = string.IsNullOrEmpty(_wrappedText) ? 0 : bodyFont.MeasureString(_wrappedText).Y;
        
        float currentY = 8f + titleHeight + (titleHeight > 0 && bodyHeight > 0 ? 4 : 0) + bodyHeight + 6f;

        // Action buttons
        if (_notification.Actions != null && _notification.Actions.Count > 0) {
            float btnX = textX;
            foreach (var action in _notification.Actions) {
                var btn = new Button(new Vector2(btnX, currentY), new Vector2(85, 26), action.Label) {
                    BackgroundColor = new Color(60, 60, 60),
                    FontSize = 13,
                    CanFocus = false
                };
                var capturedAction = action;
                btn.OnClickAction = () => {
                    capturedAction.OnClick?.Invoke();
                };
                AddChild(btn);
                _actionButtons.Add(btn);
                btnX += 90;
            }
            currentY += 32f;
        }
        
        float contentHeight = currentY + 22f; // Area for timestamp
        Size = new Vector2(width, Math.Max(75f, contentHeight));
        CanFocus = false;

        _dismissBtn = new Button(new Vector2(Size.X - 25, 5), new Vector2(20, 20), "×") {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(200, 50, 50),
            CanFocus = false
        };
        _dismissBtn.OnClickAction = () => {
            if (!_isAnimatingOut) {
                AnimateOut(() => OnDismiss?.Invoke());
            }
        };
        _dismissBtn.Tooltip = "Dismiss notification";
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

        // Check if mouse is over any action button
        bool overActionButton = false;
        foreach (var btn in _actionButtons) {
            if (new Rectangle((int)btn.AbsolutePosition.X, (int)btn.AbsolutePosition.Y, (int)btn.Size.X, (int)btn.Size.Y).Contains(InputManager.MousePosition)) {
                overActionButton = true;
                break;
            }
        }

        if (isHovering && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            if (!dismissBounds.Contains(InputManager.MousePosition) && !overActionButton) {
                _isDragging = true;
                _dragStartX = InputManager.MousePosition.X;
                // Don't consume here yet, let UIElement see the press if it's just a click
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
                } else if (_swipeOffset < 5f) {
                    // Simple click
                    OnClick();
                    _swipeOffset = 0f;
                } else {
                    Tweener.To(this, v => _swipeOffset = v, _swipeOffset, 0f, 0.15f, Easing.EaseOutQuad);
                }
            }
        }

        base.Update(gameTime);
    }

    protected override void OnClick() {
        if (_isAnimatingOut) return;
        _notification.OnClick?.Invoke();
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
            var titleFont = GameContent.FontSystem.GetFont(18);
            var bodyFont = GameContent.FontSystem.GetFont(15);
            var timeFont = GameContent.FontSystem.GetFont(12);

            Vector2 titleSize = Vector2.Zero;
            if (!string.IsNullOrEmpty(_wrappedTitle)) {
                titleFont.DrawText(batch, _wrappedTitle, drawPos + new Vector2(textX, 8), Color.White * alpha);
                titleSize = titleFont.MeasureString(_wrappedTitle);
            }

            if (!string.IsNullOrEmpty(_wrappedText)) {
                float bodyY = 8f + (titleSize.Y > 0 ? titleSize.Y + 4f : 0);
                bodyFont.DrawText(batch, _wrappedText, drawPos + new Vector2(textX, bodyY), Color.LightGray * alpha);
            }

            float timeY = Size.Y - 20f;
            timeFont.DrawText(batch, _notification.Timestamp.ToString("HH:mm"), drawPos + new Vector2(textX, timeY), Color.Gray * alpha);
        }

        _dismissBtn.Position = new Vector2(Size.X - 25 + _swipeOffset, 5);
        _dismissBtn.Opacity = alpha;

        // Ensure action buttons follow swipe
        float btnBaseX = 55f;
        int i = 0;
        foreach (var btn in _actionButtons) {
            btn.Position = new Vector2(btnBaseX + i * 90 + _swipeOffset, btn.Position.Y);
            btn.Opacity = alpha;
            i++;
        }
    }
}
