using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core.Input;
using Microsoft.Xna.Framework.Input;

namespace TheGame.Core.OS;

public static class Shell {
    // Shared state
    public static Panel WindowLayer;
    public static Action RefreshDesktop;
    public static ContextMenu GlobalContextMenu;
    public static class Desktop {
        /// <summary>
        /// Gets the next free position for an icon on the desktop. 
        /// Returns Vector2.Zero if desktop is not active or no spot found.
        /// Optional localOccupied set can be provided for batch operations.
        /// </summary>
        public static Func<Vector2?, HashSet<(int x, int y)>, Vector2> GetNextFreePosition;

        /// <summary>
        /// Manually sets/saves an icon's position for a specific virtual path.
        /// </summary>
        public static Action<string, Vector2> SetIconPosition;

        /// <summary>
        /// Creates a desktop shortcut for the specified target path.
        /// </summary>
        public static void CreateShortcut(string targetPath, string label = null) {
            CreateShortcuts(new[] { targetPath });
        }

        /// <summary>
        /// Creates multiple desktop shortcuts for the specified target paths.
        /// Handles anti-overlap positioning for the entire batch.
        /// </summary>
        public static void CreateShortcuts(IEnumerable<string> targetPaths) {
            string desktopPath = "C:\\Users\\Admin\\Desktop\\";
            int createdCount = 0;
            var localOccupied = new HashSet<(int x, int y)>();

            foreach (var path in targetPaths) {
                string fileName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
                string shortcutLabel = fileName;
                
                if (fileName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
                    shortcutLabel = System.IO.Path.GetFileNameWithoutExtension(fileName);
                }

                string shortcutName = $"{shortcutLabel} - Shortcut.slnk";
                string destPath = System.IO.Path.Combine(desktopPath, shortcutName);

                int i = 1;
                while (VirtualFileSystem.Instance.Exists(destPath)) {
                    destPath = System.IO.Path.Combine(desktopPath, $"{shortcutLabel} - Shortcut ({i++}).slnk");
                }

                // Get position and track it locally for this batch
                Vector2 pos = GetNextFreePosition?.Invoke(null, localOccupied) ?? Vector2.Zero;
                
                // Note: localOccupied is now updated by GetNextFreePosition handler directly
                // to avoid fragile hardcoded grid math here.

                string json = "{\n" +
                             $"  \"targetPath\": \"{path.Replace("\\", "\\\\")}\",\n" +
                             $"  \"label\": \"{shortcutLabel}\",\n" +
                             $"  \"iconPath\": null\n" +
                             "}";

                VirtualFileSystem.Instance.WriteAllText(destPath, json);
                
                if (pos != Vector2.Zero) {
                    SetIconPosition?.Invoke(destPath, pos);
                }
                createdCount++;
            }

            if (createdCount > 0) {
                Notifications.Show("Success", $"Created {createdCount} shortcut(s) on the desktop.");
                RefreshDesktop?.Invoke();
            }
        }
    }

    public static class StartMenu {
        public static void CreateShortcuts(IEnumerable<string> targetPaths) {
            string startMenuPath = "C:\\Users\\Admin\\Start Menu\\";

            foreach (var path in targetPaths) {
                string fileName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
                string shortcutLabel = fileName;
                
                if (fileName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
                    shortcutLabel = System.IO.Path.GetFileNameWithoutExtension(fileName);
                }
            
                string shortcutName = $"{shortcutLabel}.slnk";
                string menuPath = System.IO.Path.Combine(startMenuPath, shortcutName);
            
                int i = 1;
                while (VirtualFileSystem.Instance.Exists(menuPath)) {
                    menuPath = System.IO.Path.Combine(menuPath, $"{shortcutLabel} ({i++}).slnk");
                }
            
                string json = "{\n" +
                              $"  \"targetPath\": \"{path.Replace("\\", "\\\\")}\",\n" +
                              $"  \"label\": \"{shortcutLabel}\",\n" +
                              $"  \"iconPath\": null\n" +
                              "}";
            
                VirtualFileSystem.Instance.WriteAllText(menuPath, json);
            }
        }
    }
    
