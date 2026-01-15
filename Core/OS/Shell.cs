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
    // Shared state
    public static Panel WindowLayer;
    public static ContextMenu GlobalContextMenu;
    public static Action RefreshDesktop;
    public static Action<UIElement> OnAddOverlayElement;
    public static bool IsRenderingDrag = false;

    public static void AddOverlayElement(UIElement element) => OnAddOverlayElement?.Invoke(element);
    public static void DrawDrag(SpriteBatch sb, ShapeBatch sbatch) => DragDropManager.Instance.DrawDragVisual(sb, sbatch);

    // Backward compatibility wrapper for DraggedItem
    public static object DraggedItem {
        get => DragDropManager.Instance.DragData;
        set {
            if (value != null) DragDropManager.Instance.BeginDrag(value, Vector2.Zero);
            else DragDropManager.Instance.EndDrag();
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
                    if (current.StartsWith(filter)) win.Close();
                } catch { }
            }
        }
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
        var handler = UI.GetFileHandler(ext);
        if (handler != null) {
            handler.Execute(virtualPath, startBounds);
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
        private static Dictionary<string, Func<Window>> _appRegistry = new();
        private static Dictionary<string, FileHandler> _handlers = new();

        internal static void InternalInitialize() {
            RegisterHandler(new ShortcutHandler());
            RegisterHandler(new AppHandler());
        }

        public static void RegisterApp(string appId, Func<Window> factory) {
            _appRegistry[appId.ToUpper()] = factory;
        }

        public static Window CreateAppWindow(string appId) {
            if (string.IsNullOrEmpty(appId)) return null;
            if (_appRegistry.TryGetValue(appId.ToUpper(), out var factory)) return factory();
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
                if (startBounds.HasValue) win.AnimateOpen(startBounds.Value);
                else ApplyWindowLayout(win);

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

    public static class Settings {
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

    public static class Registry {
        internal static void Initialize() { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T GetSetting<T>(string key, T defaultValue = default, string appIdOverride = null) {
            string appId = appIdOverride ?? AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return defaultValue;
            return OS.Registry.GetValue($"HKCU\\Software\\{appId}\\{key}", defaultValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetSetting<T>(string key, T value, string appIdOverride = null) {
            string appId = appIdOverride ?? AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return;
            OS.Registry.SetValue($"HKCU\\Software\\{appId}\\{key}", value);
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
