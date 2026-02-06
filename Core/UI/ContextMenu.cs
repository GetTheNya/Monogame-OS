using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using TheGame.Core.Input;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core.Animation;
using TheGame.Core.OS;


namespace TheGame.Core.UI;

public enum MenuItemType {
    Normal,
    Separator,
    Checkbox
}

public class MenuItem : IEquatable<MenuItem> {
    public string Text { get; set; } = "";
    public Action Action { get; set; }
    public Texture2D Icon { get; set; }
    public string ShortcutText { get; set; }
    public List<MenuItem> SubItems { get; set; }
    public bool HasSubItems => SubItems != null && SubItems.Count > 0;

    public MenuItemType Type { get; set; } = MenuItemType.Normal;
    public int Priority { get; set; } = 0;
    public bool IsDefault { get; set; } = false;
    public bool IsChecked { get; set; } = false;
    public bool IsEnabled { get; set; } = true;
    public bool IsVisible { get; set; } = true;

    // Helper for deduplication
    public override bool Equals(object obj) => obj is MenuItem other && Equals(other);

    public bool Equals(MenuItem other) {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Type == MenuItemType.Separator && other.Type == MenuItemType.Separator) return true;
        return Text == other.Text && Action == other.Action && Type == other.Type;
    }

    public override int GetHashCode() {
        if (Type == MenuItemType.Separator) return typeof(MenuItemType).GetHashCode();
        return HashCode.Combine(Text, Action, Type);
    }
}

public class ContextMenu : Panel {
    private const float ItemHeight = 30f;
    private const float ItemPadding = 5f;
    private const float Width = 260f;
    private ContextMenu _openedSubMenu;
    private ContextMenu _parentMenu;
    private UIElement _hoveredItem;
    private float _autoOpenTimer = 0f;
    private float _subMenuCloseTimer = 0f;
    private const float AutoOpenDelay = 0.2f;
    private const float SubMenuCloseDelay = 0.3f;

    public ContextMenu() : base(Vector2.Zero, Vector2.Zero) {
        BackgroundColor = new Color(40, 40, 40);
        BorderColor = Color.Gray;
        BorderThickness = 1f;
        IsVisible = false;
        ConsumesInput = true;
        // High Z-order usually handled by being added last or specific layer.
    }

    public void Show(Vector2 position, List<MenuItem> items, ContextMenu parent = null) {
        _parentMenu = parent;
        Position = position;
        
        IsVisible = true;
        Opacity = 0f;
        
        ClearChildren();

        float yOffset = ItemPadding;
        foreach (var item in items) {
            if (item.Type == MenuItemType.Separator) {
                var sep = new Panel(new Vector2(ItemPadding, yOffset + 4), new Vector2(Width - ItemPadding * 2, 1)) {
                    BackgroundColor = new Color(80, 80, 80),
                    BorderThickness = 0,
                    ConsumesInput = false
                };
                AddChild(sep);
                yOffset += 10f; // Slot for separator
                continue;
            }

            var btn = new Button(new Vector2(ItemPadding, yOffset), new Vector2(Width - ItemPadding * 2, ItemHeight), item.Text) {
                BackgroundColor = Color.Transparent,
                HoverColor = new Color(60, 60, 60),
                Tag = item,
                IsEnabled = item.IsEnabled,
                TextColor = item.IsEnabled ? Color.White : Color.Gray,
                TextAlign = TextAlign.Left,
                Padding = new Vector4(30, 0, 30, 0),
                UseBoldFont = item.IsDefault,
                OnClickAction = () => {
                    if (item.Type == MenuItemType.Checkbox) {
                        item.IsChecked = !item.IsChecked;
                    }

                    if (!item.HasSubItems) {
                        item.Action?.Invoke();
                        CloseAll();
                    }
                }
            };
            AddChild(btn);
            yOffset += ItemHeight + ItemPadding;
        }

        Vector2 targetSize = new Vector2(Width, yOffset);
        Size = targetSize;
        
        // Ensure within screen bounds (clamp to viewport)
        var viewport = G.GraphicsDevice.Viewport;
        if (Position.X + Size.X > viewport.Width) {
            // For sub-menus, if we can't fit to the right, flip to the left
            if (_parentMenu != null) {
                Position = new Vector2(_parentMenu.Position.X - Size.X, Position.Y);
            } else {
                Position = new Vector2(viewport.Width - Size.X, Position.Y);
            }
        }
        if (Position.Y + Size.Y > viewport.Height) Position = new Vector2(Position.X, viewport.Height - Size.Y);

        // Start animation
        Tweener.CancelAll(this);
        Size = new Vector2(Width, 0); // Start at zero height
        Tweener.To(this, (Action<Vector2>)(s => Size = s), Size, targetSize, 0.25f, Easing.EaseOutQuad);
        Tweener.To(this, (Action<float>)(o => Opacity = o), 0f, 1f, 0.2f, Easing.Linear);
    }

    public void Close() {
        if (!IsVisible) return;
        
        Tweener.CancelAll(this);
        Tweener.To(this, (System.Action<Vector2>)(s => this.Size = s), Size, new Vector2(Width, 0), 0.15f, Easing.EaseInQuad);
        Tweener.To(this, (System.Action<float>)(o => this.Opacity = o), Opacity, 0f, 0.15f, Easing.Linear)
            .OnCompleteAction(() => {
                IsVisible = false;
                if (_openedSubMenu != null) {
                    _openedSubMenu.Close();
                    // We don't remove overlay here - ContextMenu should manage its own overlay if it added one
                }
                _openedSubMenu = null;
                ClearChildren();
            });
    }

