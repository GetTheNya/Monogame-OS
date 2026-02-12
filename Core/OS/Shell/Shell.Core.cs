namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Core {
        public static System.Threading.Tasks.Task<Microsoft.Xna.Framework.Graphics.Texture2D> TakeScreenshotAsync() {
            return Game1.CaptureScreenshotAsync();
        }

        public static void SetStartup(TheGame.Core.OS.Process process, bool enabled) => SetStartup(process?.AppId, enabled);
        
        public static void SetStartup(string appId, bool enabled) {
            if (string.IsNullOrEmpty(appId)) return;
            TheGame.Core.OS.Registry.Instance.SetValue($"{Shell.Registry.Startup}\\{appId.ToUpper()}", enabled);
        }

        public static bool GetStartup(TheGame.Core.OS.Process process) => GetStartup(process?.AppId);

        public static bool GetStartup(string appId) {
            if (string.IsNullOrEmpty(appId)) return false;
            return TheGame.Core.OS.Registry.Instance.GetValue($"{Shell.Registry.Startup}\\{appId.ToUpper()}", false);
        }
    }
}
