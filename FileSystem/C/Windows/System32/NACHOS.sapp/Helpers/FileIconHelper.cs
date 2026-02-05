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

        if (Directory.Exists(hostIconDir)) {
            LoadIcon(gd, hostIconDir, "audio.png", ".wav", ".mp3", ".ogg");
            LoadIcon(gd, hostIconDir, "c_sharp.png", ".cs");
            LoadIcon(gd, hostIconDir, "image.png", ".png", ".jpg", ".jpeg", ".bmp", ".gif");
            LoadIcon(gd, hostIconDir, "json.png", ".json");
        }
    }

    private static void LoadIcon(GraphicsDevice gd, string dir, string filename, params string[] extensions) {
        string path = Path.Combine(dir, filename);
        if (File.Exists(path)) {
            var tex = ImageLoader.Load(gd, path);
            foreach (var ext in extensions) {
                _icons[ext.ToLower()] = tex;
            }
        }
    }

    public static Texture2D GetIcon(string path) {
        if (VirtualFileSystem.Instance.IsDirectory(path)) return GameContent.FolderIcon;
        
        string ext = Path.GetExtension(path).ToLower();
        if (_icons.TryGetValue(ext, out var icon)) return icon;
        
        return GameContent.FileIcon;
    }
}
