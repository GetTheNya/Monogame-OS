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

namespace NACHOS;

public class Node : Panel {
    public string FullPath;
    public bool IsDirectory;
    public bool IsExpanded;
    public List<Node> NodeChildren = new();
    public Sidebar Sidebar;
    public int Depth;

    private Button _expandBtn;
    private Texture2D _iconTex;
    private Label _label;

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