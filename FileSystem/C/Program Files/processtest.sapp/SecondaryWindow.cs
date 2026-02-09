// Secondary Window - Created via Shell.Process.CreateWindow<T>()
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace ProcessTestApp;

/// <summary>
/// A secondary window to demonstrate multi-window process ownership.
/// Created via Shell.Process.CreateWindow<SecondaryWindow>()
/// </summary>
public class SecondaryWindow : Window {
    private static int _counter = 0;
    
    public SecondaryWindow() : base(new Vector2(200 + _counter * 30, 150 + _counter * 30), new Vector2(300, 200)) {
        _counter++;
        Title = $"Secondary Window #{_counter}";
        
        SetupUI();
    }

    private void SetupUI() {
        AddChild(new Label(new Vector2(10, 10), "This window belongs to the same process!") { 
            TextColor = Color.White 
        });
        
        var processInfo = this.OwnerProcess;
        if (processInfo != null) {
            AddChild(new Label(new Vector2(10, 35), $"Owner: {processInfo.AppId}") { 
                TextColor = Color.Gray,
                FontSize = 14
            });
            AddChild(new Label(new Vector2(10, 55), $"Total windows: {processInfo.Windows.Count}") { 
                TextColor = Color.Gray,
                FontSize = 14
            });
        }
        
        AddChild(new Label(new Vector2(10, 85), $"ShowInTaskbar: {ShowInTaskbar}") { 
            TextColor = Color.Cyan,
            FontSize = 14
        });
        
        var toggleBtn = new Button(new Vector2(10, 110), new Vector2(280, 30), "Toggle ShowInTaskbar") {
            BackgroundColor = new Color(60, 60, 70),
            HoverColor = new Color(80, 80, 95)
        };
        toggleBtn.OnClickAction = () => {
            ShowInTaskbar = !ShowInTaskbar;
            Shell.Notifications.Show("Toggled", $"ShowInTaskbar = {ShowInTaskbar}");
        };
        AddChild(toggleBtn);
        
        var closeBtn = new Button(new Vector2(10, 150), new Vector2(280, 30), "Close This Window") {
            BackgroundColor = new Color(100, 50, 50),
            HoverColor = new Color(130, 60, 60)
        };
        closeBtn.OnClickAction = Close;
        AddChild(closeBtn);
    }
}
