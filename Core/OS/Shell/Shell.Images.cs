using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// Image and texture management.
    /// </summary>
    public static class Images {
        /// <summary>
        /// Loads a texture from an app-relative path (inside the .sapp folder).
        /// Automatically detects the calling application.
        /// </summary>
        public static Texture2D LoadAppImage(TheGame.Core.OS.Process process, string fileName) {
            string appId = process.AppId;
            if (appId == null) return null;
            string virtualPath = VirtualFileSystem.Instance.GetAppResourcePath(appId, fileName);
            if (virtualPath == null) return null;
            return ImageLoader.Load(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(virtualPath));
        }

        /// <summary>
        /// Loads a texture from a virtual path (e.g. C:\Windows\SystemResources\Icons\PC.png).
        /// </summary>
        public static Texture2D Load(string virtualPath) {
            if (string.IsNullOrEmpty(virtualPath)) return null;
            return ImageLoader.Load(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(virtualPath));
        }

        /// <summary>
        /// Asynchronously loads a texture from a virtual path.
        /// </summary>
        public static System.Threading.Tasks.Task<Texture2D> LoadAsync(string virtualPath) {
            if (string.IsNullOrEmpty(virtualPath)) return System.Threading.Tasks.Task.FromResult<Texture2D>(null);
            return ImageLoader.LoadAsync(G.GraphicsDevice, VirtualFileSystem.Instance.ToHostPath(virtualPath));
        }

        /// <summary>
        /// Unloads a texture from memory by virtual path (e.g. C:\Windows\SystemResources\Icons\PC.png).
        /// </summary>
        public static void Unload(string virtualPath) {
            if (string.IsNullOrEmpty(virtualPath)) return;
            ImageLoader.Unload(VirtualFileSystem.Instance.ToHostPath(virtualPath));
        }
    }
}
