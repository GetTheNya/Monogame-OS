using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS.Terminal;
using TheGame.Graphics;
using TheGame;
using System.Threading.Tasks;
using System.Threading;
using TheGame.Core.Input;
using Microsoft.Xna.Framework.Input;
using System.Collections.Concurrent;
using NACHOS.Designer;

namespace NACHOS;

public class MainWindow : Window {
    private Sidebar _sidebar;
    private TabControl _tabControl;
    private TerminalControl _terminal;
    private ClosablePanel _sidebarPanel;
    private ClosablePanel _terminalPanel;
    private MenuBar _menuBar;
    private string _projectPath;
    private FileSystemWatcher _manifestWatcher;
    
    private bool _sidebarVisible = true;
    private bool _terminalVisible = true;
    private float _sidebarWidth = 200;
    private float _terminalHeight = 250;
    private List<string> _projectReferences = new();
    private readonly ConcurrentQueue<Action> _pendingUiActions = new();
    
    private List<OpenPage> _pages = new();

    private class OpenPage {
        public string Path;
        public NachosTab Tab;
        public TabPage Page;
        public CodeEditor Editor => (Tab as EditorTab)?.Editor;
    }

    public MainWindow() {
        Title = "NACHOS";
        Size = new Vector2(900, 600);
    }
    
    public MainWindow(string projectPath) : this() {
        Initialize(projectPath);
    }

    public void Initialize(string projectPath) {
        OpenProject(projectPath);
    }

    private void AddToRecent(string path) {
        var settings = Shell.AppSettings.Load<NachosSettings>(OwnerProcess);
        settings.RecentProjects.Remove(path); // Remove if exists to move to top
        settings.RecentProjects.Add(path);
        if (settings.RecentProjects.Count > 10) settings.RecentProjects.RemoveAt(0);
        Shell.AppSettings.Save(OwnerProcess, settings);
    }

    protected override void OnLoad() {
        FileIconHelper.Initialize(G.GraphicsDevice);
        SetupUI();
    }

