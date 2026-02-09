using CefSharp;
using Microsoft.Xna.Framework;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HorizonBrowser;

public class DownloadWindow : Window {
    private DownloadItem _item;
    private ProgressBar _progressBar;
    private Label _statusLabel;
    
    public DownloadWindow(DownloadItem item) {
        _item = item;
        Title = "Download";
        Size = new Vector2(400, 120);
        BackgroundColor = new Color(30, 30, 30);

        // Initialize components here to avoid race conditions with UpdateProgress
        _progressBar = new ProgressBar(new Vector2(70, 45), new Vector2(310, 20)) {
            Value = 0,
            ProgressColor = new Color(0, 120, 215)
        };
        AddChild(_progressBar);

        _statusLabel = new Label(new Vector2(70, 75), "Starting...") { FontSize = 12, Color = Color.Gray };
        AddChild(_statusLabel);
    }

    protected override void OnLoad() {
        var icon = Shell.GetIcon(_item.SuggestedFileName);
        var iconImg = new Icon(new Vector2(10, 10), new Vector2(48, 48), icon);
        AddChild(iconImg);

        var nameLabel = new Label(new Vector2(70, 15), _item.SuggestedFileName) { FontSize = 16 };
        AddChild(nameLabel);
    }

    public void UpdateProgress(DownloadItem item) {
        _item = item;
        if (_progressBar == null || _statusLabel == null) return;

        float progress = item.PercentComplete / 100f;
        _progressBar.Value = progress;
        _statusLabel.Text = $"{item.ReceivedBytes / 1024} KB / {item.TotalBytes / 1024} KB";
        
        if (item.IsComplete) {
            _statusLabel.Text = "Finished";
            _statusLabel.Color = Color.Green;
        }
    }
}