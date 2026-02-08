using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;

namespace NeonWave;

public class HistoryWindow : Window {
    private Program App => OwnerProcess.Application as Program;
    private ScrollPanel _scroll;
    
    private Color _accentCyan = new Color(0, 255, 255);
    private Color _accentMagenta = new Color(255, 0, 255);

    public HistoryWindow() {
        Title = "PLAYLIST & HISTORY";
        Size = new Vector2(400, 500);
        BackgroundColor = new Color(10, 10, 10, 250);
    }

    protected override void OnLoad() {
        SetupUI();
    }

    private void SetupUI() {
        _scroll = new ScrollPanel(new Vector2(10, 40), new Vector2(Size.X - 20, Size.Y - 60)) {
            BackgroundColor = Color.Transparent,
            BorderColor = _accentCyan * 0.2f
        };
        AddChild(_scroll);

        RefreshList();
    }

    public void RefreshList() {
        _scroll.ClearChildren();
        float y = 0;

        // Current Playlist
        AddSectionHeader("CURRENT PLAYLIST", ref y);
        for (int i = 0; i < App.Playlist.Count; i++) {
            int index = i;
            string path = App.Playlist[i];
            bool isCurrent = i == App.CurrentIndex;
            
            var btn = new Button(new Vector2(0, y), new Vector2(_scroll.Size.X, 30), Path.GetFileName(path)) {
                BackgroundColor = isCurrent ? _accentCyan * 0.1f : Color.Transparent,
                TextAlign = TextAlign.Left,
                HoverColor = _accentCyan * 0.2f,
                TextColor = isCurrent ? _accentCyan : Color.White
            };
            btn.OnClickAction = () => {
                App.PlayTrack(index);
                RefreshList();
            };
            _scroll.AddChild(btn);
            y += 32;
        }

        y += 20;

        // History
        if (App.History.Count > 0) {
            AddSectionHeader("RECENTLY PLAYED", ref y);
            var historyRev = App.History.AsEnumerable().Reverse().ToList();
            foreach (var path in historyRev) {
                var label = new Label(new Vector2(10, y + 5), Path.GetFileName(path)) {
                    FontSize = 14,
                    Color = Color.Gray
                };
                _scroll.AddChild(label);
                y += 25;
            }
        }

        _scroll.UpdateContentHeight(y + 20);
    }

    private void AddSectionHeader(string text, ref float y) {
        var header = new Label(new Vector2(0, y), text) {
            FontSize = 18,
            Color = _accentMagenta
        };
        _scroll.AddChild(header);
        y += 30;
    }

    protected override void OnDraw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
    }
}
