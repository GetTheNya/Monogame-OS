using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame;

namespace ExplorerApp;

public class AppSettings {
    public string LastPath { get; set; } = "C:\\";
    public float WindowX { get; set; } = 100;
    public float WindowY { get; set; } = 100;
    public float WindowWidth { get; set; } = 800;
    public float WindowHeight { get; set; } = 600;
}

public class FileExplorerWindow : Window {
    public static Window CreateWindow() {
        var settings = Shell.LoadSettings<AppSettings>();
        return new FileExplorerWindow(new Vector2(settings.WindowX, settings.WindowY), new Vector2(settings.WindowWidth, settings.WindowHeight), settings);
    }

    private string _currentPath = "C:\\";
    public string CurrentPath => _currentPath;
    private Panel _fileListPanel;
    private Panel _sidebar;
    private Label _pathLabel;
    private DropTargetButton _trashBtn;
    private AppSettings _settings;

    public FileExplorerWindow(Vector2 pos, Vector2 size, AppSettings settings) : base(pos, size) {
        Title = "File Explorer";
        _settings = settings;
        _currentPath = _settings.LastPath;

        OnResize += () => {
            _settings.WindowWidth = Size.X;
            _settings.WindowHeight = Size.Y;
            Shell.SaveSettings(_settings);
        };

        OnMove += () => {
            if (Opacity < 0.9f) return;
            _settings.WindowX = Position.X;
            _settings.WindowY = Position.Y;
            Shell.SaveSettings(_settings);
        };

        SetupUI();
    }

    private void SetupUI() {
        var sidebarWidth = 150f;
        var toolbarHeight = 40f;

        // Toolbar area
        var toolbar = new Panel(Vector2.Zero, new Vector2(ClientSize.X, toolbarHeight)) {
            BackgroundColor = new Color(45, 45, 45),
            BorderThickness = 0,
            ConsumesInput = false
        };
        AddChild(toolbar);

        var backBtn = new Button(new Vector2(5, 5), new Vector2(30, 30), "<") {
            OnClickAction = NavigateBack
        };
        toolbar.AddChild(backBtn);

        _pathLabel = new Label(new Vector2(45, 10), _currentPath) { FontSize = 16 };
        toolbar.AddChild(_pathLabel);

        // Sidebar
        _sidebar = new Panel(new Vector2(5, toolbarHeight), new Vector2(sidebarWidth, ClientSize.Y - toolbarHeight - 5)) {
            BackgroundColor = new Color(35, 35, 35),
            BorderThickness = 0,
            ConsumesInput = false
        };
        AddChild(_sidebar);

        // Main File List
        _fileListPanel = new Panel(new Vector2(sidebarWidth + 5, toolbarHeight), new Vector2(ClientSize.X - sidebarWidth - 10, ClientSize.Y - toolbarHeight - 5)) {
            BackgroundColor = Color.Transparent,
            BorderThickness = 0,
            ConsumesInput = false
        };
        AddChild(_fileListPanel);

        RefreshSidebar();
        RefreshList();
    }

