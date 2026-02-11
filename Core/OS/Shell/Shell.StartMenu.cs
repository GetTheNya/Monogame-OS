using System;
using System.Collections.Generic;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class StartMenu {
        public static void CreateShortcut(string targetPath, string label = null) {
            CreateShortcuts(new[] { (targetPath, label) });
        }

        public static void CreateShortcuts(IEnumerable<(string path, string label)> targets) {
            string startMenuPath = $"C:\\Users\\{SystemConfig.Username}\\Start Menu\\";

            foreach (var target in targets) {
                string path = target.path;
                string fileName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
                string shortcutLabel = target.label ?? fileName;
                
                if (target.label == null && fileName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
                    shortcutLabel = System.IO.Path.GetFileNameWithoutExtension(fileName);
                }
            
                string shortcutName = $"{shortcutLabel}.slnk";
                string menuPath = System.IO.Path.Combine(startMenuPath, shortcutName);
            
                int i = 1;
                while (VirtualFileSystem.Instance.Exists(menuPath)) {
                    menuPath = System.IO.Path.Combine(startMenuPath, $"{shortcutLabel} ({i++}).slnk");
                }
            
                string json = "{\n" +
                               $"  \"targetPath\": \"{path.Replace("\\", "\\\\")}\",\n" +
                               $"  \"label\": \"{shortcutLabel}\",\n" +
                               $"  \"iconPath\": null\n" +
                               "}";
            
                VirtualFileSystem.Instance.WriteAllText(menuPath, json);
            }
        }

        public static void CreateShortcuts(IEnumerable<string> targetPaths) {
            CreateShortcuts(System.Linq.Enumerable.Select(targetPaths, p => (p, (string)null)));
        }
    }
}
