using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using SettingsApp.Panels;

namespace SettingsApp;

public class AppSettings {
    // App-specific settings
}

public class SettingsWindow : Window {
    private AppSettings _settings;
    private TabControl _tabs;

    public static Window CreateWindow() {
        var settings = Shell.AppSettings.Load<AppSettings>();
        return new SettingsWindow(new Vector2(100, 100), new Vector2(600, 500), settings);
    }

    public SettingsWindow(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "Settings";
        AppId = "SETTINGS";
        _settings = settings;
        
        OnResize += () => {
            if (_tabs != null) _tabs.Size = ClientSize;
        };
    }
    
    protected override void OnLoad() {
        SetupUI();
    }

    private void SetupUI() {
        _tabs = new TabControl(Vector2.Zero, ClientSize);
        
        _tabs.AddTab("Personalization").Content.AddChild(CreatePersonalizationTab());
        _tabs.AddTab("System").Content.AddChild(CreateSystemTab());
        _tabs.AddTab("About").Content.AddChild(CreateAboutTab());

        AddChild(_tabs);
    }

    private Panel CreatePersonalizationTab() {
        return new PersonalizationPanel();
    }

    private Panel CreateSystemTab() {
        var p = new Panel(Vector2.Zero, Vector2.Zero);
        p.AddChild(new Label(new Vector2(10, 10), "Display Resolution") { TextColor = Color.White });
        return p;
    }

    private Panel CreateAboutTab() {
        var p = new Panel(Vector2.Zero, Vector2.Zero);
        p.AddChild(new Label(new Vector2(10, 10), "TheGame OS v1.0") { TextColor = Color.White });
        p.AddChild(new Label(new Vector2(10, 35), "Agent-built Operating System Simulator") { TextColor = Color.Gray, FontSize = 16 });
        return p;
    }
}

