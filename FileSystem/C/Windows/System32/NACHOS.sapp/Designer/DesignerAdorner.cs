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
        ConsumesInput = true;
        
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
        // The adorner itself should cover the target plus handles
        Position = targetBounds.Location.ToVector2() - new Vector2(HandleSize);
        Size = targetBounds.Size.ToVector2() + new Vector2(HandleSize * 2);
        
        foreach (var handle in _handles) {
            handle.Bounds = CalculateHandleRect(handle.Position, targetBounds);
        }
    }
    
    private Rectangle CalculateHandleRect(HandlePosition pos, Rectangle target) {
        int half = HandleSize / 2;
        int x = 0, y = 0;
        
        switch (pos) {
            case HandlePosition.TopLeft: x = target.Left; y = target.Top; break;
            case HandlePosition.Top: x = target.Center.X; y = target.Top; break;
            case HandlePosition.TopRight: x = target.Right; y = target.Top; break;
            case HandlePosition.Left: x = target.Left; y = target.Center.Y; break;
            case HandlePosition.Right: x = target.Right; y = target.Center.Y; break;
            case HandlePosition.BottomLeft: x = target.Left; y = target.Bottom; break;
            case HandlePosition.Bottom: x = target.Center.X; y = target.Bottom; break;
            case HandlePosition.BottomRight: x = target.Right; y = target.Bottom; break;
        }
        
        return new Rectangle(x - half, y - half, HandleSize, HandleSize);
    }
    
    public ResizeHandle GetHandleAt(Vector2 mousePos) {
        foreach (var handle in _handles) {
            if (handle.ContainsPoint(mousePos)) return handle;
        }
        return null;
    }
    
    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        if (!IsSelected) return;
        
        var targetBounds = Target.Bounds;
        var color = new Color(0, 120, 215); // Designer Blue
        
        // Draw selection border
        batch.BorderRectangle(targetBounds.Location.ToVector2(), targetBounds.Size.ToVector2(), color, 1f);
        
        // Draw handles
        foreach (var handle in _handles) {
            handle.Draw(batch, Color.White);
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
