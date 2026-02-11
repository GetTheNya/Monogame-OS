using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core;
using TheGame.Core.OS;
using TheGame.Core.UI;
using TheGame.Core.UI.Controls;

namespace TheGame.Scenes;

public class LoginScene : Core.Scenes.Scene {
    private UIManager _uiManager;
    private Panel _loginPanel;
    private TextInput _passwordInput;
    private Button _loginButton;

    public override void LoadContent(ContentManager content) {
        _uiManager = new UIManager();
        var viewport = G.GraphicsDevice.Viewport;

        float panelWidth = 300;
        float panelHeight = 400;
        _loginPanel = new Panel(new Vector2(viewport.Width / 2 - panelWidth / 2, viewport.Height / 2 - panelHeight / 2), new Vector2(panelWidth, panelHeight)) {
            BackgroundColor = new Color(30, 30, 30, 200),
            BorderColor = new Color(60, 60, 60),
            BorderThickness = 1
        };

        // User Avatar
        var avatar = new Image(new Vector2(panelWidth / 2 - 64, 40), new Vector2(128, 128)) {
            Texture = GameContent.UserIcon
        };
        _loginPanel.AddChild(avatar);

        // Username
        var usernameLabel = new Label(new Vector2(panelWidth / 2 - 30, 180), SystemConfig.DisplayName) {
            TextColor = Color.White,
            FontSize = 24
        };
        // Manual centering if needed, but Label size is auto-calculated based on text
        // Let's adjust position after size is updated in first frame update, or just use a fixed offset
        _loginPanel.AddChild(usernameLabel);

        // Password Input
        _passwordInput = new TextInput(new Vector2(40, 230), new Vector2(panelWidth - 80, 40)) {
            Placeholder = "Password",
        };
        _loginPanel.AddChild(_passwordInput);

        // Login Button
        _loginButton = new Button(new Vector2(40, 290), new Vector2(panelWidth - 80, 45), "Login") {
            BackgroundColor = new Color(0, 120, 215),
            HoverColor = new Color(0, 140, 235),
            OnClickAction = () => {
                SceneManager.TransitionTo(new DesktopScene());
            },
            FontSize = 30,
        };
        _loginPanel.AddChild(_loginButton);

        _uiManager.AddElement(_loginPanel);

        // Shutdown/Restart buttons in bottom right
        float btnSize = 48;
        float padding = 20;

        var powerBtn = new Button(new Vector2(viewport.Width - btnSize - padding, viewport.Height - btnSize - padding), new Vector2(btnSize, btnSize), "") {
            Icon = GameContent.PowerIcon,
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(200, 50, 50, 100),
            OnClickAction = () => Shell.Shutdown(),
            Tooltip = "Shut Down"
        };
        _uiManager.AddElement(powerBtn);

        var restartBtn = new Button(new Vector2(viewport.Width - (btnSize + padding) * 2, viewport.Height - btnSize - padding), new Vector2(btnSize, btnSize), "") {
            Icon = GameContent.RestartIcon,
            BackgroundColor = Color.Transparent,
            HoverColor = new Color(50, 150, 200, 100),
            OnClickAction = () => Shell.Restart(),
            Tooltip = "Restart"
        };
        _uiManager.AddElement(restartBtn);
    }

    public override void UnloadContent() { }

    public override void Update(GameTime gameTime) {
        var viewport = G.GraphicsDevice.Viewport;
        if (viewport.Width != _loginPanel.Parent?.Size.X || viewport.Height != _loginPanel.Parent?.Size.Y) {
            // Handle resize
            _loginPanel.Position = new Vector2(viewport.Width / 2 - _loginPanel.Size.X / 2, viewport.Height / 2 - _loginPanel.Size.Y / 2);
        }

        _uiManager.Update(gameTime);

        // Support Enter key for login
        if (TheGame.Core.Input.InputManager.IsKeyJustPressed(Microsoft.Xna.Framework.Input.Keys.Enter)) {
            _loginButton.OnClickAction?.Invoke();
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        G.GraphicsDevice.Clear(new Color(15, 15, 15)); // Deep dark background
        
        shapeBatch.Begin();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
        
        _uiManager.Draw(spriteBatch, shapeBatch);
        
        shapeBatch.End();
        spriteBatch.End();
    }
}

// Simple Image helper if not exists, otherwise I'll need to check if there is a real Image control
public class Image : UIElement {
    public Texture2D Texture { get; set; }
    public Color Tint { get; set; } = Color.White;

    public Image(Vector2 pos, Vector2 size) {
        Position = pos;
        Size = size;
    }

    protected override void DrawSelf(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        if (Texture != null) {
            spriteBatch.Draw(Texture, Bounds, Tint * Opacity);
        }
    }
}
