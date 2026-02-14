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

public abstract class BaseDetailsPage : StorePage, IDisposable {
    protected StoreApp _app;
    protected Process _process;
    
    protected ScrollPanel _contentScroll;
    protected ScrollPanel _screenshotScroll;
    protected Icon _iconImage;
    protected Label _nameLabel;
    protected Label _authorLabel;
    protected Label _descLabel;
    protected Button _backBtn;
    protected Button _installBtn;
    protected Button _uninstallBtn;
    protected Button _changePathBtn;
    protected Label _pathHeader;
    protected Label _pathLabel;
    protected Label _sizeLabel;
    protected Label _minOSLabel;
    protected Label _incompatibleLabel;
    protected string _customInstallPath = null;
    
    protected Texture2D _loadedIcon;
    protected List<Texture2D> _loadedScreenshots = new();

    public BaseDetailsPage(StoreApp app, Process process) : base(app.Name) {
        _app = app;
        _process = process;
        
        SetupUI();
        InitializeAsync();
    }

    protected async void InitializeAsync() {
        // Load detailed manifest first
        await StoreManager.Instance.LoadAppManifestAsync(_app, _process);
        
        // Refresh UI elements with detailed data
        _authorLabel.Text = $"by {_app.Author} (v{_app.Version})";
        _minOSLabel.Text = $"Required OS: {_app.MinOSVersion}";
        UpdateDescriptionLayout();
        UpdateStatus();
        UpdatePathLabel();
        UpdateSizeLabel();

        LoadIcon();
        LoadScreenshots();
    }

