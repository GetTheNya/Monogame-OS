using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame;

namespace NACHOS;

public class WelcomeWindow : Window {
    private ScrollPanel _recentList;
    private NachosSettings _settings;
    private TaskCompletionSource<string> _tcs;

    public WelcomeWindow() {
        Title = "Welcome to NACHOS";
        Size = new Vector2(650, 450);
        // Center window will be handled by the OS usually, but we can set position if needed.
    }

    public async Task<string> WaitForSelectionAsync() {
        _tcs = new TaskCompletionSource<string>();
        return await _tcs.Task;
    }

    protected override void OnLoad() {
        _settings = Shell.AppSettings.Load<NachosSettings>(OwnerProcess);
        SetupUI();
    }

    private void SetupUI() {
        BackgroundColor = new Color(25, 25, 25);

        // Header
        var titleLabel = new Label(new Vector2(20, 20), "NACHOS") {
            FontSize = 36,
            TextColor = Color.White
        };
        AddChild(titleLabel);

        var subTitle = new Label(new Vector2(20, 65), "Native Application Creator for HentOS") {
            FontSize = 14,
            TextColor = Color.Gray
        };
        AddChild(subTitle);

        float leftColWidth = 200;
        
        // Actions Column
        var actionsPanel = new Panel(new Vector2(20, 100), new Vector2(leftColWidth, 300));
        actionsPanel.BackgroundColor = Color.Transparent;
        AddChild(actionsPanel);

        var newBtn = new Button(Vector2.Zero, new Vector2(leftColWidth, 35), "New Project") {
            BackgroundColor = new Color(45, 45, 45),
            HoverColor = new Color(60, 60, 60),
            OnClickAction = () => { /* New project logic */ }
        };
        actionsPanel.AddChild(newBtn);

        var openBtn = new Button(new Vector2(0, 45), new Vector2(leftColWidth, 35), "Open Project") {
            BackgroundColor = new Color(45, 45, 45),
            HoverColor = new Color(60, 60, 60),
            OnClickAction = OpenProjectFolder
        };
        actionsPanel.AddChild(openBtn);

        // Recent Projects Column
        var recentLabel = new Label(new Vector2(leftColWidth + 50, 100), "Recent Projects") {
            FontSize = 18,
            TextColor = Color.White
        };
        AddChild(recentLabel);

        _recentList = new ScrollPanel(new Vector2(leftColWidth + 50, 130), new Vector2(ClientSize.X - leftColWidth - 70, 270)) {
            BackgroundColor = new Color(30, 30, 30),
            BorderColor = new Color(50, 50, 50)
        };
        AddChild(_recentList);

        PopulateRecent();
    }

    private void PopulateRecent() {
        _recentList.ClearChildren();
        float y = 0;
        float itemHeight = 50;

        if (_settings.RecentProjects.Count == 0) {
            var empty = new Label(new Vector2(10, 10), "No recent projects") { TextColor = Color.Gray };
            _recentList.AddChild(empty);
            return;
        }

        foreach (var path in _settings.RecentProjects.AsEnumerable().Reverse()) {
            string currentPath = path;
            var btn = new Button(new Vector2(0, y), new Vector2(_recentList.Size.X - 15, itemHeight), "") {
                BackgroundColor = Color.Transparent,
                HoverColor = new Color(40, 40, 40),
                BorderColor = Color.Transparent,
                OnClickAction = () => SelectProject(currentPath)
            };

            var nameLabel = new Label(new Vector2(10, 5), Path.GetFileName(currentPath)) { FontSize = 16, TextColor = Color.White };
            var pathLabel = new Label(new Vector2(10, 28), currentPath) { FontSize = 10, TextColor = Color.Gray };
            
            btn.AddChild(nameLabel);
            btn.AddChild(pathLabel);
            
            _recentList.AddChild(btn);
            y += itemHeight;
        }
    }

    private void OpenProjectFolder() {
        var fp = new FilePickerWindow("Select Project Folder", "C:\\", "", FilePickerMode.ChooseDirectory, (path) => {
            SelectProject(path);
        });
        Shell.UI.OpenWindow(fp);
    }

    private void SelectProject(string path) {
        _tcs.TrySetResult(path);
        Close();
    }

    protected override void ExecuteClose() {
        _tcs.TrySetResult(null);
        base.ExecuteClose();
    }
}
