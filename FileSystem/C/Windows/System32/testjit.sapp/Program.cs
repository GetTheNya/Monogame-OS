using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core;

namespace TestJit;

public class AppSettings {
    public int Counter { get; set; } = 0;
}

public class JitApp : Window {
    public static Window CreateWindow() {
        return new JitApp(new Vector2(100, 100), new Vector2(400, 300));
    }

    private AppSettings _settings;
    private string notifPath = "C:\\Windows\\Media\\notify.wav";

    public JitApp(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "JIT Test App";
        AppId = "TESTJIT";
    }

    protected override void OnLoad() {
        _settings = Shell.AppSettings.Load<AppSettings>(OwnerProcess);
        SetupUI();
    }

    private void SetupUI() {
        Label counterLabel = new Label(new Vector2(20, 20), $"Loaded from settings: {_settings.Counter}") {
            TextColor = Color.White,
            FontSize = 24
        };
        AddChild(counterLabel);

        var btn = new Button(new Vector2(20, 70), new Vector2(150, 40), "Increment & Save") {
            OnClickAction = () => {
                _settings.Counter++;
                counterLabel.Text = $"Counter: {_settings.Counter}";
                Shell.AppSettings.Save(OwnerProcess, _settings);
                Shell.Notifications.Show("Success", "Settings saved!", null, null);
                Shell.Audio.PlaySound(notifPath);
            }
        };
        AddChild(btn);

        Texture2D icon = Shell.Images.LoadAppImage("tray_icon.png");

        string trayIconId = "";

        var trayIcon = new TrayIcon(icon, "JIT Test App") {
            OnClick = () => {
                Shell.Notifications.Show("JIT Test App", "Clicked!", icon, null);
            }, 
            OnDoubleClick = () => {
                Shell.Notifications.Show("JIT Test App", "Double clicked!", icon, null);
            },
            OnRightClick = () => {
                Shell.Notifications.Show("JIT Test App", "Right clicked!", icon, null);
            },
            OnRightDoubleClick = () => {
                Shell.Notifications.Show("JIT Test App", "Right double clicked!", icon, null);
            },
            OnMouseWheel = (int delta) => {
                Shell.Notifications.Show("JIT Test App", "Mouse wheel!" + delta, icon, null);
            }
        };
        trayIconId = Shell.SystemTray.AddIcon(this, trayIcon);
    }
}