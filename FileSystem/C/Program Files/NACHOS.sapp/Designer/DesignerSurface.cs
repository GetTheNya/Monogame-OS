using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.OS.DragDrop;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Designer;
using TheGame.Core.OS.History;

namespace NACHOS.Designer;

public class DesignerSurface : UIElement, IDesignerContext, IDropTarget {
    public UIElement ContentLayer { get; }
    public UIElement AdornerLayer { get; }
    
    public UIElement SelectedElement { get; private set; }
    public DesignerAdorner ActiveAdorner { get; private set; }
    public Assembly UserAssembly { get; set; }
    public CommandHistory History { get; set; }
    
    // Interaction state
    private bool _isDragging;
    private bool _isResizing;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPosition;
    private Vector2 _resizeStartSize;
    private ResizeHandle _activeHandle;
    
    public event System.Action<UIElement> OnSelectionChanged;
    public event System.Action<UIElement> OnElementModified;
    public event System.Func<object, Vector2, bool> OnDropReceived;

    public DesignerSurface(Vector2 position, Vector2 size) : base(position, size) {
        ConsumesInput = true;
        ContentLayer = new Container(Vector2.Zero, size);
        AdornerLayer = new Container(Vector2.Zero, size);
        
        AddChild(ContentLayer);
        AddChild(AdornerLayer);
    }
    
    private class Container : UIElement {
        public Container(Vector2 pos, Vector2 size) : base(pos, size) {
            ConsumesInput = false;
        }

        public override UIElement GetElementAt(Vector2 pos) {
            // Container shouldn't block hits, but it should let children be hit
            if (!IsVisible) return null;
            var snapshot = Children;
            for (int i = snapshot.Count - 1; i >= 0; i--) {
                var found = snapshot[i].GetElementAt(pos);
                if (found != null) return found;
            }
            return null;
        }
    }