    private void SetupUI() {
        // Menu Bar
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, 25));
        var fileMenu = new Menu("File");
        fileMenu.AddItem("New Project", OpenNewProjectWizard, "Ctrl+N");
        fileMenu.AddItem("New UI Layout", CreateNewUILayout, "Ctrl+Shift+N");
        fileMenu.AddItem("Open Project", () => {
            var fp = new FilePickerWindow("NACHOS - Open Project Folder", "C:\\", "", 
            FilePickerMode.ChooseDirectory, OpenProject);
            Shell.UI.OpenWindow(fp);
        }, "Ctrl+O");
        fileMenu.AddSeparator();
        fileMenu.AddItem("Save", SaveActiveFile, "Ctrl+S");
        fileMenu.AddItem("Exit", Close, "Alt+F4");
        _menuBar.AddMenu(fileMenu);
 
        var editMenu = new Menu("Edit");
        editMenu.AddItem("Undo", () => GetActiveTab()?.History?.Undo(), "Ctrl+Z");
        editMenu.AddItem("Redo", () => GetActiveTab()?.History?.Redo(), "Ctrl+Y");
        _menuBar.AddMenu(editMenu);

        var viewMenu = new Menu("View");
        viewMenu.AddItem("Project explorer", ToggleSidebar);
        viewMenu.AddItem("Terminal", ToggleTerminal);
        _menuBar.AddMenu(viewMenu);

        var buildMenu = new Menu("Build");
        buildMenu.AddItem("Run Project", RunProject, "F5");
        buildMenu.AddItem("Build Project", BuildProject, "Shift+F5");
        _menuBar.AddMenu(buildMenu);

        _menuBar.RegisterHotkeys(OwnerProcess);

        // Editor global hotkeys
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Alt+Up", () => GetActiveEditor()?.SwapLineUp());
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Alt+Down", () => GetActiveEditor()?.SwapLineDown());
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Ctrl+D", () => GetActiveEditor()?.DuplicateCurrentLine());
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Shift+Alt+Down", () => GetActiveEditor()?.DuplicateCurrentLine());
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Ctrl+/", () => GetActiveEditor()?.ToggleComment());
        
        Shell.Hotkeys.RegisterLocal(OwnerProcess, "Ctrl+Shift+Z", () => GetActiveTab()?.History?.Redo());

        AddChild(_menuBar);

        // Sidebar
        _sidebar = new Sidebar(Vector2.Zero, Vector2.Zero, _projectPath ?? "C:\\");
        _sidebar.OnFileSelected = OpenFile;
        _sidebarPanel = new ClosablePanel(new Vector2(0, 25), new Vector2(_sidebarWidth, ClientSize.Y - _terminalHeight - 25), "PROJECT EXPLORER");
        _sidebarPanel.SetContent(_sidebar);
        _sidebarPanel.OnClose = ToggleSidebar;
        AddChild(_sidebarPanel);

        // Tab Control
        _tabControl = new TabControl(new Vector2(_sidebarWidth, 25), new Vector2(ClientSize.X - _sidebarWidth, ClientSize.Y - _terminalHeight - 25));
        _tabControl.OnTabClosed += (index) => {
            if (index >= 0 && index < _pages.Count) {
                _pages[index].Tab.Dispose();
                _pages.RemoveAt(index);
                
                // Update sidebar highlight to new active tab or null
                if (_tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _pages.Count) {
                    _sidebar.SelectedPath = _pages[_tabControl.SelectedIndex].Path;
                } else {
                    _sidebar.SelectedPath = null;
                }
            }
        };
        _tabControl.OnTabChanged += (index) => {
            if (index >= 0 && index < _pages.Count) {
                var activePage = _pages[index];
                _sidebar.SelectedPath = activePage.Path;
            } else {
                _sidebar.SelectedPath = null;
            }
        };
        AddChild(_tabControl);

        // Terminal
        _terminal = new TerminalControl(Vector2.Zero, Vector2.Zero);
        _terminal.Backend.WorkingDirectory = _projectPath ?? "C:\\";
        
        _terminalPanel = new ClosablePanel(new Vector2(0, ClientSize.Y - _terminalHeight), new Vector2(ClientSize.X, _terminalHeight), "TERMINAL");
        _terminalPanel.SetContent(_terminal);
        _terminalPanel.OnClose = ToggleTerminal;
        AddChild(_terminalPanel);

        // Welcome Message
        _terminal.Execute("echo \"Welcome to NACHOS!\"");

        OnResize += RefreshLayout;
    }

    private void RefreshLayout() {
        if (_menuBar == null) return;

        float top = 25;
        float effSidebarWidth = _sidebarVisible ? _sidebarWidth : 0;
        float effTerminalHeight = _terminalVisible ? _terminalHeight : 0;

        _menuBar.Size = new Vector2(ClientSize.X, top);

        _sidebarPanel.IsVisible = _sidebarVisible;
        if (_sidebarVisible) {
            _sidebarPanel.Position = new Vector2(0, top);
            _sidebarPanel.Size = new Vector2(_sidebarWidth, ClientSize.Y - effTerminalHeight - top);
            _sidebar.UpdateLayout();
        }

        _terminalPanel.IsVisible = _terminalVisible;
        if (_terminalVisible) {
            _terminalPanel.Position = new Vector2(0, ClientSize.Y - _terminalHeight);
            _terminalPanel.Size = new Vector2(ClientSize.X, _terminalHeight);
        }

        _tabControl.Position = new Vector2(effSidebarWidth, top);
        _tabControl.Size = new Vector2(ClientSize.X - effSidebarWidth, ClientSize.Y - effTerminalHeight - top);
        _tabControl.RefreshLayout();

        foreach (var p in _pages) {
            p.Tab.Size = _tabControl.ContentArea.Size;
            p.Tab.UpdateLayout();
        }
    }

    private void ToggleSidebar() {
        _sidebarVisible = !_sidebarVisible;
        RefreshLayout();
    }

    private void ToggleTerminal() {
        _terminalVisible = !_terminalVisible;
        RefreshLayout();
    }

    private void OpenFile(string path) {
        // Check if already open
        for (int i = 0; i < _pages.Count; i++) {
             if (_pages[i].Path == path) {
                 _tabControl.SelectedIndex = i;
                 return;
             }
        }

        TabPage page;
        OpenPage pageInfo;

        string actualLayoutFile = path;
        if (path.EndsWith(".uilayout") && VirtualFileSystem.Instance.IsDirectory(path)) {
            actualLayoutFile = Path.Combine(path, "layout.json");
        }

        NachosTab tab = path.EndsWith(".uilayout") 
            ? new DesignerTab(Vector2.Zero, _tabControl.ContentArea.Size, path, _projectPath)
            : new EditorTab(Vector2.Zero, _tabControl.ContentArea.Size, path);

        var icon = FileIconHelper.GetIcon(path);
        page = _tabControl.AddTab(tab.DisplayTitle, icon);
        page.Content.AddChild(tab);
        pageInfo = new OpenPage { Path = path, Tab = tab, Page = page };

        tab.OnDirtyChanged = () => {
            page.Title = tab.DisplayTitle;
            page.TabButton.Text = page.Title;
        };

        if (tab is EditorTab editorTab) {
            var editor = editorTab.Editor;
            editor.Intelligence.SetWorkspaceContext(
                () => ProjectWorkspace.GetSources(_projectPath, GetOpenSources),
                () => ProjectWorkspace.GetReferences(_projectPath)
            );

            editorTab.OnContentChanged = () => UpdateEditorSize(editor);
            editorTab.OnSelectionChanged = () => { };
            editorTab.UpdateLayout();
        }
        
        _pages.Add(pageInfo);
        _sidebar.SelectedPath = path;
        _tabControl.SelectedIndex = _pages.Count - 1;
    }

    private void UpdateEditorSize(CodeEditor editor) {
        if (_tabControl == null || _tabControl.ContentArea == null) return;
        
        editor.Size = new Vector2(
            Math.Max(_tabControl.ContentArea.Size.X, editor.GetTotalWidth()), 
            Math.Max(_tabControl.ContentArea.Size.Y, editor.GetTotalHeight())
        );
    }
        
    private void OpenNewProjectWizard() {
        var settings = new ProjectSettings();
        var wizard = new ProjectWizardWindow(settings);
        wizard.OnFinished += (data) => {
            string projectPath = ProjectGenerator.CreateProject(OwnerProcess, data);
            OpenProject(projectPath);
        };
        Shell.UI.OpenWindow(wizard);
    }

    private void OpenProject(string path) {
        _projectPath = path;
        Title = "NACHOS - " + (path ?? "No Project");
        
        // Setup manifest watcher
        if (_manifestWatcher != null) {
            _manifestWatcher.EnableRaisingEvents = false;
            _manifestWatcher.Dispose();
            _manifestWatcher = null;
        }

        if (!string.IsNullOrEmpty(path)) {
            if (_sidebar != null) _sidebar.RootPath = path;
            if (_terminal != null) _terminal.Execute($"cd \"{path}\""); // Use command to update prompt folder correctly
            AddToRecent(path);
            
            // Initialize metadata manager
            ProjectMetadataManager.Initialize(path);
            
            // Initialize usage tracker for this project
            UsageTracker.Initialize();

            ReloadProjectReferences();

            // Watch manifest for changes
            var hostProjectPath = VirtualFileSystem.Instance.ToHostPath(path);
            if (!string.IsNullOrEmpty(hostProjectPath) && Directory.Exists(hostProjectPath)) {
                _manifestWatcher = new FileSystemWatcher(hostProjectPath, "manifest.json");
                _manifestWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                _manifestWatcher.Changed += (s, e) => {
                    _pendingUiActions.Enqueue(() => {
                        DebugLogger.Log("Manifest changed, reloading references...");
                        ReloadProjectReferences();
                        foreach (var p in _pages.Where(pg => pg.Editor != null)) {
                            p.Editor.Intelligence.TriggerAnalysis();
                        }
                    });
                };
                _manifestWatcher.EnableRaisingEvents = true;
            }
        }
    }

    private void ReloadProjectReferences() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        try {
            string manifestPath = Path.Combine(_projectPath, "manifest.json");
            if (VirtualFileSystem.Instance.Exists(manifestPath)) {
                string json = VirtualFileSystem.Instance.ReadAllText(manifestPath);
                var manifest = AppManifest.FromJson(json);
                _projectReferences = manifest.References?.ToList() ?? new List<string>();
            } else {
                _projectReferences = new List<string>();
            }
        } catch (Exception ex) {
            DebugLogger.Log("Failed to load project references: " + ex.Message);
            _projectReferences = new List<string>();
        }
    }

    private void SaveActiveFile() {
        if (_tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _pages.Count) {
            _pages[_tabControl.SelectedIndex].Tab.Save();
        }
    }

    private void CreateNewUILayout() {
        if (string.IsNullOrEmpty(_projectPath)) return;

        var fp = new FilePickerWindow("Create New UI Layout", _projectPath, "NewLayout", FilePickerMode.Save, (path) => {
            // Ensure path ends with .uilayout directory name
            if (!path.EndsWith(".uilayout")) path += ".uilayout";
            
            if (VirtualFileSystem.Instance.Exists(path)) {
                Shell.Notifications.Show("Designer", "A layout with this name already exists.");
                return;
            }

            string layoutName = Path.GetFileNameWithoutExtension(path);
            string projectNamespace = "App";

            // Try to find project namespace from ProjectMetadataManager
            try {
                projectNamespace = ProjectMetadataManager.GetNamespace();
            } catch { /* Fallback to 'App' */ }

            // Create Directory
            VirtualFileSystem.Instance.CreateDirectory(path);

            // 1. layout.json
            var root = new Window { Title = layoutName, Size = new Vector2(400, 300) };
            string layoutJson = UISerializer.Serialize(root);
            VirtualFileSystem.Instance.WriteAllText(Path.Combine(path, "layout.json"), layoutJson);

            // 2. [Name].cs (User Code)
            string userTemplatePath = "C:/Windows/System32/NACHOS.sapp/Templates/Designer/UserCode.txt";
            string codeBehind = "// Template not found";
            if (VirtualFileSystem.Instance.Exists(userTemplatePath)) {
                codeBehind = VirtualFileSystem.Instance.ReadAllText(userTemplatePath)
                    .Replace("{namespace}", projectNamespace)
                    .Replace("{className}", layoutName);
            }
            VirtualFileSystem.Instance.WriteAllText(Path.Combine(path, layoutName + ".cs"), codeBehind);

            // 3. [Name].Designer.cs (Generated Code)
            string designerTemplatePath = "C:/Windows/System32/NACHOS.sapp/Templates/Designer/DesignerCode.txt";
            string designerCode = "// Template not found";
            if (VirtualFileSystem.Instance.Exists(designerTemplatePath)) {
                designerCode = VirtualFileSystem.Instance.ReadAllText(designerTemplatePath)
                    .Replace("{namespace}", projectNamespace)
                    .Replace("{className}", layoutName)
                    .Replace("{fields}", "")
                    .Replace("{construction}", "        // UI construction will be generated here");
            }
            VirtualFileSystem.Instance.WriteAllText(Path.Combine(path, layoutName + ".Designer.cs"), designerCode);

            OpenFile(path);
        });
        Shell.UI.OpenWindow(fp);
    }

    private void BuildProject() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        if (!_terminalVisible) ToggleTerminal();
        _terminal.Execute($"sappc \"{_projectPath}\"");
    }
 
    private void RunProject() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        if (!_terminalVisible) ToggleTerminal();
        _terminal.Execute($"sappc \"{_projectPath}\" -run");
    }

    public override void Update(GameTime gameTime) {
        while (_pendingUiActions.TryDequeue(out var action)) {
            try {
                action();
            } catch (Exception ex) {
                DebugLogger.Log("Error executing pending action: " + ex.Message);
            }
        }
        base.Update(gameTime);
    }

    public override void Undo() => GetActiveTab()?.Undo();
    public override void Redo() => GetActiveTab()?.Redo();

    protected override void ExecuteClose() {
        _manifestWatcher?.Dispose();
        _manifestWatcher = null;

        // Save IntelliSense usage tracking data
        UsageTracker.Shutdown();
        
        base.ExecuteClose();
    }
    private CodeEditor GetActiveEditor() {
        if (_tabControl == null || _tabControl.SelectedPage == null) return null;
        var active = _pages.FirstOrDefault(p => p.Page == _tabControl.SelectedPage);
        return active?.Editor;
    }
    
    private NachosTab GetActiveTab() {
        if (_tabControl == null || _tabControl.SelectedPage == null) return null;
        var active = _pages.FirstOrDefault(p => p.Page == _tabControl.SelectedPage);
        return active?.Tab;
    }

    public IEnumerable<(string Path, string Content)> GetOpenSources() {
        return _pages.Where(p => p.Editor != null).Select(p => (p.Path, p.Editor.Value));
    }
}
