using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS.Terminal;
using TheGame.Graphics;
using TheGame;

namespace NACHOS;

public class MainWindow : Window {
    private Sidebar _sidebar;
    private TabControl _tabControl;
    private TerminalControl _terminal;
    private MenuBar _menuBar;
    private string _projectPath;
    public MainWindow() {
        Title = "NACHOS";
        Size = new Vector2(900, 600);
    }
    
    public MainWindow(string projectPath) : this() {
        Initialize(projectPath);
    }

    public void Initialize(string projectPath) {
        _projectPath = projectPath;
        Title = "NACHOS - " + (_projectPath ?? "No Project");
    }

    protected override void OnLoad() {
        SetupUI();
    }

    private void SetupUI() {
        // Menu Bar
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, 25));
        var fileMenu = new Menu("File");
        fileMenu.AddItem("New Project", () => { /* TODO */ });
        fileMenu.AddItem("Open Project", () => {
            var fp = new FilePickerWindow("NACHOS - Open Project Folder", "C:\\", "", FilePickerMode.ChooseDirectory, (path) => {
                _projectPath = path;
                 Title = "NACHOS - " + _projectPath;
                 _sidebar.Refresh(); // Need to update sidebar root
            });
             Shell.UI.OpenWindow(fp);
        });
        fileMenu.AddSeparator();
        fileMenu.AddItem("Save", SaveActiveFile);
        fileMenu.AddItem("Exit", Close);
        _menuBar.AddMenu(fileMenu);

        var buildMenu = new Menu("Build");
        buildMenu.AddItem("Build Project", BuildProject);
        buildMenu.AddItem("Run Project", RunProject);
        _menuBar.AddMenu(buildMenu);

        AddChild(_menuBar);

        // Sidebar
        float sidebarWidth = 200;
        _sidebar = new Sidebar(new Vector2(0, 25), new Vector2(sidebarWidth, ClientSize.Y - 200 - 25), _projectPath ?? "C:\\");
        _sidebar.OnFileSelected = OpenFile;
        AddChild(_sidebar);

        // Tab Control
        _tabControl = new TabControl(new Vector2(sidebarWidth, 25), new Vector2(ClientSize.X - sidebarWidth, ClientSize.Y - 200 - 25));
        AddChild(_tabControl);

        // Terminal
        _terminal = new TerminalControl(new Vector2(0, ClientSize.Y - 200), new Vector2(ClientSize.X, 200));
        _terminal.Backend.WorkingDirectory = _projectPath ?? "C:\\";
        AddChild(_terminal);

        OnResize += () => {
             _menuBar.Size = new Vector2(ClientSize.X, 25);
             _sidebar.Size = new Vector2(sidebarWidth, ClientSize.Y - 200 - 25);
             _tabControl.Position = new Vector2(sidebarWidth, 25);
             _tabControl.Size = new Vector2(ClientSize.X - sidebarWidth, ClientSize.Y - 200 - 25);
             _terminal.Position = new Vector2(0, ClientSize.Y - 200);
             _terminal.Size = new Vector2(ClientSize.X, 200);
        };
    }

    private void OpenFile(string path) {
        // Check if already open
        for (int i = 0; i < _pages.Count; i++) {
             if (_pages[i].Path == path) {
                 _tabControl.SelectedIndex = i;
                 return;
             }
        }

        var editor = new CodeEditor(Vector2.Zero, _tabControl.ContentArea.Size, path);
        var page = _tabControl.AddTab(editor.FileName);
        page.Content.AddChild(editor);
        
        var pageInfo = new OpenPage { Path = path, Editor = editor, Page = page };
        _pages.Add(pageInfo);

        editor.OnDirtyChanged = () => {
            page.Title = editor.FileName + (editor.IsDirty ? "*" : "");
            page.TabButton.Text = page.Title;
        };
        
        _tabControl.SelectedIndex = _pages.Count - 1;
    }

    private class OpenPage {
        public string Path;
        public CodeEditor Editor;
        public TabPage Page;
    }
    private List<OpenPage> _pages = new();

    private void SaveActiveFile() {
        if (_tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _pages.Count) {
            _pages[_tabControl.SelectedIndex].Editor.Save();
        }
    }

    private void BuildProject() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        _terminal.Backend.ExecuteCommand($"sappc \"{_projectPath}\"");
    }

    private void RunProject() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        _terminal.Backend.ExecuteCommand($"sappc \"{_projectPath}\" -run");
    }
}
