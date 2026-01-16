using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using TheGame;
using TheGame.Core;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Graphics;
using MessageBox = TheGame.Core.UI.MessageBox;

namespace FileExplorerApp;

public class AppSettings {
    public string LastPath { get; set; } = "C:\\";
}

public class FileExplorerWindow : Window {
    public static Window CreateWindow() {
        var settings = Shell.AppSettings.Load<AppSettings>();
        return new FileExplorerWindow(new Vector2(100, 100), new Vector2(850, 600), settings);
    }

    private string _currentPath = "C:\\";
    public string CurrentPath => _currentPath;
    private AppSettings _settings;
    private FileListPanel _fileList;
    private Panel _sidebar;
    private MenuBar _menuBar;
    private TextInput _pathInput;
    private const float SidebarWidth = 180f;
    private bool _isComputerMode = false;

    public FileExplorerWindow(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "File Explorer";
        AppId = "EXPLORER";
        _settings = settings;
        _currentPath = _settings.LastPath;

        OnResize += () => LayoutUI();

        SetupUI();
        NavigateTo(_currentPath);
    }

    private void SetupUI() {
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, 26f));
        _menuBar.AddMenu("File", m => {
            m.AddItem("New Window", () => Shell.UI.OpenWindow(CreateWindow()));
            m.AddSeparator();
            m.AddItem("Close", Close);
        });
        _menuBar.AddMenu("View", m => {
            m.AddItem("Refresh", RefreshList);
        });
        AddChild(_menuBar);

        // Go Up Button
        var goUpBtn = new Button(new Vector2(5, 31), new Vector2(30, 30), "^") {
            BackgroundColor = new Color(50, 50, 50),
            HoverColor = new Color(70, 70, 70)
        };
        goUpBtn.OnClickAction = GoUp;
        AddChild(goUpBtn);

        _pathInput = new TextInput(new Vector2(40, 31), new Vector2(ClientSize.X - 45, 30)) {
            Value = _currentPath
        };
        _pathInput.OnSubmit += (val) => NavigateTo(val);
        AddChild(_pathInput);

        // Sidebar
        _sidebar = new Panel(new Vector2(0, 66), new Vector2(SidebarWidth, ClientSize.Y - 66)) {
            BackgroundColor = new Color(30, 30, 30),
            BorderThickness = 0
        };
        AddChild(_sidebar);
        SetupSidebar();

        // File List
        _fileList = new FileListPanel(new Vector2(SidebarWidth, 66), new Vector2(ClientSize.X - SidebarWidth, ClientSize.Y - 66), this);
        AddChild(_fileList);
    }

    private void GoUp() {
        if (_isComputerMode || _currentPath == "COMPUTER") return;
        
        string parent = System.IO.Path.GetDirectoryName(_currentPath.TrimEnd('\\'));
        if (string.IsNullOrEmpty(parent) || parent.Length <= 2) {
            // At root of a drive, go to Computer view
            NavigateTo("COMPUTER");
        } else {
            NavigateTo(parent);
        }
    }

    private void SetupSidebar() {
        _sidebar.ClearChildren();
        float y = 10;
        
        AddSidebarItem("Computer", GameContent.PCIcon ?? Shell.GetIcon("C:\\"), "COMPUTER", ref y);
        
        var drives = VirtualFileSystem.Instance.GetDrives();
        foreach (var drive in drives) {
            AddSidebarItem(drive, GameContent.DiskIcon ?? Shell.GetIcon(drive), drive, ref y);
        }

        y += 10;
        AddSidebarItem("Recycle Bin", GameContent.TrashEmptyIcon, "C:\\$Recycle.Bin\\", ref y);
        
        y += 10;
        AddSidebarItem("Desktop", GameContent.DesktopIcon ?? GameContent.FolderIcon, "C:\\Users\\Admin\\Desktop\\", ref y);
        AddSidebarItem("Documents", GameContent.FolderIcon, "C:\\Users\\Admin\\Documents\\", ref y);
    }

    private void AddSidebarItem(string label, Texture2D icon, string path, ref float y) {
        var btn = new Button(new Vector2(5, y), new Vector2(SidebarWidth - 10, 30), label) {
            BackgroundColor = Color.Transparent,
            TextAlign = TextAlign.Left,
            Icon = icon
        };
        btn.OnClickAction = () => NavigateTo(path);
        _sidebar.AddChild(btn);
        y += 35;
    }

    private void LayoutUI() {
        _menuBar.Size = new Vector2(ClientSize.X, 26f);
        _pathInput.Size = new Vector2(ClientSize.X - 45, 30);
        _sidebar.Size = new Vector2(SidebarWidth, ClientSize.Y - 66);
        _fileList.Position = new Vector2(SidebarWidth, 66);
        _fileList.Size = new Vector2(ClientSize.X - SidebarWidth, ClientSize.Y - 66);
        _fileList.UpdateLayout();
    }

    public void NavigateTo(string path) {
        if (path == "COMPUTER") {
            _isComputerMode = true;
            _currentPath = "COMPUTER";
            _pathInput.Value = "My Computer";
            RefreshList();
            return;
        }

        if (VirtualFileSystem.Instance.IsDirectory(path)) {
            _isComputerMode = false;
            _currentPath = path;
            _pathInput.Value = path;
            _settings.LastPath = path;
            Shell.AppSettings.Save(_settings);
            RefreshList();
        }
    }

    public void RefreshList() {
        IEnumerable<string> items;
        if (_isComputerMode) {
            items = VirtualFileSystem.Instance.GetDrives();
        } else {
            var files = VirtualFileSystem.Instance.GetFiles(_currentPath);
            var dirs = VirtualFileSystem.Instance.GetDirectories(_currentPath).Where(d => !d.Contains("$Recycle.Bin"));
            items = dirs.Concat(files);
        }
        _fileList.SetItems(items.ToList(), _isComputerMode);
    }

    public void HandleDropData(object dropped, string targetPath) {
        if (targetPath == "COMPUTER") return;
        
        List<string> paths = new();
        if (dropped is string p) paths.Add(p);
        else if (dropped is List<string> ps) paths.AddRange(ps);
        else if (dropped is DesktopIcon d && !string.IsNullOrEmpty(d.VirtualPath)) paths.Add(d.VirtualPath);

        bool changed = false;
        foreach (var src in paths) {
            string name = System.IO.Path.GetFileName(src.TrimEnd('\\'));
            string dest = System.IO.Path.Combine(targetPath, name);
            if (src.ToUpper().TrimEnd('\\') == dest.ToUpper().TrimEnd('\\')) continue;
            
            VirtualFileSystem.Instance.Move(src, dest);
            changed = true;
        }

        if (changed) {
            RefreshList();
            Shell.RefreshDesktop?.Invoke();
            Shell.RefreshExplorers();
        }
    }
}

