using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using System.Threading.Tasks;
using System.Threading;

namespace NetworkTestApp;

public class Program : Application {
    public static Application Main(string[] args) => new Program();

    protected override void OnLoad(string[] args) {
        Shell.Network.RegisterForNetwork(Process);
        MainWindow = CreateWindow<NetworkTestWindow>();
        MainWindow.Title = "Network Utility";
        MainWindow.Size = new Vector2(500, 470);
    }
}

public class NetworkTestWindow : Window {
    private TextInput _urlInput;
    private TextArea _responseArea;
    private Label _statsLabel;
    private ProgressBar _downloadProgress;
    private Button _getBtn;
    private Button _downloadBtn;

    public NetworkTestWindow() {
        Title = "Network Utility";
        Size = new Vector2(500, 450);
    }

    protected override void OnLoad() {
        // --- HTTP Request Section ---
        AddChild(new Label(new Vector2(10, 10), "HTTP Request") { UseBoldFont = true, FontSize = 16 });
        
        _urlInput = new TextInput(new Vector2(10, 35), new Vector2(380, 30)) {
            Placeholder = "http://example.com",
            Value = "http://google.com"
        };
        AddChild(_urlInput);

        _getBtn = new Button(new Vector2(400, 35), new Vector2(80, 30), "GET");
        _getBtn.OnClickAction = () => PerformRequest("GET");
        AddChild(_getBtn);

        // --- Response Area ---
        _responseArea = new TextArea(new Vector2(10, 75), new Vector2(480, 150)) {
            BackgroundColor = new Color(30, 30, 30)
        };
        AddChild(_responseArea);

        // --- File Transfer Section ---
        AddChild(new Label(new Vector2(10, 240), "File Transfer") { UseBoldFont = true, FontSize = 16 });
        
        _downloadBtn = new Button(new Vector2(10, 265), new Vector2(150, 30), "Download Test File");
        _downloadBtn.OnClickAction = DownloadTest;
        AddChild(_downloadBtn);

        _downloadProgress = new ProgressBar(new Vector2(170, 265), new Vector2(320, 30)) {
            Value = 0,
            TextFormat = "{0}%"
        };
        AddChild(_downloadProgress);

        // --- Firewall Section ---
        AddChild(new Label(new Vector2(10, 310), "Firewall Testing") { UseBoldFont = true, FontSize = 16 });
        
        var blockBtn = new Button(new Vector2(10, 335), new Vector2(235, 30), "Test Blocked Domain");
        blockBtn.OnClickAction = () => PerformRequest("GET", "http://malware.com");
        AddChild(blockBtn);

        var allowBtn = new Button(new Vector2(255, 335), new Vector2(235, 30), "Test Allowed Domain");
        allowBtn.OnClickAction = () => PerformRequest("GET", "http://google.com");
        AddChild(allowBtn);

        // --- Stats Section ---
        AddChild(new Label(new Vector2(10, 380), "Statistics") { UseBoldFont = true, FontSize = 16 });
        _statsLabel = new Label(new Vector2(10, 405), "Down: 0 B | Up: 0 B") { Color = Color.Gray };
        AddChild(_statsLabel);
        
        var resetBtn = new Button(new Vector2(390, 400), new Vector2(100, 30), "Reset Stats");
        resetBtn.OnClickAction = () => Shell.Network.ResetStats(OwnerProcess);
        AddChild(resetBtn);
    }

    private async void PerformRequest(string method, string overrideUrl = null) {
        string url = overrideUrl ?? _urlInput.Value;
        _responseArea.SetValue($"Sending {method} to {url}...");
        _getBtn.IsEnabled = false;

        try {
            NetworkResponse response;
            if (method == "GET") {
                response = await Shell.Network.GetAsync(OwnerProcess, url);
            } else {
                response = await Shell.Network.PostAsync(OwnerProcess, url, null);
            }

            if (response.IsSuccessStatusCode) {
                _responseArea.SetValue($"Status: {response.StatusCode}\n\nContent:\n{response.BodyText}");
            } else {
                _responseArea.SetValue($"Error: {response.StatusCode}\n{response.ErrorMessage}");
            }
        } catch (Exception ex) {
            _responseArea.SetValue($"Exception: {ex.Message}");
        } finally {
            _getBtn.IsEnabled = true;
        }
    }

    private async void DownloadTest() {
        _downloadBtn.IsEnabled = false;
        _downloadProgress.Value = 0;
        string url = "https://upload.wikimedia.org/wikipedia/commons/3/3f/JPEG_example_flower.jpg"; // A reliable test file
        string path = "C:\\Users\\Admin\\Downloads\\flower.jpg";

        var progress = new Progress<float>(v => _downloadProgress.Value = v);
        
        try {
            _responseArea.SetValue($"Downloading {url} to {path}...");
            await Shell.Network.DownloadToFileAsync(OwnerProcess, url, path, progress);
            
            _responseArea.SetValue("Download complete!");
            Shell.Notifications.Show("Network Utility", "File downloaded successfully to " + path);
        } catch (Exception ex) {
            _responseArea.SetValue($"Download Exception: {ex.Message}");
        } finally {
            _downloadBtn.IsEnabled = true;
        }
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update stats display
        var stats = Shell.Network.GetStats(OwnerProcess);
        if (stats != null) {
            _statsLabel.Text = $"Down: {FormatBytes(stats.BytesDownloaded)} | Up: {FormatBytes(stats.BytesUploaded)}";
        }
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
}
