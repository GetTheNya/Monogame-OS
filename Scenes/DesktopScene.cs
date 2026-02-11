using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using TheGame.Core.UI;
using TheGame.Core.Input;
using TheGame.Core;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.UI.Controls;
using TheGame.Core.OS.DragDrop;
using System.Linq;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace TheGame.Scenes;

public class DesktopScene : Core.Scenes.Scene {
    private const int TaskbarHeight = 40;
    // TODO: Make start menu decide its own size
    private const int StartMenuWidth = 350;
    private const int StartMenuHeight = 450;

    private UIManager _uiManager;
    private DesktopPanel _desktopLayer;
    private BlurredWindowLayerPanel _windowLayer;
    private StartMenu _startMenu;
    private ClipboardHistoryPanel _clipboardPanel;
    private VolumeMixerPanel _volumeMixerPanel;
    private Taskbar _taskbar;
    private ContextMenu _contextMenu;
    private DesktopIcon _trashIconEl;

    private Dictionary<string, Vector2> _iconPositions = new Dictionary<string, Vector2>();
    private string _sortType = "None";
    
    // Grid alignment settings
    private const int DesktopPadding = 15;
    private const int GridCellWidth = 100;
    private const int GridCellHeight = 110;
    private bool _alignToGrid = true;

    private RenderTarget2D _mainTarget;
    private Texture2D _wallpaperTexture;
    private string _wallpaperPath;

    // Notification System
    private NotificationHistoryPanel _notificationPanel;
    private List<NotificationToast> _activeToasts = new List<NotificationToast>();

    public DesktopScene() {
    }

    public override void LoadContent(ContentManager content) {
        Shell.RefreshDesktop = LoadDesktopIcons;
        _uiManager = new UIManager();
        
        // Load grid alignment setting from Registry
        _alignToGrid = TheGame.Core.OS.Registry.Instance.GetValue($"{Shell.Registry.Desktop}\\AlignToGrid", true);

        var viewport = G.GraphicsDevice.Viewport;
        var screenWidth = viewport.Width;
        var screenHeight = viewport.Height;

        RegisterGlobalHotkeys();

        // Load wallpaper from settings
        LoadWallpaper();
        Settings.Personalization.OnWallpaperChanged = LoadWallpaper;

        // 0. Shell Overlay Layer - Context Menus (Created first so others can reference it)
        _contextMenu = new ContextMenu();

        // 1. Desktop Layer (Bottom) - Icons etc.
        _desktopLayer = new DesktopPanel(this, Vector2.Zero, new Vector2(screenWidth, screenHeight));
        _uiManager.AddElement(_desktopLayer);
        _desktopLayer.ContextMenu = _contextMenu;
        _desktopLayer.RefreshAction = LoadDesktopIcons;

        // 2. Window Layer (Middle) - Windows
        _windowLayer = new BlurredWindowLayerPanel(Vector2.Zero, new Vector2(screenWidth, screenHeight));
        _uiManager.AddElement(_windowLayer);

        _startMenu = new StartMenu(new Vector2(0, screenHeight - 500 - 40), new Vector2(400, 500));
        _startMenu.IsVisible = false;
        _uiManager.AddElement(_startMenu);

        _clipboardPanel = new ClipboardHistoryPanel();
        _clipboardPanel.IsVisible = false;
        _uiManager.AddElement(_clipboardPanel);
        
        _volumeMixerPanel = new VolumeMixerPanel();
        _volumeMixerPanel.IsVisible = false;
        _uiManager.AddElement(_volumeMixerPanel);
        
        // Register Shell event to update Explorers
        Shell.Initialize(_windowLayer, _contextMenu);
        Shell.OnAddOverlayElement = (el) => _uiManager.AddElement(el);
        Shell.OnRemoveOverlayElement = (el) => _uiManager.RemoveElement(el);

        // Initialize Notification System
        _notificationPanel = new NotificationHistoryPanel();
        _uiManager.AddElement(_notificationPanel);

        // 3. Taskbar Layer (Added LAST so it updates FIRST in the reverse-order loop)
        _taskbar = new Taskbar(new Vector2(0, screenHeight - TaskbarHeight), new Vector2(screenWidth, TaskbarHeight), _windowLayer, _startMenu, _volumeMixerPanel);
        _uiManager.AddElement(_taskbar);

        // Add Context Menu last
        _uiManager.AddElement(_contextMenu);
        
        // Wire up SystemTray notification click
        _taskbar.SystemTray.OnNotificationClick = () => _notificationPanel.Toggle();

        // Subscribe to notifications for toasts
        NotificationManager.Instance.OnNotificationAdded += ShowToast;
        NotificationManager.Instance.OnNotificationDismissed += DismissToastById;

        // Play Startup Sound
        Shell.Audio.PlaySound("C:\\Windows\\Media\\startup.wav");

        // Demo notification
        string welcomeId = "";
        welcomeId = Shell.Notifications.Show("Welcome!", "The notification system is ready.", null, null, new List<NotificationAction> {
            new NotificationAction { Label = "Got it", OnClick = () => {
                DebugLogger.Log("User acknowledged.");
                if (!string.IsNullOrEmpty(welcomeId)) Shell.Notifications.Dismiss(welcomeId);
            }}
        });

        Shell.Notifications.Show("System Update", "A very long message that previously would have been truncated but now wraps nicely across multiple lines in both the toast and the history panel.", actions: new List<NotificationAction> {
            new NotificationAction { Label = "Details", OnClick = () => DebugLogger.Log("Update details.") },
            new NotificationAction { Label = "OK", OnClick = () => { } }
        });

        // Load startup apps from Registry
        ProcessManager.Instance.LoadStartupApps();

        string pcIconPath = VirtualFileSystem.Instance.ToHostPath("C:\\Windows\\SystemResources\\Icons\\PC.png");
        var pcIcon = System.IO.File.Exists(pcIconPath) ? Core.ImageLoader.Load(G.GraphicsDevice, pcIconPath) : GameContent.FileIcon;

        // Load Dynamic Desktop Icons
        LoadDesktopIcons();

        // Populate Start Menu
        _startMenu.RefreshItems();
    }

