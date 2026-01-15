using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using FontStashSharp;

namespace TheGame;

public static class GameContent {
    public static FontSystem FontSystem;
    public static Texture2D FolderIcon;
    public static Texture2D FileIcon;
    public static Texture2D ExplorerIcon;
    public static Texture2D TrashEmptyIcon;
    public static Texture2D TrashFullIcon;
    public static Texture2D PCIcon;
    public static Texture2D DiskIcon;
    public static Texture2D DesktopIcon;
    public static Texture2D Pixel;

    public static void InitContent() {
        FontSystem = new FontSystem();

        if (File.Exists(@"Assets/Fonts/JetMono.ttf"))
            FontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/JetMono.ttf"));

        Pixel = new Texture2D(G.GraphicsDevice, 1, 1);
        Pixel.SetData(new Color[] { Color.White });

        // Load icons from FileSystem
        FolderIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\folder.png");
        FileIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\file.png");
        ExplorerIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\folder.png");
        TrashEmptyIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\trash_can.png");
        TrashFullIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\trash_can_full.png");
        PCIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\PC.png");
        DiskIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\disk.png"); // Assuming disk.png exists or falls back
        DesktopIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\desktop.png");
    }

    private static Texture2D LoadIcon(string virtualPath) {
        string hostPath = Core.OS.VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (File.Exists(hostPath)) {
            try { return Core.ImageLoader.Load(G.GraphicsDevice, hostPath); } catch { }
        }
        return null;
    }
}
