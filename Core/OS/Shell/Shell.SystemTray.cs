using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// System tray icon management.
    /// </summary>
    public static class SystemTray {
        private static TheGame.Core.UI.SystemTray _systemTray;
        
        /// <summary>
        /// Connects the Shell.SystemTray API to the actual SystemTray instance.
        /// Called by Taskbar during initialization.
        /// </summary>
        internal static void Initialize(TheGame.Core.UI.SystemTray systemTray) {
            _systemTray = systemTray;
        }
        
        /// <summary>
        /// Adds a tray icon owned by a window.
        /// The icon will be automatically removed when the window closes (unless PersistAfterWindowClose is true).
        /// </summary>
        public static string AddIcon(TheGame.Core.UI.Window ownerWindow, TheGame.Core.UI.TrayIcon icon) {
            if (icon == null || _systemTray == null) return null;
            if (ownerWindow == null) {
                DebugLogger.Log("[Shell.SystemTray] AddIcon called with null window");
                return null;
            }
            
            icon.OwnerWindow = ownerWindow;
            icon.OwnerProcess = ownerWindow.OwnerProcess;
            
            // Hook window close event to remove icon (unless PersistAfterWindowClose)
            ownerWindow.OnClosed += () => _systemTray?.RemoveIconsForWindow(ownerWindow);
            
            _systemTray.AddIcon(icon);
            return icon.Id;
        }
        
        /// <summary>
        /// Adds a tray icon owned by a process (for background services without windows).
        /// The icon will be automatically removed when the process terminates.
        /// </summary>
        public static string AddIcon(TheGame.Core.OS.Process ownerProcess, TheGame.Core.UI.TrayIcon icon) {
            if (icon == null || _systemTray == null) return null;
            if (ownerProcess == null) {
                DebugLogger.Log("[Shell.SystemTray] AddIcon called with null process");
                return null;
            }
            
            icon.OwnerProcess = ownerProcess;
            
            _systemTray.AddIcon(icon);
            return icon.Id;
        }
        
        /// <summary>
        /// Removes a tray icon by ID.
        /// </summary>
        public static bool RemoveIcon(string id) {
            return _systemTray?.RemoveIcon(id) ?? false;
        }
        
        /// <summary>
        /// Removes all tray icons owned by the specified process.
        /// Called automatically when a process terminates.
        /// </summary>
        internal static void RemoveIconsForProcess(TheGame.Core.OS.Process process) {
            _systemTray?.RemoveIconsForProcess(process);
        }
        
        /// <summary>
        /// Gets a tray icon by ID for dynamic updates.
        /// </summary>
        public static TheGame.Core.UI.TrayIcon GetIcon(string id) {
            return _systemTray?.GetIcon(id);
        }
        
        /// <summary>
        /// Updates the icon texture for a tray icon.
        /// </summary>
        public static void UpdateIcon(string id, Texture2D newIcon) {
            _systemTray?.GetIcon(id)?.SetIcon(newIcon);
        }
        
        /// <summary>
        /// Updates the tooltip for a tray icon.
        /// </summary>
        public static void UpdateTooltip(string id, string newTooltip) {
            _systemTray?.GetIcon(id)?.SetTooltip(newTooltip);
        }
    }
}
