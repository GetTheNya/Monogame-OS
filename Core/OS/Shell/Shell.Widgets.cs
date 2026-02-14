using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheGame.Core.UI.Widgets;
using System.Linq;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Widgets {
        public static Action RefreshWidgets { get; set; }
        
        public static bool IsEditingLayout { get; set; } = false;

        public const string RegistryPath = "HKCU\\Software\\OS\\Desktop\\Widgets";

        public static void AddWidget(string widgetType, Vector2? position = null) {
            string widgetId = Guid.NewGuid().ToString();
            Vector2 pos = position ?? new Vector2(100, 100);
            Vector2 size = new Vector2(200, 200); // Default size, widgets will override this

            string key = $"{RegistryPath}\\{widgetId}";
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Type", widgetType);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\X", pos.X);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Y", pos.Y);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\ZIndex", 0);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Locked", true);

            DebugLogger.Log($"[Widgets] Added new widget {widgetType} ({widgetId})");
            RefreshWidgets?.Invoke();
        }

        public static void RemoveWidget(string widgetId) {
            TheGame.Core.OS.Registry.Instance.DeleteKey($"{RegistryPath}\\{widgetId}");
            RefreshWidgets?.Invoke();
        }

        public static void UninstallWidget(string widgetType) {
            try {
                // 1. Remove all active instances of this widget
                var active = GetActiveWidgets();
                foreach (var w in active) {
                    if (w.Type == widgetType) {
                        RemoveWidget(w.Id);
                    }
                }

                // 2. Delete the .dtoy directory
                string dtoyDir = System.IO.Path.Combine(WidgetDirectory, widgetType + ".dtoy");
                if (VirtualFileSystem.Instance.Exists(dtoyDir)) {
                    VirtualFileSystem.Instance.DeleteDirectory(dtoyDir, true);
                    DebugLogger.Log($"[Widgets] Uninstalled widget: {widgetType}");
                }

                // 3. Reload Loader and Refresh UI
                WidgetLoader.Instance.ReloadDynamicWidgets();
                RefreshWidgets?.Invoke();
                
            } catch (Exception ex) {
                DebugLogger.Log($"[Widgets] Error uninstalling widget {widgetType}: {ex.Message}");
            }
        }

        public static void SaveWidgetPosition(string widgetId, Vector2 position) {
            string key = $"{RegistryPath}\\{widgetId}";
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\X", position.X);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Y", position.Y);
        }

        public static void SaveWidgetSize(string widgetId, Vector2 size) {
            string key = $"{RegistryPath}\\{widgetId}";
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Width", size.X);
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\Height", size.Y);
        }

        public static void SetZIndex(string widgetId, int zIndex) {
            string key = $"{RegistryPath}\\{widgetId}";
            TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\ZIndex", zIndex);
            RefreshWidgets?.Invoke();
        }

        public static void SetZIndices(List<(string Id, int ZIndex)> updates) {
            foreach (var update in updates) {
                string key = $"{RegistryPath}\\{update.Id}";
                TheGame.Core.OS.Registry.Instance.SetValue($"{key}\\ZIndex", update.ZIndex);
            }
            RefreshWidgets?.Invoke();
        }

        public static void ToggleEditMode() {
            IsEditingLayout = !IsEditingLayout;
            RefreshWidgets?.Invoke();
        }

        public static List<WidgetData> GetActiveWidgets() {
            var widgets = new List<WidgetData>();
            var keys = TheGame.Core.OS.Registry.Instance.GetSubKeys(RegistryPath);

            foreach (var widgetId in keys) {
                string key = $"{RegistryPath}\\{widgetId}";
                string type = TheGame.Core.OS.Registry.Instance.GetValue<string>($"{key}\\Type", null);
                if (type == null) continue;

                widgets.Add(new WidgetData {
                    Id = widgetId,
                    Type = type,
                    Position = new Vector2(
                        TheGame.Core.OS.Registry.Instance.GetValue<float>($"{key}\\X", 0f),
                        TheGame.Core.OS.Registry.Instance.GetValue<float>($"{key}\\Y", 0f)
                    ),
                    Size = new Vector2(
                        TheGame.Core.OS.Registry.Instance.GetValue<float>($"{key}\\Width", 0f),
                        TheGame.Core.OS.Registry.Instance.GetValue<float>($"{key}\\Height", 0f)
                    ),
                    ZIndex = TheGame.Core.OS.Registry.Instance.GetValue<int>($"{key}\\ZIndex", 0)
                });
            }

            return widgets.OrderBy(w => w.ZIndex).ToList();
        }

        // === Communication Hub ===
        private static Dictionary<string, List<Action<object>>> _subscribers = new();
        private static Dictionary<string, object> _latestData = new();

        public static string WidgetDirectory => $@"C:\Users\{SystemConfig.Username}\DeskToys\Widgets\";

        /// <summary>
        /// Publish data to a channel. Apps use this to broadcast state to widgets.
        /// Example: Shell.Widgets.Publish("NeonWave.Playback", playbackData);
        /// </summary>
        public static void Publish(string channel, object data) {
            _latestData[channel] = data;

            if (_subscribers.TryGetValue(channel, out var callbacks)) {
                foreach (var callback in callbacks.ToList()) {
                    try {
                        callback(data);
                    } catch (Exception ex) {
                        DebugLogger.Log($"[Widgets] Channel {channel} error: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// Subscribe to a channel. Widgets use this to receive app updates.
        /// Returns unsubscribe action (call in widget Dispose).
        /// </summary>
        public static Action Subscribe(string channel, Action<object> callback) {
            if (!_subscribers.ContainsKey(channel)) {
                _subscribers[channel] = new List<Action<object>>();
            }

            _subscribers[channel].Add(callback);

            // Send latest data immediately (late-join support)
            if (_latestData.TryGetValue(channel, out var latest)) {
                try {
                    callback(latest);
                } catch { }
            }

            return () => _subscribers[channel]?.Remove(callback);
        }

        /// <summary>
        /// Get latest published data for a channel (polling alternative).
        /// </summary>
        public static object GetLatestData(string channel) {
            return _latestData.TryGetValue(channel, out var data) ? data : null;
        }
    }

    public class WidgetData {
        public string Id { get; set; }
        public string Type { get; set; }
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public int ZIndex { get; set; }
    }
}
