using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheGame.Core.OS;

namespace NACHOS;

public static class SnippetManager {
    private static Dictionary<string, SnippetItem> _snippets = new();
    private static string _snippetsDir = @"C:\Windows\System32\NACHOS.sapp\Templates\Snippets";
    private static DateTime _lastLoadTime = DateTime.MinValue;

    public static List<SnippetItem> GetSnippets() {
        EnsureLoaded();
        return _snippets.Values.ToList();
    }

    public static void EnsureLoaded() {
        // In this environment, we use VirtualFileSystem
        if (!VirtualFileSystem.Instance.IsDirectory(_snippetsDir)) return;

        // Simple debounce/cache check
        // VirtualFileSystem doesn't have LastWriteTime for directories easily, so we just reload if empty or on some trigger
        if (_snippets.Count > 0) return;

        _snippets.Clear();
        var files = VirtualFileSystem.Instance.GetFiles(_snippetsDir);
        foreach (var file in files) {
            if (file.EndsWith(".txt") || file.EndsWith(".snippet")) {
                var content = VirtualFileSystem.Instance.ReadAllText(file);
                var snippet = ParseSnippet(Path.GetFileNameWithoutExtension(file), content, file);
                if (snippet != null) {
                    _snippets[snippet.Shortcut] = snippet;
                }
            }
        }
    }

    private static SnippetItem ParseSnippet(string shortcut, string content, string filePath) {
        string title = shortcut;
        string description = "";
        string body = "";

        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int separatorIndex = -1;
        for (int i = 0; i < lines.Length; i++) {
            if (lines[i].Trim() == "---") {
                separatorIndex = i;
                break;
            }
        }

        if (separatorIndex != -1) {
            for (int i = 0; i < separatorIndex; i++) {
                var line = lines[i];
                if (line.StartsWith("title:", StringComparison.OrdinalIgnoreCase)) {
                    title = line.Substring(6).Trim();
                } else if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase)) {
                    description = line.Substring(12).Trim();
                }
            }
            body = string.Join("\n", lines.Skip(separatorIndex + 1));
        } else {
            body = content;
        }

        return new SnippetItem(shortcut, title, description, body, filePath);
    }
}
