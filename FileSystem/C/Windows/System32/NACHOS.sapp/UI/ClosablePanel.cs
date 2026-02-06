using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;
using TheGame.Graphics;

namespace NACHOS;

public class ClosablePanel : UIControl {
    private Label _titleLabel;
    private Button _closeButton;
    private UIElement _content;
    private float _headerHeight = 25;

    public Action OnClose;

    public ClosablePanel(Vector2 position, Vector2 size, string title) : base(position, size) {
        BackgroundColor = new Color(35, 35, 35);
        
        _titleLabel = new Label(new Vector2(10, 5), title) {
            FontSize = 14,
            TextColor = Color.LightGray
        };
        AddChild(_titleLabel);

        _closeButton = new Button(new Vector2(size.X - 25, 2), new Vector2(20, 20), "x") {
            FontSize = 16,
            BackgroundColor = Color.Transparent,
            BorderColor = Color.Transparent,
            HoverColor = Color.Red * 0.5f,
            TextColor = Color.LightGray
        };
        _closeButton.OnClickAction = () => OnClose?.Invoke();
        AddChild(_closeButton);
    }

    public void SetContent(UIElement content) {
        if (_content != null) RemoveChild(_content);
        _content = content;
        if (_content != null) {
            _content.Position = new Vector2(0, _headerHeight);
            _content.Size = new Vector2(Size.X, Size.Y - _headerHeight);
            AddChild(_content);
        }
    }

    public override void Update(GameTime gameTime) {
        base.Update(gameTime);
        if (_content != null) {
            _content.Size = new Vector2(Size.X, Size.Y - _headerHeight);
        }
        _titleLabel.Size = new Vector2(Size.X - 40, _headerHeight);
        _closeButton.Position = new Vector2(Size.X - 25, 2);
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        var absPos = AbsolutePosition;
        // Header background
        batch.FillRectangle(absPos, new Vector2(Size.X, _headerHeight), new Color(45, 45, 45) * AbsoluteOpacity);
        // Border
        batch.BorderRectangle(absPos, Size, Color.Black * 0.5f * AbsoluteOpacity, 1f);
    }
}
