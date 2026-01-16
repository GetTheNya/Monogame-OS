using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using TheGame.Graphics;

namespace TheGame.Core.UI.Controls;

public class TabPage {
    public string Title { get; set; }
    public Texture2D Icon { get; set; }
    public ScrollPanel Content { get; set; }
    public Button TabButton { get; internal set; }
}

public class TabControl : UIElement {
    private Panel _sidebar;
    private Panel _contentArea;
    private List<TabPage> _pages = new();
    private int _selectedIndex = -1;
    
    public float SidebarWidth { get; set; } = 180f;
    public float SidebarVerticalOffset { get; set; } = 50f;
    public Color SidebarColor { get; set; } = new Color(30, 30, 30);
    public Color ActiveTabColor { get; set; } = new Color(50, 50, 50);
    public Color AccentColor { get; set; } = new Color(0, 120, 215);

    public Panel Sidebar => _sidebar;
    public Panel ContentArea => _contentArea;
    public event Action<int> OnTabChanged;

    public TabControl(Vector2 position, Vector2 size) : base(position, size) {
        _sidebar = new Panel(Vector2.Zero, new Vector2(SidebarWidth, size.Y)) {
            BackgroundColor = SidebarColor,
            BorderThickness = 0
        };
        AddChild(_sidebar);

        _contentArea = new Panel(new Vector2(SidebarWidth, 0), new Vector2(size.X - SidebarWidth, size.Y)) {
            BackgroundColor = Color.Transparent,
            BorderThickness = 0,
            ConsumesInput = false
        };
        AddChild(_contentArea);
    }

    public TabPage AddTab(string title, Texture2D icon = null) {
        var page = new TabPage {
            Title = title,
            Icon = icon,
            Content = new ScrollPanel(Vector2.Zero, _contentArea.Size) {
                BackgroundColor = Color.Transparent,
                BorderThickness = 0,
                IsVisible = false
            }
        };

        var btn = new Button(new Vector2(5, SidebarVerticalOffset + _pages.Count * 40), new Vector2(SidebarWidth - 10, 35), title) {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(50, 50, 50),
            PressedColor = new Color(70, 70, 70),
            Icon = icon
        };
        
        int index = _pages.Count;
        btn.OnClickAction = () => SelectedIndex = index;
        
        page.TabButton = btn;
        _pages.Add(page);
        _sidebar.AddChild(btn);
        _contentArea.AddChild(page.Content);

        if (_selectedIndex == -1) SelectedIndex = 0;
        
        return page;
    }

    public int SelectedIndex {
        get => _selectedIndex;
        set {
            if (value < 0 || value >= _pages.Count) return;
            if (_selectedIndex == value) return;

            _selectedIndex = value;
            UpdateTabs();
            OnTabChanged?.Invoke(_selectedIndex);
        }
    }

    private void UpdateTabs() {
        for (int i = 0; i < _pages.Count; i++) {
            bool isSelected = (i == _selectedIndex);
            _pages[i].Content.IsVisible = isSelected;
            
            // Visual feedback on button
            var btn = _pages[i].TabButton;
            btn.BackgroundColor = isSelected ? ActiveTabColor : Color.Transparent;
        }
    }

    public Panel GetPageContent(int index) => _pages[index].Content;

    public override void Update(GameTime gameTime) {
        // Layout maintenance BEFORE base.Update (so children get fresh sizes)
        _sidebar.Size = new Vector2(SidebarWidth, Size.Y);
        _contentArea.Position = new Vector2(SidebarWidth, 0);
        _contentArea.Size = new Vector2(Size.X - SidebarWidth, Size.Y);

        foreach (var page in _pages) {
            page.Content.Size = _contentArea.Size;
            page.TabButton.Size = new Vector2(SidebarWidth - 10, 35);
        }

        base.Update(gameTime);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        
        // Draw selection accent line for active tab
        if (_selectedIndex >= 0 && _selectedIndex < _pages.Count) {
            var btn = _pages[_selectedIndex].TabButton;
            var pos = btn.AbsolutePosition;
            batch.FillRectangle(new Vector2(pos.X, pos.Y + 5), new Vector2(3, btn.Size.Y - 10), AccentColor);
        }
    }
}
