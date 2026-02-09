using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Collections.Generic;
using TheGame;
using TheGame.Core;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using CefSharp;

namespace HorizonBrowser;

public class DownloadConfirmationWindow : Window {
    private DownloadItem _item;
    private IBeforeDownloadCallback _callback;
    private TextInput _pathInput;
    private string _targetPath;
    private Action<string> _onConfirmed;

    public DownloadConfirmationWindow(DownloadItem item, IBeforeDownloadCallback callback, Action<string> onConfirmed) 
        : base(Vector2.Zero, new Vector2(480, 280)) 
    {
        _item = item;
        _callback = callback;
        _onConfirmed = onConfirmed;
        Title = "Confirm Download";
        CanResize = false;

        // Default download path
        _targetPath = Path.Combine("C:\\Users\\User\\Downloads", _item.SuggestedFileName);

        var viewport = G.GraphicsDevice.Viewport;
        var pos = new Vector2(viewport.Width / 2 - Size.X / 2, viewport.Height / 2 - Size.Y / 2);
        Position = pos;
    }

    protected override void OnLoad() {
        BackgroundColor = new Color(35, 35, 35);

        // Site info
        string siteHost = "Unknown Site";
        try {
            string effectiveUrl = string.IsNullOrEmpty(_item.OriginalUrl) ? _item.Url : _item.OriginalUrl;
            if (!string.IsNullOrEmpty(effectiveUrl)) {
                var uri = new Uri(effectiveUrl);
                if (!string.IsNullOrEmpty(uri.Host)) {
                    siteHost = uri.Host;
                } else {
                    siteHost = uri.Scheme + " context";
                }
            }
        } catch { }

        if (string.IsNullOrEmpty(siteHost)) siteHost = "Unknown Site";

        var siteLabel = new Label(new Vector2(20, 20), $"{siteHost} wants to download a file:") {
            FontSize = 16,
            Color = Color.LightGray
        };
        AddChild(siteLabel);

        // File Details Panel
        var iconTexture = Shell.GetIcon(_item.SuggestedFileName);
        var icon = new Icon(new Vector2(20, 50), new Vector2(48, 48), iconTexture);
        AddChild(icon);

        var nameLabel = new Label(new Vector2(80, 55), _item.SuggestedFileName) {
            FontSize = 18,
            UseBoldFont = true
        };
        AddChild(nameLabel);

        long bytes = _item.TotalBytes;
        string sizeText = FormatFileSize(bytes);
        var sizeLabel = new Label(new Vector2(80, 80), sizeText) {
            FontSize = 14,
            Color = Color.Gray
        };
        AddChild(sizeLabel);

        // Path Selection
        var pathLabel = new Label(new Vector2(20, 125), "Download to:") {
            FontSize = 14,
            Color = Color.LightGray
        };
        AddChild(pathLabel);

        _pathInput = new TextInput(new Vector2(20, 150), new Vector2(360, 30)) {
            Value = _targetPath
        };
        AddChild(_pathInput);

        var browseBtn = new Button(new Vector2(390, 150), new Vector2(70, 30), "Browse") {
            OnClickAction = BrowsePath
        };
        AddChild(browseBtn);

        // Action Buttons
        var cancelBtn = new Button(new Vector2(300, 210), new Vector2(80, 35), "Cancel") {
            OnClickAction = () => {
                if (!_callback.IsDisposed) _callback.Dispose();
                Close();
            }
        };
        AddChild(cancelBtn);

        var saveBtn = new Button(new Vector2(390, 210), new Vector2(80, 35), "Save") {
            BackgroundColor = new Color(0, 120, 215), 
            TextColor = Color.White,
            OnClickAction = ConfirmSave
        };
        AddChild(saveBtn);
    }

    private void BrowsePath() {
        string pathVal = _pathInput.Value ?? "";
        string currentDir = "";
        string currentName = "";
        try {
            currentDir = Path.GetDirectoryName(pathVal.Replace('/', '\\')) ?? "C:\\Users\\User\\Downloads";
            currentName = Path.GetFileName(pathVal);
        } catch {
            currentDir = "C:\\Users\\User\\Downloads";
            currentName = _item.SuggestedFileName;
        }

        Shell.UI.OpenWindow(new FilePickerWindow("Select Download Location", currentDir, currentName, FilePickerMode.Save, (path) => {
            if (!string.IsNullOrEmpty(path)) {
                _pathInput.Value = path;
            }
        }), owner: OwnerProcess);
    }

    private void ConfirmSave() {
        string path = _pathInput.Value;
        if (string.IsNullOrEmpty(path)) return;

        try {
            // CefSharp requires a HOST path
            string hostPath = VirtualFileSystem.Instance.ToHostPath(path);
            if (!_callback.IsDisposed) {
                _callback.Continue(hostPath, showDialog: false);
                _onConfirmed?.Invoke(path);
            }
            Close();
        } catch (Exception ex) {
            DebugLogger.Log($"Error confirming download: {ex.Message}");
        }
    }

    private string FormatFileSize(long bytes) {
        if (bytes <= 0) return "Unknown size";
        string[] suffix = { "B", "KB", "MB", "GB", "TB" };
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return num.ToString() + " " + suffix[place];
    }
}
