using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class UI {
        private static Dictionary<string, Func<string[], Action<TheGame.Core.OS.Process>, WindowBase>> _appRegistry = new();
        private static readonly object _appRegistryLock = new();
        private static Dictionary<string, FileHandler> _handlers = new();

        internal static void InternalInitialize() {
            RegisterHandler(new ShortcutHandler());
            RegisterHandler(new AppHandler());
        }

        public static void RegisterApp(string appId, Func<string[], Action<TheGame.Core.OS.Process>, WindowBase> factory) {
            lock (_appRegistryLock) {
                _appRegistry[appId.ToUpper()] = factory;
            }
        }

        public static void Reset() {
            lock (_appRegistryLock) {
                _appRegistry.Clear();
                InternalInitialize();
            }
            DebugLogger.Log("[Shell.UI] Registry reset for restart");
        }

        public static void SetTooltip(UIElement element, string text, float delay = 0.5f) {
            if (element == null) return;
            element.Tooltip = text;
            element.TooltipDelay = delay;
        }

        public static WindowBase CreateAppWindow(string appId, string[] args = null, Action<TheGame.Core.OS.Process> setup = null) {
            if (string.IsNullOrEmpty(appId)) return null;
            string upperAppId = appId.ToUpper();
            DebugLogger.Log($"CreateAppWindow: Looking for {appId} (uppercase: {upperAppId})");

            // ... (rest of logic)
            Func<string[], Action<TheGame.Core.OS.Process>, WindowBase> factory = null;
            lock (_appRegistryLock) {
                _appRegistry.TryGetValue(upperAppId, out factory);
            }

            if (factory != null) {
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

        public static void OpenWindow(WindowBase win, Rectangle? startBounds = null, TheGame.Core.OS.Process owner = null, WindowBase parent = null) {
            if (WindowLayer != null) {
                // Prevent double-open: if already in WindowLayer, just bring to front
                if (WindowLayer.Children.Contains(win)) {
                    WindowBase.ActiveWindow = win;
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
                    var finalParent = parent ?? win.ParentWindow ?? WindowBase.ActiveWindow;

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
                if (startBounds.HasValue && win is Window osWin) osWin.AnimateOpen(startBounds.Value);

                WindowLayer.AddChild(win);
                WindowBase.ActiveWindow = win;
                WindowLayer.BringToFront(win);

                win.OnMove += () => { if (win is Window w) w.LayoutDirty = true; };
                win.OnResize += () => { if (win is Window w) w.LayoutDirty = true; };
            }
        }

        public static void SaveWindowLayout(WindowBase win) {
            if (string.IsNullOrEmpty(win.AppId) || !win.IsVisible || win.Opacity < 0.5f) return;

            var layout = new WindowLayout {
                IsMaximized = win is Window w && w.IsMaximized,
                X = (win is Window w2 && w2.IsMaximized) ? w2.RestoreBounds.X : win.Position.X,
                Y = (win is Window w3 && w3.IsMaximized) ? w3.RestoreBounds.Y : win.Position.Y,
                Width = (win is Window w4 && w4.IsMaximized) ? w4.RestoreBounds.Width : win.Size.X,
                Height = (win is Window w5 && w5.IsMaximized) ? w5.RestoreBounds.Height : win.Size.Y
            };

            Registry.SetSetting("WindowLayout", layout, win.AppId);
        }

        public static void ApplyWindowLayout(WindowBase win) {
            if (string.IsNullOrEmpty(win.AppId)) return;

            var layout = Registry.GetSetting<WindowLayout>("WindowLayout", null, win.AppId);
            if (layout != null) {
                win.Position = new Vector2(layout.X, layout.Y);
                win.Size = new Vector2(layout.Width, layout.Height);
                if (layout.IsMaximized && win is Window w) {
                    // Apply maximized state after adding to layer or instantly
                    var viewport = G.GraphicsDevice.Viewport;
                    w.SetMaximized(true, new Rectangle(0, 0, viewport.Width, viewport.Height - 40));
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
