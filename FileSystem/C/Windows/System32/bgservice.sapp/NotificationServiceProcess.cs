// Notification Service Process - Demonstrates Background Processing
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;

namespace BackgroundServiceApp;

/// <summary>
/// A true background process that runs without windows and shows notifications.
/// Demonstrates:
/// - Process subclassing with OnUpdate override
/// - Background work (timer-based notifications)
/// - Creating windows from background when needed
/// - Process lifecycle management
/// </summary>
public class NotificationServiceProcess : Process {
    private double _timer = 0;
    private double _heartBeatTimer = 0;
    private double _notificationInterval = 10.0; // Every 10 seconds
    private double _heartBeatInterval = 1.0; // Every second
    private int _notificationCount = 0;
    private bool _hasShownControlPanel = false;
    
    private bool _heartBeat = false;
    private Texture2D _trayIconHeart1;
    private Texture2D _trayIconHeart2;
    private string _trayIconId = "";

    
    public NotificationServiceProcess() {
        // Set low priority since we're just a background service
        Priority = ProcessPriority.Low;
    }
    
    public override void OnStart(string[] args) {
        // Process starts in background with no windows
        State = ProcessState.Background;
        
        DebugLogger.Log($"NotificationService: Started in background (ProcessId: {ProcessId})");
        
        // Show initial notification
        Shell.Notifications.Show("Background Service", "Notification service started! You'll receive a notification every 10 seconds.");

        _trayIconHeart1 = Shell.Images.LoadAppImage(this, "heart\\heart1.png");
        _trayIconHeart2 = Shell.Images.LoadAppImage(this, "heart\\heart2.png");

        var trayIcon = new TrayIcon(_trayIconHeart1, "Service");

        _trayIconId = Shell.SystemTray.AddIcon(this, trayIcon);
    }
    
    public override void OnUpdate(GameTime gameTime) {
        // This is called every frame (or throttled based on Priority)
        // Priority.Low = ~10 times per second
        
        _timer += gameTime.ElapsedGameTime.TotalSeconds;
        _heartBeatTimer += gameTime.ElapsedGameTime.TotalSeconds;
        
        // Send a notification every 10 seconds
        if (_timer >= _notificationInterval) {
            _timer = 0;
            _notificationCount++;
            
            Shell.Notifications.Show(
                "Background Service", 
                $"Background notification #{_notificationCount} - Service is running!"
            );
            
            DebugLogger.Log($"NotificationService: Sent notification #{_notificationCount} (State: {State}, Windows: {Windows.Count})");
            
            // After 3 notifications, open a control panel window
            if (_notificationCount == 2 && !_hasShownControlPanel) {
                _hasShownControlPanel = true;
                OpenControlPanel();
            }
        }

        if (_heartBeatTimer >= _heartBeatInterval) {
            _heartBeatTimer = 0;
            _heartBeat = !_heartBeat;
            Shell.SystemTray.UpdateIcon(_trayIconId, _heartBeat ? _trayIconHeart1 : _trayIconHeart2);
        }
    }
    
    public override void OnTerminate() {
        DebugLogger.Log($"NotificationService: Terminating after {_notificationCount} notifications");
        
        // // Manually remove tray icon (should also be auto-removed by process cleanup, but this is a safeguard)
        // if (!string.IsNullOrEmpty(_trayIconId)) {
        //     Shell.SystemTray.RemoveIcon(_trayIconId);
        // }
        
        Shell.Notifications.Show("Background Service", "Service stopped.");
    }
    
    /// <summary>
    /// Creates and opens the control panel window from the background process.
    /// This demonstrates how a background process can create windows when needed.
    /// </summary>
    private void OpenControlPanel() {
        DebugLogger.Log("NotificationService: Creating control panel window");
        
        // Create a window owned by this process
        var controlPanel = CreateWindow<ServiceControlPanelWindow>();
        
        // Pass a reference to this process so the window can call our methods
        controlPanel.SetServiceProcess(this);
        
        // Open the window
        Shell.UI.OpenWindow(controlPanel);
        
        // State automatically changes to Running when we have visible windows
        DebugLogger.Log($"NotificationService: Control panel opened (State: {State})");
    }
    
    /// <summary>
    /// Public method that the control panel can call to adjust notification interval.
    /// </summary>
    public void SetNotificationInterval(double seconds) {
        _notificationInterval = seconds;
        DebugLogger.Log($"NotificationService: Interval changed to {seconds}s");
    }
    
    /// <summary>
    /// Public method to manually trigger a notification.
    /// </summary>
    public void SendTestNotification() {
        Shell.Notifications.Show("Background Service", "Manual test notification triggered!");
    }
}
