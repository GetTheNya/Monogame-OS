using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core;
using TheGame;

namespace BGTransitionTest;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        ExitOnMainWindowClose = false;
        
        var win = CreateWindow<Window>();
        win.Title = "Background Hook Test";
        win.Size = new Vector2(400, 300);
        
        // Manual layout (StackPanel is not available)
        var label = new Label(new Vector2(20, 20), "Check debug_log.txt for hook triggers");
        win.AddChild(label);
        
        var btn = new Button(new Vector2(20, 60), new Vector2(200, 40), "Go Background") {
            OnClickAction = () => GoToBackground()
        };
        win.AddChild(btn);
        
        MainWindow = win;
        OpenMainWindow();
        
        DebugLogger.Log("[BGTest] App Loaded");
    }

    protected override void OnBackground() {
        DebugLogger.Log("[BGTest] Enters Background");
        Shell.Notifications.Show("BG Test", "App is now in background");
    }

    protected override void OnForeground() {
        DebugLogger.Log("[BGTest] Enters Foreground");
        Shell.Notifications.Show("BG Test", "App is now in foreground");
    }
}
