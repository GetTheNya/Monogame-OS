using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Graphics;
using TheGame.Core.Input;
using TheGame.Core.UI.Controls;
using TheGame.Core.Animation;

namespace TheGame.Core.UI;

public class ClipboardHistoryPanel : Panel {
    private const float PanelWidth = 300f;
    private const float PanelHeight = 400f;
    private const float ItemHeight = 60f;
    private const float Padding = 10f;

    private ScrollPanel _scrollPanel;
    private float _showAnim = 0f;
    private bool _isClosing = false;

    private Label _titleLabel;
    private Button _clearButton;

    public ClipboardHistoryPanel() : base(Vector2.Zero, new Vector2(PanelWidth, PanelHeight)) {
        BackgroundColor = new Color(30, 30, 30, 240);
        BorderThickness = 0.5f;
        
        IsVisible = false;
        Opacity = 0f;
        CanFocus = false;

        _titleLabel = new Label(new Vector2(Padding, 10), "Clipboard History") {
            FontSize = 22,
            Color = Color.White,
            CanFocus = false
        };
        AddChild(_titleLabel);

        _clearButton = new Button(new Vector2(PanelWidth - 100, 8), new Vector2(85, 24), "Clear") {
            BackgroundColor = new Color(60, 60, 60, 180),
            FontSize = 14,
            CanFocus = false
        };
        _clearButton.OnClickAction = ClearAndClose;
        AddChild(_clearButton);
        
        _scrollPanel = new ScrollPanel(new Vector2(0, 40), new Vector2(PanelWidth, PanelHeight - 40));
        _scrollPanel.BackgroundColor = Color.Transparent;
        _scrollPanel.BorderThickness = 0;
        _scrollPanel.CanFocus = false;
        AddChild(_scrollPanel);

        RefreshHistory();
        Shell.Clipboard.OnChanged += RefreshHistory;
    }

    public void Toggle() {
        if (!IsVisible || _isClosing) {
            // Open
            IsVisible = true;
            _isClosing = false;
            RefreshHistory();
            UpdatePosition();
            Parent?.BringToFront(this);
            
            Tweener.CancelAll(this, "show_anim");
            var tween = Tweener.To(this, (v) => _showAnim = v, _showAnim, 1f, 0.2f, Easing.EaseOutQuad);
            tween.Tag = "show_anim";
        } else {
            // Close
            _isClosing = true;
            Tweener.CancelAll(this, "show_anim");
            var tween = Tweener.To(this, (v) => _showAnim = v, _showAnim, 0f, 0.2f, Easing.EaseOutQuad);
            tween.Tag = "show_anim";
            tween.OnCompleteAction(() => {
                IsVisible = false;
                _isClosing = false;
            });
        }
    }

    private void UpdatePosition() {
        // Positioning: Try caret position first, then mouse
        Vector2? caretPos = UIManager.FocusedElement?.GetCaretPosition();
        Vector2 targetPos;
        
        var viewport = G.GraphicsDevice.Viewport;
        
        if (caretPos.HasValue) {
            targetPos = caretPos.Value + new Vector2(0, 25);
        } else {
            targetPos = InputManager.MousePosition.ToVector2() + new Vector2(10, 10);
        }
        
        // Keep on screen
        if (targetPos.X + PanelWidth > viewport.Width) targetPos.X = viewport.Width - PanelWidth - 10;
        if (targetPos.Y + PanelHeight > viewport.Height - 40) targetPos.Y = viewport.Height - 40 - PanelHeight - 10;
        if (targetPos.X < 0) targetPos.X = 10;
        if (targetPos.Y < 0) targetPos.Y = 10;
        
        Position = targetPos;
    }


