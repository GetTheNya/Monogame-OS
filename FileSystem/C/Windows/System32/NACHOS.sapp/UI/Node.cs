using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace NACHOS;

public class Node : Panel, IDraggable, IDropTarget {
    public string FullPath;
    public bool IsDirectory;
    public bool IsExpanded;
    public List<Node> NodeChildren = new();
    public Sidebar Sidebar;
    public int Depth;

    private Button _expandBtn;
    private Texture2D _iconTex;
    private Label _label;

    private Vector2 _dragStartPos;
    private bool _isHoveredByDrag;
    private float _originalOpacity = 1f;
    private float _hoverTimer = 0f;

    public Node(string path, int depth, Sidebar sidebar) : base(Vector2.Zero, new Vector2(0, 24)) {
        FullPath = path;
        Depth = depth;
        Sidebar = sidebar;
        IsDirectory = VirtualFileSystem.Instance.IsDirectory(path);
        
        BackgroundColor = Color.Transparent;
        BorderThickness = 0;
        ConsumesInput = true;
        
        SetupUI();
    }

    private void SetupUI() {
        float indent = Depth * 16;
        
        if (IsDirectory) {
            // Centered better in 24px row (y=2 for 20px button), smaller font to avoid clipping
            _expandBtn = new Button(new Vector2(indent + 2, 2), new Vector2(20, 20), "▶") {
                BackgroundColor = Color.Transparent,
                BorderColor = Color.Transparent,
                OnClickAction = ToggleExpand,
                FontSize = 10 
            };
            AddChild(_expandBtn);
        }

        _iconTex = FileIconHelper.GetIcon(FullPath);

        // Spaced accurately
        _label = new Label(new Vector2(indent + 46, 4), Path.GetFileName(FullPath)) {
            FontSize = 14,
            Color = Color.LightGray
        };
        AddChild(_label);
    }

    protected override void UpdateInput() {
        base.UpdateInput();
        if (!IsVisible) return;

        // Start drag detection
        if (IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            _dragStartPos = InputManager.MousePosition.ToVector2();
        }

        if (InputManager.IsMouseButtonDown(MouseButton.Left) && !Shell.Drag.IsActive && _dragStartPos != Vector2.Zero) {
            if (Vector2.Distance(_dragStartPos, InputManager.MousePosition.ToVector2()) > 5f) {
                Vector2 grabOffset = _dragStartPos - AbsolutePosition;
                Shell.Drag.BeginDraggable(this, AbsolutePosition, grabOffset);
                _dragStartPos = Vector2.Zero;
            }
        }

        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            _dragStartPos = Vector2.Zero;
        }

        // Handle drop if we are hovered and drag released
        if (Shell.Drag.IsActive && IsMouseOver && InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
        }
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Visual feedback for drop target
        if (Shell.Drag.IsActive && IsMouseOver) {
            Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());

