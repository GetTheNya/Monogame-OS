using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace TheGame.Core.Scenes;

public class SceneManager {
    private Scene _currentScene;
    private Scene _nextScene;
    private ContentManager _content;
    private GraphicsDevice _graphicsDevice;

    private float _fadeAlpha = 0f;
    private float _fadeSpeed = 2f;
    private bool _isTransitioning = false;

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
        _fadeAlpha = 0f; // Reset fade on instant load
    }

    public void TransitionTo(Scene scene) {
        if (_isTransitioning) return;
        _nextScene = scene;
        _isTransitioning = true;
    }

    public void Update(GameTime gameTime) {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        if (_isTransitioning) {
            if (_nextScene != null) {
                // Fading out
                _fadeAlpha += _fadeSpeed * dt;
                if (_fadeAlpha >= 1f) {
                    _fadeAlpha = 1f;
                    
                    // Switch scenes
                    if (_currentScene != null) _currentScene.UnloadContent();
                    _currentScene = _nextScene;
                    _nextScene = null;
                    _currentScene.SceneManager = this;
                    _currentScene.LoadContent(_content);
                }
            } else {
                // Fading in
                _fadeAlpha -= _fadeSpeed * dt;
                if (_fadeAlpha <= 0f) {
                    _fadeAlpha = 0f;
                    _isTransitioning = false;
                }
            }
        }

        _currentScene?.Update(gameTime);
    }

    public void Draw(SpriteBatch spriteBatch, Graphics.ShapeBatch shapeBatch) {
        _currentScene?.Draw(spriteBatch, shapeBatch);

        if (_fadeAlpha > 0) {
            // Draw fade overlay
            shapeBatch.Begin();
            var viewport = _graphicsDevice.Viewport;
            shapeBatch.FillRectangle(Vector2.Zero, new Vector2(viewport.Width, viewport.Height), Color.Black * _fadeAlpha);
            shapeBatch.End();
        }
    }
}