// Custom panel that handles file items, selection, and drag/drop internally
public class FileListPanel : ScrollPanel {
    private FileExplorerWindow _window;
    private List<FileListItem> _items = new();
    private HashSet<string> _selectedPaths = new();

    // Marquee
    private bool _isSelecting;
    private Vector2 _selectionStart;
    private Rectangle _marqueeRect;

    // Drag
    private bool _isDragging;
    private Vector2 _dragStartPos;
    private string _dragSourcePath;
    private FileListItem _dropTargetItem;

    public FileListPanel(Vector2 pos, Vector2 size, FileExplorerWindow window) : base(pos, size) {
        _window = window;
        BackgroundColor = Color.Transparent;
    }

    public void SetItems(List<string> paths, bool isComputerMode) {
        ClearChildren();
        _items.Clear();
        _selectedPaths.Clear();

        foreach (var path in paths) {
            bool isDir = isComputerMode || VirtualFileSystem.Instance.IsDirectory(path);
            var item = new FileListItem(path, isDir);
            _items.Add(item);
            AddChild(item);
        }
        UpdateLayout();
    }

    public void UpdateLayout() {
        float y = 5;
        float w = ClientSize.X - 10;
        foreach (var item in _items) {
            item.Position = new Vector2(5, y);
            item.Size = new Vector2(w, 28);
            y += 32;
        }
        UpdateContentHeight(y + 20);
    }

