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
using TheGame.Core.OS.DragDrop;
using TheGame.Core.Input;
using TheGame.Graphics;
using MessageBox = TheGame.Core.UI.MessageBox;

namespace FileExplorerApp;

public class AppSettings {
    public string LastPath { get; set; } = "C:\\";
}

public class ExplorerApp : Application {
    public static ExplorerApp Main(string[] args) {
        return new ExplorerApp();
    }

    protected override void OnLoad(string[] args) {
        var win = new FileExplorerWindow(new Vector2(100, 100), new Vector2(850, 600));
        MainWindow = win;
        
        if (args != null && args.Length > 0) {
            win.InitialPath = args[0];
        }
    }
}

public class FileExplorerWindow : Window {
    public string InitialPath { get; set; }

    private string _currentPath = "C:\\";
    public string CurrentPath => _currentPath;
    private AppSettings _settings;
    private FileListPanel _fileList;
    private ScrollPanel _sidebar;
    private MenuBar _menuBar;
    private TextInput _pathInput;
    private const float SidebarWidth = 180f;
    private bool _isComputerMode = false;

    public FileExplorerWindow(Vector2 pos, Vector2 size) : base(pos, size) {
        Title = "File Explorer";
        AppId = "EXPLORER";

        OnResize += () => LayoutUI();
        OnClosed += () => {
            if (!string.IsNullOrEmpty(_currentPath)) {
                VirtualFileSystem.Instance.UnwatchDirectory(_currentPath, OnFileSystemChanged);
            }
        };
    }
    
    protected override void OnLoad() {
        _settings = Shell.AppSettings.Load<AppSettings>(OwnerProcess);
        SetupUI();
        
        // Use InitialPath if provided, otherwise fallback to settings
        if (!string.IsNullOrEmpty(InitialPath)) {
            _currentPath = InitialPath;
        } else if (string.IsNullOrEmpty(_currentPath) || _currentPath == "C:\\") {
            if (!string.IsNullOrEmpty(_settings.LastPath)) _currentPath = _settings.LastPath;
        }
        
        NavigateTo(_currentPath);
    }

