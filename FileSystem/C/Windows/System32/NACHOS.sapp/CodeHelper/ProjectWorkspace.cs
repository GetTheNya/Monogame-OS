using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;

namespace NACHOS;

public static class ProjectWorkspace {
    public static Dictionary<string, string> GetSources(string projectPath, Func<IEnumerable<(string Path, string Content)>> getOpenEditors) {
        var sources = new Dictionary<string, string>();
        
        // 1. Load everything from disk first
        if (!string.IsNullOrEmpty(projectPath) && VirtualFileSystem.Instance.Exists(projectPath)) {
            var files = VirtualFileSystem.Instance.GetFilesRecursive(projectPath, "*.cs");
            foreach (var file in files) {
                try {
                    sources[file] = VirtualFileSystem.Instance.ReadAllText(file);
                } catch {
                    // Skip files that can't be read
                }
            }
        }

        // 2. Override with open editor content (which might be unsaved)
        var openEditors = getOpenEditors?.Invoke();
        if (openEditors != null) {
            foreach (var editor in openEditors) {
                if (!string.IsNullOrEmpty(editor.Path) && editor.Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) {
                    sources[editor.Path] = editor.Content;
                }
            }
        }

        return sources;
    }

    public static IEnumerable<string> GetReferences(string projectPath) {
        if (string.IsNullOrEmpty(projectPath)) return Array.Empty<string>();

        try {
            string manifestPath = Path.Combine(projectPath, "manifest.json");
            if (VirtualFileSystem.Instance.Exists(manifestPath)) {
                var manifest = AppManifest.FromJson(VirtualFileSystem.Instance.ReadAllText(manifestPath));
                return manifest.References ?? Array.Empty<string>();
            }
        } catch {
            // Fallback
        }

        return Array.Empty<string>();
    }
}
