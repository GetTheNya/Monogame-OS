using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TheGame;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace HentHub;

public class DependencyDialog : Window {
    private List<string> _missingDependencies;
    private Action _onCancel;
    private Action _onInstallAll;
    private Action _onIgnore;
    
    public DependencyDialog(List<string> missing, Action onCancel, Action onInstallAll, Action onIgnore) 
        : base(Vector2.Zero, new Vector2(400, 300)) {
        Title = "Missing Dependencies";
        _missingDependencies = missing;
        _onCancel = onCancel;
        _onInstallAll = onInstallAll;
        _onIgnore = onIgnore;
        
        IsModal = true;

        var viewport = G.GraphicsDevice.Viewport;
        var pos = new Vector2(viewport.Width / 2 - Size.X / 2, viewport.Height / 2 - Size.Y / 2);
        Position = pos;

        SetupUI();
    }

    private void SetupUI() {
        var label = new Label(new Vector2(15, 15), "The following dependencies are missing:") {
            FontSize = 14,
            Color = Color.LightGray
        };
        AddChild(label);

        var listPanel = new ScrollPanel(new Vector2(15, 40), new Vector2(ClientSize.X - 30, ClientSize.Y - 100));
        AddChild(listPanel);

        float y = 0;
        foreach (var dep in _missingDependencies) {
            var depLabel = new Label(new Vector2(5, y), $"- {dep}") {
                FontSize = 14,
                Color = Color.White
            };
            listPanel.AddChild(depLabel);
            y += 20;
        }
        listPanel.UpdateContentHeight(y);

        var cancelBtn = new Button(new Vector2(15, ClientSize.Y - 45), new Vector2(100, 30), "Cancel") {
            OnClickAction = () => {
                _onCancel?.Invoke();
                Close();
            }
        };
        AddChild(cancelBtn);

        var installAllBtn = new Button(new Vector2(125, ClientSize.Y - 45), new Vector2(120, 30), "Install All") {
            BackgroundColor = new Color(0, 100, 0),
            OnClickAction = () => {
                _onInstallAll?.Invoke();
                Close();
            }
        };
        AddChild(installAllBtn);

        var ignoreBtn = new Button(new Vector2(ClientSize.X - 145, ClientSize.Y - 45), new Vector2(130, 30), "Ignore") {
            BackgroundColor = new Color(80, 40, 40),
            FontSize = 11,
            OnClickAction = () => {
                _onIgnore?.Invoke();
                Close();
            }
        };
        ignoreBtn.Tooltip = "I KNOW WHAT I'M DOING";
        AddChild(ignoreBtn);
    }
}
