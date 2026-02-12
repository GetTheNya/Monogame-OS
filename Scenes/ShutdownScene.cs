using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using TheGame.Core;
using TheGame.Core.UI.Controls;

namespace TheGame.Scenes;

public enum ShutdownMode {
    Shutdown,
    Restart,
    Update
}

public class ShutdownScene : Core.Scenes.Scene {
    private readonly ShutdownMode _mode;
    private readonly LoadingSpinner _spinner;
    private float _timer = 0f;
    private const float TransitionDelay = 2.0f;

    public ShutdownScene(ShutdownMode mode) {
        _mode = mode;
        _spinner = new LoadingSpinner(Vector2.Zero, new Vector2(60, 60)) {
            Color = Color.White,
            Thickness = 4
        };
    }

    public override void LoadContent(ContentManager content) {
        var viewport = G.GraphicsDevice.Viewport;
        _spinner.Position = new Vector2(viewport.Width / 2 - 30, viewport.Height / 2 + 50);
    }

    public override void UnloadContent() { }

    public override void Update(GameTime gameTime) {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _timer += dt;
        _spinner.Update(gameTime);

        if (_timer >= TransitionDelay) {
            if (_mode == ShutdownMode.Restart) {
                SceneManager.TransitionTo(new LoadingScene());
            } else {
                System.Environment.Exit(0);
            }
        }
    }

    public override void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        var viewport = G.GraphicsDevice.Viewport;
        var font = GameContent.FontSystem.GetFont(32);
        
        string text = _mode switch {
            ShutdownMode.Restart => "Restarting...",
            ShutdownMode.Update => "Updating...",
            _ => "Shutting down..."
        };
        var textSize = font.MeasureString(text);
        
        spriteBatch.Begin();
        font.DrawText(spriteBatch, text, new Vector2(viewport.Width / 2 - textSize.X / 2, viewport.Height / 2 - 20), Color.White);
        spriteBatch.End();

        _spinner.Draw(spriteBatch, shapeBatch);
    }
}
