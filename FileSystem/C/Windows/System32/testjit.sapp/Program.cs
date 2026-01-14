using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;

namespace JitTestApp;

public class AppSettings {
    public int ClickCount { get; set; } = 0;
    public float WindowX { get; set; } = 250;
    public float WindowY { get; set; } = 200;
    public float WindowWidth { get; set; } = 400;
    public float WindowHeight { get; set; } = 300;
}

public class JitApp : Window {
    public static Window CreateWindow() {
        var settings = Shell.LoadSettings<AppSettings>();
        return new JitApp(new Vector2(settings.WindowX, settings.WindowY), new Vector2(settings.WindowWidth, settings.WindowHeight), settings);
    }

    private readonly string notifPath = Shell.GetAppResourcePath("notif.wav");
    private AppSettings _settings;
    private Label _clickLabel;
    private int _notifCount = 0;

    public JitApp(Vector2 position, Vector2 size, AppSettings settings) : base(position, size) {
        Title = "JIT Compiled App!";
        _settings = settings;

        OnResize += () => {
            _settings.WindowWidth = Size.X;
            _settings.WindowHeight = Size.Y;
            Shell.SaveSettings(_settings);
        };

        OnMove += () => {
            if (Opacity < 0.9f) return;
            _settings.WindowX = Position.X;
            _settings.WindowY = Position.Y;
            Shell.SaveSettings(_settings);
        };

        SetupUI();
    }

    private void SetupUI() {
        var label = new Label(new Vector2(20, 20), "This app persists its settings!") { Color = Color.LightGray };
        AddChild(label);

        _clickLabel = new Label(new Vector2(20, 50), $"Total Clicks: {_settings.ClickCount}") { FontSize = 22, Color = Color.White };
        AddChild(_clickLabel);
        
        var button = new Button(new Vector2(20, 100), new Vector2(150, 35), "Click Me!") {
            BackgroundColor = new Color(0, 120, 215)
        };
        button.OnClickAction = () => {
            _settings.ClickCount++;
            _clickLabel.Text = $"Total Clicks: {_settings.ClickCount}";
            Title = $"JIT Compiled App! {_settings.ClickCount}";
            Shell.SaveSettings(_settings);
            Shell.PlaySound(notifPath);
        };
        AddChild(button);

        var resetBtn = new Button(new Vector2(180, 100), new Vector2(100, 35), "Reset") {
            BackgroundColor = new Color(80, 80, 80)
        };
        resetBtn.OnClickAction = () => {
            _settings.ClickCount = 0;
            Title = $"JIT Compiled App! {_settings.ClickCount}";
            _clickLabel.Text = $"Total Clicks: {_settings.ClickCount}";
            Shell.SaveSettings(_settings);
        };
        AddChild(resetBtn);

        var notifyBtn = new Button(new Vector2(20, 150), new Vector2(150, 35), "Notify") {
            BackgroundColor = new Color(0, 120, 215)
        };
        notifyBtn.OnClickAction = () => {
            _notifCount++;
            Shell.ShowNotification("Notification", $"This is a notification! {_notifCount}", null, null, null);
        };
        AddChild(notifyBtn);
    }
}
