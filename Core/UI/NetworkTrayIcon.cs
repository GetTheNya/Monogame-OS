using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core;

namespace TheGame.Core.UI;

/// <summary>
/// Managed tray icon that reflects the current OS network status.
/// </summary>
public class NetworkTrayIcon : IDisposable {
    private readonly TrayIcon _trayIcon;
    private Texture2D _connectedIcon;
    private Texture2D _noConnectionIcon;
    private Texture2D _connectingIcon;

    public TrayIcon TrayIcon => _trayIcon;

    public NetworkTrayIcon() {
        // Load icons from the system resources
        _connectedIcon = LoadSystemIcon("C:\\Windows\\SystemResources\\Icons\\Network\\Connected.png");
        _noConnectionIcon = LoadSystemIcon("C:\\Windows\\SystemResources\\Icons\\Network\\NoConnection.png");
        _connectingIcon = LoadSystemIcon("C:\\Windows\\SystemResources\\Icons\\Network\\Connecting.png");

        _trayIcon = new TrayIcon(_connectedIcon, "Network: Connected");
        _trayIcon.OnClick = ToggleNetwork;
        _trayIcon.OnRightClick = ShowNetworkMenu;
        
        // Initial sync
        UpdateStatus();
        
        // Register for updates
        NetworkManager.Instance.OnStateChanged += UpdateStatus;
    }

    private float _updateTimer = 0f;
    public void Update(GameTime gameTime) {
        _updateTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_updateTimer >= 1.0f) { // Update stats tooltip every second
            _updateTimer = 0f;
            UpdateStatus();
        }
    }

    private Texture2D LoadSystemIcon(string virtualPath) {
        string hostPath = VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (System.IO.File.Exists(hostPath)) {
            try { return ImageLoader.Load(G.GraphicsDevice, hostPath); } catch { }
        }
        return null;
    }

    private void UpdateStatus() {
        bool enabled = NetworkManager.Instance.IsEnabled;
        _trayIcon.SetIcon(enabled ? _connectedIcon : _noConnectionIcon);
        _trayIcon.Tooltip = $"Network: {(enabled ? "Connected" : "Disabled")}";
        
        // Add bandwidth stats to tooltip
        var stats = NetworkManager.Instance.GetAllStats().Values;
        long totalDownloaded = stats.Sum(s => s.BytesDownloaded);
        long totalUploaded = stats.Sum(s => s.BytesUploaded);
        
        _trayIcon.Tooltip += $"\nTotal Down: {FormatBytes(totalDownloaded)}";
        _trayIcon.Tooltip += $"\nTotal Up: {FormatBytes(totalUploaded)}";
    }

    private string FormatBytes(long bytes) {
        string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
        int i;
        double dblSByte = bytes;
        for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024) {
            dblSByte = bytes / 1024.0;
        }
        return $"{dblSByte:0.##} {Suffix[i]}";
    }

    private void ToggleNetwork() {
        NetworkManager.Instance.IsEnabled = !NetworkManager.Instance.IsEnabled;
        UpdateStatus();
    }

    private void ShowNetworkMenu() {
        bool enabled = NetworkManager.Instance.IsEnabled;
        
        var items = new List<MenuItem> {
            new MenuItem { Text = enabled ? "Disable Network" : "Enable Network", Action = ToggleNetwork },
            new MenuItem { Type = MenuItemType.Separator },
            new MenuItem { Text = "Reset Statistics", Action = () => {
                NetworkManager.Instance.ResetAllStats();
                UpdateStatus();
            }}
        };
        
        Shell.ContextMenu.Show(InputManager.MousePosition.ToVector2(), items);
    }

    public void Dispose() {
        NetworkManager.Instance.OnStateChanged -= UpdateStatus;
        _connectedIcon.Dispose();
        _noConnectionIcon.Dispose();
        _connectingIcon.Dispose();;
    }
}
