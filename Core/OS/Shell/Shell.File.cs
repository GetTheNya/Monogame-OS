using System.Reflection;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class File {
        public static void RegisterFileTypeHandler(string extension, string appId = null) {
            // Auto-detect appId from calling assembly if not provided
            if (string.IsNullOrEmpty(appId)) {
                var callingAssembly = Assembly.GetCallingAssembly();
                appId = AppLoader.Instance.GetAppIdFromAssembly(callingAssembly);
                if (string.IsNullOrEmpty(appId)) {
                    DebugLogger.Log($"RegisterFileTypeHandler: Could not detect AppId from calling assembly");
                    return;
                }

                DebugLogger.Log($"RegisterFileTypeHandler: Auto-detected AppId: {appId}");
            }

            DebugLogger.Log($"Registering file type handler for {extension} -> {appId}");
            if (string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(appId)) return;
            extension = extension.ToLower();
            if (!extension.StartsWith(".")) extension = "." + extension;
            TheGame.Core.OS.Registry.SetValue($"{Shell.Registry.FileAssociations}\\{extension}", appId);
        }

        public static string GetFileTypeHandler(string extension) {
            if (string.IsNullOrEmpty(extension)) return null;
            extension = extension.ToLower();
            if (!extension.StartsWith(".")) extension = "." + extension;
            return TheGame.Core.OS.Registry.GetValue<string>($"{Shell.Registry.FileAssociations}\\{extension}", null);
        }
    }
}
