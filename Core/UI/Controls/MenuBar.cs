using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TheGame.Core.Input;
using TheGame.Core.UI; // For MenuItem
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

/// <summary>
/// Represents a single menu in the menu bar (e.g., "File", "Edit").
/// </summary>
public class Menu {
    public string Title { get; set; }
    public List<MenuItem> Items { get; set; } = new();

    public Menu(string title) {
        Title = title;
    }

    public Menu AddItem(string text, Action action, string shortcut = null) {
        Items.Add(new MenuItem { Text = text, Action = action });
        return this;
    }

    public Menu AddSeparator() {
        Items.Add(new MenuItem { Text = "---" }); // Separator marker
        return this;
    }
}

/// <summary>
/// A Windows-style menu bar control that displays horizontally across the top of a window.
/// </summary>
public class MenuBar : UIControl {
    private List<Menu> _menus = new();
    private int _activeMenuIndex = -1;
    private bool _isOpen = false;
    private Panel _dropdownPanel;

    public Color MenuTextColor { get; set; } = Color.White;
    public Color MenuHoverColor { get; set; } = new Color(60, 60, 60);
    public Color DropdownBackgroundColor { get; set; } = new Color(45, 45, 45);

    public MenuBar(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = new Color(35, 35, 35);
        BorderColor = Color.Transparent;
    }

    public MenuBar AddMenu(Menu menu) {
        _menus.Add(menu);
        return this;
    }

    public MenuBar AddMenu(string title, Action<Menu> configure) {
        var menu = new Menu(title);
        configure(menu);
        _menus.Add(menu);
        return this;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);

