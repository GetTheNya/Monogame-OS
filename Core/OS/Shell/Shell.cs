using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;
using TheGame.Core.Input;

namespace TheGame.Core.OS;

public static partial class Shell {
    // Shared state
    public static Panel WindowLayer;
    public static Action RefreshDesktop;
    public static TheGame.Core.UI.ContextMenu GlobalContextMenu;

    public static Action<UIElement> OnAddOverlayElement;
    public static Action<UIElement> OnRemoveOverlayElement;
    public static bool IsRenderingDrag = false;

    public static void AddOverlayElement(UIElement element) => OnAddOverlayElement?.Invoke(element);
    public static void RemoveOverlayElement(UIElement element) => OnRemoveOverlayElement?.Invoke(element);
    public static void DrawDrag(SpriteBatch sb, ShapeBatch sbatch) => DragDropManager.Instance.DrawDragVisual(sb, sbatch);

    public static void Update(GameTime gameTime) {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        // Update Tweener for global animations (OS-level)
        TheGame.Core.Animation.Tweener.Update(dt);

        // Update DragDropManager for snap-back animation
        DragDropManager.Instance.Update(gameTime);
        
        // Update all running processes
        ProcessManager.Instance.Update(gameTime);

        // Global watchdog: If mouse is released but drag is still active, it means the drop wasn't handled.
        // Only trigger if snap-back isn't already animating (prevents re-triggering)
        if (DragDropManager.Instance.IsActive && 
            !DragDropManager.Instance.IsSnapBackAnimating &&
            !InputManager.IsMouseButtonDown(MouseButton.Left)) {
            DragDropManager.Instance.CancelDrag();
        }
    }

    // Drag and Drop API (Deprecated - Use Shell.Drag instead)
    /// <summary>
    /// Checks if a drag operation is currently active.
    /// </summary>
    [System.Obsolete("Use Shell.Drag.IsActive instead")]
    public static bool IsDragging => DragDropManager.Instance.IsActive;

    /// <summary>
    /// Gets or sets the currently dragged item. Setting to null ends the drag.
    /// Note: For proper snap-back behavior, use BeginDrag() with source position.
    /// </summary>
    [System.Obsolete("Use Shell.Drag.DraggedItem instead")]
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
    [System.Obsolete("Use Shell.Drag.Begin() instead")]
    public static void BeginDrag(object data, Vector2 sourcePosition, Vector2 grabOffset)
        => DragDropManager.Instance.BeginDrag(data, sourcePosition, grabOffset);

    /// <summary>
    /// Ends the current drag operation (successful drop).
    /// </summary>
    [System.Obsolete("Use Shell.Drag.End() instead")]
    public static void EndDrag()
        => DragDropManager.Instance.EndDrag();

    /// <summary>
    /// Cancels the drag and restores original positions.
    /// </summary>
    [System.Obsolete("Use Shell.Drag.Cancel() instead")]
    public static void CancelDrag()
        => DragDropManager.Instance.CancelDrag();

    /// <summary>
    /// Checks if a specific item is currently being dragged.
    /// </summary>
    [System.Obsolete("Use Shell.Drag.IsItemDragged() instead")]
    public static bool IsItemBeingDragged(object item)
        => DragDropManager.Instance.IsItemDragged(item);

