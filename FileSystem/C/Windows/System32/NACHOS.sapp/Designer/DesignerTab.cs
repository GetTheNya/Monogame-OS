using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core.OS.History;
using TheGame.Core;
using TheGame.Core.Input;
using TheGame.Core.Designer;

using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using NACHOS;

namespace NACHOS.Designer;

public class DesignerTab : NachosTab {
    public string FilePath { get => base.FilePath; set => base.FilePath = value; }
    private DesignerSurface _surface;
    private ScrollPanel _surfaceScrollPanel;
    private PropertyGrid _propertyGrid;
    private ToolboxPanel _toolbox;
    private HierarchyPanel _hierarchy;
    private Panel _toolbar;
    public string ProjectPath { get; set; }
    
    private System.Runtime.Loader.AssemblyLoadContext _loadContext;
    private Action<bool> _modeChangedHandler;

    public override CommandHistory History { get; } = new();
    public override bool IsDirty => History.IsDirty;
    public new Action OnDirtyChanged { get; set; }
    public override string DisplayTitle => Path.GetFileName(FilePath) + (IsDirty ? "*" : "");


    public DesignerTab(Vector2 position, Vector2 size, string filePath, string projectPath) : base(position, size, filePath) {
        FilePath = filePath;
        ProjectPath = projectPath;
        
        float sidebarWidth = 170;
        float propertyWidth = 250;
        float toolbarHeight = 35;
        
        _toolbar = new Panel(Vector2.Zero, new Vector2(size.X, toolbarHeight)) {
            BackgroundColor = new Color(35, 35, 35)
        };
        
        var saveBtn = new Button(new Vector2(5, 5), new Vector2(80, 25), "Save") {
            OnClickAction = () => Save()
        };
        _toolbar.AddChild(saveBtn);
        
        var refreshBtn = new Button(new Vector2(90, 5), new Vector2(130, 25), "Refresh Toolbox") {
            OnClickAction = () => {
                _ = RefreshToolboxAsync();
            }
        };
        _toolbar.AddChild(refreshBtn);

        var designToggle = new Checkbox(new Vector2(225, 7), "Design Mode") {
            Value = DesignMode.IsEnabled,
            OnValueChanged = (val) => DesignMode.SetEnabled(val)
        };
        _toolbar.AddChild(designToggle);

        // Keep checkbox in sync if mode is changed elsewhere (e.g. tab switch)
        _modeChangedHandler = (enabled) => {
            designToggle.SetValue(enabled, false); // Don't notify to avoid loops
        };
        DesignMode.OnModeChanged += _modeChangedHandler;
        
        AddChild(_toolbar);
        
        _toolbox = new ToolboxPanel(new Vector2(0, toolbarHeight), new Vector2(sidebarWidth, (size.Y - toolbarHeight) * 0.4f));
        AddChild(_toolbox);
 
        _surfaceScrollPanel = new ScrollPanel(new Vector2(sidebarWidth, toolbarHeight), new Vector2(size.X - sidebarWidth - propertyWidth, size.Y - toolbarHeight));
        _surface = new DesignerSurface(Vector2.Zero, _surfaceScrollPanel.Size);
        _surface.History = History;
        _surfaceScrollPanel.AddChild(_surface);
        AddChild(_surfaceScrollPanel);
        
        _hierarchy = new HierarchyPanel(new Vector2(0, toolbarHeight + _toolbox.Size.Y), new Vector2(sidebarWidth, size.Y - (toolbarHeight + _toolbox.Size.Y)), _surface, History);
        AddChild(_hierarchy);
        
        _propertyGrid = new PropertyGrid(new Vector2(size.X - propertyWidth, toolbarHeight), new Vector2(propertyWidth, size.Y - toolbarHeight));
        _propertyGrid.History = History;
        AddChild(_propertyGrid);
        
        _surface.OnSelectionChanged += (el) => _propertyGrid.Inspect(el);
        _surface.OnElementModified += (el) => {
            History.NotifyChanged(); // Trigger dirty check
            _propertyGrid.Inspect(el);
            _hierarchy.Refresh();
        };

        History.OnHistoryChanged += () => {
             OnDirtyChanged?.Invoke();
             _hierarchy?.Refresh();
        };
        
        OnResize += () => {
            _toolbar.Size = new Vector2(Size.X, toolbarHeight);
            _toolbox.Size = new Vector2(sidebarWidth, (Size.Y - toolbarHeight) * 0.4f);
            
            _hierarchy.Position = new Vector2(0, toolbarHeight + _toolbox.Size.Y);
            _hierarchy.Size = new Vector2(sidebarWidth, Size.Y - (toolbarHeight + _toolbox.Size.Y));

            _propertyGrid.Position = new Vector2(Size.X - propertyWidth, toolbarHeight);
            _propertyGrid.Size = new Vector2(propertyWidth, Size.Y - toolbarHeight);
            
            _surfaceScrollPanel.Position = new Vector2(sidebarWidth, toolbarHeight);
            _surfaceScrollPanel.Size = new Vector2(Size.X - sidebarWidth - propertyWidth, Size.Y - toolbarHeight);
            
            // Note: DesignerSurface will automatically resize itself in its Update method
            // based on its children and the ScrollPanel's viewport size.
        };
        
        Load();

        // Handle toolbox drops
        _surface.OnDropReceived += (data, pos) => {
            if (data is ControlTypeDragData toolData) {
                var instance = Activator.CreateInstance(toolData.ControlType) as UIControl;
                if (instance != null) {
                    instance.Position = pos;
                    var root = _surface.ContentLayer.Children.FirstOrDefault() as UIControl;
                    if (root != null) {
                        History.Execute(new AddElementCommand(root, instance));
                        _surface.SelectElement(instance);
                        return true;
                    }
                }
            }
            return false;
        };
    }
    
