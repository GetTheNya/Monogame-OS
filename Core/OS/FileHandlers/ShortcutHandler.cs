using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public class ShortcutHandler : FileHandler {
    public override string Extension => ".slnk";

    public override void Execute(string virtualPath, string args, Rectangle? startBounds = null) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut == null) return;
        string target = shortcut.TargetPath;
        
        // Combine shortcut arguments with passed args
        string finalArgs = shortcut.Arguments;
        if (!string.IsNullOrEmpty(args)) {
            finalArgs = string.IsNullOrEmpty(finalArgs) ? args : finalArgs + " " + args;
        }

        string[] argArray = null;
        if (!string.IsNullOrEmpty(finalArgs)) argArray = new[] { finalArgs };

        var win = Shell.UI.CreateAppWindow(target, argArray);
        if (win != null) {
            Shell.UI.OpenWindow(win, startBounds);
            return;
        }

        if (VirtualFileSystem.Instance.Exists(target)) Shell.Execute(target, finalArgs, startBounds);
    }

    public override Texture2D GetIcon(string virtualPath) {
        string json = VirtualFileSystem.Instance.ReadAllText(virtualPath);
        var shortcut = Shortcut.FromJson(json);
        if (shortcut != null && VirtualFileSystem.Instance.Exists(shortcut.TargetPath)) return Shell.GetIcon(shortcut.TargetPath);
        return base.GetIcon(virtualPath);
    }
}