        // Close dropdown if clicked outside
        if (_isOpen && InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
            var dropBounds = _dropdownPanel?.Bounds ?? Rectangle.Empty;
            if (!Bounds.Contains(InputManager.MousePosition) && !dropBounds.Contains(InputManager.MousePosition)) {
                CloseDropdown();
            }
        }
    }

    protected override void UpdateInput() {
        base.UpdateInput();

        if (!IsMouseOver && !(_dropdownPanel?.IsMouseOver ?? false)) return;

        var font = GameContent.FontSystem?.GetFont(16);
        if (font == null) return;

        float x = 10;
        float padding = 15;

        for (int i = 0; i < _menus.Count; i++) {
            var menu = _menus[i];
            float textWidth = font.MeasureString(menu.Title).X;
            float itemWidth = textWidth + padding * 2;

            Rectangle itemRect = new Rectangle((int)(AbsolutePosition.X + x), (int)AbsolutePosition.Y, (int)itemWidth, (int)Size.Y);

            if (itemRect.Contains(InputManager.MousePosition)) {
                if (InputManager.IsAnyMouseButtonJustPressed(MouseButton.Left)) {
                    if (_isOpen && _activeMenuIndex == i) {
                        CloseDropdown();
                    } else {
                        OpenDropdown(i, itemRect);
                    }
                    InputManager.IsMouseConsumed = true;
                } else if (_isOpen && _activeMenuIndex != i) {
                    // Hover to switch menus when already open
                    OpenDropdown(i, itemRect);
                }
            }

            x += itemWidth;
        }
    }

    private void OpenDropdown(int index, Rectangle anchorRect) {
        _activeMenuIndex = index;
        bool wasOpen = _isOpen;
        _isOpen = true;

        // Remove old dropdown immediately if switching
        if (_dropdownPanel != null && Parent != null) {
            Parent.RemoveChild(_dropdownPanel);
        }

        var menu = _menus[index];
        var font = GameContent.FontSystem?.GetFont(16);
        if (font == null) return;

        float itemHeight = 28;
        float separatorHeight = 8;
        float dropdownWidth = 200;
        float dropdownHeight = 0;

        foreach (var item in menu.Items) {
            bool isSeparator = item.Text == "---";
            dropdownHeight += isSeparator ? separatorHeight : itemHeight;
        }

        Vector2 targetPos = new Vector2(anchorRect.X - AbsolutePosition.X + Position.X, Position.Y + Size.Y);
        Vector2 startPos = targetPos - new Vector2(0, 5); // Start slightly higher for slide effect

        _dropdownPanel = new Panel(startPos, new Vector2(dropdownWidth, dropdownHeight)) {
            BackgroundColor = DropdownBackgroundColor,
            BorderColor = new Color(80, 80, 80),
            BorderThickness = 1,
            Opacity = 0f
        };

        float y = 0;
        foreach (var item in menu.Items) {
            bool isSeparator = item.Text == "---";
            
            if (isSeparator) {
                var sep = new Panel(new Vector2(5, y + separatorHeight / 2 - 1), new Vector2(dropdownWidth - 10, 1)) {
                    BackgroundColor = new Color(80, 80, 80),
                    BorderThickness = 0
                };
                _dropdownPanel.AddChild(sep);
                y += separatorHeight;
            } else {
                var btn = new Button(new Vector2(0, y), new Vector2(dropdownWidth, itemHeight), item.Text) {
                    BackgroundColor = Color.Transparent,
                    BorderColor = Color.Transparent,
                    HoverColor = MenuHoverColor,
                    TextAlign = TextAlign.Left,
                    TextColor = MenuTextColor
                };

                var action = item.Action;
                btn.OnClickAction = () => {
                    action?.Invoke();
                    CloseDropdown();
                };

                _dropdownPanel.AddChild(btn);
                y += itemHeight;
            }
        }

        Parent?.AddChild(_dropdownPanel);

        // Animate in
        TheGame.Core.Animation.Tweener.To(_dropdownPanel, p => _dropdownPanel.Position = p, startPos, targetPos, 0.15f, TheGame.Core.Animation.Easing.EaseOutQuad);
        TheGame.Core.Animation.Tweener.To(_dropdownPanel, o => _dropdownPanel.Opacity = o, 0f, 1f, 0.15f, TheGame.Core.Animation.Easing.Linear);
    }

    private void CloseDropdown() {
        if (!_isOpen) return;
        _isOpen = false;
        _activeMenuIndex = -1;
        
        if (_dropdownPanel != null) {
            var panel = _dropdownPanel;
            _dropdownPanel = null;
            
            // Animate out then remove
            TheGame.Core.Animation.Tweener.To(panel, o => panel.Opacity = o, panel.Opacity, 0f, 0.1f, TheGame.Core.Animation.Easing.Linear)
                .OnComplete = () => { Parent?.RemoveChild(panel); };
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;

        // Background
        batch.FillRectangle(absPos, Size, BackgroundColor * AbsoluteOpacity);

        var font = GameContent.FontSystem?.GetFont(16);
        if (font == null) return;

        // Clip region for menu items (prevent draw outside MenuBar)
        var oldScissor = G.GraphicsDevice.ScissorRectangle;
        var scissor = new Rectangle((int)absPos.X, (int)absPos.Y, (int)Size.X, (int)Size.Y);
        scissor = Rectangle.Intersect(oldScissor, scissor);
        
        if (scissor.Width <= 0 || scissor.Height <= 0) return;

        batch.End();
        spriteBatch.End();
        G.GraphicsDevice.ScissorRectangle = scissor;
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, new RasterizerState { ScissorTestEnable = true });

        float x = 10;
        float padding = 15;

        for (int i = 0; i < _menus.Count; i++) {
            var menu = _menus[i];
            float textWidth = font.MeasureString(menu.Title).X;
            float itemWidth = textWidth + padding * 2;

            Rectangle itemRect = new Rectangle((int)(absPos.X + x), (int)absPos.Y, (int)itemWidth, (int)Size.Y);
            bool isHovered = itemRect.Contains(InputManager.MousePosition);
            bool isActive = _isOpen && _activeMenuIndex == i;

            if (isHovered || isActive) {
                batch.FillRectangle(new Vector2(itemRect.X, itemRect.Y), new Vector2(itemRect.Width, itemRect.Height), MenuHoverColor * AbsoluteOpacity);
            }

            Vector2 textPos = new Vector2(absPos.X + x + padding, absPos.Y + (Size.Y - font.LineHeight) / 2);
            font.DrawText(batch, menu.Title, textPos, MenuTextColor * AbsoluteOpacity);

            x += itemWidth;
        }
        
        batch.End();
        spriteBatch.End();
        G.GraphicsDevice.ScissorRectangle = oldScissor;
        batch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
    }
}