    public override void Save() {
        if (string.IsNullOrEmpty(FilePath)) return;
        
        try {
            var root = _surface.ContentLayer.Children.FirstOrDefault();
            if (root != null) {
                string json = UISerializer.Serialize(root);
                VirtualFileSystem.Instance.WriteAllText(FilePath, json);
                History.MarkAsSaved();
                OnDirtyChanged?.Invoke();
                Shell.Notifications.Show("Designer", "UI Layout saved successfully.");
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error saving UI to {FilePath}: {ex.Message}");
            Shell.Notifications.Show("Designer", "Error saving layout: " + ex.Message);
        }
    }

    public override void Undo() => History.Undo();
    public override void Redo() => History.Redo();
    
    public void Load() {
        if (VirtualFileSystem.Instance.Exists(FilePath)) {
            try {
                string json = VirtualFileSystem.Instance.ReadAllText(FilePath);
                var root = UISerializer.Deserialize(json);
                if (root != null) {
                    _surface.ContentLayer.ClearChildren();
                    _surface.ContentLayer.AddChild(root);
                    _hierarchy.Refresh();
                }
            } catch (Exception ex) {
                DebugLogger.Log($"Error loading UI from {FilePath}: {ex.Message}");
                Shell.Notifications.Show("Designer", "Error loading layout: " + ex.Message);
            }
        }
        
        // If empty / new layout, add default Window base component
        if (!_surface.ContentLayer.Children.Any()) {
            var defaultWindow = new DesignerWindow(new Vector2(50, 50), new Vector2(400, 300)) {
                Title = "New Window"
            };
            _surface.ContentLayer.AddChild(defaultWindow);
            _surface.SelectElement(defaultWindow);
            
            History.Clear(); // Don't want adding default to be undoable at start
            History.MarkAsSaved();
            OnDirtyChanged?.Invoke();
        }
    }

    public async Task RefreshToolboxAsync() {
        if (string.IsNullOrEmpty(ProjectPath)) return;

        try {
            DesignMode.IsToolboxGeneration = true;

            // 1. Serialization of current state
            string currentUi = UISerializer.Serialize(_surface.ContentLayer.Children.FirstOrDefault());

            // 2. Cleanup
            _surface.ContentLayer.ClearChildren();
            _toolbox.ClearItems();
            _toolbox.RegisterSystemComponents();
            
            if (_loadContext != null) {
                _loadContext.Unload();
                _loadContext = null;
                _surface.UserAssembly = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            // 3. Scan & Compile
            string hostProjectPath = VirtualFileSystem.Instance.ToHostPath(ProjectPath);
            if (!Directory.Exists(hostProjectPath)) return;

            var csFiles = Directory.GetFiles(hostProjectPath, "*.cs", SearchOption.AllDirectories);
            var sourceFiles = new Dictionary<string, string>();
            foreach (var f in csFiles) sourceFiles[f] = File.ReadAllText(f);

            // 3.1 Load Manifest References
            string[] projectReferences = null;
            string manifestPath = Path.Combine(hostProjectPath, "manifest.json");
            if (File.Exists(manifestPath)) {
                try {
                    string json = File.ReadAllText(manifestPath);
                    var manifest = AppManifest.FromJson(json);
                    projectReferences = manifest.References;
                } catch (Exception ex) {
                    DebugLogger.Log($"[Toolbox Scan] Failed to read manifest: {ex.Message}");
                }
            }

            _loadContext = new AssemblyLoadContext("UserComponents", isCollectible: true);
            
            // Iterative Compilation
            Assembly assembly = null;
            List<string> errorFiles = new List<string>();
            List<string> lastDiagnostics = new List<string>();

            while (sourceFiles.Count > 0) {
                IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics;
                assembly = AppCompiler.Instance.CompileCollectible(sourceFiles, "UserComponents", out diagnostics, _loadContext, projectReferences);
                if (assembly != null) break;

                lastDiagnostics.Clear();
                var fatalErrors = diagnostics.Where(d => (int)d.Severity == (int)Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();
                foreach (var diag in diagnostics) {
                    string msg = $"{diag.Severity} {diag.Id}: {diag.GetMessage()} at {diag.Location}";
                    DebugLogger.Log($"[Toolbox Scan] {msg}");
                    if ((int)diag.Severity == (int)Microsoft.CodeAnalysis.DiagnosticSeverity.Error) lastDiagnostics.Add(msg);
                }
                if (fatalErrors.Count == 0) break; 

                var filesToRemove = fatalErrors
                    .Select((Microsoft.CodeAnalysis.Diagnostic d) => d.Location.SourceTree?.FilePath)
                    .Where(f => f != null)
                    .Cast<string>()
                    .Distinct()
                    .ToList();
                    
                if (filesToRemove.Count == 0) break; 

                foreach (string f in filesToRemove) {
                    sourceFiles.Remove(f);
                    errorFiles.Add(System.IO.Path.GetFileName(f));
                }
            }

            if (errorFiles.Count > 0 || (assembly == null && lastDiagnostics.Count > 0)) {
                string errorMsg = "Compilation Issues:\n";
                if (errorFiles.Count > 0) errorMsg += "Skipped files: " + string.Join(", ", errorFiles) + "\n\n";
                if (assembly == null && lastDiagnostics.Count > 0) {
                    errorMsg += "Critical errors prevented compilation:\n" + string.Join("\n", lastDiagnostics.Take(3));
                }
                Shell.UI.OpenWindow(new MessageBox("Toolbox Refresh", errorMsg));
            }

            // 4. Discovery
            if (assembly != null) {
                _surface.UserAssembly = assembly;
                var userTypes = assembly.GetTypes()
                    .Where(t => t.IsPublic && !t.IsAbstract && !t.IsGenericType)
                    .Where(t => typeof(UIElement).IsAssignableFrom(t))
                    .Where(t => !typeof(WindowBase).IsAssignableFrom(t))
                    .Where(t => !t.IsDefined(typeof(DesignerIgnoreControl), true))
                    .Where(t => t.GetConstructor(Type.EmptyTypes) != null)
                    .ToList();

                if (userTypes.Count > 0) {
                    _toolbox.AddSeparator("User Components");
                    foreach (var type in userTypes) {
                        _toolbox.AddToolboxItem(type.Name, type);
                    }
                }
            }

            // 5. Restore State (Binding to new types)
            var newRoot = UISerializer.Deserialize(currentUi, assembly);
            if (newRoot != null) {
                _surface.ContentLayer.AddChild(newRoot);
                _hierarchy.Refresh();
            }

        } catch (Exception ex) {
            DebugLogger.Log($"Error refreshing toolbox: {ex.Message}");
            Shell.Notifications.Show("Designer", "Toolbox refresh failed: " + ex.Message);
        } finally {
            DesignMode.IsToolboxGeneration = false;
        }
    }
    
    public override void Dispose() {
        if (_modeChangedHandler != null) {
            DesignMode.OnModeChanged -= _modeChangedHandler;
        }
        base.Dispose();
    }
}
