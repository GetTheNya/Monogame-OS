using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.OS;

public class ClipboardAPI : BaseAPI {
    public ClipboardAPI(Process process) : base(process) {
    }

    /// <summary> Sets the current clipboard text. </summary>
    public void SetText(string text) => Shell.Clipboard.SetText(text, AppId);

    /// <summary> Gets the current clipboard text. </summary>
    public string GetText() => Shell.Clipboard.GetText();
    
    /// <summary> Sets a list of file paths to the clipboard. </summary>
    public void SetFiles(IEnumerable<string> paths) => Shell.Clipboard.SetFiles(paths);

    /// <summary> Gets the current list of files from the clipboard. </summary>
    public List<string> GetFiles() => Shell.Clipboard.GetFiles();

    /// <summary> Sets an image to the clipboard. </summary>
    public void SetImage(Texture2D image) => Shell.Clipboard.SetImage(image);

    /// <summary> Gets the current image from the clipboard. </summary>
    public Texture2D GetImage() => Shell.Clipboard.GetImage();

    /// <summary> Clears the clipboard history. </summary>
    public void Clear() => Shell.Clipboard.Clear();
}
