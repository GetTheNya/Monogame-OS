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

public class Sidebar : ScrollPanel {
    private string _rootPath;
    private List<Node> _rootNodes = new();
    public Action<string> OnFileSelected;

    private class Node : Panel {
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

            _iconTex = IsDirectory ? GameContent.FolderIcon : GameContent.FileIcon;

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
             items.Add(new MenuItem { Text = "Rename", Action = () => { /* TODO */ } });
             items.Add(new MenuItem { Text = "Delete", Action = () => {
                 var mb = new MessageBox("Delete", $"Are you sure you want to delete {Path.GetFileName(FullPath)}?", MessageBoxButtons.YesNo, (ok) => {
                     if (ok) {
                         VirtualFileSystem.Instance.Delete(FullPath);
                         Sidebar.Refresh();
                     }
                 });
                 Shell.UI.OpenWindow(mb);
             } });
             if (IsDirectory) {
                 items.Add(new MenuItem { Type = MenuItemType.Separator });
                 items.Add(new MenuItem { Text = "New File", Action = () => { /* TODO: Prompt for name */ } });
             }
             base.PopulateContextMenu(context, items);
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
            var files = VirtualFileSystem.Instance.GetFiles(FullPath).OrderBy(f => f);

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
        
        var dirs = VirtualFileSystem.Instance.GetDirectories(_rootPath).OrderBy(d => d);
        var files = VirtualFileSystem.Instance.GetFiles(_rootPath).OrderBy(f => f);

        foreach (var d in dirs) _rootNodes.Add(new Node(d, 0, this));
        foreach (var f in files) _rootNodes.Add(new Node(f, 0, this));
        
        UpdateLayout();
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
}
