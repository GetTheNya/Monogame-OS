using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.OS.DragDrop;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Designer;

namespace NACHOS.Designer;

public class DesignerSurface : UIElement, IDropTarget, IDesignerContext {
    public UIElement ContentLayer { get; }
    public UIElement AdornerLayer { get; }
    
    public UIElement SelectedElement { get; private set; }
    public DesignerAdorner ActiveAdorner { get; private set; }
    
    // Interaction state
    private bool _isDragging;
    private bool _isResizing;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPosition;
    private Vector2 _resizeStartSize;
    private ResizeHandle _activeHandle;
    
    public event System.Action<UIElement> OnSelectionChanged;
    public event System.Action<UIElement> OnElementModified;

    public DesignerSurface(Vector2 position, Vector2 size) : base(position, size) {
        ContentLayer = new Container(Vector2.Zero, size);
        AdornerLayer = new Container(Vector2.Zero, size);
        
        AddChild(ContentLayer);
        AddChild(AdornerLayer);
    }
    
    private class Container : UIElement {
        public Container(Vector2 pos, Vector2 size) : base(pos, size) {
            ConsumesInput = false;
        }
    }

    public override void Update(GameTime gameTime) {
        if (!DesignMode.IsEnabled) {
            base.Update(gameTime);
            return;
        }
        
        base.Update(gameTime);

        // Visual feedback during drag
        if (Shell.Drag.IsActive && Bounds.Contains(InputManager.MousePosition)) {
            Shell.Drag.CheckDropTarget(this, InputManager.MousePosition.ToVector2());
        }
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

        // Try selecting or starting interaction
        if (isJustPressed) {
            // 1. Check handles first
            if (ActiveAdorner != null && ActiveAdorner.IsVisible) {
                _activeHandle = ActiveAdorner.GetHandleAt(mousePos);
                if (_activeHandle != null) {
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
                // Window is base component - cannot be dragged
                if (SelectedElement is Window) {
                    // Start selection but block dragging
                    InputManager.IsMouseConsumed = true;
                    return;
                }

                _isDragging = true;
                _dragStartMouse = mousePos;
                _dragStartPosition = SelectedElement.Position;
                InputManager.IsMouseConsumed = true;
                return;
            }

            // 3. New selection
            var hit = ContentLayer.GetElementAt(mousePos);
            SelectElement(hit);
            if (hit != null) {
                InputManager.IsMouseConsumed = true;
            }
        }

        // Handle dropping
        if (Shell.Drag.IsActive && InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            if (Bounds.Contains(InputManager.MousePosition)) {
                Shell.Drag.TryDropOn(this, InputManager.MousePosition.ToVector2());
            }
        }
    }

    public void SelectElement(UIElement element) {
        if (SelectedElement == element) return;
        
        SelectedElement = element;
        AdornerLayer.ClearChildren();
        ActiveAdorner = null;
        
        if (element != null && element != ContentLayer) {
            ActiveAdorner = new DesignerAdorner(element);
            AdornerLayer.AddChild(ActiveAdorner);
            element.OnDesignSelect?.Invoke();
        }
        
        OnSelectionChanged?.Invoke(element);
    }

    private void HandleDragging(Vector2 mousePos, bool isDown) {
        if (!isDown) {
            _isDragging = false;
            OnElementModified?.Invoke(SelectedElement);
            return;
        }

        var delta = mousePos - _dragStartMouse;
        SelectedElement.Position = _dragStartPosition + delta;
        InputManager.IsMouseConsumed = true;
    }

    private void HandleResizing(Vector2 mousePos, bool isDown) {
        if (!isDown) {
            _isResizing = false;
            _activeHandle = null;
            OnElementModified?.Invoke(SelectedElement);
            return;
        }

        var delta = mousePos - _dragStartMouse;
        var dir = _activeHandle.ResizeDirection;
        var newSize = _resizeStartSize;
        var newPos = _dragStartPosition;

        if (dir.X == 1) newSize.X = System.Math.Max(10, _resizeStartSize.X + delta.X);
        if (dir.X == -1) {
            float oldX = newSize.X;
            newSize.X = System.Math.Max(10, _resizeStartSize.X - delta.X);
            newPos.X = _dragStartPosition.X + (oldX - newSize.X);
        }
        
        if (dir.Y == 1) newSize.Y = System.Math.Max(10, _resizeStartSize.Y + delta.Y);
        if (dir.Y == -1) {
            float oldY = newSize.Y;
            newSize.Y = System.Math.Max(10, _resizeStartSize.Y - delta.Y);
            newPos.Y = _dragStartPosition.Y + (oldY - newSize.Y);
        }

        SelectedElement.Size = newSize;
        SelectedElement.Position = newPos;
        InputManager.IsMouseConsumed = true;
    }

    // IDropTarget implementation
    public bool CanAcceptDrop(object data) {
        if (data is ControlTypeDragData) return true;
        if (data is IDraggable draggable) {
            return draggable.GetDragData() is ControlTypeDragData;
        }
        return false;
    }

    public DragDropEffect OnDragOver(object data, Vector2 position) {
        if (CanAcceptDrop(data)) {
            TheGame.Core.OS.Shell.Drag.SetDropPreview("toolbox_preview", position);
            return DragDropEffect.Copy;
        }
        return DragDropEffect.None;
    }

    public void OnDragLeave() {
        TheGame.Core.OS.Shell.Drag.SetDropPreview("toolbox_preview", null);
    }

    public Rectangle GetDropBounds() => Bounds;

    public bool OnDrop(object data, Vector2 dropPosition) {
        object actualData = data;
        if (data is IDraggable draggable) {
            actualData = draggable.GetDragData();
        }

        if (actualData is ControlTypeDragData dragData) {
            // Window specialization: Only one allowed as root
            if (dragData.ControlType == typeof(Window)) {
                if (ContentLayer.Children.Any(c => c is Window)) {
                    Shell.Notifications.Show("Designer", "Only one Window allowed as the base component.");
                    return false;
                }
            }

            var instance = System.Activator.CreateInstance(dragData.ControlType) as UIElement;
            if (instance != null) {
                // Find container to nest in
                UIElement targetContainer = ContentLayer.GetElementAt(dropPosition);
                // We want the most nested Container (Window or Panel)
                while (targetContainer != null && !(targetContainer is Window || targetContainer is Panel)) {
                    targetContainer = targetContainer.Parent;
                }
                
                // Fallback to ContentLayer if no container found
                if (targetContainer == null) targetContainer = ContentLayer;

                // Adjust position to local coordinates of target container
                instance.Position = dropPosition - targetContainer.AbsolutePosition;
                
                // Sensible default size
                if (instance.Size == Vector2.Zero) {
                    if (instance is Window) instance.Size = new Vector2(400, 300);
                    else if (instance is Panel) instance.Size = new Vector2(200, 150);
                    else if (instance is Button) instance.Size = new Vector2(100, 30);
                    else if (instance is Label) instance.Size = new Vector2(100, 20);
                    else instance.Size = new Vector2(100, 30);
                }
                
                targetContainer.AddChild(instance);
                SelectElement(instance);
                OnElementModified?.Invoke(instance);
                return true;
            }
        }
        return false;
    }
}

public class ControlTypeDragData {
    public System.Type ControlType { get; set; }
    public string DisplayName { get; set; }
}
