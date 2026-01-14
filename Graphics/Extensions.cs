using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Graphics; 

public static class Extensions {
    public static VertexShape ToVertexShape(this ref VertexPositionColorTexture item) {
        var position = item.Position;
        var textureCoordinate = item.TextureCoordinate;
        var c = item.Color;

        return new VertexShape(position, textureCoordinate, c);
    }
}