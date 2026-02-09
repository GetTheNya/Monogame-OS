// Process List Window - Shows all running processes
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace ProcessTestApp;

/// <summary>
/// Displays information about all running processes.
/// </summary>
public class ProcessListWindow : Window {
    private Label _infoLabel;
    
    public ProcessListWindow() : base(new Vector2(150, 150), new Vector2(400, 300)) {
        Title = "Running Processes";
        CanResize = true;
        
        SetupUI();
    }
    
    public void SetProcessInfo(string info) {
        if (_infoLabel != null) {
            _infoLabel.Text = info;
        }
    }

    private void SetupUI() {
        _infoLabel = new Label(new Vector2(10, 10), "Loading...") { 
            TextColor = Color.White,
            FontSize = 14
        };
        AddChild(_infoLabel);
        
        var closeBtn = new Button(new Vector2(10, ClientSize.Y - 45), new Vector2(100, 35), "Close") {
            BackgroundColor = new Color(60, 60, 70),
            HoverColor = new Color(80, 80, 95)
        };
        closeBtn.OnClickAction = Close;
        AddChild(closeBtn);
    }
}
