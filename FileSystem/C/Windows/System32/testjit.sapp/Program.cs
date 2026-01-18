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
        var settings = Shell.AppSettings.Load<AppSettings>();
        return new JitApp(new Vector2(100, 100), new Vector2(400, 300), settings);
    }

    private readonly string notifPath = "C:\\Windows\\Media\\notify.wav";
    private readonly AppSettings _settings;
    private readonly Label _counterLabel;
    private readonly Texture2D _icon;

    public JitApp(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "JIT Test App";
        AppId = "TESTJIT";
        _settings = settings;

        _counterLabel = new Label(new Vector2(20, 20), $"Loaded from settings: {settings.Counter}") {
            TextColor = Color.White,
            FontSize = 24
        };
        AddChild(_counterLabel);

        var btn = new Button(new Vector2(20, 70), new Vector2(150, 40), "Increment & Save") {
            OnClickAction = () => {
                _settings.Counter++;
                _counterLabel.Text = $"Counter: {_settings.Counter}";
                Shell.AppSettings.Save(_settings);
                Shell.Notifications.Show("Success", "Settings saved!", null, null);
                Shell.Audio.PlaySound(notifPath);
            }
        };
        AddChild(btn);

        _icon = Shell.Images.LoadAppImage("tray_icon.png");

        string trayIconId = "";

        var trayIcon = new TrayIcon(_icon, "JIT Test App") {
            OnClick = () => {
                Shell.Notifications.Show("JIT Test App", "Clicked!", _icon, null);
            }, 
            OnDoubleClick = () => {
                Shell.Notifications.Show("JIT Test App", "Double clicked!", _icon, null);
            },
            OnRightClick = () => {
                Shell.Notifications.Show("JIT Test App", "Right clicked!", _icon, null);
            },
            OnRightDoubleClick = () => {
                Shell.Notifications.Show("JIT Test App", "Right double clicked!", _icon, null);
            },
            OnMouseWheel = (int delta) => {
                Shell.Notifications.Show("JIT Test App", "Mouse wheel!" + delta, _icon, null);
            }
        };
        trayIconId = Shell.SystemTray.AddIcon(this, trayIcon);
    }
}