    public void CloseAll() {
        Close();
        _parentMenu?.CloseAll();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        if (!IsVisible || Opacity <= 0) return;

        DrawSelf(spriteBatch, shapeBatch);

        foreach (var child in Children) {
            if (child.Position.Y < Size.Y) {
                child.Draw(spriteBatch, shapeBatch);

                if (child is Button b && b.Tag is MenuItem mi) {
                    float opacity = AbsoluteOpacity;
                    if (!mi.IsEnabled) opacity *= 0.5f;

                    // Draw Checkbox
                    if (mi.Type == MenuItemType.Checkbox) {
                        var tex = mi.IsChecked ? GameContent.CheckboxCheckedIcon : GameContent.CheckboxIcon;
                        if (tex != null) {
                            spriteBatch.Draw(tex, new Rectangle((int)b.AbsolutePosition.X + 7, (int)b.AbsolutePosition.Y + (int)(b.Size.Y - 16) / 2, 16, 16), Color.White * opacity);
                        }
                    }

                    // Draw Icon
                    if (mi.Icon != null) {
                        spriteBatch.Draw(mi.Icon, new Rectangle((int)b.AbsolutePosition.X + 5, (int)b.AbsolutePosition.Y + 5, 20, 20), Color.White * opacity);
                    }

                    // Draw Shortcut Text
                    if (!string.IsNullOrEmpty(mi.ShortcutText)) {
                        var font = GameContent.FontSystem.GetFont(14);
                        var text = mi.ShortcutText;
                        var pos = b.AbsolutePosition + new Vector2(b.Size.X - font.MeasureString(text).X - 25, (b.Size.Y - font.MeasureString(text).Y) / 2f);
                        font.DrawText(shapeBatch, text, pos, Color.Gray * opacity);
                    }

                    // Draw arrow for submenus
                    if (mi.HasSubItems) {
                        var font = GameContent.FontSystem.GetFont(20);
                        var arrowPos = b.AbsolutePosition + new Vector2(b.Size.X - 15, (b.Size.Y - font.MeasureString(">").Y) / 2f);
                        font.DrawText(shapeBatch, ">", arrowPos, Color.White * opacity);
                    }

                }
            }
        }
    }

    public override void Update(GameTime gameTime) {
        if (!IsVisible) return;
        base.Update(gameTime);

        // Handle sub-menu opening on hover
        UIElement currentHover = null;
        foreach (var child in Children) {
            if (child.IsVisible && child.Bounds.Contains(InputManager.MousePosition)) {
                currentHover = child;
                break;
            }
        }

        if (currentHover != _hoveredItem) {
            _hoveredItem = currentHover;
            _autoOpenTimer = 0f;
            
            // If we hover away from an item that has an open sub-menu, we don't close it immediately
            // to allow the user to move the mouse into the sub-menu.
        }

        if (_hoveredItem is Button b && b.Tag is MenuItem mi && mi.HasSubItems) {
            _autoOpenTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_autoOpenTimer >= AutoOpenDelay && (_openedSubMenu == null || _openedSubMenu.Tag != mi)) {
                OpenSubMenu(b, mi);
            }
        } else if (_openedSubMenu != null) {
            // Logic to close sub-menu if we hover another item or stay out too long
            bool overSub = _openedSubMenu.IsMouseOverAnyMenu();
            if (!overSub) {
                 _subMenuCloseTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                 if (_subMenuCloseTimer >= SubMenuCloseDelay || (_hoveredItem != null && _hoveredItem is Button b2 && b2.Tag is MenuItem mi2 && !mi2.HasSubItems)) {
                     _openedSubMenu.Close();
                     _openedSubMenu = null;
                     _subMenuCloseTimer = 0;
                 }
            } else {
                _subMenuCloseTimer = 0;
            }
        }

        // Close logic
        if (_parentMenu == null && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            if (!IsMouseOverAnyMenu()) {
                 CloseAll();
            }
        }
    }

    private bool IsMouseOverAnyMenu() {
        if (Bounds.Contains(InputManager.MousePosition)) return true;
        if (_openedSubMenu != null && _openedSubMenu.IsMouseOverAnyMenu()) return true;
        return false;
    }

    private void OpenSubMenu(Button b, MenuItem item) {
        _openedSubMenu?.Close();
        _openedSubMenu = new ContextMenu();
        _openedSubMenu.Tag = item;
        
        // Position sub-menu to the right
        Vector2 subPos = b.AbsolutePosition + new Vector2(b.Size.X, -ItemPadding);
        
        // Check screen bounds for sub-menu
        var viewport = G.GraphicsDevice.Viewport;
        if (subPos.X + Width > viewport.Width) {
            subPos.X = b.AbsolutePosition.X - Width;
        }

        _openedSubMenu.Show(subPos, item.SubItems, this);
        
        // Add sub-menu to UI Manager layer? 
        // Actually, we can just draw it manually or add it to our parent.
        // For simplicity, let's assume there is a Shell.Overlay or similar.
        Shell.AddOverlayElement(_openedSubMenu);
    }
}
