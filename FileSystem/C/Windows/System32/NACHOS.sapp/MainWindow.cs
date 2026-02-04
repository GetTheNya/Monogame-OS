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

namespace NACHOS;

public class MainWindow : Window {
    private Sidebar _sidebar;
    private TabControl _tabControl;
    private TerminalControl _terminal;
    private MenuBar _menuBar;
    private string _projectPath;
    private readonly ConcurrentQueue<Action> _pendingUiActions = new();
    
    private List<OpenPage> _pages = new();

    private class OpenPage {
        public string Path;
        public CodeEditor Editor;
        public TabPage Page;
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
        fileMenu.AddItem("New Project", () => { 
            /* TODO */ 
        }, "Ctrl+N");
        fileMenu.AddItem("Open Project", () => {
            var fp = new FilePickerWindow("NACHOS - Open Project Folder", "C:\\", "", 
            FilePickerMode.ChooseDirectory, OpenProject);
            Shell.UI.OpenWindow(fp);
        }, "Ctrl+O");
        fileMenu.AddSeparator();
        fileMenu.AddItem("Save", SaveActiveFile, "Ctrl+S");
        fileMenu.AddItem("Exit", Close, "Alt+F4");
        _menuBar.AddMenu(fileMenu);

        var buildMenu = new Menu("Build");
        buildMenu.AddItem("Run Project", RunProject, "F5");
        buildMenu.AddItem("Build Project", BuildProject, "Shift+F5");
        _menuBar.AddMenu(buildMenu);

        _menuBar.RegisterHotkeys(OwnerProcess);

        AddChild(_menuBar);

        // Sidebar
        float sidebarWidth = 200;
        _sidebar = new Sidebar(new Vector2(0, 25), new Vector2(sidebarWidth, ClientSize.Y - 200 - 25), _projectPath ?? "C:\\");
        _sidebar.OnFileSelected = OpenFile;
        AddChild(_sidebar);

        // Tab Control
        _tabControl = new TabControl(new Vector2(sidebarWidth, 25), new Vector2(ClientSize.X - sidebarWidth, ClientSize.Y - 200 - 25));
        _tabControl.OnTabClosed += (index) => {
            if (index >= 0 && index < _pages.Count) {
                _pages[index].Editor?.Dispose(); 
                _pages.RemoveAt(index);
            }
        };
        _tabControl.OnTabChanged += (index) => {
            if (_activePopup != null) {
                Shell.RemoveOverlayElement(_activePopup);
                _activePopup = null;
            }
            if (index >= 0 && index < _pages.Count) {
                QueueAnalysis(_pages[index]);
            }
        };
        AddChild(_tabControl);

        // Terminal
        _terminal = new TerminalControl(new Vector2(0, ClientSize.Y - 200), new Vector2(ClientSize.X, 200));
        _terminal.Backend.WorkingDirectory = _projectPath ?? "C:\\";
        AddChild(_terminal);

        // Welcome Message
        _terminal.Execute("echo \"Welcome to NACHOS!\"");

        OnResize += () => {
             _menuBar.Size = new Vector2(ClientSize.X, 25);
             _sidebar.Size = new Vector2(sidebarWidth, ClientSize.Y - 200 - 25);
             _tabControl.Position = new Vector2(sidebarWidth, 25);
             _tabControl.Size = new Vector2(ClientSize.X - sidebarWidth, ClientSize.Y - 200 - 25);
             _terminal.Position = new Vector2(0, ClientSize.Y - 200);
             _terminal.Size = new Vector2(ClientSize.X, 200);

             foreach (var p in _pages) {
                 if (p.Editor != null) {
                     p.Editor.Size = new Vector2(
                         Math.Max(_tabControl.ContentArea.Size.X, p.Editor.GetTotalWidth()), 
                         Math.Max(_tabControl.ContentArea.Size.Y, p.Editor.GetTotalHeight())
                     );
                 }
             }
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
        var icon = FileIconHelper.GetIcon(path);
        var page = _tabControl.AddTab(editor.FileName, icon);
        page.Content.AddChild(editor);
        
        var pageInfo = new OpenPage { Path = path, Editor = editor, Page = page };
        _pages.Add(pageInfo);

        editor.OnDirtyChanged = () => {
            page.Title = editor.FileName + (editor.IsDirty ? "*" : "");
            page.TabButton.Text = page.Title;
        };

        editor.UseInternalScrolling = false;
        editor.Size = new Vector2(
            Math.Max(_tabControl.ContentArea.Size.X, editor.GetTotalWidth()), 
            Math.Max(_tabControl.ContentArea.Size.Y, editor.GetTotalHeight())
        );

         editor.OnValueChanged += (val) => {
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

                 var currentPopup = _activePopup;
                 if (lastChar == '.' || lastChar == '(' || lastChar == ',') {
                     // Always re-trigger on these chars for context
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
                 } else {
                     // No word, and not trigger char: hide.
                     if (currentPopup != null) {
                         Shell.RemoveOverlayElement(currentPopup);
                         if (_activePopup == currentPopup) _activePopup = null;
                     }
                 }
             }
        };
        
