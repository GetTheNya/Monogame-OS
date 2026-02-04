using System;
using System.Linq;
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

    public static Texture2D GetIcon(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return GameContent.FileIcon;
        if (virtualPath.ToUpper().Contains("$RECYCLE.BIN")) return VirtualFileSystem.Instance.IsRecycleBinEmpty() ? GameContent.TrashEmptyIcon : GameContent.TrashFullIcon;
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath) && !virtualPath.ToLower().EndsWith(".sapp")) return GameContent.FolderIcon;
        string ext = System.IO.Path.GetExtension(virtualPath).ToLower();
        var handler = UI.GetFileHandler(ext);
        return handler?.GetIcon(virtualPath) ?? GameContent.FileIcon;
    }

    public static void Initialize(Panel windowLayer, TheGame.Core.UI.ContextMenu contextMenu) {
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
                }
                catch (Exception ex) {
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
            var win = UI.CreateAppWindow(appId, new[] { virtualPath });
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
                try {
                    dynamic dwin = win;
                    dwin.NavigateTo(virtualPath);
                }
                catch {
                }

                UI.OpenWindow(win, startBounds);
            }

            return;
        }

        DebugLogger.Log($"No handler for {ext}. Path: {virtualPath}");
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