    protected override void UpdateInput() {
        // Don't call base.UpdateInput() here - we handle everything manually
        if (!IsVisible) return;

        Vector2 mouse = InputManager.MousePosition.ToVector2();
        bool inBounds = Bounds.Contains(mouse.ToPoint()) && !InputManager.IsMouseConsumed;

        // Find hovered item
        FileListItem hoveredItem = null;
        foreach (var item in _items) {
            item.IsHovered = item.Bounds.Contains(mouse.ToPoint());
            if (item.IsHovered) hoveredItem = item;
        }

        // Update drop target highlight during drag
        if (Shell.IsDragging && hoveredItem != null && hoveredItem.IsDir) {
            _dropTargetItem = hoveredItem;
        } else {
            _dropTargetItem = null;
        }
        UpdateDropTargetVisuals();

        // Handle active drag release
        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            if (Shell.IsDragging && inBounds) {
                var dropped = Shell.DraggedItem;
                string targetPath = hoveredItem != null && hoveredItem.IsDir ? hoveredItem.Path : _window.CurrentPath;
                _window.HandleDropData(dropped, targetPath);
                Shell.EndDrag();
            }
            _isDragging = false;
            _isSelecting = false;
            _marqueeRect = Rectangle.Empty;
            _dropTargetItem = null;
            UpdateDropTargetVisuals();
        }