    /// <summary>
    /// Registry path constants for common system locations.
    /// </summary>
    public static class Registry {
        public const string HKLM = "HKLM";
        public const string HKCU = "HKCU";
        
        /// <summary>Path for file type associations: HKLM\Software\FileAssociations</summary>
        public const string FileAssociations = "HKLM\\Software\\FileAssociations";
        
        /// <summary>Path for startup apps: HKLM\Software\Startup</summary>
        public const string Startup = "HKLM\\Software\\Startup";
        
        /// <summary>Path for desktop settings: HKCU\Desktop</summary>
        public const string Desktop = "HKCU\\Desktop";
        
        /// <summary>Path for per-app settings: HKCU\Software\{AppId}</summary>
        public static string AppSettings(string appId) => $"HKCU\\Software\\{appId}";

        internal static void Initialize() { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T GetSetting<T>(string key, T defaultValue = default, string appIdOverride = null) {
            string appId = appIdOverride ?? AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return defaultValue;
            return TheGame.Core.OS.Registry.GetValue($"HKCU\\Software\\{appId}\\{key}", defaultValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetSetting<T>(string key, T value, string appIdOverride = null) {
            string appId = appIdOverride ?? AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return;
            TheGame.Core.OS.Registry.SetValue($"HKCU\\Software\\{appId}\\{key}", value);
        }
    }
    
    /// <summary>
    /// System tray icon management.
    /// </summary>
    public static class SystemTray {
        private static TheGame.Core.UI.SystemTray _systemTray;
        
        /// <summary>
        /// Connects the Shell.SystemTray API to the actual SystemTray instance.
        /// Called by Taskbar during initialization.
        /// </summary>
        internal static void Initialize(TheGame.Core.UI.SystemTray systemTray) {
            _systemTray = systemTray;
        }
        
        /// <summary>
        /// Adds a tray icon owned by a window.
        /// The icon will be automatically removed when the window closes (unless PersistAfterWindowClose is true).
        /// </summary>
        public static string AddIcon(TheGame.Core.UI.Window ownerWindow, TheGame.Core.UI.TrayIcon icon) {
            if (icon == null || _systemTray == null) return null;
            if (ownerWindow == null) {
                DebugLogger.Log("[Shell.SystemTray] AddIcon called with null window");
                return null;
            }
            
            icon.OwnerWindow = ownerWindow;
            icon.OwnerProcess = ownerWindow.OwnerProcess;
            
            // Hook window close event to remove icon (unless PersistAfterWindowClose)
            ownerWindow.OnClosed += () => _systemTray?.RemoveIconsForWindow(ownerWindow);
            
            _systemTray.AddIcon(icon);
            return icon.Id;
        }
        
        /// <summary>
        /// Adds a tray icon owned by a process (for background services without windows).
        /// The icon will be automatically removed when the process terminates.
        /// </summary>
        public static string AddIcon(TheGame.Core.OS.Process ownerProcess, TheGame.Core.UI.TrayIcon icon) {
            if (icon == null || _systemTray == null) return null;
            if (ownerProcess == null) {
                DebugLogger.Log("[Shell.SystemTray] AddIcon called with null process");
                return null;
            }
            
            icon.OwnerProcess = ownerProcess;
            
            _systemTray.AddIcon(icon);
            return icon.Id;
        }
        
        /// <summary>
        /// Removes a tray icon by ID.
        /// </summary>
        public static bool RemoveIcon(string id) {
            return _systemTray?.RemoveIcon(id) ?? false;
        }
        
        /// <summary>
        /// Removes all tray icons owned by the specified process.
        /// Called automatically when a process terminates.
        /// </summary>
        internal static void RemoveIconsForProcess(TheGame.Core.OS.Process process) {
            _systemTray?.RemoveIconsForProcess(process);
        }
        
        /// <summary>
        /// Gets a tray icon by ID for dynamic updates.
        /// </summary>
        public static TheGame.Core.UI.TrayIcon GetIcon(string id) {
            return _systemTray?.GetIcon(id);
        }
        
        /// <summary>
        /// Updates the icon texture for a tray icon.
        /// </summary>
        public static void UpdateIcon(string id, Texture2D newIcon) {
            _systemTray?.GetIcon(id)?.SetIcon(newIcon);
        }
        
        /// <summary>
        /// Updates the tooltip for a tray icon.
        /// </summary>
        public static void UpdateTooltip(string id, string newTooltip) {
            _systemTray?.GetIcon(id)?.SetTooltip(newTooltip);
        }
    }
    
    public static Action<UIElement> OnAddOverlayElement;
    public static bool IsRenderingDrag = false;

    public static void AddOverlayElement(UIElement element) => OnAddOverlayElement?.Invoke(element);
    public static void DrawDrag(SpriteBatch sb, ShapeBatch sbatch) => DragDropManager.Instance.DrawDragVisual(sb, sbatch);

    /// <summary>
    /// Image and texture management.
    /// </summary>
    public static class Images {
        /// <summary>
        /// Loads a texture from an app-relative path (inside the .sapp folder).
        /// Automatically detects the calling application.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Texture2D LoadAppImage(string fileName) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return null;
            string virtualPath = VirtualFileSystem.Instance.GetAppResourcePath(appId, fileName);
            if (virtualPath == null) return null;
            return ImageLoader.Load(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(virtualPath));
        }

        /// <summary>
        /// Loads a texture from a virtual path (e.g. C:\Windows\SystemResources\Icons\PC.png).
        /// </summary>
        public static Texture2D Load(string virtualPath) {
            if (string.IsNullOrEmpty(virtualPath)) return null;
            return ImageLoader.Load(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(virtualPath));
        }
    }

    public static void Update(GameTime gameTime) {
        // Update all running processes
        ProcessManager.Instance.Update(gameTime);
        
        // Global watchdog: If mouse is released but drag is still active, it means the drop wasn't handled.
        // We use !IsMouseButtonDown instead of IsJustReleased to catch it even if consumed.
        if (DragDropManager.Instance.IsActive && !InputManager.IsMouseButtonDown(MouseButton.Left)) {
            DragDropManager.Instance.CancelDrag();
        }
    }

    // Drag and Drop API
    /// <summary>
    /// Checks if a drag operation is currently active.
    /// </summary>
    public static bool IsDragging => DragDropManager.Instance.IsActive;

    /// <summary>
    /// Gets or sets the currently dragged item. Setting to null ends the drag.
    /// Note: For proper snap-back behavior, use BeginDrag() with source position.
    /// </summary>
    public static object DraggedItem {
        get => DragDropManager.Instance.DragData;
        set {
            if (value != null) DragDropManager.Instance.BeginDrag(value, Vector2.Zero, Vector2.Zero);
            else DragDropManager.Instance.EndDrag();
        }
    }

    /// <summary>
    /// Begins a drag operation with source position for snap-back.
    /// </summary>
    public static void BeginDrag(object data, Vector2 sourcePosition, Vector2 grabOffset) 
        => DragDropManager.Instance.BeginDrag(data, sourcePosition, grabOffset);

    /// <summary>
    /// Ends the current drag operation (successful drop).
    /// </summary>
    public static void EndDrag() 
        => DragDropManager.Instance.EndDrag();

    /// <summary>
    /// Cancels the drag and restores original positions.
    /// </summary>
    public static void CancelDrag() 
        => DragDropManager.Instance.CancelDrag();

    /// <summary>
    /// Checks if a specific item is currently being dragged.
    /// </summary>
    public static bool IsItemBeingDragged(object item) 
        => DragDropManager.Instance.IsItemDragged(item);

    /// <summary>
    /// Drag visual customization API
    /// </summary>
    public static class Drag {
        /// <summary>
        /// Sets the opacity for drag visuals (0.1 to 1.0).
        /// </summary>
        public static void SetVisualOpacity(float opacity) 
            => DragDropManager.Instance.SetDragOpacity(opacity);
        
        /// <summary>
        /// Sets whether to show item count badge for multi-item drags.
        /// </summary>
        public static void SetShowItemCount(bool show) 
            => DragDropManager.Instance.SetShowItemCount(show);
        
        /// <summary>
        /// Sets the position where drop preview should be rendered.
        /// Set to null to hide the preview.
        /// </summary>
        public static void SetDropPreview(object id, Vector2? position)
            => DragDropManager.Instance.SetDropPreviewPosition(id, position);
    }

    /// <summary>
    /// Process API for apps to interact with their own process.
    /// </summary>
    public static class Process {
        /// <summary>
        /// Gets the calling app's process (detected from the active window).
        /// </summary>
        public static TheGame.Core.OS.Process Current => Window.ActiveWindow?.OwnerProcess;
        
        /// <summary>
        /// Gets the ProcessManager for advanced process control.
        /// </summary>
        public static ProcessManager Manager => ProcessManager.Instance;
        
        /// <summary>
        /// Creates a new window of the specified type owned by the current process.
        /// </summary>
        public static T CreateWindow<T>() where T : Window, new() {
            var process = Current;
            if (process == null) {
                DebugLogger.Log("Shell.Process.CreateWindow: No active process");
                return null;
            }
            return process.CreateWindow<T>();
        }
        
        /// <summary>
        /// Shows a modal dialog that blocks input to the current window.
        /// </summary>
        public static void ShowModal(Window dialog) {
            var process = Current;
            if (process == null) {
                UI.OpenWindow(dialog);
                return;
            }
            // Use MainWindow as parent, not ActiveWindow, to avoid confusing behavior
            // when secondary windows are active
            process.ShowModal(dialog, process.MainWindow);
        }
        
        /// <summary>
        /// Closes all windows and enters background mode.
        /// The process continues receiving OnUpdate calls.
        /// </summary>
        public static void GoToBackground() {
            Current?.GoToBackground();
        }
        
        /// <summary>
        /// Terminates the current process.
        /// </summary>
        public static void Exit() {
            Current?.Terminate();
        }
        
        /// <summary>
        /// Gets all running processes.
        /// </summary>
        public static IEnumerable<OS.Process> GetAll() => ProcessManager.Instance.GetAllProcesses();
    }


    // Hot Reload Control
    public static bool HotReloadEnabled {
        get => AppHotReloadManager.Instance.Enabled;
        set => AppHotReloadManager.Instance.Enabled = value;
    }


    public static void CloseExplorers(string pathFilter) {
        if (WindowLayer == null || string.IsNullOrEmpty(pathFilter)) return;
        string filter = pathFilter.ToUpper().TrimEnd('\\') + "\\";
        foreach (var child in WindowLayer.Children.ToArray()) {
            if (child is Window win && win.AppId == "EXPLORER") {
                try {
                    dynamic explorer = win;
                    string current = ((string)explorer.CurrentPath).ToUpper().TrimEnd('\\') + "\\";
                    if (current.StartsWith(filter)) win.Close();
                } catch { }
            }
        }
    }

    public static void PromptEmptyRecycleBin() {
        var mb = new TheGame.Core.UI.MessageBox("Empty Recycle Bin", 
            "Are you sure you want to permanently delete all items in the Recycle Bin?", 
            MessageBoxButtons.YesNo, (confirmed) => {
            if (confirmed) {
                VirtualFileSystem.Instance.EmptyRecycleBin();
                Audio.PlaySound("C:\\Windows\\Media\\trash_empty.wav");
                RefreshDesktop?.Invoke();
                RefreshExplorers();
            }
        });
        UI.OpenWindow(mb);
    }

    public static Texture2D GetIcon(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return GameContent.FileIcon;
        if (virtualPath.ToUpper().Contains("$RECYCLE.BIN")) return VirtualFileSystem.Instance.IsRecycleBinEmpty() ? GameContent.TrashEmptyIcon : GameContent.TrashFullIcon;
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath) && !virtualPath.ToLower().EndsWith(".sapp")) return GameContent.FolderIcon;
        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();
        var handler = UI.GetFileHandler(ext);
        return handler?.GetIcon(virtualPath) ?? GameContent.FileIcon;
    }