    private void RefreshHistory() {
        _scrollPanel.ClearChildren();
        var history = Shell.Clipboard.GetHistory();
        
        // Use post-update or similar if we ever have recursive click issues,
        // but for now it's fine since we are just recreating UI.

        if (history.Count == 0) {
            var emptyLabel = new Label(new Vector2(20, 20), "Clipboard is empty") {
                TextColor = Color.Gray,
                FontSize = 18,
                CanFocus = false
            };
            _scrollPanel.AddChild(emptyLabel);
            return;
        }

        float y = 0;
        var pinned = history.Where(i => i.IsPinned).ToList();
        var unpinned = history.Where(i => !i.IsPinned).ToList();

        if (pinned.Count > 0) {
            _scrollPanel.AddChild(new Label(new Vector2(Padding, y + 10), "PINNED") { FontSize = 12, Color = Color.LightBlue * 0.6f, CanFocus = false });
            y += 30;
            foreach (var item in pinned) {
                var itemButton = CreateHistoryItem(y, item);
                _scrollPanel.AddChild(itemButton);
                y += ItemHeight;
            }
            // Visible Section Separator
            var sep = new Panel(new Vector2(10, y + 15), new Vector2(PanelWidth - 20, 1)) { 
                BackgroundColor = Color.White * 0.15f, 
                CanFocus = false 
            };
            _scrollPanel.AddChild(sep);
            y += 40;
        }

        if (unpinned.Count > 0) {
            _scrollPanel.AddChild(new Label(new Vector2(Padding, y + 10), "RECENT") { FontSize = 12, Color = Color.Gray * 0.8f, CanFocus = false });
            y += 30;
            foreach (var item in unpinned) {
                var itemButton = CreateHistoryItem(y, item);
                _scrollPanel.AddChild(itemButton);
                y += ItemHeight;
            }
        }
        
        _scrollPanel.UpdateContentHeight(y + 20);
    }

    private HistoryItemButton CreateHistoryItem(float y, ClipboardItem item) {
        var itemButton = new HistoryItemButton(new Vector2(0, y), new Vector2(PanelWidth, ItemHeight), item);
        itemButton.OnClickAction = () => {
            // Move this item to the absolute top (MRU) before pasting
            Shell.Clipboard.SetText(item.PreviewText, item.SourceApp);
            
            if (UIManager.FocusedElement != null) {
                try {
                    UIManager.FocusedElement.Paste();
                } catch (Exception ex) {
                    DebugLogger.Log($"Error pasting: {ex.Message}");
                }
            }
            Close();
        };
        return itemButton;
    }

    public void Close() {
        if (!IsVisible || _isClosing) return;
        _isClosing = true;
        
        Tweener.CancelAll(this, "show_anim");
        var tween = Tweener.To(this, (v) => _showAnim = v, _showAnim, 0f, 0.2f, Easing.EaseOutQuad);
        tween.Tag = "show_anim";
        tween.OnCompleteAction(() => {
            IsVisible = false;
            _isClosing = false;
        });
    }

    public void ClearAndClose() {
        Shell.Clipboard.Clear();
        Close();
    }

    public override void Update(GameTime gameTime) {
        if (!IsVisible) return;

        base.Update(gameTime);

        // Close if clicking outside the panel
        // Use ignoreConsumed: false to ensure we don't close if a popup in front was clicked.
        if (IsVisible && !_isClosing && InputManager.IsMouseButtonJustPressed(MouseButton.Left) && !Bounds.Contains(InputManager.MousePosition)) {
            Close();
        }
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (_showAnim < 0.01f && !IsVisible) return;

        // Apply animation offset/fade
        float oldOpacity = Opacity;
        Vector2 oldPos = Position;
        
        Opacity = _showAnim;
        // Slide down animation
        Position = oldPos + new Vector2(0, (1f - _showAnim) * 10f);

        base.Draw(spriteBatch, batch);

        Opacity = oldOpacity;
        Position = oldPos;
    }

    private class HistoryItemButton : Button {
        private ClipboardItem _item;
        private Button _pinBtn;
        private Label _iconLabel;
        private Label _previewLabel;
        private Label _metaLabel;

