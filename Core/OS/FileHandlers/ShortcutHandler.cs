using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public class ShortcutHandler : FileHandler {
    public override string Extension => ".slnk";

    public override void Execute(string virtualPath, Rectangle? startBounds = null) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut == null) return;
        string target = shortcut.TargetPath;
        var win = Shell.UI.CreateAppWindow(target);
        if (win != null) {
            Shell.UI.OpenWindow(win, startBounds);
            return;
        }

        if (VirtualFileSystem.Instance.Exists(target)) Shell.Execute(target, startBounds);
    }

    public override Texture2D GetIcon(string virtualPath) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut != null && VirtualFileSystem.Instance.Exists(shortcut.TargetPath)) return Shell.GetIcon(shortcut.TargetPath);
        return base.GetIcon(virtualPath);
    }
}
