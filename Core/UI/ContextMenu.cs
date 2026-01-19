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

public class MenuItem {
    public string Text { get; set; } = "";
    public Action Action { get; set; }
    public Texture2D Icon { get; set; }
    public string Shortcut { get; set; }
    public List<MenuItem> SubItems { get; set; }
    public bool HasSubItems => SubItems != null && SubItems.Count > 0;
}

public class ContextMenu : Panel {
    private const float ItemHeight = 30f;
    private const float ItemPadding = 5f;
    private const float Width = 200f;
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
        Opacity = 0f; // Fade in if framework supports it (AbsoluteOpacity uses it)
        
        Children.Clear();

        float yOffset = ItemPadding;
        foreach (var item in items) {
            var btn = new Button(new Vector2(ItemPadding, yOffset), new Vector2(Width - ItemPadding * 2, ItemHeight), item.Text) {
                BackgroundColor = Color.Transparent,
                HoverColor = new Color(60, 60, 60),
                Tag = item,
                OnClickAction = () => {
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
        Size = targetSize; // Temporarily set size to calculate bounds
        
        // Ensure within screen bounds (clamp to viewport)
        var viewport = G.GraphicsDevice.Viewport;
        if (Position.X + Size.X > viewport.Width) Position = new Vector2(viewport.Width - Size.X, Position.Y);
        if (Position.Y + Size.Y > viewport.Height) Position = new Vector2(Position.X, viewport.Height - Size.Y);

        // Start animation
        Tweener.CancelAll(this);
        Size = new Vector2(Width, 0); // Start at zero height
        Tweener.To(this, (Action<Vector2>)(s => Size = s), Size, targetSize, 0.25f, Easing.EaseOutQuad);
        Tweener.To(this, (Action<float>)(o => Opacity = o), 0f, 1f, 0.2f, Easing.Linear);
        
        // Ensure within screen bounds?
        // Logic to clamp to viewport could be added here.
    }

    public void Close() {
        if (!IsVisible) return;
        
        Tweener.CancelAll(this);
        Tweener.To(this, (System.Action<Vector2>)(s => this.Size = s), Size, new Vector2(Width, 0), 0.15f, Easing.EaseInQuad);
        Tweener.To(this, (System.Action<float>)(o => this.Opacity = o), Opacity, 0f, 0.15f, Easing.Linear)
            .OnCompleteAction(() => {
                IsVisible = false;
                _openedSubMenu?.Close();
                _openedSubMenu = null;
                Children.Clear();
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

                // Draw arrow for submenus
                if (child is Button b && b.Tag is MenuItem mi && mi.HasSubItems) {
                    var font = GameContent.FontSystem.GetFont(20);
                    var arrowPos = b.AbsolutePosition + new Vector2(b.Size.X - 15, (b.Size.Y - font.MeasureString(">").Y) / 2f);
                    font.DrawText(shapeBatch, ">", arrowPos, Color.White * AbsoluteOpacity);
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