    private void SetupUI() {
        _menuBar = new MenuBar(Vector2.Zero, new Vector2(ClientSize.X, 26f));
        _menuBar.AddMenu("File", m => {
            m.AddItem("New Window", () => {
                var win = CreateWindow();
                Shell.UI.OpenWindow(win, owner: this.OwnerProcess);
            });
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
        _sidebar = new ScrollPanel(new Vector2(0, 66), new Vector2(SidebarWidth, ClientSize.Y - 66)) {
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
        AddSidebarItem("Desktop", GameContent.DesktopIcon ?? GameContent.FolderIcon, $@"C:\Users\{SystemConfig.Username}\Desktop\", ref y);
        AddSidebarItem("Documents", GameContent.FolderIcon, $@"C:\Users\{SystemConfig.Username}\Documents\", ref y);
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

    private Window CreateWindow() {
        return new FileExplorerWindow(new Vector2(Position.X + 20, Position.Y + 20), Size);
    }

    public void NavigateTo(string path) {
        if (!string.IsNullOrEmpty(_currentPath)) {
            VirtualFileSystem.Instance.UnwatchDirectory(_currentPath, OnFileSystemChanged);
        }

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
            Shell.AppSettings.Save(OwnerProcess, _settings);

            VirtualFileSystem.Instance.WatchDirectory(_currentPath, OnFileSystemChanged);

            RefreshList();
        }
    }

    public void RefreshList() {
        IEnumerable<string> items;
        if (_isComputerMode) {
            items = VirtualFileSystem.Instance.GetDrives();
        } else {
            bool inRecycleBin = _currentPath.ToUpper().Contains("$RECYCLE.BIN");
            var files = VirtualFileSystem.Instance.GetFiles(_currentPath).Where(f => !f.EndsWith("$trash_info.json", StringComparison.OrdinalIgnoreCase));
            var dirs = VirtualFileSystem.Instance.GetDirectories(_currentPath);
            
            // If not already in the recycle bin, hide the recycle bin system folder itself
            if (!inRecycleBin) {
                dirs = dirs.Where(d => !d.ToUpper().Contains("$RECYCLE.BIN")).ToArray();
            }

            items = dirs.Concat(files);
        }
        _fileList.SetItems(items.ToList(), _isComputerMode);
    }

    public void HandleDropData(object dropped, string targetPath) {
        if (targetPath == "COMPUTER") return;
        
        // Extract actual data if it's IDraggable (like from Browser)
        object dragData = dropped;
        if (dropped is IDraggable draggable) {
            dragData = draggable.GetDragData();
        }

        if (dragData == null) return;
        
        List<string> paths = new();
        if (dragData is string p) paths.Add(p);
        else if (dragData is List<string> ps) paths.AddRange(ps);
        else if (dragData is DesktopIcon d && !string.IsNullOrEmpty(d.VirtualPath)) paths.Add(d.VirtualPath);

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

    private void OnFileSystemChanged(FileSystemEventArgs e) {
        RefreshList();
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
    
    // Rename functionality
    private TextInput _renameInput;
    private string _renamingPath;
    private bool _isRenaming = false;

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
    
    public void StartRename(string path) {
        if (_isRenaming || string.IsNullOrEmpty(path)) return;
        
        _isRenaming = true;
        _renamingPath = path;
        
        string currentName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        bool isFile = !VirtualFileSystem.Instance.IsDirectory(path);
        
        // For files, remove extension
        string editName = isFile ? System.IO.Path.GetFileNameWithoutExtension(currentName) : currentName;
        
        // Find item position
        var item = _items.FirstOrDefault(i => i.Path == path);
        if (item == null) {
            _isRenaming = false;
            return;
        }
        
        // Create text input positioned over filename (x=30 for icon offset)
        var absPos = AbsolutePosition;
        _renameInput = new TextInput(
            new Vector2(absPos.X + item.Position.X + 30, absPos.Y + item.Position.Y + 4),
            new Vector2(item.Size.X - 35, 20)
        ) {
            Value = editName,
            BackgroundColor = Color.White,
            TextColor = Color.Black
        };
        _renameInput.OnSubmit += CompleteRename;
        _renameInput.IsFocused = true;
    }
    
    private void CompleteRename(string newName) {
        if (!_isRenaming) return;
        
        try {
            if (string.IsNullOrWhiteSpace(newName)) {
                CancelRename();
                return;
            }
            
            char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
            if (newName.Any(c => invalidChars.Contains(c))) {
                Shell.Notifications.Show("Invalid Name", "Filename contains invalid characters.");
                CancelRename();
                return;
            }
            
            string directory = System.IO.Path.GetDirectoryName(_renamingPath.TrimEnd('\\'));
            bool isFile = !VirtualFileSystem.Instance.IsDirectory(_renamingPath);
            
            // For files, preserve extension
            if (isFile) {
                string extension = System.IO.Path.GetExtension(_renamingPath);
                if (!newName.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
                    newName += extension;
                }
            }
            
            string newPath = System.IO.Path.Combine(directory, newName);
            
            // Check if already exists
            if (VirtualFileSystem.Instance.Exists(newPath) && 
                !newPath.Equals(_renamingPath, StringComparison.OrdinalIgnoreCase)) {
                Shell.Notifications.Show("Name Conflict", "A file or folder with that name already exists.");
                CancelRename();
                return;
            }
            
            // Don't rename if name didn't change
            if (newPath.Equals(_renamingPath, StringComparison.OrdinalIgnoreCase)) {
                CancelRename();
                return;
            }
            
            // Perform rename
            VirtualFileSystem.Instance.Move(_renamingPath, newPath);
            
            // Refresh and update selection
            _selectedPaths.Remove(_renamingPath);
            _selectedPaths.Add(newPath);
            _window.RefreshList();
            Shell.RefreshDesktop?.Invoke();
            Shell.RefreshExplorers();
            
        } catch (Exception ex) {
            Shell.Notifications.Show("Rename Error", ex.Message);
        } finally {
            _isRenaming = false;
            _renameInput = null;
            _renamingPath = null;
        }
    }
    
    private void CancelRename() {
        _isRenaming = false;
        _renameInput = null;
        _renamingPath = null;
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        
        // Update rename input
        if (_isRenaming && _renameInput != null) {
            var item = _items.FirstOrDefault(i => i.Path == _renamingPath);
            if (item != null) {
                var absPos = AbsolutePosition;
                _renameInput.Position = new Vector2(absPos.X + item.Position.X + 30, absPos.Y + item.Position.Y + 4);
                _renameInput.Update(gameTime);
            }
            
            // Cancel on Escape or click outside
            if (InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Escape)) {
                CancelRename();
            } else if (InputManager.IsMouseButtonJustPressed(MouseButton.Left) && 
                       _renameInput != null && !_renameInput.Bounds.Contains(InputManager.MousePosition)) {
                CompleteRename(_renameInput.Value);
            }
        }
    }
    
    protected override void UpdateInput() {
        bool alreadyConsumed = InputManager.IsMouseConsumed;

        // Call base to handle scrollbars, mouse wheel, and centralized context menu trigger
        base.UpdateInput();

        if (!IsVisible || _isRenaming) return;

        Vector2 mouse = InputManager.MousePosition.ToVector2();
        bool inBounds = Bounds.Contains(mouse.ToPoint());

        // Find hovered item
        FileListItem hoveredItem = null;
        foreach (var item in _items) {
            item.IsHovered = item.Bounds.Contains(mouse.ToPoint());
            if (item.IsHovered) hoveredItem = item;
        }

        // Update drop target highlight during drag
        if (Shell.Drag.IsActive && hoveredItem != null && hoveredItem.IsDir) {
            _dropTargetItem = hoveredItem;
        } else {
            _dropTargetItem = null;
        }
        UpdateDropTargetVisuals();

        if (alreadyConsumed && !IsMouseOver) return;

        // Handle active drag release
        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            if (Shell.Drag.IsActive && inBounds) {
                var dropped = Shell.Drag.DraggedItem;
                string targetPath = hoveredItem != null && hoveredItem.IsDir ? hoveredItem.Path : _window.CurrentPath;
                _window.HandleDropData(dropped, targetPath);
                Shell.Drag.End();
            }
            _isDragging = false;
            _isSelecting = false;
            _marqueeRect = Rectangle.Empty;
            _dropTargetItem = null;
            UpdateDropTargetVisuals();
        }

        // Double-click
        bool isDoubleClick = inBounds && hoveredItem != null && InputManager.IsDoubleClick(MouseButton.Left, ignoreConsumed: true);
        if (isDoubleClick) {
            if (_window.CurrentPath.ToUpper().Contains("$RECYCLE.BIN")) {
                InputManager.IsMouseConsumed = true;
                return;
            }

            if (hoveredItem.IsDir) {
                if (hoveredItem.Path.ToLower().EndsWith(".sapp")) {
                    Shell.Execute(hoveredItem.Path, hoveredItem.Bounds);
                } else {
                    _window.NavigateTo(hoveredItem.Path);
                }
            } else {
                Shell.Execute(hoveredItem.Path, hoveredItem.Bounds);
            }
            InputManager.IsMouseConsumed = true;
            return;
        }

        // Handle clicks
        if (inBounds && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            bool ctrl = Keyboard.GetState().IsKeyDown(Keys.LeftControl);

            if (hoveredItem != null) {
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
                if (!ctrl) _selectedPaths.Clear();
                _isSelecting = true;
                _selectionStart = mouse;
                _marqueeRect = Rectangle.Empty;
            }
            UpdateSelectionVisuals();
            InputManager.IsMouseConsumed = true;
        }

        // Handle dragging to start Shell drag
        if (_isDragging && InputManager.IsMouseButtonDown(MouseButton.Left) && !Shell.Drag.IsActive) {
            if (Vector2.Distance(_dragStartPos, mouse) > 6) {
                var paths = _selectedPaths.Count > 0 ? _selectedPaths.ToList() : new List<string> { _dragSourcePath };
                Vector2 grabOffset = new Vector2(24, 24);

                if (paths.Count > 1) Shell.Drag.Begin(paths, _dragStartPos, grabOffset);
                else Shell.Drag.Begin(paths[0], _dragStartPos, grabOffset);
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

        // Right Click Selection
        if (inBounds && InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
            bool ctrl = Keyboard.GetState().IsKeyDown(Keys.LeftControl);
            if (hoveredItem != null) {
                if (!ctrl && !_selectedPaths.Contains(hoveredItem.Path)) {
                    _selectedPaths.Clear();
                    _selectedPaths.Add(hoveredItem.Path);
                } else if (ctrl) {
                    _selectedPaths.Add(hoveredItem.Path);
                }
                UpdateSelectionVisuals();
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

    public override void PopulateContextMenu(ContextMenuContext context, List<MenuItem> items) {
        // Find item under mouse
        FileListItem item = null;
        foreach (var i in _items) {
            if (i.Bounds.Contains(context.Position.ToPoint())) {
                item = i;
                break;
            }
        }

        if (item == null) {
            // Background context menu
            items.Add(new MenuItem { Text = "Refresh", Action = () => _window.RefreshList(), Priority = 100 });
            items.Add(new MenuItem { Type = MenuItemType.Separator, Priority = 90 });
            items.Add(new MenuItem { 
                Text = "New", 
                Priority = 80,
                SubItems = new List<MenuItem> {
                    new MenuItem { Text = "Folder", Action = () => CreateNewItem("New Folder", true) },
                    new MenuItem { Text = "Text Document", Action = () => CreateNewItem("New Text Document.txt", false) }
                }
            });
            return;
        }

        // If item clicked is not selected, select it exclusively
        if (!_selectedPaths.Contains(item.Path)) {
            _selectedPaths.Clear();
            _selectedPaths.Add(item.Path);
            UpdateSelectionVisuals();
        }

        bool isSapp = item.IsDir && item.Path.ToLower().EndsWith(".sapp");
        bool isRecycleBinRoot = item.Path.ToUpper().TrimEnd('\\') == "C:\\$RECYCLE.BIN";
        bool inRecycleBin = _window.CurrentPath.ToUpper().Contains("$RECYCLE.BIN");

        if (inRecycleBin) {
            items.Add(new MenuItem { 
                Text = _selectedPaths.Count > 1 ? $"Restore ({_selectedPaths.Count} items)" : "Restore", 
                Action = RestoreSelectedItems,
                Priority = 100
            });
            items.Add(new MenuItem { 
                Text = _selectedPaths.Count > 1 ? $"Delete Permanently ({_selectedPaths.Count} items)" : "Delete Permanently", 
                Action = DeletePermanentlySelectedItems,
                Priority = 90
            });
            items.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path), Priority = 80 });
        } else if (isSapp) {
            items.Add(new MenuItem { Text = "Run", Action = () => Shell.Execute(item.Path, item.Bounds), Priority = 100 });
            items.Add(new MenuItem { Text = "Open as Folder", Action = () => _window.NavigateTo(item.Path), Priority = 90 });
            if (_selectedPaths.Count == 1) {
                items.Add(new MenuItem { Text = "Rename", Action = () => RenameItem(item.Path), Priority = 80 });
            }
            items.Add(new MenuItem { 
                Text = "Send to", 
                Priority = 70,
                SubItems = new List<MenuItem> {
                    new MenuItem { Text = "Desktop (create shortcut)", Action = SendToDesktopShortcut },
                    new MenuItem { Text = "Start menu (create shortcut)", Action = SendToStartMenuShortcut }
                }
            });
            items.Add(new MenuItem { Text = "Delete", Action = () => DeleteItems(), Priority = 60 });
            items.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path), Priority = 50 });
        } else if (isRecycleBinRoot) {
            items.Add(new MenuItem { Text = "Open", Action = () => _window.NavigateTo(item.Path), Priority = 100 });
            items.Add(new MenuItem { 
                Text = "Empty Recycle Bin", 
                Action = () => Shell.PromptEmptyRecycleBin(),
                Priority = 90
            });
            items.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path), Priority = 80 });
        } else if (item.IsDir) {
            items.Add(new MenuItem { Text = "Open", Action = () => _window.NavigateTo(item.Path), Priority = 100 });
            if (_selectedPaths.Count == 1) {
                items.Add(new MenuItem { Text = "Rename", Action = () => RenameItem(item.Path), Priority = 90 });
            }
            items.Add(new MenuItem { 
                Text = "Send to", 
                Priority = 80,
                SubItems = new List<MenuItem> {
                    new MenuItem { Text = "Desktop (create shortcut)", Action = SendToDesktopShortcut },
                    new MenuItem { Text = "Start menu (create shortcut)", Action = SendToStartMenuShortcut }
                }
            });
            items.Add(new MenuItem { Text = "Delete", Action = () => DeleteItems(), Priority = 70 });
            items.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path), Priority = 60 });
        } else {
            items.Add(new MenuItem { Text = "Open", Action = () => Shell.Execute(item.Path, item.Bounds), Priority = 100 });
            if (_selectedPaths.Count == 1) {
                items.Add(new MenuItem { Text = "Rename", Action = () => RenameItem(item.Path), Priority = 90 });
            }
            items.Add(new MenuItem { 
                Text = "Send to", 
                Priority = 80,
                SubItems = new List<MenuItem> {
                    new MenuItem { Text = "Desktop (create shortcut)", Action = SendToDesktopShortcut },
                    new MenuItem { Text = "Start menu (create shortcut)", Action = SendToStartMenuShortcut }
                }
            });
            items.Add(new MenuItem { Text = "Delete", Action = () => DeleteItems(), Priority = 70 });
            items.Add(new MenuItem { Text = "Properties", Action = () => ShowProperties(item.Path), Priority = 60 });
        }
    }

    private void CreateNewItem(string defaultName, bool isDir) {
        try {
            string path = System.IO.Path.Combine(_window.CurrentPath, defaultName);
            int i = 1;
            string baseName = System.IO.Path.GetFileNameWithoutExtension(defaultName);
            string ext = System.IO.Path.GetExtension(defaultName);
            
            while (VirtualFileSystem.Instance.Exists(path)) {
                path = System.IO.Path.Combine(_window.CurrentPath, $"{baseName} ({i++}){ext}");
            }

            if (isDir) VirtualFileSystem.Instance.CreateDirectory(path);
            else VirtualFileSystem.Instance.WriteAllText(path, "");

            _window.RefreshList();
            Shell.RefreshDesktop?.Invoke();
            Shell.RefreshExplorers();
            
            // Start renaming the new item
            StartRename(path);
        } catch (Exception ex) {
            Shell.Notifications.Show("Error", $"Could not create item: {ex.Message}");
        }
    }

    [Obsolete("Use PopulateContextMenu instead")]
    private void ShowContextMenu(FileListItem item) {
    }

    private void RestoreSelectedItems() {
        var itemsToRestore = _selectedPaths.ToList();
        if (itemsToRestore.Count == 0) return;

        foreach (var path in itemsToRestore) {
            VirtualFileSystem.Instance.Restore(path);
        }
        
        _selectedPaths.Clear();
        _window.RefreshList();
        Shell.RefreshDesktop?.Invoke();
        Shell.RefreshExplorers();
    }

    private void DeletePermanentlySelectedItems() {
        var itemsToDelete = _selectedPaths.ToList();
        if (itemsToDelete.Count == 0) return;

        string message = itemsToDelete.Count == 1 
            ? $"Are you sure you want to permanently delete '{System.IO.Path.GetFileName(itemsToDelete[0].TrimEnd('\\'))}'?"
            : $"Are you sure you want to permanently delete these {itemsToDelete.Count} items?";

        var mb = new MessageBox("Delete Permanently", message, MessageBoxButtons.YesNo, (confirmed) => {
            if (confirmed) {
                foreach (var path in itemsToDelete) {
                    VirtualFileSystem.Instance.Delete(path);
                }
                _selectedPaths.Clear();
                _window.RefreshList();
                Shell.RefreshDesktop?.Invoke();
                Shell.RefreshExplorers();
            }
        });
        Shell.UI.OpenWindow(mb, owner: _window.OwnerProcess);
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
        Shell.UI.OpenWindow(mb, owner: _window.OwnerProcess);
    }

    private void ShowProperties(string path) {
        string name = System.IO.Path.GetFileName(path.TrimEnd('\\'));
        bool isDir = VirtualFileSystem.Instance.IsDirectory(path);
        string type = isDir ? "Folder" : "File";
        string fullPath = path;
        
        string info = $"Name: {name}\nType: {type}\nLocation: {fullPath}";
        Shell.Notifications.Show("Properties", info);
    }
    
    private void RenameItem(string itemPath) {
        StartRename(itemPath);
    }

    private void SendToDesktopShortcut() {
        var items = _selectedPaths.ToList();
        if (items.Count == 0) return;

        Shell.Desktop.CreateShortcuts(items);
        _selectedPaths.Clear();
    }

    private void SendToStartMenuShortcut() {
        var items = _selectedPaths.ToList();
        if (items.Count == 0) return;

        Shell.StartMenu.CreateShortcuts(items);
        _selectedPaths.Clear();
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
        base.Draw(spriteBatch, shapeBatch);

        // Draw marquee on top
        // _marqueeRect is in screen coordinates, but during RT rendering AbsolutePosition is offset
        // So we need to draw the marquee at its correct position relative to current AbsolutePosition
        if (_isSelecting && _marqueeRect.Width > 0 && _marqueeRect.Height > 0) {
            // The marquee rect was calculated in screen-space. During RT rendering,
            // AbsolutePosition includes RenderOffset. We draw at the marquee location directly
            // since the draw coordinates are also offset by RenderOffset.
            shapeBatch.FillRectangle(_marqueeRect.Location.ToVector2() - UIElement.RenderOffset, 
                                     _marqueeRect.Size.ToVector2(), 
                                     new Color(0, 120, 215, 40));
            shapeBatch.BorderRectangle(_marqueeRect.Location.ToVector2() - UIElement.RenderOffset, 
                                       _marqueeRect.Size.ToVector2(), 
                                       new Color(0, 120, 215, 150), 1f);
        }
        
        // Draw rename input on top
        if (_isRenaming && _renameInput != null) {
            _renameInput.Draw(spriteBatch, shapeBatch);
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
            float scale = Math.Min(iconSize / _cachedIcon.Width, iconSize / _cachedIcon.Height);
            float drawW = _cachedIcon.Width * scale;
            float drawH = _cachedIcon.Height * scale;
            Vector2 iconPos = AbsolutePosition + new Vector2(4 + (iconSize - drawW) / 2, 4 + (iconSize - drawH) / 2);

            batch.DrawTexture(_cachedIcon, iconPos, Color.White * AbsoluteOpacity, scale);
        }

        var font = GameContent.FontSystem.GetFont(16);
        font.DrawText(batch, _displayName, AbsolutePosition + new Vector2(30, 5), Color.White * AbsoluteOpacity);
    }
}
