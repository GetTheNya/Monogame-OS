using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Graphics;
using TheGame.Core.UI.Controls;
using TheGame.Core;
using TheGame;

namespace UnifiedTestApp;

/// <summary>
/// Comprehensive test app demonstrating all new Application API features:
/// - Application lifecycle: OnLoad, OnUpdate, OnDraw, OnClose
/// - Window lifecycle: OnLoad, OnUpdate, OnDraw
/// - ExitOnMainWindowClose (background mode)
/// - Priority (process throttling)
/// - ShowModal<T> helper
/// - UIElement.Name and GetChild<T> methods
/// </summary>
public class Program : Application {
    public static Application Main(string[] args) => new Program();

    private float _timer = 0;
    private int _updateCount = 0;
    private bool _inBackground = false;

    protected override void OnLoad(string[] args) {
        DebugLogger.Log("UnifiedTestApp: OnLoad called!");
        
        // Configure app behavior
        ExitOnMainWindowClose = false;  // Allow background mode when main window closes
        Priority = ProcessPriority.Normal;  // Can throttle with Low/VeryLow
        
        // Just set MainWindow - ProcessManager opens it with animation from icon
        MainWindow = CreateWindow<TestWindow>();
        MainWindow.Title = "Unified API Test";
        
        // Secondary window opened directly
        var secondary = CreateWindow<SecondaryWindow>();
        secondary.Title = "Secondary Window";
        secondary.Position = new Vector2(100, 100);
        secondary.Size = new Vector2(350, 250);
        OpenWindow(secondary);
    }

    protected override void OnUpdate(GameTime gameTime) {
        _timer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        _updateCount++;

        // Check if we went to background
        if (!_inBackground && Windows.Count == 0) {
            _inBackground = true;
            Shell.Notifications.Show("Unified Test", "App is now in background! Will notify every 10 seconds.");
        }

        // Periodic notification (demonstrates background processing)
        if (_timer >= 10.0f) {
            _timer = 0;
            if (_inBackground) {
                Shell.Notifications.Show("Background Service", 
                    $"Still running! Updates: {_updateCount}\nClick tray to restore.");
            }
        }
    }

    protected override void OnDraw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        // Global overlay showing update count
        shapeBatch.Begin();
        shapeBatch.FillRectangle(new Vector2(10, 50), new Vector2(220, 30), new Color(0, 0, 0, 180));
        shapeBatch.End();

        spriteBatch.Begin();
        var font = GameContent.FontSystem.GetFont(18);
        font.DrawText(spriteBatch, $"App Updates: {_updateCount} | BG: {_inBackground}", new Vector2(15, 55), Color.Lime);
        spriteBatch.End();
    }

    protected override void OnClose() {
        DebugLogger.Log("UnifiedTestApp: OnClose called!");
        Shell.Notifications.Show("Unified Test", "App terminated.");
    }
}

/// <summary>
/// Main test window demonstrating Window lifecycle and element finding.
/// </summary>
public class TestWindow : Window {
    private Label _statusLabel;
    private int _windowUpdates = 0;
    
    public TestWindow() {
        Title = "Test Window";
        Size = new Vector2(450, 350);
    }
    
