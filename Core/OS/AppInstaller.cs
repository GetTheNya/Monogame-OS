using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace TheGame.Core.OS;

public enum InstallPhase {
    Downloading,
    Extracting,
    Installing,
    Finalizing,
    Failed,
    Complete
}

public class InstallProgress {
    public InstallPhase Phase { get; set; }
    public float Progress { get; set; }
    public string Message { get; set; }
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public int CurrentIndex { get; set; }
    public int TotalCount { get; set; }
}

public struct InstallRequest {
    public string AppId;
    public string Name;
    public string DownloadUrl;
    public string Version;
    public bool IsTerminalOnly;
}

public class AppInstaller {
    private static AppInstaller _instance;
    public static AppInstaller Instance => _instance ??= new AppInstaller();

    private const string DefaultProgramFiles = "C:\\Program Files\\";
    private const string TempDownloadPath = "C:\\Temp\\Downloads\\";
    private const string TempExtractPath = "C:\\Temp\\NewVersion\\";

    private HashSet<string> _installedAppsCache;
    private Dictionary<string, string> _versionCache;
    private bool _cacheValid = false;

    private AppInstaller() { }

    /// <summary>
    /// Forces a refresh of the installed apps and version cache.
    /// </summary>
    public void RefreshCache() {
        RefreshCacheAsync().Wait();
    }

    /// <summary>
    /// Asynchronously refreshes the installed apps and version cache.
    /// </summary>
    public async Task RefreshCacheAsync() {
        var installedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var versions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        await Task.Run(() => {
            // Scan common registry paths for installed apps
            var subKeys = Registry.Instance.GetSubKeys("HKLM\\Software\\HentOS\\InstalledApps");
            DebugLogger.Log($"[AppInstaller] Found {subKeys.Count} apps in registry.");
            
            foreach (var subKeyName in subKeys) {
                installedApps.Add(subKeyName);
                
                var version = Registry.Instance.GetValue<string>($"HKLM\\Software\\HentOS\\InstalledApps\\{subKeyName}", "Version", null);
                if (!string.IsNullOrEmpty(version)) {
                    versions[subKeyName] = version;
                    DebugLogger.Log($"[AppInstaller] Cache: {subKeyName} v{version}");
                } else {
                    DebugLogger.Log($"[AppInstaller] Warning: {subKeyName} has no version in registry.");
                }
            }
        });

        _installedAppsCache = installedApps;
        _versionCache = versions;
        _cacheValid = true;
    }

    /// <summary>
    /// Cleans up registry entries for apps that are no longer present on disk.
    /// </summary>
    public async Task CleanupRegistryAsync() {
        await Task.Run(() => {
            var subKeys = Registry.Instance.GetSubKeys("HKLM\\Software\\HentOS\\InstalledApps");
            foreach (var appId in subKeys) {
                string path = Registry.Instance.GetValue<string>($"HKLM\\Software\\HentOS\\InstalledApps\\{appId}", "InstallPath", null);
                
                // If path is set but doesn't exist, it's an orphan
                if (!string.IsNullOrEmpty(path)) {
                    if (!VirtualFileSystem.Instance.Exists(path)) {
                        DebugLogger.Log($"[AppInstaller] Cleaning orphaned registry entry for: {appId} (Path not found: {path})");
                        Registry.Instance.DeleteKey($"HKLM\\Software\\HentOS\\InstalledApps\\{appId}");
                    }
                } else {
                     DebugLogger.Log($"[AppInstaller] App {appId} has no InstallPath, skipping cleanup.");
                }
            }
        });
        
        // Invalidate cache since we modified registry
        _cacheValid = false;
    }

    /// <summary>
    /// Gets the default installation directory from registry or fallback.
    /// </summary>
    public string GetDefaultInstallPath(bool terminalOnly = false) {
        if (terminalOnly) {
            DebugLogger.Log($"[AppInstaller] Returning default path for terminal app: C:\\Program Files\\TerminalApps");
            return "C:\\Program Files\\TerminalApps";
        }
        
        string path = Registry.Instance.GetValue<string>("HKLM\\Software\\HentOS\\AppInstaller", "DefaultInstallPath", null);
        if (string.IsNullOrEmpty(path)) {
            DebugLogger.Log($"[AppInstaller] No custom default install path found in registry. Using default: C:\\Program Files");
            return "C:\\Program Files";
        }
        DebugLogger.Log($"[AppInstaller] Using custom default install path from registry: {path}");
        return path;
    }

