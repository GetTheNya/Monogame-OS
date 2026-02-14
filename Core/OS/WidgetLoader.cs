using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using TheGame.Core.UI.Widgets;

namespace TheGame.Core.OS;

public class WidgetLoader {
    private static WidgetLoader _instance;
    public static WidgetLoader Instance => _instance ??= new WidgetLoader();

    private readonly Dictionary<string, Type> _widgetTypes = new();
    private readonly Dictionary<string, string> _dynamicWidgetPaths = new(); // widgetId -> .dtoy path
    private readonly Dictionary<string, Assembly> _compiledAssemblies = new(); // widgetId -> assembly

    private WidgetLoader() { 
        // Auto-scan for dynamic widgets on startup
        ScanForDynamicWidgets();
    }

    private void ScanForDynamicWidgets() {
        string path = Shell.Widgets.WidgetDirectory;
        string hostPath = VirtualFileSystem.Instance.ToHostPath(path);
        
        if (Directory.Exists(hostPath)) {
            foreach (var dir in Directory.GetDirectories(hostPath, "*.dtoy")) {
                RegisterDynamicWidget(VirtualFileSystem.Instance.ToVirtualPath(dir));
            }
        }
    }

    public void RegisterWidget<T>(string typeName) where T : Widget {
        _widgetTypes[typeName] = typeof(T);
    }

    public void RegisterDynamicWidget(string dtoyPath) {
        try {
            string hostPath = VirtualFileSystem.Instance.ToHostPath(dtoyPath);
            string manifestPath = Path.Combine(hostPath, "manifest.json");
            
            if (File.Exists(manifestPath)) {
                string json = File.ReadAllText(manifestPath);
                var manifest = WidgetManifest.FromJson(json);
                _dynamicWidgetPaths[manifest.Id] = dtoyPath;
                DebugLogger.Log($"[WidgetLoader] Registered dynamic widget: {manifest.Name} ({manifest.Id})");
            }
        } catch (Exception ex) {
            DebugLogger.Log($"[WidgetLoader] Error registering dynamic widget at {dtoyPath}: {ex.Message}");
        }
    }

    public Widget CreateWidget(string typeName, string widgetId, Vector2 position, Vector2 size) {
        // Try built-in first
        if (_widgetTypes.TryGetValue(typeName, out Type type)) {
            try {
                return (Widget)Activator.CreateInstance(type, position, size, widgetId);
            } catch (Exception ex) {
                DebugLogger.Log($"Error instantiating built-in widget '{typeName}': {ex.Message}");
                return null;
            }
        }

        // Try dynamic widgets
        if (_dynamicWidgetPaths.TryGetValue(typeName, out string dtoyPath)) {
            return LoadDynamicWidget(typeName, dtoyPath, widgetId, position, size);
        }

        DebugLogger.Log($"Warning: Unknown widget type '{typeName}' requested.");
        return null;
    }

    private Widget LoadDynamicWidget(string widgetId, string dtoyPath, string instanceId, Vector2 position, Vector2 size) {
        try {
            string hostPath = VirtualFileSystem.Instance.ToHostPath(dtoyPath);
            string manifestPath = Path.Combine(hostPath, "manifest.json");
            string manifestJson = File.ReadAllText(manifestPath);
            var manifest = WidgetManifest.FromJson(manifestJson);

            Assembly assembly;
            if (!_compiledAssemblies.TryGetValue(widgetId, out assembly)) {
                // Compile
                var sourceFiles = GatherSourceFiles(hostPath);
                assembly = AppCompiler.Instance.Compile(sourceFiles, $"Widget_{widgetId}_{DateTime.Now.Ticks}", out var diagnostics, manifest.References);
                
                if (assembly == null) {
                    DebugLogger.Log($"[WidgetLoader] Compilation failed for {widgetId}:");
                    foreach (var diag in diagnostics) DebugLogger.Log(diag.ToString());
                    return null;
                }
                _compiledAssemblies[widgetId] = assembly;
            }

            Type widgetType = assembly.GetType(manifest.WidgetClass);
            if (widgetType == null) {
                DebugLogger.Log($"[WidgetLoader] Could not find class {manifest.WidgetClass} in {widgetId}");
                return null;
            }

            Vector2 finalSize = size;
            if (finalSize == Vector2.Zero) {
                finalSize = new Vector2(manifest.DefaultSize.Width, manifest.DefaultSize.Height);
            }

            var widget = (Widget)Activator.CreateInstance(widgetType, position, finalSize, instanceId);
            
            // Apply manifest settings
            widget.RefreshPolicy = manifest.RefreshPolicy;
            widget.UpdateIntervalMs = manifest.IntervalMs;
            widget.IsResizable = manifest.IsResizable;
            
            return widget;

        } catch (Exception ex) {
            DebugLogger.Log($"[WidgetLoader] Error loading dynamic widget {widgetId}: {ex.Message}");
            return null;
        }
    }

    private Dictionary<string, string> GatherSourceFiles(string hostPath) {
        var sourceFiles = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)) {
            sourceFiles[Path.GetRelativePath(hostPath, file)] = File.ReadAllText(file);
        }
        return sourceFiles;
    }

    public IEnumerable<string> GetAvailableWidgetTypes() {
        var types = new List<string>(_widgetTypes.Keys);
        types.AddRange(_dynamicWidgetPaths.Keys);
        return types;
    }

    public WidgetManifest GetWidgetManifest(string widgetId) {
        if (_dynamicWidgetPaths.TryGetValue(widgetId, out string path)) {
            string hostPath = VirtualFileSystem.Instance.ToHostPath(path);
            string manifestPath = Path.Combine(hostPath, "manifest.json");
            if (File.Exists(manifestPath)) {
                return WidgetManifest.FromJson(File.ReadAllText(manifestPath));
            }
        }
        return null;
    }

    public void UnregisterDynamicWidget(string widgetId) {
        _dynamicWidgetPaths.Remove(widgetId);
        _compiledAssemblies.Remove(widgetId);
        DebugLogger.Log($"[WidgetLoader] Unregistered dynamic widget: {widgetId}");
    }

    public void ReloadDynamicWidgets() {
        _dynamicWidgetPaths.Clear();
        _compiledAssemblies.Clear();
        ScanForDynamicWidgets();
        DebugLogger.Log("[WidgetLoader] Reloaded all dynamic widgets.");
    }
}
