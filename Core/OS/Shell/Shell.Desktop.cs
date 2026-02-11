using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Desktop {
        /// <summary>
        /// Gets the next free position for an icon on the desktop. 
        /// Returns Vector2.Zero if desktop is not active or no spot found.
        /// Optional localOccupied set can be provided for batch operations.
        /// </summary>
        public static Func<Vector2?, HashSet<(int x, int y)>, Vector2> GetNextFreePosition;

        /// <summary>
        /// Manually sets/saves an icon's position for a specific virtual path.
        /// </summary>
        public static Action<string, Vector2> SetIconPosition;

        /// <summary>
        /// Creates a desktop shortcut for the specified target path.
        /// </summary>
        public static void CreateShortcut(string targetPath, string label = null) {
            CreateShortcuts(new[] { targetPath });
        }

        /// <summary>
        /// Creates multiple desktop shortcuts for the specified target paths.
        /// Handles anti-overlap positioning for the entire batch.
        /// </summary>
        public static void CreateShortcuts(IEnumerable<string> targetPaths) {
            string desktopPath = $"C:\\Users\\{SystemConfig.Username}\\Desktop\\";
            int createdCount = 0;
            var localOccupied = new HashSet<(int x, int y)>();

            foreach (var path in targetPaths) {
                string fileName = System.IO.Path.GetFileName(path.TrimEnd('\\'));
                string shortcutLabel = fileName;
                
                if (fileName.EndsWith(".sapp", StringComparison.OrdinalIgnoreCase)) {
                    shortcutLabel = System.IO.Path.GetFileNameWithoutExtension(fileName);
                }

                string shortcutName = $"{shortcutLabel} - Shortcut.slnk";
                string destPath = System.IO.Path.Combine(desktopPath, shortcutName);

                int i = 1;
                while (VirtualFileSystem.Instance.Exists(destPath)) {
                    destPath = System.IO.Path.Combine(desktopPath, $"{shortcutLabel} - Shortcut ({i++}).slnk");
                }

                // Get position and track it locally for this batch
                Vector2 pos = GetNextFreePosition?.Invoke(null, localOccupied) ?? Vector2.Zero;
                
                // Note: localOccupied is now updated by GetNextFreePosition handler directly
                // to avoid fragile hardcoded grid math here.

                string json = "{\n" +
                              $"  \"targetPath\": \"{path.Replace("\\", "\\\\")}\",\n" +
                              $"  \"label\": \"{shortcutLabel}\",\n" +
                              $"  \"iconPath\": null\n" +
                              "}";

                VirtualFileSystem.Instance.WriteAllText(destPath, json);
                
                if (pos != Vector2.Zero) {
                    SetIconPosition?.Invoke(destPath, pos);
                }
                createdCount++;
            }

            if (createdCount > 0) {
                Notifications.Show("Success", $"Created {createdCount} shortcut(s) on the desktop.");
                RefreshDesktop?.Invoke();
            }
        }
    }
}
