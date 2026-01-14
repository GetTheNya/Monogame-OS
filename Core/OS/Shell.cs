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

namespace TheGame.Core.OS;

public static class Shell {
    private static Dictionary<string, FileHandler> _handlers = new();
    private static Dictionary<string, Func<Window>> _appRegistry = new();
    
    // Reference to scene/window layer for spawning windows
    public static Panel WindowLayer;
    public static ContextMenu GlobalContextMenu;
    
    // Backward compatibility wrapper for DraggedItem
    public static object DraggedItem {
        get => DragDropManager.Instance.DragData;
        set {
            if (value != null) {
                DragDropManager.Instance.BeginDrag(value, Vector2.Zero);
            } else {
                DragDropManager.Instance.EndDrag();
            }
        }
    }
    
    public static Action RefreshDesktop;
    public static Action<UIElement> OnAddOverlayElement;
    public static bool IsRenderingDrag = false;

    public static void AddOverlayElement(UIElement element) {
        OnAddOverlayElement?.Invoke(element);
    }

    public static void RefreshExplorers(string pathFilter = null) {
        if (WindowLayer == null) return;
        foreach (var child in WindowLayer.Children) {
            if (child is Window win && win.AppId == "EXPLORER") {
                try {
                    // Use dynamic to safely call RefreshList on a JIT-loaded type
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

    public static void CloseExplorers(string pathFilter) {
        if (WindowLayer == null || string.IsNullOrEmpty(pathFilter)) return;
        string filter = pathFilter.ToUpper().TrimEnd('\\') + "\\";
        
        foreach (var child in WindowLayer.Children.ToArray()) {
            if (child is Window win && win.AppId == "EXPLORER") {
                try {
                    dynamic explorer = win;
                    string current = ((string)explorer.CurrentPath).ToUpper().TrimEnd('\\') + "\\";
                    if (current.StartsWith(filter)) {
                        win.Close();
                    }
                } catch { }
            }
        }
    }

    public static void Initialize(Panel windowLayer, ContextMenu contextMenu) {
        WindowLayer = windowLayer;
        GlobalContextMenu = contextMenu;
        // Register default handlers
        RegisterHandler(new ShortcutHandler());
        RegisterHandler(new AppHandler());
    }

    public static void RegisterApp(string appId, Func<Window> factory) {
        _appRegistry[appId.ToUpper()] = factory;
    }

    public static Window CreateAppWindow(string appId) {
        if (string.IsNullOrEmpty(appId)) return null;
        if (_appRegistry.TryGetValue(appId.ToUpper(), out var factory)) {
            return factory();
        }
        return null;
    }

    public static void RegisterHandler(FileHandler handler) {
        _handlers[handler.Extension.ToLower()] = handler;
    }

    public static void Execute(string virtualPath, Rectangle? startBounds = null) {
        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();

        // 1. Try specific handlers first (important for .sapp which are directories)
        if (_handlers.TryGetValue(ext, out var handler)) {
            handler.Execute(virtualPath, startBounds);
            return;
        }

        // 2. If no handler, check if it's a directory
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath)) {
            var win = CreateAppWindow("EXPLORER");
            if (win != null) {
                try {
                    dynamic dwin = win;
                    dwin.NavigateTo(virtualPath);
                } catch { }
                OpenWindow(win, startBounds);
            }
            return;
        }

        // 3. Fallback
        DebugLogger.Log($"No handler for {ext}. Path: {virtualPath}");
    }

    public static void PlaySound(string virtualPath, float volume = 1.0f) {
        AudioManager.Instance.PlaySound(virtualPath, volume);
    }

    public static string ShowNotification(string title, string text, Texture2D icon = null, 
                                         Action onClick = null, List<NotificationAction> actions = null) {
        return NotificationManager.Instance.ShowNotification(title, text, icon, onClick, actions);
    }

    public static void DismissNotification(string notificationId) {
        NotificationManager.Instance.Dismiss(notificationId);
    }

    public static string GetAppHomeDirectory(string appId) {
        return VirtualFileSystem.Instance.GetAppHomeDirectory(appId);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetAppHomeDirectory() {
        string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
        return GetAppHomeDirectory(appId);
    }

    public static string GetAppResourcePath(string appId, string resourceName) {
        return VirtualFileSystem.Instance.GetAppResourcePath(appId, resourceName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static string GetAppResourcePath(string resourceName) {
        string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
        if (appId == null) return null;
        return GetAppResourcePath(appId, resourceName);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SaveSettings<T>(T settings) {
        string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
        if (appId == null) return;
        string dir = GetAppHomeDirectory(appId);
        string path = System.IO.Path.Combine(dir, "settings.json");
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        VirtualFileSystem.Instance.WriteAllText(path, json);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static T LoadSettings<T>() where T : new() {
        string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
        if (appId == null) return new T();
        
        string dir = GetAppHomeDirectory(appId);
        string path = System.IO.Path.Combine(dir, "settings.json");
        
        string json = null;
        if (VirtualFileSystem.Instance.Exists(path)) {
            json = VirtualFileSystem.Instance.ReadAllText(path);
        } else {
            // Fallback to bundled settings.json
            string bundlePath = GetAppResourcePath(appId, "settings.json");
            if (VirtualFileSystem.Instance.Exists(bundlePath)) {
                json = VirtualFileSystem.Instance.ReadAllText(bundlePath);
            }
        }

        if (json == null) return new T();

        try {
            return JsonSerializer.Deserialize<T>(json) ?? new T();
        } catch {
            return new T();
        }
    }

    public static void OpenWindow(Window win, Rectangle? startBounds = null) {
        if (WindowLayer != null) {
            if (startBounds.HasValue) win.AnimateOpen(startBounds.Value);
            WindowLayer.AddChild(win);
            Window.ActiveWindow = win;
        }
    }

    public static Texture2D GetIcon(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return GameContent.FileIcon;

        if (virtualPath.ToUpper().Contains("$RECYCLE.BIN")) {
            return VirtualFileSystem.Instance.IsRecycleBinEmpty() ? GameContent.TrashEmptyIcon : GameContent.TrashFullIcon;
        }

        if (VirtualFileSystem.Instance.IsDirectory(virtualPath) && !virtualPath.ToLower().EndsWith(".sapp")) {
            return GameContent.FolderIcon;
        }

        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();
        if (_handlers.TryGetValue(ext, out var handler)) {
            return handler.GetIcon(virtualPath);
        }
        return GameContent.FileIcon;
    }

    public static void DrawDrag(SpriteBatch sb, ShapeBatch sbatch) {
        DragDropManager.Instance.DrawDragVisual(sb, sbatch);
    }
}

public class ShortcutHandler : FileHandler {
    public override string Extension => ".slnk";

    public override void Execute(string virtualPath, Rectangle? startBounds = null) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut == null) return;

        // Command processing
        string target = shortcut.TargetPath;

        // If it's a direct command (old style/special)
        var win = Shell.CreateAppWindow(target);
        if (win != null) {
            Shell.OpenWindow(win, startBounds);
            return;
        }

        // If it's a virtual path, execute it
        if (VirtualFileSystem.Instance.Exists(target)) {
            Shell.Execute(target, startBounds);
        } else {
            DebugLogger.Log($"Shortcut target not found: {target}");
        }
    }

    public override Texture2D GetIcon(string virtualPath) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        
        if (shortcut != null && VirtualFileSystem.Instance.Exists(shortcut.TargetPath)) {
            return Shell.GetIcon(shortcut.TargetPath);
        }

        return base.GetIcon(virtualPath);
    }
}

public class AppHandler : FileHandler {
    public override string Extension => ".sapp";

    public override void Execute(string virtualPath, Rectangle? startBounds = null) {
        DebugLogger.Log($"AppHandler: Executing {virtualPath}");
        string appId = GetAppId(virtualPath);
        if (string.IsNullOrEmpty(appId)) {
            DebugLogger.Log($"AppHandler: Could not find AppId for {virtualPath}");
            return;
        }

        DebugLogger.Log($"AppHandler: Found AppId '{appId}', spawning window...");
        var win = Shell.CreateAppWindow(appId);
        if (win != null) {
            Shell.OpenWindow(win, startBounds);
        } else {
            DebugLogger.Log($"AppHandler: No app registered for ID '{appId}'");
        }
    }

    private string GetAppId(string virtualPath) {
        // 1. Try reading as bundle with manifest.json (new JIT style)
        string manifestPath = System.IO.Path.Combine(virtualPath, "manifest.json");
        if (VirtualFileSystem.Instance.Exists(manifestPath)) {
            try {
                string json = VirtualFileSystem.Instance.ReadAllText(manifestPath);
                var manifest = AppManifest.FromJson(json);
                if (manifest != null && !string.IsNullOrEmpty(manifest.AppId)) {
                    return manifest.AppId;
                }
            } catch (Exception ex) {
                DebugLogger.Log($"AppHandler: Error reading manifest: {ex.Message}");
            }
        }

        // 2. Try reading as bundle with app_id.txt (legacy style)
        string pkgPath = System.IO.Path.Combine(virtualPath, "app_id.txt");
        if (VirtualFileSystem.Instance.Exists(pkgPath)) {
            string content = VirtualFileSystem.Instance.ReadAllText(pkgPath);
            if (!string.IsNullOrEmpty(content)) return content.Trim();
        }

        // 3. Try reading as file (legacy/simple)
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath)) return null;
        string fileContent = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        return fileContent?.Trim();
    }

    public override Texture2D GetIcon(string virtualPath) {
        // Try to load custom icon from bundle (icon.png)
        string iconPath = System.IO.Path.Combine(virtualPath, "icon.png");
        if (VirtualFileSystem.Instance.Exists(iconPath)) {
            try {
                string hostPath = VirtualFileSystem.Instance.ToHostPath(iconPath);
                return Core.ImageLoader.Load(G.GraphicsDevice, hostPath);
            } catch (Exception ex) {
                DebugLogger.Log($"Error loading app icon from {iconPath}: {ex.Message}");
            }
        }

        string appId = GetAppId(virtualPath);
        if (appId == "EXPLORER") return GameContent.ExplorerIcon;
        
        return base.GetIcon(virtualPath);
    }
}
