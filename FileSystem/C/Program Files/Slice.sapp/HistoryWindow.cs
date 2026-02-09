using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace ScreenCapture;

public class HistoryWindow : Window {
    private ScrollPanel _scrollPanel;
    private CaptureHistory _history;

    private static readonly string[] SliceIntros = {
        "Slice. Capture your reality.",
        "Slice — the screenshot app for HentOS.",
        "Slice. The tastiest way to grab your screen.",
        "Slice: Serving HentOS in slices.",
        "Slice. Stop the moment.",
        "Slice: Slice it, Save it, Love it.",
        "Slice — Sharp. Fast. Digital."
    };

    public HistoryWindow(CaptureHistory history) : base(Vector2.Zero, new Vector2(400, 500)) {
        _history = history;
        Title = "Capture History";
        CanResize = false;

        var viewport = G.GraphicsDevice.Viewport;
        Position = new Vector2(viewport.Width - Size.X - 20, viewport.Height - Size.Y - 60);
    }

    protected override void OnLoad() {
        base.OnLoad();

        var label = new Label(new Vector2(10, 15), SliceIntros[Random.Shared.Next(SliceIntros.Length)]) {
            Color = Color.LightBlue,
            FontSize = 18,
            UseBoldFont = true,
        };
        AddChild(label);

        // Setup layout
        _scrollPanel = new ScrollPanel(new Vector2(10, 40), new Vector2(ClientSize.X - 20, ClientSize.Y - 110));
        _scrollPanel.BackgroundColor = new Color(30, 30, 30, 100);
        AddChild(_scrollPanel);
        
        // Refresh List Button (at bottom left)
        var refreshBtn = new Button(new Vector2(10, ClientSize.Y - 50), new Vector2(100, 30), "Refresh");
        refreshBtn.OnClickAction = () => RefreshList();
        AddChild(refreshBtn);

        // Clear All Button (at bottom right)
        var clearBtn = new Button(new Vector2(ClientSize.X - 110, ClientSize.Y - 50), new Vector2(100, 30), "Clear All");
        clearBtn.OnClickAction = () => {
            _history.Items.Clear();
            RefreshList();
            SaveHistory();
        };
        AddChild(clearBtn);

        RefreshList();
    }
    
    public void RefreshList() {
        _scrollPanel.ClearChildren();
        float y = 5;
        
        // Prune non-existent files before showing
        _history.Items.RemoveAll(item => !VirtualFileSystem.Instance.Exists(item.Path));
        
        if (_history.Items.Count == 0) {
            var emptyLabel = new Label(new Vector2(20, 20), "No history yet.");
            emptyLabel.TextColor = Color.Gray;
            _scrollPanel.AddChild(emptyLabel);
            _scrollPanel.UpdateContentHeight(60);
            return;
        }

        // Show newest first
        var sortedItems = new List<HistoryItem>(_history.Items);
        sortedItems.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));

        foreach (var item in sortedItems) {
            string fileName = System.IO.Path.GetFileName(item.Path);
            var btn = new Button(new Vector2(5, y), new Vector2(_scrollPanel.Size.X - 15, 60), "");
            btn.BackgroundColor = new Color(50, 50, 50, 150);
            
            // Text labels inside button
            var nameLabel = new Label(new Vector2(10, 10), fileName) { FontSize = 18 };
            var dateLabel = new Label(new Vector2(10, 35), item.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")) {
                TextColor = Color.LightGray * 0.7f,
                FontSize = 14
            };
            
            btn.AddChild(nameLabel);
            btn.AddChild(dateLabel);
            
            string path = item.Path;
            btn.OnClickAction = () => {
                Shell.Execute(path);
            };
            
            _scrollPanel.AddChild(btn);
            y += 65;
        }
        
        _scrollPanel.UpdateContentHeight(y);
    }
    
    private void SaveHistory() {
        Shell.AppSettings.Save(OwnerProcess, _history);
    }
}
