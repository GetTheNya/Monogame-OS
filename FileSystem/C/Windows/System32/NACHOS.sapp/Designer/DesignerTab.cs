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
    
    // Code View Support
    private Panel _codeViewContainer;
    private CodeEditor _userCodeEditor;
    private CodeEditor _designerCodeEditor;
    private string _userCodePath;
    private string _designerCodePath;
    private ComboBox _viewModeCombo;

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
        
        // Mode Selector (for folder-based layouts)
        bool isFolderLayout = filePath.EndsWith(".uilayout", StringComparison.OrdinalIgnoreCase);
        
        if (isFolderLayout) {
            string layoutName = Path.GetFileNameWithoutExtension(filePath);
            _userCodePath = Path.Combine(filePath, layoutName + ".cs");
            _designerCodePath = Path.Combine(filePath, layoutName + ".Designer.cs");

            _viewModeCombo = new ComboBox(new Vector2(Size.X - 185, 5), new Vector2(180, 25));
            _viewModeCombo.Items.Add("Designer");
            _viewModeCombo.Items.Add("User Code");
            _viewModeCombo.Items.Add("Generated Code");
            _viewModeCombo.Value = 0;
            _viewModeCombo.OnValueChanged = (val) => SetViewMode(val);
            _toolbar.AddChild(_viewModeCombo);
        }

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

        // Code Editors
        _codeViewContainer = new Panel(new Vector2(0, toolbarHeight), new Vector2(size.X, size.Y - toolbarHeight)) {
            IsVisible = false,
            BackgroundColor = new Color(20, 20, 20)
        };
        _userCodeEditor = new CodeEditor(Vector2.Zero, _codeViewContainer.Size);
        _designerCodeEditor = new CodeEditor(Vector2.Zero, _codeViewContainer.Size) { IsReadOnly = true };
        
        _codeViewContainer.AddChild(_userCodeEditor);
        _codeViewContainer.AddChild(_designerCodeEditor);
        AddChild(_codeViewContainer);

        SetupEditorIntelligence();
        
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

            _codeViewContainer.Size = new Vector2(Size.X, Size.Y - toolbarHeight);
            _userCodeEditor.Size = _codeViewContainer.Size;
            _designerCodeEditor.Size = _codeViewContainer.Size;

            if (_viewModeCombo != null) {
                _viewModeCombo.Position = new Vector2(Size.X - 185, 5);
            }
        };
        
        _ = RefreshToolboxAsync(true);

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
    
    private void SetViewMode(int mode) {
        bool isDesigner = mode == 0;
        bool isUserCode = mode == 1;
        bool isGenCode = mode == 2;

        _surfaceScrollPanel.IsVisible = isDesigner;
        _toolbox.IsVisible = isDesigner;
        _hierarchy.IsVisible = isDesigner;
        _propertyGrid.IsVisible = isDesigner;
        _codeViewContainer.IsVisible = !isDesigner;

        if (isUserCode && VirtualFileSystem.Instance.Exists(_userCodePath)) {
            if (string.IsNullOrEmpty(_userCodeEditor.Value)) {
                _userCodeEditor.Value = VirtualFileSystem.Instance.ReadAllText(_userCodePath);
            }
            _userCodeEditor.IsVisible = true;
            _designerCodeEditor.IsVisible = false;
        } else if (isGenCode && VirtualFileSystem.Instance.Exists(_designerCodePath)) {
            _designerCodeEditor.Value = VirtualFileSystem.Instance.ReadAllText(_designerCodePath);
            _designerCodeEditor.IsVisible = true;
            _userCodeEditor.IsVisible = false;
        }
    }

    public override void Save() {
        if (string.IsNullOrEmpty(FilePath)) return;
        
        try {
            var root = _surface.ContentLayer.Children.FirstOrDefault();
            if (root != null) {
                // 1. Save Layout
                string json = UISerializer.Serialize(root);
                string layoutPath = VirtualFileSystem.Instance.IsDirectory(FilePath) ? Path.Combine(FilePath, "layout.json") : FilePath;
                VirtualFileSystem.Instance.WriteAllText(layoutPath, json);
                
                // 2. Sync Code-Behind if it's a folder layout
                if (!string.IsNullOrEmpty(_userCodePath)) {
                    // Update designer file
                    UpdateDesignerCode(root);
                    
                    // Save user code if it was ever loaded/edited
                    if (!string.IsNullOrEmpty(_userCodeEditor.Value)) {
                         VirtualFileSystem.Instance.WriteAllText(_userCodePath, _userCodeEditor.Value);
                    }
                }

                History.MarkAsSaved();
                OnDirtyChanged?.Invoke();
                Shell.Notifications.Show("Designer", "UI Layout saved successfully.");
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error saving UI to {FilePath}: {ex.Message}");
            Shell.Notifications.Show("Designer", "Error saving layout: " + ex.Message);
        }
    }

    private void UpdateDesignerCode(UIElement root) {
        if (string.IsNullOrEmpty(_designerCodePath)) return;

        try {
            string fileName = Path.GetFileNameWithoutExtension(_designerCodePath);
            string layoutName = fileName.Replace(".Designer", "");
            
            // Get namespace from project settings
            string projectNamespace = ProjectMetadataManager.GetNamespace();

            var fields = new List<string>();
            var construction = new System.Text.StringBuilder();
            var elementToVar = new Dictionary<UIElement, string>();
            elementToVar[root] = "this";

            // 1. Set properties of the root (this)
            var rootProps = UISerializer.GetSerializableProperties(root);
            foreach (var prop in rootProps) {
                if (prop.Name == "Name") continue;
                var val = prop.GetValue(root);
                string valCode = GetValueCode(val);
                if (valCode != null) {
                    construction.AppendLine($"        this.{prop.Name} = {valCode};");
                }
            }

            // 2. Generate children construction
            foreach (var child in root.Children) {
                if (DesignMode.IsDesignableElement(child)) {
                    construction.Append(GenerateConstructionCode(child, "this", fields, elementToVar));
                }
            }

            // 3. Load Template
            string templatePath = "C:/Windows/System32/NACHOS.sapp/Templates/Designer/DesignerCode.txt";
            string designerCode = "// Template not found";
            if (VirtualFileSystem.Instance.Exists(templatePath)) {
                designerCode = VirtualFileSystem.Instance.ReadAllText(templatePath)
                    .Replace("{namespace}", projectNamespace)
                    .Replace("{className}", layoutName)
                    .Replace("{fields}", string.Join("\n", fields))
                    .Replace("{construction}", construction.ToString().TrimEnd('\r', '\n'));
            }

            VirtualFileSystem.Instance.WriteAllText(_designerCodePath, designerCode);
        } catch (Exception ex) {
            DebugLogger.Log($"Error updating designer code: {ex.Message}");
        }
    }

    private string GenerateConstructionCode(UIElement el, string parentVar, List<string> fields, Dictionary<UIElement, string> elementToVar) {
        var sb = new System.Text.StringBuilder();
        string typeName = el.GetType().Name;
        string varName = !string.IsNullOrEmpty(el.Name) ? el.Name : $"_el{elementToVar.Count}";
        elementToVar[el] = varName;

        if (!string.IsNullOrEmpty(el.Name)) {
            fields.Add($"    private {typeName} {el.Name};");
            sb.AppendLine($"        {el.Name} = new {typeName}();");
        } else {
            sb.AppendLine($"        var {varName} = new {typeName}();");
        }

        // Set properties
        var props = UISerializer.GetSerializableProperties(el);
        foreach (var prop in props) {
            var val = prop.GetValue(el);
            string valCode = GetValueCode(val);
            if (valCode != null) {
                sb.AppendLine($"        {varName}.{prop.Name} = {valCode};");
            }
        }

        // Hierarchy
        if (parentVar == "this") {
            sb.AppendLine($"        this.AddChild({varName});");
        } else {
            sb.AppendLine($"        {parentVar}.AddChild({varName});");
        }

        // Recursion
        foreach (var child in el.Children) {
            if (DesignMode.IsDesignableElement(child)) {
                sb.Append(GenerateConstructionCode(child, varName, fields, elementToVar));
            }
        }

        return sb.ToString();
    }


    private string GetValueCode(object val) {
        if (val == null) return "null";
        if (val is string s) return $"\"{s.Replace("\"", "\\\"")}\"";
        if (val is bool b) return b ? "true" : "false";
        if (val is float f) return $"{f}f".Replace(",", ".");
        if (val is int i) return i.ToString();
        if (val is Vector2 v) return $"new Vector2({v.X}f, {v.Y}f)";
        if (val is Vector4 v4) return $"new Vector4({v4.X}f, {v4.Y}f, {v4.Z}f, {v4.W}f)";
        if (val is Color c) return $"new Color({c.R}, {c.G}, {c.B}, {c.A})";
        if (val is Enum e) return $"{e.GetType().Name}.{e}";
        return null;
    }

    public override void Undo() => History.Undo();
    public override void Redo() => History.Redo();
    
    public void Load() {
        if (VirtualFileSystem.Instance.Exists(FilePath)) {
            try {
                string layoutPath = VirtualFileSystem.Instance.IsDirectory(FilePath) ? Path.Combine(FilePath, "layout.json") : FilePath;
                string json = VirtualFileSystem.Instance.ReadAllText(layoutPath);
                var root = UISerializer.Deserialize(json, _surface.UserAssembly);
                if (root != null) {
                    _surface.ContentLayer.ClearChildren();
                    _surface.ContentLayer.AddChild(root);
                    _hierarchy.Refresh();
                    
                    // Sync code-behind on load (Requirement: "IDE must read it and generate code FOR user")
                    UpdateDesignerCode(root);
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

    public async Task RefreshToolboxAsync(bool initialLoad = false) {
        if (string.IsNullOrEmpty(ProjectPath)) return;

        try {
            DesignMode.IsToolboxGeneration = true;

            // 1. Serialization of current state
            var rootEl = _surface.ContentLayer.Children.FirstOrDefault();
            string currentUi = rootEl != null ? UISerializer.Serialize(rootEl) : null;

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
            if (!VirtualFileSystem.Instance.Exists(ProjectPath)) return;

            var csFiles = VirtualFileSystem.Instance.GetFilesRecursive(ProjectPath, "*.cs");
            var sourceFiles = new Dictionary<string, string>();
            foreach (var f in csFiles) sourceFiles[f] = VirtualFileSystem.Instance.ReadAllText(f);

            // 3.1 Load Manifest References
            string[] projectReferences = null;
            string manifestPath = Path.Combine(ProjectPath, "manifest.json");
            if (VirtualFileSystem.Instance.Exists(manifestPath)) {
                try {
                    string json = VirtualFileSystem.Instance.ReadAllText(manifestPath);
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
            if (initialLoad) {
                Load();
            } else if (currentUi != null) {
                var newRoot = UISerializer.Deserialize(currentUi, assembly);
                if (newRoot != null) {
                    _surface.ContentLayer.AddChild(newRoot);
                    _hierarchy.Refresh();
                }
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
        _userCodeEditor?.Dispose();
        _designerCodeEditor?.Dispose();
        base.Dispose();
    }

    private void SetupEditorIntelligence() {
        _userCodeEditor.FilePath = _userCodePath;
        _designerCodeEditor.FilePath = _designerCodePath;

        Func<Dictionary<string, string>> getSources = () => {
            // Priority:
            // 1. Current editors in THIS tab (might be unsaved in this specific designer session)
            // 2. Other open tabs in MainWindow
            // 3. Project on disk
            
            return ProjectWorkspace.GetSources(ProjectPath, () => {
                var list = new List<(string Path, string Content)>();
                if (!string.IsNullOrEmpty(_userCodePath)) list.Add((_userCodePath, _userCodeEditor.Value));
                if (!string.IsNullOrEmpty(_designerCodePath)) list.Add((_designerCodePath, _designerCodeEditor.Value));
                
                // Try to get from MainWindow if possible
                if (GetOwnerWindow() is MainWindow mw) {
                    list.AddRange(mw.GetOpenSources());
                }
                
                return list;
            });
        };

        Func<IEnumerable<string>> getRefs = () => ProjectWorkspace.GetReferences(ProjectPath);

        _userCodeEditor.Intelligence.SetWorkspaceContext(getSources, getRefs);
        _designerCodeEditor.Intelligence.SetWorkspaceContext(getSources, getRefs);
    }
}