        // Initial highlight
        QueueAnalysis(pageInfo);
        
        editor.OnCursorMoved += () => {
            if (_activePopup != null && _tabControl.SelectedPage == page) {
                var caretPos = editor.GetCaretPosition();
                if (caretPos != null) {
                    _activePopup.Position = caretPos.Value + new Vector2(0, 18);
                }
            }
        };

        _tabControl.SelectedIndex = _pages.Count - 1;
    }

    private void OpenProject(string path) {
        _projectPath = path;
        Title = "NACHOS - " + (path ?? "No Project");
        if (!string.IsNullOrEmpty(path)) {
            if (_sidebar != null) _sidebar.RootPath = path;
            if (_terminal != null) _terminal.Execute($"cd \"{path}\""); // Use command to update prompt folder correctly
            AddToRecent(path);
        }
    }

    private void SaveActiveFile() {
        if (_tabControl.SelectedIndex >= 0 && _tabControl.SelectedIndex < _pages.Count) {
            _pages[_tabControl.SelectedIndex].Editor.Save();
        }
    }

    private void BuildProject() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        _terminal.Execute($"sappc \"{_projectPath}\"");
    }
 
    private void RunProject() {
        if (string.IsNullOrEmpty(_projectPath)) return;
        _terminal.Execute($"sappc \"{_projectPath}\" -run");
    }

    private CompletionPopup _activePopup;
    private bool _isCompleting = false;
    private CancellationTokenSource _intelliSenseCts;

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
                    var caretPos = page.Editor.GetCaretPosition();
                    if (caretPos != null) {
                        currentPopup.Position = caretPos.Value + new Vector2(0, 18);
                    }
                } else {
                    Shell.RemoveOverlayElement(currentPopup);
                    if (_activePopup == currentPopup) _activePopup = null;
                }
            }

            if (page.Editor.IsFocused) {
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
        var pagesSnapshot = _pages.ToList();
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
                                page.Editor.ReplaceCurrentWord(item.Label); 
                                _isCompleting = false; 
                                
                                // Immediate update for color/diagnostics
                                QueueAnalysis(page);

                                 if (_activePopup != null) {
                                     Shell.RemoveOverlayElement(_activePopup);
                                     _activePopup = null;
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
    // --- Background Analysis ---

    private CancellationTokenSource _analysisCts;
    private Dictionary<string, System.Timers.Timer> _highlightTimers = new();

    private void QueueAnalysis(OpenPage page) {
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
        var pagesSnapshot = _pages.ToList();
        foreach (var p in pagesSnapshot) {
            sourceFiles[p.Path] = p.Editor.Value;
        }

        Task.Run(async () => {
            try {
                await Task.Delay(1000, token); // Wait a bit longer for diagnostics
                if (token.IsCancellationRequested) return;

                if (string.IsNullOrEmpty(_projectPath)) return;

                AppCompiler.Instance.Validate(sourceFiles, "NACHOS_ANALYSIS", out var diagnostics);
                
                // Group diagnostics by file
                var diagsByFile = diagnostics.GroupBy(d => d.Location.SourceTree?.FilePath).ToList();

                _pendingUiActions.Enqueue(() => {
                    if (token.IsCancellationRequested) return;
                    
                    foreach (var p in pagesSnapshot) {
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
 
    protected override void ExecuteClose() {
        if (_activePopup != null) {
            Shell.RemoveOverlayElement(_activePopup);
            _activePopup = null;
        }
        foreach (var p in _pages) {
            p.Editor.OnValueChanged = null;
            p.Editor.OnCursorMoved = null;
        }
        base.ExecuteClose();
    }
}
