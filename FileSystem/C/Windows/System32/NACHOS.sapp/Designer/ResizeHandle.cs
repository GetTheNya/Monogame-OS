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
    public Rectangle Bounds { get; set; }
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
    
    public bool ContainsPoint(Vector2 point) => Bounds.Contains(point.ToPoint());
    
    public void Draw(ShapeBatch batch, Color color) {
        batch.FillRectangle(Bounds.Location.ToVector2(), Bounds.Size.ToVector2(), color);
        batch.BorderRectangle(Bounds.Location.ToVector2(), Bounds.Size.ToVector2(), Color.White * 0.5f, 1f);
    }
    
    public void ApplyCursor() {
        CustomCursor.Instance.SetCursor(Cursor);
    }
}