        // Double-click - check BEFORE consuming input
        bool isDoubleClick = inBounds && hoveredItem != null && InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true);
        if (isDoubleClick) {
            if (hoveredItem.IsDir) {
                // Check if it's a .sapp app - execute it instead of navigating
                if (hoveredItem.Path.ToLower().EndsWith(".sapp")) {
                    Shell.Execute(hoveredItem.Path, hoveredItem.Bounds);
                } else {
                    _window.NavigateTo(hoveredItem.Path);
                }
            } else {
                Shell.Execute(hoveredItem.Path, hoveredItem.Bounds);
            }
            InputManager.IsMouseConsumed = true;
            return; // Don't process single click logic on double-click
        }

        // Right-click context menu
        if (inBounds && hoveredItem != null && InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
            if (!_selectedPaths.Contains(hoveredItem.Path)) {
                _selectedPaths.Clear();
                _selectedPaths.Add(hoveredItem.Path);
                UpdateSelectionVisuals();
            }
            ShowContextMenu(hoveredItem);
            InputManager.IsMouseConsumed = true;
            return;
        }

        // Handle clicks
        if (inBounds && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            bool ctrl = Keyboard.GetState().IsKeyDown(Keys.LeftControl);

            if (hoveredItem != null) {
                // Clicked on an item
                if (ctrl) {
                    if (_selectedPaths.Contains(hoveredItem.Path)) _selectedPaths.Remove(hoveredItem.Path);
                    else _selectedPaths.Add(hoveredItem.Path);
                } else {
                    if (!_selectedPaths.Contains(hoveredItem.Path)) {
                        _selectedPaths.Clear();
                        _selectedPaths.Add(hoveredItem.Path);
                    }
                }
                _dragStartPos = mouse;
                _dragSourcePath = hoveredItem.Path;
                _isDragging = true;
            } else {
                // Clicked on empty space - start marquee
                if (!ctrl) _selectedPaths.Clear();
                _isSelecting = true;
                _selectionStart = mouse;
                _marqueeRect = Rectangle.Empty;
            }
            UpdateSelectionVisuals();
            InputManager.IsMouseConsumed = true;
        }

        // Handle dragging to start Shell drag
        if (_isDragging && InputManager.IsMouseButtonDown(MouseButton.Left) && !Shell.IsDragging) {
            if (Vector2.Distance(_dragStartPos, mouse) > 6) {
                var paths = _selectedPaths.Count > 0 ? _selectedPaths.ToList() : new List<string> { _dragSourcePath };
                if (paths.Count > 1) Shell.BeginDrag(paths, _dragStartPos);
                else Shell.BeginDrag(paths[0], _dragStartPos);
                _isDragging = false;
            }
        }

        // Handle marquee selection
        if (_isSelecting && InputManager.IsMouseButtonDown(MouseButton.Left)) {
            float x = Math.Min(_selectionStart.X, mouse.X);
            float y = Math.Min(_selectionStart.Y, mouse.Y);
            float w = Math.Abs(_selectionStart.X - mouse.X);
            float h = Math.Abs(_selectionStart.Y - mouse.Y);
            var rawRect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            _marqueeRect = Rectangle.Intersect(rawRect, Bounds);

            bool ctrl = Keyboard.GetState().IsKeyDown(Keys.LeftControl);
            foreach (var item in _items) {
                bool inRect = _marqueeRect.Intersects(item.Bounds);
                if (inRect) _selectedPaths.Add(item.Path);
                else if (!ctrl) _selectedPaths.Remove(item.Path);
            }
            UpdateSelectionVisuals();
            InputManager.IsMouseConsumed = true;
        }

        // Scroll input - only if in bounds and not consumed
        if (inBounds && HasScrollableContent()) {
            float scrollDelta = InputManager.ScrollDelta;
            if (scrollDelta != 0) {
                TargetScrollY += (scrollDelta / 120f) * 60f;
                ClampScroll();
                InputManager.IsMouseConsumed = true;
            }
        }
    }

    private void UpdateSelectionVisuals() {
        foreach (var item in _items) {
            item.IsSelected = _selectedPaths.Contains(item.Path);
        }
    }

    private void UpdateDropTargetVisuals() {
        foreach (var item in _items) {
            item.IsDropTarget = (item == _dropTargetItem);
        }
    }

    private void ShowContextMenu(FileListItem item) {
        var menuItems = new List<MenuItem>();
        bool isSapp = item.IsDir && item.Path.ToLower().EndsWith(".sapp");
        bool isRecycleBin = item.Path.ToUpper().Contains("$RECYCLE.BIN");

        if (isSapp) {
            // .sapp app context menu
            menuItems.Add(new MenuItem { Text = "Run", Action = () => Shell.Execute(item.Path, item.Bounds) });
            menuItems.Add(new MenuItem { Text = "Open as Folder", Action = () => _window.NavigateTo(item.Path) });
            menuItems.Add(new MenuItem { Text = "Delete", Action = () => DeleteItems() });
            menuItems.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path) });
        } else if (isRecycleBin) {
            // Recycle Bin context menu
            menuItems.Add(new MenuItem { Text = "Open", Action = () => _window.NavigateTo(item.Path) });
            menuItems.Add(new MenuItem { 
                Text = "Empty Recycle Bin", 
                Action = () => {
                    var mb = new MessageBox("Empty Recycle Bin", 
                        "Are you sure you want to permanently delete all items in the Recycle Bin?", 
                        MessageBoxButtons.YesNo, (confirmed) => {
                        if (confirmed) {
                            VirtualFileSystem.Instance.EmptyRecycleBin();
                            Shell.Audio.PlaySound("C:\\Windows\\Media\\trash_empty.wav");
                            _window.RefreshList();
                            Shell.RefreshDesktop?.Invoke();
                            Shell.RefreshExplorers();
                        }
                    });
                    Shell.UI.OpenWindow(mb);
                }
            });
            menuItems.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path) });
        } else if (item.IsDir) {
            // Regular folder context menu
            menuItems.Add(new MenuItem { Text = "Open", Action = () => _window.NavigateTo(item.Path) });
            menuItems.Add(new MenuItem { Text = "Delete", Action = () => DeleteItems() });
            menuItems.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path) });
        } else {
            // File context menu
            menuItems.Add(new MenuItem { Text = "Open", Action = () => Shell.Execute(item.Path, item.Bounds) });
            menuItems.Add(new MenuItem { Text = "Delete", Action = () => DeleteItems() });
            menuItems.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path) });
        }

        Shell.GlobalContextMenu?.Show(InputManager.MousePosition.ToVector2(), menuItems);
    }

    private void DeleteItems() {
        var itemsToDelete = _selectedPaths.ToList();
        if (itemsToDelete.Count == 0) return;

        string message = itemsToDelete.Count == 1 
            ? $"Are you sure you want to move '{System.IO.Path.GetFileName(itemsToDelete[0])}' to the Recycle Bin?"
            : $"Are you sure you want to move {itemsToDelete.Count} items to the Recycle Bin?";

        var mb = new MessageBox("Delete", message, MessageBoxButtons.YesNo, (confirmed) => {
            if (confirmed) {
                foreach (var path in itemsToDelete) {
                    VirtualFileSystem.Instance.Recycle(path);
                }
                _selectedPaths.Clear();
                _window.RefreshList();
                Shell.RefreshDesktop?.Invoke();
                Shell.RefreshExplorers();
            }
        });
        Shell.UI.OpenWindow(mb);
    }

    private void ShowProperties(string path) {
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        bool isDir = VirtualFileSystem.Instance.IsDirectory(path);
        string type = isDir ? "Folder" : "File";
        string fullPath = path;
        
        string info = $"Name: {name}\nType: {type}\nLocation: {fullPath}";
        Shell.Notifications.Show("Properties", info);
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        base.Draw(spriteBatch, shapeBatch);

        // Draw marquee on top
        if (_isSelecting && _marqueeRect.Width > 0 && _marqueeRect.Height > 0) {
            shapeBatch.FillRectangle(_marqueeRect.Location.ToVector2(), _marqueeRect.Size.ToVector2(), new Color(0, 120, 215, 40));
            shapeBatch.BorderRectangle(_marqueeRect.Location.ToVector2(), _marqueeRect.Size.ToVector2(), new Color(0, 120, 215, 150), 1f);
        }
    }
}

