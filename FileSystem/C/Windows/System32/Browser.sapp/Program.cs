using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;

namespace BrowserApp;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        MainWindow = CreateWindow<BrowserWindow>();
        MainWindow.Title = "Web Browser";
        MainWindow.Size = new Vector2(1000, 700);
    }
}

public class BrowserWindow : Window {
    private TextInput _urlInput;
    private BrowserControl _browser;
    private Button _backBtn;
    private Button _forwardBtn;
    private Button _refreshBtn;
    private Button _goBtn;
    private Label _statusLabel;
    private Panel _toolbarBg;
    private Panel _statusBg;

    public BrowserWindow() {
        Title = "Web Browser";
        Size = new Vector2(1000, 700);
    }

    protected override void OnLoad() {
        // --- Toolbar Section ---
        _toolbarBg = new Panel(new Vector2(0, 0), new Vector2(Size.X, 40)) {
            BackgroundColor = new Color(25, 25, 25)
        };
        AddChild(_toolbarBg);

        // Back button
        _backBtn = new Button(new Vector2(5, 5), new Vector2(60, 30), "← Back") {
            IsEnabled = false
        };
        _backBtn.OnClickAction = () => {
            _browser?.GoBack();
            UpdateNavButtons();
        };
        _toolbarBg.AddChild(_backBtn);

        // Forward button
        _forwardBtn = new Button(new Vector2(70, 5), new Vector2(80, 30), "Forward →") {
            IsEnabled = false
        };
        _forwardBtn.OnClickAction = () => {
            _browser?.GoForward();
            UpdateNavButtons();
        };
        _toolbarBg.AddChild(_forwardBtn);

        // Refresh button
        _refreshBtn = new Button(new Vector2(155, 5), new Vector2(70, 30), "⟳ Reload");
        _refreshBtn.OnClickAction = () => _browser?.Reload();
        _toolbarBg.AddChild(_refreshBtn);

        // URL Input
        _urlInput = new TextInput(new Vector2(230, 5), new Vector2(Size.X - 310, 30)) {
            Placeholder = "Enter URL (e.g., https://example.com)",
            Value = "https://google.com"
        };
        _urlInput.OnSubmit = NavigateToUrl;
        _toolbarBg.AddChild(_urlInput);

        // Go button
        _goBtn = new Button(new Vector2(Size.X - 75, 5), new Vector2(60, 30), "Go");
        _goBtn.OnClickAction = NavigateToUrl;
        _toolbarBg.AddChild(_goBtn);

        // --- Browser Control ---
        _browser = new BrowserControl(new Vector2(0, 40), new Vector2(Size.X, Size.Y - 60)) {
            Url = "https://google.com"
        };
        _browser.InitializeBrowser();
        AddChild(_browser);

        // --- Status Bar ---
        _statusBg = new Panel(new Vector2(0, Size.Y - 20), new Vector2(Size.X, 20)) {
            BackgroundColor = new Color(25, 25, 25)
        };
        AddChild(_statusBg);

        _statusLabel = new Label(new Vector2(5, Size.Y - 18), "Ready") {
            Color = Color.LightGray,
            FontSize = 12
        };
        _statusBg.AddChild(_statusLabel);

        // Handle window resize
        OnResize += HandleWindowResize;
        
        // Navigate to initial URL
        _browser.Navigate(_urlInput.Value);
    }

    private void NavigateToUrl(string url) {
        url = url.Trim();
        
        if (_statusLabel != null) {
            if (string.IsNullOrEmpty(url)) {
                _statusLabel.Text = "Please enter a URL";
                return;
            }
            _statusLabel.Text = $"Loading {url}...";
        }

        // Add http:// if no protocol specified
        if (!url.StartsWith("http://") && !url.StartsWith("https://") && !url.StartsWith("about:")) {
            url = "https://" + url;
            if (_urlInput != null) _urlInput.Value = url;
        }

        _browser?.Navigate(url);
        UpdateNavButtons();
    }

    private void NavigateToUrl() => NavigateToUrl(_urlInput?.Value ?? "");

    private void UpdateNavButtons() {
        if (_backBtn != null) _backBtn.IsEnabled = true; 
        if (_forwardBtn != null) _forwardBtn.IsEnabled = true; 
    }

    private void HandleWindowResize() {
        // Update toolbar
        if (_toolbarBg != null) {
            _toolbarBg.Size = new Vector2(Size.X, 40);
        }
        
        // Update URL input width
        if (_urlInput != null) _urlInput.Size = new Vector2(Size.X - 310, 30);
        
        // Update Go button position
        if (_goBtn != null) _goBtn.Position = new Vector2(Size.X - 75, 5);

        // Update browser size
        if (_browser != null) _browser.Size = new Vector2(Size.X, Size.Y - 60);

        // Update status bar
        if (_statusBg != null) {
            _statusBg.Position = new Vector2(0, Size.Y - 20);
            _statusBg.Size = new Vector2(Size.X, 20);
        }
        if (_statusLabel != null) _statusLabel.Position = new Vector2(5, Size.Y - 18);
    }
}
