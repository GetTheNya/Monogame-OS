namespace TheGame.Core.OS;

public static partial class Shell {
    public static class Core {
        public static System.Threading.Tasks.Task<Microsoft.Xna.Framework.Graphics.Texture2D> TakeScreenshotAsync() {
            return Game1.CaptureScreenshotAsync();
        }

        public static void SetStartup(TheGame.Core.OS.Process process, bool enabled) {
            var appId = process.AppId;
            if (string.IsNullOrEmpty(appId)) return;
            TheGame.Core.OS.Registry.SetValue($"{Shell.Registry.Startup}\\{appId.ToUpper()}", enabled);
        }

        public static bool GetStartup(TheGame.Core.OS.Process process) {
            var appId = process.AppId;           
            if (string.IsNullOrEmpty(appId)) return false;
            return TheGame.Core.OS.Registry.GetValue($"{Shell.Registry.Startup}\\{appId.ToUpper()}", false);
        }
    }
}
