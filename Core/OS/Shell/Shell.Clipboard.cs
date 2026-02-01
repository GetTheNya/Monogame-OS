using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public static partial class Shell {
    /// <summary>
    /// System-wide Clipboard API.
    /// Supports text, files, and images with history.
    /// </summary>
    public static class Clipboard {
        /// <summary>
        /// Sets the current clipboard text.
        /// </summary>
        public static void SetText(string text, string appId = null) {
            if (appId == null) appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            ClipboardManager.Instance.SetData(text, ClipboardContentType.Text, appId);
        }

        /// <summary>
        /// Gets the current clipboard text, or null if clipboard is not text.
        /// </summary>
        public static string GetText() {
            return ClipboardManager.Instance.GetData<string>(ClipboardContentType.Text);
        }

        /// <summary>
        /// Sets a list of file paths to the clipboard.
        /// </summary>
        public static void SetFiles(IEnumerable<string> paths) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            ClipboardManager.Instance.SetData(paths.ToList(), ClipboardContentType.FileList, appId);
        }

        /// <summary>
        /// Gets the current clipboard file list, or null if clipboard is not a file list.
        /// </summary>
        public static List<string> GetFiles() {
            return ClipboardManager.Instance.GetData<List<string>>(ClipboardContentType.FileList);
        }

        /// <summary>
        /// Sets an image to the clipboard.
        /// </summary>
        public static void SetImage(Texture2D image) {
            string appId = AppLoader.Instance.GetAppIdFromAssembly(Assembly.GetCallingAssembly());
            ClipboardManager.Instance.SetData(image, ClipboardContentType.Image, appId);
        }

        /// <summary>
        /// Gets the current clipboard image, or null if clipboard is not an image.
        /// </summary>
        public static Texture2D GetImage() {
            return ClipboardManager.Instance.GetData<Texture2D>(ClipboardContentType.Image);
        }

        /// <summary>
        /// Gets the full clipboard history.
        /// </summary>
        public static IReadOnlyList<ClipboardItem> GetHistory() {
            return ClipboardManager.Instance.GetHistory();
        }

        /// <summary>
        /// Clears the clipboard history.
        /// </summary>
        public static void Clear() {
            ClipboardManager.Instance.Clear();
        }

        /// <summary>
        /// Event triggered whenever the clipboard content changes.
        /// </summary>
        public static event Action OnChanged {
            add => ClipboardManager.Instance.OnClipboardChanged += value;
            remove => ClipboardManager.Instance.OnClipboardChanged -= value;
        }
    }
}
