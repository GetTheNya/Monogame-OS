using System;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.IO;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Graphics;
using TheGame;

namespace NACHOS;

public static class FileIconHelper {
    private static Dictionary<string, Texture2D> _icons = new();

    public static void Initialize(GraphicsDevice gd) {
        string iconDir = "C:\\Windows\\System32\\NACHOS.sapp\\Content\\Icons";
        string hostIconDir = VirtualFileSystem.Instance.ToHostPath(iconDir);

        if (VirtualFileSystem.Instance.IsDirectory(iconDir)) {
            LoadIcon(gd, iconDir, "audio.png", ".wav", ".mp3", ".ogg");
            LoadIcon(gd, iconDir, "c_sharp.png", ".cs");
            LoadIcon(gd, iconDir, "image.png", ".png", ".jpg", ".jpeg", ".bmp", ".gif");
            LoadIcon(gd, iconDir, "json.png", ".json");
            LoadIcon(gd, iconDir, "layout.png", ".uilayout");
        }
    }

    private static void LoadIcon(GraphicsDevice gd, string vDir, string filename, params string[] extensions) {
        string vPath = Path.Combine(vDir, filename);
        if (VirtualFileSystem.Instance.Exists(vPath)) {
            string hostPath = VirtualFileSystem.Instance.ToHostPath(vPath);
            var tex = ImageLoader.Load(gd, hostPath);
            foreach (var ext in extensions) {
                _icons[ext.ToLower()] = tex;
            }
        }
    }

    public static Texture2D GetIcon(string path) {
        if (VirtualFileSystem.Instance.IsDirectory(path) && !path.EndsWith(".uilayout", StringComparison.OrdinalIgnoreCase)) return GameContent.FolderIcon;
        
        string ext = Path.GetExtension(path).ToLower();
        if (_icons.TryGetValue(ext, out var icon)) return icon;
        
        return GameContent.FileIcon;
    }
}
