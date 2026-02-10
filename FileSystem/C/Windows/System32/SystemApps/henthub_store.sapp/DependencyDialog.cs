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
    private DependencyNode _root;
    private Action _onCancel;
    private Action<List<string>> _onInstallAll;
    private Action _onIgnore;
    
    public DependencyDialog(DependencyNode root, Action onCancel, Action<List<string>> onInstallAll, Action onIgnore) 
        : base(Vector2.Zero, new Vector2(450, 400)) {
        Title = "Missing Dependencies";
        _root = root;
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
        var label = new Label(new Vector2(15, 15), "The following dependencies will be installed:") {
            FontSize = 14,
            Color = Color.LightGray
        };
        AddChild(label);

        var listPanel = new ScrollPanel(new Vector2(15, 40), new Vector2(ClientSize.X - 30, ClientSize.Y - 100));
        AddChild(listPanel);

        float y = 5;
        RenderNode(listPanel, _root, 0, ref y);
        listPanel.UpdateContentHeight(y + 10);

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
                _onInstallAll?.Invoke(_root.GetFlatMissingList());
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

    private void RenderNode(ScrollPanel panel, DependencyNode node, int depth, ref float y) {
        string indent = depth > 0 ? new string(' ', depth * 4) + "|- " : "";
        var nodeLabel = new Label(new Vector2(10, y), $"{indent}{node.Name}") {
            FontSize = 14,
            Color = node.IsInstalled ? Color.Gray : Color.White
        };
        panel.AddChild(nodeLabel);
        y += 20;

        foreach (var dep in node.Dependencies) {
            RenderNode(panel, dep, depth + 1, ref y);
        }
    }
}
