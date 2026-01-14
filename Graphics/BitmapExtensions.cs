using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Graphics; 

public static class BitmapExtensions {
    public static Texture2D ToTexture2D(this Bitmap b, GraphicsDevice g) {
        int bufferSize = b.Height * b.Width * 4;
        var stream = new MemoryStream(bufferSize);
        stream.Seek(0, SeekOrigin.Begin);
        b.Save(stream, ImageFormat.Png);
        return Texture2D.FromStream(g, stream);
    }
}
