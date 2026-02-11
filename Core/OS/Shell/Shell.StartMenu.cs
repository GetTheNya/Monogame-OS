using System;
using System.Collections.Generic;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class StartMenu {
        public static void CreateShortcuts(IEnumerable<string> targetPaths) {
            string startMenuPath = $"C:\\Users\\{SystemConfig.Username}\\Start Menu\\";

            foreach (var path in targetPaths) {
                string fileName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
                string shortcutLabel = fileName;
                
                if (fileName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
                    shortcutLabel = System.IO.Path.GetFileNameWithoutExtension(fileName);
                }
            
                string shortcutName = $"{shortcutLabel}.slnk";
                string menuPath = System.IO.Path.Combine(startMenuPath, shortcutName);
            
                int i = 1;
                while (VirtualFileSystem.Instance.Exists(menuPath)) {
                    menuPath = System.IO.Path.Combine(menuPath, $"{shortcutLabel} ({i++}).slnk");
                }
            
                string json = "{\n" +
                              $"  \"targetPath\": \"{path.Replace("\\", "\\\\")}\",\n" +
                              $"  \"label\": \"{shortcutLabel}\",\n" +
                              $"  \"iconPath\": null\n" +
                              "}";
            
                VirtualFileSystem.Instance.WriteAllText(menuPath, json);
            }
        }
    }
}