        public HistoryItemButton(Vector2 pos, Vector2 size, ClipboardItem item) : base(pos, size, "") {
            _item = item;
            BackgroundColor = _item.IsPinned ? new Color(0, 0, 0, 20) : Color.Transparent;
            HoverColor = new Color(90, 90, 90, 5);
            BorderColor = Color.Transparent;
            EnableScaleAnimation = false;
            CanFocus = false;

            // Type Icon Label
            _iconLabel = new Label(new Vector2(15, 12), GetIconChar()) {
                FontSize = 18,
                Color = GetIconColor(),
                CanFocus = false
            };
            AddChild(_iconLabel);

            // Preview Text Label
            string text = _item.PreviewText;
            if (text.Length > 28) text = text.Substring(0, 25) + "...";
            _previewLabel = new Label(new Vector2(40, 12), text) {
                FontSize = 18,
                Color = Color.White,
                CanFocus = false
            };
            AddChild(_previewLabel);

            // Meta Label
            string meta = $"{_item.SourceApp ?? "Unknown"} • {_item.Timestamp:HH:mm}";
            _metaLabel = new Label(new Vector2(40, 34), meta) {
                FontSize = 14,
                Color = Color.Gray * 0.8f,
                CanFocus = false
            };
            AddChild(_metaLabel);

            // Pin Button - ensure it stands out but fits
            _pinBtn = new Button(new Vector2(size.X - 52, 10), new Vector2(46, 18), _item.IsPinned ? "Unpin" : "Pin") {
                BackgroundColor = _item.IsPinned ? new Color(80, 80, 80, 80) : new Color(40, 40, 40, 80),
                HoverColor = new Color(100, 100, 100, 150),
                BorderColor = Color.Transparent,
                FontSize = 10,
                CanFocus = false,
                TextColor = Color.White * 0.8f
            };
            _pinBtn.OnClickAction = () => {
                ClipboardManager.Instance.TogglePin(_item.Id);
            };
            AddChild(_pinBtn);

            // Delete Button (More compact)
            var deleteBtn = new Button(new Vector2(size.X - 52, 32), new Vector2(46, 18), "Delete") {
                BackgroundColor = new Color(60, 20, 20, 60),
                HoverColor = new Color(100, 30, 30, 120),
                BorderColor = Color.Transparent,
                FontSize = 10,
                CanFocus = false,
                TextColor = Color.White * 0.8f
            };
            deleteBtn.OnClickAction = () => {
                ClipboardManager.Instance.RemoveItem(_item.Id);
            };
            AddChild(deleteBtn);
        }

        private Color GetIconColor() {
            return _item.Type switch {
                ClipboardContentType.Text => Color.LightBlue,
                ClipboardContentType.FileList => Color.Orange,
                ClipboardContentType.Image => Color.LightGreen,
                _ => Color.Gray
            };
        }

        protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
            var absPos = AbsolutePosition;
            float alpha = AbsoluteOpacity;
            
            // Background
            batch.FillRectangle(absPos, Size, CurrentBackgroundColor * alpha);
            
            // Pinned indicator strip (Darker blue)
            if (_item.IsPinned) {
                batch.FillRectangle(absPos, new Vector2(4, Size.Y), new Color(0, 80, 180) * alpha);
            }

            if (BorderColor != Color.Transparent) {
                batch.BorderRectangle(absPos, Size, BorderColor * alpha, thickness: 1f);
            }
            
            // Very subtle separator line
            const float pad = 10f;
            batch.FillRectangle(absPos + new Vector2(pad, Size.Y - 1), new Vector2(Size.X - pad * 2, 1), Color.White * (0.04f * alpha));
        }

        private string GetIconChar() {
            return _item.Type switch {
                ClipboardContentType.Text => "T",
                ClipboardContentType.FileList => "F",
                ClipboardContentType.Image => "I",
                _ => "?"
            };
        }
    }
}
