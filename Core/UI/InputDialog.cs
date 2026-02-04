using Microsoft.Xna.Framework;
using System;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

public class InputDialog : Window {
    private string _message;
    private string _initialValue;
    private Action<string> _callback;
    private TextInput _input;

    public InputDialog(string title, string message, string initialValue = "", Action<string> callback = null) 
        : base(Vector2.Zero, new Vector2(400, 180)) {
        Title = title;
        _message = message;
        _initialValue = initialValue;
        _callback = callback;
        
        // Center on screen
        var viewport = TheGame.G.GraphicsDevice.Viewport;
        Position = new Vector2((viewport.Width - Size.X) / 2, (viewport.Height - Size.Y) / 2);
        
        CanResize = false;
        IsModal = true;
        ShowInTaskbar = false;
        
        SetupUI();

        // Opening animation
        Vector2 finalSize = Size;
        Vector2 finalPos = Position;
        Size = finalSize * 0.8f;
        Position = finalPos + (finalSize - Size) / 2f;
        Opacity = 0f;

        Core.Animation.Tweener.To(this, v => Position = v, Position, finalPos, 0.3f, Core.Animation.Easing.EaseOutBack);
        Core.Animation.Tweener.To(this, v => Size = v, Size, finalSize, 0.3f, Core.Animation.Easing.EaseOutBack);
        Core.Animation.Tweener.To(this, v => Opacity = v, 0f, 1f, 0.2f, Core.Animation.Easing.Linear);
    }

    private void SetupUI() {
        var label = new Label(new Vector2(20, 25), _message) {
            FontSize = 18,
            Color = Color.White
        };
        AddChild(label);

        _input = new TextInput(new Vector2(20, 65), new Vector2(ClientSize.X - 40, 35)) {
            Value = _initialValue,
            OnSubmit = (val) => {
                _callback?.Invoke(val);
                Close();
            }
        };
        AddChild(_input);
        
        // Auto-focus and select all
        UIManager.SetFocus(_input);
        _input.SelectAll();

        float btnWidth = 80;
        float btnHeight = 30;
        float bottomPadding = 15;

        var okBtn = new Button(new Vector2(ClientSize.X / 2 - btnWidth - 10, ClientSize.Y - btnHeight - bottomPadding), new Vector2(btnWidth, btnHeight), "OK") {
            OnClickAction = () => {
                _callback?.Invoke(_input.Value);
                Close();
            }
        };
        var cancelBtn = new Button(new Vector2(ClientSize.X / 2 + 10, ClientSize.Y - btnHeight - bottomPadding), new Vector2(btnWidth, btnHeight), "Cancel") {
            OnClickAction = () => {
                _callback?.Invoke(null);
                Close();
            }
        };
        AddChild(okBtn);
        AddChild(cancelBtn);
    }
}