    public static void Initialize(Panel windowLayer, ContextMenu contextMenu) {
        WindowLayer = windowLayer;
        GlobalContextMenu = contextMenu;
        Registry.Initialize();
        UI.InternalInitialize();
    }

    /// <summary>
    /// System-wide and per-app hotkeys.
    /// </summary>
    public static class Hotkeys {
        public static void RegisterGlobal(Keys key, HotkeyModifiers mods, Action callback) {
            HotkeyManager.RegisterGlobal(new Hotkey(key, mods), callback);
        }

        public static void RegisterLocal(Keys key, HotkeyModifiers mods, Action callback) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return;
            HotkeyManager.RegisterLocal(appId, new Hotkey(key, mods), callback);
        }

        public static void RegisterLocal(string shortcut, Action callback) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return;
            HotkeyManager.RegisterLocal(appId, Hotkey.Parse(shortcut), callback);
        }
    }

    public static class File {
        public static void RegisterFileTypeHandler(string extension, string appId = null) {
            // Auto-detect appId from calling assembly if not provided
            if (string.IsNullOrEmpty(appId)) {
                var callingAssembly = Assembly.GetCallingAssembly();
                appId = AppLoader.Instance.GetAppIdFromAssembly(callingAssembly);
                if (string.IsNullOrEmpty(appId)) {
                    DebugLogger.Log($"RegisterFileTypeHandler: Could not detect AppId from calling assembly");
                    return;
                }
                DebugLogger.Log($"RegisterFileTypeHandler: Auto-detected AppId: {appId}");
            }
            
            DebugLogger.Log($"Registering file type handler for {extension} -> {appId}");
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(appId)) return;
            extension = extension.ToLower();
            if (!extension.StartsWith(".")) extension = "." + extension;
            TheGame.Core.OS.Registry.SetValue($"{Shell.Registry.FileAssociations}\\{extension}", appId);
        }

        public static string GetFileTypeHandler(string extension) {
            if (string.IsNullOrEmpty(extension)) return null;
            extension = extension.ToLower();
            if (!extension.StartsWith(".")) extension = "." + extension;
            return TheGame.Core.OS.Registry.GetValue<string>($"{Shell.Registry.FileAssociations}\\{extension}", null);
        }
    }

    public static void RefreshExplorers(string pathFilter = null) {
        if (WindowLayer == null) return;
        foreach (var child in WindowLayer.Children) {
            if (child is Window win && win.AppId == "EXPLORER") {
                try {
                    dynamic explorer = win;
                    string currentPath = explorer.CurrentPath;
                    if (string.IsNullOrEmpty(pathFilter) || currentPath.ToUpper().Contains(pathFilter.ToUpper())) {
                        explorer.RefreshList();
                    }
                } catch (Exception ex) {
                    DebugLogger.Log($"Error refreshing explorer: {ex.Message}");
                }
            }
        }
    }

    public static void Execute(string virtualPath, Rectangle? startBounds = null) {
        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();
        DebugLogger.Log($"Shell.Execute: {virtualPath}, extension: {ext}");
        
        // Handle system file types (.sapp, .slnk) through built-in handlers first
        // These should never go through file associations
        if (ext == ".sapp" || ext == ".slnk") {
            var handler = UI.GetFileHandler(ext);
            if (handler != null) {
                handler.Execute(virtualPath, startBounds);
                return;
            }
        }
        
        // Check registry for user file type associations
        string appId = File.GetFileTypeHandler(ext);
        DebugLogger.Log($"File.GetFileTypeHandler({ext}) returned: {appId ?? "null"}");
        if (!string.IsNullOrEmpty(appId)) {
            var win = UI.CreateAppWindow(appId, virtualPath);
            if (win != null) {
                UI.OpenWindow(win, startBounds);
                return;
            }
        }
        
        // Fall back to other hardcoded handlers
        var handler2 = UI.GetFileHandler(ext);
        if (handler2 != null) {
            handler2.Execute(virtualPath, startBounds);
            return;
        }

        if (VirtualFileSystem.Instance.IsDirectory(virtualPath)) {
            var win = UI.CreateAppWindow("EXPLORER");
            if (win != null) {
                try { dynamic dwin = win; dwin.NavigateTo(virtualPath); } catch { }
                UI.OpenWindow(win, startBounds);
            }
            return;
        }
        DebugLogger.Log($"No handler for {ext}. Path: {virtualPath}");
    }

    // --- Nested API Classes ---

    public static class UI {
        private static Dictionary<string, Func<string[], Window>> _appRegistry = new();
        private static Dictionary<string, FileHandler> _handlers = new();

        internal static void InternalInitialize() {
            RegisterHandler(new ShortcutHandler());
            RegisterHandler(new AppHandler());
        }

        public static void RegisterApp(string appId, Func<string[], Window> factory) {
            _appRegistry[appId.ToUpper()] = factory;
        }

        public static void SetTooltip(UIElement element, string text, float delay = 0.5f) {
            if (element == null) return;
            element.Tooltip = text;
            element.TooltipDelay = delay;
        }

        public static Window CreateAppWindow(string appId, params string[] args) {
            if (string.IsNullOrEmpty(appId)) return null;
            string upperAppId = appId.ToUpper();
            DebugLogger.Log($"CreateAppWindow: Looking for {appId} (uppercase: {upperAppId})");
            
            // Check for single instance mode - if app is already running, focus it instead
            var existingProcess = ProcessManager.Instance.GetProcessesByApp(upperAppId)
                .FirstOrDefault(p => p.State != ProcessState.Terminated);
            if (existingProcess != null) {
                // Check manifest for singleInstance flag
                string appPath = AppLoader.Instance.GetAppDirectory(upperAppId);
                if (!string.IsNullOrEmpty(appPath)) {
                    string manifestPath = System.IO.Path.Combine(
                        VirtualFileSystem.Instance.ToHostPath(appPath),
                        "manifest.json"
                    );
                    if (System.IO.File.Exists(manifestPath)) {
                        try {
                            string json = System.IO.File.ReadAllText(manifestPath);
                            var manifest = AppManifest.FromJson(json);
                            if (manifest?.SingleInstance == true) {
                                DebugLogger.Log($"  SingleInstance: Focusing existing window for {upperAppId}");
                                if (existingProcess.MainWindow != null) {
                                    existingProcess.MainWindow.Parent?.BringToFront(existingProcess.MainWindow);
                                    if (!existingProcess.MainWindow.IsVisible) {
                                        existingProcess.MainWindow.Restore();
                                    }
                                    Window.ActiveWindow = existingProcess.MainWindow;
                                }
                                return null; // Don't create new window
                            }
                        } catch { }
                    }
                }
            }
            
            DebugLogger.Log($"  Registry has {_appRegistry.Count} apps: {string.Join(", ", _appRegistry.Keys)}");
            if (_appRegistry.TryGetValue(upperAppId, out var factory)) {
                DebugLogger.Log($"  Found factory for {upperAppId}");
                return factory(args ?? new string[0]);
            }
            DebugLogger.Log($"  No factory found for {upperAppId}");
            return null;
        }

        public static void RegisterHandler(FileHandler handler) {
            _handlers[handler.Extension.ToLower()] = handler;
        }

        internal static FileHandler GetFileHandler(string ext) {
            _handlers.TryGetValue(ext.ToLower(), out var handler);
            return handler;
        }

        public static void OpenWindow(Window win, Rectangle? startBounds = null) {
            if (WindowLayer != null) {
                // For modal windows, automatically set up parent-child relationship
                // ONLY if not already set (e.g. by Process.ShowModal)
                if (win.IsModal && Window.ActiveWindow != null && win.ParentWindow == null) {
                    win.ParentWindow = Window.ActiveWindow;
                    Window.ActiveWindow.ChildWindows.Add(win);
                    
                    // Copy process ownership if not already set
                    if (win.OwnerProcess == null && Window.ActiveWindow.OwnerProcess != null) {
                        win.OwnerProcess = Window.ActiveWindow.OwnerProcess;
                        Window.ActiveWindow.OwnerProcess.Windows.Add(win);
                    }
                }
                
                ApplyWindowLayout(win);
                if (startBounds.HasValue) win.AnimateOpen(startBounds.Value);

                WindowLayer.AddChild(win);
                Window.ActiveWindow = win;

                win.OnMove += () => win.LayoutDirty = true;
                win.OnResize += () => win.LayoutDirty = true;
            }
        }

        public static void SaveWindowLayout(Window win) {
            if (string.IsNullOrEmpty(win.AppId) || !win.IsVisible || win.Opacity < 0.5f) return;
            
            var layout = new WindowLayout {
                IsMaximized = win.IsMaximized,
                X = win.IsMaximized ? win.RestoreBounds.X : win.Position.X,
                Y = win.IsMaximized ? win.RestoreBounds.Y : win.Position.Y,
                Width = win.IsMaximized ? win.RestoreBounds.Width : win.Size.X,
                Height = win.IsMaximized ? win.RestoreBounds.Height : win.Size.Y
            };
            
            Registry.SetSetting("WindowLayout", layout, win.AppId);
        }

        public static void ApplyWindowLayout(Window win) {
            if (string.IsNullOrEmpty(win.AppId)) return;
            
            var layout = Registry.GetSetting<WindowLayout>("WindowLayout", null, win.AppId);
            if (layout != null) {
                win.Position = new Vector2(layout.X, layout.Y);
                win.Size = new Vector2(layout.Width, layout.Height);
                if (layout.IsMaximized) {
                    // Apply maximized state after adding to layer or instantly
                    var viewport = G.GraphicsDevice.Viewport;
                    win.SetMaximized(true, new Rectangle(0, 0, viewport.Width, viewport.Height - 40));
                }
            }
        }

        public static void PickFile(string title, string defaultPath, Action<string> onFilePicked) {
            if (WindowLayer == null) return;
            OpenWindow(new FilePickerWindow(title, defaultPath, "", FilePickerMode.Open, onFilePicked));
        }

        public static void SaveFile(string title, string defaultPath, string defaultName, Action<string> onFilePicked) {
            if (WindowLayer == null) return;
            OpenWindow(new FilePickerWindow(title, defaultPath, defaultName, FilePickerMode.Save, onFilePicked));
        }
    }

    public static class AppSettings {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Save<T>(T settings) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return;
            string dir = VirtualFileSystem.Instance.GetAppHomeDirectory(appId);
            string path = System.IO.Path.Combine(dir, "settings.json");
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            VirtualFileSystem.Instance.WriteAllText(path, json);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T Load<T>() where T : new() {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return new T();
            string dir = VirtualFileSystem.Instance.GetAppHomeDirectory(appId);
            string path = System.IO.Path.Combine(dir, "settings.json");
            
            string json = null;
            if (VirtualFileSystem.Instance.Exists(path)) json = VirtualFileSystem.Instance.ReadAllText(path);
            else {
                string bundlePath = VirtualFileSystem.Instance.GetAppResourcePath(appId, "settings.json");
                if (VirtualFileSystem.Instance.Exists(bundlePath)) json = VirtualFileSystem.Instance.ReadAllText(bundlePath);
            }

            if (json == null) return new T();
            try { return JsonSerializer.Deserialize<T>(json) ?? new T(); } catch { return new T(); }
        }
    }

    public static class Audio {
        public static void PlaySound(string virtualPath, float volume = 1.0f) {
            AudioManager.Instance.PlaySound(virtualPath, volume);
        }
    }

    public static class Notifications {
        public static string Show(string title, string text, Texture2D icon = null, Action onClick = null, List<NotificationAction> actions = null) {
            return NotificationManager.Instance.ShowNotification(title, text, icon, onClick, actions);
        }
        public static void Dismiss(string notificationId) {
            NotificationManager.Instance.Dismiss(notificationId);
        }
    }

    /// <summary>
    /// System-wide Clipboard API.
    /// Supports text, files, and images with history.
    /// </summary>
    public static class Clipboard {
        /// <summary>
        /// Sets the current clipboard text.
        /// </summary>
        public static void SetText(string text, string appId = null) {
            if (appId == null) appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            ClipboardManager.Instance.SetData(text, ClipboardContentType.Text, appId);
        }

        /// <summary>
        /// Gets the current clipboard text, or null if clipboard is not text.
        /// </summary>
        public static string GetText() {
            return ClipboardManager.Instance.GetData<string>(ClipboardContentType.Text);
        }

        /// <summary>
        /// Sets a list of file paths to the clipboard.
        /// </summary>
        public static void SetFiles(IEnumerable<string> paths) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            ClipboardManager.Instance.SetData(paths.ToList(), ClipboardContentType.FileList, appId);
        }

        /// <summary>
        /// Gets the current clipboard file list, or null if clipboard is not a file list.
        /// </summary>
        public static List<string> GetFiles() {
            return ClipboardManager.Instance.GetData<List<string>>(ClipboardContentType.FileList);
        }

        /// <summary>
        /// Sets an image to the clipboard.
        /// </summary>
        public static void SetImage(Texture2D image) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            ClipboardManager.Instance.SetData(image, ClipboardContentType.Image, appId);
        }

        /// <summary>
        /// Gets the current clipboard image, or null if clipboard is not an image.
        /// </summary>
        public static Texture2D GetImage() {
            return ClipboardManager.Instance.GetData<Texture2D>(ClipboardContentType.Image);
        }

        /// <summary>
        /// Gets the full clipboard history.
        /// </summary>
        public static IReadOnlyList<ClipboardItem> GetHistory() {
            return ClipboardManager.Instance.GetHistory();
        }

        /// <summary>
        /// Clears the clipboard history.
        /// </summary>
        public static void Clear() {
            ClipboardManager.Instance.Clear();
        }

        /// <summary>
        /// Event triggered whenever the clipboard content changes.
        /// </summary>
        public static event Action OnChanged {
            add => ClipboardManager.Instance.OnClipboardChanged += value;
            remove => ClipboardManager.Instance.OnClipboardChanged -= value;
        }
    }

    // --- Helper Models ---
    public class WindowLayout {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool IsMaximized { get; set; }
    }
}