    protected virtual void SetupUI() {
        _backBtn = new Button(new Vector2(10, 10), new Vector2(80, 30), "< Back");
        _backBtn.OnClickAction = () => base.Stack?.Pop();
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

        _sizeLabel = new Label(new Vector2(150, 70), "Size: ...") {
            Color = Color.Gray,
            FontSize = 14
        };
        _contentScroll.AddChild(_sizeLabel);

        _minOSLabel = new Label(new Vector2(150, 95), $"Required OS: {_app.MinOSVersion}") {
            Color = Color.Gray,
            FontSize = 14
        };
        _contentScroll.AddChild(_minOSLabel);

        _installBtn = new Button(new Vector2(150, 125), new Vector2(120, 35), GetInstallButtonText());
        _installBtn.BackgroundColor = new Color(0, 120, 215);
        _installBtn.OnClickAction = () => {
            string downloadUrl = _app.DownloadUrl;
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

        _incompatibleLabel = new Label(new Vector2(150, 125), "Incompatible OS Version") {
            Color = Color.LightPink,
            FontSize = 16,
            UseBoldFont = true,
            IsVisible = false
        };
        _contentScroll.AddChild(_incompatibleLabel);

        // Custom Install Path Section
        _pathHeader = new Label(new Vector2(450, 10), "Install to:") {
            Color = Color.Gray,
            FontSize = 14
        };
        _contentScroll.AddChild(_pathHeader);

        string defaultPath = GetDefaultInstallPath();
        _pathLabel = new Label(new Vector2(450, 28), defaultPath) {
            Color = new Color(200, 200, 200),
            FontSize = 14
        };
        _contentScroll.AddChild(_pathLabel);

        _changePathBtn = new Button(new Vector2(450, 50), new Vector2(100, 25), "Change...") {
            BackgroundColor = new Color(60, 60, 60),
            FontSize = 12
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
        _contentScroll.AddChild(_uninstallBtn);

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

        // Bottom Padding
        _contentScroll.ContentPadding = new Vector2(0, 100);

        OnResize += () => {
            _contentScroll.Size = new Vector2(ClientSize.X, ClientSize.Y - 50);
            if (_screenshotScroll != null) _screenshotScroll.Size = new Vector2(ClientSize.X, 220);
            UpdateDescriptionLayout();
        };

        UpdateDescriptionLayout();
    }

    protected void UpdateDescriptionLayout() {
        if (_descLabel == null || _app == null || _contentScroll == null) return;

        float padding = 20;
        float maxWidth = _contentScroll.ClientSize.X - padding;
        if (maxWidth <= 0) return;

        var font = GameContent.FontSystem.GetFont(_descLabel.FontSize);
        if (font == null) return;

        _descLabel.Text = TextHelper.WrapText(font, _app.Description ?? "No description provided.", maxWidth);
        _contentScroll.UpdateContentHeight(_descLabel.Position.Y + _descLabel.Size.Y + 20);
    }

    protected string GetInstallButtonText() {
        if (!VersionHelper.IsCompatible(_app.MinOSVersion)) return "Incompatible OS Version";
        if (!AppInstaller.Instance.IsAppInstalled(_app.AppId)) return "Install";
        
        string installedVer = AppInstaller.Instance.GetInstalledVersion(_app.AppId);
        if (AppInstaller.IsNewerVersion(installedVer, _app.Version)) return "Update";
        
        return "Reinstall";
    }

    protected virtual void UpdateStatus() {
        bool isCompatible = VersionHelper.IsCompatible(_app.MinOSVersion);
        _installBtn.Text = GetInstallButtonText();
        _installBtn.IsVisible = isCompatible;
        _incompatibleLabel.IsVisible = !isCompatible;
        _minOSLabel.Color = isCompatible ? Color.Gray : Color.LightPink;
        
        _uninstallBtn.IsVisible = AppInstaller.Instance.IsAppInstalled(_app.AppId);
    }

    protected abstract string GetDefaultInstallPath();
    protected abstract void UpdateSizeLabel();
    protected abstract void OnPostInstallSuccess();

    protected async void StartInstallation() {
        if (!VersionHelper.IsCompatible(_app.MinOSVersion)) {
            Shell.Notifications.Show("HentHub", $"Error: This app requires at least {_app.MinOSVersion}. Your system is {SystemVersion.Current}.");
            return;
        }

        Shell.Notifications.Show("HentHub", $"Beginning installation of {_app.Name}...");
        
        string installPath = _customInstallPath ?? GetDefaultInstallPath();

        bool success = await AppInstaller.Instance.InstallAppAsync(
            _app.AppId, 
            _app.Name,
            _app.DownloadUrl,
            _app.Version, 
            _process,
            installPath,
            isTerminalOnly: _app.TerminalOnly,
            extensionType: _app.ExtensionType
        );

        if (success) {
            UpdateStatus();
            UpdatePathLabel();
            OnPostInstallSuccess();
            Shell.Notifications.Show("HentHub", $"{_app.Name} installed successfully!");
        } else {
            Shell.Notifications.Show("HentHub", $"Failed to install {_app.Name}. Check debug_log.txt for details.");
        }
    }

    protected async void StartBatchInstallation(List<string> missingIds) {
        var requests = new List<InstallRequest>();
        
        foreach (var id in missingIds) {
            var depApp = StoreManager.Instance.GetApp(id);
            if (depApp != null) {
                requests.Add(new InstallRequest {
                    AppId = depApp.AppId,
                    Name = depApp.Name,
                    DownloadUrl = depApp.DownloadUrl,
                    Version = depApp.Version,
                    IsTerminalOnly = depApp.TerminalOnly,
                    ExtensionType = depApp.ExtensionType
                });
            }
        }

        if (!requests.Any(r => r.AppId.Equals(_app.AppId, StringComparison.OrdinalIgnoreCase))) {
            requests.Add(new InstallRequest {
                AppId = _app.AppId,
                Name = _app.Name,
                DownloadUrl = _app.DownloadUrl,
                Version = _app.Version,
                IsTerminalOnly = _app.TerminalOnly,
                ExtensionType = _app.ExtensionType
            });
        }

        Shell.Notifications.Show("HentHub", $"Queueing {requests.Count} apps for installation...");
        
        bool success = await AppInstaller.Instance.InstallAppsAsync(requests, _process, _customInstallPath);
        if (success) {
            UpdateStatus();
            UpdatePathLabel();
        }
    }

    protected virtual void UpdatePathLabel() {
        if (AppInstaller.Instance.IsAppInstalled(_app.AppId)) {
            string installedPath = AppInstaller.Instance.GetInstalledPath(_app.AppId);
            _pathLabel.Text = installedPath ?? "Unknown Location";
            _pathHeader.Text = "Installed at:";
            _changePathBtn.IsVisible = false;
        } else {
            string defaultPath = _customInstallPath ?? GetDefaultInstallPath();
            _pathLabel.Text = defaultPath;
            _pathHeader.Text = "Install to:";
            _changePathBtn.IsVisible = true;
        }
    }

    protected async void LoadIcon() {
        if (string.IsNullOrEmpty(_app.IconUrl)) return;
        try {
            string url = _app.IconUrl;
            var response = await Shell.Network.GetAsync(_process, url);
            if (response.IsSuccessStatusCode && response.BodyBytes != null) {
                using (var ms = new MemoryStream(response.BodyBytes)) {
                    var texture = ImageLoader.LoadFromStream(TheGame.G.GraphicsDevice, ms);
                    if (texture != null) {
                        _loadedIcon?.Dispose();
                        _loadedIcon = texture;
                        _iconImage.ShowPlaceholder = false;
                        _iconImage.Texture = texture;
                    }
                }
            }
        } catch { }
    }

    protected async void LoadScreenshots() {
        if (_app.ScreenshotCount <= 0 || _screenshotScroll == null) return;

        try {
            var tasks = new List<Task<Texture2D>>();
            for (int i = 0; i < _app.ScreenshotCount; i++) {
                string category = StoreManager.Instance.GetCategoryFolder(_app.ExtensionType);
                string url = $"https://getthenya.github.io/HentHub-Store/assets/screenshots/{category}/{_app.AppId.ToLower()}/{i}.png";
                tasks.Add(DownloadTextureAsync(url));
            }

            var textures = await Task.WhenAll(tasks);
            _loadedScreenshots.AddRange(textures.Where(t => t != null));

            float xOffset = 10;
            float galleryHeight = 180;

            foreach (var texture in textures) {
                if (texture == null) continue;

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

    protected async Task<Texture2D> DownloadTextureAsync(string url) {
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

    public virtual void Dispose() {
        _iconImage?.Dispose();
        _loadedIcon?.Dispose();
        foreach (var tex in _loadedScreenshots) {
            tex?.Dispose();
        }
        _loadedScreenshots.Clear();

        if (_screenshotScroll != null) {
            foreach (var child in _screenshotScroll.Children) {
                if (child is IDisposable disposable) disposable.Dispose();
            }
        }
    }
}
