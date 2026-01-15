using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using TheGame;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;

namespace FileExplorerApp;

public class AppSettings {
    public string LastPath { get; set; } = "C:\\";
}

public class FileExplorerWindow : Window {
    public static Window CreateWindow() {
        var settings = Shell.Settings.Load<AppSettings>();
        return new FileExplorerWindow(new Vector2(100, 100), new Vector2(800, 600), settings);
    }

    private string _currentPath = "C:\\";
    public string CurrentPath => _currentPath;
    private AppSettings _settings;
    private ScrollPanel _fileList;
    private MenuBar _menuBar;
    private TextInput _pathInput;

    public FileExplorerWindow(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "File Explorer";
        AppId = "EXPLORER";
        _settings = settings;
        _currentPath = _settings.LastPath;

        OnResize += () => LayoutUI();

        SetupUI();
        RefreshList();
    }

    private void SetupUI() {
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, 26f));
        _menuBar.AddMenu("File", m => {
            m.AddItem("New Window", () => Shell.UI.OpenWindow(CreateWindow()));
            m.AddSeparator();
            m.AddItem("Close", Close);
        });
        _menuBar.AddMenu("View", m => {
            m.AddItem("Refresh", RefreshList);
        });
        AddChild(_menuBar);

        _pathInput = new TextInput(new Vector2(5, 31), new Vector2(ClientSize.X - 10, 30)) {
            Value = _currentPath
        };
        _pathInput.OnSubmit += (val) => NavigateTo(val);
        AddChild(_pathInput);

        _fileList = new ScrollPanel(new Vector2(0, 66), new Vector2(ClientSize.X, ClientSize.Y - 66));
        _fileList.BackgroundColor = Color.Transparent;
        AddChild(_fileList);
    }

    private void LayoutUI() {
        _menuBar.Size = new Vector2(ClientSize.X, 26f);
        _pathInput.Size = new Vector2(ClientSize.X - 10, 30);
        _fileList.Size = new Vector2(ClientSize.X, ClientSize.Y - 66);
        RefreshList();
    }

    public void NavigateTo(string path) {
        if (VirtualFileSystem.Instance.IsDirectory(path)) {
            _currentPath = path;
            _pathInput.Value = path;
            _settings.LastPath = path;
            Shell.Settings.Save(_settings);
            RefreshList();
        }
    }

    public void RefreshList() {
        _fileList.ClearChildren();
        var files = VirtualFileSystem.Instance.GetFiles(_currentPath);
        var dirs = VirtualFileSystem.Instance.GetDirectories(_currentPath);

        float y = 5;
        foreach (var dir in dirs) AddItem(dir, true, ref y);
        foreach (var file in files) AddItem(file, false, ref y);
    }

    private void AddItem(string path, bool isDir, ref float y) {
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        var btn = new Button(new Vector2(5, y), new Vector2(_fileList.ClientSize.X - 10, 30), name) {
            BackgroundColor = Color.Transparent,
            TextAlign = TextAlign.Left,
            Icon = Shell.GetIcon(path)
        };
        btn.OnClickAction = () => {
            if (isDir) NavigateTo(path);
            else Shell.Execute(path);
        };
        _fileList.AddChild(btn);
        y += 35;
    }
}
