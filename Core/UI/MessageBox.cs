using Microsoft.Xna.Framework;
using System;
using TheGame.Core.UI.Controls;

namespace TheGame.Core.UI;

public enum MessageBoxButtons {
    OK,
    YesNo
}

public class MessageBox : Window {
    private string _message;
    private MessageBoxButtons _buttons;
    private Action<bool> _callback;

    public MessageBox(string title, string message, MessageBoxButtons buttons = MessageBoxButtons.OK, Action<bool> callback = null) 
        : base(Vector2.Zero, Vector2.Zero) {
        Title = title;
        _message = message;
        _buttons = buttons;
        _callback = callback;
        
        // Measure text for auto-sizing
        var font = GameContent.FontSystem?.GetFont(18);
        Vector2 textSize = font?.MeasureString(message) ?? new Vector2(300, 20);
        
        // Add padding for buttons and margins
        float width = Math.Max(350, textSize.X + 60);
        float height = Math.Max(150, textSize.Y + 100);
        Size = new Vector2(width, height);

        // Center on screen
        var viewport = TheGame.G.GraphicsDevice.Viewport;
        Position = new Vector2((viewport.Width - Size.X) / 2, (viewport.Height - Size.Y) / 2);
        
        CanResize = false;
        IsModal = true;           // Block input to parent
        ShowInTaskbar = false;    // Don't show modal dialogs in taskbar
        SetupUI();

        // Opening animation: Scale and Fade
        // Start small and transparent
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

        float btnWidth = 80;
        float btnHeight = 30;
        float bottomPadding = 15;

        if (_buttons == MessageBoxButtons.OK) {
            var okBtn = new Button(new Vector2((ClientSize.X - btnWidth) / 2, ClientSize.Y - btnHeight - bottomPadding), new Vector2(btnWidth, btnHeight), "OK") {
                OnClickAction = () => {
                    _callback?.Invoke(true);
                    Close();
                }
            };
            AddChild(okBtn);
        } else if (_buttons == MessageBoxButtons.YesNo) {
            var yesBtn = new Button(new Vector2(ClientSize.X / 2 - btnWidth - 10, ClientSize.Y - btnHeight - bottomPadding), new Vector2(btnWidth, btnHeight), "Yes") {
                OnClickAction = () => {
                    _callback?.Invoke(true);
                    Close();
                }
            };
            var noBtn = new Button(new Vector2(ClientSize.X / 2 + 10, ClientSize.Y - btnHeight - bottomPadding), new Vector2(btnWidth, btnHeight), "No") {
                OnClickAction = () => {
                    _callback?.Invoke(false);
                    Close();
                }
            };
            AddChild(yesBtn);
            AddChild(noBtn);
        }
    }
}
