using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class AppCard : Panel, IDisposable {
    private StoreApp _app;
    private Action _onMore;
    private Icon _iconImage;
    private Label _nameLabel;
    private Label _authorLabel;
    private Label _statusLabel;
    private Button _moreBtn;
    private Process _ownerProcess;

    public AppCard(StoreApp app, Vector2 size, Process ownerProcess, Action onMore) : base(Vector2.Zero, size) {
        _app = app;
        _onMore = onMore;
        _ownerProcess = ownerProcess;
        
        BackgroundColor = new Color(0, 0, 0, 50);
        BorderThickness = 1;

        SetupUI();
        LoadIcon();
    }

    private void SetupUI() {
        _iconImage = new Icon(new Vector2(10, 10), new Vector2(64, 64), null) {
            ShowPlaceholder = false
        };
        AddChild(_iconImage);

        _nameLabel = new Label(new Vector2(85, 10), _app.Name) {
            FontSize = 18,
            UseBoldFont = true
        };
        AddChild(_nameLabel);

        _authorLabel = new Label(new Vector2(85, 35), $"by {_app.Author}") {
            FontSize = 14,
            Color = Color.Gray
        };
        AddChild(_authorLabel);

        _statusLabel = new Label(new Vector2(Size.X - 100, 10), "") {
            FontSize = 12,
            Color = Color.LightGreen
        };
        AddChild(_statusLabel);
        UpdateStatus();

        _moreBtn = new Button(new Vector2(Size.X - 90, Size.Y - 40), new Vector2(80, 30), "More");
        _moreBtn.OnClickAction = _onMore;
        AddChild(_moreBtn);

        OnResize += () => {
            if (_moreBtn != null) _moreBtn.Position = new Vector2(Size.X - 90, Size.Y - 40);
            if (_statusLabel != null) _statusLabel.Position = new Vector2(Size.X - _statusLabel.Size.X - 10, 10);
        };
    }

    public void UpdateStatus() {
        bool isInstalled = AppInstaller.Instance.IsAppInstalled(_app.AppId);
        string installedVer = AppInstaller.Instance.GetInstalledVersion(_app.AppId);
        
        if (!isInstalled) {
            _statusLabel.Text = "";
            return;
        }

        if (AppInstaller.IsNewerVersion(installedVer, _app.Version)) {
            _statusLabel.Text = "Update Available";
            _statusLabel.Color = Color.Orange;
            DebugLogger.Log($"[AppCard] Update Available for {_app.AppId}: {installedVer} -> {_app.Version}");
        } else {
            _statusLabel.Text = "Installed";
            _statusLabel.Color = Color.LightGreen;
        }

        // Force measurement for immediate layout
        if (GameContent.FontSystem != null && !string.IsNullOrEmpty(_statusLabel.Text)) {
            var font = GameContent.FontSystem.GetFont(_statusLabel.FontSize);
            var size = font.MeasureString(_statusLabel.Text);
            _statusLabel.Position = new Vector2(Size.X - size.X - 10, 10);
        }
    }

    private async void LoadIcon() {
        if (string.IsNullOrEmpty(_app.IconUrl)) return;

        try {
            // Replace localhost with 127.0.0.1 for simulator compatibility
            string url = _app.IconUrl.Replace("localhost", "127.0.0.1");
            
            // Use the stored _ownerProcess to ensure we have context even before adding to UI
            var response = await Shell.Network.GetAsync(_ownerProcess, url);
            
            if (response.IsSuccessStatusCode && response.BodyBytes != null && response.BodyBytes.Length > 0) {
                using (var ms = new MemoryStream(response.BodyBytes)) {
                    var texture = ImageLoader.LoadFromStream(TheGame.G.GraphicsDevice, ms);
                    if (texture != null) {
                        _iconImage.Texture = texture;
                    } else {
                        Console.WriteLine($"[HentHub] ImageLoader returned null texture for {url}");
                    }
                }
            } else {
                Console.WriteLine($"[HentHub] Failed to load icon: {url} - Status: {response.StatusCode} - {response.ErrorMessage}");
            }
        } catch (Exception ex) {
            Console.WriteLine($"[HentHub] Exception loading icon for {_app.Name}: {ex.Message}");
        }
    }

    public void Dispose() {
        _iconImage?.Dispose();
    }
}
