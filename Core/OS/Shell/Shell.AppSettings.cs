using System.Text.Json;

namespace TheGame.Core.OS;

public static partial class Shell {
    public static class AppSettings {
        public static void Save<T>(TheGame.Core.OS.Process process, T settings) {
            if (process == null) return;
            string appId = process.AppId;
            if (appId == null) return;
            string dir = VirtualFileSystem.Instance.GetAppHomeDirectory(appId);
            string path = System.IO.Path.Combine(dir, "settings.json");
            string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            VirtualFileSystem.Instance.WriteAllText(path, json);
        }

        public static T Load<T>(TheGame.Core.OS.Process process) where T : new() {
            if (process == null) return new T();
            string appId = process.AppId;
            if (appId == null) return new T();
            string dir = VirtualFileSystem.Instance.GetAppHomeDirectory(appId);
            string path = System.IO.Path.Combine(dir, "settings.json");

            string json = null;
            if (VirtualFileSystem.Instance.Exists(path)) json = VirtualFileSystem.Instance.ReadAllText(path);
            else {
                string bundlePath = VirtualFileSystem.Instance.GetAppResourcePath(appId, "settings.json");
                if (VirtualFileSystem.Instance.Exists(bundlePath)) json = VirtualFileSystem.Instance.ReadAllText(bundlePath);
            }

            if (json == null) return new T();
            try {
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch {
                return new T();
            }
        }
    }
}
