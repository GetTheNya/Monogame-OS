using System;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using SettingsApp.Panels;

namespace SettingsApp;

public class SettingsApp : Application {
    public static Application Main(string[] args) {
        return new SettingsApp();
    }

    protected override void OnLoad(string[] args) {
        var win = new SettingsWindow(new Vector2(100, 100), new Vector2(600, 500));
        MainWindow = win;

        if (args != null) {
            foreach (var arg in args) {
                if (arg.Equals("--updates", StringComparison.OrdinalIgnoreCase)) {
                    win.SelectTab("Update");
                    break;
                }
            }
        }
    }
}

public class SettingsWindow : Window {
    private TabControl _tabs;

    public SettingsWindow(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "Settings";
        SetupUI();
        
        OnResize += () => {
            if (_tabs != null) _tabs.Size = ClientSize;
        };
    }
    
    public void SelectTab(string tabTitle) {
        _tabs?.SelectTab(tabTitle);
    }

    private void SetupUI() {
        _tabs = new TabControl(Vector2.Zero, ClientSize) {
            AllowCloseTabs = false
        };
        
        _tabs.AddTab("Personalization", Shell.Images.Load(@"C:\Windows\SystemResources\Icons\user.png")).Content.AddChild(CreatePersonalizationTab());
        _tabs.AddTab("System", Shell.Images.Load(@"C:\Windows\SystemResources\Icons\settings.png")).Content.AddChild(CreateSystemTab());
        _tabs.AddTab("Update", Shell.Images.Load(@"C:\Windows\SystemResources\Icons\settings.png")).Content.AddChild(new UpdatePanel());
        _tabs.AddTab("About", Shell.Images.Load(@"C:\Windows\SystemResources\Icons\PC.png")).Content.AddChild(CreateAboutTab());

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
        p.AddChild(new Label(new Vector2(10, 10), $"HentOS {SystemVersion.Current}") { TextColor = Color.White });
        return p;
    }
}

