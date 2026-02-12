using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace TheGame.Core.OS;

public class Registry {
    private static Registry _instance;
    public static Registry Instance => _instance ??= new Registry();

    private const string REGISTRY_PATH = "C:\\Windows\\System32\\config\\registry.json";
    private JsonObject _cachedRoot;
    private bool _isDirty;
    
    // Valid hive roots
    private readonly string[] ValidHives = { "HKLM", "HKCU" };
    
    // Debounce timer - only save after delay to avoid disk thrashing
    private float _saveTimer = 0f;
    private const float SAVE_DELAY = 1.0f; // seconds

    private Registry() { }

    public void Initialize() {
        EnsureLoaded();
    }

    public void Update(float deltaTime) {
        if (_isDirty) {
            _saveTimer += deltaTime;
            if (_saveTimer >= SAVE_DELAY) {
                FlushToDisk();
            }
        }
    }

    private readonly object _lock = new object();
    private bool _isWriting = false;

    public void FlushToDisk() {
        string json;
        lock (_lock) {
            if (!_isDirty || _cachedRoot == null || _isWriting) return;
            json = _cachedRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = new DefaultJsonTypeInfoResolver() });
            _isDirty = false;
            _saveTimer = 0f;
            _isWriting = true;
        }

        System.Threading.Tasks.Task.Run(() => {
            string tmpPath = REGISTRY_PATH + ".tmp";
            try {
                // Atomic save: Write to temp file first
                VirtualFileSystem.Instance.WriteAllText(tmpPath, json);
                
                // Then swap (Move with overwrite)
                // We use host paths for direct File.Move if VFS Move is not atomic-swap guaranteed
                string hostTarget = VirtualFileSystem.Instance.ToHostPath(REGISTRY_PATH);
                string hostTmp = VirtualFileSystem.Instance.ToHostPath(tmpPath);
                
                if (File.Exists(hostTmp)) {
                    if (File.Exists(hostTarget)) File.Delete(hostTarget);
                    File.Move(hostTmp, hostTarget);
                    DebugLogger.Log($"[Registry] Saved successfully to {REGISTRY_PATH}");
                }
            } catch (Exception ex) {
                DebugLogger.Log($"[Registry] Error flushing to disk: {ex.Message}");
            } finally {
                lock (_lock) {
                    _isWriting = false;
                }
            }
        });
    }

    private void EnsureLoaded() {
        lock (_lock) {
            if (_cachedRoot != null) return;

            if (VirtualFileSystem.Instance.Exists(REGISTRY_PATH)) {
                string json = "";
                try {
                    json = VirtualFileSystem.Instance.ReadAllText(REGISTRY_PATH);
                    var node = JsonNode.Parse(json);
                    _cachedRoot = node as JsonObject ?? new JsonObject();
                } catch (Exception ex) {
                    DebugLogger.Log($"[Registry] CRITICAL: Failed to parse registry JSON: {ex.Message}");
                    
                    // Backup corrupted file
                    try {
                        string badPath = REGISTRY_PATH + ".bad";
                        if (VirtualFileSystem.Instance.Exists(badPath)) VirtualFileSystem.Instance.Delete(badPath);
                        VirtualFileSystem.Instance.WriteAllText(badPath, json);
                        DebugLogger.Log($"[Registry] Corrupted registry backed up to {badPath}");
                    } catch { }

                    _cachedRoot = new JsonObject();
                    MarkDirty(); // Force a clean rewrite
                }
            } else {
                _cachedRoot = new JsonObject();
                // Ensure file exists
                try {
                    VirtualFileSystem.Instance.WriteAllText(REGISTRY_PATH, "{}");
                } catch { }
            }
        }
    }

    private void MarkDirty() {
        _isDirty = true;
        _saveTimer = 0f; // Reset timer on new changes
    }

    private JsonObject NavigateToKey(string path, bool createIfMissing) {
        lock (_lock) {
            EnsureLoaded();
            
            // Normalize path separators and remove leading/trailing slashes for splitting
            path = path.Replace('/', '\\').Trim('\\');
            if (string.IsNullOrEmpty(path)) return _cachedRoot;

            string[] parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            JsonObject current = _cachedRoot;

            foreach (var part in parts) {
                // Try direct match first (fastest)
                if (current.ContainsKey(part)) {
                    var next = current[part] as JsonObject;
                    if (next == null) {
                        if (createIfMissing) {
                            next = new JsonObject();
                            current[part] = next;
                        } else {
                            return null;
                        }
                    }
                    current = next;
                } else {
                    // Try case-insensitive match
                    string matchedKey = current.Select(kvp => kvp.Key).FirstOrDefault(k => k.Equals(part, StringComparison.OrdinalIgnoreCase));
                    
                    if (matchedKey != null) {
                        var next = current[matchedKey] as JsonObject;
                        if (next != null) {
                            current = next;
                            continue;
                        }
                    }

                    if (createIfMissing) {
                        var newObj = new JsonObject();
                        current[part] = newObj;
                        current = newObj;
                    } else {
                        return null;
                    }
                }
            }

            return current;
        }
    }
    
    /// <summary>
    /// Validates that a path starts with a valid hive. Logs warning if not.
    /// </summary>
    private bool ValidateHive(string path) {
        if (string.IsNullOrEmpty(path)) return false;
        
        string normalizedPath = path.Replace('/', '\\');
        string firstPart = normalizedPath.Split('\\', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        
        if (firstPart == null) return false;
        
        foreach (var hive in ValidHives) {
            if (firstPart.Equals(hive, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
        }
        
        DebugLogger.Log($"[Registry] WARNING: Path '{path}' doesn't start with a valid hive (HKLM/HKCU). This write will be blocked.");
        return false;
    }
    
    // Splits "path\key" into "path" and "key"
    private void SplitPathAndKey(string fullPath, out string parentPath, out string keyName) {
        fullPath = fullPath.Replace('/', '\\').TrimEnd('\\');
        int lastSlash = fullPath.LastIndexOf('\\');
        
        if (lastSlash == -1) {
            // Root level assignment? unlikely but handleable
            parentPath = "";
            keyName = fullPath;
        } else {
            parentPath = fullPath.Substring(0, lastSlash);
            keyName = fullPath.Substring(lastSlash + 1);
        }
    }

    public void SetValue<T>(string fullPath, T value) {
        if (!ValidateHive(fullPath)) return; // Block root-level writes
        
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);
        
        JsonObject parent = NavigateToKey(parentPath, true);
        if (parent != null) {
            JsonNode valNode;
            if (value == null) {
                valNode = null;
            } else {
                Type type = typeof(T);
                if (type == typeof(string) || type == typeof(int) || type == typeof(float) || 
                    type == typeof(double) || type == typeof(bool) || type == typeof(long)) {
                    valNode = JsonValue.Create(value);
                } else {
                    valNode = JsonSerializer.SerializeToNode(value);
                }
            }
            
            parent[keyName] = valNode;
            MarkDirty();
        }
    }

    public void SetValue<T>(string path, string key, T value) {
        SetValue(CombinePath(path, key), value);
    }

    private string CombinePath(string path, string key) {
        if (string.IsNullOrEmpty(path)) return key;
        path = path.Replace('/', '\\');
        if (!path.EndsWith("\\")) path += "\\";
        return path + key;
    }

    public T GetValue<T>(string fullPath, T defaultValue = default) {
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);

        JsonObject parent = NavigateToKey(parentPath, false);
        if (parent != null && parent.ContainsKey(keyName)) {
            try {
                var node = parent[keyName];
                if (node == null) return defaultValue; // null explicitly stored
                return node.Deserialize<T>();
            } catch {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }

    public T GetValue<T>(string path, string key, T defaultValue = default) {
        return GetValue(CombinePath(path, key), defaultValue);
    }

    public bool KeyExists(string fullPath) {
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);
        
        JsonObject parent = NavigateToKey(parentPath, false);
        return parent != null && parent.ContainsKey(keyName);
    }

    public bool KeyExists(string path, string key) {
        return KeyExists(CombinePath(path, key));
    }
    
    public void DeleteKey(string fullPath) {
        if (!ValidateHive(fullPath)) return; // Block root-level deletes
        
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);
        
        JsonObject parent = NavigateToKey(parentPath, false);
        if (parent != null && parent.ContainsKey(keyName)) {
            parent.Remove(keyName);
            MarkDirty();
        }
    }

    /// <summary>
    /// Gets all key-value pairs at a specific path.
    /// </summary>
    public Dictionary<string, T> GetAllValues<T>(string path) {
        EnsureLoaded();
        var result = new Dictionary<string, T>();
        
        JsonObject container = NavigateToKey(path, false);
        if (container == null) return result;
        
        foreach (var kvp in container) {
            try {
                if (kvp.Value != null) {
                    T value = kvp.Value.Deserialize<T>();
                    result[kvp.Key] = value;
                }
            } catch {
                // Skip values that can't be deserialized
            }
        }
        
        return result;
    }

    public List<string> GetSubKeys(string path) {
        EnsureLoaded();
        var result = new List<string>();
        
        JsonObject container = NavigateToKey(path, false);
        if (container == null) return result;
        
        foreach (var kvp in container) {
            if (kvp.Value is JsonObject) {
                result.Add(kvp.Key);
            }
        }
        
        return result;
    }
}
