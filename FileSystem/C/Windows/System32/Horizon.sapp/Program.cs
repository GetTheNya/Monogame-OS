using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using System.Linq;
using System.Threading.Tasks;

namespace BrowserApp;

public static class Icons {
    public const string Refresh = "\uf01e";
    public const string ArrowLeft = "\uf060"; 
    public const string ArrowRight = "\uf061";
}

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        MainWindow = CreateWindow<BrowserWindow>();
        MainWindow.Title = "Horizon";
        MainWindow.Size = new Vector2(1000, 700);

        Shell.Network.RegisterForNetwork(Process);
        Shell.Media.RegisterAsPlayer(Process);
    }
}

public class BrowserWindow : Window {
    private Panel _toolbarBg;
    private TabControl _tabControl;
    private TextInput _urlInput;
    private Button _backBtn;
    private Button _forwardBtn;
    private Button _refreshBtn;
    private Button _newTabBtn;
    
    private readonly Color _themeColor = new Color(25, 25, 25);
    private readonly Color _accentColor = new Color(0, 120, 215);

    public BrowserWindow() {
        Title = "Horizon";
        Size = new Vector2(1000, 750);
        BackgroundColor = _themeColor;
    }

    protected override void OnLoad() {
        // --- Tab Control ---
        _tabControl = new TabControl(new Vector2(0, 0), new Vector2(Size.X, Size.Y)) {
            TabBarHeight = 30,
            TabBarColor = _themeColor,
            ActiveTabColor = new Color(45, 45, 45),
            AccentColor = _accentColor
        };
        _tabControl.OnTabChanged += (index) => SyncUIWithActiveTab();
        _tabControl.OnTabClosed += (index, page) => {
            var browser = page.Content.Children.OfType<BrowserControl>().FirstOrDefault();
            browser?.Dispose();
            
            HandleWindowResize();
            if (_tabControl.Pages.Count == 0) Close();
        };
        AddChild(_tabControl);

        OnClosed += () => {
            foreach (var page in _tabControl.Pages) {
                var browser = page.Content.Children.OfType<BrowserControl>().FirstOrDefault();
                browser?.Dispose();
            }
        };

        // --- Toolbar Section (Add AFTER TabControl to draw on top) ---
        _toolbarBg = new Panel(new Vector2(0, 30), new Vector2(Size.X, 45)) {
            BackgroundColor = _themeColor
        };
        AddChild(_toolbarBg);

        _backBtn = CreateNavButton(new Vector2(10, 7), Icons.ArrowLeft, () => GetActiveBrowser()?.GoBack());
        _forwardBtn = CreateNavButton(new Vector2(45, 7), Icons.ArrowRight, () => GetActiveBrowser()?.GoForward());
        _refreshBtn = CreateNavButton(new Vector2(85, 7), Icons.Refresh, () => GetActiveBrowser()?.Reload());
        
        _toolbarBg.AddChild(_backBtn);
        _toolbarBg.AddChild(_forwardBtn);
        _toolbarBg.AddChild(_refreshBtn);

        _urlInput = new TextInput(new Vector2(125, 7), new Vector2(Size.X - 180, 30)) {
            Placeholder = "Search or enter address",
            BackgroundColor = new Color(40, 40, 40),
            BorderColor = Color.Transparent
        };
        _urlInput.OnSubmit = (val) => Navigate(val);
        _toolbarBg.AddChild(_urlInput);

        // New Tab button in Tab Bar
        _newTabBtn = new Button(Vector2.Zero, new Vector2(30, 30), "+") {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            HoverColor = new Color(60, 60, 60),
            FontSize = 20
        };
        _newTabBtn.OnClickAction = () => AddNewTab("https://google.com");
        _tabControl.TabBar.AddChild(_newTabBtn);

        // Initial Tab
        AddNewTab("https://google.com");

        OnResize += HandleWindowResize;
        HandleWindowResize();
    }

    private Button CreateNavButton(Vector2 pos, string text, Action onClick) {
        return new Button(pos, new Vector2(30, 30), text) {
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            HoverColor = new Color(60, 60, 60),
            FontSize = 18,
            OnClickAction = onClick
        };
    }

