using System.Reflection;
using System.Runtime.CompilerServices;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Registry path constants for common system locations.
    /// </summary>
    public static class Registry {
        public const string HKLM = "HKLM";
        public const string HKCU = "HKCU";
        
        /// <summary>Path for file type associations: HKLM\Software\FileAssociations</summary>
        public const string FileAssociations = "HKLM\\Software\\FileAssociations";
        
        /// <summary>Path for startup apps: HKLM\Software\Startup</summary>
        public const string Startup = "HKLM\\Software\\Startup";

        /// <summary>Path for custom URI schemes: HKLM\Software\Protocols</summary>
        public const string Protocols = "HKLM\\Software\\Protocols";
        
        /// <summary>Path for desktop settings: HKCU\Desktop</summary>
        public const string Desktop = "HKCU\\Desktop";

        /// <summary>Path for audio settings: HKCU\Audio</summary>
        public const string Audio = "HKCU\\Audio";
        
        /// <summary>Path for update status: HKLM\Software\System\Update</summary>
        public const string Update = "HKLM\\Software\\System\\Update";
        
        /// <summary>Path for per-app settings: HKCU\Software\{AppId}</summary>
        public static string AppSettings(string appId) => $"HKCU\\Software\\{appId}";

        internal static void Initialize() { }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static T GetSetting<T>(string key, T defaultValue = default, string appIdOverride = null) {
            string appId = appIdOverride ?? AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return defaultValue;
            return TheGame.Core.OS.Registry.Instance.GetValue($"HKCU\\Software\\{appId}\\{key}", defaultValue);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void SetSetting<T>(string key, T value, string appIdOverride = null) {
            string appId = appIdOverride ?? AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            if (appId == null) return;
            TheGame.Core.OS.Registry.Instance.SetValue($"HKCU\\Software\\{appId}\\{key}", value);
        }
    }
}