    private void RegisterGlobalHotkeys() {
        DebugLogger.Log("Registering global/system hotkeys");
        
        // Win key -> Start Menu (System default)
        Shell.Hotkeys.RegisterGlobal(Keys.None, HotkeyModifiers.Win, () => {
            _startMenu?.Toggle();
        });

        // Win+V -> Clipboard History
        Shell.Hotkeys.RegisterGlobal(Keys.V, HotkeyModifiers.Win, () => {
            _clipboardPanel?.Toggle();
        });

        // Global Copy/Cut/Paste/Undo/Redo (Standard fallback)
        Shell.Hotkeys.RegisterGlobal(Keys.C, HotkeyModifiers.Ctrl, () => UIManager.FocusedElement?.Copy());
        Shell.Hotkeys.RegisterGlobal(Keys.X, HotkeyModifiers.Ctrl, () => UIManager.FocusedElement?.Cut());
        Shell.Hotkeys.RegisterGlobal(Keys.V, HotkeyModifiers.Ctrl, () => UIManager.FocusedElement?.Paste());
        Shell.Hotkeys.RegisterGlobal(Keys.Z, HotkeyModifiers.Ctrl, () => (UIManager.FocusedElement ?? Window.ActiveWindow)?.Undo());
        Shell.Hotkeys.RegisterGlobal(Keys.Y, HotkeyModifiers.Ctrl, () => (UIManager.FocusedElement ?? Window.ActiveWindow)?.Redo());
    }

