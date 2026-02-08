using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using FontStashSharp;

namespace TheGame;

public static class GameContent {
    public static FontSystem FontSystem;
    public static FontSystem BoldFontSystem;
    public static Texture2D FolderIcon;
    public static Texture2D FileIcon;
    public static Texture2D ExplorerIcon;
    public static Texture2D TrashEmptyIcon;
    public static Texture2D TrashFullIcon;
    public static Texture2D PCIcon;
    public static Texture2D DiskIcon;
    public static Texture2D DesktopIcon;
    public static Texture2D NotificationIcon;
    public static Texture2D UserIcon;
    public static Texture2D RestartIcon;
    public static Texture2D PowerIcon;
    public static Texture2D CheckboxIcon;
    public static Texture2D CheckboxCheckedIcon;
    public static Texture2D[] VolumeIcons; // 0: mute, 1: volume0, 2: volume1, 3: volume med, 4: volume high
    public static Texture2D Pixel;

    public static void InitContent() {
        FontSystem = new FontSystem();

        if (File.Exists(@"Assets/Fonts/JetMono.ttf"))
            FontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/JetMono.ttf"));
            
        BoldFontSystem = new FontSystem();
        if (File.Exists(@"Assets/Fonts/JetMono-Bold.ttf"))
            BoldFontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/JetMono-Bold.ttf"));
        else if (File.Exists(@"Assets/Fonts/JetMono.ttf"))
            BoldFontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/JetMono.ttf")); // Fallback

        if (File.Exists(@"Assets/Fonts/fa-solid-900.ttf")) {
            FontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/fa-solid-900.ttf"));
            BoldFontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/fa-solid-900.ttf"));
        }


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
        NotificationIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\notification.png");
        UserIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\user.png");
        RestartIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\restart.png");
        PowerIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\power.png");
        CheckboxIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\UI\\checkbox.png");
        CheckboxCheckedIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\UI\\checkbox_checked.png");

        VolumeIcons = new Texture2D[5];
        VolumeIcons[0] = LoadIcon("C:\\Windows\\SystemResources\\Icons\\Volume\\mute.png");
        VolumeIcons[1] = LoadIcon("C:\\Windows\\SystemResources\\Icons\\Volume\\volume0.png");
        VolumeIcons[2] = LoadIcon("C:\\Windows\\SystemResources\\Icons\\Volume\\volume1.png");
        VolumeIcons[3] = LoadIcon("C:\\Windows\\SystemResources\\Icons\\Volume\\volume2.png");
        VolumeIcons[4] = LoadIcon("C:\\Windows\\SystemResources\\Icons\\Volume\\volume3.png");
    }

    private static Texture2D LoadIcon(string virtualPath) {
        string hostPath = Core.OS.VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (File.Exists(hostPath)) {
            try { return Core.ImageLoader.Load(G.GraphicsDevice, hostPath); } catch { }
        }
        return null;
    }
}
