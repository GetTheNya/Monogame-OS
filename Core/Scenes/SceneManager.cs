using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.Scenes;

public class SceneManager {
    private Scene _currentScene;
    private ContentManager _content;
    private GraphicsDevice _graphicsDevice;

    public Scene CurrentScene => _currentScene;

    public SceneManager(ContentManager content, GraphicsDevice graphicsDevice) {
        _content = content;
        _graphicsDevice = graphicsDevice;
    }

    public void LoadScene(Scene scene) {
        if (_currentScene != null) {
            _currentScene.UnloadContent();
        }

        _currentScene = scene;
        _currentScene.SceneManager = this;
        _currentScene.LoadContent(_content);
    }

    public void Update(GameTime gameTime) {
        _currentScene?.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        _currentScene?.Draw(spriteBatch, shapeBatch);
    }
}
