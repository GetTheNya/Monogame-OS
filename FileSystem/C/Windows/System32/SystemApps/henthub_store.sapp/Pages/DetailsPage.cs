using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class DetailsPage : StorePage {
    private StoreApp _app;
    private Process _process;
    
    private ScrollPanel _contentScroll;
    private ScrollPanel _screenshotScroll;
    private Icon _iconImage;
    private Label _nameLabel;
    private Label _authorLabel;
    private Label _descLabel;
    private Button _backBtn;
    private Button _installBtn;

    public DetailsPage(StoreApp app, Process process) : base(app.Name) {
        _app = app;
        _process = process;
        
        SetupUI();
        LoadIcon();
        LoadScreenshots();
    }

    private void SetupUI() {
        _backBtn = new Button(new Vector2(10, 10), new Vector2(80, 30), "< Back");
        _backBtn.OnClickAction = () => Stack?.Pop();
        AddChild(_backBtn);

        _contentScroll = new ScrollPanel(new Vector2(0, 50), new Vector2(ClientSize.X, ClientSize.Y - 50));
        AddChild(_contentScroll);

        // Header section (Icon + Info)
        _iconImage = new Icon(new Vector2(10, 10), new Vector2(128, 128), null) {
            ShowPlaceholder = false
        };
        _contentScroll.AddChild(_iconImage);

        _nameLabel = new Label(new Vector2(150, 10), _app.Name) {
            FontSize = 24,
            UseBoldFont = true
        };
        _contentScroll.AddChild(_nameLabel);

        _authorLabel = new Label(new Vector2(150, 45), $"by {_app.Author} (v{_app.Version})") {
            Color = Color.Gray,
            FontSize = 16
        };
        _contentScroll.AddChild(_authorLabel);

        string sizeStr = _app.Size > 1024 * 1024 
            ? $"{_app.Size / (1024 * 1024f):F1} MB" 
            : $"{_app.Size / 1024f:F1} KB";
        var sizeLabel = new Label(new Vector2(150, 70), $"Size: {sizeStr}") {
            Color = Color.Gray,
            FontSize = 14
        };
        _contentScroll.AddChild(sizeLabel);

        _installBtn = new Button(new Vector2(150, 100), new Vector2(120, 35), "Install");
        _installBtn.BackgroundColor = new Color(0, 120, 215);
        _installBtn.OnClickAction = () => {
            Shell.Notifications.Show("HentHub", $"Beginning installation of {_app.Name}...");
        };
        _contentScroll.AddChild(_installBtn);

        // Screenshots section
        if (_app.ScreenshotCount > 0) {
            var ssHeader = new Label(new Vector2(10, 160), "Screenshots") {
                FontSize = 18,
                UseBoldFont = true
            };
            _contentScroll.AddChild(ssHeader);

            _screenshotScroll = new ScrollPanel(new Vector2(0, 190), new Vector2(ClientSize.X, 220)) {
                BackgroundColor = Color.Transparent
            };
            _contentScroll.AddChild(_screenshotScroll);
        }

        // Description section
        float descY = _app.ScreenshotCount > 0 ? 430 : 160;
        var descHeader = new Label(new Vector2(10, descY), "Description") {
            FontSize = 18,
            UseBoldFont = true
        };
        _contentScroll.AddChild(descHeader);

        _descLabel = new Label(new Vector2(10, descY + 30), _app.Description ?? "No description provided.") {
            FontSize = 14,
            Color = new Color(220, 220, 220),
            // We don't have multi-line label auto-wrap easily here so we just show it
        };
        _contentScroll.AddChild(_descLabel);

        OnResize += () => {
            _contentScroll.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
            if (_screenshotScroll != null) _screenshotScroll.Size = new Vector2(ClientSize.X, 220);
        };
    }

    private async void LoadIcon() {
        if (string.IsNullOrEmpty(_app.IconUrl)) return;
        try {
            string url = _app.IconUrl.Replace("localhost", "127.0.0.1");
            var response = await Shell.Network.GetAsync(_process, url);
            if (response.IsSuccessStatusCode && response.BodyBytes != null) {
                using (var ms = new MemoryStream(response.BodyBytes)) {
                    var texture = ImageLoader.LoadFromStream(TheGame.G.GraphicsDevice, ms);
                    if (texture != null) {
                        _iconImage.ShowPlaceholder = false;
                        _iconImage.Texture = texture;
                    }
                }
            }
        } catch { }
    }

    private async void LoadScreenshots() {
        if (_app.ScreenshotCount <= 0 || _screenshotScroll == null) return;

        try {
            var tasks = new List<Task<Texture2D>>();
            for (int i = 0; i < _app.ScreenshotCount; i++) {
                string url = $"http://127.0.0.1:3000/assets/screenshots/{_app.AppId}/{i}.png";
                tasks.Add(DownloadTextureAsync(url));
            }

            var textures = await Task.WhenAll(tasks);

            float xOffset = 10;
            float galleryHeight = 180;

            foreach (var texture in textures) {
                if (texture == null) continue;

                // Maintain aspect ratio: width = (texWidth / texHeight) * targetHeight
                float width = (texture.Width / (float)texture.Height) * galleryHeight;
                
                var ssIcon = new Icon(new Vector2(xOffset, 10), new Vector2(width, galleryHeight), texture) {
                    ShowPlaceholder = false
                };
                _screenshotScroll.AddChild(ssIcon);
                xOffset += width + 10;
            }

            _screenshotScroll.UpdateContentWidth(xOffset);
        } catch (Exception ex) {
            Console.WriteLine($"[HentHub] Error loading screenshots: {ex.Message}");
        }
    }

    private async Task<Texture2D> DownloadTextureAsync(string url) {
        try {
            var response = await Shell.Network.GetAsync(_process, url);
            if (response.IsSuccessStatusCode && response.BodyBytes != null && response.BodyBytes.Length > 0) {
                using (var ms = new MemoryStream(response.BodyBytes)) {
                    return ImageLoader.LoadFromStream(TheGame.G.GraphicsDevice, ms);
                }
            }
        } catch { }
        return null;
    }
}