public class FileListItem : UIElement {
    public string Path { get; }
    public bool IsDir { get; }
    public bool IsSelected { get; set; }
    public bool IsHovered { get; set; }
    public bool IsDropTarget { get; set; }
    
    private readonly Texture2D _cachedIcon;
    private readonly string _displayName;

    public FileListItem(string path, bool isDir) : base(Vector2.Zero, Vector2.Zero) {
        Path = path;
        IsDir = isDir;
        ConsumesInput = false; // Parent handles input
        
        // Cache icon and name at creation time - NOT in Draw()
        _cachedIcon = Shell.GetIcon(path);
        _displayName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        if (string.IsNullOrEmpty(_displayName)) _displayName = path;
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsVisible) return;

        var bgColor = Color.Transparent;
        if (IsSelected) bgColor = new Color(0, 120, 215, 80);
        else if (IsHovered) bgColor = new Color(0, 0, 0, 50);

        if (bgColor != Color.Transparent) {
            batch.FillRectangle(AbsolutePosition, Size, bgColor, rounded: 3);
        }

        // Draw drop target highlight
        if (IsDropTarget && IsDir) {
            batch.BorderRectangle(AbsolutePosition, Size, new Color(0, 120, 215, 200), thickness: 2f, rounded: 3);
            batch.FillRectangle(AbsolutePosition, Size, new Color(0, 120, 215, 30), rounded: 3);
        }

        if (_cachedIcon != null) {
            float iconSize = 20f;
            float scale = iconSize / _cachedIcon.Width;
            batch.DrawTexture(_cachedIcon, AbsolutePosition + new Vector2(4, 4), Color.White * AbsoluteOpacity, scale);
        }

        var font = GameContent.FontSystem.GetFont(16);
        font.DrawText(batch, _displayName, AbsolutePosition + new Vector2(30, 5), Color.White * AbsoluteOpacity);
    }
}