    public override void Update(GameTime gameTime) {
        // Discovery Phase: Check for clicks BEFORE base.Update consumes the state
        bool justPressed = InputManager.IsMouseButtonJustPressed(MouseButton.Left);
        bool justRightPressed = InputManager.IsMouseButtonJustPressed(MouseButton.Right);
        bool justReleased = InputManager.IsMouseButtonJustReleased(MouseButton.Left);

        base.Update(gameTime);

        float toolbarHeight = 40f;
        float sidebarWidth = 150f;
        float margin = 5f;

        foreach (var child in Children) {
            if (child is Panel p) {
                if (child == _sidebar) {
                    p.Size = new Vector2(sidebarWidth, ClientSize.Y - toolbarHeight - margin);
                } else if (child == _fileListPanel) {
                    p.Position = new Vector2(sidebarWidth + margin, toolbarHeight);
                    p.Size = new Vector2(ClientSize.X - sidebarWidth - (margin * 2), ClientSize.Y - toolbarHeight - margin);
                } else if (child.Position == Vector2.Zero) {
                    p.Size = new Vector2(ClientSize.X, toolbarHeight);
                }
            }
        }

        if (_fileListPanel != null) {
            bool isInBounds = _fileListPanel.Bounds.Contains(InputManager.MousePosition);
            bool overChild = false;
            foreach(var child in _fileListPanel.Children) {
                if (child.IsVisible && child.Bounds.Contains(InputManager.MousePosition)) {
                    overChild = true;
                    break;
                }
            }

            if (isInBounds && !overChild && justPressed) {
                _isSelecting = true;
                _selectionStart = InputManager.MousePosition.ToVector2();
                _selectionRect = Rectangle.Empty;
                foreach(var child in _fileListPanel.Children) if (child is FileButton fb) fb.IsSelected = false;
                InputManager.IsMouseConsumed = true;
            }
            
            if (_isSelecting) {
                if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                     Vector2 currentPos = InputManager.MousePosition.ToVector2();
                     var min = Vector2.Min(_selectionStart, currentPos);
                     var max = Vector2.Max(_selectionStart, currentPos);
                     _selectionRect = new Rectangle((int)min.X, (int)min.Y, (int)(max.X - min.X), (int)(max.Y - min.Y));
                     foreach(var child in _fileListPanel.Children) if (child is FileButton fb) fb.IsSelected = _selectionRect.Intersects(fb.Bounds);
                } else {
                    _isSelecting = false;
                    _selectionRect = Rectangle.Empty;
                }
            }

            if (isInBounds && !overChild && justRightPressed) {
                var menuItems = new List<MenuItem> { new MenuItem { Text = "Refresh", Action = RefreshList } };
                if (_currentPath.ToUpper().Contains("$RECYCLE.BIN")) {
                    menuItems.Add(new MenuItem { Text = "Empty Recycle Bin", Action = () => { VirtualFileSystem.Instance.EmptyRecycleBin(); RefreshList(); Shell.RefreshDesktop?.Invoke(); }});
                    menuItems.Add(new MenuItem { Text = "Restore All", Action = () => { VirtualFileSystem.Instance.RestoreAll(); RefreshList(); Shell.RefreshDesktop?.Invoke(); }});
                } else {
                    menuItems.Add(new MenuItem { Text = "New Folder", Action = () => {
                        string path = System.IO.Path.Combine(_currentPath, "New Folder");
                        int i = 1;
                        while (VirtualFileSystem.Instance.Exists(path)) path = System.IO.Path.Combine(_currentPath, $"New Folder ({i++})");
                        VirtualFileSystem.Instance.CreateDirectory(path);
                        RefreshList();
                    }});
                }
                Shell.GlobalContextMenu?.Show(InputManager.MousePosition.ToVector2(), menuItems);
                InputManager.IsMouseConsumed = true;
            }

            _wasMouseDown = InputManager.IsMouseButtonDown(MouseButton.Left);
            if (isInBounds && justReleased && Shell.DraggedItem != null) HandleDrop(Shell.DraggedItem);
        }
    }

    private bool _wasMouseDown;
    private bool _isSelecting;
    private Vector2 _selectionStart;
    private Rectangle _selectionRect;

    public override void Draw(SpriteBatch sb, ShapeBatch sbatch) {
        base.Draw(sb, sbatch);
        if (_isSelecting && _fileListPanel != null) {
            var clip = Rectangle.Intersect(_selectionRect, _fileListPanel.Bounds);
            if (clip.Width > 0 && clip.Height > 0) sbatch.FillRectangle(new Vector2(clip.X, clip.Y), new Vector2(clip.Width, clip.Height), Color.CornflowerBlue * 0.3f);
        }
    }

    private void HandleDrop(object item) {
        if (item is DesktopIcon d && d.VirtualPath.IndexOf("$Recycle.Bin", StringComparison.OrdinalIgnoreCase) >= 0) return;
        bool changed = false;
        if (item is string path) { MoveToCurrent(path); changed = true; }
        else if (item is DesktopIcon di) { MoveToCurrent(di.VirtualPath); changed = true; }
        else if (item is List<string> list) { foreach (var p in list) MoveToCurrent(p); changed = true; }
        if (changed) { RefreshList(); Shell.RefreshDesktop?.Invoke(); Shell.RefreshExplorers(); Shell.DraggedItem = null; }
        InputManager.IsMouseConsumed = true;
    }

    private void MoveToCurrent(string sourcePath) {
        if (string.IsNullOrEmpty(sourcePath)) return;
        string fileName = System.IO.Path.GetFileName(sourcePath.TrimEnd('\\', '/'));
        string destPath = System.IO.Path.Combine(_currentPath, fileName);
        if (sourcePath.ToUpper() == destPath.ToUpper()) return;
        if (VirtualFileSystem.Instance.Exists(destPath)) {
            string dir = _currentPath; string name = System.IO.Path.GetFileNameWithoutExtension(fileName); string ext = System.IO.Path.GetExtension(fileName);
            int i = 1; while (VirtualFileSystem.Instance.Exists(destPath)) destPath = System.IO.Path.Combine(dir, $"{name} ({i++}){ext}");
        }
        VirtualFileSystem.Instance.Move(sourcePath, destPath);
    }

    private void RefreshSidebar() {
        foreach (var child in _sidebar.Children.ToArray()) _sidebar.RemoveChild(child);
        float y = 10;
        _sidebar.AddChild(new Label(new Vector2(10, y), "This PC") { FontSize = 14, Color = Color.Gray });
        y += 25;
        foreach (var drive in VirtualFileSystem.Instance.GetDrives()) {
            string pcIconPath = VirtualFileSystem.Instance.ToHostPath("C:\\Windows\\SystemResources\\Icons\\PC.png");
            var btn = new Button(new Vector2(5, y), new Vector2(_sidebar.Size.X - 10, 30), drive) { 
                BackgroundColor = Color.Transparent, 
                Icon = System.IO.File.Exists(pcIconPath) ? TheGame.Core.ImageLoader.Load(G.GraphicsDevice, pcIconPath) : null 
            };
            btn.OnClickAction = () => NavigateTo(drive);
            _sidebar.AddChild(btn);
            y += 35;
        }
        _trashBtn = new DropTargetButton(new Vector2(5, y), new Vector2(_sidebar.Size.X - 10, 30), "Recycle Bin") { 
            BackgroundColor = Color.Transparent, 
            Icon = Shell.GetIcon("C:\\$Recycle.Bin\\") 
        };
        _trashBtn.OnClickAction = () => NavigateTo("C:\\$Recycle.Bin\\");
        _trashBtn.OnDropAction = (item) => {
            if (item is string p) VirtualFileSystem.Instance.Recycle(p);
            else if (item is DesktopIcon di) VirtualFileSystem.Instance.Recycle(di.VirtualPath);
            else if (item is List<string> list) foreach(var path in list) VirtualFileSystem.Instance.Recycle(path);
            RefreshList(); Shell.RefreshDesktop?.Invoke(); Shell.RefreshExplorers(); Shell.DraggedItem = null;
        };
        _sidebar.AddChild(_trashBtn);
    }

    public void RefreshList() {
        foreach (var child in _fileListPanel.Children.ToArray()) _fileListPanel.RemoveChild(child);
        _pathLabel.Text = _currentPath;
        
        // Update trash icon state
        if (_trashBtn != null) _trashBtn.Icon = Shell.GetIcon("C:\\$Recycle.Bin\\");

        float y = 10; float padding = 10; float btnWidth = _fileListPanel.Size.X - (padding * 2);
        foreach (var dir in VirtualFileSystem.Instance.GetDirectories(_currentPath)) {
            string name = dir.Split('\\').Last(); if (string.IsNullOrEmpty(name)) continue;
            
            // Hide system/hidden folders starting with $ (like $Recycle.Bin)
            if (name.StartsWith("$")) continue;

            if (name.ToLower().EndsWith(".sapp")) {
                var btn = new FileButton(new Vector2(padding, y), new Vector2(btnWidth, 30), name, dir) { Icon = Shell.GetIcon(dir) };
                btn.OnClickAction = () => Shell.Execute(dir, btn.Bounds);
                btn.OnRightClickAction = () => ShowContextMenu(btn);
                _fileListPanel.AddChild(btn);
            } else {
                var btn = new FileButton(new Vector2(padding, y), new Vector2(btnWidth, 30), name, dir) { Icon = GameContent.FolderIcon, OnClickAction = () => NavigateTo(dir + "\\") };
                btn.OnRightClickAction = () => ShowContextMenu(btn);
                _fileListPanel.AddChild(btn);
            }
            y += 35;
        }
        foreach (var file in VirtualFileSystem.Instance.GetFiles(_currentPath)) {
            string name = file.Split('\\').Last();
            
            // Hide system/hidden files starting with $
            if (name.StartsWith("$")) continue;

            var btn = new FileButton(new Vector2(padding, y), new Vector2(btnWidth, 30), name, file) { Icon = GameContent.FileIcon };
            btn.OnClickAction = () => Shell.Execute(file, btn.Bounds);
            btn.OnRightClickAction = () => ShowContextMenu(btn);
            _fileListPanel.AddChild(btn);
            y += 35;
        }
    }

    private void ShowContextMenu(FileButton btn) {
        var menuItems = new List<MenuItem>();
        string path = btn.VirtualPath;
        bool isApp = path.ToLower().EndsWith(".sapp");
        bool isDir = VirtualFileSystem.Instance.IsDirectory(path);

        menuItems.Add(new MenuItem { 
            Text = "Open", 
            Action = () => {
                if (isDir && !isApp) NavigateTo(path + "\\");
                else Shell.Execute(path, btn.Bounds);
            } 
        });

        if (isApp) {
            menuItems.Add(new MenuItem { Text = "Open app as folder", Action = () => NavigateTo(path + "\\") });
        }

        menuItems.Add(new MenuItem { Text = "Delete", Action = () => { 
            VirtualFileSystem.Instance.Recycle(path); 
            RefreshList(); 
            Shell.RefreshDesktop?.Invoke(); 
        }});

        Shell.GlobalContextMenu?.Show(InputManager.MousePosition.ToVector2(), menuItems);
    }

    public void NavigateTo(string path) {
        _currentPath = path;
        _settings.LastPath = path;
        Shell.SaveSettings(_settings);

        if (_currentPath.ToUpper().Contains("$RECYCLE.BIN")) {
            Title = "Recycle Bin";
            var trashFiles = VirtualFileSystem.Instance.GetFiles(_currentPath); var trashDirs = VirtualFileSystem.Instance.GetDirectories(_currentPath);
            Icon = (trashFiles.Length > 0 || trashDirs.Length > 0) ? GameContent.TrashFullIcon : GameContent.TrashEmptyIcon;
        } else { Title = "File Explorer"; Icon = GameContent.ExplorerIcon; }
        RefreshList();
    }

    private void NavigateBack() {
        var parts = _currentPath.TrimEnd('\\').Split('\\');
        if (parts.Length > 1) {
            var newPath = string.Join("\\", parts.Take(parts.Length - 1)) + "\\";
            NavigateTo(newPath);
        }
    }
}
