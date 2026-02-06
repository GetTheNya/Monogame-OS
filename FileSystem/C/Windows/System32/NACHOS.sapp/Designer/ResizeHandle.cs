using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core;

namespace NACHOS.Designer;

public enum HandlePosition {
    TopLeft, Top, TopRight,
    Left, Right,
    BottomLeft, Bottom, BottomRight
}

public class ResizeHandle {
    public HandlePosition Position { get; }
    public Vector2 LocalPosition { get; set; }
    public float Size { get; set; } = 7f;
    public Vector2 ResizeDirection { get; }
    public CursorType Cursor { get; }
    
    public ResizeHandle(HandlePosition position) {
        Position = position;
        
        switch (position) {
            case HandlePosition.TopLeft:
                ResizeDirection = new Vector2(-1, -1);
                Cursor = CursorType.DiagonalNW;
                break;
            case HandlePosition.TopRight:
                ResizeDirection = new Vector2(1, -1);
                Cursor = CursorType.DiagonalNE;
                break;
            case HandlePosition.BottomLeft:
                ResizeDirection = new Vector2(-1, 1);
                Cursor = CursorType.DiagonalNE;
                break;
            case HandlePosition.BottomRight:
                ResizeDirection = new Vector2(1, 1);
                Cursor = CursorType.DiagonalNW;
                break;
            case HandlePosition.Top:
                ResizeDirection = new Vector2(0, -1);
                Cursor = CursorType.Vertical;
                break;
            case HandlePosition.Bottom:
                ResizeDirection = new Vector2(0, 1);
                Cursor = CursorType.Vertical;
                break;
            case HandlePosition.Left:
                ResizeDirection = new Vector2(-1, 0);
                Cursor = CursorType.Horizontal;
                break;
            case HandlePosition.Right:
                ResizeDirection = new Vector2(1, 0);
                Cursor = CursorType.Horizontal;
                break;
        }
    }
    
    public bool ContainsPoint(Vector2 point, Vector2 parentAbsPos) {
        Vector2 absCenter = parentAbsPos + LocalPosition;
        float half = Size / 2f + 5f; // More generous margin (5px extra)
        return point.X >= absCenter.X - half && point.X <= absCenter.X + half &&
               point.Y >= absCenter.Y - half && point.Y <= absCenter.Y + half;
    }
    
    public void Draw(ShapeBatch batch, Vector2 parentAbsPos, Color color) {
        Vector2 pos = parentAbsPos + LocalPosition - new Vector2(Size / 2f);
        batch.FillRectangle(pos, new Vector2(Size), color);
        batch.BorderRectangle(pos, new Vector2(Size), Color.White * 0.5f, 1f);
    }
    
    public void ApplyCursor() {
        CustomCursor.Instance.SetCursor(Cursor);
    }
}
