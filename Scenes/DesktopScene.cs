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
using System.Linq;

namespace TheGame.Scenes;

public class DesktopScene : Core.Scenes.Scene {
    private const int TaskbarHeight = 40;
    private const int StartMenuWidth = 250;
    private const int StartMenuHeight = 350;

    private UIManager _uiManager;
    private DesktopPanel _desktopLayer;
    private BlurredWindowLayerPanel _windowLayer;
    private StartMenu _startMenu;
    private Taskbar _taskbar;
    private ContextMenu _contextMenu;
    private DesktopIcon _trashIconEl;

    private Dictionary<string, Vector2> _iconPositions = new Dictionary<string, Vector2>();
    private string _sortType = "None";

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

        var viewport = G.GraphicsDevice.Viewport;
        var screenWidth = viewport.Width;
        var screenHeight = viewport.Height;

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

        // 3. Taskbar Layer (Top of Windows)
        _startMenu = new StartMenu(new Vector2(0, screenHeight - TaskbarHeight - StartMenuHeight), new Vector2(StartMenuWidth, StartMenuHeight));
        _uiManager.AddElement(_startMenu);

        // Initialize Shell
        Shell.Initialize(_windowLayer, _contextMenu);
        Shell.OnAddOverlayElement = (el) => _uiManager.AddElement(el);

        // Initialize Notification System
        _notificationPanel = new NotificationHistoryPanel();
        _uiManager.AddElement(_notificationPanel);

        _taskbar = new Taskbar(new Vector2(0, screenHeight - TaskbarHeight), new Vector2(screenWidth, TaskbarHeight), _windowLayer, _startMenu);
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
        Shell.Notifications.Show("Welcome!", "The notification system is ready.", null, null, new List<NotificationAction> {
            new NotificationAction { Label = "Got it", OnClick = () => DebugLogger.Log("User acknowledged.") }
        });

        string pcIconPath = VirtualFileSystem.Instance.ToHostPath("C:\\Windows\\SystemResources\\Icons\\PC.png");
        var pcIcon = System.IO.File.Exists(pcIconPath) ? Core.ImageLoader.Load(G.GraphicsDevice, pcIconPath) : GameContent.FileIcon;

        // Load Dynamic Desktop Icons
        LoadDesktopIcons();

        // Update Start Menu to use icons
        _startMenu.Position = new Vector2(0, screenHeight - TaskbarHeight - StartMenuHeight);
        _startMenu.ClearMenuItems();

        string notepadPath = "C:\\Users\\Admin\\Desktop\\Notepad.slnk";
        string settingsPath = "C:\\Users\\Admin\\Desktop\\Settings.slnk";
        string explorerPath = "C:\\Users\\Admin\\Desktop\\My Computer.slnk";

        _startMenu.AddMenuItem(0, "Notepad", Shell.GetIcon(notepadPath), () => {
            Shell.Execute(notepadPath);
            _startMenu.Toggle();
        });
        string calcIconPath = VirtualFileSystem.Instance.ToHostPath("C:\\Windows\\SystemResources\\Icons\\calculator.png");
        Texture2D calcIcon = System.IO.File.Exists(calcIconPath) ? Core.ImageLoader.Load(G.GraphicsDevice, calcIconPath) : GameContent.FileIcon;

