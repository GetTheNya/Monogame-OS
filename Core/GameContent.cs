using System.IO;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame;

public static class GameContent {
    public static FontSystem FontSystem;
    public static Microsoft.Xna.Framework.Graphics.Texture2D FolderIcon;
    public static Microsoft.Xna.Framework.Graphics.Texture2D FileIcon;
    public static Microsoft.Xna.Framework.Graphics.Texture2D ExplorerIcon;
    public static Microsoft.Xna.Framework.Graphics.Texture2D TrashEmptyIcon;
    public static Microsoft.Xna.Framework.Graphics.Texture2D TrashFullIcon;
    public static Microsoft.Xna.Framework.Graphics.Texture2D Pixel;

    public static void InitContent() {
        FontSystem = new FontSystem();

        // Fonts still in Assets for now as they are loaded early
        if (File.Exists(@"Assets/Fonts/JetMono.ttf"))
            FontSystem.AddFont(File.ReadAllBytes(@"Assets/Fonts/JetMono.ttf"));

        Pixel = new Texture2D(G.GraphicsDevice, 1, 1);
        Pixel.SetData(new Color[] { Color.White });

        // Load icons from FileSystem
        FolderIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\folder.png");
        FileIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\file.png");
        ExplorerIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\folder.png"); // Default explorer icon
        TrashEmptyIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\trash_can.png");
        TrashFullIcon = LoadIcon("C:\\Windows\\SystemResources\\Icons\\trash_can_full.png");
    }

    private static Microsoft.Xna.Framework.Graphics.Texture2D LoadIcon(string virtualPath) {
        string hostPath = Core.OS.VirtualFileSystem.Instance.ToHostPath(virtualPath);
        if (File.Exists(hostPath)) {
            return Core.ImageLoader.Load(G.GraphicsDevice, hostPath);
        }
        return null;
    }
}
