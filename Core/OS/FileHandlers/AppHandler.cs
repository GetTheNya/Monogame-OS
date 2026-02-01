using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public class AppHandler : FileHandler {
    public override string Extension => ".sapp";

    public override void Execute(string virtualPath, Rectangle? startBounds = null) {
        string appId = GetAppId(virtualPath);
        if (string.IsNullOrEmpty(appId)) return;
        var win = Shell.UI.CreateAppWindow(appId);
        if (win != null) Shell.UI.OpenWindow(win, startBounds);
    }

    private string GetAppId(string virtualPath) {
        string manifestPath = System.IO.Path.Combine(virtualPath, "manifest.json");
        if (VirtualFileSystem.Instance.Exists(manifestPath)) {
            try {
                string json = VirtualFileSystem.Instance.ReadAllText(manifestPath);
                var manifest = AppManifest.FromJson(json);
                if (manifest != null && !string.IsNullOrEmpty(manifest.AppId)) return manifest.AppId;
            }
            catch {
            }
        }

        string pkgPath = System.IO.Path.Combine(virtualPath, "app_id.txt");
        if (VirtualFileSystem.Instance.Exists(pkgPath)) return VirtualFileSystem.Instance.ReadAllText(pkgPath)?.Trim();
        if (VirtualFileSystem.Instance.IsDirectory(virtualPath)) return null;
        return VirtualFileSystem.Instance.ReadAllText(virtualPath)?.Trim();
    }

    public override Texture2D GetIcon(string virtualPath) {
        string iconPath = System.IO.Path.Combine(virtualPath, "icon.png");
        if (VirtualFileSystem.Instance.Exists(iconPath)) {
            try {
                return Core.ImageLoader.Load(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(iconPath));
            }
            catch {
            }
        }

        string appId = GetAppId(virtualPath);
        if (appId == "EXPLORER") return GameContent.ExplorerIcon;
        return base.GetIcon(virtualPath);
    }
}