            // Auto-expand folder on hover
            if (IsDirectory && _isHoveredByDrag && !IsExpanded) {
                _hoverTimer += dt;
                if (_hoverTimer >= 1.0f) {
                    ToggleExpand();
                    _hoverTimer = 0;
                }
            } else {
                _hoverTimer = 0;
            }
        } else {
            _isHoveredByDrag = false;
            _hoverTimer = 0;
        }

        BackgroundColor = _isHoveredByDrag ? new Color(60, 60, 60) : 
                         (Sidebar.SelectedPath == FullPath ? new Color(45, 45, 45) : Color.Transparent);
    }

    protected override void OnClick() {
        if (!IsDirectory) {
            Sidebar.OnFileSelected?.Invoke(FullPath);
        }
        base.OnClick();
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        
        float indent = Depth * 16;
        if (_iconTex != null) {
            // Moved icon slightly left but still clear of the button
            batch.DrawTexture(_iconTex, AbsolutePosition + new Vector2(indent + 24, 4), Color.White * AbsoluteOpacity, 16f / _iconTex.Width);
        }
    }

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
         // Determine the target directory for "Global" operations like New... or Paste
         string containerPath = IsDirectory ? FullPath : Path.GetDirectoryName(FullPath);
         context.SetProperty("IsDirectory", IsDirectory);
         context.SetProperty("VirtualPath", containerPath);

         // Group 1: Open
         if (!IsDirectory) {
             items.Add(new MenuItem { 
                 Text = "Open", 
                 IsDefault = true, 
                 Icon = GameContent.FileIcon, 
                 Action = () => Sidebar.OnFileSelected?.Invoke(FullPath) 
             });
             items.Add(new MenuItem { Type = MenuItemType.Separator });
         }
         
         // Group 2: Clipboard (Actions ON this node)
         items.Add(new MenuItem { Text = "Cut", ShortcutText = "Ctrl+X", Action = () => { Shell.Clipboard.SetFiles(new[] { FullPath }); } });
         items.Add(new MenuItem { Text = "Copy", ShortcutText = "Ctrl+C", Action = () => Shell.Clipboard.SetFiles(new[] { FullPath }) });
         
         items.Add(new MenuItem { Type = MenuItemType.Separator });

         // Group 3: Navigation / Paths
         items.Add(new MenuItem { Text = "Copy Path", Action = () => Shell.Clipboard.SetText(FullPath) });
         items.Add(new MenuItem { Text = "Copy Relative Path", Action = () => Shell.Clipboard.SetText(GetRelativePath()) });
         
         items.Add(new MenuItem { Type = MenuItemType.Separator });

         // Group 4: Refactoring
         items.Add(new MenuItem { Text = "Rename", ShortcutText = "F2", Action = () => PromptRename() });
         items.Add(new MenuItem { Text = "Duplicate", Action = () => Duplicate() });
         
         // Project group (Add Reference)
         if (IsDirectory && VirtualFileSystem.Instance.Exists(Path.Combine(FullPath, "manifest.json"))) {
             items.Add(new MenuItem { Type = MenuItemType.Separator });
             var refItem = new MenuItem { Text = "Add Reference...", Icon = GameContent.DiskIcon };
             var refSubItems = new List<MenuItem>();
             foreach (var r in AppCompiler.Instance.AvailableReferences) {
                 refSubItems.Add(new MenuItem { Text = r, Action = () => AddReference(r) });
             }
             refItem.SubItems = refSubItems;
             items.Add(refItem);
         }

         items.Add(new MenuItem { Type = MenuItemType.Separator });

         // Group 5: Deletion
         items.Add(new MenuItem { 
             Text = "Delete", 
             Action = () => {
                 var mb = new MessageBox("Delete", $"Are you sure you want to move {Path.GetFileName(FullPath)} to Recycle Bin?", MessageBoxButtons.YesNo, (ok) => {
                     if (ok) {
                         VirtualFileSystem.Instance.Recycle(FullPath);
                         Sidebar.Refresh();
                     }
                 });
                GetOwnerProcess().Application.OpenModal(mb);
             } 
         });

         // NOTE: New..., Paste, Refresh, and Reveal in Explorer will bubble up to Sidebar 
         // and use the "VirtualPath" we set above.
    }

    // === IDraggable ===
    public object GetDragData() => FullPath;
    public Texture2D GetDragIcon() => _iconTex;
    public string GetDragLabel() => Path.GetFileName(FullPath);

    public UIElement GetCustomDragVisual() {
        var panel = new Panel(Vector2.Zero, new Vector2(200, 24)) {
            BackgroundColor = new Color(0, 0, 0, 150),
            BorderColor = Color.White * 0.5f,
            BorderThickness = 1
        };

        if (_iconTex != null) {
            panel.AddChild(new Icon(new Vector2(4, 4), new Vector2(16, 16), _iconTex));
        }

        panel.AddChild(new Label(new Vector2(24, 4), Path.GetFileName(FullPath)) {
            FontSize = 14,
            Color = Color.White
        });

        return panel;
    }

    public void OnDragStart(Vector2 grabOffset) {
        _originalOpacity = Opacity;
        Opacity = 0.5f;
    }

    public void OnDragEnd() {
        Opacity = _originalOpacity;
    }

    public void OnDragCancel() {
        Opacity = _originalOpacity;
    }

    // === IDropTarget ===
    public bool CanAcceptDrop(object dragData) {
        if (!IsDirectory) return false;
        
        var paths = GetPathsFromData(dragData);
        if (paths.Count == 0) return false;

        // Don't allow dropping into itself or its own children
        foreach (var p in paths) {
            if (FullPath.Equals(p, StringComparison.OrdinalIgnoreCase)) return false;
            if (FullPath.StartsWith(p + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return false;
        }

        return true;
    }

    public DragDropEffect OnDragOver(object dragData, Vector2 position) {
        bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || 
                    InputManager.IsKeyDown(Keys.RightControl);
        
        _isHoveredByDrag = true;

        if (!ctrl) {
            var paths = GetPathsFromData(dragData);
            string normPath = NormalizePath(FullPath);
            if (paths.Count > 0 && paths.All(p => string.Equals(NormalizePath(Path.GetDirectoryName(p)), normPath, StringComparison.OrdinalIgnoreCase))) {
                _isHoveredByDrag = false;
                return DragDropEffect.None;
            }
        }

        return ctrl ? DragDropEffect.Copy : DragDropEffect.Move;
    }

    public void OnDragLeave() {
        _isHoveredByDrag = false;
        _hoverTimer = 0;
    }

    public bool OnDrop(object dragData, Vector2 position) {
        _isHoveredByDrag = false;
        _hoverTimer = 0;
        var paths = GetPathsFromData(dragData);
        if (paths.Count == 0) return false;

        bool ctrl = InputManager.IsKeyDown(Keys.LeftControl) || 
                    InputManager.IsKeyDown(Keys.RightControl);

        try {
            string normPath = NormalizePath(FullPath);
            foreach (var sourcePath in paths) {
                // Ignore moves to same folder
                if (!ctrl && string.Equals(NormalizePath(Path.GetDirectoryName(sourcePath)), normPath, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                string fileName = Path.GetFileName(sourcePath);
                string destPath = Path.Combine(FullPath, fileName);

                // Collision handling
                if (VirtualFileSystem.Instance.Exists(destPath)) {
                    string baseName = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    int i = 1;
                    while (VirtualFileSystem.Instance.Exists(destPath)) {
                        destPath = Path.Combine(FullPath, $"{baseName} ({i++}){ext}");
                    }
                }

                if (ctrl) {
                    VirtualFileSystem.Instance.Copy(sourcePath, destPath);
                } else {
                    VirtualFileSystem.Instance.Move(sourcePath, destPath);
                }
            }
            Sidebar.Refresh();
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

    private void PromptRename() {
        string oldName = Path.GetFileName(FullPath);
        var dialog = new InputDialog("Rename", "Enter new name:", oldName, (newName) => {
            if (string.IsNullOrEmpty(newName) || newName == oldName) return;
            string newPath = Path.Combine(Path.GetDirectoryName(FullPath), newName);
            VirtualFileSystem.Instance.Move(FullPath, newPath);
            Sidebar.Refresh();
        });
        GetOwnerProcess().Application.OpenModal(dialog);
    }

    private void Duplicate() {
        string parent = Path.GetDirectoryName(FullPath);
        string name = Path.GetFileNameWithoutExtension(FullPath);
        string ext = Path.GetExtension(FullPath);
        string newPath = Path.Combine(parent, name + " (Copy)" + ext);
        
        int i = 1;
        while (VirtualFileSystem.Instance.Exists(newPath)) {
            newPath = Path.Combine(parent, $"{name} (Copy {i++}){ext}");
        }
        
        VirtualFileSystem.Instance.Copy(FullPath, newPath);
        Sidebar.Refresh();
    }

    private void AddReference(string libName) {
        string manifestPath = Path.Combine(FullPath, "manifest.json");
        if (!VirtualFileSystem.Instance.Exists(manifestPath)) return;
        
        try {
            string json = VirtualFileSystem.Instance.ReadAllText(manifestPath);
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            
            if (data != null) {
                List<string> refs = new List<string>();
                if (data.TryGetValue("references", out var rObj)) {
                    var existingRefs = System.Text.Json.JsonSerializer.Deserialize<List<string>>(rObj.ToString());
                    if (existingRefs != null) refs.AddRange(existingRefs);
                }
                
                if (!refs.Contains(libName)) {
                    refs.Add(libName);
                    data["references"] = refs;
                    string newJson = System.Text.Json.JsonSerializer.Serialize(data, options);
                    VirtualFileSystem.Instance.WriteAllText(manifestPath, newJson);
                    Shell.Notifications.Show("Project", $"Added reference to {libName}");
                }
            }
        } catch (Exception ex) {
            Shell.Notifications.Show("Error", "Failed to add reference: " + ex.Message);
        }
    }

    private string GetRelativePath() {
         return "." + FullPath.Substring(Sidebar.RootPath.Length).Replace('\\', '/');
    }

    private void ToggleExpand() {
        IsExpanded = !IsExpanded;
        if (IsExpanded && NodeChildren.Count == 0) {
            LoadChildren();
        }
        _expandBtn.Text = IsExpanded ? "▼" : "▶";
        Sidebar.UpdateLayout();
    }

    private void LoadChildren() {
        NodeChildren.Clear();
        var dirs = VirtualFileSystem.Instance.GetDirectories(FullPath).OrderBy(d => d);
        var files = VirtualFileSystem.Instance.GetFiles(FullPath)
            .Where(f => !f.EndsWith(".nproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f);

        foreach (var d in dirs) NodeChildren.Add(new Node(d, Depth + 1, Sidebar));
        foreach (var f in files) NodeChildren.Add(new Node(f, Depth + 1, Sidebar));
    }

    public float BuildDisplayList(List<Node> list) {
        list.Add(this);
        float h = Size.Y;
        if (IsExpanded) {
            foreach (var child in NodeChildren) {
                h += child.BuildDisplayList(list);
            }
        }
        return h;
    }
}