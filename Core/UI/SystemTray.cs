using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Graphics;
using TheGame.Core.OS;
using TheGame.Core.Input;
using TheGame.Core.UI.Controls;
using FontStashSharp;

namespace TheGame.Core.UI;

public class SystemTray : Panel {
    private string _currentTime = "";
    private float _updateTimer = 0f;
    public float DesiredWidth { get; private set; } = 150f;

    private Button _notificationButton;

    public SystemTray(Vector2 position, Vector2 size) : base(position, size) {
        BackgroundColor = Color.Transparent;
        BorderColor = Color.Transparent;
        
        // Create notification button
        _notificationButton = new Button(new Vector2(size.X - 35, 4), new Vector2(30, size.Y - 8), "🔔") {
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(60, 60, 60)
        };
        AddChild(_notificationButton);
        
        UpdateTime();
    }

    public Action OnNotificationClick {
        get => _notificationButton.OnClickAction;
        set => _notificationButton.OnClickAction = value;
    }

    public override void Update(GameTime gameTime) {
        _updateTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_updateTimer >= 1f) {
            UpdateTime();
            _updateTimer = 0f;
        }
        
        // Update button position based on current size
        _notificationButton.Position = new Vector2(Size.X - 35, 4);
        _notificationButton.Size = new Vector2(30, Size.Y - 8);
        
        base.Update(gameTime);
    }

    private void UpdateTime() {
        _currentTime = DateTime.Now.ToString("HH:mm");
        
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(16);
            float timeWidth = font.MeasureString(_currentTime).X;
            float iconSpace = (16f * 2) + (8f * 1);
            float notifBtnWidth = 40f;
            DesiredWidth = timeWidth + iconSpace + notifBtnWidth + 30f;
        }
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, ShapeBatch batch) {
        base.DrawSelf(spriteBatch, batch);
        
        var absPos = AbsolutePosition;
        
        // Draw Status Icons (left side)
        float iconSize = 16f;
        float spacing = 8f;
        float x = absPos.X + 8f;
        float iconY = absPos.Y + (Size.Y - iconSize) / 2f;
        
        // Volume
        batch.FillRectangle(new Vector2(x, iconY), new Vector2(iconSize, iconSize), Color.Gray * 0.8f, rounded: 2f);
        x += iconSize + spacing;
        // Network
        batch.FillRectangle(new Vector2(x, iconY), new Vector2(iconSize, iconSize), Color.LightBlue * 0.8f, rounded: 2f);
        x += iconSize + spacing + 5f;

        // Draw Clock
        if (GameContent.FontSystem != null) {
            var font = GameContent.FontSystem.GetFont(16);
            var timeSize = font.MeasureString(_currentTime);
            Vector2 timePos = new Vector2(x, absPos.Y + (Size.Y - timeSize.Y) / 2f);
            font.DrawText(batch, _currentTime, timePos, Color.White);
        }

        // Draw unread badge on notification button
        int unread = NotificationManager.Instance.UnreadCount;
        if (unread > 0 && GameContent.FontSystem != null) {
            var btnPos = _notificationButton.AbsolutePosition;
            var badgeFont = GameContent.FontSystem.GetFont(9);
            string badgeText = unread > 9 ? "9+" : unread.ToString();
            Vector2 badgePos = btnPos + new Vector2(_notificationButton.Size.X - 8, 0);
            batch.FillCircle(badgePos + new Vector2(5, 5), 8f, Color.Red);
            badgeFont.DrawText(batch, badgeText, badgePos + new Vector2(unread > 9 ? 1 : 3, 1), Color.White);
        }
    }
}