        _startMenu.AddMenuItem(1, "Calculator", calcIcon, () => {
            DebugLogger.Log("Opening Calculator...");
            _startMenu.Toggle();
        });
        _startMenu.AddMenuItem(2, "Settings", Shell.GetIcon(settingsPath), () => {
            Shell.Execute(settingsPath);
            _startMenu.Toggle();
        });
        _startMenu.AddMenuItem(3, "File Explorer", Shell.GetIcon(explorerPath), () => {
            Shell.Execute(explorerPath);
            _startMenu.Toggle();
        });
        _startMenu.AddMenuItem(4, "Shut Down", GameContent.ExplorerIcon, () => { System.Environment.Exit(0); });
    }

    private void LoadDesktopIcons() {
        foreach (var child in _desktopLayer.Children.ToArray()) _desktopLayer.RemoveChild(child);

        string desktopPath = "C:\\Users\\Admin\\Desktop\\";
        var files = Core.OS.VirtualFileSystem.Instance.GetFiles(desktopPath);
        var dirs = Core.OS.VirtualFileSystem.Instance.GetDirectories(desktopPath);

        // Combine files and all folders for desktop display
        var itemsList = files.Concat(dirs).Where(i => !i.Contains("$Recycle.Bin"));
        if (_sortType == "Name") itemsList = itemsList.OrderBy(System.IO.Path.GetFileName);
        else if (_sortType == "Type") itemsList = itemsList.OrderBy(System.IO.Path.GetExtension);
        else if (_sortType == "Size") itemsList = itemsList.OrderBy(f => { try { return new System.IO.FileInfo(VirtualFileSystem.Instance.ToHostPath(f)).Length; } catch { return 0; } });

        var items = itemsList.ToArray();

        float x = 20;
        float y = 20;
        float gap = 20;

        foreach (var item in items) {
            string fileName = System.IO.Path.GetFileName(item);
            if (fileName.ToLower().EndsWith(".slnk") || fileName.ToLower().EndsWith(".sapp")) {
                fileName = System.IO.Path.GetFileNameWithoutExtension(item);
            }
            Texture2D iconTex = Shell.GetIcon(item);

            var icon = new DesktopIcon(Vector2.Zero, fileName, iconTex);

            if (_iconPositions.ContainsKey(item)) {
                icon.Position = _iconPositions[item];
            } else {
                icon.Position = new Vector2(x, y);
                _iconPositions[item] = icon.Position;
            }

            y += icon.Size.Y + gap;
            if (y + icon.Size.Y > G.GraphicsDevice.Viewport.Height - 150) {
                y = 20;
                x += icon.Size.X + gap;
            }

            icon.VirtualPath = item;
            icon.OnDragAction = (i, delta) => { _iconPositions[i.VirtualPath] = i.Position; };
            icon.OnSelectedAction = (selected) => {
                foreach (var child in _desktopLayer.Children) {
                    if (child is DesktopIcon d && d != selected) d.IsSelected = false;
                }
            };
            icon.OnDoubleClickAction = () => Shell.Execute(item, icon.Bounds);
            icon.OnDropAction = () => {
                if (_trashIconEl != null && _trashIconEl.Bounds.Intersects(icon.Bounds)) {
                    if (icon.IsSelected) {
                        var selectedData = _desktopLayer.Children.OfType<DesktopIcon>()
                            .Where(i => i.IsSelected && !string.IsNullOrEmpty(i.VirtualPath))
                            .Select(i => i.VirtualPath)
                            .ToList();

                        foreach (var path in selectedData) VirtualFileSystem.Instance.Recycle(path);
                    } else {
                        VirtualFileSystem.Instance.Recycle(icon.VirtualPath);
                    }

                    LoadDesktopIcons();
                    Shell.RefreshExplorers("$Recycle.Bin");
                }
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
            icon.OnRenamed = () => {
                LoadDesktopIcons();
                Shell.RefreshExplorers();
            };
            _desktopLayer.AddChild(icon);
        }

        // Trash Can
        string trashPath = "C:\\$Recycle.Bin\\";
        Vector2 trashPos = new Vector2(x, y);
        if (_iconPositions.ContainsKey(trashPath)) {
            trashPos = _iconPositions[trashPath];
        } else {
            _iconPositions[trashPath] = trashPos;
        }

        var trashIcon = VirtualFileSystem.Instance.IsRecycleBinEmpty() ? GameContent.TrashEmptyIcon : GameContent.TrashFullIcon;

        _trashIconEl = new DesktopIcon(trashPos, "Recycle Bin", trashIcon);
        _trashIconEl.VirtualPath = trashPath;
        _trashIconEl.OnDragAction = (i, delta) => { _iconPositions[i.VirtualPath] = i.Position; };
        _trashIconEl.OnSelectedAction = (selected) => {
            foreach (var child in _desktopLayer.Children) {
                if (child is DesktopIcon d && d != selected) d.IsSelected = false;
            }
        };
        _trashIconEl.OnDoubleClickAction = () => Shell.Execute(trashPath, _trashIconEl.Bounds);
        _trashIconEl.OnDropAction = () => {
            var dragged = Shell.DraggedItem;
            if (dragged != null && dragged != _trashIconEl) {
                if (dragged is DesktopIcon di) {
                    VirtualFileSystem.Instance.Recycle(di.VirtualPath);
                } else if (dragged is string s) {
                    VirtualFileSystem.Instance.Recycle(s);
                } else if (dragged is List<string> list) {
                    foreach (var path in list) VirtualFileSystem.Instance.Recycle(path);
                }

                LoadDesktopIcons();
                Shell.RefreshExplorers();
                Shell.DraggedItem = null;
            }
        };
        _trashIconEl.OnRightClickAction = () => {
            _contextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                new MenuItem { Text = "Open", Action = () => Shell.Execute(trashPath, _trashIconEl.Bounds) },
                new MenuItem {
                    Text = "Empty Recycle Bin", Action = () => Shell.PromptEmptyRecycleBin()
                },
                new MenuItem {
                    Text = "Restore All", Action = () => {
                        var mb = new MessageBox("Restore All", "Are you sure you want to restore all items from the Recycle Bin?", MessageBoxButtons.YesNo, (confirmed) => {
                            if (confirmed) {
                                VirtualFileSystem.Instance.RestoreAll();
                                DebugLogger.Log("All items restored from Recycle Bin.");
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
        _desktopLayer.AddChild(_trashIconEl);
        _desktopLayer.TrashIcon = _trashIconEl;
        _desktopLayer.SortAction = SortIcons;
        _desktopLayer.IconMovedAction = (i) => {
            if (!string.IsNullOrEmpty(i.VirtualPath)) _iconPositions[i.VirtualPath] = i.Position;
        };
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
        // Process hot reload on main thread
        AppHotReloadManager.Instance.Update();
        
        _uiManager.Update(gameTime);
        Core.Animation.Tweener.Update((float)gameTime.ElapsedGameTime.TotalSeconds);

        var vp = G.GraphicsDevice.Viewport;
        _windowLayer.Size = new Vector2(vp.Width, vp.Height);
        _desktopLayer.Size = _windowLayer.Size; // Sync desktop layer size
        _taskbar.Size = new Vector2(vp.Width, TaskbarHeight);
        _taskbar.Position = new Vector2(0, vp.Height - TaskbarHeight);

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
        _iconPositions.Clear();
        LoadDesktopIcons();
        Shell.RefreshExplorers(); // Refresh explorers too for consistent view
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
        public Action RefreshAction { get; set; }
        public Action<string> SortAction { get; set; }
        public Action<DesktopIcon> IconMovedAction { get; set; }
        public DesktopIcon TrashIcon { get; set; }
        public ContextMenu ContextMenu { get; set; }

        public DesktopPanel(DesktopScene scene, Vector2 pos, Vector2 size) : base(pos, size) {
            _scene = scene;
            BackgroundColor = Color.Transparent;
            BorderThickness = 0;
            ConsumesInput = false;
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
            if (DragDropManager.Instance.GetStoredPositions().Count == 0) {
                foreach (var child in Children) {
                    if (child is DesktopIcon icon && icon.IsSelected) {
                        DragDropManager.Instance.StoreIconPosition(icon, icon.Position);
                    }
                }
            }
            foreach (var child in Children) {
                if (child is DesktopIcon icon && icon != leader && icon.IsSelected) {
                    icon.Position += delta;
                    IconMovedAction?.Invoke(icon);
                }
            }
        }

        protected override void UpdateInput() {
            bool alreadyConsumed = InputManager.IsMouseConsumed;
            bool isHovered = InputManager.IsMouseHovering(Bounds);

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
            bool draggingItself = Shell.DraggedItem == TrashIcon || (Shell.DraggedItem is System.Collections.Generic.List<string> list && list.Count == 1 && list[0] == TrashIcon.VirtualPath);

            if ((!alreadyConsumed || (overTrash && !draggingItself)) && isOverDesktop && justReleased && Shell.DraggedItem != null) {
                HandleDesktopDrop(Shell.DraggedItem);
            }

            if (justReleased && Shell.DraggedItem != null && !alreadyConsumed) {
                DragDropManager.Instance.CancelDrag();
                foreach (var kvp in DragDropManager.Instance.GetStoredPositions()) IconMovedAction?.Invoke(kvp.Key);
            }

            if (!alreadyConsumed && isHovered && InputManager.IsMouseButtonJustPressed(MouseButton.Right)) {
                if (ContextMenu != null) {
                    ContextMenu.Show(InputManager.MousePosition.ToVector2(), new List<MenuItem> {
                        new MenuItem { Text = "Refresh", Action = RefreshAction },
                        new MenuItem {
                            Text = "Sort by",
                            SubItems = new List<MenuItem> {
                                new MenuItem { Text = "Name", Action = () => SortAction?.Invoke("Name") },
                                new MenuItem { Text = "Type", Action = () => SortAction?.Invoke("Type") },
                                new MenuItem { Text = "Size", Action = () => SortAction?.Invoke("Size") }
                            }
                        },
                        new MenuItem {
                            Text = "New Folder", Action = () => {
                                try {
                                    string desktopPath = "C:\\Users\\Admin\\Desktop\\";
                                    string path = System.IO.Path.Combine(desktopPath, "New Folder");
                                    int i = 1;
                                    while (VirtualFileSystem.Instance.Exists(path)) path = System.IO.Path.Combine(desktopPath, $"New Folder ({i++})");
                                    VirtualFileSystem.Instance.CreateDirectory(path);
                                    RefreshAction?.Invoke();
                                } catch { }
                            }
                        },
                        new MenuItem {
                            Text = "New Text File", Action = () => {
                                string desktopPath = "C:\\Users\\Admin\\Desktop\\";
                                string path = System.IO.Path.Combine(desktopPath, "New Text Document.txt");
                                int i = 1;
                                while (VirtualFileSystem.Instance.Exists(path)) path = System.IO.Path.Combine(desktopPath, $"New Text Document ({i++}).txt");
                                VirtualFileSystem.Instance.WriteAllText(path, "");
                                RefreshAction?.Invoke();
                            }
                        }
                    });
                    InputManager.IsMouseConsumed = true;
                }
            }

            if (_isSelecting) {
                if (InputManager.IsMouseButtonDown(MouseButton.Left)) {
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
            base.UpdateInput();
        }

        private void HandleDesktopDrop(object item) {
            if (TrashIcon != null && TrashIcon.Bounds.Contains(InputManager.MousePosition)) {
                if (item is string path) VirtualFileSystem.Instance.Recycle(path);
                else if (item is List<string> list) foreach (var p in list) VirtualFileSystem.Instance.Recycle(p);
                RefreshAction?.Invoke();
                Shell.RefreshExplorers();
                Shell.DraggedItem = null;
                return;
            }
            string desktopPath = "C:\\Users\\Admin\\Desktop\\";
            bool changed = false;
            Vector2 dropPos = InputManager.MousePosition.ToVector2();
            if (item is DesktopIcon) { Shell.DraggedItem = null; return; }
            if (item is string itemPath) {
                string newPath = MoveToDesktop(itemPath, desktopPath);
                if (!string.IsNullOrEmpty(newPath)) _scene._iconPositions[newPath] = dropPos;
                changed = true;
            } else if (item is List<string> list) {
                foreach (var p in list) {
                    string newPath = MoveToDesktop(p, desktopPath);
                    if (!string.IsNullOrEmpty(newPath)) _scene._iconPositions[newPath] = dropPos;
                }
                changed = true;
            }
            if (changed) { RefreshAction?.Invoke(); Shell.RefreshExplorers(); Shell.DraggedItem = null; }
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