public class ShortcutHandler : FileHandler {
    public override string Extension => ".slnk";
    public override void Execute(string virtualPath, Rectangle? startBounds = null) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut == null) return;
        string target = shortcut.TargetPath;
        var win = Shell.UI.CreateAppWindow(target);
        if (win != null) { Shell.UI.OpenWindow(win, startBounds); return; }
        if (VirtualFileSystem.Instance.Exists(target)) Shell.Execute(target, startBounds);
    }
    public override Texture2D GetIcon(string virtualPath) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut != null && VirtualFileSystem.Instance.Exists(shortcut.TargetPath)) return Shell.GetIcon(shortcut.TargetPath);
        return base.GetIcon(virtualPath);
    }
}

public class AppHandler : FileHandler {
    public override string Extension => ".sapp";
    public override void Execute(string virtualPath, Rectangle? startBounds = null) {
        string appId = GetAppId(virtualPath);
        if (string.IsNullOrEmpty(appId)) return;
        var win = Shell.UI.CreateAppWindow(appId);
        if (win != null) Shell.UI.OpenWindow(win, startBounds);
    }
    private string GetAppId(string virtualPath) {
        string manifestPath = System.IO.Path.Combine(virtualPath, "manifest.json");
        if (VirtualFileSystem.Instance.Exists(manifestPath)) {
            try {
                string json = VirtualFileSystem.Instance.ReadAllText(manifestPath);
                var manifest = AppManifest.FromJson(json);
                if (manifest != null && !string.IsNullOrEmpty(manifest.AppId)) return manifest.AppId;
            } catch { }
        }
        string pkgPath = System.IO.Path.Combine(virtualPath, "app_id.txt");
        if (VirtualFileSystem.Instance.Exists(pkgPath)) return VirtualFileSystem.Instance.ReadAllText(pkgPath)?.Trim();
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath)) return null;
        return VirtualFileSystem.Instance.ReadAllText(virtualPath)?.Trim();
    }
    public override Texture2D GetIcon(string virtualPath) {
        string iconPath = System.IO.Path.Combine(virtualPath, "icon.png");
        if (VirtualFileSystem.Instance.Exists(iconPath)) {
            try { return Core.ImageLoader.Load(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(iconPath)); } catch { }
        }
        string appId = GetAppId(virtualPath);
        if (appId == "EXPLORER") return GameContent.ExplorerIcon;
        return base.GetIcon(virtualPath);
    }
}
