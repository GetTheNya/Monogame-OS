using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Linq;
using TheGame.Graphics;
using TheGame.Core.Input;

namespace TheGame.Core.UI.Controls;

public class TabPage {
    public string Title { get; set; }
    public Texture2D Icon { get; set; }
    public ScrollPanel Content { get; set; }
    public Button TabButton { get; internal set; }
}

public class TabControl : UIElement {
    private ScrollPanel _tabBar;
    private Panel _contentArea;
    private List<TabPage> _pages = new();
    private int _selectedIndex = -1;
    
    public float TabBarHeight { get; set; } = 30f;
    public Color TabBarColor { get; set; } = new Color(30, 30, 30);
    public Color ActiveTabColor { get; set; } = new Color(45, 45, 45);
    public Color HoverTabColor { get; set; } = new Color(55, 55, 55);
    public Color AccentColor { get; set; } = new Color(0, 120, 215);

    public ScrollPanel TabBar => _tabBar;
    public Panel ContentArea => _contentArea;
    public TabPage SelectedPage => (_selectedIndex >= 0 && _selectedIndex < _pages.Count) ? _pages[_selectedIndex] : null;
    
    public event Action<int> OnTabChanged;
    public event Action<int> OnTabClosed; // New event

    public TabControl(Vector2 position, Vector2 size) : base(position, size) {
        _tabBar = new ScrollPanel(Vector2.Zero, new Vector2(size.X, TabBarHeight)) {
            BackgroundColor = TabBarColor,
            BorderColor = Color.Transparent // BorderThickness doesn't exist on Panel directly in some versions
        };
        AddChild(_tabBar);

        _contentArea = new Panel(new Vector2(0, TabBarHeight), new Vector2(size.X, size.Y - TabBarHeight)) {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
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

        var btn = new Button(Vector2.Zero, new Vector2(120, TabBarHeight), title) {
            BackgroundColor = Color.Transparent,
            HoverColor = HoverTabColor,
            PressedColor = new Color(70, 70, 70),
            Icon = icon,
            BorderColor = Color.Transparent,
            FontSize = 14 // Increased from 12
        };
        
        int index = _pages.Count;
        btn.OnClickAction = () => SelectedIndex = _pages.IndexOf(page);

        // Close button
        var closeBtn = new Button(new Vector2(btn.Size.X - 22, 5), new Vector2(18, 18), "×") {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            HoverColor = Color.Red * 0.5f,
            FontSize = 14
        };
        closeBtn.OnClickAction = () => RemoveTab(_pages.IndexOf(page));
        btn.AddChild(closeBtn);
        
        page.TabButton = btn;
        _pages.Add(page);
        _tabBar.AddChild(btn);
        _contentArea.AddChild(page.Content);

        UpdateLayout(); // Recalculate horizontal positions

        if (_selectedIndex == -1) SelectedIndex = 0;
        
        return page;
    }

    public void RemoveTab(int index) {
        if (index < 0 || index >= _pages.Count) return;

        var page = _pages[index];
        _tabBar.RemoveChild(page.TabButton);
        _contentArea.RemoveChild(page.Content);
        _pages.RemoveAt(index);

        if (_selectedIndex >= _pages.Count) {
             _selectedIndex = _pages.Count - 1;
        }
        
        UpdateLayout();
        UpdateTabs();
        OnTabClosed?.Invoke(index);
    }

    private void UpdateLayout() {
        float x = 0;
        foreach (var page in _pages) {
            page.TabButton.Position = new Vector2(x, 0);
            page.TabButton.Size = new Vector2(150, TabBarHeight);
            // Re-position close button if size changed
            foreach (var child in page.TabButton.Children) {
                if (child is Button b && b.Text == "×") {
                    b.Position = new Vector2(page.TabButton.Size.X - 22, (TabBarHeight - 18) / 2);
                }
            }
            x += 150;
        }
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

    protected override void UpdateInput() {
        base.UpdateInput();

        if (InputManager.IsMouseButtonJustPressed(MouseButton.Middle)) {
             // Close tab on middle click
             foreach (var page in _pages.ToList()) {
                 if (page.TabButton.IsMouseOver) {
                     RemoveTab(_pages.IndexOf(page));
                     InputManager.IsMouseConsumed = true;
                     break;
                 }
             }
        }
    }

    public Panel GetPageContent(int index) => _pages[index].Content;

    public override void Update(GameTime gameTime) {
        // Layout maintenance
        _tabBar.Size = new Vector2(Size.X, TabBarHeight);
        _contentArea.Position = new Vector2(0, TabBarHeight);
        _contentArea.Size = new Vector2(Size.X, Size.Y - TabBarHeight);

        foreach (var page in _pages) {
            page.Content.Size = _contentArea.Size;
        }

        base.Update(gameTime);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        
        // Draw selection accent line for active tab
        if (_selectedIndex >= 0 && _selectedIndex < _pages.Count) {
            var btn = _pages[_selectedIndex].TabButton;
            var pos = btn.AbsolutePosition;
            batch.FillRectangle(new Vector2(pos.X, pos.Y + btn.Size.Y - 2), new Vector2(btn.Size.X, 2), AccentColor);
        }
    }
}
