// Modal Dialog Window - Demonstrates blocking modal dialogs
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace ProcessTestApp;

/// <summary>
/// A modal dialog that blocks input to its parent window.
/// Opened via Shell.Process.ShowModal()
/// </summary>
public class ModalDialogWindow : Window {
    private Label _parentLabel;
    
    public ModalDialogWindow() : base(Vector2.Zero, new Vector2(350, 180)) {
        Title = "Modal Dialog";
        CanResize = false;
        
        // Center on screen
        var vp = TheGame.G.GraphicsDevice.Viewport;
        Position = new Vector2((vp.Width - Size.X) / 2, (vp.Height - Size.Y) / 2);
        
        // Note: IsModal is set automatically by Shell.Process.ShowModal()
        // But we can also set it manually:
        // IsModal = true;
        // ShowInTaskbar = false;
        
        SetupUI();
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update parent label dynamically
        if (_parentLabel != null) {
            _parentLabel.Text = $"ParentWindow: {ParentWindow?.Title ?? "none"}";
        }
    }

    private void SetupUI() {
        AddChild(new Label(new Vector2(10, 10), "This is a MODAL dialog!") { 
            TextColor = Color.Yellow,
            FontSize = 18
        });
        
        AddChild(new Label(new Vector2(10, 40), "The parent window is now BLOCKED.") { 
            TextColor = Color.White 
        });
        AddChild(new Label(new Vector2(10, 60), "Try clicking the main window - it won't respond.") { 
            TextColor = Color.Gray,
            FontSize = 14
        });
        
        _parentLabel = new Label(new Vector2(10, 90), "ParentWindow: (checking...)") { 
            TextColor = Color.Cyan,
            FontSize = 14
        };
        AddChild(_parentLabel);
        
        var okBtn = new Button(new Vector2(125, 125), new Vector2(100, 35), "OK") {
            BackgroundColor = new Color(0, 100, 180),
            HoverColor = new Color(0, 130, 220)
        };
        okBtn.OnClickAction = () => {
            Shell.Notifications.Show("Modal Closed", "Parent window is now unblocked!");
            Close();
        };
        AddChild(okBtn);
    }
}
