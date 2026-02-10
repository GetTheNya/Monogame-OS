using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class MainPage : StorePage {
    private ScrollPanel _listContainer;
    private Label _statusLabel;
    private LoadingSpinner _spinner;
    private List<AppCard> _cards = new();

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

        _listContainer = new ScrollPanel(new Vector2(0, 50), new Vector2(ClientSize.X, ClientSize.Y - 50)) {
            BackgroundColor = Color.Transparent
        };
        AddChild(_listContainer);

        OnResize += () => {
            if (_listContainer != null) _listContainer.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
            if (_spinner != null) _spinner.Position = new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20);
            if (refreshBtn != null) refreshBtn.Position = new Vector2(ClientSize.X - 100, 10);
            if (_statusLabel != null) {
                _statusLabel.Position = new Vector2(ClientSize.X / 2 - _statusLabel.Size.X / 2, ClientSize.Y / 2);
            }
            
            // Re-layout cards
            float yOffset = 10;
            float cardWidth = ClientSize.X - 20;
            foreach (var card in _cards) {
                card.Size = new Vector2(cardWidth, card.Size.Y);
                card.Position = new Vector2(10, yOffset);
                yOffset += card.Size.Y + 10;
            }
        };
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (_statusLabel != null && _statusLabel.IsVisible) {
            _statusLabel.Position = new Vector2(ClientSize.X / 2 - _statusLabel.Size.X / 2, ClientSize.Y / 3);
        }
    }

    public override async void OnNavigatedTo() {
        base.OnNavigatedTo();
        if (_listContainer != null) _listContainer.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
        if (_spinner != null) _spinner.Position = new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20);
        
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
        try {
            _spinner.IsVisible = true;
            _statusLabel.IsVisible = false;

            // Clear existing UI immediately on refresh to show activity
            foreach (var card in _cards) {
                card.Dispose();
            }
            _cards.Clear();
            _listContainer.ClearChildren();
            
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
        }
    }

    private async Task RenderApps(List<StoreApp> apps) {
        foreach (var card in _cards) {
            card.Dispose();
        }
        _listContainer.ClearChildren();
        _cards.Clear();

        float yOffset = 10;
        float cardWidth = ClientSize.X - 20;
        float cardHeight = 84;
        var process = GetOwnerProcess();

        int count = 0;

        foreach (var app in apps) {
            var card = new AppCard(app, new Vector2(cardWidth, cardHeight), process, () => {
                Stack?.Push(new DetailsPage(app, process));
            }) {
                Position = new Vector2(10, yOffset)
            };
            
            _listContainer.AddChild(card);
            _cards.Add(card);
            yOffset += cardHeight + 10;

            count++;
            if (count % 3 == 0) {
                await Task.Delay(1);
            }
        }
    }
}
