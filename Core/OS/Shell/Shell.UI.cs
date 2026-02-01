using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class UI {
        private static Dictionary<string, Func<string[], Action<TheGame.Core.OS.Process>, Window>> _appRegistry = new();
        private static Dictionary<string, FileHandler> _handlers = new();

        internal static void InternalInitialize() {
            RegisterHandler(new ShortcutHandler());
            RegisterHandler(new AppHandler());
        }

        public static void RegisterApp(string appId, Func<string[], Action<TheGame.Core.OS.Process>, Window> factory) {
            _appRegistry[appId.ToUpper()] = factory;
        }

        public static void SetTooltip(UIElement element, string text, float delay = 0.5f) {
            if (element == null) return;
            element.Tooltip = text;
            element.TooltipDelay = delay;
        }

        public static Window CreateAppWindow(string appId, string[] args = null, Action<TheGame.Core.OS.Process> setup = null) {
            if (string.IsNullOrEmpty(appId)) return null;
            string upperAppId = appId.ToUpper();
            DebugLogger.Log($"CreateAppWindow: Looking for {appId} (uppercase: {upperAppId})");

            // ... (rest of logic)
            if (_appRegistry.TryGetValue(upperAppId, out var factory)) {
                DebugLogger.Log($"  Found factory for {upperAppId}");
                return factory(args ?? new string[0], setup);
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

        public static void OpenWindow(Window win, Rectangle? startBounds = null, TheGame.Core.OS.Process owner = null, Window parent = null) {
            if (WindowLayer != null) {
                // Prevent double-open: if already in WindowLayer, just bring to front
                if (WindowLayer.Children.Contains(win)) {
                    Window.ActiveWindow = win;
                    WindowLayer.BringToFront(win);
                    return;
                }

                // If owner is provided, ensure it's set on the window
                if (owner != null) {
                    if (win.OwnerProcess == null) win.OwnerProcess = owner;
                    if (!owner.Windows.Contains(win)) owner.Windows.Add(win);
                }

                // Handle parenting and modal logic
                if (win.IsModal) {
                    // 1. Explicitly requested parent
                    // 2. Window's existing ParentWindow
                    // 3. Current ActiveWindow (fallback - still allowed but less ideal)
                    var finalParent = parent ?? win.ParentWindow ?? Window.ActiveWindow;

                    if (finalParent != null && finalParent != win) {
                        win.ParentWindow = finalParent;
                        if (!finalParent.ChildWindows.Contains(win)) {
                            finalParent.ChildWindows.Add(win);
                        }
                    }

                    // Ensure process ownership consistency for modals
                    if (owner == null && win.ParentWindow?.OwnerProcess != null) {
                        win.OwnerProcess = win.ParentWindow.OwnerProcess;
                        if (!win.OwnerProcess.Windows.Contains(win)) {
                            win.OwnerProcess.Windows.Add(win);
                        }
                    }
                }

                ApplyWindowLayout(win);
                if (startBounds.HasValue) win.AnimateOpen(startBounds.Value);

                WindowLayer.AddChild(win);
                Window.ActiveWindow = win;
                WindowLayer.BringToFront(win);

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
}