    public override void Update(GameTime gameTime) {
        // Automatically calculate our size from content layer's children
        // This makes us grow/shrink so the parent ScrollPanel knows when to show bars
        if (ContentLayer != null && Parent != null) {
            float maxR = 0;
            float maxB = 0;
            foreach (var child in ContentLayer.Children) {
                if (!child.IsVisible) continue;
                maxR = Math.Max(maxR, child.Position.X + child.Size.X);
                maxB = Math.Max(maxB, child.Position.Y + child.Size.Y);
            }
            
            // We should be at least as big as our container viewport to avoid "double snapping"
            var minSize = Parent.Size;
            Size = new Vector2(Math.Max(minSize.X, maxR), Math.Max(minSize.Y, maxB));
            
            // Ensure layers follow surface size
            ContentLayer.Size = Size;
            AdornerLayer.Size = Size;
        }

        base.Update(gameTime);
    }

    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        // Draw layers sequentially
        ContentLayer.Draw(spriteBatch, batch);
        AdornerLayer.Draw(spriteBatch, batch);
    }

    protected override void UpdateInput() {
        if (!DesignMode.IsEnabled) {
            base.UpdateInput();
            return;
        }

        // If ScrollPanel consumed input (e.g. scrollbar drag), don't do designer logic
        if (InputManager.IsMouseConsumed) return;

        var mousePos = InputManager.MousePosition.ToVector2();
        bool isJustPressed = InputManager.IsMouseButtonJustPressed(MouseButton.Left);
        bool isDown = InputManager.IsMouseButtonDown(MouseButton.Left);

        if (_isResizing) {
            HandleResizing(mousePos, isDown);
            return;
        }

        if (_isDragging) {
            HandleDragging(mousePos, isDown);
            return;
        }

        IsMouseOver = UIManager.IsHovered(this);

        // Try selecting or starting interaction
        if (isJustPressed && IsMouseOver && !InputManager.IsMouseConsumed) {
            // 1. Check handles first
            if (ActiveAdorner != null && ActiveAdorner.IsVisible) {
                _activeHandle = ActiveAdorner.GetHandleAt(mousePos);
                if (_activeHandle != null) {
                    DebugLogger.Log($"Designer: Resizing STARTED. Handle: {_activeHandle.Position}, Mouse: {mousePos}");
                    History?.BeginTransaction($"Resize {SelectedElement.GetType().Name}");
                    _isResizing = true;
                    _dragStartMouse = mousePos;
                    _resizeStartSize = SelectedElement.Size;
                    _dragStartPosition = SelectedElement.Position;
                    InputManager.IsMouseConsumed = true;
                    return;
                }
            }

            // 2. Check current selection for dragging body
            if (SelectedElement != null && SelectedElement.Bounds.Contains(mousePos.ToPoint())) {
                // Window is base component - cannot be dragged, but CAN be resized via handles (handled above)
                if (SelectedElement is DesignerWindow) {
                    InputManager.IsMouseConsumed = true;
                } else {
                    History?.BeginTransaction($"Move {SelectedElement.GetType().Name}");
                    _isDragging = true;
                    _dragStartMouse = mousePos;
                    _dragStartPosition = SelectedElement.Position;
                    InputManager.IsMouseConsumed = true;
                    return;
                }
            }

            // 3. New selection - only if we clicked something or background specifically
            var hit = ContentLayer.GetElementAt(mousePos);
            SelectElement(hit);
            if (hit != null) {
                InputManager.IsMouseConsumed = true;
            }
        }

        if (Shell.Drag.IsActive && IsMouseOver) {
            Shell.Drag.CheckDropTarget(this, mousePos);
            if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
                Shell.Drag.TryDropOn(this, mousePos);
            }
        }
    }

    // IDropTarget
    public bool CanAcceptDrop(object dragData) => dragData is ControlTypeDragData;
    public DragDropEffect OnDragOver(object dragData, Vector2 position) => DragDropEffect.Copy;
    public void OnDragLeave() { }
    public bool OnDrop(object dragData, Vector2 position) {
        return OnDropReceived?.Invoke(dragData, position) ?? false;
    }
    public Rectangle GetDropBounds() => Bounds;

    public void SelectElement(UIElement element) {
        if (SelectedElement == element) return;
        
        SelectedElement = element;
        AdornerLayer.ClearChildren();
        ActiveAdorner = null;
        
        if (element != null && element != ContentLayer) {
            ActiveAdorner = new DesignerAdorner(element);
            AdornerLayer.AddChild(ActiveAdorner);
            
            // Ensure adorners update their bounds to match element (including scroll offset)
            ActiveAdorner.Update(new GameTime()); 
            
            element.OnDesignSelect?.Invoke();
        }
        
        OnSelectionChanged?.Invoke(element);
    }

    public void NotifyElementModified(UIElement element) {
        OnElementModified?.Invoke(element);
    }

    private void HandleDragging(Vector2 mousePos, bool isDown) {
        if (!isDown) {
            _isDragging = false;
            DebugLogger.Log($"Designer: Dragging ENDED at {SelectedElement.Position}");
            
            // Revert temporarily to get the initial state for the command
            var finalPos = SelectedElement.Position;
            if (finalPos != _dragStartPosition) {
                SelectedElement.Position = _dragStartPosition;
                History?.AddOrExecute(new SetPropertyCommand(SelectedElement, "Position", finalPos));
                History?.EndTransaction();
            } else {
                History?.EndTransaction(); // Cancel if no move
            }

            OnElementModified?.Invoke(SelectedElement);
            return;
        }

        var delta = mousePos - _dragStartMouse;
        var newPos = _dragStartPosition + delta;

        // Clamp to parent bounds if applicable
        if (SelectedElement.Parent != null) {
            var parentSize = SelectedElement.Parent.Size;
            var childOffset = SelectedElement.Parent.GetChildOffset(SelectedElement);
            var maxPos = parentSize - childOffset - SelectedElement.Size;

            newPos.X = MathHelper.Clamp(newPos.X, 0, Math.Max(0, maxPos.X));
            newPos.Y = MathHelper.Clamp(newPos.Y, 0, Math.Max(0, maxPos.Y));
        }

        SelectedElement.Position = newPos;
        OnElementModified?.Invoke(SelectedElement);
        InputManager.IsMouseConsumed = true;
    }

    private void HandleResizing(Vector2 mousePos, bool isDown) {
        if (!isDown) {
            _isResizing = false;
            _activeHandle = null;
            DebugLogger.Log($"Designer: Resizing ENDED. New Size: {SelectedElement.Size}");

            var finalSize = SelectedElement.Size;
            var finalPos = SelectedElement.Position;
            
            if (finalSize != _resizeStartSize || finalPos != _dragStartPosition) {
                 // Resizing might change both pos and size if dragging top/left
                 SelectedElement.Size = _resizeStartSize;
                 SelectedElement.Position = _dragStartPosition;
                 
                 History?.AddOrExecute(new SetPropertyCommand(SelectedElement, "Size", finalSize));
                 if (finalPos != _dragStartPosition) {
                     History?.AddOrExecute(new SetPropertyCommand(SelectedElement, "Position", finalPos));
                 }
                 History?.EndTransaction();
            } else {
                 History?.EndTransaction();
            }

            OnElementModified?.Invoke(SelectedElement);
            return;
        }

        if (SelectedElement == null) return;

        var delta = mousePos - _dragStartMouse;
        var dir = _activeHandle.ResizeDirection;
        var newSize = _resizeStartSize;
        var newPos = _dragStartPosition;

        // Apply resizing
        if (dir.X == 1) newSize.X = System.Math.Max(10, _resizeStartSize.X + delta.X);
        if (dir.X == -1) {
            float diff = System.Math.Min(_resizeStartSize.X - 10, delta.X);
            newSize.X = _resizeStartSize.X - diff;
            newPos.X = _dragStartPosition.X + diff;
        }
        
        if (dir.Y == 1) newSize.Y = System.Math.Max(10, _resizeStartSize.Y + delta.Y);
        if (dir.Y == -1) {
            float diff = System.Math.Min(_resizeStartSize.Y - 10, delta.Y);
            newSize.Y = _resizeStartSize.Y - diff;
            newPos.Y = _dragStartPosition.Y + diff;
        }

        // Clamp to parent bounds (exclude root DesignerWindow from clamping to surface)
        if (SelectedElement.Parent != null && SelectedElement.Parent != ContentLayer) {
            var parentSize = SelectedElement.Parent.Size;
            var childOffset = SelectedElement.Parent.GetChildOffset(SelectedElement);
            var contentSize = parentSize - childOffset;
            
            // X-axis clamping
            if (newPos.X < 0) {
                float diff = -newPos.X;
                newSize.X -= diff;
                newPos.X = 0;
            }
            if (newPos.X + newSize.X > contentSize.X) {
                newSize.X = contentSize.X - newPos.X;
            }

            // Y-axis clamping
            if (newPos.Y < 0) {
                float diff = -newPos.Y;
                newSize.Y -= diff;
                newPos.Y = 0;
            }
            if (newPos.Y + newSize.Y > contentSize.Y) {
                newSize.Y = contentSize.Y - newPos.Y;
            }
            
            // Re-enforce minimum size after clamping
            newSize.X = Math.Max(10, newSize.X);
            newSize.Y = Math.Max(10, newSize.Y);
        }

        if (SelectedElement.Size != newSize) {
            SelectedElement.Size = newSize;
        }
        if (SelectedElement.Position != newPos) {
            SelectedElement.Position = newPos;
        }
        
        OnElementModified?.Invoke(SelectedElement);
        InputManager.IsMouseConsumed = true;
    }
}

public class ControlTypeDragData {
    public System.Type ControlType { get; set; }
    public string DisplayName { get; set; }
}
