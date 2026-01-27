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

            string upperAppId = manifest.AppId.ToUpper();

            // Check if already compiled
            if (_compiledApps.ContainsKey(upperAppId)) {
                DebugLogger.Log($"App {manifest.AppId} already loaded");
                return true;
            }

            // Store path early so hot reload can find the app even if compilation fails
            _appPaths[upperAppId] = appFolderPath;

            // Gather all .cs source files
            var sourceFiles = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)) {
                string relativePath = Path.GetRelativePath(hostPath, file);
                string sourceCode = File.ReadAllText(file);
                sourceFiles[relativePath] = sourceCode;
            }

            if (sourceFiles.Count == 0) {
                // No source files - app might be hardcoded, skip silently
                _appPaths.Remove(upperAppId);
                return false;
            }

            // Start hot reload watching BEFORE compilation - so failed apps can be fixed
            AppHotReloadManager.Instance.StartWatching(upperAppId, appFolderPath);

            // Compile
            Assembly assembly = AppCompiler.Instance.Compile(sourceFiles, manifest.AppId, out var diagnostics);
            
            if (assembly == null) {
                DebugLogger.Log($"Compilation failed for {manifest.AppId}:");
                foreach (var diag in diagnostics) {
                    DebugLogger.Log($"  {diag}");
                }
                // Don't return false - hot reload is still watching
                return false;
            }

            _compiledApps[upperAppId] = assembly;

            // Register app factory with Shell
            Shell.UI.RegisterApp(upperAppId, (args) => {
                return CreateWindowFromAssembly(assembly, manifest, hostPath, args);
            });

            DebugLogger.Log($"Successfully loaded app: {manifest.Name} ({manifest.AppId})");
            return true;

        } catch (Exception ex) {
            DebugLogger.Log($"Error loading app {appFolderPath}: {ex.Message}");
            return false;
        }
    }

    private Window CreateWindowFromAssembly(Assembly assembly, AppManifest manifest, string hostPath, string[] args) {
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

            // Try to invoke with args first (new signature), fall back to no args (old signature)
            object result = null;
            try {
                result = entryMethod.Invoke(null, new object[] { args });
            } catch (TargetParameterCountException) {
                // Old apps don't accept args, try calling with no parameters
                try {
                    result = entryMethod.Invoke(null, null);
                } catch (Exception ex) {
                    DebugLogger.Log($"AppLoader: Failed to invoke {manifest.EntryMethod}: {ex.Message}");
                    return null;
                }
            }
            
            // Handle Window-returning apps (legacy)
            if (result is Window window) {
                window.AppId = manifest.AppId; // Set the AppId from manifest
                
                // Create a Process to own this window
                var windowProcess = new Process {
                    AppId = manifest.AppId.ToUpper()
                };
                windowProcess.Windows.Add(window);
                windowProcess.MainWindow = window;
                window.OwnerProcess = windowProcess;
                ProcessManager.Instance.RegisterProcess(windowProcess);
                
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

                DebugLogger.Log($"AppLoader: Successfully created window for {manifest.AppId} (Process: {windowProcess.ProcessId})");
                return window;
            }
            
            // Handle Application-returning apps (modern)
            if (result is Application app) {
                app.Process = new Process {
                    AppId = manifest.AppId.ToUpper(),
                    Application = app
                };
                ProcessManager.Instance.RegisterProcess(app.Process);
                
                // Initialize the app (calls Application.Initialize)
                try {
                    app.Process.Initialize(args);
                } catch (Exception ex) {
                    if (CrashHandler.IsAppException(ex, app.Process)) {
                        CrashHandler.HandleAppException(app.Process, ex);
                    } else {
                        throw;
                    }
                }
                
                if (app.MainWindow != null) {
                    app.MainWindow.AppId = manifest.AppId;
                    DebugLogger.Log($"AppLoader: Application created MainWindow: {app.MainWindow.Title}");
                }

                DebugLogger.Log($"AppLoader: Successfully started application for {manifest.AppId} (Process: {app.Process.ProcessId})");
                return app.MainWindow;
            }

            // Handle Process-returning apps (true background processes)
            if (result is Process process) {
                process.AppId = manifest.AppId.ToUpper();
                ProcessManager.Instance.RegisterProcess(process);
                
                // Start the process (calls OnStart)
                try {
                    process.Initialize(args); // Use modern Initialize instead of OnStart
                } catch (Exception ex) {
                    if (CrashHandler.IsAppException(ex, process)) {
                        CrashHandler.HandleAppException(process, ex);
                    } else {
                        throw;
                    }
                }
                
                DebugLogger.Log($"AppLoader: Successfully started background process for {manifest.AppId} (Process: {process.ProcessId})");
                return null; // Background processes don't return a window
            }
            
            DebugLogger.Log($"AppLoader: Entry method did not return a Window or Process (returned {result?.GetType().Name ?? "null"})");
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

    /// <summary>
    /// Reloads an app by recompiling its source and updating the assembly.
    /// Closes all running instances of the app.
    /// </summary>
    public bool ReloadApp(string appId, out List<string> diagnostics) {
        diagnostics = new List<string>();
        
        if (string.IsNullOrEmpty(appId)) {
            diagnostics.Add("AppId is null or empty");
            return false;
        }

        string upperAppId = appId.ToUpper();

        if (!_appPaths.TryGetValue(upperAppId, out string appVirtualPath)) {
            diagnostics.Add($"App {appId} is not loaded");
            return false;
        }

        try {
            // Close all running instances
            CloseAllInstances(upperAppId);

            string hostPath = VirtualFileSystem.Instance.ToHostPath(appVirtualPath);
            if (!Directory.Exists(hostPath)) {
                diagnostics.Add($"App folder not found: {appVirtualPath}");
                return false;
            }

            // Read manifest
            string manifestPath = Path.Combine(hostPath, "manifest.json");
            if (!File.Exists(manifestPath)) {
                diagnostics.Add($"manifest.json not found in {appVirtualPath}");
                return false;
            }

            string manifestJson = File.ReadAllText(manifestPath);
            AppManifest manifest = AppManifest.FromJson(manifestJson);

            // Gather all .cs source files
            var sourceFiles = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)) {
                string relativePath = Path.GetRelativePath(hostPath, file);
                string sourceCode = File.ReadAllText(file);
                sourceFiles[relativePath] = sourceCode;
            }

            if (sourceFiles.Count == 0) {
                diagnostics.Add("No source files found");
                return false;
            }

            // Recompile with a unique assembly name to avoid conflicts
            string assemblyName = $"{manifest.AppId}_{DateTime.Now.Ticks}";
            Assembly assembly = AppCompiler.Instance.Compile(sourceFiles, assemblyName, out diagnostics);
            
            if (assembly == null) {
                return false;
            }

            // Update the compiled assembly
            _compiledApps[upperAppId] = assembly;

            // Re-register app factory with Shell
            Shell.UI.RegisterApp(upperAppId, (args) => {
                return CreateWindowFromAssembly(assembly, manifest, hostPath, args);
            });

            DebugLogger.Log($"Successfully reloaded app: {manifest.Name} ({upperAppId})");
            return true;

        } catch (Exception ex) {
            diagnostics.Add($"Exception during reload: {ex.Message}");
            DebugLogger.Log($"Error reloading app {appId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Close all running instances of an app (uses ProcessManager).
    /// </summary>
    private void CloseAllInstances(string appId) {
        if (string.IsNullOrEmpty(appId)) return;
        
        var processes = ProcessManager.Instance.GetProcessesByApp(appId);
        foreach (var process in processes) {
            process.Terminate();
        }
    }

    /// <summary>
    /// Get all running windows of an app (uses ProcessManager).
    /// </summary>
    public List<Window> GetRunningInstances(string appId) {
        if (string.IsNullOrEmpty(appId)) return new List<Window>();
        
        var processes = ProcessManager.Instance.GetProcessesByApp(appId);
        return processes.SelectMany(p => p.Windows).ToList();
    }
}
