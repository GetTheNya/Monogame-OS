using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame;
using TheGame.Core.Input;
using TheGame.Core.OS.DragDrop;

namespace NACHOS;

public class Sidebar : ScrollPanel, IDropTarget {
    private string _rootPath;
    private List<Node> _rootNodes = new();
    public Action<string> OnFileSelected;
    public string SelectedPath { get; set; }

    public Sidebar(Vector2 position, Vector2 size, string rootPath) : base(position, size) {
        _rootPath = rootPath;
        BackgroundColor = new Color(25, 25, 25);
        Refresh();
    }

    public string RootPath {
        get => _rootPath;
        set {
            if (_rootPath != value) {
                _rootPath = value;
                Refresh();
            }
        }
    }

    public void Refresh() {
        ClearChildren();
        _rootNodes.Clear();
        
        var dirs = VirtualFileSystem.Instance.GetDirectories(_rootPath)
            .Where(d => !Path.GetFileName(d).StartsWith("."))
            .OrderBy(d => d);
        var files = VirtualFileSystem.Instance.GetFiles(_rootPath)
            .Where(f => !f.EndsWith(".nproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f);

        foreach (var d in dirs) _rootNodes.Add(new Node(d, 0, this));
        foreach (var f in files) _rootNodes.Add(new Node(f, 0, this));
        
        UpdateLayout();
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (Shell.Drag.IsActive && IsMouseOver) {
            Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
        }
    }

    protected override void UpdateInput() {
        base.UpdateInput();
        if (Shell.Drag.IsActive && IsMouseOver && InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
        }
    }

    public void UpdateLayout() {
        // We reuse node objects but adjust their positions based on expansion
        List<Node> visibleNodes = new();
        foreach (var node in _rootNodes) {
            node.BuildDisplayList(visibleNodes);
        }

        ClearChildren();
        float y = 5;
        foreach (var node in visibleNodes) {
            node.Position = new Vector2(0, y);
            node.Size = new Vector2(Size.X, 24);
            AddChild(node);
            y += 24;
        }
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // Use the path from context if set (by a Node), otherwise default to our root
        string targetPath = context.GetProperty<string>("VirtualPath") ?? _rootPath;

        var createMenu = new MenuItem { Text = "New..." };
        var createSubItems = new List<MenuItem>();
             
        createSubItems.Add(new MenuItem { Text = "File", Icon = GameContent.FileIcon, Action = () => PromptCreate(targetPath, false) });
        createSubItems.Add(new MenuItem { Text = "Folder", Icon = GameContent.FolderIcon, Action = () => PromptCreate(targetPath, true) });
        createSubItems.Add(new MenuItem { Type = MenuItemType.Separator });
             
        // Templates
        string templatePath = "C:\\Windows\\System32\\NACHOS.sapp\\Templates";
        if (VirtualFileSystem.Instance.IsDirectory(templatePath)) {
            var templates = VirtualFileSystem.Instance.GetFiles(templatePath).Where(f => f.EndsWith(".txt"));
            foreach (var t in templates) {
                string name = Path.GetFileNameWithoutExtension(t);
                createSubItems.Add(new MenuItem { 
                    Text = name, 
                    Action = () => PromptCreateTemplate(targetPath, t) 
                });
            }
        }
        createMenu.SubItems = createSubItems;
        items.Add(createMenu);

        items.Add(new MenuItem { Type = MenuItemType.Separator });

        var clipboardFiles = Shell.Clipboard.GetFiles();
        items.Add(new MenuItem { 
            Text = "Paste", 
            ShortcutText = "Ctrl+V", 
            IsEnabled = clipboardFiles != null && clipboardFiles.Count > 0,
            Action = () => PasteInto(targetPath)
        });

        items.Add(new MenuItem { Type = MenuItemType.Separator });
        items.Add(new MenuItem { Text = "Refresh", Action = Refresh });

        items.Add(new MenuItem { Type = MenuItemType.Separator });
        items.Add(new MenuItem { 
            Text = "Reveal in Explorer", 
            Icon = GameContent.FolderIcon,
            Action = () => {
                bool isDir = context.GetProperty<bool>("IsDirectory");
                Shell.Execute(isDir ? targetPath : _rootPath);
            }
        });
    }

    private void PromptCreate(string atPath, bool isFolder) {
        string title = isFolder ? "New Folder" : "New File";
        string msg = isFolder ? "Enter folder name:" : "Enter file name:";
        
        var dialog = new InputDialog(title, msg, "", (name) => {
            if (string.IsNullOrEmpty(name)) return;
            string newPath = Path.Combine(atPath, name);
            if (isFolder) VirtualFileSystem.Instance.CreateDirectory(newPath);
            else VirtualFileSystem.Instance.CreateFile(newPath);
            Refresh();
        });
        GetOwnerProcess().Application.OpenModal(dialog);
    }

    private void PromptCreateTemplate(string atPath, string templatePath) {
        string templateName = Path.GetFileNameWithoutExtension(templatePath);
        
        var dialog = new InputDialog($"New {templateName}", $"Enter {templateName} name:", "", (name) => {
            if (string.IsNullOrEmpty(name)) return;
            
            string fileName = name;
            if (templateName.ToLower() == "interface" && !fileName.StartsWith("I")) fileName = "I" + fileName;
            
            string newPath = Path.Combine(atPath, fileName + ".cs");
            string content = VirtualFileSystem.Instance.ReadAllText(templatePath);
            
            // Replacements
            content = content.Replace("{fileName}", fileName);
            content = content.Replace("{namespace}", GetNamespace(atPath));
            
            VirtualFileSystem.Instance.WriteAllText(newPath, content);
            Refresh();
            OnFileSelected?.Invoke(newPath);
        });
        GetOwnerProcess().Application.OpenModal(dialog);
    }

    private string GetNamespace(string path) {
         // Find manifest.json to get appId or project name
         string current = path;
         while (!string.IsNullOrEmpty(current) && current.Length > 3) {
             string manifest = Path.Combine(current, "manifest.json");
             if (VirtualFileSystem.Instance.Exists(manifest)) {
                 try {
                     string json = VirtualFileSystem.Instance.ReadAllText(manifest);
                     var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
                     if (data != null && data.TryGetValue("appId", out var appId)) return appId.ToString();
                 } catch {}
             }
             current = Path.GetDirectoryName(current);
         }
         return "MyNamespace";
    }

    private void PasteInto(string atPath) {
        var files = Shell.Clipboard.GetFiles();
        if (files == null) return;
        
        foreach (var f in files) {
            HandleSinglePaste(atPath, f, false);
        }
        Refresh();
    }

    private void HandleSinglePaste(string atPath, string sourcePath, bool isCopy) {
        string name = Path.GetFileName(sourcePath);
        string dest = Path.Combine(atPath, name);
        
        // Collision handle
        if (VirtualFileSystem.Instance.Exists(dest)) {
            string baseName = Path.GetFileNameWithoutExtension(name);
            string ext = Path.GetExtension(name);
            int i = 1;
            while (VirtualFileSystem.Instance.Exists(dest)) {
                dest = Path.Combine(atPath, $"{baseName} ({i++}){ext}");
            }
        }
        
        if (isCopy) {
            VirtualFileSystem.Instance.Copy(sourcePath, dest);
        } else {
            VirtualFileSystem.Instance.Move(sourcePath, dest);
        }
    }

    // === IDropTarget ===
    public bool CanAcceptDrop(object dragData) {
        var paths = GetPathsFromData(dragData);
        if (paths.Count == 0) return false;

        // Don't allow dropping into itself
        foreach (var p in paths) {
            if (_rootPath.Equals(p, StringComparison.OrdinalIgnoreCase)) return false;
            if (_rootPath.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    public DragDropEffect OnDragOver(object dragData, Vector2 position) {
        bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || 
                    InputManager.IsKeyDown(Keys.RightControl);
        
        if (!ctrl) {
            var paths = GetPathsFromData(dragData);
            string normRoot = NormalizePath(_rootPath);
            if (paths.Count > 0 && paths.All(p => string.Equals(NormalizePath(Path.GetDirectoryName(p)), normRoot, StringComparison.OrdinalIgnoreCase))) {
                return DragDropEffect.None;
            }
        }

        return ctrl ? DragDropEffect.Copy : DragDropEffect.Move;
    }

    public void OnDragLeave() { }

    public bool OnDrop(object dragData, Vector2 position) {
        var paths = GetPathsFromData(dragData);
        if (paths.Count == 0) return false;

        bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || 
                    InputManager.IsKeyDown(Keys.RightControl);

        try {
            string normRoot = NormalizePath(_rootPath);
            foreach (var p in paths) {
                // Ignore moves to same folder
                if (!ctrl && string.Equals(NormalizePath(Path.GetDirectoryName(p)), normRoot, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }
                HandleSinglePaste(_rootPath, p, ctrl);
            }
            Refresh();
            return true;
        } catch (Exception ex) {
            Shell.Notifications.Show("Error", ex.Message);
            return false;
        }
    }

    public Rectangle GetDropBounds() => Bounds;

    private List<string> GetPathsFromData(object data) {
        if (data is string s) return new List<string> { s };
        if (data is List<string> l) return l;
        if (data is IDraggable d) {
            var dragData = d.GetDragData();
            if (dragData is string s2) return new List<string> { s2 };
            if (dragData is List<string> l2) return l2;
        }
        return new List<string>();
    }

    private string NormalizePath(string path) {
        if (string.IsNullOrEmpty(path)) return "";
        return path.Replace('/', '\\').TrimEnd('\\').ToUpper();
    }
}
