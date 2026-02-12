using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core;

namespace SettingsApp.Panels;

public class UpdatePanel : Panel {
    private Label _statusLabel;
    private Label _versionLabel;
    private Button _checkButton;
    private Button _downloadBtn;
    private Button _cancelBtn;
    private Button _installBtn;
    private ProgressBar _progressBar;
    
    public UpdatePanel() : base(Vector2.Zero, Vector2.Zero) {
        BackgroundColor = Color.Transparent;
        BorderThickness = 0;
        SetupUI();

        UpdateManager.Instance.OnStateChanged += SyncWithManager;
        UpdateManager.Instance.OnDownloadProgress += (p) => _progressBar.Value = p;
        
        SyncWithManager();
    }
    
    private void SyncWithManager() {
        var manager = UpdateManager.Instance;
        var state = manager.State;
        var result = manager.LastResult;

        // Reset visibility
        _checkButton.IsVisible = true;
        _checkButton.IsEnabled = true;
        _downloadBtn.IsVisible = false;
        _cancelBtn.IsVisible = false;
        _progressBar.IsVisible = false;
        _installBtn.IsVisible = false;
        _statusLabel.TextColor = Color.White;

        switch (state) {
            case UpdateState.Idle:
                _statusLabel.Text = "Ready to check for updates.";
                break;
            case UpdateState.Checking:
                _statusLabel.Text = "Checking for updates...";
                _checkButton.IsEnabled = false;
                break;
            case UpdateState.UpdateAvailable:
                _statusLabel.Text = $"Revision {result?.LatestVersion} is available.";
                _statusLabel.TextColor = Color.Yellow;
                _checkButton.IsVisible = false;
                _downloadBtn.IsVisible = true;
                break;
            case UpdateState.NoUpdateAvailable:
                _statusLabel.Text = "You are running the latest version.";
                _statusLabel.TextColor = Color.LightGreen;
                break;
            case UpdateState.Downloading:
                _statusLabel.Text = "Downloading update...";
                _checkButton.IsVisible = false;
                _cancelBtn.IsVisible = true;
                _progressBar.IsVisible = true;
                _progressBar.Value = manager.DownloadProgress;
                break;
            case UpdateState.Downloaded:
                _statusLabel.Text = "Download complete. Ready to install.";
                _checkButton.IsVisible = false;
                _installBtn.IsVisible = true;
                break;
            case UpdateState.Error:
                _statusLabel.Text = result?.ErrorMessage ?? "An unknown error occurred.";
                _statusLabel.TextColor = Color.Red;
                if (result != null && result.IsUpdateAvailable) {
                    _checkButton.IsVisible = false;
                    _downloadBtn.IsVisible = true;
                }
                break;
        }
    }
    
    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        float maxR = 0;
        float maxB = 0;
        foreach (var child in Children) {
            if (!child.IsVisible) continue;
            maxR = Math.Max(maxR, child.Position.X + child.Size.X);
            maxB = Math.Max(maxB, child.Position.Y + child.Size.Y);
        }
        
        float parentWidth = Parent?.ClientSize.X ?? maxR;
        Size = new Vector2(Math.Max(parentWidth, maxR), maxB + 40);
    }
    
    private void SetupUI() {
        float y = 20;
        
        AddChild(new Label(new Vector2(20, y), "Software Update") { FontSize = 20 });
        y += 40;
        
        _versionLabel = new Label(new Vector2(20, y), $"Current Version: {TheGame.SystemVersion.Current}") { TextColor = Color.Gray };
        AddChild(_versionLabel);
        y += 30;
        
        _statusLabel = new Label(new Vector2(20, y), "Ready to check for updates.") { TextColor = Color.White };
        AddChild(_statusLabel);
        y += 40;
        
        _checkButton = new Button(new Vector2(20, y), new Vector2(190, 30), "Check for Updates") {
            OnClickAction = () => { CheckForUpdates(); }
        };
        AddChild(_checkButton);

        _downloadBtn = new Button(new Vector2(20, y), new Vector2(190, 30), "Download Update") {
            IsVisible = false,
            OnClickAction = () => { StartDownload(); }
        };
        AddChild(_downloadBtn);

        _cancelBtn = new Button(new Vector2(20, y), new Vector2(100, 30), "Cancel") {
            IsVisible = false,
            OnClickAction = () => { UpdateManager.Instance.CancelDownload(); }
        };
        AddChild(_cancelBtn);

        y += 40;

        var startupCheck = new Checkbox(new Vector2(20, y), "Check for updates on startup") {
            Value = Shell.Core.GetStartup("UPDATESVC"),
            OnValueChanged = (val) => {
                Shell.Core.SetStartup("UPDATESVC", val);
            }
        };
        AddChild(startupCheck);
        y += 50;
        
        _progressBar = new ProgressBar(new Vector2(20, y), new Vector2(400, 20)) {
            IsVisible = false
        };
        AddChild(_progressBar);
        
        y += 30;
        _installBtn = new Button(new Vector2(20, y), new Vector2(160, 30), "Install Update") {
            IsVisible = false,
            OnClickAction = () => { UpdateManager.Instance.LaunchUpdater(@"C:\Temp\update.zip"); }
        };
        AddChild(_installBtn);
    }
    
    private async void CheckForUpdates() {
        try {
            var process = ProcessManager.Instance.GetProcessByAppId("SETTINGS");
            var result = await UpdateManager.Instance.CheckForUpdatesAsync(process);
            
            if (result.Success && result.IsUpdateAvailable && !string.IsNullOrEmpty(result.DownloadUrl)) {
                PromptUpdate(result.DownloadUrl, result.LatestVersion);
            }
        } catch (Exception ex) {
            DebugLogger.Log($"UpdatePanel Check Error: {ex}");
        }
    }
    
    private void PromptUpdate(string url, string version) {
        var dialog = new MessageBox("Update Available", $"A new version ({version}) is available.\nWould you like to download and install it?", MessageBoxButtons.YesNo, (result) => {
            if (result) {
                StartDownload();
            }
        });
        Shell.UI.OpenWindow(dialog);
    }
    
    private async void StartDownload() {
        try {
            var process = ProcessManager.Instance.GetProcessByAppId("SETTINGS");
            await UpdateManager.Instance.StartDownloadAsync(process);
        } catch (Exception ex) {
            DebugLogger.Log($"UpdatePanel Download Error: {ex}");
        }
    }
}