    protected override void OnLoad() {
        // Header
        AddChild(new Label(new Vector2(20, 10), "Application API Test") { 
            Name = "Header",
            Color = Color.White, 
            FontSize = 24 
        });
        
        // Status label - will update in OnUpdate
        _statusLabel = new Label(new Vector2(20, 50), "Window updates: 0") { 
            Name = "StatusLabel",
            Color = Color.LightGray 
        };
        AddChild(_statusLabel);
        
        // --- Test buttons panel ---
        var buttonPanel = new Panel(new Vector2(20, 80), new Vector2(400, 200)) {
            Name = "ButtonPanel",
            BackgroundColor = new Color(40, 40, 40, 150)
        };
        AddChild(buttonPanel);
        
        // ShowModal<T> test
        var modalBtn = new Button(new Vector2(10, 10), new Vector2(180, 30), "ShowModal<T>") { 
            Name = "ModalButton" 
        };
        modalBtn.OnClickAction = () => {
            OwnerProcess.Application.ShowModal<SettingsDialog>(dialog => {
                dialog.Title = "Settings (via ShowModal<T>)";
            });
        };
        buttonPanel.AddChild(modalBtn);
        
        // GetChild<T> test
        var findBtn = new Button(new Vector2(10, 50), new Vector2(180, 30), "Test GetChild<T>") { 
            Name = "FindButton" 
        };
        findBtn.OnClickAction = () => {
            // Find by type
            var header = this.GetChild<Label>("Header");
            
            // Find by path
            var modalBtnByPath = this.GetChildByPath<Button>("ButtonPanel/ModalButton");
            
            // Find all buttons
            var allButtons = this.GetChildren<Button>();
            
            Shell.Notifications.Show("GetChild Test", 
                $"Header: {header?.Name ?? "null"}\n" +
                $"Path find: {modalBtnByPath?.Text ?? "null"}\n" +
                $"Total buttons: {allButtons.Count}");
        };
        buttonPanel.AddChild(findBtn);
        
        // Background mode test
        var bgBtn = new Button(new Vector2(10, 90), new Vector2(180, 30), "Go to Background") { 
            Name = "BackgroundButton" 
        };
        bgBtn.OnClickAction = () => {
            OwnerProcess.Application.GoToBackground();
        };
        buttonPanel.AddChild(bgBtn);
        
        // Priority test
        var priorityBtn = new Button(new Vector2(10, 130), new Vector2(180, 30), "Toggle Priority") { 
            Name = "PriorityButton" 
        };
        priorityBtn.OnClickAction = () => {
            var app = OwnerProcess.Application;
            app.Priority = app.Priority == ProcessPriority.Normal 
                ? ProcessPriority.Low 
                : ProcessPriority.Normal;
            Shell.Notifications.Show("Priority", $"Now: {app.Priority}");
        };
        buttonPanel.AddChild(priorityBtn);
        
        // Exit button
        var exitBtn = new Button(new Vector2(200, 10), new Vector2(180, 30), "Exit App") { 
            Name = "ExitButton",
            BackgroundColor = new Color(150, 50, 50)
        };
        exitBtn.OnClickAction = () => {
            OwnerProcess.Application.Exit();
        };
        buttonPanel.AddChild(exitBtn);
    }
    
    protected override void OnUpdate(GameTime gameTime) {
        _windowUpdates++;
        if (_statusLabel != null && _windowUpdates % 60 == 0) {
            _statusLabel.Text = $"Window updates: {_windowUpdates}";
        }
    }
    
    protected override void OnDraw(SpriteBatch spriteBatch, ShapeBatch batch) {
        // Custom line separator in window content area
        // Note: batch is already Begin'd by framework - don't call Begin/End here
        batch.FillLine(new Vector2(10, 290), new Vector2(430, 290), 1f, Color.Gray);
    }
}

/// <summary>
/// Secondary window to test multi-window behavior.
/// </summary>
public class SecondaryWindow : Window {
    public SecondaryWindow() {
        Title = "Secondary";
        Size = new Vector2(300, 200);
    }
    
    protected override void OnLoad() {
        AddChild(new Label(new Vector2(20, 20), "I'm a secondary window") { Color = Color.White });
        AddChild(new Label(new Vector2(20, 50), "Close me - main window stays open") { 
            Color = Color.LightGray, 
            FontSize = 14 
        });
    }
}

/// <summary>
/// Modal dialog to test ShowModal<T> pattern.
/// </summary>
public class SettingsDialog : Window {
    public SettingsDialog() {
        Title = "Settings";
        Size = new Vector2(400, 300);
        CanResize = false;
    }
    
    protected override void OnLoad() {
        AddChild(new Label(new Vector2(20, 20), "This is a modal dialog") { Color = Color.White });
        AddChild(new Label(new Vector2(20, 50), "Created via ShowModal<T>") { Color = Color.LightGray });
        
        var slider = new Slider(new Vector2(20, 90), 200f) { 
            Name = "VolumeSlider",
            Value = 0.7f 
        };
        AddChild(slider);
        AddChild(new Label(new Vector2(230, 90), "Volume") { Color = Color.Gray });
        
        var closeBtn = new Button(new Vector2(150, 130), new Vector2(100, 30), "Close");
        closeBtn.OnClickAction = Close;
        AddChild(closeBtn);
    }
}
