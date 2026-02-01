using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.UI;
using Microsoft.Xna.Framework;

namespace TheGame.Core.OS;

public class VirtualFileSystem {
    private static VirtualFileSystem _instance;
    public static VirtualFileSystem Instance => _instance ??= new VirtualFileSystem();

    private string _hostRoot;
    
    // Cached recycle bin state to avoid disk I/O every frame
    private bool? _isRecycleBinEmptyCache = null;

    public void Initialize(string hostRoot) {
        // Register Apps first so they are available for shortcuts
        // All apps are now loaded dynamically from C:\Windows\System32\ via AppLoader

        _hostRoot = Path.GetFullPath(hostRoot);
        if (!Directory.Exists(_hostRoot)) Directory.CreateDirectory(_hostRoot);
        
        // Ensure C drive exists
        string cPath = Path.Combine(_hostRoot, "C");
        if (!Directory.Exists(cPath)) Directory.CreateDirectory(cPath);

        // Ensure default structure
        string users = Path.Combine(cPath, "Users", "Admin");
        if (!Directory.Exists(users)) Directory.CreateDirectory(users);

        string desktop = Path.Combine(users, "Desktop");
        if (!Directory.Exists(desktop)) Directory.CreateDirectory(desktop);

        string docs = Path.Combine(users, "Documents");
        if (!Directory.Exists(docs)) Directory.CreateDirectory(docs);
        string recycle = Path.Combine(cPath, "$Recycle.Bin");
        if (!Directory.Exists(recycle)) Directory.CreateDirectory(recycle);

        string appData = Path.Combine(users, "AppData", "Local");
        if (!Directory.Exists(appData)) Directory.CreateDirectory(appData);
        
        string windows = Path.Combine(cPath, "Windows", "System32");
        if (!Directory.Exists(windows)) Directory.CreateDirectory(windows);

        // Load all apps from System32 using AppLoader
        AppLoader.Instance.LoadAppsFromDirectory("C:\\Windows\\System32\\");
		AppLoader.Instance.LoadAppsFromDirectory("C:\\Windows\\System32\\TerminalApps\\");
    }

