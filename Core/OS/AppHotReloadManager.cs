using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;

namespace TheGame.Core.OS;

/// <summary>
/// Manages hot reload functionality for apps - watches source files and triggers recompilation on changes.
/// </summary>
public class AppHotReloadManager {
    private static AppHotReloadManager _instance;
    public static AppHotReloadManager Instance => _instance ??= new AppHotReloadManager();

    private bool _enabled = true;
    public bool Enabled {
        get => _enabled;
        set {
            _enabled = value;
            if (!_enabled) {
                StopAll();
            } else {
                // Re-watch all loaded apps
                RestartWatching();
            }
        }
    }

    private Dictionary<string, FileSystemWatcher> _watchers = new Dictionary<string, FileSystemWatcher>();
    private Dictionary<string, System.Timers.Timer> _debounceTimers = new Dictionary<string, System.Timers.Timer>();
    private Dictionary<string, string> _appPaths = new Dictionary<string, string>();
    private Queue<string> _pendingReloads = new Queue<string>();
    private object _reloadLock = new object();
    private const int DebounceDelayMs = 300;

    private AppHotReloadManager() { }

    /// <summary>
    /// Call this from the main game loop Update method to process pending reloads safely.
    /// </summary>
    public void Update() {
        // Process pending reloads on the main thread
        lock (_reloadLock) {
            while (_pendingReloads.Count > 0) {
                string appId = _pendingReloads.Dequeue();
                ExecuteReload(appId);
            }
        }
    }

    /// <summary>
    /// Start watching an app directory for changes.
    /// </summary>
    public void StartWatching(string appId, string appVirtualPath) {
        if (!_enabled) return;
        if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appVirtualPath)) return;

        string upperAppId = appId.ToUpper();
        
        // Stop existing watcher if any
        StopWatching(upperAppId);

        try {
            string hostPath = VirtualFileSystem.Instance.ToHostPath(appVirtualPath);
            if (!Directory.Exists(hostPath)) {
                DebugLogger.Log($"HotReload: Cannot watch {appId} - directory not found: {hostPath}");
                return;
            }

            _appPaths[upperAppId] = appVirtualPath;

            var watcher = new FileSystemWatcher(hostPath) {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime,
                Filter = "*.cs",
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            watcher.Changed += (s, e) => OnFileChanged(upperAppId, e.FullPath);
            watcher.Created += (s, e) => OnFileChanged(upperAppId, e.FullPath);
            watcher.Deleted += (s, e) => OnFileChanged(upperAppId, e.FullPath);
            watcher.Renamed += (s, e) => OnFileChanged(upperAppId, e.FullPath);

            _watchers[upperAppId] = watcher;
            DebugLogger.Log($"HotReload: Now watching {appId} at {appVirtualPath}");

        } catch (Exception ex) {
            DebugLogger.Log($"HotReload: Error starting watcher for {appId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Stop watching a specific app.
    /// </summary>
    public void StopWatching(string appId) {
        if (string.IsNullOrEmpty(appId)) return;
        string upperAppId = appId.ToUpper();

        if (_watchers.TryGetValue(upperAppId, out var watcher)) {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _watchers.Remove(upperAppId);
            DebugLogger.Log($"HotReload: Stopped watching {appId}");
        }

        if (_debounceTimers.TryGetValue(upperAppId, out var timer)) {
            timer.Stop();
            timer.Dispose();
            _debounceTimers.Remove(upperAppId);
        }

        _appPaths.Remove(upperAppId);
    }

    /// <summary>
    /// Stop watching all apps.
    /// </summary>
    public void StopAll() {
        var appIds = new List<string>(_watchers.Keys);
        foreach (var appId in appIds) {
            StopWatching(appId);
        }
    }

    /// <summary>
    /// Restart watching all previously watched apps.
    /// </summary>
    private void RestartWatching() {
        var appPaths = new Dictionary<string, string>(_appPaths);
        foreach (var kvp in appPaths) {
            StartWatching(kvp.Key, kvp.Value);
        }
    }

    private void OnFileChanged(string appId, string filePath) {
        if (!_enabled) return;

        // Debounce - reset timer on each change
        if (_debounceTimers.TryGetValue(appId, out var existingTimer)) {
            existingTimer.Stop();
            existingTimer.Start();
        } else {
            var timer = new Timer(DebounceDelayMs) {
                AutoReset = false
            };
            timer.Elapsed += (s, e) => QueueReload(appId);
            _debounceTimers[appId] = timer;
            timer.Start();
        }
    }

    private void QueueReload(string appId) {
        if (!_enabled) return;

        // Queue the reload to be processed on the main thread
        lock (_reloadLock) {
            if (!_pendingReloads.Contains(appId)) {
                _pendingReloads.Enqueue(appId);
                DebugLogger.Log($"HotReload: Queued reload for {appId}");
            }
        }

        // Clean up timer
        if (_debounceTimers.TryGetValue(appId, out var timer)) {
            timer.Dispose();
            _debounceTimers.Remove(appId);
        }
    }

    private void ExecuteReload(string appId) {
        try {
            DebugLogger.Log($"HotReload: Executing reload for {appId}...");
            
            bool success = AppLoader.Instance.ReloadApp(appId, out var diagnostics);

            if (success) {
                Shell.Notifications.Show("Hot Reload", $"{appId} reloaded successfully!");
                DebugLogger.Log($"HotReload: {appId} reloaded successfully");
            } else {
                string errors = diagnostics.Count > 0 ? string.Join("\n", diagnostics) : "Unknown error";
                Shell.Notifications.Show("Hot Reload Failed", $"{appId} compilation failed:\n{errors}");
                DebugLogger.Log($"HotReload: {appId} reload failed:");
                foreach (var diag in diagnostics) {
                    DebugLogger.Log($"  {diag}");
                }
            }

        } catch (Exception ex) {
            DebugLogger.Log($"HotReload: Exception during reload of {appId}: {ex.Message}");
            Shell.Notifications.Show("Hot Reload Error", $"Failed to reload {appId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Resets the HotReload state for a full system restart.
    /// </summary>
    public void Reset() {
        StopAll();
        _pendingReloads.Clear();
        _debounceTimers.Clear();
        _appPaths.Clear();
        DebugLogger.Log("[AppHotReloadManager] State reset for restart");
    }
}
