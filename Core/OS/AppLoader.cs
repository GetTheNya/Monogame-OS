using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TheGame.Core.UI;

namespace TheGame.Core.OS;

/// <summary>
/// Loads and executes apps from .sapp folders.
/// </summary>
public class AppLoader {
    private static AppLoader _instance;
    public static AppLoader Instance => _instance ??= new AppLoader();

    private Dictionary<string, Assembly> _compiledApps = new Dictionary<string, Assembly>();
    private Dictionary<string, string> _appPaths = new Dictionary<string, string>();

    private AppLoader() { }

    /// <summary>
    /// Loads an app from a folder, compiles it if needed, and registers it with the Shell.
    /// </summary>
    /// <param name="appFolderPath">Virtual path to .sapp folder</param>
    /// <returns>True if app loaded successfully</returns>
    public bool LoadApp(string appFolderPath) {
        try {
            string hostPath = VirtualFileSystem.Instance.ToHostPath(appFolderPath);
            if (!Directory.Exists(hostPath)) {
                DebugLogger.Log($"App folder not found: {appFolderPath}");
                return false;
            }

            // Read manifest
            string manifestPath = Path.Combine(hostPath, "manifest.json");
            if (!File.Exists(manifestPath)) {
                DebugLogger.Log($"manifest.json not found in {appFolderPath}");
                return false;
            }

            string manifestJson = File.ReadAllText(manifestPath);
            AppManifest manifest = AppManifest.FromJson(manifestJson);

            if (string.IsNullOrEmpty(manifest.AppId)) {
                DebugLogger.Log($"AppId missing in manifest: {appFolderPath}");
                return false;
            }

            // Check if already compiled
            if (_compiledApps.ContainsKey(manifest.AppId)) {
                DebugLogger.Log($"App {manifest.AppId} already loaded");
                return true;
            }

            // Gather all .cs source files
            var sourceFiles = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)) {
                string relativePath = Path.GetRelativePath(hostPath, file);
                string sourceCode = File.ReadAllText(file);
                sourceFiles[relativePath] = sourceCode;
            }

            if (sourceFiles.Count == 0) {
                // No source files - app might be hardcoded, skip silently
                return false;
            }

            // Compile
            Assembly assembly = AppCompiler.Instance.Compile(sourceFiles, manifest.AppId, out var diagnostics);
            
            if (assembly == null) {
                DebugLogger.Log($"Compilation failed for {manifest.AppId}:");
                foreach (var diag in diagnostics) {
                    DebugLogger.Log($"  {diag}");
                }
                return false;
            }

            _compiledApps[manifest.AppId] = assembly;
            _appPaths[manifest.AppId] = appFolderPath;

            // Register app factory with Shell
            Shell.RegisterApp(manifest.AppId, () => {
                return CreateWindowFromAssembly(assembly, manifest, hostPath);
            });

            DebugLogger.Log($"Successfully loaded app: {manifest.Name} ({manifest.AppId})");
            return true;

        } catch (Exception ex) {
            DebugLogger.Log($"Error loading app {appFolderPath}: {ex.Message}");
            return false;
        }
    }

    private Window CreateWindowFromAssembly(Assembly assembly, AppManifest manifest, string hostPath) {
        try {
            DebugLogger.Log($"AppLoader: Creating window for {manifest.AppId} using {manifest.EntryClass}.{manifest.EntryMethod}");
            
            // Find the entry class
            Type entryType = assembly.GetTypes().FirstOrDefault(t => t.FullName == manifest.EntryClass);
            if (entryType == null) {
                DebugLogger.Log($"AppLoader: Could not find entry class {manifest.EntryClass}");
                return null;
            }

            // Find the entry method
            MethodInfo entryMethod = entryType.GetMethod(manifest.EntryMethod, BindingFlags.Public | BindingFlags.Static);
            if (entryMethod == null) {
                DebugLogger.Log($"AppLoader: Could not find static entry method {manifest.EntryMethod} in {manifest.EntryClass}");
                return null;
            }

            // Invoke the method
            object result = entryMethod.Invoke(null, null);
            if (result is Window window) {
                window.AppId = manifest.AppId; // Set the AppId from manifest
                
                // Try to load custom icon
                string iconName = manifest.Icon ?? "icon.png";
                string iconPath = Path.Combine(hostPath, iconName);
                if (File.Exists(iconPath)) {
                    try {
                        window.Icon = ImageLoader.Load(G.GraphicsDevice, iconPath);
                    } catch (Exception ex) {
                        DebugLogger.Log($"AppLoader: Error loading icon {iconPath}: {ex.Message}");
                    }
                }

                DebugLogger.Log($"AppLoader: Successfully created window for {manifest.AppId}");
                return window;
            }
            
            DebugLogger.Log($"AppLoader: Entry method did not return a Window (returned {result?.GetType().Name ?? "null"})");
            return null;

        } catch (Exception ex) {
            DebugLogger.Log($"AppLoader: Exception during app startup for {manifest.AppId}: {ex.Message}");
            if (ex.InnerException != null) {
                DebugLogger.Log($"  Inner Error: {ex.InnerException.Message}");
                DebugLogger.Log($"  Stack: {ex.InnerException.StackTrace}");
            }
            return null;
        }
    }

    /// <summary>
    /// Loads all apps from a directory (e.g., C:\Windows\System32\)
    /// </summary>
    public void LoadAppsFromDirectory(string directoryPath) {
        string hostPath = VirtualFileSystem.Instance.ToHostPath(directoryPath);
        if (!Directory.Exists(hostPath)) return;

        foreach (var appFolder in Directory.GetDirectories(hostPath, "*.sapp")) {
            string virtualPath = VirtualFileSystem.Instance.ToVirtualPath(appFolder);
            LoadApp(virtualPath);
        }
    }

    public void UpdateAppPath(string oldPath, string newPath) {
        string oldP = oldPath.ToUpper().TrimEnd('\\');
        string newP = newPath.TrimEnd('\\');

        var keys = _appPaths.Keys.ToList();
        foreach (var appId in keys) {
            string currentPath = _appPaths[appId].ToUpper().TrimEnd('\\');
            if (currentPath == oldP) {
                _appPaths[appId] = newP;
                DebugLogger.Log($"App path updated: {appId} -> {newP}");
            } else if (currentPath.StartsWith(oldP + "\\")) {
                string relative = _appPaths[appId].Substring(oldP.Length);
                string updatedPath = newP + relative;
                _appPaths[appId] = updatedPath;
                DebugLogger.Log($"App path updated (recursive): {appId} -> {updatedPath}");
            }
        }
    }

    public string GetAppDirectory(string appId) {
        if (string.IsNullOrEmpty(appId)) return null;
        if (_appPaths.TryGetValue(appId.ToUpper(), out var path)) return path;
        return null;
    }

    public string GetAppIdFromAssembly(Assembly assembly) {
        if (assembly == null) return null;
        string name = assembly.GetName().Name;
        // Check if this assembly is one of our loaded apps
        if (_appPaths.ContainsKey(name.ToUpper())) return name.ToUpper();
        return null;
    }
}
