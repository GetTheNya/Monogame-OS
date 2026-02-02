using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace BorderlessTestApp;

public class MainWindow : BorderlessWindow {
    public MainWindow() : base(new Vector2(200, 200), new Vector2(300, 300)) {
        BackgroundColor = new Color(0, 0, 100, 150); // Semi-transparent blue
        
        var panel = new Panel(new Vector2(20, 20), new Vector2(260, 260)) {
            BackgroundColor = new Color(50, 50, 50, 200),
            BorderColor = Color.White,
            BorderThickness = 2f
        };
        AddChild(panel);
        
        panel.AddChild(new Label(new Vector2(10, 10), "Borderless Window") {
            TextColor = Color.Yellow,
            FontSize = 20
        });
        
        panel.AddChild(new Label(new Vector2(10, 50), "This window has no chrome.\nYou can see through it.") {
            TextColor = Color.White,
            FontSize = 14
        });
        
        var closeBtn = new Button(new Vector2(10, 210), new Vector2(240, 40), "Close Test App") {
            BackgroundColor = new Color(150, 50, 50),
            HoverColor = new Color(200, 70, 70),
            OnClickAction = () => { Close(); }
        };
        panel.AddChild(closeBtn);
    }
}
