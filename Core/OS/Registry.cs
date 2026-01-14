using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TheGame.Core.OS;

public static class Registry {
    private const string REGISTRY_PATH = "C:\\Windows\\System32\\config\\registry.json";
    private static JsonObject _cachedRoot;
    private static bool _isDirty;

    private static void EnsureLoaded() {
        if (_cachedRoot != null) return;

        if (VirtualFileSystem.Instance.Exists(REGISTRY_PATH)) {
            try {
                string json = VirtualFileSystem.Instance.ReadAllText(REGISTRY_PATH);
                var node = JsonNode.Parse(json);
                _cachedRoot = node as JsonObject ?? new JsonObject();
            } catch {
                _cachedRoot = new JsonObject();
            }
        } else {
            _cachedRoot = new JsonObject();
            // Ensure directory exists
            VirtualFileSystem.Instance.WriteAllText(REGISTRY_PATH, "{}");
        }
    }

    private static void Save() {
        if (!_isDirty || _cachedRoot == null) return;
        
        string json = _cachedRoot.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        VirtualFileSystem.Instance.WriteAllText(REGISTRY_PATH, json);
        _isDirty = false;
    }

    private static JsonObject NavigateToKey(string path, bool createIfMissing) {
        EnsureLoaded();
        
        // Normalize path separators
        path = path.Replace('/', '\\');
        string[] parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        JsonObject current = _cachedRoot;

        foreach (var part in parts) {
            if (current.ContainsKey(part)) {
                var next = current[part] as JsonObject;
                if (next == null) {
                    // It might be a value node, not a container. If we need to traverse through it, that's a problem (or overwrite it).
                    if (createIfMissing) {
                        next = new JsonObject();
                        current[part] = next;
                    } else {
                        return null;
                    }
                }
                current = next;
            } else {
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
    
    // Splits "path\key" into "path" and "key"
    private static void SplitPathAndKey(string fullPath, out string parentPath, out string keyName) {
        fullPath = fullPath.Replace('/', '\\');
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

    public static void SetValue<T>(string fullPath, T value) {
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);
        
        JsonObject parent = NavigateToKey(parentPath, true);
        if (parent != null) {
            // We use JsonValue.Create to handle primitives correctly
            // Note: JsonValue.Create handles int, float, bool, string, etc.
            // Complex objects usually need SerializeToNode
            
            JsonNode valNode;
            
            // Handle primitives explicitly where possible or fallback to generic
            if (value == null) {
                valNode = null;
            } else {
                // Determine if simple usage
                Type type = typeof(T);
                if (type == typeof(string) || type == typeof(int) || type == typeof(float) || 
                    type == typeof(double) || type == typeof(bool) || type == typeof(long)) {
                    valNode = JsonValue.Create(value);
                } else {
                    // Fallback for arrays/objects
                    valNode = JsonSerializer.SerializeToNode(value);
                }
            }
            
            parent[keyName] = valNode;
            _isDirty = true;
            Save();
        }
    }

    public static T GetValue<T>(string fullPath, T defaultValue = default) {
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);

        JsonObject parent = NavigateToKey(parentPath, false);
        if (parent != null && parent.ContainsKey(keyName)) {
            try {
                var node = parent[keyName];
                if (node == null) return defaultValue; // null explicitly stored
                
                // If it's a JsonValue, we might need direct extraction, 
                // but Deserialize usually handles conversion well.
                return node.Deserialize<T>();
            } catch {
                return defaultValue;
            }
        }
        
        return defaultValue;
    }

    public static bool KeyExists(string fullPath) {
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);
        
        JsonObject parent = NavigateToKey(parentPath, false);
        return parent != null && parent.ContainsKey(keyName);
    }
    
    public static void DeleteKey(string fullPath) {
        EnsureLoaded();
        SplitPathAndKey(fullPath, out string parentPath, out string keyName);
        
        JsonObject parent = NavigateToKey(parentPath, false);
        if (parent != null && parent.ContainsKey(keyName)) {
            parent.Remove(keyName);
            _isDirty = true;
            Save();
        }
    }
}