    private void AddNewTab(string url) {
        var page = _tabControl.AddTab("New Tab");
        // Offset browser by toolbar height (45)
        var browser = new BrowserControl(new Vector2(0, 45), new Vector2(page.Content.Size.X, page.Content.Size.Y - 45)) {
            Url = url
        };
        
        var spinner = new LoadingSpinner(new Vector2(page.Content.Size.X / 2 - 20, (page.Content.Size.Y + 45) / 2 - 20), new Vector2(40, 40)) {
            Color = _accentColor,
            IsVisible = true
        };

        browser.OnAddressChanged += (newUrl) => {
            if (_tabControl.SelectedPage == page) {
                if (!_urlInput.IsFocused) _urlInput.Value = ShortenUrl(newUrl);
            }
            UpdateNavState();
        };

        browser.OnTitleChanged += (title) => {
            page.Title = title;
            if (_tabControl.SelectedPage == page) Title = $"{title} - Horizon";
        };

        browser.OnLoadingStateChanged += (isLoading) => {
            spinner.IsVisible = isLoading;
        };

        page.Content.AddChild(browser);
        page.Content.AddChild(spinner);
        
        _tabControl.SelectedIndex = _tabControl.Pages.Count - 1;
        
        HandleWindowResize();

        browser.InitializeBrowserAsync().ContinueWith(t => {
            browser.IsVisible = true;
            browser.Navigate(url);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private BrowserControl GetActiveBrowser() {
        var page = _tabControl.SelectedPage;
        return page?.Content.Children.OfType<BrowserControl>().FirstOrDefault();
    }

    private void Navigate(string url) {
        if (string.IsNullOrWhiteSpace(url)) return;
        
        url = url.Trim();
        // Smarter URL detection: has extension, or is localhost/ip, or has protocol
        bool isLikelyUrl = (url.Contains(".") && !url.Contains(" ") && url.Length > 3) 
                        || url.StartsWith("localhost") 
                        || url.StartsWith("127.0.0.1");
        bool hasProtocol = url.Contains("://") || url.StartsWith("about:");

        if (!isLikelyUrl && !hasProtocol) {
            url = "https://www.google.com/search?q=" + System.Uri.EscapeDataString(url);
        } else if (!hasProtocol) {
            url = "https://" + url;
        }

        var browser = GetActiveBrowser();
        browser?.Navigate(url);
    }

    private string ShortenUrl(string url) {
        if (string.IsNullOrEmpty(url) || url.StartsWith("about:")) return url;
        try {
            var uri = new System.Uri(url);
            return $"{uri.Scheme}://{uri.Host}/";
        } catch {
            return url;
        }
    }

    private void SyncUIWithActiveTab() {
        var browser = GetActiveBrowser();
        if (browser != null) {
            _urlInput.Value = _urlInput.IsFocused ? browser.Url : ShortenUrl(browser.Url);
            Title = $"{_tabControl.SelectedPage.Title} - Horizon";
        }
        UpdateNavState();
    }

    private void UpdateNavState() {
        var browser = GetActiveBrowser();
        if (browser != null) {
            _backBtn.IsEnabled = browser.CanGoBack;
            _forwardBtn.IsEnabled = browser.CanGoForward;
        }
    }

    private bool _lastUrlFocused = false;
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        if (_urlInput != null) {
            if (_urlInput.IsFocused && !_lastUrlFocused) {
                // Just focused: show full URL and cursor at 0
                var browser = GetActiveBrowser();
                if (browser != null) {
                    _urlInput.Value = browser.Url;
                    _urlInput.SetCursorAndSelection(0, 0);
                }
            } else if (!_urlInput.IsFocused && _lastUrlFocused) {
                // Just unfocused: shorten URL
                var browser = GetActiveBrowser();
                if (browser != null) _urlInput.Value = ShortenUrl(browser.Url);
            }
            _lastUrlFocused = _urlInput.IsFocused;
        }
    }

    private void HandleWindowResize() {
        _tabControl.Size = Size;
        _toolbarBg.Size = new Vector2(Size.X, 45);
        _urlInput.Size = new Vector2(Size.X - 180, 30);
        
        // Reposition New Tab button at the end of tab list
        float tabsWidth = _tabControl.Pages.Count * 150;
        _newTabBtn.Position = new Vector2(tabsWidth, 0);

        foreach (var page in _tabControl.Pages) {
            var browser = page.Content.Children.OfType<BrowserControl>().FirstOrDefault();
            if (browser != null) {
                browser.Position = new Vector2(0, 45);
                browser.Size = new Vector2(page.Content.Size.X, page.Content.Size.Y - 45);
            }
            
            var spinner = page.Content.Children.OfType<LoadingSpinner>().FirstOrDefault();
            if (spinner != null) spinner.Position = new Vector2(page.Content.Size.X / 2 - 20, (page.Content.Size.Y + 45) / 2 - 20);
        }
    }
}
