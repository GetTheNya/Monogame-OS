// Service Control Panel Window
using System;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace BackgroundServiceApp;

/// <summary>
/// Control panel window for the background notification service.
/// Created FROM the background process to allow user control.
/// </summary>
public class ServiceControlPanelWindow : Window {
    private NotificationServiceProcess _serviceProcess;
    private Label _statusLabel;
    
    public ServiceControlPanelWindow() : base(new Vector2(200, 200), new Vector2(400, 300)) {
        Title = "Background Service Control";
        CanResize = false;
        
        SetupUI();
    }
    
    public void SetServiceProcess(NotificationServiceProcess process) {
        _serviceProcess = process;
        UpdateStatus();
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        UpdateStatus();
    }
    
    private void UpdateStatus() {
        if (_serviceProcess != null && _statusLabel != null) {
            _statusLabel.Text = $"Process State: {_serviceProcess.State}\n" +
                               $"Windows: {_serviceProcess.Windows.Count}\n" +
                               $"Priority: {_serviceProcess.Priority}";
        }
    }
    
    private void SetupUI() {
        float y = 10;
        
        AddChild(new Label(new Vector2(10, y), "Background Service Control Panel") { 
            TextColor = Color.Yellow,
            FontSize = 18
        });
        y += 30;
        
        AddChild(new Label(new Vector2(10, y), "This window was created BY the background process!") { 
            TextColor = Color.White,
            FontSize = 14
        });
        y += 25;
        
        _statusLabel = new Label(new Vector2(10, y), "Status: Loading...") { 
            TextColor = Color.Cyan,
            FontSize = 14
        };
        AddChild(_statusLabel);
        y += 70;
        
        // Interval controls
        AddChild(new Label(new Vector2(10, y), "Notification Interval:") { 
            TextColor = Color.White 
        });
        y += 25;
        
        AddButton("5 seconds", new Vector2(10, y), new Vector2(120, 30), () => {
            _serviceProcess?.SetNotificationInterval(5.0);
            Shell.Notifications.Show("Control Panel", "Interval set to 5 seconds");
        });
        
        AddButton("10 seconds", new Vector2(140, y), new Vector2(120, 30), () => {
            _serviceProcess?.SetNotificationInterval(10.0);
            Shell.Notifications.Show("Control Panel", "Interval set to 10 seconds");
        });
        
        AddButton("20 seconds", new Vector2(270, y), new Vector2(120, 30), () => {
            _serviceProcess?.SetNotificationInterval(20.0);
            Shell.Notifications.Show("Control Panel", "Interval set to 20 seconds");
        });
        y += 40;
        
        // Manual notification
        AddButton("Send Test Notification", new Vector2(10, y), new Vector2(380, 35), () => {
            _serviceProcess?.SendTestNotification();
        });
        y += 45;
        
        // Stop service
        AddButton("Stop Service (closes all windows + terminates)", new Vector2(10, y), new Vector2(380, 35), () => {
            Shell.Notifications.Show("Control Panel", "Stopping service...");
            _serviceProcess?.Terminate();
        });
    }
    
    private void AddButton(string text, Vector2 pos, Vector2 size, Action onClick) {
        var btn = new Button(pos, size, text) {
            BackgroundColor = new Color(60, 60, 70),
            HoverColor = new Color(80, 80, 95)
        };
        btn.OnClickAction = onClick;
        AddChild(btn);
    }
}
