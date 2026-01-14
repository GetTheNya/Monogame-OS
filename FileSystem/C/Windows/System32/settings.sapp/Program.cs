using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace SettingsApp;

public class AppSettings {
    public int SelectedIndex { get; set; } = 0;
    public float WindowX { get; set; } = 200;
    public float WindowY { get; set; } = 200;
    public float WindowWidth { get; set; } = 600;
    public float WindowHeight { get; set; } = 500;
    public bool IsMaximized { get; set; } = false;
}

public class SettingsWindow : Window {
    public static Window CreateWindow() {
        var settings = Shell.LoadSettings<AppSettings>();
        var win = new SettingsWindow(new Vector2(settings.WindowX, settings.WindowY), new Vector2(settings.WindowWidth, settings.WindowHeight), settings);
        if (settings.IsMaximized) {
            win.SetMaximized(true, new Rectangle(0, 0, G.GraphicsDevice.Viewport.Width, G.GraphicsDevice.Viewport.Height - 40));
        }
        return win;
    }

    private TabControl _tabs;
    private AppSettings _settings;

    public SettingsWindow(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "Settings";
        _settings = settings;
        
        OnResize += () => {
            if (IsMaximized) {
                _settings.WindowWidth = RestoreBounds.Width;
                _settings.WindowHeight = RestoreBounds.Height;
            } else {
                _settings.WindowWidth = Size.X;
                _settings.WindowHeight = Size.Y;
            }
            _settings.IsMaximized = IsMaximized;
            Shell.SaveSettings(_settings);
        };

        OnMove += () => {
            if (Opacity < 0.9f) return;
            if (IsMaximized) {
                _settings.WindowX = RestoreBounds.X;
                _settings.WindowY = RestoreBounds.Y;
            } else {
                _settings.WindowX = Position.X;
                _settings.WindowY = Position.Y;
            }
            _settings.IsMaximized = IsMaximized;
            Shell.SaveSettings(_settings);
        };

        SetupUI();
    }

    private void SetupUI() {
        _tabs = new TabControl(Vector2.Zero, ClientSize) {
            SidebarVerticalOffset = 60f
        };
        AddChild(_tabs);

        // Sidebar title
        var sidebarTitle = new Label(new Vector2(15, 15), "Settings") { FontSize = 22, Color = Color.White };
        _tabs.Sidebar.AddChild(sidebarTitle);

        CreateSections();
        _tabs.SelectedIndex = _settings.SelectedIndex;
        _tabs.OnTabChanged += (idx) => {
            _settings.SelectedIndex = idx;
            Shell.SaveSettings(_settings);
        };
    }

    private void CreateSections() {
        var padding = 25f;

        // --- System Section ---
        var systemPage = _tabs.AddTab("System");
        var systemPanel = systemPage.Content;
        systemPanel.AddChild(new Label(new Vector2(padding, padding), "System") { FontSize = 28, Color = Color.White });
        
        float currentY = padding + 50;
        
        // Sound
        systemPanel.AddChild(new Label(new Vector2(padding, currentY), "Sound") { FontSize = 18, Color = Color.LightGray });
        currentY += 30;
        systemPanel.AddChild(new Label(new Vector2(padding, currentY), "Master Volume") { FontSize = 14, Color = Color.Gray });
        currentY += 25;
        var volSlider = new Slider(new Vector2(padding, currentY), 300) { Value = 0.75f };
        systemPanel.AddChild(volSlider);
        currentY += 45;

        // Display
        systemPanel.AddChild(new Label(new Vector2(padding, currentY), "Display") { FontSize = 18, Color = Color.LightGray });
        currentY += 30;
        systemPanel.AddChild(new Label(new Vector2(padding, currentY), "Brightness") { FontSize = 14, Color = Color.Gray });
        currentY += 25;
        systemPanel.AddChild(new Slider(new Vector2(padding, currentY), 300) { Value = 0.8f });
        currentY += 45;

        systemPanel.AddChild(new Label(new Vector2(padding, currentY), "Resolution") { FontSize = 14, Color = Color.Gray });
        currentY += 25;
        var resCombo = new ComboBox(new Vector2(padding, currentY), new Vector2(250, 32));
        resCombo.Items.AddRange(new[] { "1920 x 1080 (Recommended)", "1600 x 900", "1280 x 720", "1024 x 768" });
        resCombo.Value = 0;
        systemPanel.AddChild(resCombo);

        // --- Devices ---
        var devicesPage = _tabs.AddTab("Devices");
        devicesPage.Content.AddChild(new Label(new Vector2(padding, padding), "Devices") { FontSize = 28, Color = Color.White });
        devicesPage.Content.AddChild(new Label(new Vector2(padding, padding + 60), "Bluetooth, Printers, Mouse") { FontSize = 16, Color = Color.Gray });

        // --- Personalization Section ---
        var personalPage = _tabs.AddTab("Personalization");
        var personalPanel = personalPage.Content;
        personalPanel.AddChild(new Label(new Vector2(padding, padding), "Personalization") { FontSize = 28, Color = Color.White });
        
        currentY = padding + 50;
        personalPanel.AddChild(new Label(new Vector2(padding, currentY), "Colors") { FontSize = 18, Color = Color.LightGray });
        currentY += 40;
        
        personalPanel.AddChild(new Switch(new Vector2(padding, currentY), "Dark Mode") { Value = true });
        currentY += 40;
        personalPanel.AddChild(new Switch(new Vector2(padding, currentY), "Transparency Effects") { Value = true });
        currentY += 40;
        personalPanel.AddChild(new Switch(new Vector2(padding, currentY), "Accent Color on Taskbar") { Value = false });
        
        currentY += 60;
        personalPanel.AddChild(new Label(new Vector2(padding, currentY), "Background Theme") { FontSize = 18, Color = Color.LightGray });
        currentY += 30;
        var themeCombo = new ComboBox(new Vector2(padding, currentY), new Vector2(250, 32));
        themeCombo.Items.AddRange(new[] { "Windows Default", "Night Sky", "Mountains", "Abstract Art", "Solid Color" });
        themeCombo.Value = 1;
        personalPanel.AddChild(themeCombo);

        // --- Apps ---
        var appsPage = _tabs.AddTab("Apps");
        appsPage.Content.AddChild(new Label(new Vector2(padding, padding), "Apps") { FontSize = 28, Color = Color.White });
        appsPage.Content.AddChild(new Label(new Vector2(padding, padding + 60), "Installed Apps, Default Apps") { FontSize = 16, Color = Color.Gray });

        // --- Accounts Section ---
        var accountsPage = _tabs.AddTab("Accounts");
        var accountsPanel = accountsPage.Content;
        accountsPanel.AddChild(new Label(new Vector2(padding, padding), "Accounts") { FontSize = 28, Color = Color.White });
        
        currentY = padding + 50;
        accountsPanel.AddChild(new Label(new Vector2(padding, currentY), "User Information") { FontSize = 18, Color = Color.LightGray });
        currentY += 40;
        accountsPanel.AddChild(new Label(new Vector2(padding, currentY), "Administrator") { FontSize = 16, Color = Color.White });
        currentY += 25;
        accountsPanel.AddChild(new Label(new Vector2(padding, currentY), "local@user") { FontSize = 14, Color = Color.Gray });
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (_tabs != null) {
            _tabs.Size = ClientSize;
        }
    }
}
