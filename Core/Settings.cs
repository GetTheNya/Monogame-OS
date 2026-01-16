using System;
using TheGame.Core.OS;

namespace TheGame.Core;

/// <summary>
/// System-wide settings for the operating system.
/// Stores settings in HKLM\System\ registry path.
/// </summary>
public static class Settings {
    
    /// <summary>
    /// Personalization settings (wallpaper, theme, colors, etc.)
    /// </summary>
    public static class Personalization {
        /// <summary>
        /// Callback invoked when wallpaper settings change.
        /// </summary>
        public static Action OnWallpaperChanged;
        
        private const string BASE_PATH = "HKLM\\System\\Personalization";
        
        /// <summary>
        /// Path to wallpaper image in virtual file system.
        /// Default: C:\Windows\Web\Wallpaper\img0.jpg
        /// </summary>
        public static string WallpaperPath {
            get => Registry.GetValue($"{BASE_PATH}\\WallpaperPath", "C:\\Windows\\Web\\Wallpaper\\img0.jpg");
            set {
                Registry.SetValue($"{BASE_PATH}\\WallpaperPath", value);
                OnWallpaperChanged?.Invoke();
            }
        }
        
        /// <summary>
        /// Wallpaper draw mode: Fill, Fit, Stretch, Tile, or Center.
        /// Default: Fill
        /// </summary>
        public static string WallpaperDrawMode {
            get => Registry.GetValue($"{BASE_PATH}\\WallpaperDrawMode", "Fill");
            set {
                Registry.SetValue($"{BASE_PATH}\\WallpaperDrawMode", value);
                // No callback needed - draw mode change doesn't require reloading texture
            }
        }
    }
    
    // Future: Add other system settings here
    // public static class Display { ... }
    // public static class Sound { ... }
    // public static class Network { ... }
}
