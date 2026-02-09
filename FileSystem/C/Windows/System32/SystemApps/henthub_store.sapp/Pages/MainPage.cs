using System;
using System.Collections.Generic;
using System.Text.Json;
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
        _statusLabel = new Label(new Vector2(10, 10), "Loading applications...") {
            Color = Color.Gray,
            IsVisible = false
        };
        AddChild(_statusLabel);

        _spinner = new LoadingSpinner(new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20), new Vector2(40, 40));
        AddChild(_spinner);

        _listContainer = new ScrollPanel(new Vector2(0, 0), ClientSize) {
            BackgroundColor = Color.Transparent
        };
        AddChild(_listContainer);

        OnResize += () => {
            if (_listContainer != null) _listContainer.Size = ClientSize;
            if (_spinner != null) _spinner.Position = new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20);
            
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

    public override void OnNavigatedTo() {
        base.OnNavigatedTo();
        if (_listContainer != null) _listContainer.Size = ClientSize;
        if (_spinner != null) _spinner.Position = new Vector2(ClientSize.X / 2 - 20, ClientSize.Y / 2 - 20);
        
        // Load only if not already loaded or failed
        if (_cards.Count == 0) {
            LoadManifest();
        }
    }

    private async void LoadManifest() {
        try {
            _spinner.IsVisible = true;
            _statusLabel.IsVisible = false;
            
            // Ensure the spinner has a chance to render before any synchronous network setup
            await System.Threading.Tasks.Task.Yield();
            
            // Try 127.0.0.1 as it's often more reliable than 'localhost' in simulator environments
            var response = await Shell.Network.GetAsync(GetOwnerProcess(), "http://127.0.0.1:3000/manifests/store-manifest.json");

            _spinner.IsVisible = false;

            if (response.IsSuccessStatusCode) {
                var manifest = JsonSerializer.Deserialize<StoreManifest>(response.BodyText);
                if (manifest != null) {
                    RenderApps(manifest.Apps);
                } else {
                    _statusLabel.IsVisible = true;
                    _statusLabel.Text = "Failed to parse manifest.";
                }
            } else {
                _statusLabel.IsVisible = true;
                _statusLabel.Text = $"Error: {response.StatusCode} - {response.ErrorMessage}";
            }
        } catch (Exception ex) {
            _spinner.IsVisible = false;
            _statusLabel.IsVisible = true;
            _statusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void RenderApps(List<StoreApp> apps) {
        _listContainer.ClearChildren();
        _cards.Clear();

        float yOffset = 10;
        float cardWidth = ClientSize.X - 20;
        float cardHeight = 84;
        var process = GetOwnerProcess();

        foreach (var app in apps) {
            var card = new AppCard(app, new Vector2(cardWidth, cardHeight), process, () => {
                Stack?.Push(new DetailsPage(app, process));
            }) {
                Position = new Vector2(10, yOffset)
            };
            
            _listContainer.AddChild(card);
            _cards.Add(card);
            yOffset += cardHeight + 10;
        }
    }
}
