using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.Xna.Framework.Graphics;
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
            var sourceFiles = GatherSourceFiles(hostPath);

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
                DebugLogger.Log($"[JIT Compile] Compilation failed for {manifest.AppId}:");
                var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError);
                    
                foreach (var diag in errors) {
                    DiagnosticFormatter.LogDiagnostic(diag);
                }
                
                // Don't return false - hot reload is still watching
                return false;
            }

            _compiledApps[upperAppId] = assembly;

            // Register app factory with Shell
            Shell.UI.RegisterApp(upperAppId, (args, setup) => {
                return CreateWindowFromAssembly(assembly, manifest, appFolderPath, args, setup);
            });

            DebugLogger.Log($"Successfully loaded app: {manifest.Name} ({manifest.AppId})");
            return true;

        } catch (Exception ex) {
            DebugLogger.Log($"Error loading app {appFolderPath}: {ex.Message}");
            return false;
        }
    }

    private Window CreateWindowFromAssembly(Assembly assembly, AppManifest manifest, string appVirtualPath, string[] args, Action<Process> setup = null) {
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

            // Try to load custom icon early
            Texture2D processIcon = null;
            string iconName = manifest.Icon ?? "icon.png";
            string iconVirtualPath = VirtualFileSystem.Instance.ResolvePath(appVirtualPath, iconName);
            
            if (VirtualFileSystem.Instance.Exists(iconVirtualPath)) {
                try {
                    string iconHostPath = VirtualFileSystem.Instance.ToHostPath(iconVirtualPath);
                    processIcon = ImageLoader.Load(G.GraphicsDevice, iconHostPath);
                } catch (Exception ex) {
                    DebugLogger.Log($"AppLoader: Error loading icon {iconVirtualPath}: {ex.Message}");
                }
            }
            
            // Handle Window-returning apps (legacy)
            if (result is Window window) {
                window.AppId = manifest.AppId; // Set the AppId from manifest
                
                // Create a Process to own this window
                var windowProcess = new Process {
                    AppId = manifest.AppId.ToUpper(),
                    Icon = processIcon,
                };
                windowProcess.Windows.Add(window);
                windowProcess.MainWindow = window;
                window.OwnerProcess = windowProcess;
                
                // Call setup before RegisterProcess
                setup?.Invoke(windowProcess);
                
                ProcessManager.Instance.RegisterProcess(windowProcess);
                
                // Explicitly set window icon if provided
                if (processIcon != null && window.Icon == null) {
                    window.Icon = processIcon;
                }

                DebugLogger.Log($"AppLoader: Successfully created window for {manifest.AppId} (Process: {windowProcess.ProcessId})");
                return window;
            }
            
            // Handle Application-returning apps (modern)
            if (result is Application app) {
                app.Process = new Process {
                    AppId = manifest.AppId.ToUpper(),
                    Application = app,
                    Icon = processIcon,
                    IsAsync = app.IsAsync
                };

                // Call setup before Initialize
                setup?.Invoke(app.Process);

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
                    
                    // Link window and process
                    if (app.MainWindow.OwnerProcess == null) app.MainWindow.OwnerProcess = app.Process;
                    if (app.Process.MainWindow == null) app.Process.MainWindow = app.MainWindow;
                    if (!app.Process.Windows.Contains(app.MainWindow)) app.Process.Windows.Add(app.MainWindow);
                    
                    // Explicitly set window icon if provided and no icon is set
                    if (processIcon != null && app.MainWindow.Icon == null) {
                        app.MainWindow.Icon = processIcon;
                    }
                    
                    DebugLogger.Log($"AppLoader: Application created MainWindow: {app.MainWindow.Title}");
                }

                DebugLogger.Log($"AppLoader: Successfully started application for {manifest.AppId} (Process: {app.Process.ProcessId})");
                return app.MainWindow;
            }

            // Handle Process-returning apps (true background processes)
            if (result is Process process) {
                process.AppId = manifest.AppId.ToUpper();
                process.Icon = processIcon;

                // Call setup before Initialize
                setup?.Invoke(process);

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

    public string GetAppIdFromPath(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return null;
        string normalized = virtualPath.Replace("/", "\\").TrimEnd('\\').ToUpper();
        
        foreach (var kvp in _appPaths) {
            string appPath = kvp.Value.Replace("/", "\\").TrimEnd('\\').ToUpper();
            if (appPath == normalized) return kvp.Key;
        }
        return null;
    }

    public string GetAppIdFromAssembly(Assembly assembly) {
        if (assembly == null) return null;
        string name = assembly.GetName().Name;
        // Check if this assembly is one of our loaded apps
        if (_appPaths.ContainsKey(name.ToUpper())) return name.ToUpper();
        return null;
    }

    private Dictionary<string, string> GatherSourceFiles(string hostPath) {
        var sourceFiles = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)) {
            string relativePath = Path.GetRelativePath(hostPath, file);
            string sourceCode = File.ReadAllText(file);
            sourceFiles[relativePath] = sourceCode;
        }
        return sourceFiles;
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
            var sourceFiles = GatherSourceFiles(hostPath);
            if (sourceFiles.Count == 0) {
                diagnostics.Add("No source files found in the app directory.");
                return false;
            }

            // Recompile with a unique assembly name to avoid conflicts
            string assemblyName = $"{manifest.AppId}_{DateTime.Now.Ticks}";
            Assembly assembly = AppCompiler.Instance.Compile(sourceFiles, assemblyName, out IEnumerable<Diagnostic> compileDiagnostics);
            
            if (assembly == null) {
                // Format diagnostics nicely for the output list
                foreach (var diag in compileDiagnostics) {
                    diagnostics.AddRange(DiagnosticFormatter.Format(diag));
                }
                return false;
            }

            // Update the compiled assembly
            _compiledApps[upperAppId] = assembly;

            // Re-register app factory with Shell
            Shell.UI.RegisterApp(upperAppId, (args, setup) => {
                return CreateWindowFromAssembly(assembly, manifest, appVirtualPath, args, setup);
            });

            DebugLogger.Log($"[AppLoader] Successfully reloaded app: {manifest.Name} ({upperAppId})");
            return true;

        } catch (Exception ex) {
            diagnostics.Add($"Exception during reload: {ex.Message}");
            DebugLogger.Log($"[AppLoader] Error reloading app {appId}: {ex.Message}");
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

public static class DiagnosticFormatter {
    /// <summary>
    /// Formats a diagnostic into a multi-line "pretty" string array.
    /// Includes header, source line, and a pointer to the column.
    /// </summary>
    public static IEnumerable<string> Format(Diagnostic diag) {
        var lines = new List<string>();
        var lineSpan = diag.Location.GetLineSpan();
        int lineIndex = lineSpan.StartLinePosition.Line;
        int columnIndex = lineSpan.StartLinePosition.Character;

        // Header format: File(1,1): Severity ID: Message
        string fileName = lineSpan.Path ?? "Source";
        string header = $"{fileName}({lineIndex + 1},{columnIndex + 1}): {diag.Severity} {diag.Id}: {diag.GetMessage()}";
        lines.Add(header);

        // Try to get the actual source code line
        var sourceText = diag.Location.SourceTree?.GetText();
        if (sourceText != null && lineIndex >= 0 && lineIndex < sourceText.Lines.Count) {
            string lineContent = sourceText.Lines[lineIndex].ToString();
            
            // Handle tabs by replacing them with spaces so the pointer stays aligned
            string visualLine = lineContent.Replace("\t", "    ");
            
            // Calculate real offset for the pointer considering tab replacement (assuming 4 spaces per tab)
            int tabsBefore = lineContent.Substring(0, Math.Max(0, Math.Min(columnIndex, lineContent.Length))).Count(c => c == '\t');
            int visualColumn = columnIndex + (tabsBefore * 3); // +3 spaces for each tab replaced

            int errorLength = diag.Location.SourceSpan.Length;
            string underlines = errorLength > 1 ? new string('~', errorLength - 1) : "";
            string pointerWithText = new string(' ', visualColumn) + "^" + underlines + " <--- Error here";

            lines.Add(visualLine);
            lines.Add(pointerWithText);
        }

        return lines;
    }

    public static void LogDiagnostic(Diagnostic diag) {
        foreach (var line in Format(diag)) {
            DebugLogger.Log(line);
        }
    }
}
