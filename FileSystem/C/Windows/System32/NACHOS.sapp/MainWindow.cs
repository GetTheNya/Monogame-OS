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
    
    private bool _sidebarVisible = true;
    private bool _terminalVisible = true;
    private float _sidebarWidth = 200;
    private float _terminalHeight = 200;
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
            if (_activePopup != null) {
                Shell.RemoveOverlayElement(_activePopup);
                _activePopup = null;
            }
            if (index >= 0 && index < _pages.Count) {
                var activePage = _pages[index];
                QueueAnalysis(activePage);
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

        NachosTab tab = path.EndsWith(".uilayout") 
            ? new DesignerTab(Vector2.Zero, _tabControl.ContentArea.Size, path)
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
            editorTab.OnContentChanged = () => UpdateEditorSizeAndAnalysis(editor, pageInfo);
            editorTab.OnSelectionChanged = () => {
                if (_activePopup != null && _tabControl.SelectedPage == page) {
                    var caretPos = editor.GetCaretPosition();
                    if (caretPos != null) {
                        _activePopup.Position = caretPos.Value + new Vector2(0, 18);
                    }
                }
                ShowSignatureHelp(pageInfo);
            };
            editorTab.UpdateLayout();
        }
        
        _pages.Add(pageInfo);
        _sidebar.SelectedPath = path;
        _tabControl.SelectedIndex = _pages.Count - 1;
        
        if (pageInfo.Editor != null) QueueAnalysis(pageInfo);
    }

    private void UpdateEditorSizeAndAnalysis(CodeEditor editor, OpenPage pageInfo) {
        if (_isCompleting) return;
        var currentTabControl = _tabControl;
        if (currentTabControl == null || currentTabControl.ContentArea == null) return;
        
        editor.Size = new Vector2(
            Math.Max(currentTabControl.ContentArea.Size.X, editor.GetTotalWidth()), 
            Math.Max(currentTabControl.ContentArea.Size.Y, editor.GetTotalHeight())
        );
        QueueAnalysis(pageInfo);
        
        // Trigger IntelliSense logic
        int cursorIdx = editor.GetIndexFromPosition(editor.CursorLine, editor.CursorCol);

        // Suppress if in a comment (Green: 87, 166, 74)
        bool inComment = editor.Tokens.Any(t => cursorIdx > t.Start && cursorIdx <= t.Start + t.Length && t.Color == new Color(87, 166, 74));
        
        // Fast path: Check if current line has // before cursor (to catch it before highlighter runs)
        if (!inComment) {
            string line = editor.Lines[editor.CursorLine];
            int commentIdx = line.IndexOf("//");
            if (commentIdx != -1 && editor.CursorCol > commentIdx) inComment = true;
        }

        if (inComment) {
            if (_activePopup != null) {
                Shell.RemoveOverlayElement(_activePopup);
                _activePopup = null;
            }
            return;
        }

        string text = editor.Value;
        if (cursorIdx >= 0 && cursorIdx <= text.Length) {
            char lastChar = cursorIdx > 0 ? text[cursorIdx - 1] : '\0';
            
            // Get current word
            int start = editor.CursorCol;
            var lines = editor.Lines;
            if (lines == null || editor.CursorLine < 0 || editor.CursorLine >= lines.Count) return;
            string currentLine = lines[editor.CursorLine];
            while (start > 0 && start <= currentLine.Length && (char.IsLetterOrDigit(currentLine[start - 1]) || currentLine[start - 1] == '_')) start--;
            string word = currentLine.Substring(start, Math.Min(editor.CursorCol, currentLine.Length) - start);

            // Check if we're after a keyword that needs IntelliSense
            bool afterKeyword = false;
            if (lastChar == ' ' && start > 0) {
                int kwStart = start - 1;
                while (kwStart > 0 && currentLine[kwStart - 1] == ' ') kwStart--;
                int kwEnd = kwStart;
                while (kwStart > 0 && (char.IsLetterOrDigit(currentLine[kwStart - 1]) || currentLine[kwStart - 1] == '_')) kwStart--;
                string previousWord = currentLine.Substring(kwStart, kwEnd - kwStart);
                
                if (previousWord == "override" || previousWord == "new" || previousWord == "partial") {
                    afterKeyword = true;
                }
            }

            var currentPopup = _activePopup;
            if (lastChar == '.' || lastChar == '(' || lastChar == ',' || lastChar == '<' || lastChar == '{' || afterKeyword) {
                // Always re-trigger on these chars/contexts
                ShowIntelliSense(pageInfo);
            } else if (word.Length > 0) {
                // Words: trigger if just started, or update if exists
                if (currentPopup == null && word.Length >= 1 && !editor.IsFetchingCompletions) {
                    ShowIntelliSense(pageInfo, word);
                } else if (currentPopup != null) {
                    currentPopup.SearchQuery = word;
                    
                    // Sync position during typing
                    var caretPos = editor.GetCaretPosition();
                     if (caretPos != null) {
                          currentPopup.Position = caretPos.Value + new Vector2(0, 18);
                     }
                  }
            } else if (lastChar != ' ') {
                // No word and not a space: hide
                // Keep popup open if space after trigger chars like ", " in arguments
                if (currentPopup != null) {
                    Shell.RemoveOverlayElement(currentPopup);
                    if (_activePopup == currentPopup) _activePopup = null;
                }
            }
            // If lastChar == ' ' and currentPopup exists, do nothing (keep it open)
        }
        
        ShowSignatureHelp(pageInfo);
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
        if (!string.IsNullOrEmpty(path)) {
            if (_sidebar != null) _sidebar.RootPath = path;
            if (_terminal != null) _terminal.Execute($"cd \"{path}\""); // Use command to update prompt folder correctly
            AddToRecent(path);
            
            // Initialize metadata manager
            ProjectMetadataManager.Initialize(path);
            
            // Initialize usage tracker for this project
            UsageTracker.Initialize();
        }
    }

    private void SaveActiveFile() {
        if (_tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _pages.Count) {
            _pages[_tabControl.SelectedIndex].Tab.Save();
        }
    }

    private void CreateNewUILayout() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        var fp = new FilePickerWindow("Create New UI Layout", _projectPath, "new_layout.uilayout", FilePickerMode.Save, (path) => {
            if (!path.EndsWith(".uilayout")) path += ".uilayout";
            // Create empty layout
            var root = new Window { Title = "My Layout", Size = new Vector2(400, 300) };
            string json = UISerializer.Serialize(root);
            VirtualFileSystem.Instance.WriteAllText(path, json);
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

    private CompletionPopup _activePopup;
    private SignaturePopup _activeSignaturePopup;
    private bool _isCompleting = false;
    private CancellationTokenSource _intelliSenseCts;
    private CancellationTokenSource _signatureCts;

    public override void Update(GameTime gameTime) {
        // Process UI actions from background threads
        while (_pendingUiActions.TryDequeue(out var action)) action?.Invoke();

        // Check for Ctrl+Space BEFORE base.Update so we can consume it before children see it
        if (_tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _pages.Count) {
            var page = _pages[_tabControl.SelectedIndex];
            
            // Sync IntelliSense position if window moved (dragging) or hide if minimized
            var currentPopup = _activePopup;
            if (currentPopup != null) {
                if (IsVisible) {
                    var caretPos = page.Editor?.GetCaretPosition();
                    if (caretPos != null) {
                        currentPopup.Position = caretPos.Value + new Vector2(0, 18);
                    }
                } else {
                    Shell.RemoveOverlayElement(currentPopup);
                    if (_activePopup == currentPopup) _activePopup = null;
                }
            }

            if (page.Editor != null && page.Editor.IsFocused) {
                if (page.Editor.ActiveSnippetSession != null) {
                    page.Editor.ActiveSnippetSession.HandleInput();
                    
                    if (InputManager.IsKeyboardConsumed && (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter) || InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Tab))) {
                        if (_activePopup != null) {
                            Shell.RemoveOverlayElement(_activePopup);
                            _activePopup = null;
                        }
                    }
                }

                if (InputManager.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.LeftControl) && InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Space)) {
                    ShowIntelliSense(page);
                    InputManager.IsKeyboardConsumed = true;
                }
            }
        }

        base.Update(gameTime);
    }

    private void ShowIntelliSense(OpenPage page, string initialSearch = "") {
        _intelliSenseCts?.Cancel();
        _intelliSenseCts = new CancellationTokenSource();
        var token = _intelliSenseCts.Token;

        page.Editor.IsFetchingCompletions = true;

        var sourceFiles = new Dictionary<string, string>();
        var pagesSnapshot = _pages.Where(p => p.Editor != null).ToList();
        foreach (var p in pagesSnapshot) sourceFiles[p.Path] = p.Editor.Value;

        int pos = page.Editor.GetIndexFromPosition(page.Editor.CursorLine, page.Editor.CursorCol);
        
        Task.Run(async () => {
            try {
                // Small delay to allow auto-brackets or multiple fast keystrokes to settle
                await Task.Delay(20, token);
                if (token.IsCancellationRequested) return;

                var items = await IntelliSenseProvider.GetCompletionsAsync(sourceFiles, page.Path, pos);
                if (token.IsCancellationRequested) return;
                
                // Back to UI thread-sh (actually just add child)
                if (items.Count > 0) {
                    _pendingUiActions.Enqueue(() => {
                        if (token.IsCancellationRequested) return;
                        
                        var caretPos = page.Editor.GetCaretPosition();
                        if (caretPos != null) {
                            Vector2 popupPos = caretPos.Value + new Vector2(0, 18);
                            
                            var popup = new CompletionPopup(popupPos, items, (item) => {
                                _isCompleting = true;
                                page.Editor.History?.BeginTransaction("Completion: " + item.Label);
                                try {
                                     if (item.Kind == "M") {
                                        // Heuristic: check if inside an expression (unmatched opening paren before cursor)
                                        string line = page.Editor.Lines[page.Editor.CursorLine];
                                        int col = page.Editor.CursorCol;
                                        
                                        int parens = 0;
                                        for (int i = 0; i < col; i++) {
                                            if (line[i] == '(') parens++;
                                            else if (line[i] == ')') parens--;
                                        }
                                        bool inExpression = parens > 0;
    
                                        // Check if followed by ( or ;
                                        bool hasParenAfter = false;
                                        bool hasSemicolonAfter = false;
                                        for (int i = col; i < line.Length; i++) {
                                            if (char.IsWhiteSpace(line[i])) continue;
                                            if (line[i] == '(') hasParenAfter = true;
                                            if (line[i] == ';') hasSemicolonAfter = true;
                                            break;
                                        }
    
                                        string suffix = "";
                                        int back = 0;
                                        if (!hasParenAfter) {
                                            suffix += "()";
                                            back = 1;
                                        }
                                        if (!inExpression && !hasSemicolonAfter) {
                                            suffix += ";";
                                            if (back > 0) back++;
                                        }
    
                                        page.Editor.ReplaceCurrentWord(item.Label + suffix);
                                        if (back > 0) page.Editor.MoveCursor(-back, 0, false);
                                     } else if (item.Kind == "SN") {
                                        var snippet = SnippetManager.GetSnippets().FirstOrDefault(s => s.Shortcut == item.Label);
                                        if (snippet != null) {
                                            page.Editor.InsertSnippet(snippet);
                                        }
                                    } else {
                                        // Handle generic types (e.g. List -> List<>)
                                        bool isGeneric = (item.Kind == "C" || item.Kind == "S" || item.Kind == "I" || item.Kind == "D") && 
                                                         (item.Detail.Contains("<") || item.Detail.Contains(">"));
                                        
                                        if (isGeneric) {
                                            page.Editor.ReplaceCurrentWord(item.Label + "<>");
                                            page.Editor.MoveCursor(-1, 0, false);
                                        } else {
                                            page.Editor.ReplaceCurrentWord(item.Label); 
                                        }
                                    }
                                    
                                    // Immediate update for color/diagnostics
                                    QueueAnalysis(page);
    
                                     if (_activePopup != null) {
                                         Shell.RemoveOverlayElement(_activePopup);
                                         _activePopup = null;
                                     }
                                } finally {
                                    page.Editor.History?.EndTransaction();
                                    _isCompleting = false; 
                                }
                            }, initialSearch);

                            if (popup.VisibleItemsCount == 0) {
                                return;
                            }

                            popup.OnClosed = () => {
                                if (_activePopup == popup) _activePopup = null;
                            };

                             if (_activePopup != null) {
                                 Shell.RemoveOverlayElement(_activePopup);
                             }
                             _activePopup = popup;
                             Shell.AddOverlayElement(popup);
                         }
                    });
                 } else {
                     _pendingUiActions.Enqueue(() => {
                         if (_activePopup != null) {
                             Shell.RemoveOverlayElement(_activePopup);
                             _activePopup = null;
                         }
                     });
                 }
            } catch (OperationCanceledException) { 
            } catch (Exception ex) {
                DebugLogger.Log("IntelliSense Task Error: " + ex.Message);
            } finally {
                if (!token.IsCancellationRequested) {
                    _pendingUiActions.Enqueue(() => page.Editor.IsFetchingCompletions = false);
                }
            }
        }, token);
    }

    private void ShowSignatureHelp(OpenPage page) {
        _signatureCts?.Cancel();
        _signatureCts = new CancellationTokenSource();
        var token = _signatureCts.Token;

        var sourceFiles = new Dictionary<string, string>();
        foreach (var p in _pages.Where(pg => pg.Editor != null)) sourceFiles[p.Path] = p.Editor.Value;

        int pos = page.Editor.GetIndexFromPosition(page.Editor.CursorLine, page.Editor.CursorCol);

        Task.Run(async () => {
            try {
                var sig = await IntelliSenseProvider.GetSignatureHelpAsync(sourceFiles, page.Path, pos);
                if (token.IsCancellationRequested) return;

                _pendingUiActions.Enqueue(() => {
                    if (token.IsCancellationRequested) return;

                    if (sig == null) {
                        if (_activeSignaturePopup != null) {
                            Shell.RemoveOverlayElement(_activeSignaturePopup);
                            _activeSignaturePopup = null;
                        }
                        return;
                    }

                    var caretPos = page.Editor.GetCaretPosition();
                    if (caretPos != null) {
                        Vector2 popupPos = caretPos.Value - new Vector2(0, 35); // Show ABOVE the line
                        
                        if (_activeSignaturePopup == null) {
                            _activeSignaturePopup = new SignaturePopup(popupPos, sig.Value.MethodName, sig.Value.Parameters);
                            _activeSignaturePopup.ActiveIndex = sig.Value.ActiveIndex;
                            Shell.AddOverlayElement(_activeSignaturePopup);
                        } else {
                            // Update existing
                            _activeSignaturePopup.Position = popupPos;
                            _activeSignaturePopup.SetSignature(sig.Value.MethodName, sig.Value.Parameters);
                            _activeSignaturePopup.UpdateParameters(sig.Value.ActiveIndex);
                        }
                    }
                });
            } catch (Exception ex) {
                DebugLogger.Log("SignatureHelp Task Error: " + ex.Message);
            }
        }, token);
    }
    // --- Background Analysis ---

    private CancellationTokenSource _analysisCts;
    private Dictionary<string, System.Timers.Timer> _highlightTimers = new();

    private void QueueAnalysis(OpenPage page) {
        if (page.Editor == null) return;
        // Debounced Highlighter (Background then UI Update)
        if (!_highlightTimers.TryGetValue(page.Path, out var timer)) {
            timer = new System.Timers.Timer(500);
            timer.AutoReset = false;
            timer.Elapsed += (s, e) => {
                string text = "";
                // We need the UI thread to safely get the value, but timer elapsed is on a pool thread.
                _pendingUiActions.Enqueue(() => {
                    text = page.Editor.Value;
                    Task.Run(() => {
                        var tokens = CSharpHighlighter.Highlight(text);
                        _pendingUiActions.Enqueue(() => {
                            page.Editor.Tokens = tokens;
                        });
                    });
                });
            };
            _highlightTimers[page.Path] = timer;
        }
        timer.Stop();
        timer.Start();

        // Project-wide Diagnostics (Background task)
        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();
        var token = _analysisCts.Token;

        // Snapshot current values on UI thread
        var sourceFiles = new Dictionary<string, string>();
        var pagesSnapshot = _pages.Where(p => p.Editor != null).ToList();
        foreach (var p in pagesSnapshot) {
            sourceFiles[p.Path] = p.Editor.Value;
        }

        Task.Run(async () => {
            try {
                await Task.Delay(1000, token); // Wait a bit longer for diagnostics
                if (token.IsCancellationRequested) return;

                if (string.IsNullOrEmpty(_projectPath)) return;

                var compilation = AppCompiler.Instance.Validate(sourceFiles, "NACHOS_ANALYSIS", out var diagnostics);
                
                // Group diagnostics by file
                var diagsByFile = diagnostics.GroupBy(d => d.Location.SourceTree?.FilePath).ToList();

                _pendingUiActions.Enqueue(() => {
                    if (token.IsCancellationRequested) return;
                    
                    foreach (var p in pagesSnapshot) {
                        // Semantic Highlighting
                        var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == p.Path);
                        if (tree != null) {
                            var semanticModel = compilation.GetSemanticModel(tree);
                            var content = sourceFiles[p.Path];
                            Task.Run(() => {
                                var semanticTokens = CSharpHighlighter.Highlight(content, semanticModel);
                                _pendingUiActions.Enqueue(() => {
                                    if (p.Editor.Value == content) { // Ensure content hasn't changed since highlight started
                                        p.Editor.Tokens = semanticTokens;
                                    }
                                });
                            });
                        }

                        // Diagnostics
                        var fileDiags = diagsByFile.FirstOrDefault(g => g.Key == p.Path);
                        if (fileDiags != null) {
                            p.Editor.Diagnostics = fileDiags.Select(d => {
                                var span = d.Location.SourceSpan;
                                return new DiagnosticInfo(
                                    span.Start, 
                                    span.Length, 
                                    d.GetMessage(), 
                                    (DiagnosticSeverity)d.Severity
                                );
                            }).ToList();
                        } else {
                            p.Editor.Diagnostics.Clear();
                        }
                    }
                });
            } catch (TaskCanceledException) { }
            catch (Exception ex) {
                // Background errors shouldn't crash IDE
                DebugLogger.Log("Analysis Error: " + ex.Message);
            }
        }, token);
    }
    public override void Undo() => GetActiveTab()?.Undo();
    public override void Redo() => GetActiveTab()?.Redo();

    protected override void ExecuteClose() {
        // Save IntelliSense usage tracking data
        UsageTracker.Shutdown();
        
        if (_activeSignaturePopup != null) {
            Shell.RemoveOverlayElement(_activeSignaturePopup);
            _activeSignaturePopup = null;
        }
        if (_activePopup != null) {
            Shell.RemoveOverlayElement(_activePopup);
            _activePopup = null;
        }
        foreach (var p in _pages) {
            if (p.Editor != null) {
                p.Editor.OnValueChanged = null;
                p.Editor.OnCursorMoved = null;
            }
        }
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
}
