using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace ConflictResolver;

public class Program : Application {
    public static Application Main(string[] args) {
        return new Program();
    }

    protected override void OnLoad(string[] args) {
        if (args == null || args.Length == 0) {
            var mb = new MessageBox("Error!", "This application should not be called manually!");
            Shell.UI.OpenWindow(mb);
            return;
        }
        string filePath = args[0];
        MainWindow = new ConflictResolverWindow(new Vector2(0, 0), new Vector2(400, 450), filePath);
    }
}

public class ConflictResolverWindow : Window {
    private string _filePath;
    private string _extension;
    private List<string> _handlers;
    private string _selectedAppId;
    
    private ScrollPanel _listArea;
    private Button _justOnceBtn;
    private Button _alwaysBtn;
    private Label _fileLabel;

    public ConflictResolverWindow(Vector2 pos, Vector2 size, string filePath) : base(pos, size) {
        _filePath = filePath;
        _extension = System.IO.Path.GetExtension(filePath).ToLower();
        Title = "Open with...";
        CanResize = false;
        
        // Center the window
        var viewport = G.GraphicsDevice.Viewport;
        Position = new Vector2((viewport.Width - size.X) / 2f, (viewport.Height - size.Y) / 2f);
    }

    protected override void OnLoad() {
        _handlers = Shell.File.GetFileTypeHandlers(_extension);
        
        SetupUI();
    }

    private void SetupUI() {
        // Description label
        var descLabel = new Label(new Vector2(10, 10), "How do you want to open this file?") {
            FontSize = 18
        };
        AddChild(descLabel);

        _fileLabel = new Label(new Vector2(10, 35), System.IO.Path.GetFileName(_filePath)) {
            FontSize = 16,
            TextColor = Color.Gray
        };
        AddChild(_fileLabel);

        // List area
        _listArea = new ScrollPanel(new Vector2(10, 65), new Vector2(ClientSize.X - 20, ClientSize.Y - 120)) {
            BackgroundColor = new Color(40, 40, 40),
            BorderColor = new Color(60, 60, 60),
            BorderThickness = 1
        };
        AddChild(_listArea);

        RefreshAppList();

        // Footer buttons
        _alwaysBtn = new Button(new Vector2(ClientSize.X - 110, ClientSize.Y - 45), new Vector2(100, 35), "Always") {
            OnClickAction = () => HandleSelection(true),
            IsEnabled = false,
            // Accent color for primary action
            BackgroundColor = new Color(0, 120, 215),
            HoverColor = new Color(0, 140, 230),
            PressedColor = new Color(0, 100, 180),
            BorderColor = Color.White * 0.3f,
            UseBoldFont = true
        };
        AddChild(_alwaysBtn);

        _justOnceBtn = new Button(new Vector2(ClientSize.X - 220, ClientSize.Y - 45), new Vector2(100, 35), "Just once") {
            OnClickAction = () => HandleSelection(false),
            IsEnabled = false
        };
        AddChild(_justOnceBtn);
    }

    private void RefreshAppList() {
        _listArea.ClearChildren();
        float y = 5;

        foreach (var appId in _handlers) {
            string currentAppId = appId; // Capture for lambda
            string appName = AppLoader.Instance.GetAppName(appId);
            
            var btn = new Button(new Vector2(5, y), new Vector2(_listArea.ClientSize.X - 10, 45), appName) {
                TextAlign = TextAlign.Left,
                BackgroundColor = Color.Transparent,
                Icon = Shell.UI.GetAppIcon(appId)
            };
            
            btn.OnClickAction = () => {
                _selectedAppId = currentAppId;
                _justOnceBtn.IsEnabled = true;
                _alwaysBtn.IsEnabled = true;
                
                // Highlight selection
                foreach (var child in _listArea.Children) {
                    if (child is Button b) {
                        b.BackgroundColor = Color.Transparent;
                        b.BorderColor = Color.Gray * 0.5f;
                    }
                }
                btn.BackgroundColor = new Color(0, 120, 215, 60); // Subtle blue highlight
                btn.BorderColor = Color.White; // Prominent white border
            };
            
            _listArea.AddChild(btn);
            y += 50;
        }
    }

    private void HandleSelection(bool setAsDefault) {
        if (string.IsNullOrEmpty(_selectedAppId)) return;

        if (setAsDefault) {
            Shell.File.SetDefaultFileTypeHandler(_extension, _selectedAppId);
        }

        // Launch the app
        ProcessManager.Instance.StartProcess(_selectedAppId, new[] { _filePath }, null, null);
        
        Close();
    }
}
