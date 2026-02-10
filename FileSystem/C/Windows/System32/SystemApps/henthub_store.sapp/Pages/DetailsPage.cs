using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class DetailsPage : StorePage, IDisposable {
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
    private Button _uninstallBtn;
    private Button _changePathBtn;
    private Label _pathHeader;
    private Label _pathLabel;
    private string _customInstallPath = null;

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

        _installBtn = new Button(new Vector2(150, 100), new Vector2(120, 35), GetInstallButtonText());
        _installBtn.BackgroundColor = new Color(0, 120, 215);
        _installBtn.OnClickAction = () => {
            string downloadUrl = _app.DownloadUrl?.Replace("localhost", "127.0.0.1");
            if (string.IsNullOrEmpty(downloadUrl)) {
                Shell.Notifications.Show("HentHub", "Error: No download URL provided for this app.");
                return;
            }

            // Dependency Check
            var root = StoreManager.Instance.ResolveDependencyTree(_app);
            if (root.HasMissingDependencies) {
                var dialog = new DependencyDialog(root, 
                    () => Shell.Notifications.Show("HentHub", "Installation cancelled due to missing dependencies."),
                    (missingIds) => StartBatchInstallation(missingIds),
                    () => StartInstallation()
                );
                _process.ShowModal(dialog);
            } else {
                StartInstallation();
            }
        };
        _contentScroll.AddChild(_installBtn);

        // Custom Install Path Section
        _pathHeader = new Label(new Vector2(280, 100), "Install to:") {
            Color = Color.Gray,
            FontSize = 14
        };
        _contentScroll.AddChild(_pathHeader);

        string defaultPath = AppInstaller.Instance.GetDefaultInstallPath(_app.TerminalOnly);
        _pathLabel = new Label(new Vector2(280, 118), defaultPath) {
            Color = new Color(200, 200, 200),
            FontSize = 14
        };
        _contentScroll.AddChild(_pathLabel);

        _changePathBtn = new Button(new Vector2(280, 140), new Vector2(120, 30), "Change...") {
            BackgroundColor = new Color(60, 60, 60)
        };
        _changePathBtn.OnClickAction = () => {
            var picker = new FilePickerWindow(
                "Select Installation Directory",
                AppInstaller.Instance.GetDefaultInstallPath(),
                "",
                FilePickerMode.ChooseDirectory,
                (selectedPath) => {
                    _customInstallPath = selectedPath;
                    UpdatePathLabel();
                    
                    // Add to AppLoader search paths automatically so it's scanned on next boot
                    if (!string.IsNullOrEmpty(selectedPath)) {
                        AppLoader.Instance.AddSearchPath(selectedPath);
                    }
                }
            );
            _process.ShowModal(picker);
        };
        _contentScroll.AddChild(_changePathBtn);

        _uninstallBtn = new Button(new Vector2(10, 145), new Vector2(128, 30), "Uninstall") {
            BackgroundColor = new Color(150, 40, 40),
            IsVisible = false
        };
        _uninstallBtn.OnClickAction = () => {
            // Check for dependents before uninstallation
            var metadata = StoreManager.Instance.Manifest.Apps
                .Select(a => (a.AppId, a.Name, a.Dependencies))
                .ToList();
            var dependents = AppInstaller.Instance.GetDependents(_app.AppId, metadata);

            string message = $"Are you sure you want to uninstall {_app.Name}?";
            if (dependents.Count > 0) {
                message = $"Warning: The following installed apps depend on {_app.Name} and may stop working:\n\n" + 
                          string.Join(", ", dependents) + 
                          "\n\nAre you sure you want to proceed?";
            }

            var msg = new MessageBox("Uninstall", message, MessageBoxButtons.YesNo, async (confirmed) => {
                if (confirmed) {
                    await AppInstaller.Instance.UninstallAppAsync(_app.AppId, _app.Name);
                    UpdateStatus();
                    UpdatePathLabel();
                }
            });
            _process.ShowModal(msg);
        };
        _contentScroll.AddChild(_uninstallBtn); // Move inside scroll area

        UpdatePathLabel();
        UpdateStatus();

        // Screenshots section
        float currentY = 220;
        if (_app.ScreenshotCount > 0) {
            var ssHeader = new Label(new Vector2(10, currentY), "Screenshots") {
                FontSize = 18,
                UseBoldFont = true
            };
            _contentScroll.AddChild(ssHeader);
            currentY += 30;

            _screenshotScroll = new ScrollPanel(new Vector2(0, currentY), new Vector2(ClientSize.X, 220)) {
                BackgroundColor = Color.Transparent
            };
            _contentScroll.AddChild(_screenshotScroll);
            currentY += 230;
        }

        // Description section
        var descHeader = new Label(new Vector2(10, currentY), "Description") {
            FontSize = 18,
            UseBoldFont = true
        };
        _contentScroll.AddChild(descHeader);
        currentY += 30;

        _descLabel = new Label(new Vector2(10, currentY), _app.Description ?? "No description provided.") {
            FontSize = 14,
            Color = new Color(220, 220, 220),
        };
        _contentScroll.AddChild(_descLabel);
        currentY += 50; // Some space for the description itself

        // Bottom Padding
        _contentScroll.ContentPadding = new Vector2(0, 100);

        OnResize += () => {
            _contentScroll.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
            if (_screenshotScroll != null) _screenshotScroll.Size = new Vector2(ClientSize.X, 220);
            UpdateDescriptionLayout();
        };

        // Initial Layout
        UpdateDescriptionLayout();
    }

    private void UpdateDescriptionLayout() {
        if (_descLabel == null || _app == null || _contentScroll == null) return;

        float padding = 20;
        float maxWidth = _contentScroll.ClientSize.X - padding;
        if (maxWidth <= 0) return;

        var font = GameContent.FontSystem.GetFont(_descLabel.FontSize);
        if (font == null) return;

        _descLabel.Text = TextHelper.WrapText(font, _app.Description ?? "No description provided.", maxWidth);
        
        // Ensure the content scroll knows its height has changed
        // We add some bottom padding for the scroll area
        _contentScroll.UpdateContentHeight(_descLabel.Position.Y + _descLabel.Size.Y + 20);
    }

    private string GetInstallButtonText() {
        if (!AppInstaller.Instance.IsAppInstalled(_app.AppId)) return "Install";
        
        string installedVer = AppInstaller.Instance.GetInstalledVersion(_app.AppId);
        if (AppInstaller.IsNewerVersion(installedVer, _app.Version)) return "Update";
        
        return "Reinstall";
    }

    private void UpdateStatus() {
        _installBtn.Text = GetInstallButtonText();
        _uninstallBtn.IsVisible = AppInstaller.Instance.IsAppInstalled(_app.AppId);
    }

    private async void StartInstallation() {
        Shell.Notifications.Show("HentHub", $"Beginning installation of {_app.Name}...");
        
        bool success = await AppInstaller.Instance.InstallAppAsync(
            _app.AppId, 
            _app.Name,
            _app.DownloadUrl?.Replace("localhost", "127.0.0.1"),
            _app.Version, 
            _process,
            _customInstallPath,
            isTerminalOnly: _app.TerminalOnly
        );

        if (success) {
            UpdateStatus();
            UpdatePathLabel();
            Shell.Notifications.Show("HentHub", $"{_app.Name} installed successfully!");
        } else {
            Shell.Notifications.Show("HentHub", $"Failed to install {_app.Name}. Check debug_log.txt for details.");
        }
    }

    private async void StartBatchInstallation(List<string> missingIds) {
        var requests = new List<InstallRequest>();
        
        // 1. Add missing dependencies and the main app (if missing) from the tree
        foreach (var id in missingIds) {
            var depApp = StoreManager.Instance.GetApp(id);
            if (depApp != null) {
                requests.Add(new InstallRequest {
                    AppId = depApp.AppId,
                    Name = depApp.Name,
                    DownloadUrl = depApp.DownloadUrl?.Replace("localhost", "127.0.0.1"),
                    Version = depApp.Version,
                    IsTerminalOnly = depApp.TerminalOnly
                });
            }
        }

        // 2. If the main app was NOT missing (e.g. re-install/update) but not in tree, add it last
        if (!requests.Any(r => r.AppId.Equals(_app.AppId, StringComparison.OrdinalIgnoreCase))) {
            requests.Add(new InstallRequest {
                AppId = _app.AppId,
                Name = _app.Name,
                DownloadUrl = _app.DownloadUrl?.Replace("localhost", "127.0.0.1"),
                Version = _app.Version,
                IsTerminalOnly = _app.TerminalOnly
            });
        }

        Shell.Notifications.Show("HentHub", $"Queueing {requests.Count} apps for installation...");
        
        bool success = await AppInstaller.Instance.InstallAppsAsync(requests, _process, _customInstallPath);
        if (success) {
            UpdateStatus();
            UpdatePathLabel();
        }
    }

    private void UpdatePathLabel() {
        if (AppInstaller.Instance.IsAppInstalled(_app.AppId)) {
            string installedPath = AppInstaller.Instance.GetInstalledPath(_app.AppId);
            _pathLabel.Text = installedPath ?? "Unknown Location";
            _pathHeader.Text = "Installed at:";
            _changePathBtn.IsVisible = false;
        } else {
            string defaultPath = AppInstaller.Instance.GetDefaultInstallPath(_app.TerminalOnly);
            _pathLabel.Text = string.IsNullOrEmpty(_customInstallPath) ? defaultPath : _customInstallPath;
            _pathHeader.Text = "Install to:";
            _changePathBtn.IsVisible = true;
        }
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

    public void Dispose() {
        _iconImage?.Dispose();
        if (_screenshotScroll != null) {
            foreach (var child in _screenshotScroll.Children) {
                if (child is IDisposable disposable) disposable.Dispose();
            }
        }
    }
}