    /// <summary>
    /// Checks if an app is installed by looking in the registry or AppLoader paths.
    /// </summary>
    public bool IsAppInstalled(string appId) {
        if (string.IsNullOrEmpty(appId)) return false;

        // 1. Check AppLoader (Memory) - This includes currently JIT-loaded system apps
        if (AppLoader.Instance.GetAppDirectory(appId) != null) {
             // DebugLogger.Log($"[AppInstaller] {appId} is installed (found in AppLoader)");
             return true;
        }

        // 2. Check Registry Cache (Installed via Store)
        if (_cacheValid && _installedAppsCache != null) {
            if (_installedAppsCache.Contains(appId)) return true;
        }
        
        // 3. Last resort: direct registry check
        var path = Registry.Instance.GetValue($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}", "InstallPath", (string)null);
        if (!string.IsNullOrEmpty(path)) {
             DebugLogger.Log($"[AppInstaller] {appId} is installed (found in Registry: {path})");
             return true;
        }

        return false;
    }

    /// <summary>
    /// Downloads and installs a new app or updates an existing one.
    /// </summary>
    public async Task<bool> InstallAppAsync(string appId, string name, string downloadUrl, string version, Process process, string customInstallPath = null, IProgress<InstallProgress> progress = null, bool isTerminalOnly = false) {
        try {
            bool isUpdate = IsAppInstalled(appId);
            string installBase = customInstallPath ?? GetDefaultInstallPath(isTerminalOnly);
            string targetPath = Path.Combine(installBase, $"{appId}.sapp");
            
            DebugLogger.Log($"[AppInstaller] Starting install of {name} ({appId}). Target: {targetPath}, IsUpdate: {isUpdate}");
            
            InstallNotification installNotif = null;
            if (progress == null) {
                installNotif = new InstallNotification(
                    isUpdate ? $"Updating {name}" : $"Installing {name}",
                    "Starting..."
                );
                NotificationManager.Instance.AddNotification(installNotif);
                
                var internalProgress = new Progress<InstallProgress>(p => {
                    installNotif.UpdateProgress(p.Progress, p.Message);
                });
                progress = internalProgress;
            }

            progress?.Report(new InstallProgress { Phase = InstallPhase.Downloading, Progress = 0, Message = "Starting download..." });

            // 1. Prepare temp paths
            string zipFile = Path.Combine(TempDownloadPath, $"{appId}_{DateTime.Now.Ticks}.zip");
            VirtualFileSystem.Instance.CreateDirectory(TempDownloadPath);
            VirtualFileSystem.Instance.CreateDirectory(TempExtractPath);

            // 2. Download
            var downloadProgress = new Progress<float>(p => {
                progress?.Report(new InstallProgress { 
                    Phase = InstallPhase.Downloading, 
                    Progress = p * 0.8f, // 80% for download
                    Message = $"Downloading... {(int)(p * 100)}%" 
                });
            });

            await NetworkManager.Instance.DownloadToFileAsync(process, downloadUrl, zipFile, downloadProgress);

            // 3. Extract
            progress?.Report(new InstallProgress { Phase = InstallPhase.Extracting, Progress = 0.85f, Message = "Extracting files..." });
            string extractDir = Path.Combine(TempExtractPath, appId);
            string hostExtract = VirtualFileSystem.Instance.ToHostPath(extractDir);
            string hostZip = VirtualFileSystem.Instance.ToHostPath(zipFile);

            if (Directory.Exists(hostExtract)) {
                Directory.Delete(hostExtract, true);
            }
            Directory.CreateDirectory(hostExtract);

            DebugLogger.Log($"[AppInstaller] Extracting {hostZip} to {hostExtract}");
            await Task.Run(() => {
                try {
                    ZipFile.ExtractToDirectory(hostZip, hostExtract, true);
                    DebugLogger.Log($"[AppInstaller] ZipFile.ExtractToDirectory finished.");
                } catch (Exception ex) {
                    DebugLogger.Log($"[AppInstaller] Zip extraction FATAL error: {ex.Message}");
                    throw;
                }
            });
            DebugLogger.Log($"[AppInstaller] Extraction task await complete.");

            // 4. Validate and Find App Folder
            DebugLogger.Log($"[AppInstaller] Validating extract directory: {extractDir}");
            string sourceAppDir = extractDir;
            string[] subDirs = VirtualFileSystem.Instance.GetDirectories(extractDir);
            string sappSubDir = subDirs.FirstOrDefault(d => d.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase));
            
            if (sappSubDir != null) {
                sourceAppDir = sappSubDir;
                DebugLogger.Log($"[AppInstaller] Found .sapp subfolder in ZIP: {sourceAppDir}");
            }

            string manifestPath = Path.Combine(sourceAppDir, "manifest.json");
            DebugLogger.Log($"[AppInstaller] Checking for manifest: {manifestPath}");
            if (!VirtualFileSystem.Instance.Exists(manifestPath)) {
                DebugLogger.Log($"[AppInstaller] Manifest NOT found at {manifestPath}");
                // Check if there's a nested folder with the same name as the appId (common zip behavior)
                string nestedDir = Path.Combine(extractDir, appId);
                if (VirtualFileSystem.Instance.Exists(nestedDir)) {
                     DebugLogger.Log($"[AppInstaller] Trying nested directory: {nestedDir}");
                     sourceAppDir = nestedDir;
                     manifestPath = Path.Combine(sourceAppDir, "manifest.json");
                }
                
                if (!VirtualFileSystem.Instance.Exists(manifestPath)) {
                    throw new Exception("Invalid app package: manifest.json missing.");
                }
            }
            DebugLogger.Log($"[AppInstaller] Manifest found at {manifestPath}");

            // 5. Deploy (Atomic Swap if update)
            progress?.Report(new InstallProgress { Phase = InstallPhase.Installing, Progress = 0.95f, Message = "Installing..." });
            DebugLogger.Log($"[AppInstaller] Deploying from {sourceAppDir} to {targetPath}");

            // Give the OS a moment to release handles after extraction (AV scanning, etc)
            await Task.Delay(200);

            if (isUpdate) {
                // Stop any hot reloading watchers before we touch the directory
                AppHotReloadManager.Instance.StopWatching(appId);
                await PerformAtomicUpdate(appId, sourceAppDir, targetPath);
            } else {
                VirtualFileSystem.Instance.CreateDirectory(installBase);
                
                try {
                    VirtualFileSystem.Instance.Move(sourceAppDir, targetPath);
                } catch (Exception ex) {
                    DebugLogger.Log($"[AppInstaller] Move failed ({ex.Message}), trying Copy + Delete...");
                    // Fallback: Copy then Delete (slower but more robust across folders/locks)
                    VirtualFileSystem.Instance.Copy(sourceAppDir, targetPath);
                    VirtualFileSystem.Instance.Delete(sourceAppDir);
                }
            }
            DebugLogger.Log($"[AppInstaller] Deployment complete.");

            // 6. Registry Registration
            Registry.Instance.SetValue($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}", "InstallPath", targetPath);
            Registry.Instance.SetValue($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}", "Version", version);
            Registry.Instance.SetValue($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}", "Name", name);

            // Add search path to AppLoader
            AppLoader.Instance.AddSearchPath(installBase);

            // 7. Load / Reload
            if (isUpdate) {
                AppLoader.Instance.ReloadApp(appId, out _);
            } else {
                AppLoader.Instance.LoadApp(targetPath);
            }

            // 8. Invalidate cache
            _cacheValid = false;
            
            // 9. Cleanup
            VirtualFileSystem.Instance.Delete(zipFile);
            if (VirtualFileSystem.Instance.Exists(extractDir)) {
                VirtualFileSystem.Instance.Delete(extractDir);
                DebugLogger.Log($"[AppInstaller] Cleaned up extraction directory: {extractDir}");
            }
            
            progress?.Report(new InstallProgress { Phase = InstallPhase.Complete, Progress = 1, Message = "Installation successful!" });
            return true;

        } catch (Exception ex) {
            DebugLogger.Log($"[AppInstaller] Error: {ex.Message}");
            progress?.Report(new InstallProgress { Phase = InstallPhase.Failed, Message = ex.Message });
            return false;
        }
    }

    /// <summary>
    /// Uninstalls an app by removing its files and registry entries.
    /// </summary>
    public async Task<bool> UninstallAppAsync(string appId, string name) {
        try {
            DebugLogger.Log($"[AppInstaller] Uninstalling {name} ({appId})...");

            // 1. Get install path
            string installPath = GetInstalledPath(appId);
            if (string.IsNullOrEmpty(installPath)) {
                DebugLogger.Log($"[AppInstaller] Warning: No install path found for {appId}. Manual cleanup may be required.");
            }

            // 2. Close running instances and stop hotreload
            AppHotReloadManager.Instance.StopWatching(appId);
            AppLoader.Instance.CloseAllInstances(appId);
            await Task.Delay(500);

            // 3. Delete from disk if possible
            if (!string.IsNullOrEmpty(installPath) && VirtualFileSystem.Instance.Exists(installPath)) {
                try {
                    // Safety check: don't delete system roots or core apps
                    if (installPath.StartsWith("C:\\Windows", StringComparison.OrdinalIgnoreCase)) {
                        DebugLogger.Log($"[AppInstaller] Blocked uninstallation of system app from {installPath}");
                        throw new Exception("Cannot uninstall system applications.");
                    }

                    VirtualFileSystem.Instance.Delete(installPath);
                    DebugLogger.Log($"[AppInstaller] Deleted {installPath}");
                } catch (Exception ex) {
                    DebugLogger.Log($"[AppInstaller] Failed to delete files: {ex.Message}");
                    // Continue anyway to clean registry if it was just a file lock
                    if (ex.Message.Contains("System")) throw; 
                }
            }

            // 4. Remove registry keys
            Registry.Instance.DeleteKey($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}");
            
            // 5. Unregister from AppLoader memory
            AppLoader.Instance.UnregisterApp(appId);
            
            _cacheValid = false;
            DebugLogger.Log($"[AppInstaller] Uninstall of {name} complete.");
            Shell.Notifications.Show("HentHub", $"{name} has been uninstalled.");
            return true;
        } catch (Exception ex) {
            DebugLogger.Log($"[AppInstaller] Uninstall error: {ex.Message}");
            Shell.Notifications.Show("HentHub", $"Error uninstalling {name}: {ex.Message}");
            return false;
        }
    }

    private async Task PerformAtomicUpdate(string appId, string sourceDir, string targetPath) {
        string oldPath = targetPath + ".OLD";

        // Close running instances
        AppLoader.Instance.CloseAllInstances(appId);
        await Task.Delay(500); // Give it a moment to release file handles

        try {
            // Swap
            if (VirtualFileSystem.Instance.Exists(targetPath)) {
                DebugLogger.Log($"[AppInstaller] Target path exists, swiping to backup: {oldPath}");
                if (VirtualFileSystem.Instance.Exists(oldPath)) {
                    VirtualFileSystem.Instance.Delete(oldPath);
                }
                VirtualFileSystem.Instance.Move(targetPath, oldPath);
            }

            DebugLogger.Log($"[AppInstaller] Moving extraction to final destination: {targetPath}");
            VirtualFileSystem.Instance.Move(sourceDir, targetPath);
        } catch (Exception ex) {
            // Fallback move: Copy + Delete
            try {
                DebugLogger.Log($"[AppInstaller] Atomic move failed ({ex.Message}), trying Copy + Delete fallback...");
                VirtualFileSystem.Instance.Copy(sourceDir, targetPath);
                VirtualFileSystem.Instance.Delete(sourceDir);
            } catch (Exception fatalEx) {
                // Rollback
                DebugLogger.Log($"[AppInstaller] Update failed even with fallback, rolling back: {fatalEx.Message}");
                if (VirtualFileSystem.Instance.Exists(oldPath)) {
                    if (VirtualFileSystem.Instance.Exists(targetPath)) VirtualFileSystem.Instance.Delete(targetPath);
                    VirtualFileSystem.Instance.Move(oldPath, targetPath);
                }
                throw;
            }
        }

        // Success - delete backup
        if (VirtualFileSystem.Instance.Exists(oldPath)) {
            DebugLogger.Log($"[AppInstaller] Cleaning up backup: {oldPath}");
            VirtualFileSystem.Instance.Delete(oldPath);
        }
    }

    public string GetInstalledVersion(string appId) {
        if (string.IsNullOrEmpty(appId)) return null;

        // 1. Check registry cache first
        if (_cacheValid && _versionCache != null && _versionCache.TryGetValue(appId, out string version)) {
            return version;
        }

        // 2. Check AppLoader (System/Loaded apps)
        string loadedVer = AppLoader.Instance.GetAppVersion(appId);
        if (!string.IsNullOrEmpty(loadedVer)) return loadedVer;

        // 3. Direct registry fallback
        return Registry.Instance.GetValue($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}", "Version", (string)null);
    }

    public string GetInstalledPath(string appId) {
        if (string.IsNullOrEmpty(appId)) return null;

        // 1. Check registry first
        string regPath = Registry.Instance.GetValue($"HKLM\\Software\\HentOS\\InstalledApps\\{appId.ToUpper()}", "InstallPath", (string)null);
        if (!string.IsNullOrEmpty(regPath)) return regPath;

        // 2. Check AppLoader (System apps)
        return AppLoader.Instance.GetAppDirectory(appId);
    }

    /// <summary>
    /// Finds all installed apps that depend on the specified appId.
    /// appsMetadata: A list of (AppId, Name, Dependencies) for ALL known store apps.
    /// </summary>
    public List<string> GetDependents(string appId, IEnumerable<(string Id, string Name, List<string> Deps)> appsMetadata) {
        var dependents = new List<string>();
        if (string.IsNullOrEmpty(appId)) return dependents;

        string targetId = appId.ToUpper();
        foreach (var app in appsMetadata) {
            if (app.Id.ToUpper() == targetId) continue;
            
            // Only check apps that are actually installed
            if (!IsAppInstalled(app.Id)) continue;

            if (app.Deps != null && app.Deps.Any(d => d.ToUpper() == targetId)) {
                dependents.Add(app.Name);
            }
        }
        return dependents;
    }

    /// <summary>
    /// Checks if a list of dependency app IDs are installed.
    /// Returns the list of missing dependencies.
    /// </summary>
    public List<string> CheckDependencies(IEnumerable<string> dependencies) {
        var missing = new List<string>();
        if (dependencies == null) return missing;
        
        foreach (var dep in dependencies) {
            if (!IsAppInstalled(dep)) {
                missing.Add(dep);
            }
        }
        return missing;
    }

    public static bool IsNewerVersion(string current, string remote) {
        if (string.IsNullOrEmpty(remote)) return false;
        if (string.IsNullOrEmpty(current) || current == "0.0.0") return true; // Force update if no version or invalid
        
        try {
            var v1 = new Version(current);
            var v2 = new Version(remote);
            return v2 > v1;
        } catch {
            // If parsing fails but remote exists, assume remote is "newer" if they aren't identical
            return current != remote;
        }
    }

    /// <summary>
    /// Installs multiple apps in sequence.
    /// </summary>
    public async Task<bool> InstallAppsAsync(List<InstallRequest> requests, Process process, string customInstallPath = null) {
        if (requests == null || requests.Count == 0) return true;

        if (requests.Count == 1) {
            var r = requests[0];
            return await InstallAppAsync(r.AppId, r.Name, r.DownloadUrl, r.Version, process, customInstallPath, isTerminalOnly: r.IsTerminalOnly);
        }

        InstallNotification installNotif = new InstallNotification(
            $"Installing {requests.Count} apps",
            "Initializing queue..."
        );
        NotificationManager.Instance.AddNotification(installNotif);

        for (int i = 0; i < requests.Count; i++) {
            var req = requests[i];
            int currentStep = i + 1;
            int totalSteps = requests.Count;

            var progress = new Progress<InstallProgress>(p => {
                float overallProgress = (i + p.Progress) / totalSteps;
                string msg = $"[{currentStep}/{totalSteps}] {req.Name}: {p.Message}";
                installNotif.UpdateProgress(overallProgress, msg);
            });

            bool success = await InstallAppAsync(req.AppId, req.Name, req.DownloadUrl, req.Version, process, customInstallPath, progress, isTerminalOnly: req.IsTerminalOnly);
            if (!success) {
                installNotif.UpdateProgress(0, $"Failed to install {req.Name}. Queue aborted.");
                return false;
            }
        }

        return true;
    }
}
