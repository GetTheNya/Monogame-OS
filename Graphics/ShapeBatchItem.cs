using System;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Graphics;

public class ShapeBatchItem {
    public Texture2D Texture2D;
    
    public VertexShape vertexTL;
    public VertexShape vertexTR;
    public VertexShape vertexBL;
    public VertexShape vertexBR;

    public ShapeBatchItem() {
        vertexTL = new VertexShape();
        vertexTR = new VertexShape();
        vertexBL = new VertexShape();
        vertexBR = new VertexShape();
    }

    public void Set(VertexShape TL, VertexShape TR, VertexShape BR, VertexShape BL, Texture2D texture = null) {
        vertexTL = TL;
        vertexTR = TR;
        vertexBR = BR;
        vertexBL = BL;
        Texture2D = texture;
    }
}