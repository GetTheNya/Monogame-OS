using Microsoft.Xna.Framework;
using TheGame.Graphics;
using System;
using TheGame.Core.Input;
using TheGame.Core.Animation;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace TheGame.Core.UI;

public class StartMenu : Panel {
    private const float MenuItemHeight = 44f;
    private const float MenuPadding = 8f;
    private const float SidebarWidth = 64f;
    
    private Vector2 _targetPosition;
    private Vector2 _hiddenPosition;
    
    private Panel _sidebar;
    private ScrollPanel _scrollPanel;
    private float _startMenuWidth = 280f;
    private float _startMenuHeight = 450f;
    private bool _isOpen = false;

    public StartMenu(Vector2 position, Vector2 size) : base(position, size) {
        _startMenuWidth = size.X;
        _startMenuHeight = size.Y;
        
        IsVisible = false;
        BackgroundColor = new Color(20, 20, 20, 220); // Dark translucent
        BorderColor = new Color(60, 60, 60, 180);
        BorderThickness = 1;
        
        UpdateDockPosition();
        
        Opacity = 0f;

        // Sidebar Implementation
        _sidebar = new Panel(Vector2.Zero, new Vector2(SidebarWidth, _startMenuHeight)) {
            BackgroundColor = new Color(15, 15, 15, 100),
            BorderThickness = 0,
            ConsumesInput = true
        };
        AddChild(_sidebar);

        // Sidebar Profile Icon (Placeholder/Dummy)
        var profileIcon = new Button(new Vector2(MenuPadding, MenuPadding), new Vector2(SidebarWidth - MenuPadding * 2, SidebarWidth - MenuPadding * 2), "") {
            BackgroundColor = new Color(60, 60, 60, 100),
            Icon = GameContent.UserIcon, // Assume this exists or fallback
            HoverColor = new Color(80, 80, 80, 150)
        };
        _sidebar.AddChild(profileIcon);

        // Power Button at bottom of sidebar
        var powerBtn = new Button(new Vector2(MenuPadding, _startMenuHeight - SidebarWidth), new Vector2(SidebarWidth - MenuPadding * 2, SidebarWidth - MenuPadding * 2), "") {
            BackgroundColor = Color.Transparent,
            Icon = GameContent.PowerIcon, // Assume this exists or fallback
            OnClickAction = () => System.Environment.Exit(0),
            Tooltip = "Shut Down",
            HoverColor = new Color(200, 50, 50, 100)
        };
        _sidebar.AddChild(powerBtn);

        // Main Scrollable Area
        _scrollPanel = new ScrollPanel(new Vector2(SidebarWidth, 0), new Vector2(_startMenuWidth - SidebarWidth, _startMenuHeight)) {
            BackgroundColor = Color.Transparent,
            BorderThickness = 0
        };
        AddChild(_scrollPanel);
        
        RefreshItems();
    }

    public void UpdateDockPosition() {
        var viewport = G.GraphicsDevice.Viewport;
        _targetPosition = new Vector2(0, viewport.Height - 40 - _startMenuHeight);
        _hiddenPosition = new Vector2(0, viewport.Height);
        
        if (!IsVisible) {
            Position = _hiddenPosition;
        } else if (Opacity >= 0.9f) {
            Position = _targetPosition;
        }
    }

    public new void OnResize(int width, int height) {
        UpdateDockPosition();
        // Update constituent sizes if needed
        Size = new Vector2(_startMenuWidth, _startMenuHeight);
        _sidebar.Size = new Vector2(SidebarWidth, _startMenuHeight);
        _scrollPanel.Size = new Vector2(_startMenuWidth - SidebarWidth, _startMenuHeight);
        base.OnResize?.Invoke();
    }

    public void RefreshItems() {
        EnsureDefaultShortcuts();
        _scrollPanel.Children.Clear();
        
        string startMenuPath = "C:\\Users\\Admin\\Start Menu\\";
        if (!Core.OS.VirtualFileSystem.Instance.Exists(startMenuPath)) {
            Core.OS.VirtualFileSystem.Instance.CreateDirectory(startMenuPath);
        }

        var files = Core.OS.VirtualFileSystem.Instance.GetFiles(startMenuPath);
        int index = 0;
        foreach (var file in files) {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
            var icon = Shell.GetIcon(file);
            string path = file;

            AddMenuItem(index++, fileName, icon, (btn) => {
                Shell.Execute(path, btn.Bounds);
                Toggle();
            });
        }
        
        _scrollPanel.UpdateContentHeight(index * (MenuItemHeight + MenuPadding) + MenuPadding);
    }

    private void EnsureDefaultShortcuts() {
        string startMenuPath = "C:\\Users\\Admin\\Start Menu\\";
        if (!VirtualFileSystem.Instance.Exists(startMenuPath)) {
            VirtualFileSystem.Instance.CreateDirectory(startMenuPath);
        }

        string notepadLink = System.IO.Path.Combine(startMenuPath, "Notepad.slnk");
        if (!VirtualFileSystem.Instance.Exists(notepadLink)) {
            var shortcut = new Shortcut { TargetPath = "C:\\Windows\\System32\\notepad.sapp" };
            VirtualFileSystem.Instance.WriteAllText(notepadLink, shortcut.ToJson());
        }

        string explorerLink = System.IO.Path.Combine(startMenuPath, "Explorer.slnk");
        if (!VirtualFileSystem.Instance.Exists(explorerLink)) {
            var shortcut = new Shortcut { TargetPath = "C:\\Windows\\System32\\explorer.sapp" };
            VirtualFileSystem.Instance.WriteAllText(explorerLink, shortcut.ToJson());
        }
    }

    private void AddMenuItem(int index, string text, Texture2D icon, Action<Button> onClick) {
        var btn = new Button(
            new Vector2(MenuPadding, MenuPadding + index * (MenuItemHeight + MenuPadding)), 
            new Vector2(_scrollPanel.Size.X - (MenuPadding * 2), MenuItemHeight), 
            text) 
        {
            Icon = icon,
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(255, 255, 255, 25),
            TextAlign = TextAlign.Left,
            Padding = new Vector2(10, 0)
        };
        btn.OnClickAction = () => onClick(btn);
        _scrollPanel.AddChild(btn);
    }

    public void Toggle() {
        if (_isOpen) Close();
        else Open();
    }

    public void Open() {
        if (_isOpen && Opacity > 0.99f) return;
        _isOpen = true;
        Tweener.CancelAll(this);
        RefreshItems(); // Refresh on open
        IsVisible = true;
        UpdateDockPosition();
        Tweener.To(this, v => Position = v, Position, _targetPosition, 0.3f, Easing.EaseOutQuad);
        Tweener.To(this, v => Opacity = v, Opacity, 1f, 0.2f, Easing.Linear);
    }

    public void Close() {
        if (!_isOpen && Opacity < 0.01f) return;
        _isOpen = false;
        Tweener.CancelAll(this);
        Tweener.To(this, v => Position = v, Position, _hiddenPosition, 0.25f, Easing.EaseInQuad).OnComplete = () => { IsVisible = false; };
        Tweener.To(this, v => Opacity = v, Opacity, 0f, 0.2f, Easing.Linear);
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        // Background with rounded top corners
        batch.FillRectangle(absPos, Size, BackgroundColor * Opacity, rounded: 8f);
        batch.BorderRectangle(absPos, Size, BorderColor * Opacity, thickness: 1f, rounded: 8f);
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        if (_isOpen && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            if (!Bounds.Contains(InputManager.MousePosition)) {
                if (!InputManager.IsMouseConsumed) {
                    Close();
                }
            }
        }
    }
}
