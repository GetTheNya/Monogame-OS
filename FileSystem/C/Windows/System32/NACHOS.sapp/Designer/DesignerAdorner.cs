using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using TheGame.Graphics;
using TheGame.Core.Input;
using TheGame.Core.UI;
using TheGame.Core;

namespace NACHOS.Designer;

public class DesignerAdorner : UIElement {
    public UIElement Target { get; }
    public bool IsSelected { get; set; } = true;
    
    private const int HandleSize = 7;
    private readonly List<ResizeHandle> _handles = new();
    
    public DesignerAdorner(UIElement target) {
        Target = target;
        ConsumesInput = false;
        
        // Initialize handles
        var positions = (HandlePosition[])System.Enum.GetValues(typeof(HandlePosition));
        foreach (var pos in positions) {
            _handles.Add(new ResizeHandle(pos));
        }
        
        UpdateHandleBounds();
    }
    
    public override void Update(GameTime gameTime) {
        if (Target == null || !Target.IsVisible) {
            IsVisible = false;
            return;
        }
        
        IsVisible = true;
        UpdateHandleBounds();
        base.Update(gameTime);
    }
    
    private void UpdateHandleBounds() {
        var targetBounds = Target.Bounds;
        
        // The adorner itself should follow the target
        // Calculate position relative to parent
        Vector2 parentAbsPos = Parent?.AbsolutePosition ?? Vector2.Zero;
        Position = targetBounds.Location.ToVector2() - parentAbsPos - new Vector2(HandleSize);
        Size = targetBounds.Size.ToVector2() + new Vector2(HandleSize * 2);
        
        foreach (var handle in _handles) {
            handle.LocalPosition = CalculateHandleOffset(handle.Position, Target.Size);
        }
    }
    
    private Vector2 CalculateHandleOffset(HandlePosition pos, Vector2 targetSize) {
        // The adorner is sized TargetSize + HandleSize * 2
        // The target starts at local (HandleSize, HandleSize) relative to this adorner's Position
        float tx = HandleSize; 
        float ty = HandleSize;
        float tw = targetSize.X;
        float th = targetSize.Y;

        float hx = 0, hy = 0;
        
        switch (pos) {
            case HandlePosition.TopLeft: hx = tx; hy = ty; break;
            case HandlePosition.Top: hx = tx + tw/2; hy = ty; break;
            case HandlePosition.TopRight: hx = tx + tw; hy = ty; break;
            case HandlePosition.Left: hx = tx; hy = ty + th/2; break;
            case HandlePosition.Right: hx = tx + tw; hy = ty + th/2; break;
            case HandlePosition.BottomLeft: hx = tx; hy = ty + th; break;
            case HandlePosition.Bottom: hx = tx + tw/2; hy = ty + th; break;
            case HandlePosition.BottomRight: hx = tx + tw; hy = ty + th; break;
        }
        
        return new Vector2(hx, hy);
    }
    
    public ResizeHandle GetHandleAt(Vector2 mousePos) {
        var absPos = AbsolutePosition;
        foreach (var handle in _handles) {
            if (handle.ContainsPoint(mousePos, absPos)) return handle;
        }
        return null;
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsSelected) return;
        
        var absPos = AbsolutePosition;
        var color = new Color(0, 120, 215); // Designer Blue
        
        // Draw selection border (Target starts at absPos + HandleSize)
        batch.BorderRectangle(absPos + new Vector2(HandleSize), Target.Size, color, 1f);
        
        // Draw handles
        foreach (var handle in _handles) {
            handle.Draw(batch, absPos, Color.White);
        }
    }
    
    protected override void UpdateInput() {
        // We don't use base UpdateInput because we want custom handle cursor logic
        if (!IsVisible) return;
        
        var mousePos = InputManager.MousePosition.ToVector2();
        var handle = GetHandleAt(mousePos);
        if (handle != null) {
            handle.ApplyCursor();
            if (InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
                // Focus target?
            }
        } else if (Target.Bounds.Contains(mousePos.ToPoint())) {
            CustomCursor.Instance.SetCursor(CursorType.Move);
        }
    }
}
