using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Core.Input;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Designer;
using TheGame.Core.OS.DragDrop;
using TheGame;

namespace NACHOS.Designer;

public class ToolboxPanel : ScrollPanel {
    private float _nextItemY = 10;

    public ToolboxPanel(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = new Color(40, 40, 40);
        Padding = new Vector4(10, 10, 10, 10);
        
        AddToolboxItem("Button", typeof(Button));
        AddToolboxItem("Label", typeof(Label));
        AddToolboxItem("TextInput", typeof(TextInput));
        AddToolboxItem("TextArea", typeof(TextArea));
        AddToolboxItem("Checkbox", typeof(Checkbox));
        AddToolboxItem("Switch", typeof(Switch));
        AddToolboxItem("Slider", typeof(Slider));
        AddToolboxItem("ComboBox", typeof(ComboBox));
        AddToolboxItem("ProgressBar", typeof(ProgressBar));
        AddToolboxItem("Panel", typeof(Panel));
        AddToolboxItem("Color picker", typeof(ColorPicker));
        AddToolboxItem("Loading Spinner", typeof(LoadingSpinner));
        AddToolboxItem("Scroll panel", typeof(ScrollPanel));
    }
    
    private void AddToolboxItem(string name, Type type) {
        var item = new ToolboxItem(name, type) {
            Position = new Vector2(10, _nextItemY)
        };
        AddChild(item);
        _nextItemY += 35; // 30 height + 5 spacing
    }
}

public class ToolboxItem : Button, IDraggable {
    public Type ControlType { get; }
    private Vector2 _dragStartPos;
    
    public ToolboxItem(string name, Type type) : base(Vector2.Zero, new Vector2(150, 30), name) {
        ControlType = type;
        BackgroundColor = new Color(50, 50, 50);
        HoverColor = new Color(70, 70, 70);
        BorderColor = Color.Transparent;
    }
    
    protected override void UpdateInput() {
        if (!IsVisible) return;

        // Threshold-based drag start
        if (IsMouseOver && InputManager.IsMouseButtonJustPressed(MouseButton.Left)) {
            _dragStartPos = InputManager.MousePosition.ToVector2();
        }
        
        if (InputManager.IsMouseButtonDown(MouseButton.Left) && !Shell.Drag.IsActive && _dragStartPos != Vector2.Zero) {
            var currentPos = InputManager.MousePosition.ToVector2();
            if (Vector2.Distance(_dragStartPos, currentPos) > 5) {
                // Begin drag
                Vector2 grabOffset = _dragStartPos - AbsolutePosition;
                Shell.Drag.BeginDraggable(this, AbsolutePosition, grabOffset);
                InputManager.IsMouseConsumed = true;
                _dragStartPos = Vector2.Zero;
                _isPressed = false; // Prevent Button from triggering click
            }
        }
        
        if (InputManager.IsMouseButtonJustReleased(MouseButton.Left)) {
            _dragStartPos = Vector2.Zero;
        }
        
        base.UpdateInput();
    }

    // IDraggable implementation
    public object GetDragData() => new ControlTypeDragData { ControlType = ControlType, DisplayName = Text };
    public Texture2D GetDragIcon() => GameContent.FileIcon;
    public string GetDragLabel() => Text;
    public void OnDragStart(Vector2 grabOffset) {
        Opacity = 0.5f;
    }
    public void OnDragEnd() => Opacity = 1.0f;
    public void OnDragCancel() => Opacity = 1.0f;
    
    public override void Draw(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.Draw(spriteBatch, batch);
    }
}