    private void LoadDesktopIcons() {
        foreach (var child in _desktopLayer.Children.ToArray()) _desktopLayer.RemoveChild(child);

        string desktopPath = $"C:\\Users\\{SystemConfig.Username}\\Desktop\\";
        var files = Core.OS.VirtualFileSystem.Instance.GetFiles(desktopPath);
        var dirs = Core.OS.VirtualFileSystem.Instance.GetDirectories(desktopPath);

        // Combine files and all folders for desktop display
        var itemsList = files.Concat(dirs).Where(i => !i.Contains("$Recycle.Bin"));
        
        // Add Recycle Bin to the list so it can be sorted
        string trashPath = "C:\\$Recycle.Bin\\";
        itemsList = itemsList.Concat(new[] { trashPath });

        if (_sortType == "Name") itemsList = itemsList.OrderBy(i => i == trashPath ? "Recycle Bin" : System.IO.Path.GetFileName(i));
        else if (_sortType == "Type") itemsList = itemsList.OrderBy(i => i == trashPath ? "!" : System.IO.Path.GetExtension(i)); // Trash first in type sort
        else if (_sortType == "Size") itemsList = itemsList.OrderBy(f => { 
            if (f == trashPath) return 0;
            try { return new System.IO.FileInfo(VirtualFileSystem.Instance.ToHostPath(f)).Length; } catch { return 0; } 
        });

        var items = itemsList.ToArray();

        float x = DesktopPadding;
        float y = DesktopPadding;
        float gap = 20;

        foreach (var item in items) {
            string fileName = System.IO.Path.GetFileName(item);
            if (item == trashPath) fileName = "Recycle Bin";
            else if (fileName.ToLower().EndsWith(".slnk") || fileName.ToLower().EndsWith(".sapp")) {
                fileName = System.IO.Path.GetFileNameWithoutExtension(item);
            }
            
            Texture2D iconTex = (item == trashPath) ? 
                (VirtualFileSystem.Instance.IsRecycleBinEmpty() ? GameContent.TrashEmptyIcon : GameContent.TrashFullIcon) : 
                Shell.GetIcon(item);

            var icon = new DesktopIcon(Vector2.Zero, fileName, iconTex);

            if (_iconPositions.ContainsKey(item)) {
                icon.Position = _iconPositions[item];
            } else {
                Vector2? savedPos = LoadIconPosition(item);
                if (savedPos.HasValue) {
                    icon.Position = savedPos.Value;
                    _iconPositions[item] = icon.Position;
                } else {
                    icon.Position = new Vector2(x, y);
                    _iconPositions[item] = icon.Position;
                }
            }

            y += icon.Size.Y + gap;
            if (y + icon.Size.Y > G.GraphicsDevice.Viewport.Height - DesktopPadding) { 
                y = DesktopPadding;
                x += icon.Size.X + gap;
            }

            icon.VirtualPath = item;
            icon.OnDragAction = (i, delta) =>  { 
                i.DragDelta += delta;
                Vector2 finalPos = _iconPositions[i.VirtualPath] + i.DragDelta;
                Shell.Drag.SetDropPreview(i, finalPos);
            };
            icon.OnSelectedAction = (selected) => {
                foreach (var child in _desktopLayer.Children) {
                    if (child is DesktopIcon d && d != selected) d.IsSelected = false;
                }
            };

            if (item == trashPath) {
                _trashIconEl = icon;
                icon.OnDoubleClickAction = () => Shell.Execute(trashPath, icon.Bounds);
                icon.OnDropAction = () => {
                    if (icon.DragDelta.LengthSquared() > 1.0f) {
                        Vector2 targetPos = icon.Position + icon.DragDelta;
                        Vector2 newPos = targetPos;
                        if (_alignToGrid) {
                            Vector2 snapped = SnapToGrid(targetPos);
                            var occupied = GetOccupiedCells(icon);
                            var nearest = FindNearestAvailableCell((int)((snapped.X - DesktopPadding) / GridCellWidth), (int)((snapped.Y - DesktopPadding) / GridCellHeight), occupied);
                            newPos = new Vector2(nearest.x * GridCellWidth + DesktopPadding, nearest.y * GridCellHeight + DesktopPadding);
                        }
                        icon.Position = newPos;
                        _iconPositions[trashPath] = newPos;
                        SaveIconPosition(trashPath, newPos);
                    }
                    icon.DragDelta = Vector2.Zero;
                    Shell.Drag.SetDropPreview(icon, null);

                    var dragged = Shell.Drag.DraggedItem;
                    if (dragged != null && dragged != icon) {
                        if (dragged is DesktopIcon di) VirtualFileSystem.Instance.Recycle(di.VirtualPath);
                        else if (dragged is string s) VirtualFileSystem.Instance.Recycle(s);
                        else if (dragged is List<string> list) foreach (var p in list) VirtualFileSystem.Instance.Recycle(p);
                        LoadDesktopIcons();
                        Shell.RefreshExplorers();
                        Shell.Drag.DraggedItem = null;
                    }
                    Shell.Drag.End();
                };
                icon.OnRightClickAction = () => {
                    _contextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                        new MenuItem { Text = "Open", Action = () => Shell.Execute(trashPath, icon.Bounds) },
                        new MenuItem { Text = "Empty Recycle Bin", Action = () => Shell.PromptEmptyRecycleBin() },
                        new MenuItem {
                            Text = "Restore All", Action = () => {
                                var mb = new MessageBox("Restore All", "Are you sure you want to restore all items from the Recycle Bin?", MessageBoxButtons.YesNo, (confirmed) => {
                                    if (confirmed) {
                                        VirtualFileSystem.Instance.RestoreAll();
                                        LoadDesktopIcons();
                                        Shell.RefreshExplorers("$Recycle.Bin");
                                    }
                                });
                                Shell.UI.OpenWindow(mb);
                            }
                        },
                        new MenuItem { Text = "Properties", Action = () => DebugLogger.Log("Trash Properties") }
                    });
                };
                _desktopLayer.TrashIcon = icon;
            } else {
                icon.OnDoubleClickAction = () => Shell.Execute(item, icon.Bounds);
                icon.OnDropAction = () => {
                    Vector2 targetPos = icon.Position + icon.DragDelta;
                    
                    // Priority 1: Check if dropped on Trash
                    bool droppedOnTrash = _trashIconEl != null && _trashIconEl.Bounds.Contains(InputManager.MousePosition);
                    
                    if (droppedOnTrash) {
                        if (icon.IsSelected) {
                            var selectedPaths = _desktopLayer.Children.OfType<DesktopIcon>().Where(i => i.IsSelected && !string.IsNullOrEmpty(i.VirtualPath)).Select(i => i.VirtualPath).ToList();
                            foreach (var path in selectedPaths) VirtualFileSystem.Instance.Recycle(path);
                        } else {
                            VirtualFileSystem.Instance.Recycle(icon.VirtualPath);
                        }
                        LoadDesktopIcons();
                        Shell.RefreshExplorers("$Recycle.Bin");
                        Shell.Drag.End();
                        return;
                    }

                    // Priority 2: Move icon(s)
                    if (icon.IsSelected) {
                        foreach (var child in _desktopLayer.Children) {
                            if (child is DesktopIcon d && d.IsSelected) {
                                Shell.Drag.SetDropPreview(d, null);
                                if (d.DragDelta.LengthSquared() > 1.0f) {
                                    Vector2 dTargetPos = d.Position + d.DragDelta;
                                    Vector2 newPos = dTargetPos;
                                    if (_alignToGrid) {
                                        Vector2 snapped = SnapToGrid(dTargetPos);
                                        var occupied = GetOccupiedCells(d);
                                        var nearest = FindNearestAvailableCell((int)((snapped.X - DesktopPadding) / GridCellWidth), (int)((snapped.Y - DesktopPadding) / GridCellHeight), occupied);
                                        newPos = new Vector2(nearest.x * GridCellWidth + DesktopPadding, nearest.y * GridCellHeight + DesktopPadding);
                                    }
                                    d.Position = newPos;
                                    _iconPositions[d.VirtualPath] = d.Position;
                                    SaveIconPosition(d.VirtualPath, d.Position);
                                }
                                d.DragDelta = Vector2.Zero;
                            }
                        }
                    } else {
                        Shell.Drag.SetDropPreview(icon, null);
                        if (icon.DragDelta.LengthSquared() > 1.0f) {
                            Vector2 newPos = targetPos;
                            if (_alignToGrid) {
                                Vector2 snapped = SnapToGrid(targetPos);
                                var occupied = GetOccupiedCells(icon);
                                var nearest = FindNearestAvailableCell((int)((snapped.X - DesktopPadding) / GridCellWidth), (int)((snapped.Y - DesktopPadding) / GridCellHeight), occupied);
                                newPos = new Vector2(nearest.x * GridCellWidth + DesktopPadding, nearest.y * GridCellHeight + DesktopPadding);
                            }
                            icon.Position = newPos;
                            _iconPositions[icon.VirtualPath] = icon.Position;
                            SaveIconPosition(icon.VirtualPath, icon.Position);
                        }
                        icon.DragDelta = Vector2.Zero;
                    }
                    Shell.Drag.End();
                };
                icon.OnRightClickAction = () => {
                    _contextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                        new MenuItem { Text = "Open", Action = () => Shell.Execute(item, icon.Bounds) },
                        new MenuItem { Text = "Run as Administrator", Action = () => Shell.Execute(item, icon.Bounds) },
                        new MenuItem { Text = "Rename", Action = () => icon.StartRename() },
                        new MenuItem { Text = "Properties", Action = () => DebugLogger.Log($"Properties for {fileName}") },
                        new MenuItem {
                            Text = "Delete", Action = () => {
                                var mb = new MessageBox("Delete", $"Are you sure you want to move '{fileName}' to the Recycle Bin?", MessageBoxButtons.YesNo, (confirmed) => {
                                    if (confirmed) {
                                        VirtualFileSystem.Instance.Recycle(item);
                                        LoadDesktopIcons();
                                        Shell.RefreshExplorers("$Recycle.Bin");
                                    }
                                });
                                Shell.UI.OpenWindow(mb);
                            }
                        }
                    });
                };
            }
            icon.OnRenamed = () => {
                LoadDesktopIcons();
                Shell.RefreshExplorers();
            };
            _desktopLayer.AddChild(icon);
        }

        _desktopLayer.SortAction = SortIcons;
    }

    private void ShowToast(Notification notification) {
        float yOffset = 0f;
        foreach (var t in _activeToasts) {
            yOffset += t.Size.Y + 10f;
        }
        var toast = new NotificationToast(notification, yOffset);
        toast.OnDismissed = (t) => {
            _activeToasts.Remove(t);
            _uiManager.RemoveElement(t);
            UpdateToastPositions();
        };
        _activeToasts.Add(toast);
        _uiManager.AddElement(toast);
    }

    private void UpdateToastPositions() {
        float yOffset = 0f;
        foreach (var toast in _activeToasts) {
            toast.UpdateTargetPosition(yOffset);
            yOffset += toast.Size.Y + 10f;
        }
    }

    private void DismissToastById(string notificationId) {
        var toast = _activeToasts.Find(t => t.NotificationId == notificationId);
        if (toast != null) {
            toast.Close();
        }
    }

    public override void Update(GameTime gameTime) {
        var viewport = G.GraphicsDevice.Viewport;
        if (viewport.Width != _desktopLayer.Size.X || viewport.Height != _desktopLayer.Size.Y) {
            // Screen Resized
            _desktopLayer.Size = new Vector2(viewport.Width, viewport.Height);
            _windowLayer.Size = new Vector2(viewport.Width, viewport.Height);
            _taskbar.Position = new Vector2(0, viewport.Height - TaskbarHeight);
            _taskbar.Size = new Vector2(viewport.Width, TaskbarHeight);
            _startMenu.OnResize(viewport.Width, viewport.Height);
            _clipboardPanel.Position = Vector2.Zero; // Let it re-position on toggle
            _volumeMixerPanel.Position = Vector2.Zero;
            _notificationPanel.OnResize(viewport.Width, viewport.Height);
        }

        // Process hot reload on main thread
        AppHotReloadManager.Instance.Update();
        
        _uiManager.Update(gameTime);

        // Update toast positions
        UpdateToastPositions();
    }

    public override void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        var gd = G.GraphicsDevice;

        if (_mainTarget == null || _mainTarget.Width != gd.Viewport.Width || _mainTarget.Height != gd.Viewport.Height) {
            _mainTarget?.Dispose();
            _mainTarget = new RenderTarget2D(
                gd,
                gd.Viewport.Width,
                gd.Viewport.Height,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0, // MipMap count
                RenderTargetUsage.PreserveContents // <--- ЦЕ ВАЖЛИВО!
            );
        }

        gd.SetRenderTarget(_mainTarget);
        gd.Clear(Color.Black);

        spriteBatch.Begin();
        if (_wallpaperTexture != null) {
            DrawWallpaper(spriteBatch, gd.Viewport);
        }
        spriteBatch.End();

        shapeBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        _uiManager.Draw(spriteBatch, shapeBatch);

        shapeBatch.End();
        spriteBatch.End();

        gd.SetRenderTarget(null);

        spriteBatch.Begin();
        spriteBatch.Draw(_mainTarget, Vector2.Zero, Color.White);
        spriteBatch.End();
    }

    private void SortIcons(string type) {
        _sortType = type;
        
        // Clear all tracked and persisted positions for the icons we're about to sort
        // This forces LoadDesktopIcons to use its default layout logic in the sorted order
        string desktopPath = $"C:\\Users\\{SystemConfig.Username}\\Desktop\\";
        var files = Core.OS.VirtualFileSystem.Instance.GetFiles(desktopPath);
        var dirs = Core.OS.VirtualFileSystem.Instance.GetDirectories(desktopPath);
        string trashPath = "C:\\$Recycle.Bin\\";
        var allItems = files.Concat(dirs).Where(i => !i.Contains("$Recycle.Bin")).Concat(new[] { trashPath }).ToList();
        
        foreach (var item in allItems) {
            _iconPositions.Remove(item);
            // Optionally delete from Registry too to make it "permanent"
            string encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(item));
            string key = $"{Shell.Registry.Desktop}\\IconPositions\\{encodedPath}";
            TheGame.Core.OS.Registry.Instance.DeleteKey($"{key}\\X");
            TheGame.Core.OS.Registry.Instance.DeleteKey($"{key}\\Y");
        }

        LoadDesktopIcons();
        
        // If grid is on, ensure they are perfectly aligned
        if (_alignToGrid) {
            ArrangeIconsToGrid();
        } else {
            // Even if grid is off, save the current positions from the sort
            foreach (var child in _desktopLayer.Children.OfType<DesktopIcon>()) {
                if (!string.IsNullOrEmpty(child.VirtualPath)) {
                    SaveIconPosition(child.VirtualPath, child.Position);
                }
            }
        }

        Shell.RefreshExplorers(); // Refresh explorers too for consistent view
    }
    
    private Vector2 SnapToGrid(Vector2 position) {
        int gridX = DesktopPadding + (int)Math.Round((position.X - DesktopPadding) / GridCellWidth) * GridCellWidth;
        int gridY = DesktopPadding + (int)Math.Round((position.Y - DesktopPadding) / GridCellHeight) * GridCellHeight;
        return new Vector2(gridX, gridY);
    }
    
    private void ArrangeIconsToGrid() {
        var icons = _desktopLayer.Children.OfType<DesktopIcon>().Where(i => !string.IsNullOrEmpty(i.VirtualPath)).ToList();
        
        // Sort icons by current position (top-left to bottom-right)
        icons = icons.OrderBy(i => i.Position.Y).ThenBy(i => i.Position.X).ToList();
        
        // Track occupied grid cells
        HashSet<(int, int)> occupiedCells = new HashSet<(int, int)>();
        
        foreach (var icon in icons) {
            // Find nearest available grid cell
            Vector2 snapped = SnapToGrid(icon.Position);
            int startX = (int)((snapped.X - DesktopPadding) / GridCellWidth);
            int startY = (int)((snapped.Y - DesktopPadding) / GridCellHeight);
            
            (int x, int y) = FindNearestAvailableCell(startX, startY, occupiedCells);
            
            // Move icon to grid position
            Vector2 newPos = new Vector2(x * GridCellWidth + DesktopPadding, y * GridCellHeight + DesktopPadding);
            icon.Position = newPos;
            _iconPositions[icon.VirtualPath] = newPos;
            SaveIconPosition(icon.VirtualPath, newPos);
            
            // Mark cell as occupied
            occupiedCells.Add((x, y));
        }
    }

    private HashSet<(int, int)> GetOccupiedCells(DesktopIcon excluding = null) {
        HashSet<(int, int)> occupied = new HashSet<(int, int)>();
        foreach (var child in _desktopLayer.Children) {
            if (child is DesktopIcon di && !string.IsNullOrEmpty(di.VirtualPath)) {
                // If it's the one we're moving, or part of the selection, don't count it as "occupying" its current spot
                // because we're about to move it.
                if (di == excluding || (excluding != null && excluding.IsSelected && di.IsSelected)) continue;
                
                Vector2 snapped = SnapToGrid(di.Position);
                int gridX = (int)((snapped.X - DesktopPadding) / GridCellWidth);
                int gridY = (int)((snapped.Y - DesktopPadding) / GridCellHeight);
                occupied.Add((gridX, gridY));
            }
        }
        return occupied;
    }
    
    private (int x, int y) FindNearestAvailableCell(int startX, int startY, HashSet<(int, int)> occupiedCells) {
        // Check starting position first
        if (!occupiedCells.Contains((startX, startY))) {
            return (startX, startY);
        }
        
        // Expand in spiral pattern to find nearest available cell
        for (int radius = 1; radius < 50; radius++) {
            for (int dx = -radius; dx <= radius; dx++) {
                for (int dy = -radius; dy <= radius; dy++) {
                    if (Math.Abs(dx) == radius || Math.Abs(dy) == radius) {
                        int testX = startX + dx;
                        int testY = startY + dy;
                        
                        if (testX >= 0 && testY >= 0 && !occupiedCells.Contains((testX, testY))) {
                            return (testX, testY);
                        }
                    }
                }
            }
        }
        
        // Fallback (should never reach here)
        return (startX, startY);
    }
    
    private void SaveIconPosition(string virtualPath, Vector2 position) {
        try {
            // Use Base64 encoding to avoid Registry key issues with special characters
            string encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(virtualPath));
            string key = $"{Shell.Registry.Desktop}\\IconPositions\\{encodedPath}";
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\X", position.X);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Y", position.Y);
        } catch { }
    }
    
    private Vector2? LoadIconPosition(string virtualPath) {
        try {
            string encodedPath = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(virtualPath));
            string key = $"{Shell.Registry.Desktop}\\IconPositions\\{encodedPath}";
            
            if (TheGame.Core.OS.Registry.Instance.KeyExists($"{key}\\X") && TheGame.Core.OS.Registry.Instance.KeyExists($"{key}\\Y")) {
                float x = TheGame.Core.OS.Registry.Instance.GetValue<float>($"{key}\\X", 0f);
                float y = TheGame.Core.OS.Registry.Instance.GetValue<float>($"{key}\\Y", 0f);
                return new Vector2(x, y);
            }
        } catch { }
        
        return null;
    }

    private void LoadWallpaper() {
        try {
            string path = Settings.Personalization.WallpaperPath;
            string hostPath = VirtualFileSystem.Instance.ToHostPath(path);
            if (System.IO.File.Exists(hostPath)) {
                // Unload previous wallpaper from cache if exists
                if (_wallpaperPath != null) {
                    Core.ImageLoader.Unload(_wallpaperPath);
                }
                
                _wallpaperPath = hostPath;
                _wallpaperTexture = Core.ImageLoader.Load(G.GraphicsDevice, hostPath);
            }
        } catch (Exception ex) {
            DebugLogger.Log($"Error loading wallpaper: {ex.Message}");
        }
    }

    private void DrawWallpaper(SpriteBatch sb, Viewport vp) {
        if (_wallpaperTexture == null) return;
        
        string mode = Settings.Personalization.WallpaperDrawMode;
        Rectangle screen = new Rectangle(0, 0, vp.Width, vp.Height);
        
        switch (mode) {
            case "Fill": // Cover screen, crop if needed
                float scaleX = (float)vp.Width / _wallpaperTexture.Width;
                float scaleY = (float)vp.Height / _wallpaperTexture.Height;
                float scale = Math.Max(scaleX, scaleY);
                int w = (int)(_wallpaperTexture.Width * scale);
                int h = (int)(_wallpaperTexture.Height * scale);
                int x = (vp.Width - w) / 2;
                int y = (vp.Height - h) / 2;
                sb.Draw(_wallpaperTexture, new Rectangle(x, y, w, h), Color.White);
                break;
                
            case "Fit": // Fit entire image, may have bars
                scaleX = (float)vp.Width / _wallpaperTexture.Width;
                scaleY = (float)vp.Height / _wallpaperTexture.Height;
                scale = Math.Min(scaleX, scaleY);
                w = (int)(_wallpaperTexture.Width * scale);
                h = (int)(_wallpaperTexture.Height * scale);
                x = (vp.Width - w) / 2;
                y = (vp.Height - h) / 2;
                sb.Draw(_wallpaperTexture, new Rectangle(x, y, w, h), Color.White);
                break;
                
            case "Stretch": // Distort to fill
                sb.Draw(_wallpaperTexture, screen, Color.White);
                break;
                
            case "Tile": // Repeat pattern
                for (int ty = 0; ty < vp.Height; ty += _wallpaperTexture.Height) {
                    for (int tx = 0; tx < vp.Width; tx += _wallpaperTexture.Width) {
                        sb.Draw(_wallpaperTexture, new Vector2(tx, ty), Color.White);
                    }
                }
                break;
                
            case "Center": // Original size, centered
                x = (vp.Width - _wallpaperTexture.Width) / 2;
                y = (vp.Height - _wallpaperTexture.Height) / 2;
                sb.Draw(_wallpaperTexture, new Vector2(x, y), Color.White);
                break;
                
            default:
                sb.Draw(_wallpaperTexture, screen, Color.White);
                break;
        }
    }



    private class DesktopPanel : Panel {
        private DesktopScene _scene;
        private bool _isSelecting;
        private Vector2 _selectionStart;
        private Rectangle _marqueeRect;
        private bool _wasMouseDown;
        private bool _isHandlingGroupDrag;
        public Action RefreshAction { get; set; }
        public Action<string> SortAction { get; set; }
        public Action<DesktopIcon> IconMovedAction { get; set; }
        public DesktopIcon TrashIcon { get; set; }
        public ContextMenu ContextMenu { get; set; }

        public DesktopPanel(DesktopScene scene, Vector2 pos, Vector2 size) : base(pos, size) {
            _scene = scene;
            BackgroundColor = Color.Transparent;
            BorderThickness = 0;
            ConsumesInput = true;

            // Connect to Shell API
            Shell.Desktop.GetNextFreePosition = FindFreeDesktopSpot;
            Shell.Desktop.SetIconPosition = (path, position) => {
                _scene._iconPositions[path] = position;
                _scene.SaveIconPosition(path, position);
            };
        }

        public override void AddChild(UIElement child) {
            base.AddChild(child);
            if (child is DesktopIcon icon) {
                icon.OnDragAction += HandleGroupDrag;
                icon.OnSelectedAction += (selected) => {
                    foreach (var c in Children) {
                        if (c is DesktopIcon other && other != selected) other.IsSelected = false;
                    }
                };
            }
        }

        private void HandleGroupDrag(DesktopIcon leader, Vector2 delta) {
            if (_isHandlingGroupDrag) return;
            _isHandlingGroupDrag = true;
            try {
                if (DragDropManager.Instance.GetStoredPositions().Count == 0) {
                    foreach (var child in Children) {
                        if (child is DesktopIcon icon && icon.IsSelected) {
                            DragDropManager.Instance.StoreIconPosition(icon, icon.Position);
                        }
                    }
                }
                // Don't actually move icons - just trigger their OnDragAction for drop preview updates
                foreach (var child in Children) {
                    if (child is DesktopIcon icon && icon != leader && icon.IsSelected) {
                        // Trigger the drag action which will update drop preview
                        icon.OnDragAction?.Invoke(icon, delta);
                    }
                }
            } finally {
                _isHandlingGroupDrag = false;
            }
        }

        protected override void UpdateInput() {
            bool alreadyConsumed = InputManager.IsMouseConsumed;
            base.UpdateInput();
            
            bool isHovered = IsMouseOver; // This is set correctly by base using UIManager.IsHovered

            if (!alreadyConsumed && isHovered && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                _isSelecting = true;
                _selectionStart = InputManager.MousePosition.ToVector2();
                _marqueeRect = Rectangle.Empty;
                Window.ActiveWindow = null;
                foreach (var child in Children) if (child is DesktopIcon icon) icon.IsSelected = false;
            }

            var currentMouse = Microsoft.Xna.Framework.Input.Mouse.GetState();
            bool justReleased = currentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Released && _wasMouseDown;
            _wasMouseDown = currentMouse.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            bool isOverDesktop = InputManager.MousePosition.X >= 0 && InputManager.MousePosition.Y >= 0 &&
                                 InputManager.MousePosition.X <= G.GraphicsDevice.Viewport.Width &&
                                 InputManager.MousePosition.Y <= G.GraphicsDevice.Viewport.Height;

            bool overTrash = TrashIcon != null && TrashIcon.Bounds.Contains(InputManager.MousePosition);
            bool draggingItself = Shell.Drag.DraggedItem == TrashIcon || (Shell.Drag.DraggedItem is System.Collections.Generic.List<string> list && list.Count == 1 && list[0] == TrashIcon.VirtualPath);

            if ((!alreadyConsumed || (overTrash && !draggingItself)) && isOverDesktop && justReleased && Shell.Drag.DraggedItem != null) {
                HandleDesktopDrop(Shell.Drag.DraggedItem);
            }

            if (justReleased && Shell.Drag.DraggedItem != null && !alreadyConsumed) {
                // Clicking/dropping on empty space - cancel drag but don't reset deltas yet
                // The icons themselves (or their drop handlers) will reset deltas.
                DragDropManager.Instance.CancelDrag();
            }

            if (!alreadyConsumed && isHovered && InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
                // Centralized context menu logic in UIElement already triggered ContextMenuManager.Show
                // We just consume the input and handled = true in PopulateContextMenu if we don't want bubbling.
                InputManager.IsMouseConsumed = true;
            }

            if (_isSelecting) {
                if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
                    // Once selection is active, we continue updating it even if hit an icon (alreadyConsumed)
                    // This prevents icons from breaking a selection drag that's already in progress.
                    var currentMouseVec = InputManager.MousePosition.ToVector2();
                    float x = Math.Min(_selectionStart.X, currentMouseVec.X);
                    float y = Math.Min(_selectionStart.Y, currentMouseVec.Y);
                    float w = Math.Abs(_selectionStart.X - currentMouseVec.X);
                    float h = Math.Abs(_selectionStart.Y - currentMouseVec.Y);
                    var rawRect = new Rectangle((int)x, (int)y, (int)w, (int)h);
                    _marqueeRect = Rectangle.Intersect(rawRect, Bounds);
                    foreach (var child in Children) if (child is DesktopIcon icon) icon.IsSelected = _marqueeRect.Intersects(icon.Bounds);
                    
                    InputManager.IsMouseConsumed = true;
                } else {
                    _isSelecting = false;
                    _marqueeRect = Rectangle.Empty;
                }
            }
        }

        public override void PopulateContextMenu(ContextMenuContext context, System.Collections.Generic.List<MenuItem> items) {
            items.Add(new MenuItem { 
                Text = "Refresh", 
                Priority = 100,
                Action = RefreshAction 
            });

            var sortMenu = new System.Collections.Generic.List<MenuItem> {
                new MenuItem { Text = "Name", Action = () => SortAction?.Invoke("Name") },
                new MenuItem { Text = "Type", Action = () => SortAction?.Invoke("Type") },
                new MenuItem { Text = "Size", Action = () => SortAction?.Invoke("Size") }
            };

            items.Add(new MenuItem { 
                Text = "Sort by", 
                Priority = 90,
                SubItems = sortMenu 
            });

            var newMenu = new System.Collections.Generic.List<MenuItem> {
                new MenuItem {
                    Text = "Folder", 
                    Action = () => CreateNewDesktopItem("New Folder", "", context.Position, true)
                },
                new MenuItem {
                    Text = "Text Document", 
                    Action = () => CreateNewDesktopItem("New Text Document", ".txt", context.Position, false)
                }
            };

            items.Add(new MenuItem { 
                Text = "New", 
                Priority = 80,
                SubItems = newMenu 
            });

            items.Add(new MenuItem { Type = MenuItemType.Separator, Priority = 70 });

            items.Add(new MenuItem { 
                Text = "Align Icons to Grid",
                Type = MenuItemType.Checkbox,
                IsChecked = _scene._alignToGrid,
                Priority = 60,
                Action = () => {
                    _scene._alignToGrid = !_scene._alignToGrid;
                    TheGame.Core.OS.Registry.Instance.SetValue($"{Shell.Registry.Desktop}\\AlignToGrid", _scene._alignToGrid);
                    if (_scene._alignToGrid) _scene.ArrangeIconsToGrid();
                }
            });
        }

        private void CreateNewDesktopItem(string defaultName, string extension, Vector2 clickPos, bool isDirectory) {
            try {
                string desktopPath = "C:\\Users\\Admin\\Desktop\\";
                string fileName = defaultName + extension;
                string path = System.IO.Path.Combine(desktopPath, fileName);
                
                int i = 1;
                while (VirtualFileSystem.Instance.Exists(path)) {
                    path = System.IO.Path.Combine(desktopPath, $"{defaultName} ({i++}){extension}");
                }

                // Find best position
                Vector2 pos = FindFreeDesktopSpot(clickPos);

                if (isDirectory) VirtualFileSystem.Instance.CreateDirectory(path);
                else VirtualFileSystem.Instance.WriteAllText(path, "");

                _scene._iconPositions[path] = pos;
                _scene.SaveIconPosition(path, pos);
                RefreshAction?.Invoke();
            } catch (Exception ex) {
                DebugLogger.Log($"Error creating new desktop item: {ex.Message}");
            }
        }

        private Vector2 FindFreeDesktopSpot(Vector2? hintPos, HashSet<(int x, int y)> localOccupied = null) {
            Vector2 startPos = hintPos ?? new Vector2(DesktopPadding, DesktopPadding);
            if (_scene._alignToGrid) {
                Vector2 snapped = _scene.SnapToGrid(startPos);
                var occupied = _scene.GetOccupiedCells();
                
                // Merge with local occupied set if provided (for batch placement)
                if (localOccupied != null) {
                    foreach (var cell in localOccupied) occupied.Add(cell);
                }

                var nearest = _scene.FindNearestAvailableCell((int)((snapped.X - DesktopPadding) / GridCellWidth), (int)((snapped.Y - DesktopPadding) / GridCellHeight), occupied);
                
                // CRITICAL: Claim the cell locally so the next item in the batch doesn't take it
                localOccupied?.Add((nearest.x, nearest.y));

                return new Vector2(nearest.x * GridCellWidth + DesktopPadding, nearest.y * GridCellHeight + DesktopPadding);
            }
            return startPos;
        }

        private void HandleDesktopDrop(object item) {
            // Handle IDraggable (like items from Browser)
            object dragData = item;
            if (item is IDraggable draggable) {
                dragData = draggable.GetDragData();
            }

            if (dragData == null) return;

            if (TrashIcon != null && TrashIcon.Bounds.Contains(InputManager.MousePosition)) {
                if (dragData is string path) VirtualFileSystem.Instance.Recycle(path);
                else if (dragData is List<string> list) foreach (var p in list) VirtualFileSystem.Instance.Recycle(p);
                RefreshAction?.Invoke();
                Shell.RefreshExplorers();
                Shell.Drag.DraggedItem = null;
                return;
            }
            string desktopPath = "C:\\Users\\Admin\\Desktop\\";
            bool changed = false;
            Vector2 dropPos = InputManager.MousePosition.ToVector2() - DragDropManager.Instance.DragGrabOffset;
            
            // If dragging a DesktopIcon from inside this scene, handled by its own OnDropAction
            // We check 'item' here because we don't want to handle internal DesktopIcon drags twice
            if (item is DesktopIcon) { return; }
            
            if (dragData is string itemPath) {
                string newPath = MoveToDesktop(itemPath, desktopPath);
                if (!string.IsNullOrEmpty(newPath)) {
                    Vector2 finalPos = dropPos;
                    if (_scene._alignToGrid) {
                        Vector2 snapped = _scene.SnapToGrid(dropPos);
                        var occupied = _scene.GetOccupiedCells();
                        var nearest = _scene.FindNearestAvailableCell((int)((snapped.X - DesktopPadding) / GridCellWidth), (int)((snapped.Y - DesktopPadding) / GridCellHeight), occupied);
                        finalPos = new Vector2(nearest.x * GridCellWidth + DesktopPadding, nearest.y * GridCellHeight + DesktopPadding);
                    }
                    _scene._iconPositions[newPath] = finalPos;
                    _scene.SaveIconPosition(newPath, finalPos);
                }
                changed = true;
            } else if (dragData is List<string> list) {
                var occupiedCells = _scene._alignToGrid ? _scene.GetOccupiedCells() : null;
                foreach (string path in list) {
                    string newPath = MoveToDesktop(path, desktopPath);
                    if (!string.IsNullOrEmpty(newPath)) {
                        Vector2 finalPos = dropPos;
                        if (_scene._alignToGrid) {
                            Vector2 snapped = _scene.SnapToGrid(dropPos);
                            var nearest = _scene.FindNearestAvailableCell((int)((snapped.X - DesktopPadding) / GridCellWidth), (int)((snapped.Y - DesktopPadding) / GridCellHeight), occupiedCells);
                            finalPos = new Vector2(nearest.x * GridCellWidth + DesktopPadding, nearest.y * GridCellHeight + DesktopPadding);
                            occupiedCells.Add((nearest.x, nearest.y));
                        }
                        _scene._iconPositions[newPath] = finalPos;
                        _scene.SaveIconPosition(newPath, finalPos);
                        
                        if (!_scene._alignToGrid) dropPos += new Vector2(20, 20);
                    }
                }
                changed = true;
            }
            if (changed) { RefreshAction?.Invoke(); Shell.RefreshExplorers(); Shell.Drag.DraggedItem = null; }
            InputManager.IsMouseConsumed = true;
        }

        private string MoveToDesktop(string sourcePath, string desktopPath) {
            if (string.IsNullOrEmpty(sourcePath)) return null;
            string sourceDir = System.IO.Path.GetDirectoryName(sourcePath.TrimEnd('\\', '/'));
            if (!string.IsNullOrEmpty(sourceDir)) {
                sourceDir = sourceDir.Replace('/', '\\').TrimEnd('\\') + "\\";
                if (sourceDir.ToUpper() == desktopPath.ToUpper()) return null;
            }
            string fileName = System.IO.Path.GetFileName(sourcePath.TrimEnd('\\', '/'));
            string destPath = System.IO.Path.Combine(desktopPath, fileName);
            if (sourcePath.ToUpper() == destPath.ToUpper()) return null;
            
            // Don't move the $Recycle.Bin folder itself (but allow moving items inside it)
            string normalizedSource = sourcePath.Replace('/', '\\').TrimEnd('\\').ToUpper();
            if (normalizedSource == "C:\\$RECYCLE.BIN") {
                return null;
            }
            
            if (VirtualFileSystem.Instance.Exists(destPath)) {
                string name = System.IO.Path.GetFileNameWithoutExtension(fileName);
                string ext = System.IO.Path.GetExtension(fileName);
                int i = 1;
                while (VirtualFileSystem.Instance.Exists(destPath)) destPath = System.IO.Path.Combine(desktopPath, $"{name} ({i++}){ext}");
            }
            VirtualFileSystem.Instance.Move(sourcePath, destPath);
            return destPath;
        }

        public override void Draw(SpriteBatch spriteBatch, ShapeBatch shapeBatch) {
            if (!IsVisible) return;
            DrawSelf(spriteBatch, shapeBatch);
            foreach (var child in Children) {
                if (!child.IsVisible) continue;
                child.Draw(spriteBatch, shapeBatch);
            }
            if (_isSelecting && _marqueeRect != Rectangle.Empty) {
                shapeBatch.FillRectangle(_marqueeRect.Location.ToVector2(), _marqueeRect.Size.ToVector2(), new Color(0, 102, 204, 50));
                shapeBatch.BorderRectangle(_marqueeRect.Location.ToVector2(), _marqueeRect.Size.ToVector2(), new Color(0, 102, 204, 150), 1f);
            }
        }
    }
}
