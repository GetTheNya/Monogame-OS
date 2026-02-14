using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class MainPage : StorePage {
    private TabControl _tabs;
    private ScrollPanel _appContainer;
    private ScrollPanel _widgetContainer;
    private Label _statusLabel;
    private Label _deskToysWarning;
    private LoadingSpinner _spinner;
    private List<AppCard> _cards = new();
    private bool _isLoading = false;

    public MainPage() : base("HentHub Store") {
        _statusLabel = new Label(new Vector2(ClientSize.X / 2 - 150, ClientSize.Y / 2), "Loading applications...") {
            Color = Color.Gray,
            IsVisible = false,
            FontSize = 16
        };
        AddChild(_statusLabel);

        _spinner = new LoadingSpinner(new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20), new Vector2(40, 40));
        AddChild(_spinner);

        var refreshBtn = new Button(new Vector2(ClientSize.X - 100, 10), new Vector2(90, 30), "Refresh") {
            BackgroundColor = new Color(60, 60, 60)
        };
        refreshBtn.OnClickAction = () => LoadManifest(true);
        AddChild(refreshBtn);

        _tabs = new TabControl(new Vector2(0, 50), new Vector2(ClientSize.X, ClientSize.Y - 50)) {
            TabBarHeight = 40,
            AllowCloseTabs = false
        };
        var appTab = _tabs.AddTab("Applications");
        _appContainer = appTab.Content;
        
        var widgetTab = _tabs.AddTab("Widgets");
        _widgetContainer = widgetTab.Content;
        
        _tabs.OnTabChanged += (index) => UpdateWarningVisibility();

        AddChild(_tabs);

        _deskToysWarning = new Label(new Vector2(10, 50), "Warning: 'DeskToys' is not installed. Please install it to use widgets.") {
            Color = Color.LightPink,
            IsVisible = false,
            FontSize = 14
        };
        AddChild(_deskToysWarning);

        OnResize += () => {
            if (_tabs != null) _tabs.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
            if (_spinner != null) _spinner.Position = new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20);
            if (refreshBtn != null) refreshBtn.Position = new Vector2(ClientSize.X - 100, 10);
            if (_statusLabel != null) {
                _statusLabel.Position = new Vector2(ClientSize.X / 2 - _statusLabel.Size.X / 2, ClientSize.Y / 2);
            }
            if (_deskToysWarning != null) {
                _deskToysWarning.Position = new Vector2(10, 50);
                if (_deskToysWarning.IsVisible) {
                    _tabs.Position = new Vector2(0, 80);
                    _tabs.Size = new Vector2(ClientSize.X, ClientSize.Y - 80);
                } else {
                    _tabs.Position = new Vector2(0, 50);
                    _tabs.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
                }
            }
            
            // Re-layout cards
            LayoutCards();
        };
    }

    private void UpdateWarningVisibility() {
        bool deskToysInstalled = AppInstaller.Instance.IsAppInstalled("DESKTOYS");
        bool isWidgetTab = _tabs.SelectedIndex == 1;
        _deskToysWarning.IsVisible = !deskToysInstalled && isWidgetTab;

        if (_deskToysWarning.IsVisible) {
            _tabs.Position = new Vector2(0, 80);
            _tabs.Size = new Vector2(ClientSize.X, ClientSize.Y - 80);
        } else {
            _tabs.Position = new Vector2(0, 50);
            _tabs.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
        }
    }

    private void LayoutCards() {
        float cardWidth = ClientSize.X - 20;
        
        float yApps = 10;
        float yWidgets = 10;

        foreach (var card in _cards) {
            card.Size = new Vector2(cardWidth, card.Size.Y);
            if (card.Tag as string == "application") {
                card.Position = new Vector2(10, yApps);
                yApps += card.Size.Y + 10;
            } else {
                card.Position = new Vector2(10, yWidgets);
                yWidgets += card.Size.Y + 10;
            }
        }
        _appContainer.UpdateContentHeight(yApps);
        _widgetContainer.UpdateContentHeight(yWidgets);
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (_statusLabel != null && _statusLabel.IsVisible) {
            _statusLabel.Position = new Vector2(ClientSize.X / 2 - _statusLabel.Size.X / 2, ClientSize.Y / 3);
        }
    }

    public override async void OnNavigatedTo() {
        base.OnNavigatedTo();
        if (_tabs != null) _tabs.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
        if (_spinner != null) _spinner.Position = new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20);
        
        UpdateWarningVisibility();

        // Refresh statuses
        await AppInstaller.Instance.RefreshCacheAsync();
        foreach (var card in _cards) {
            card.UpdateStatus();
        }

        // Load only if not already loaded or failed
        if (_cards.Count == 0) {
            LoadManifest(false);
        }
    }

    private async void LoadManifest(bool forceRefresh) {
        if (_isLoading) return;
        try {
            _isLoading = true;
            _spinner.IsVisible = true;
            _statusLabel.IsVisible = false;

            // Clear existing UI immediately on refresh to show activity
            foreach (var card in _cards) {
                card.Dispose();
            }
            _cards.Clear();
            _appContainer.ClearChildren();
            _widgetContainer.ClearChildren();
            
            await Task.Yield();
            
            bool success = await StoreManager.Instance.LoadManifestAsync(GetOwnerProcess(), forceRefresh);

            _spinner.IsVisible = false;

            if (success) {
                // Refresh registry cache before rendering to ensure status labels are accurate
                await AppInstaller.Instance.RefreshCacheAsync();
                await RenderApps(StoreManager.Instance.Manifest.Apps);
            } else {
                foreach (var card in _cards) {
                    card.Dispose();
                }
                _cards.Clear();

                _statusLabel.IsVisible = true;
                _statusLabel.Text = "Oops! We couldn't reach the store. Check your internet?";
            }
        } catch (Exception ex) {
            _spinner.IsVisible = false;
            _statusLabel.IsVisible = true;
            _statusLabel.Text = $"Error: {ex.Message}";
        } finally {
            _isLoading = false;
        }
    }

    private async Task RenderApps(List<StoreApp> apps) {
        foreach (var card in _cards) {
            card.Dispose();
        }
        _appContainer.ClearChildren();
        _widgetContainer.ClearChildren();
        _cards.Clear();

        float cardWidth = ClientSize.X - 20;
        float cardHeight = 84;
        var process = GetOwnerProcess();

        int count = 0;

        foreach (var app in apps) {
            bool isWidget = app.ExtensionType?.Equals("widget", StringComparison.OrdinalIgnoreCase) ?? false;
            var container = isWidget ? _widgetContainer : _appContainer;

            var card = new AppCard(app, new Vector2(cardWidth, cardHeight), process, () => {
                Stack?.Push(DetailsPageFactory.Create(app, process));
            }) {
                Tag = isWidget ? "widget" : "application"
            };
            
            container.AddChild(card);
            _cards.Add(card);
            
            count++;
            if (count % 3 == 0) {
                await Task.Delay(1);
            }
        }
        
        LayoutCards();
    }
}