    /// <summary>
    /// Gets the Application instance for the focused application.
    /// [DEPRECATED] Use explicit application context instead.
    /// </summary>
    [Obsolete("Use explicit Application context instead.")]
    public static Application App => Process.Current?.Application;


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
                }
                catch { }
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

    private static Dictionary<string, Texture2D> _resolvedFileIcons = new();

    public static Texture2D GetIcon(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return GameContent.FileIcon;
        if (virtualPath.ToUpper().Contains("$RECYCLE.BIN")) return VirtualFileSystem.Instance.IsRecycleBinEmpty() ? GameContent.TrashEmptyIcon : GameContent.TrashFullIcon;
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath) && !virtualPath.ToLower().EndsWith(".sapp")) return GameContent.FolderIcon;
        
        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();
        
        // 1. Check new file association system for custom icons
        string appId = File.GetDefaultFileTypeHandler(ext);
        if (!string.IsNullOrEmpty(appId)) {
            string cacheKey = $"{appId}:{ext}";
            if (_resolvedFileIcons.TryGetValue(cacheKey, out var cached)) return cached;

            var data = File.GetFileTypeHandlers(ext); // This triggers self-healing
            var handlers = TheGame.Core.OS.Registry.Instance.GetValue<FileAssociationData>($"{Shell.Registry.FileAssociations}\\{ext}", null);
            
            if (handlers != null && handlers.Handlers.TryGetValue(appId, out var handler) && !string.IsNullOrEmpty(handler.Icon)) {
                string appDir = AppLoader.Instance.GetAppDirectory(appId);
                if (appDir != null) {
                    string iconPath = VirtualFileSystem.Instance.ResolvePath(appDir, handler.Icon);
                    if (VirtualFileSystem.Instance.Exists(iconPath)) {
                        try {
                            string hostPath = VirtualFileSystem.Instance.ToHostPath(iconPath);
                            var tex = ImageLoader.Load(G.GraphicsDevice, hostPath);
                            if (tex != null) {
                                _resolvedFileIcons[cacheKey] = tex;
                                return tex;
                            }
                        } catch { }
                    }
                }
            }
        }

        // 2. Fallback to hardcoded handlers
        var builtInHandler = UI.GetFileHandler(ext);
        if (builtInHandler != null) return builtInHandler.GetIcon(virtualPath);

        return GameContent.FileIcon;
    }

    public static void Initialize(Panel windowLayer, TheGame.Core.UI.ContextMenu contextMenu) {
        WindowLayer = windowLayer;
        GlobalContextMenu = contextMenu;
        TheGame.Core.OS.Registry.Instance.Initialize();
        UI.InternalInitialize();
    }

    /// <summary>
    /// Performs a full system restart.
    /// </summary>
    public static void Restart() {
        DebugLogger.Log("--- SYSTEM RESTART INITIATED ---");
        
        // 1. Terminate all processes
        ProcessManager.Instance.TerminateAll();
        
        // 2. Reset AppLoader
        AppLoader.Instance.Reset();
        
        // 3. Reset HotReload
        AppHotReloadManager.Instance.Reset();
        
        // 4. Reset Shell UI Registry
        UI.Reset();
        
        // 5. Clear Window Layer
        WindowLayer?.ClearChildren();
        
        // 6. Cancel active drags
        DragDropManager.Instance.CancelDrag();
        
        // 7. Transition to ShutdownScene (Restart mode)
        Game1.Instance.SceneManager.TransitionTo(new TheGame.Scenes.ShutdownScene(TheGame.Scenes.ShutdownMode.Restart));
        
        DebugLogger.Log("--- SYSTEM RESTART READY ---");
    }

    /// <summary>
    /// Performs a full system shutdown.
    /// </summary>
    public static void Shutdown() {
        DebugLogger.Log("--- SYSTEM SHUTDOWN INITIATED ---");
        
        // 1. Terminate all processes
        ProcessManager.Instance.TerminateAll();
        
        // 2. Reset AppLoader
        AppLoader.Instance.Reset();
        
        // 3. Reset HotReload
        AppHotReloadManager.Instance.Reset();
        
        // 4. Reset Shell UI Registry
        UI.Reset();
        
        // 5. Clear Window Layer
        WindowLayer?.ClearChildren();
        
        // 6. Cancel active drags
        DragDropManager.Instance.CancelDrag();
        
        // 7. Transition to ShutdownScene (Shutdown mode)
        Game1.Instance.SceneManager.TransitionTo(new TheGame.Scenes.ShutdownScene(TheGame.Scenes.ShutdownMode.Shutdown));
        
        DebugLogger.Log("--- SYSTEM SHUTDOWN READY ---");
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
                }
                catch (Exception ex) {
                    DebugLogger.Log($"Error refreshing explorer: {ex.Message}");
                }
            }
        }
    }

    public static void Execute(string virtualPath, Rectangle? startBounds = null) {
        Execute(virtualPath, null, startBounds);
    }

    public static void Execute(string virtualPath, string args, Rectangle? startBounds = null) {
        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();
        DebugLogger.Log($"Shell.Execute: {virtualPath}, extension: {ext}");

        // Handle system file types (.sapp, .slnk) through built-in handlers first
        // These should never go through file associations
        if (ext == ".sapp" || ext == ".slnk") {
            var builtIn = UI.GetFileHandler(ext);
            if (builtIn != null) {
                builtIn.Execute(virtualPath, args, startBounds);
                return;
            }
        }

        // 1. Try default handler from association system
        string appId = File.GetDefaultFileTypeHandler(ext);
        if (!string.IsNullOrEmpty(appId)) {
            DebugLogger.Log($"Shell.Execute: Using default handler {appId} for {ext}");
            ProcessManager.Instance.StartProcess(appId, new[] { virtualPath }, null, startBounds);
            return;
        }

        // 2. If no default, check if we have multiple handlers
        var handlers = File.GetFileTypeHandlers(ext);
        if (handlers.Count > 1) {
            DebugLogger.Log($"Shell.Execute: Multiple handlers found for {ext}. Invoking Conflict Resolver.");
            ProcessManager.Instance.StartProcess("CONFLICTRESOLVER", new[] { virtualPath }, null, startBounds);
            return;
        } else if (handlers.Count == 1) {
            // Only one handler, use it directly (and it becomes the default implicitly by the system, or we can just launch it)
            DebugLogger.Log($"Shell.Execute: Only one handler found ({handlers[0]}). Using it.");
            ProcessManager.Instance.StartProcess(handlers[0], new[] { virtualPath }, null, startBounds);
            return;
        }

        // 3. Fall back to hardcoded built-in handlers
        var handler2 = UI.GetFileHandler(ext);
        if (handler2 != null) {
            handler2.Execute(virtualPath, startBounds);
            return;
        }

        // 4. Fall back to Explorer if it's a directory
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath)) {
            string[] argArray = new[] { args ?? virtualPath };
            var process = ProcessManager.Instance.StartProcess("EXPLORER", argArray, null, startBounds);
            if (process?.MainWindow != null) {
                try {
                    dynamic dwin = process.MainWindow;
                    dwin.NavigateTo(args ?? virtualPath);
                }
                catch { }
            }
            return;
        }

        DebugLogger.Log($"No handler found for {ext}. Path: {virtualPath}");
    }

    // --- Nested API Classes ---

    // --- Helper Models ---
    public class WindowLayout {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public bool IsMaximized { get; set; }
    }
}