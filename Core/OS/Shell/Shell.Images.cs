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
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static Texture2D LoadAppImage(string fileName) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
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
    }
}