    private void EnsureFile(string path, string content) {
        string dir = Path.GetDirectoryName(path);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, content);
    }

    private void EnsureBinaryFile(string path, string sourceResourcePath) {
        if (!File.Exists(path)) {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            if (File.Exists(sourceResourcePath)) {
                File.Copy(sourceResourcePath, path);
            }
        }
    }

    public string ToHostPath(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return null;

        // Normalize: handle slashes and ensure no trailing slash (unless it's a drive root like C:\)
        virtualPath = virtualPath.Replace('/', '\\').Trim();
        if (virtualPath.EndsWith("\\") && virtualPath.Length > 3) {
            virtualPath = virtualPath.TrimEnd('\\');
        }
        
        string drive;
        string rest = "";

        int colonIdx = virtualPath.IndexOf(':');
        if (colonIdx != -1) {
            drive = virtualPath.Substring(0, colonIdx);
            if (colonIdx + 1 < virtualPath.Length) {
                rest = virtualPath.Substring(colonIdx + 1).TrimStart('\\');
            }
        } else {
            return null;
        }

        return Path.Combine(_hostRoot, drive, rest);
    }

    public string ToVirtualPath(string hostPath) {
        string relative = Path.GetRelativePath(_hostRoot, hostPath);
        int separatorIdx = relative.IndexOf(Path.DirectorySeparatorChar);
        
        if (separatorIdx == -1) {
             return relative.ToUpper() + ":\\";
        }

        string drive = relative.Substring(0, separatorIdx).ToUpper();
        string rest = relative.Substring(separatorIdx + 1);
        
        return drive + ":\\" + rest;
    }

    public string[] GetDrives() {
        if (!Directory.Exists(_hostRoot)) return Array.Empty<string>();
        return Directory.GetDirectories(_hostRoot)
            .Select(d => Path.GetFileName(d).ToUpper() + ":\\")
            .ToArray();
    }

    public string[] GetDirectories(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        if (string.IsNullOrEmpty(hostPath) || !Directory.Exists(hostPath)) return Array.Empty<string>();
        
        return Directory.GetDirectories(hostPath)
            .Select(ToVirtualPath)
            .ToArray();
    }

    public string[] GetFiles(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        if (string.IsNullOrEmpty(hostPath) || !Directory.Exists(hostPath)) return Array.Empty<string>();
        
        return Directory.GetFiles(hostPath)
            .Select(ToVirtualPath)
            .ToArray();
    }

    public void CreateDirectory(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        if (!string.IsNullOrEmpty(hostPath)) {
            Directory.CreateDirectory(hostPath);
        }
    }

    public void CreateFile(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        if (string.IsNullOrEmpty(hostPath)) return;

        string dir = Path.GetDirectoryName(hostPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        if (!File.Exists(hostPath)) {
            File.Create(hostPath).Dispose();
        }
    }

    public bool IsDirectory(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        return Directory.Exists(hostPath);
    }

    public string NormalizePath(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return "C:\\";
        
        // Normalize slashes
        string p = virtualPath.Replace('/', '\\').Trim();
        if (p.EndsWith("\\") && p.Length > 3) p = p.TrimEnd('\\');

        string[] parts = p.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new Stack<string>();

        foreach (var part in parts) {
            if (part == ".") continue;
            if (part == "..") {
                if (stack.Count > 1) stack.Pop();
                continue;
            }
            stack.Push(part);
        }

        string result = string.Join("\\", stack.Reverse());
        if (result.Length == 2 && result.EndsWith(":")) result += "\\";
        if (!result.Contains(":")) result = "C:\\" + result; // Default to C: if drive missing

        return result;
    }

    public string ResolvePath(string currentDir, string targetPath) {
        if (string.IsNullOrEmpty(targetPath)) return currentDir;
        
        targetPath = targetPath.Replace('/', '\\');
        
        string combined;
        if (targetPath.Contains(':')) {
            combined = targetPath;
        } else {
            combined = Path.Combine(currentDir, targetPath);
        }

        return NormalizePath(combined);
    }

    public string GetActualCasing(string virtualPath) {
        string normalized = NormalizePath(virtualPath);
        string hostPath = ToHostPath(normalized);
        if (string.IsNullOrEmpty(hostPath)) return normalized;

        if (Directory.Exists(hostPath) || File.Exists(hostPath)) {
            // Traverse down from root to get actual casing for each part
            string drive = normalized.Substring(0, 3); // C:\
            string rest = normalized.Substring(3);
            if (string.IsNullOrEmpty(rest)) return drive;

            string currentHost = ToHostPath(drive);
            string[] parts = rest.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            List<string> resultParts = new List<string>();

            foreach (var part in parts) {
                var entry = Directory.GetFileSystemEntries(currentHost)
                    .FirstOrDefault(e => Path.GetFileName(e).Equals(part, StringComparison.OrdinalIgnoreCase));
                
                if (entry != null) {
                    string actualName = Path.GetFileName(entry);
                    resultParts.Add(actualName);
                    currentHost = Path.Combine(currentHost, actualName);
                } else {
                    // Fallback to what user typed if not found
                    resultParts.Add(part);
                    currentHost = Path.Combine(currentHost, part);
                }
            }

            return drive + string.Join("\\", resultParts);
        }

        return normalized;
    }

    public bool Exists(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        return File.Exists(hostPath) || Directory.Exists(hostPath);
    }
    
    public string ReadAllText(string virtualPath) {
        string hostPath = ToHostPath(virtualPath);
        if (File.Exists(hostPath)) return File.ReadAllText(hostPath);
        return null;
    }

    public void WriteAllText(string virtualPath, string content) {
        string hostPath = ToHostPath(virtualPath);
        string dir = Path.GetDirectoryName(hostPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(hostPath, content);
    }

    public void Delete(string virtualPath) {
        if (IsSystemProtectedPath(virtualPath)) {
            DebugLogger.Log($"Access Denied: Cannot delete system path '{virtualPath}'");
            return;
        }

        string hostPath = ToHostPath(virtualPath);
        if (File.Exists(hostPath)) File.Delete(hostPath);
        else if (Directory.Exists(hostPath)) Directory.Delete(hostPath, true);
    }

    private bool IsSystemProtectedPath(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return false;
        string norm = virtualPath.Replace('/', '\\').TrimEnd('\\').ToUpper();
        
        // Root drives
        if (norm.Length <= 2 && norm.EndsWith(":")) return true;
        if (norm.Length == 3 && norm.EndsWith(":\\")) return true;

        string[] protectedPaths = { 
            "C:", "C:\\", 
            "C:\\WINDOWS", 
            "C:\\USERS", 
            "C:\\USERS\\ADMIN", 
            "C:\\$RECYCLE.BIN" 
        };
        
        return protectedPaths.Any(p => norm == p);
    }

    public void Move(string sourceVirtualPath, string destVirtualPath) {
        string sourceHost = ToHostPath(sourceVirtualPath);
        string destHost = ToHostPath(destVirtualPath);
        
        if (string.IsNullOrEmpty(sourceHost) || string.IsNullOrEmpty(destHost)) return;
        
        // Normalize for comparison
        string s = sourceHost.ToUpper().TrimEnd(Path.DirectorySeparatorChar);
        string d = destHost.ToUpper().TrimEnd(Path.DirectorySeparatorChar);
        
        if (s == d) return;

        if (IsSystemProtectedPath(sourceVirtualPath)) {
             DebugLogger.Log($"Access Denied: Cannot move or rename system directory '{sourceVirtualPath}'");
             return;
        }

        // Prevent moving a directory into itself or its own subdirectory
        if (d.StartsWith(s + Path.DirectorySeparatorChar)) {
            DebugLogger.Log("Cannot move a directory into its own subdirectory.");
            return;
        }

        // Close any windows browsing this path before moving/deleting
        Shell.CloseExplorers(sourceVirtualPath);

        string destDir = Path.GetDirectoryName(destHost);
        if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

        if (File.Exists(sourceHost)) {
            if (File.Exists(destHost)) File.Delete(destHost);
            File.Move(sourceHost, destHost);
        } else if (Directory.Exists(sourceHost)) {
            if (Directory.Exists(destHost)) Directory.Delete(destHost, true);
            Directory.Move(sourceHost, destHost);

            // Notify AppLoader about path changes (recursively updates any .sapp paths inside)
            AppLoader.Instance.UpdateAppPath(sourceVirtualPath, destVirtualPath);
        }

        // Clean up trash info if moving OUT of recycle bin
        if (s.Contains("$RECYCLE.BIN") && !d.Contains("$RECYCLE.BIN")) {
            RemoveTrashInfo(sourceVirtualPath);
        }

        // Trigger refreshes if moving into/out of Recycle Bin
        if (s.Contains("$RECYCLE.BIN") || d.Contains("$RECYCLE.BIN")) {
            InvalidateRecycleBinCache();
            Shell.RefreshDesktop?.Invoke();
            Shell.RefreshExplorers();
        }
    }

    private void RemoveTrashInfo(string trashVirtualPath) {
        string metadataPath = "C:\\$Recycle.Bin\\$trash_info.json";
        if (!Exists(metadataPath)) return;
        try {
            string json = ReadAllText(metadataPath);
            var info = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (info != null) {
                string normalized = trashVirtualPath.Replace('/', '\\').TrimEnd('\\');
                string fileName = Path.GetFileName(normalized);
                if (info.Remove(fileName)) {
                    WriteAllText(metadataPath, System.Text.Json.JsonSerializer.Serialize(info));
                }
            }
        } catch { }
    }

    public void Recycle(string virtualPath) {
        if (string.IsNullOrEmpty(virtualPath)) return;

        // Trim trailing slash for directories so Path.GetFileName works
        string normalizedPath = virtualPath.Replace('/', '\\').TrimEnd('\\');
        if (normalizedPath.Length <= 2 && normalizedPath.Contains(':')) return; // Don't recycle drives
        
        // Don't recycle system folders
        if (IsSystemProtectedPath(virtualPath)) return;

        if (normalizedPath.ToUpper().Contains("$RECYCLE.BIN")) {
            // Already in recycle bin or IS the bin
            return;
        }

        string fileName = Path.GetFileName(normalizedPath);
        string recycleBin = "C:\\$Recycle.Bin\\";
        string destPath = Path.Combine(recycleBin, fileName);
        
        // Handle name collisions in recycle bin
        int i = 1;
        string baseName = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        while (Exists(destPath)) {
            destPath = Path.Combine(recycleBin, $"{baseName} ({i++}){ext}");
        }

        // Store original path metadata before moving
        SaveTrashInfo(destPath, virtualPath);
        
        Move(normalizedPath, destPath);
        InvalidateRecycleBinCache();
        Shell.RefreshDesktop?.Invoke();
    }

    private void SaveTrashInfo(string trashVirtualPath, string originalVirtualPath) {
        string metadataPath = "C:\\$Recycle.Bin\\$trash_info.json";
        Dictionary<string, string> info = new();
        
        if (Exists(metadataPath)) {
            try {
                string json = ReadAllText(metadataPath);
                if (!string.IsNullOrEmpty(json)) {
                    info = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
                }
            } catch { }
        }

        string fileName = Path.GetFileName(trashVirtualPath);
        info[fileName] = originalVirtualPath;

        try {
            string newJson = System.Text.Json.JsonSerializer.Serialize(info);
            WriteAllText(metadataPath, newJson);
        } catch { }
    }

    public void EmptyRecycleBin() {
        string trashPath = "C:\\$Recycle.Bin\\";
        string hostPath = ToHostPath(trashPath);
        if (!Directory.Exists(hostPath)) return;

        // Delete all files and directories inside (even hidden ones)
        foreach (var file in Directory.GetFiles(hostPath)) File.Delete(file);
        foreach (var dir in Directory.GetDirectories(hostPath)) Directory.Delete(dir, true);
        
        InvalidateRecycleBinCache();
        // Metadata is already deleted by the above if it was in that folder
    }

    public string GetOriginalPath(string trashVirtualPath) {
        string metadataPath = "C:\\$Recycle.Bin\\$trash_info.json";
        if (!Exists(metadataPath)) return null;

        try {
            string json = ReadAllText(metadataPath);
            var info = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            
            // Normalize path to get accurate filename (handles trailing slashes)
            string normalized = trashVirtualPath.Replace('/', '\\').TrimEnd('\\');
            string fileName = Path.GetFileName(normalized);
            
            if (info != null && info.TryGetValue(fileName, out var original)) return original;
        } catch { }

        return null;
    }

    public void Restore(string trashVirtualPath) {
        string normalized = trashVirtualPath.Replace('/', '\\').TrimEnd('\\');
        string originalPath = GetOriginalPath(normalized);
        if (string.IsNullOrEmpty(originalPath)) {
            // Fallback: Restore to Desktop if original path unknown
            originalPath = "C:\\Users\\Admin\\Desktop\\" + Path.GetFileName(trashVirtualPath);
        }

        // Handle collisions in destination
        string restorePath = originalPath;
        if (Exists(restorePath)) {
             int i = 1;
             string dir = Path.GetDirectoryName(originalPath);
             string name = Path.GetFileNameWithoutExtension(originalPath);
             string ext = Path.GetExtension(originalPath);
             while (Exists(restorePath)) {
                 restorePath = Path.Combine(dir, $"{name} ({i++}){ext}");
             }
        }

        Move(normalized, restorePath);
        InvalidateRecycleBinCache();
        
        // Clean up metadata
        string metadataPath = "C:\\$Recycle.Bin\\$trash_info.json";
        if (Exists(metadataPath)) {
            try {
                string json = ReadAllText(metadataPath);
                var info = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (info != null) {
                    info.Remove(Path.GetFileName(normalized));
                    WriteAllText(metadataPath, System.Text.Json.JsonSerializer.Serialize(info));
                }
            } catch { }
        }
    }

    public void RestoreAll() {
        string trashPath = "C:\\$Recycle.Bin\\";
        var files = GetFiles(trashPath);
        var dirs = GetDirectories(trashPath);

        foreach (var file in files) {
            // Skip metadata file
            if (file.EndsWith("$trash_info.json")) continue;
            Restore(file);
        }
        foreach (var dir in dirs) Restore(dir);
    }

    public bool IsRecycleBinEmpty() {
        // Return cached value if available to avoid disk I/O every frame
        if (_isRecycleBinEmptyCache.HasValue) return _isRecycleBinEmptyCache.Value;
        
        string trashPath = "C:\\$Recycle.Bin\\";
        // Ignore metadata file $trash_info.json
        var files = GetFiles(trashPath);
        var dirs = GetDirectories(trashPath);
        
        int fileCount = 0;
        foreach (var f in files) {
            if (!f.EndsWith("$trash_info.json", StringComparison.OrdinalIgnoreCase)) fileCount++;
        }
        
        _isRecycleBinEmptyCache = fileCount == 0 && dirs.Length == 0;
        return _isRecycleBinEmptyCache.Value;
    }
    
    public void InvalidateRecycleBinCache() {
        _isRecycleBinEmptyCache = null;
    }

    public string GetAppHomeDirectory(string appId) {
        if (string.IsNullOrEmpty(appId)) return null;
        string path = Path.Combine("C:\\Users\\Admin\\AppData\\Local", appId);
        CreateDirectory(path);
        return path;
    }

    public string GetAppBundleDirectory(string appId) {
        return AppLoader.Instance.GetAppDirectory(appId);
    }

    public string GetAppResourcePath(string appId, string resourceName) {
        string bundle = GetAppBundleDirectory(appId);
        if (bundle == null) return null;
        return Path.Combine(bundle, resourceName);
    }
}
